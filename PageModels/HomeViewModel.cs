using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly FeatureService _features;
    private readonly PdfPageOrganizerService _pageOrganizer;
    private readonly HistoryService _history;
    private readonly PdfOutputNamingService _outputNaming;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _greeting = "DocSuite";

    [ObservableProperty]
    private string _announcement = string.Empty;

    [ObservableProperty]
    private bool _hasAnnouncement;

    public ObservableCollection<PdfToolGroup> Groups { get; } = [];

    public HomeViewModel(AuthService auth, FeatureService features, PdfPageOrganizerService pageOrganizer,
        HistoryService history, PdfOutputNamingService outputNaming)
    {
        _auth = auth;
        _features = features;
        _pageOrganizer = pageOrganizer;
        _history = history;
        _outputNaming = outputNaming;
        _features.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        _auth.SessionChanged += () => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    public void Refresh()
    {
        var isAdmin = _features.HasAdminAccess(_auth.IsAdmin);
        var name = _auth.Current?.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        Greeting = string.IsNullOrEmpty(name) ? "DocSuite" : $"Hello, {name} 👋";

        var global = _features.Global;
        Announcement = global.Announcement.Trim();
        HasAnnouncement = global.AnnouncementOn && Announcement.Length > 0;

        var q = SearchText.Trim();
        var cards = ToolCatalog.All
            .Where(t => _features.IsVisible(t.Id, isAdmin))
            .Where(t => q.Length == 0 ||
                        t.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(t => new ToolCard(t, _features.BlockedReason(t.Id, isAdmin)));

        Groups.Clear();
        foreach (var g in cards.GroupBy(c => c.Tool.Category))
            Groups.Add(new PdfToolGroup(ToolCatalog.CategoryTitle(g.Key), g));
    }

    [RelayCommand]
    private Task OpenDocuments() => Shell.Current.GoToAsync("//documents");

    [RelayCommand]
    private Task OpenBatch() => Shell.Current.GoToAsync("//batch");

    [RelayCommand]
    private Task QuickScan() => OpenToolById("scan");

    [RelayCommand]
    private Task QuickMerge() => OpenToolById("merge");

    [RelayCommand]
    private Task QuickCompress() => OpenToolById("compress");

    [RelayCommand]
    private Task QuickSign() => OpenToolById("sign");

    [RelayCommand]
    private Task QuickImageToPdf() => OpenToolById("jpg2pdf");

    [RelayCommand]
    private async Task OpenTool(ToolCard card) => await OpenToolCore(card.Tool);

    private async Task OpenToolById(string id)
    {
        var tool = ToolCatalog.Find(id);
        if (tool is null)
            return;

        await OpenToolCore(tool);
    }

    private async Task OpenOrganizerAsync()
    {
        try
        {
            var file = await FilePickerHelper.PickOneAsync(new PickOptions
            {
                PickerTitle = "Choose a PDF to organize",
                FileTypes = FilePickerFileType.Pdf
            });
            if (file is null)
                return;

            var cachedPath = await CachePdfAsync(file);
            await Shell.Current.Navigation.PushModalAsync(new Pages.PageOrganizerPage(
                cachedPath, _pageOrganizer, _history, _outputNaming));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't open PDF", ex.Message, "OK");
        }
    }

    private static async Task<string> CachePdfAsync(FileResult file)
    {
        const long maxFileSize = 512L * 1024 * 1024;
        var target = Path.Combine(FileSystem.CacheDirectory, "OrganizerInputs", $"{Guid.NewGuid():N}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            await using var source = await file.OpenReadAsync();
            if (source.CanSeek && source.Length > maxFileSize)
                throw new InvalidOperationException("This PDF exceeds the 512 MB limit for page organization.");

            await using var destination = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long total = 0;
            var prefix = new byte[5];
            var prefixLength = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                total += read;
                if (total > maxFileSize)
                    throw new InvalidOperationException("This PDF exceeds the 512 MB limit for page organization.");
                if (prefixLength < prefix.Length)
                {
                    var copy = Math.Min(prefix.Length - prefixLength, read);
                    Array.Copy(buffer, 0, prefix, prefixLength, copy);
                    prefixLength += copy;
                }
                await destination.WriteAsync(buffer.AsMemory(0, read));
            }

            if (prefixLength != prefix.Length || !prefix.AsSpan().SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("The selected file does not have a valid PDF header.");
            return target;
        }
        catch
        {
            if (File.Exists(target))
                File.Delete(target);
            throw;
        }
    }

    private async Task OpenToolCore(PdfTool tool)
    {
        if (_features.BlockedReason(tool.Id, _features.HasAdminAccess(_auth.IsAdmin)) is { } reason)
        {
            await Shell.Current.DisplayAlertAsync(tool.Title, reason, "OK");
            return;
        }
        if (!tool.IsAvailable)
        {
            await Shell.Current.DisplayAlertAsync(tool.Title, "This tool is not available on this platform yet.", "OK");
            return;
        }
        if (tool.Kind == PdfToolKind.Organize)
        {
            await OpenOrganizerAsync();
            return;
        }
        await Shell.Current.GoToAsync($"tool?ToolId={tool.Id}");
    }
}
