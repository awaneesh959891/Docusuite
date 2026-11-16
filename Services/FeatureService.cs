using System.Text.Json;
using MauiApp.Models;

namespace MauiApp.Services;

public sealed class FeatureSetting
{
    public string ToolId { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public string Message { get; set; } = string.Empty;
}

public sealed class GlobalSettings
{
    public bool MaintenanceMode { get; set; }
    public string MaintenanceMessage { get; set; } = "The app is under maintenance. Please try again later.";
    public bool AnnouncementOn { get; set; }
    public string Announcement { get; set; } = string.Empty;
    public bool AllowSignUp { get; set; } = true;
}

public sealed class FeatureConfig
{
    public GlobalSettings Global { get; set; } = new();
    public List<FeatureSetting> Features { get; set; } = [];
}

// Admin-controlled switches. Administrators are never restricted so they can always test a tool.
public class FeatureService
{
    public const string DefaultDisabledMessage = "This feature is temporarily unavailable.";

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(FileSystem.AppDataDirectory, "features.json");
    private FeatureConfig? _config;

    public event Action? Changed;

    public bool PreviewAsUser
    {
        get => Preferences.Default.Get("admin_preview_as_user", false);
        set
        {
            Preferences.Default.Set("admin_preview_as_user", value);
            Changed?.Invoke();
        }
    }

    public bool HasAdminAccess(bool isAdmin) => isAdmin && !PreviewAsUser;

    public GlobalSettings Global
    {
        get { lock (_gate) return Load().Global; }
    }

    public FeatureSetting Get(string toolId)
    {
        lock (_gate)
            return Load().Features.FirstOrDefault(f => f.ToolId == toolId) ?? new FeatureSetting { ToolId = toolId };
    }

    public bool IsVisible(string toolId, bool isAdmin) => isAdmin || Get(toolId).Visible;

    // Returns the message to show when the tool is blocked, or null when it can be used.
    public string? BlockedReason(string toolId, bool isAdmin)
    {
        if (isAdmin)
            return null;
        var global = Global;
        if (global.MaintenanceMode)
            return string.IsNullOrWhiteSpace(global.MaintenanceMessage) ? DefaultDisabledMessage : global.MaintenanceMessage;
        var f = Get(toolId);
        if (f.Enabled)
            return null;
        return string.IsNullOrWhiteSpace(f.Message) ? DefaultDisabledMessage : f.Message;
    }

    public void SaveFeatures(IEnumerable<FeatureSetting> settings)
    {
        lock (_gate)
        {
            var config = Load();
            config.Features = settings
                .Where(s => !s.Visible || !s.Enabled || !string.IsNullOrWhiteSpace(s.Message))
                .Select(s => new FeatureSetting { ToolId = s.ToolId, Visible = s.Visible, Enabled = s.Enabled, Message = s.Message.Trim() })
                .ToList();
            Save();
        }
        Changed?.Invoke();
    }

    public void SaveGlobal(GlobalSettings global)
    {
        lock (_gate)
        {
            Load().Global = global;
            Save();
        }
        Changed?.Invoke();
    }

    private FeatureConfig Load()
    {
        if (_config is not null)
            return _config;
        try
        {
            _config = File.Exists(_file)
                ? JsonSerializer.Deserialize<FeatureConfig>(File.ReadAllText(_file)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _config = new();
        }
        return _config;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(_config, Json));
    }
}
