using CommunityToolkit.Maui.Storage;

namespace MauiApp.Services;

public static class FileActions
{
    // Returns a human-readable outcome. Tries the system save dialog first, then falls back to the Downloads folder.
    public static async Task<string> SaveAsync(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length > 33 && name[32] == '_')
            name = name[33..];

        try
        {
            await using var stream = File.OpenRead(path);
            var result = await FileSaver.Default.SaveAsync(name, stream, CancellationToken.None);
            if (result.IsSuccessful)
                return $"Saved to {result.FilePath}";
            if (result.Exception is null)
                return "Save cancelled.";
        }
        catch (Exception)
        {
            // fall through to the Downloads copy
        }

        try
        {
            var dir = DownloadsDirectory();
            Directory.CreateDirectory(dir);
            var target = UniquePath(dir, name);
            File.Copy(path, target);
            return $"Saved to {target}";
        }
        catch (Exception ex)
        {
            return $"Could not save the file: {ex.Message}";
        }
    }

    public static async Task<string?> OpenAsync(string path)
    {
        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest(Path.GetFileName(path), new ReadOnlyFile(path)));
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not open the file: {ex.Message}";
        }
    }

    public static async Task<string?> ShareAsync(string path)
    {
        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest { Title = Path.GetFileName(path), File = new ShareFile(path) });
            return null;
        }
        catch (Exception ex)
        {
            return $"Could not share the file: {ex.Message}";
        }
    }

    private static string DownloadsDirectory()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloads = Path.Combine(profile, "Downloads");
        return Directory.Exists(downloads) ? downloads : Path.Combine(FileSystem.AppDataDirectory, "Exports");
    }

    private static string UniquePath(string dir, string name)
    {
        var target = Path.Combine(dir, name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var i = 1; File.Exists(target); i++)
            target = Path.Combine(dir, $"{stem} ({i}){ext}");
        return target;
    }
}
