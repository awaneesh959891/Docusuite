using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MauiApp.Models;
using SkiaSharp;

namespace MauiApp.Services;

public sealed class DocumentWorkspaceService
{
    private sealed class WorkspaceState
    {
        public Dictionary<string, WorkspaceMeta> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class WorkspaceMeta
    {
        public bool Favorite { get; set; }
        public bool Pinned { get; set; }
        public string Tag { get; set; } = "Other";
        public DateTime? LastOpenedUtc { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] ImageExt = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp"];

    private readonly object _gate = new();
    private readonly IPdfOrganizeService _organize;
    private readonly IPdfSecurityService _security;
    private readonly string _workspaceRoot = Path.Combine(FileSystem.AppDataDirectory, "Workspace");
    private readonly string _recycleRoot = Path.Combine(FileSystem.AppDataDirectory, "WorkspaceRecycleBin");
    private readonly string _thumbRoot = Path.Combine(FileSystem.CacheDirectory, "WorkspaceThumbs");
    private readonly string _statePath = Path.Combine(FileSystem.AppDataDirectory, "workspace-state.json");
    private WorkspaceState? _state;

    public DocumentWorkspaceService(IPdfOrganizeService organize, IPdfSecurityService security)
    {
        _organize = organize;
        _security = security;
    }

    public IReadOnlyList<string> AvailableTags => ["All", "Personal", "Office", "Bills", "IDs", "Other"];
    public string WorkspaceRoot => _workspaceRoot;

    public async Task ImportAsync(IEnumerable<FileResult> picks)
    {
        Directory.CreateDirectory(_workspaceRoot);

        foreach (var file in picks)
        {
            await using var source = await file.OpenReadAsync();
            var ext = Path.GetExtension(file.FileName);
            var name = Path.GetFileNameWithoutExtension(file.FileName);
            var target = UniquePath(_workspaceRoot, name, ext);
            await using var dest = File.Create(target);
            await source.CopyToAsync(dest);

            lock (_gate)
                EnsureMeta(target);
        }

        SaveState();
    }

    public IReadOnlyList<WorkspaceDocument> LoadDocuments()
    {
        Directory.CreateDirectory(_workspaceRoot);
        Directory.CreateDirectory(_thumbRoot);

        lock (_gate)
        {
            var state = LoadState();
            var files = EnumerateWorkspaceFiles().ToList();
            PruneMissing(state, files);

            var docs = new List<WorkspaceDocument>(files.Count);
            foreach (var path in files)
            {
                var meta = EnsureMeta(path);
                var info = new FileInfo(path);
                var encrypted = false;
                var pages = 1;

                if (Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    encrypted = _security.IsEncrypted(path);
                    if (!encrypted)
                    {
                        try
                        {
                            pages = _organize.GetPageCount(path);
                        }
                        catch (InvalidOperationException)
                        {
                            pages = 1;
                        }
                    }
                }

                docs.Add(new WorkspaceDocument
                {
                    FilePath = path,
                    FileName = Path.GetFileName(path),
                    FolderName = ParentFolder(path),
                    ModifiedUtc = info.LastWriteTimeUtc,
                    SizeBytes = info.Length,
                    PageCount = pages,
                    IsEncrypted = encrypted,
                    LastOpenedUtc = meta.LastOpenedUtc,
                    IsFavorite = meta.Favorite,
                    IsPinned = meta.Pinned,
                    Tag = meta.Tag,
                    ThumbnailPath = EnsureThumbnail(path, info.LastWriteTimeUtc, encrypted)
                });
            }

            SaveState();
            return docs;
        }
    }

    public IReadOnlyList<string> Folders()
    {
        Directory.CreateDirectory(_workspaceRoot);
        var dirs = Directory.EnumerateDirectories(_workspaceRoot)
            .Select(Path.GetFileName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .OrderBy(n => n)
            .ToList();
        dirs.Insert(0, "Workspace");
        return dirs;
    }

    public void CreateFolder(string folderName) => Directory.CreateDirectory(FolderPath(folderName));

    public IReadOnlyList<RecycleBinDocument> LoadRecycleBin()
    {
        if (!Directory.Exists(_recycleRoot))
            return [];

        return Directory.EnumerateFiles(_recycleRoot)
            .Where(path => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
                           ImageExt.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Select(info => new RecycleBinDocument(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length))
            .ToList();
    }

    public Task RestoreAsync(string recyclePath)
    {
        EnsureInRecycleBin(recyclePath);
        if (!File.Exists(recyclePath))
            throw new FileNotFoundException("The deleted file is no longer in the recycle bin.", recyclePath);

        var target = UniquePath(_workspaceRoot, Path.GetFileNameWithoutExtension(recyclePath), Path.GetExtension(recyclePath));
        File.Move(recyclePath, target);
        lock (_gate)
        {
            EnsureMeta(target);
            SaveState();
        }
        return Task.CompletedTask;
    }

    public Task DeletePermanentlyAsync(string recyclePath)
    {
        EnsureInRecycleBin(recyclePath);
        if (File.Exists(recyclePath))
            File.Delete(recyclePath);
        return Task.CompletedTask;
    }

    public void SetFavorite(string path, bool value)
    {
        lock (_gate)
        {
            EnsureMeta(path).Favorite = value;
            SaveState();
        }
    }

    public void SetPinned(string path, bool value)
    {
        lock (_gate)
        {
            EnsureMeta(path).Pinned = value;
            SaveState();
        }
    }

    public void SetTag(string path, string tag)
    {
        lock (_gate)
        {
            EnsureMeta(path).Tag = string.IsNullOrWhiteSpace(tag) ? "Other" : tag;
            SaveState();
        }
    }

    public void MarkOpened(string path)
    {
        lock (_gate)
        {
            EnsureMeta(path).LastOpenedUtc = DateTime.UtcNow;
            SaveState();
        }
    }

    public Task RenameAsync(string path, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new InvalidOperationException("File name is required.");

        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(newName.Trim());
        if (stem.Length == 0)
            throw new InvalidOperationException("File name is required.");

        var target = UniquePath(Path.GetDirectoryName(path)!, stem, ext);
        File.Move(path, target);

        lock (_gate)
        {
            var state = LoadState();
            if (state.Files.Remove(path, out var meta))
                state.Files[target] = meta;
            SaveState();
        }

        return Task.CompletedTask;
    }

    public Task DuplicateAsync(string path)
    {
        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path) + "_copy";
        var target = UniquePath(Path.GetDirectoryName(path)!, stem, ext);
        File.Copy(path, target);

        lock (_gate)
        {
            EnsureMeta(target);
            SaveState();
        }

        return Task.CompletedTask;
    }

    public Task MoveAsync(string path, string folderName)
    {
        var targetDir = FolderPath(folderName);
        Directory.CreateDirectory(targetDir);

        var ext = Path.GetExtension(path);
        var stem = Path.GetFileNameWithoutExtension(path);
        var target = UniquePath(targetDir, stem, ext);
        File.Move(path, target);

        lock (_gate)
        {
            var state = LoadState();
            if (state.Files.Remove(path, out var meta))
                state.Files[target] = meta;
            SaveState();
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(IEnumerable<string> paths, bool permanent)
    {
        var list = paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
            return Task.CompletedTask;

        Directory.CreateDirectory(_recycleRoot);

        foreach (var path in list)
        {
            if (permanent)
                File.Delete(path);
            else
            {
                var ext = Path.GetExtension(path);
                var stem = Path.GetFileNameWithoutExtension(path);
                var target = UniquePath(_recycleRoot, stem, ext);
                File.Move(path, target);
            }
        }

        lock (_gate)
        {
            var state = LoadState();
            foreach (var path in list)
                state.Files.Remove(path);
            SaveState();
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<ContinueEditingItem> ContinueEditing(HistoryService history)
    {
        return history.All()
            .SelectMany(h => h.Outputs.Select(o => new ContinueEditingItem(h.ToolTitle, o, h.Timestamp, h.Glyph)))
            .Where(i => File.Exists(i.FilePath))
            .DistinctBy(i => i.FilePath)
            .Take(8)
            .ToList();
    }

    private IEnumerable<string> EnumerateWorkspaceFiles()
    {
        foreach (var file in Directory.EnumerateFiles(_workspaceRoot, "*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase) || ImageExt.Contains(ext, StringComparer.OrdinalIgnoreCase))
                yield return file;
        }

        if (Directory.Exists(HistoryService.OutputDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(HistoryService.OutputDirectory, "*.pdf"))
                if (File.Exists(file))
                    yield return file;
        }
    }

    private static string ParentFolder(string path)
    {
        var parent = Directory.GetParent(path);
        return parent?.Name ?? "Workspace";
    }

    private string FolderPath(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            throw new InvalidOperationException("Folder name is required.");

        var name = folderName.Trim();
        if (name.Equals("Workspace", StringComparison.OrdinalIgnoreCase))
            return _workspaceRoot;
        if (name is "." or ".." || name.Contains('/') || name.Contains('\\') ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Folder name is invalid.");

        var root = Path.GetFullPath(_workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(root, name));
        var prefix = root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!target.StartsWith(prefix, comparison))
            throw new InvalidOperationException("Folder must be inside the document workspace.");
        return target;
    }

    private void EnsureInRecycleBin(string path)
    {
        var root = Path.GetFullPath(_recycleRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path);
        var prefix = root + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(prefix, comparison))
            throw new InvalidOperationException("The selected path is outside the recycle bin.");
    }

    private WorkspaceState LoadState()
    {
        if (_state is not null)
            return _state;

        if (!File.Exists(_statePath))
            return _state = new WorkspaceState();

        try
        {
            _state = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(_statePath)) ?? new WorkspaceState();
        }
        catch (JsonException)
        {
            _state = new WorkspaceState();
        }

        return _state;
    }

    private WorkspaceMeta EnsureMeta(string path)
    {
        var state = LoadState();
        if (!state.Files.TryGetValue(path, out var meta))
        {
            meta = new WorkspaceMeta();
            state.Files[path] = meta;
        }

        if (string.IsNullOrWhiteSpace(meta.Tag))
            meta.Tag = "Other";

        return meta;
    }

    private static void PruneMissing(WorkspaceState state, IEnumerable<string> existing)
    {
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toRemove = state.Files.Keys.Where(path => !existingSet.Contains(path)).ToList();
        foreach (var path in toRemove)
            state.Files.Remove(path);
    }

    private void SaveState()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        File.WriteAllText(_statePath, JsonSerializer.Serialize(LoadState(), JsonOptions));
    }

    private bool TryCreateContentThumbnail(string filePath, string target, bool isEncrypted)
    {
        var extension = Path.GetExtension(filePath);
        if (ImageExt.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            using var source = ImageTools.DecodeOriented(File.ReadAllBytes(filePath));
            using var scaled = ImageTools.Downscale(source.Copy(), 360);
            using var image = SKImage.FromBitmap(scaled);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 88);
            using var output = File.Create(target);
            encoded.SaveTo(output);
            return true;
        }

        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) || isEncrypted)
            return false;

        try
        {
            var thumbnail = PdfPageRenderService.RenderPage(filePath, 0, 480);
            File.WriteAllBytes(target, thumbnail);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private string EnsureThumbnail(string filePath, DateTime modifiedUtc, bool isEncrypted)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{filePath}|{modifiedUtc.Ticks}|{isEncrypted}")));
        var thumb = Path.Combine(_thumbRoot, key + ".png");
        if (File.Exists(thumb))
            return thumb;

        if (TryCreateContentThumbnail(filePath, thumb, isEncrypted))
            return thumb;

        using var bitmap = new SKBitmap(360, 480);
        using var canvas = new SKCanvas(bitmap);
        var ext = Path.GetExtension(filePath).ToUpperInvariant().TrimStart('.');

        using var bg = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(360, 480),
                [new SKColor(59, 130, 246), new SKColor(99, 102, 241)],
                null,
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(new SKRect(0, 0, 360, 480), bg);

        using var panel = new SKPaint { Color = new SKColor(255, 255, 255, 42), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(20, 28, 340, 452), 30, 30), panel);

        using var line = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true, StrokeWidth = 12, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(56, 110, 300, 110, line);
        canvas.DrawLine(56, 160, 280, 160, line);
        canvas.DrawLine(56, 210, 240, 210, line);

        using var chip = new SKPaint { Color = new SKColor(239, 68, 68), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(190, 332, 334, 420), 24, 24), chip);

        using var extBar = new SKPaint { Color = ext == "PDF" ? new SKColor(56, 189, 248) : new SKColor(16, 185, 129), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(34, 332, 166, 420), 24, 24), extBar);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);
        using var stream = File.Create(thumb);
        data.SaveTo(stream);

        return thumb;
    }

    private static string UniquePath(string folder, string stem, string ext)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, stem + ext);
        for (var i = 1; File.Exists(path); i++)
            path = Path.Combine(folder, $"{stem} ({i}){ext}");
        return path;
    }
}
