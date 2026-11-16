using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class FeatureRow : ObservableObject
{
    private readonly Action _changed;

    public FeatureRow(PdfTool tool, FeatureSetting setting, Action changed)
    {
        Tool = tool;
        _changed = changed;
        _visible = setting.Visible;
        _enabled = setting.Enabled;
        _message = setting.Message;
    }

    public PdfTool Tool { get; }

    [ObservableProperty] private bool _visible;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _message;

    partial void OnVisibleChanged(bool value) => _changed();
    partial void OnEnabledChanged(bool value) => _changed();
    partial void OnMessageChanged(string value) => _changed();

    public FeatureSetting ToSetting() => new() { ToolId = Tool.Id, Visible = Visible, Enabled = Enabled, Message = Message ?? string.Empty };
}

public partial class FeaturesViewModel : ObservableObject
{
    private readonly FeatureService _features;
    private bool _loading;

    public FeaturesViewModel(FeatureService features)
    {
        _features = features;
        Load();
    }

    public ObservableCollection<FeatureRow> Rows { get; } = [];

    [ObservableProperty] private bool _maintenanceMode;
    [ObservableProperty] private string _maintenanceMessage = string.Empty;
    [ObservableProperty] private bool _announcementOn;
    [ObservableProperty] private string _announcement = string.Empty;
    [ObservableProperty] private bool _allowSignUp = true;
    [ObservableProperty] private bool _previewAsUser;
    [ObservableProperty] private string _status = string.Empty;

    partial void OnMaintenanceModeChanged(bool value) => PersistGlobal();
    partial void OnMaintenanceMessageChanged(string value) => PersistGlobal();
    partial void OnAnnouncementOnChanged(bool value) => PersistGlobal();
    partial void OnAnnouncementChanged(string value) => PersistGlobal();
    partial void OnAllowSignUpChanged(bool value) => PersistGlobal();

    partial void OnPreviewAsUserChanged(bool value)
    {
        if (!_loading)
        {
            _features.PreviewAsUser = value;
            Status = value ? "User preview is ON. Open PDF Tools to test restrictions." : "User preview is OFF. Admin bypass restored.";
        }
    }

    public void Load()
    {
        _loading = true;
        var g = _features.Global;
        MaintenanceMode = g.MaintenanceMode;
        MaintenanceMessage = g.MaintenanceMessage;
        AnnouncementOn = g.AnnouncementOn;
        Announcement = g.Announcement;
        AllowSignUp = g.AllowSignUp;
        PreviewAsUser = _features.PreviewAsUser;

        Rows.Clear();
        foreach (var tool in ToolCatalog.All)
            Rows.Add(new FeatureRow(tool, _features.Get(tool.Id), PersistFeatures));
        _loading = false;
    }

    private void PersistGlobal()
    {
        if (_loading)
            return;
        _features.SaveGlobal(new GlobalSettings
        {
            MaintenanceMode = MaintenanceMode,
            MaintenanceMessage = MaintenanceMessage.Trim(),
            AnnouncementOn = AnnouncementOn,
            Announcement = Announcement.Trim(),
            AllowSignUp = AllowSignUp
        });
        Status = $"App settings saved at {DateTime.Now:h:mm:ss tt}.";
    }

    private void PersistFeatures()
    {
        if (_loading)
            return;
        _features.SaveFeatures(Rows.Select(r => r.ToSetting()));
        Status = $"Feature settings saved at {DateTime.Now:h:mm:ss tt}.";
    }

    [RelayCommand]
    private void Save()
    {
        PersistGlobal();
        PersistFeatures();
        Status = $"Everything saved at {DateTime.Now:h:mm:ss tt}.";
    }

    [RelayCommand]
    private void ResetAll()
    {
        _loading = true;
        foreach (var row in Rows)
        {
            row.Visible = true;
            row.Enabled = true;
            row.Message = string.Empty;
        }
        _loading = false;
        PersistFeatures();
        Status = "All tools are visible and enabled.";
    }

    [RelayCommand]
    private void EnableAll()
    {
        _loading = true;
        foreach (var row in Rows)
            row.Enabled = true;
        _loading = false;
        PersistFeatures();
        Status = "All tools enabled.";
    }

    [RelayCommand]
    private void DisableAll()
    {
        _loading = true;
        foreach (var row in Rows)
            row.Enabled = false;
        _loading = false;
        PersistFeatures();
        Status = "All tools disabled.";
    }
}
