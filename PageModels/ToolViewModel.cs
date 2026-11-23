using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using SkiaSharp;

namespace MauiApp.PageModels;

public partial class ToolViewModel : ObservableObject, IQueryAttributable
{
    private readonly IPdfOrganizeService _organize;
    private readonly IPdfEditService _edit;
    private readonly IPdfConvertService _convert;
    private readonly IPdfOptimizeService _optimize;
    private readonly IPdfRecoveryService _recovery;
    private readonly IPdfOcrService _ocr;
    private readonly IPdfSecurityService _security;
    private readonly IScanService _scan;
    private readonly HistoryService _history;
    private readonly AuthService _auth;
    private readonly FeatureService _features;
    private readonly PdfPresetService _presets;
    private readonly PdfOutputNamingService _outputNaming;

    public ToolViewModel(IPdfOrganizeService organize, IPdfEditService edit, IPdfConvertService convert,
        IPdfOptimizeService optimize, IPdfRecoveryService recovery, IPdfOcrService ocr,
        IPdfSecurityService security, IScanService scan, HistoryService history,
        AuthService auth, FeatureService features, PdfPresetService presets, PdfOutputNamingService outputNaming)
    {
        _auth = auth;
        _features = features;
        _organize = organize;
        _edit = edit;
        _convert = convert;
        _optimize = optimize;
        _recovery = recovery;
        _ocr = ocr;
        _security = security;
        _scan = scan;
        _history = history;
        _presets = presets;
        _outputNaming = outputNaming;
        OutputNamePattern = _outputNaming.Pattern;
    }

    // ---- Picker data ----
    public IReadOnlyList<string> SplitModes { get; } = ["Custom ranges", "Every N pages"];
    public IReadOnlyList<string> RotationOptions { get; } = ["90° clockwise", "180°", "90° counter-clockwise"];
    public IReadOnlyList<string> PageSizes { get; } = ["A4", "Letter", "Fit to image"];
    public IReadOnlyList<string> CompressionLevels { get; } = ["Low – best quality", "Medium – recommended", "High – smallest file"];
    public IReadOnlyList<string> NumberPositions { get; } = ["Bottom center", "Bottom right", "Bottom left", "Top center", "Top right", "Top left"];
    public IReadOnlyList<string> NumberFormats { get; } = ["1", "Page 1", "1 / N", "Page 1 of N"];
    public IReadOnlyList<string> Strengths { get; } = ["AES-256 (recommended)", "AES-128 (wider compatibility)"];
    public IReadOnlyList<string> SignModes { get; } = ["Draw", "Type", "Upload"];
    public IReadOnlyList<string> SignPositions { get; } = ["Bottom right", "Bottom left", "Bottom center", "Center", "Top right", "Top left", "Top center"];
    public IReadOnlyList<string> InkColors { get; } = ["Black", "Blue"];
    public IReadOnlyList<string> ScanFilters { get; } = ["Original", "Enhanced", "Grayscale", "Black & white"];
    private static readonly string[] NumberFormatTokens = ["{n}", "Page {n}", "{n} / {total}", "Page {n} of {total}"];

    public ObservableCollection<PdfDocumentItem> Files { get; } = [];
    public ObservableCollection<PdfDocumentItem> Results { get; } = [];
    public ObservableCollection<string> PresetNames { get; } = [];
    public StrokeCollection Pad { get; } = new();

    // ---- State ----
    [ObservableProperty] private PdfTool? _tool;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _ranges = string.Empty;
    [ObservableProperty] private string? _selectedPresetName;
    [ObservableProperty] private string _outputNamePattern = PdfOutputNamingService.DefaultPattern;

    [ObservableProperty] private int _splitModeIndex;
    [ObservableProperty] private int _pagesPerFile = 1;
    [ObservableProperty] private int _rotationIndex;
    [ObservableProperty] private int _pageSizeIndex;
    [ObservableProperty] private bool _landscape;
    [ObservableProperty] private double _margin = 20;

    [ObservableProperty] private int _compressionIndex = 1;
    [ObservableProperty] private double _cropMarginPercent = 10;

    [ObservableProperty] private int _numberPositionIndex;
    [ObservableProperty] private int _numberFormatIndex;
    [ObservableProperty] private int _startNumber = 1;
    [ObservableProperty] private double _numberFontSize = 12;
    [ObservableProperty] private bool _skipFirstPage;

    [ObservableProperty] private string _watermarkText = "CONFIDENTIAL";
    [ObservableProperty] private double _watermarkOpacity = 25;
    [ObservableProperty] private double _watermarkRotation = 45;
    [ObservableProperty] private double _watermarkFontSize = 64;
    [ObservableProperty] private bool _watermarkTile;
    [ObservableProperty] private string? _logoPath;

    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _ownerPassword = string.Empty;
    [ObservableProperty] private int _strengthIndex;
    [ObservableProperty] private bool _allowPrint = true;
    [ObservableProperty] private bool _allowCopy = true;
    [ObservableProperty] private bool _allowEdit = true;
    [ObservableProperty] private bool _allowAnnotate = true;

    [ObservableProperty] private int _signModeIndex;
    [ObservableProperty] private string _typedSignature = string.Empty;
    [ObservableProperty] private string _signerName = string.Empty;
    [ObservableProperty] private string? _signatureUploadPath;
    [ObservableProperty] private int _inkIndex;
    [ObservableProperty] private string _signPage = string.Empty;
    [ObservableProperty] private int _signPositionIndex;
    [ObservableProperty] private double _signWidth = 30;
    [ObservableProperty] private bool _addTimestamp = true;
    [ObservableProperty] private bool _signAllPages;
    [ObservableProperty] private ImageSource? _signaturePreview;

    [ObservableProperty] private int _scanFilterIndex = 1;
    [ObservableProperty] private bool _autoCrop = true;

    private PdfToolKind Kind => Tool?.Kind ?? PdfToolKind.ComingSoon;

    // ---- Visibility ----
    public bool IsMultiple => Kind is PdfToolKind.Merge or PdfToolKind.ImagesToPdf or PdfToolKind.Scan;
    public bool IsImages => Kind is PdfToolKind.ImagesToPdf or PdfToolKind.Scan;
    public bool IsScan => Kind == PdfToolKind.Scan;
    public bool HasFiles => Files.Count > 0;
    public bool HasResults => Results.Count > 0;
    public bool ShowRanges => Kind switch
    {
        PdfToolKind.Split => SplitModeIndex == 0,
        PdfToolKind.RemovePages or PdfToolKind.ExtractPages or PdfToolKind.Rotate or PdfToolKind.Crop
            or PdfToolKind.PageNumbers or PdfToolKind.Watermark => true,
        _ => false
    };
    public bool ShowSplitOptions => Kind == PdfToolKind.Split;
    public bool ShowPagesPerFile => ShowSplitOptions && SplitModeIndex == 1;
    public bool ShowRotation => Kind == PdfToolKind.Rotate;
    public bool ShowCrop => Kind == PdfToolKind.Crop;
    public bool ShowMetadata => Kind == PdfToolKind.ClearMetadata;
    public bool ShowRepair => Kind == PdfToolKind.Repair;
    public bool ShowOcr => Kind == PdfToolKind.Ocr;
    public bool ShowImageOptions => Kind == PdfToolKind.ImagesToPdf || IsScan;
    public bool ShowMargin => ShowImageOptions && PageSizeIndex != 2;
    public bool ShowLandscape => Kind == PdfToolKind.ImagesToPdf && PageSizeIndex != 2;
    public bool ShowCompress => Kind == PdfToolKind.Compress;
    public bool ShowPageNumbers => Kind == PdfToolKind.PageNumbers;
    public bool ShowWatermark => Kind == PdfToolKind.Watermark;
    public bool ShowProtect => Kind == PdfToolKind.Protect;
    public bool ShowUnlock => Kind == PdfToolKind.Unlock;
    public bool ShowSign => Kind == PdfToolKind.Sign;
    public bool ShowDrawMode => ShowSign && SignModeIndex == 0;
    public bool ShowTypeMode => ShowSign && SignModeIndex == 1;
    public bool ShowUploadMode => ShowSign && SignModeIndex == 2;
    public bool HasSignaturePreview => SignaturePreview is not null;
    public bool HasLogo => !string.IsNullOrEmpty(LogoPath);
    public string LogoLabel => HasLogo ? Path.GetFileName(LogoPath!)[33..] : "No logo selected";
    public string UploadLabel => string.IsNullOrEmpty(SignatureUploadPath) ? "No image selected" : "Image selected ✓";
    public bool ShowNoOptions => Kind is PdfToolKind.Merge or PdfToolKind.Copy;
    public bool ShowOptionsCard => HasFiles && (!ShowNoOptions);

    public string RangesLabel => Kind switch
    {
        PdfToolKind.Split => "Page ranges (each comma-separated range becomes a file)",
        PdfToolKind.RemovePages => "Pages to remove",
        PdfToolKind.ExtractPages => "Pages to extract",
        PdfToolKind.Rotate => "Pages to rotate (leave empty for all)",
        PdfToolKind.Crop => "Pages to crop (leave empty for all)",
        PdfToolKind.PageNumbers => "Pages to number (leave empty for all)",
        PdfToolKind.Watermark => "Pages to watermark (leave empty for all)",
        _ => "Pages"
    };

    public string PickLabel => Kind switch
    {
        PdfToolKind.ImagesToPdf => "Add images",
        PdfToolKind.Scan => "Choose photos from gallery",
        PdfToolKind.Merge => "Add PDF files",
        _ => Files.Count > 0 ? "Choose another PDF" : "Choose PDF"
    };

    public string RunLabel => Kind switch
    {
        PdfToolKind.Merge => "Merge",
        PdfToolKind.Split => "Split",
        PdfToolKind.RemovePages => "Remove pages",
        PdfToolKind.ExtractPages => "Extract pages",
        PdfToolKind.Rotate => "Rotate",
        PdfToolKind.Copy => "Create copy",
        PdfToolKind.ImagesToPdf => "Create PDF",
        PdfToolKind.PdfToPng => "Export PNG pages",
        PdfToolKind.PdfToJpg => "Export JPG pages",
        PdfToolKind.Repair => "Rebuild PDF as a copy",
        PdfToolKind.Ocr => "Extract text offline",
        PdfToolKind.Crop => "Crop pages",
        PdfToolKind.ClearMetadata => "Clear basic metadata",
        PdfToolKind.Compress => "Compress",
        PdfToolKind.PageNumbers => "Add page numbers",
        PdfToolKind.Watermark => "Add watermark",
        PdfToolKind.Protect => "Protect with password",
        PdfToolKind.Unlock => "Unlock",
        PdfToolKind.Sign => "Sign document",
        PdfToolKind.Scan => "Create PDF from scans",
        _ => "Run"
    };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ToolId", out var id) && id is string s)
            Tool = ToolCatalog.Find(s);
    }

    partial void OnToolChanged(PdfTool? value)
    {
        RaiseAll();
        RefreshPresetNames();
    }

    partial void OnOutputNamePatternChanged(string value) => _outputNaming.Pattern = value;
    partial void OnSplitModeIndexChanged(int value) => RaiseAll();
    partial void OnPageSizeIndexChanged(int value) => RaiseAll();
    partial void OnLogoPathChanged(string? value) { OnPropertyChanged(nameof(HasLogo)); OnPropertyChanged(nameof(LogoLabel)); }
    partial void OnSignatureUploadPathChanged(string? value) { OnPropertyChanged(nameof(UploadLabel)); RefreshSignaturePreview(); }
    partial void OnSignModeIndexChanged(int value) { RaiseAll(); RefreshSignaturePreview(); }
    partial void OnTypedSignatureChanged(string value) => RefreshSignaturePreview();
    partial void OnInkIndexChanged(int value) => RefreshSignaturePreview();
    partial void OnSignaturePreviewChanged(ImageSource? value) => OnPropertyChanged(nameof(HasSignaturePreview));

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(IsMultiple), nameof(IsImages), nameof(IsScan), nameof(ShowRanges), nameof(ShowSplitOptions),
            nameof(ShowPagesPerFile), nameof(ShowRotation), nameof(ShowCrop), nameof(ShowMetadata), nameof(ShowRepair), nameof(ShowOcr), nameof(ShowImageOptions), nameof(ShowMargin), nameof(ShowLandscape),
            nameof(ShowCompress), nameof(ShowPageNumbers), nameof(ShowWatermark), nameof(ShowProtect), nameof(ShowUnlock),
            nameof(ShowSign), nameof(ShowDrawMode), nameof(ShowTypeMode), nameof(ShowUploadMode), nameof(ShowNoOptions),
            nameof(ShowOptionsCard), nameof(RangesLabel), nameof(PickLabel), nameof(RunLabel), nameof(HasFiles)
        })
            OnPropertyChanged(name);
    }

    private void FilesChanged()
    {
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(PickLabel));
        OnPropertyChanged(nameof(ShowOptionsCard));
    }

    private SKColor Ink => InkIndex == 1 ? new SKColor(31, 58, 147) : SKColors.Black;

    // ---- Signature preview ----
    public void RefreshSignaturePreview()
    {
        try
        {
            byte[]? png = null;
            if (SignModeIndex == 0 && Pad.HasStrokes)
                png = ImageTools.SignatureFromStrokes(Pad.Strokes, Ink);
            else if (SignModeIndex == 1 && !string.IsNullOrWhiteSpace(TypedSignature))
                png = ImageTools.SignatureFromText(TypedSignature, Ink);
            else if (SignModeIndex == 2 && !string.IsNullOrEmpty(SignatureUploadPath))
                png = ImageTools.SignatureFromImage(File.ReadAllBytes(SignatureUploadPath));

            SignaturePreview = png is null ? null : ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch (Exception)
        {
            SignaturePreview = null;
        }
    }

    [RelayCommand]
    private Task DrawSignature() =>
        Shell.Current.Navigation.PushModalAsync(new Pages.SignatureDrawPage(Pad, RefreshSignaturePreview, InkIndex == 1));

    [RelayCommand]
    private void ClearSignature()
    {
        Pad.Clear();
        TypedSignature = string.Empty;
        SignatureUploadPath = null;
        SignaturePreview = null;
    }

    // ---- File selection ----
    [RelayCommand]
    private async Task PickFiles()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = IsImages ? "Select images" : "Select PDF",
                FileTypes = IsImages ? FilePickerFileType.Images : FilePickerFileType.Pdf
            };

            var picked = await FilePickerHelper.PickAsync(options, IsMultiple);

            if (picked.Count == 0)
                return;
            await AddFilesAsync(picked);
        }
        catch (Exception ex)
        {
            await ShowError("Couldn't open file", ex);
        }
    }

    [RelayCommand]
    private async Task TakePhoto()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await Shell.Current.DisplayAlertAsync("Camera", "No camera is available on this device.", "OK");
                return;
            }
            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo is not null)
                await AddFilesAsync([photo]);
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Camera", "Camera permission is required to scan documents.", "OK");
        }
        catch (Exception ex)
        {
            await ShowError("Couldn't capture photo", ex);
        }
    }

    private async Task AddFilesAsync(List<FileResult> picked)
    {
        var added = new List<PdfDocumentItem>();
        foreach (var file in picked)
        {
            var local = await CacheAsync(file);
            var item = await Task.Run(() => Describe(local));
            added.Add(item);
        }

        if (!IsMultiple)
            Files.Clear();
        foreach (var item in added)
            Files.Add(item);

        Results.Clear();
        OnPropertyChanged(nameof(HasResults));
        Status = string.Empty;
        FilesChanged();
    }

    private PdfDocumentItem Describe(string local)
    {
        var size = new FileInfo(local).Length;
        if (IsImages)
            return new PdfDocumentItem { FilePath = local, SizeBytes = size, IsImage = true, PageCount = 1 };

        var encrypted = _security.IsEncrypted(local);
        if (encrypted && Kind != PdfToolKind.Unlock)
            throw new InvalidOperationException($"'{Path.GetFileName(local)[33..]}' is password protected. Use Unlock PDF first.");

        return new PdfDocumentItem
        {
            FilePath = local,
            SizeBytes = size,
            IsEncrypted = encrypted,
            PageCount = encrypted ? 0 : _organize.GetPageCount(local)
        };
    }

    private static async Task<string> CacheAsync(FileResult file)
    {
        Directory.CreateDirectory(HistoryService.CacheDirectory);
        var target = Path.Combine(HistoryService.CacheDirectory, $"{Guid.NewGuid():N}_{file.FileName}");
        await using var source = await file.OpenReadAsync();
        await using var dest = File.Create(target);
        await source.CopyToAsync(dest);
        return target;
    }

    [RelayCommand]
    private async Task PickLogo()
    {
        try
        {
            var file = await FilePickerHelper.PickOneAsync(new PickOptions { PickerTitle = "Select logo", FileTypes = FilePickerFileType.Images });
            if (file is not null)
                LogoPath = await CacheAsync(file);
        }
        catch (Exception ex)
        {
            await ShowError("Couldn't open logo", ex);
        }
    }

    [RelayCommand]
    private void RemoveLogo() => LogoPath = null;

    [RelayCommand]
    private async Task PickSignatureImage()
    {
        try
        {
            var file = await FilePickerHelper.PickOneAsync(new PickOptions { PickerTitle = "Select signature image", FileTypes = FilePickerFileType.Images });
            if (file is not null)
                SignatureUploadPath = await CacheAsync(file);
        }
        catch (Exception ex)
        {
            await ShowError("Couldn't open image", ex);
        }
    }

    [RelayCommand]
    private void Remove(PdfDocumentItem item)
    {
        Files.Remove(item);
        FilesChanged();
    }

    [RelayCommand]
    private void MoveUp(PdfDocumentItem item)
    {
        var i = Files.IndexOf(item);
        if (i > 0) Files.Move(i, i - 1);
    }

    [RelayCommand]
    private void MoveDown(PdfDocumentItem item)
    {
        var i = Files.IndexOf(item);
        if (i >= 0 && i < Files.Count - 1) Files.Move(i, i + 1);
    }

    private void RefreshPresetNames()
    {
        PresetNames.Clear();
        SelectedPresetName = null;
        if (Tool is null)
            return;

        try
        {
            foreach (var preset in _presets.ForTool(Tool.Id))
                PresetNames.Add(preset.Name);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SavePreset()
    {
        if (Tool is null)
            return;
        var name = await Shell.Current.DisplayPromptAsync("Save preset", "Name these reusable tool settings", "Save", "Cancel", "Preset name", 40);
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _presets.Save(CapturePreset(name.Trim()));
            RefreshPresetNames();
            SelectedPresetName = name.Trim();
            Status = $"Preset '{name.Trim()}' saved on this device.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't save preset", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task ApplyPreset()
    {
        if (Tool is null || string.IsNullOrWhiteSpace(SelectedPresetName))
            return;
        try
        {
            var preset = _presets.ForTool(Tool.Id).FirstOrDefault(p => p.Name == SelectedPresetName);
            if (preset is null)
            {
                await Shell.Current.DisplayAlertAsync("Preset not found", "Choose a saved preset for this tool.", "OK");
                RefreshPresetNames();
                return;
            }

            ApplyPreset(preset);
            Status = $"Applied preset '{preset.Name}'. Passwords and signature material are never stored in presets.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't apply preset", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task DeletePreset()
    {
        if (Tool is null || string.IsNullOrWhiteSpace(SelectedPresetName))
            return;
        var name = SelectedPresetName;
        if (!await Shell.Current.DisplayAlertAsync("Delete preset?", $"Remove '{name}' from this device?", "Delete", "Cancel"))
            return;
        try
        {
            _presets.Delete(Tool.Id, name);
            RefreshPresetNames();
            Status = $"Preset '{name}' deleted.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't delete preset", ex.Message, "OK");
        }
    }

    private PdfOperationPreset CapturePreset(string name) => new()
    {
        Name = name,
        ToolId = Tool!.Id,
        SplitModeIndex = SplitModeIndex,
        PagesPerFile = PagesPerFile,
        Ranges = Ranges,
        RotationIndex = RotationIndex,
        PageSizeIndex = PageSizeIndex,
        Landscape = Landscape,
        Margin = Margin,
        CompressionIndex = CompressionIndex,
        CropMarginPercent = CropMarginPercent,
        NumberPositionIndex = NumberPositionIndex,
        NumberFormatIndex = NumberFormatIndex,
        StartNumber = StartNumber,
        NumberFontSize = NumberFontSize,
        SkipFirstPage = SkipFirstPage,
        WatermarkText = WatermarkText,
        WatermarkOpacity = WatermarkOpacity,
        WatermarkRotation = WatermarkRotation,
        WatermarkFontSize = WatermarkFontSize,
        WatermarkTile = WatermarkTile,
        StrengthIndex = StrengthIndex,
        AllowPrint = AllowPrint,
        AllowCopy = AllowCopy,
        AllowEdit = AllowEdit,
        AllowAnnotate = AllowAnnotate,
        ScanFilterIndex = ScanFilterIndex,
        AutoCrop = AutoCrop
    };

    private void ApplyPreset(PdfOperationPreset preset)
    {
        SplitModeIndex = Math.Clamp(preset.SplitModeIndex, 0, SplitModes.Count - 1);
        PagesPerFile = Math.Max(1, preset.PagesPerFile);
        Ranges = preset.Ranges;
        RotationIndex = Math.Clamp(preset.RotationIndex, 0, RotationOptions.Count - 1);
        PageSizeIndex = Math.Clamp(preset.PageSizeIndex, 0, PageSizes.Count - 1);
        Landscape = preset.Landscape;
        Margin = Math.Clamp(preset.Margin, 0, 72);
        CompressionIndex = Math.Clamp(preset.CompressionIndex, 0, CompressionLevels.Count - 1);
        CropMarginPercent = Math.Clamp(preset.CropMarginPercent, 1, 40);
        NumberPositionIndex = Math.Clamp(preset.NumberPositionIndex, 0, NumberPositions.Count - 1);
        NumberFormatIndex = Math.Clamp(preset.NumberFormatIndex, 0, NumberFormats.Count - 1);
        StartNumber = Math.Max(1, preset.StartNumber);
        NumberFontSize = Math.Clamp(preset.NumberFontSize, 8, 32);
        SkipFirstPage = preset.SkipFirstPage;
        WatermarkText = preset.WatermarkText;
        WatermarkOpacity = Math.Clamp(preset.WatermarkOpacity, 5, 100);
        WatermarkRotation = Math.Clamp(preset.WatermarkRotation, -90, 90);
        WatermarkFontSize = Math.Clamp(preset.WatermarkFontSize, 12, 160);
        WatermarkTile = preset.WatermarkTile;
        StrengthIndex = Math.Clamp(preset.StrengthIndex, 0, Strengths.Count - 1);
        AllowPrint = preset.AllowPrint;
        AllowCopy = preset.AllowCopy;
        AllowEdit = preset.AllowEdit;
        AllowAnnotate = preset.AllowAnnotate;
        ScanFilterIndex = Math.Clamp(preset.ScanFilterIndex, 0, ScanFilters.Count - 1);
        AutoCrop = preset.AutoCrop;
    }

    // ---- Running ----
    private string? Validate()
    {
        switch (Kind)
        {
            case PdfToolKind.Merge when Files.Count < 2:
                return "Add at least two PDF files to merge.";
            case PdfToolKind.Protect when string.IsNullOrEmpty(Password):
                return "Enter a password.";
            case PdfToolKind.Protect when Password != ConfirmPassword:
                return "The passwords don't match.";
            case PdfToolKind.Watermark when string.IsNullOrWhiteSpace(WatermarkText) && !HasLogo:
                return "Enter watermark text or choose a logo.";
            case PdfToolKind.Sign when !HasSignaturePreview:
                return SignModeIndex switch
                {
                    0 => "Draw your signature first.",
                    1 => "Type your name to create a signature.",
                    _ => "Choose a signature image."
                };
            case PdfToolKind.Sign when !SignAllPages && !string.IsNullOrWhiteSpace(SignPage) && !int.TryParse(SignPage, out _):
                return "Signature page must be a number.";
            case PdfToolKind.Split when SplitModeIndex == 0 && string.IsNullOrWhiteSpace(Ranges):
                return "Enter the page ranges to split, e.g. 1-3, 5, 7-9.";
            case PdfToolKind.RemovePages or PdfToolKind.ExtractPages when string.IsNullOrWhiteSpace(Ranges):
                return "Enter the page numbers first, e.g. 1-3, 5.";
            case PdfToolKind.Crop when CropMarginPercent <= 0:
                return "Choose a crop margin greater than zero.";
            case PdfToolKind.Split when SplitModeIndex == 1 && PagesPerFile < 1:
                return "Pages per file must be at least 1.";
        }
        return null;
    }

    [RelayCommand]
    private async Task Run()
    {
        if (Tool is null || IsBusy)
            return;
        if (_features.BlockedReason(Tool.Id, _features.HasAdminAccess(_auth.IsAdmin)) is { } blocked)
        {
            await Shell.Current.DisplayAlertAsync(Tool.Title, blocked, "OK");
            return;
        }
        if (Files.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync(Tool.Title, "Please choose a file first.", "OK");
            return;
        }
        if (Validate() is { } problem)
        {
            await Shell.Current.DisplayAlertAsync(Tool.Title, problem, "OK");
            return;
        }
        if (PdfOutputNamingService.Validate(OutputNamePattern) is { } namingProblem)
        {
            await Shell.Current.DisplayAlertAsync("Output name pattern", namingProblem, "OK");
            return;
        }

        IsBusy = true;
        Status = "Working…";
        try
        {
            var inputs = Files.Select(f => f.FilePath).ToList();
            var inputBytes = Files.Sum(f => f.SizeBytes);
            var operationId = Tool.Id;
            var outputPattern = OutputNamePattern.Trim();
            var outputs = Kind == PdfToolKind.Ocr
                ? _outputNaming.Apply([await _ocr.ExtractTextAsync(inputs[0], HistoryService.OutputDirectory)], inputs[0], operationId, outputPattern)
                : await Task.Run(() => _outputNaming.Apply(Execute(inputs), inputs[0], operationId, outputPattern));

            Results.Clear();
            foreach (var path in outputs)
                Results.Add(DescribeResult(path));
            OnPropertyChanged(nameof(HasResults));

            var outputBytes = Results.Sum(r => r.SizeBytes);
            Status = BuildStatus(inputBytes, outputBytes, outputs.Count);
            _history.Add(new HistoryEntry
            {
                ToolId = Tool.Id,
                ToolTitle = Tool.Title,
                UserEmail = _auth.Current?.Email ?? string.Empty,
                Glyph = Tool.Glyph,
                InputBytes = inputBytes,
                OutputBytes = outputBytes,
                Outputs = outputs.ToList()
            });
        }
        catch (Exception ex)
        {
            Status = string.Empty;
            await Shell.Current.DisplayAlertAsync("Something went wrong", PdfErrors.Friendly(ex), "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private PdfDocumentItem DescribeResult(string output)
    {
        var isImage = !Path.GetExtension(output).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var pages = isImage ? 1 : _organize.GetPageCount(output);
        return new PdfDocumentItem { FilePath = output, SizeBytes = new FileInfo(output).Length, PageCount = pages, IsImage = isImage };
    }

    private string BuildStatus(long inputBytes, long outputBytes, int count)
    {
        if (Kind == PdfToolKind.Repair)
            return "Recovery completed. Pages were rebuilt as images; text, links, forms, layers, and interactive content were not preserved.";
        if (Kind == PdfToolKind.Ocr)
            return "Offline OCR completed. Recognized text was saved to a separate text file; the source PDF was not changed.";
        if (Kind == PdfToolKind.Crop)
            return "Visual crop complete. Content outside the crop area remains in the PDF and may be recoverable; this is not redaction.";
        if (Kind == PdfToolKind.ClearMetadata)
            return "Basic document info and XMP metadata cleared. Attachments, annotations, form data, scripts, and other embedded content are not sanitized.";
        if (Kind == PdfToolKind.Compress)
        {
            if (outputBytes >= inputBytes)
                return "This file is already well optimized, so no further savings were possible.";
            var pct = 100.0 * (inputBytes - outputBytes) / inputBytes;
            return $"Done! {Formatting.Bytes(inputBytes)} → {Formatting.Bytes(outputBytes)} ({pct:0}% smaller).";
        }
        return count == 1 ? "Done! Your file is ready." : $"Done! {count} files are ready.";
    }

    // Runs on a background thread: capture everything it needs from the UI state first.
    private IReadOnlyList<string> Execute(List<string> inputs)
    {
        var outDir = HistoryService.OutputDirectory;
        var first = inputs[0];
        var ranges = Ranges;

        switch (Kind)
        {
            case PdfToolKind.Merge:
                return [_organize.Merge(inputs, outDir)];
            case PdfToolKind.Split:
                return SplitModeIndex == 0
                    ? _organize.SplitByRanges(first, ranges, outDir)
                    : _organize.SplitEvery(first, PagesPerFile, outDir);
            case PdfToolKind.RemovePages:
                return [_organize.RemovePages(first, ranges, outDir)];
            case PdfToolKind.ExtractPages:
                return [_organize.ExtractPages(first, ranges, outDir)];
            case PdfToolKind.Copy:
                return [_organize.Copy(first, outDir)];
            case PdfToolKind.Rotate:
                return [_edit.Rotate(first, RotationIndex switch { 0 => 90, 1 => 180, _ => 270 }, ranges, outDir)];
            case PdfToolKind.ImagesToPdf:
                return [_convert.ImagesToPdf(inputs, (PageSizeOption)PageSizeIndex, Landscape, Margin, outDir)];
            case PdfToolKind.PdfToPng:
                return PdfPageRenderService.ExportPngPages(first, outDir);
            case PdfToolKind.PdfToJpg:
                return PdfPageRenderService.ExportJpgPages(first, outDir);
            case PdfToolKind.Repair:
                return [_recovery.RebuildFromRenderedPages(first, outDir)];
            case PdfToolKind.Ocr:
                throw new InvalidOperationException("OCR must be executed asynchronously by the tool page.");
            case PdfToolKind.Crop:
                return [_edit.Crop(first, CropMarginPercent, ranges, outDir)];
            case PdfToolKind.ClearMetadata:
                return [_edit.ClearBasicMetadata(first, outDir)];
            case PdfToolKind.Compress:
                return [_optimize.Compress(first, (CompressionLevel)CompressionIndex, outDir)];
            case PdfToolKind.PageNumbers:
                return [_edit.AddPageNumbers(first, new PageNumberOptions(
                    (NumberPosition)NumberPositionIndex, StartNumber, NumberFormatTokens[NumberFormatIndex],
                    NumberFontSize, 28, SkipFirstPage, ranges), outDir)];
            case PdfToolKind.Watermark:
                return [_edit.AddWatermark(first, new WatermarkOptions(
                    WatermarkText, LogoPath is null ? null : File.ReadAllBytes(LogoPath), WatermarkOpacity / 100.0,
                    WatermarkRotation, WatermarkTile, WatermarkFontSize, 40, ranges), outDir)];
            case PdfToolKind.Protect:
                return [_security.Protect(first, Password, OwnerPassword,
                    StrengthIndex == 0 ? EncryptionStrength.Aes256 : EncryptionStrength.Aes128,
                    new PdfPermissions(AllowPrint, AllowCopy, AllowEdit, AllowAnnotate), outDir)];
            case PdfToolKind.Unlock:
                return [_security.Unlock(first, Password, outDir)];
            case PdfToolKind.Sign:
                return [_security.Sign(first, new SignOptions(BuildSignature(), int.TryParse(SignPage, out var page) ? page : 0,
                    (SignaturePosition)SignPositionIndex, SignWidth, SignerName, AddTimestamp, SignAllPages), outDir)];
            case PdfToolKind.Scan:
                var scans = _scan.Process(inputs, (ScanFilter)ScanFilterIndex, AutoCrop, Path.Combine(FileSystem.CacheDirectory, "scan"));
                return [_convert.ImagesToPdf(scans, (PageSizeOption)PageSizeIndex, false, Margin, outDir)];
            default:
                throw new NotSupportedException();
        }
    }

    private byte[] BuildSignature() => SignModeIndex switch
    {
        0 => ImageTools.SignatureFromStrokes(Pad.Strokes, Ink),
        1 => ImageTools.SignatureFromText(TypedSignature, Ink),
        _ => ImageTools.SignatureFromImage(File.ReadAllBytes(SignatureUploadPath!))
    };

    // ---- Results ----
    [RelayCommand]
    private async Task Share(PdfDocumentItem item)
    {
        if (await FileActions.ShareAsync(item.FilePath) is { } error)
            Status = error;
    }

    [RelayCommand]
    private async Task Save(PdfDocumentItem item)
    {
        Status = await FileActions.SaveAsync(item.FilePath);
    }

    [RelayCommand]
    private async Task OpenResult(PdfDocumentItem item)
    {
        if (await FileActions.OpenAsync(item.FilePath) is { } error)
            Status = error;
    }

    [RelayCommand]
    private async Task Preview(PdfDocumentItem item)
    {
        if (item.IsImage)
        {
            await FileActions.OpenAsync(item.FilePath);
            return;
        }
        if (item.IsEncrypted)
        {
            Status = "Protected files can't be previewed. Unlock them first.";
            return;
        }
        await Shell.Current.Navigation.PushModalAsync(new Pages.PdfPreviewPage(item.FilePath));
    }

    private static Task ShowError(string title, Exception ex) =>
        Shell.Current.DisplayAlertAsync(title, PdfErrors.Friendly(ex), "OK");
}
