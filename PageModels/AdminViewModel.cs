using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class AdminViewModel : ObservableObject
{
    private readonly HistoryService _history;
    private readonly AppSettingsService _settings;
    private readonly AuthService _auth;
    private bool _loading;

    public AdminViewModel(HistoryService history, AppSettingsService settings, AuthService auth)
    {
        _history = history;
        _settings = settings;
        _auth = auth;
        _auth.UsersChanged += () => MainThread.BeginInvokeOnMainThread(RefreshUsers);
        _history.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
        _ = LoadAiAsync();
    }

    public IReadOnlyList<string> Providers => AppSettingsService.Providers;

    public ObservableCollection<HistoryEntry> History { get; } = [];
    public ObservableCollection<ToolUsage> TopTools { get; } = [];
    public ObservableCollection<UserAccount> Users { get; } = [];

    [ObservableProperty] private string _userCount = "0";

    [ObservableProperty] private string _operations = "0";
    [ObservableProperty] private string _filesCreated = "0";
    [ObservableProperty] private string _storage = "0 B";
    [ObservableProperty] private string _saved = "0 B";
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoHistory))]
    private bool _hasHistory;

    public bool HasNoHistory => !HasHistory;
    [ObservableProperty] private bool _hasTopTools;

    [ObservableProperty] private int _providerIndex;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _endpoint = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private bool _keepHistory;
    [ObservableProperty] private bool _autoCleanCache;

    public bool NeedsApiKey => SelectedProvider != "Ollama (local)";
    private string SelectedProvider => Providers[Math.Clamp(ProviderIndex, 0, Providers.Count - 1)];

    partial void OnProviderIndexChanged(int value)
    {
        OnPropertyChanged(nameof(NeedsApiKey));
        if (!_loading)
            _ = LoadProviderAsync();
    }

    partial void OnKeepHistoryChanged(bool value)
    {
        if (!_loading)
            _settings.KeepHistory = value;
    }

    partial void OnAutoCleanCacheChanged(bool value)
    {
        if (!_loading)
            _settings.AutoCleanCache = value;
    }

    private async Task LoadAiAsync()
    {
        _loading = true;
        KeepHistory = _settings.KeepHistory;
        AutoCleanCache = _settings.AutoCleanCache;
        var idx = Providers.ToList().IndexOf(_settings.Provider);
        ProviderIndex = idx < 0 ? 0 : idx;
        _loading = false;
        await LoadProviderAsync();
    }

    private async Task LoadProviderAsync()
    {
        var provider = SelectedProvider;
        Model = _settings.Model(provider);
        Endpoint = _settings.Endpoint(provider);
        var key = await _settings.GetApiKeyAsync(provider);
        if (provider == SelectedProvider)
            ApiKey = key;
    }

    public void Refresh()
    {
        RefreshUsers();
        var stats = _history.GetStats();
        Operations = stats.TotalOperations.ToString();
        FilesCreated = stats.FilesCreated.ToString();
        Storage = Formatting.Bytes(stats.StorageBytes);
        Saved = Formatting.Bytes(stats.BytesSaved);

        TopTools.Clear();
        foreach (var t in stats.TopTools)
            TopTools.Add(t);
        HasTopTools = TopTools.Count > 0;

        History.Clear();
        foreach (var e in _history.All().Take(100))
            History.Add(e);
        HasHistory = History.Count > 0;
    }

    [RelayCommand]
    private async Task SaveAiAsync()
    {
        try
        {
            var provider = SelectedProvider;
            _settings.Provider = provider;
            _settings.SetModel(provider, string.IsNullOrWhiteSpace(Model) ? AppSettingsService.DefaultModel(provider) : Model);
            _settings.SetEndpoint(provider, string.IsNullOrWhiteSpace(Endpoint) ? AppSettingsService.DefaultEndpoint(provider) : Endpoint);
            await _settings.SetApiKeyAsync(provider, ApiKey);
            Status = $"{provider} settings saved. The key is kept in secure storage.";
        }
        catch (Exception ex)
        {
            Status = $"Could not save settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearCache()
    {
        var freed = _history.ClearCache();
        Status = $"Cache cleared. Freed {Formatting.Bytes(freed)}.";
    }

    [RelayCommand]
    private async Task DeleteOutputsAsync()
    {
        if (!await Confirm("Delete all output files?", "Every generated PDF stored in the app will be removed."))
            return;
        _history.DeleteAllOutputs();
        Status = "All generated files deleted.";
    }

    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (!await Confirm("Clear history?", "The activity log will be emptied. Generated files are kept."))
            return;
        _history.ClearHistory();
        Status = "History cleared.";
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(HistoryEntry? entry)
    {
        if (entry is null || !await Confirm("Delete this entry?", "The entry and its generated files will be removed."))
            return;
        _history.Remove(entry, deleteFiles: true);
    }

    [RelayCommand]
    private async Task ShareEntryAsync(HistoryEntry? entry)
    {
        var files = entry?.Outputs.Where(File.Exists).Select(p => new ShareFile(p)).ToList();
        if (files is null || files.Count == 0)
        {
            Status = "The file no longer exists.";
            return;
        }
        try
        {
            await Share.Default.RequestAsync(new ShareMultipleFilesRequest { Title = entry!.ToolTitle, Files = files });
        }
        catch (Exception ex)
        {
            Status = $"Could not share: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenEntryAsync(HistoryEntry? entry)
    {
        var path = entry?.Outputs.FirstOrDefault(File.Exists);
        if (path is null)
        {
            Status = "The file no longer exists.";
            return;
        }
        try
        {
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                await Shell.Current.Navigation.PushModalAsync(new Pages.PdfPreviewPage(path, entry!.ToolTitle));
            else
                Status = await FileActions.OpenAsync(path) ?? string.Empty;
        }
        catch (Exception ex)
        {
            Status = $"Could not open: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveEntryAsync(HistoryEntry? entry)
    {
        var path = entry?.Outputs.FirstOrDefault(File.Exists);
        if (path is null)
        {
            Status = "The file no longer exists.";
            return;
        }
        Status = await FileActions.SaveAsync(path);
    }

    private void RefreshUsers()
    {
        Users.Clear();
        foreach (var u in _auth.Users().Where(u => !u.IsAdmin))
            Users.Add(u);
        UserCount = Users.Count.ToString();
        OnPropertyChanged(nameof(HasNoUsers));
    }

    public bool HasNoUsers => Users.Count == 0;

    [RelayCommand]
    private void ToggleUser(UserAccount? user)
    {
        if (user is null)
            return;
        var result = _auth.SetDisabled(user, !user.Disabled);
        Status = result.Success ? $"{user.Email} is now {(user.Disabled ? "disabled" : "enabled")}." : result.Error ?? string.Empty;
        RefreshUsers();
    }

    [RelayCommand]
    private async Task DeleteUserAsync(UserAccount? user)
    {
        if (user is null || !await Confirm("Delete this user?", $"{user.Email} will no longer be able to sign in."))
            return;
        var result = _auth.Delete(user);
        Status = result.Success ? "User deleted." : result.Error ?? string.Empty;
    }

    [RelayCommand]
    private void RefreshNow() => Refresh();

    private static Task<bool> Confirm(string title, string message)
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        return page is null ? Task.FromResult(false) : page.DisplayAlertAsync(title, message, "Yes", "Cancel");
    }
}
