using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class PrivacyViewModel : ObservableObject
{
    private readonly HistoryService _history;
    private readonly AppSettingsService _settings;

    public PrivacyViewModel(HistoryService history, AppSettingsService settings)
    {
        _history = history;
        _settings = settings;
        KeepHistory = _settings.KeepHistory;
        AutoCleanCache = _settings.AutoCleanCache;
        HistoryRetentionIndex = _settings.HistoryRetentionDays switch { 7 => 1, 30 => 2, 90 => 3, _ => 0 };
        _history.PruneExpired();
        Refresh();
    }

    [ObservableProperty] private string _localStorage = "0 B";
    [ObservableProperty] private string _cacheStorage = "0 B";
    [ObservableProperty] private string _historyCount = "0";
    [ObservableProperty] private bool _keepHistory;
    [ObservableProperty] private bool _autoCleanCache;
    [ObservableProperty] private int _historyRetentionIndex;
    [ObservableProperty] private string _status = string.Empty;

    public IReadOnlyList<string> HistoryRetentionOptions { get; } = ["Never", "7 days", "30 days", "90 days"];

    partial void OnKeepHistoryChanged(bool value) => _settings.KeepHistory = value;
    partial void OnAutoCleanCacheChanged(bool value) => _settings.AutoCleanCache = value;
    partial void OnHistoryRetentionIndexChanged(int value)
    {
        _settings.HistoryRetentionDays = value switch { 1 => 7, 2 => 30, 3 => 90, _ => 0 };
        _history.PruneExpired();
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        LocalStorage = Formatting.Bytes(DirectorySize(HistoryService.OutputDirectory) +
            DirectorySize(Path.Combine(FileSystem.AppDataDirectory, "Workspace")) +
            DirectorySize(Path.Combine(FileSystem.AppDataDirectory, "WorkspaceRecycleBin")));
        CacheStorage = Formatting.Bytes(DirectorySize(FileSystem.CacheDirectory));
        HistoryCount = _history.All().Count.ToString();
    }

    [RelayCommand]
    private void ClearCache()
    {
        var freed = _history.ClearCache();
        Status = $"Temporary files and thumbnails cleared. Freed {Formatting.Bytes(freed)}.";
        Refresh();
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!await Shell.Current.DisplayAlertAsync("Clear local history?", "Activity records will be removed. Generated files are kept.", "Clear", "Cancel"))
            return;

        _history.ClearHistory();
        Status = "Local activity history cleared.";
        Refresh();
    }

    private static long DirectorySize(string directory)
    {
        if (!Directory.Exists(directory))
            return 0;

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
    }
}
