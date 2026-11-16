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

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _greeting = "DocSuite";

    [ObservableProperty]
    private string _announcement = string.Empty;

    [ObservableProperty]
    private bool _hasAnnouncement;

    public ObservableCollection<PdfToolGroup> Groups { get; } = [];

    public HomeViewModel(AuthService auth, FeatureService features)
    {
        _auth = auth;
        _features = features;
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

    private async Task OpenToolCore(PdfTool tool)
    {
        if (_features.BlockedReason(tool.Id, _features.HasAdminAccess(_auth.IsAdmin)) is { } reason)
        {
            await Shell.Current.DisplayAlertAsync(tool.Title, reason, "OK");
            return;
        }
        if (!tool.IsAvailable)
        {
            await Shell.Current.DisplayAlertAsync(tool.Title, "This tool is coming in an upcoming phase.", "OK");
            return;
        }
        await Shell.Current.GoToAsync($"tool?ToolId={tool.Id}");
    }
}
