using System.Text.Json;
using MauiApp.Models;

namespace MauiApp.Services;

public static class Formatting
{
    public static string Bytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:0.#} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.##} GB"
    };
}

public class HistoryService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(FileSystem.AppDataDirectory, "history.json");
    private List<HistoryEntry>? _entries;

    public event Action? Changed;

    public static string OutputDirectory => Path.Combine(FileSystem.AppDataDirectory, "Output");
    public static string CacheDirectory => Path.Combine(FileSystem.CacheDirectory, "Inputs");

    public IReadOnlyList<HistoryEntry> All()
    {
        lock (_gate)
            return Load().OrderByDescending(e => e.Timestamp).ToList();
    }

    public void Add(HistoryEntry entry)
    {
        if (!Preferences.Default.Get(AppSettingsService.KeepHistoryKey, true))
            return;
        lock (_gate)
        {
            RemoveExpiredEntries();
            Load().Add(entry);
            Save();
        }
        Changed?.Invoke();
    }

    public void PruneExpired()
    {
        var removed = false;
        lock (_gate)
        {
            removed = RemoveExpiredEntries();
            if (removed)
                Save();
        }
        if (removed)
            Changed?.Invoke();
    }

    private bool RemoveExpiredEntries()
    {
        var days = Preferences.Default.Get(AppSettingsService.HistoryRetentionDaysKey, 0);
        if (days <= 0)
            return false;
        var cutoff = DateTime.Now.AddDays(-days);
        return Load().RemoveAll(entry => entry.Timestamp < cutoff) > 0;
    }

    public void Remove(HistoryEntry entry, bool deleteFiles)
    {
        lock (_gate)
        {
            Load().RemoveAll(e => e.Id == entry.Id);
            Save();
            if (deleteFiles)
                foreach (var path in entry.Outputs)
                    TryDelete(path);
        }
        Changed?.Invoke();
    }

    public void ClearHistory()
    {
        lock (_gate)
        {
            Load().Clear();
            Save();
        }
        Changed?.Invoke();
    }

    public void DeleteAllOutputs()
    {
        lock (_gate)
        {
            if (Directory.Exists(OutputDirectory))
                foreach (var file in Directory.EnumerateFiles(OutputDirectory))
                    TryDelete(file);
        }
        Changed?.Invoke();
    }

    public long ClearCache()
    {
        long freed = 0;
        foreach (var dir in new[]
        {
            CacheDirectory,
            Path.Combine(FileSystem.CacheDirectory, "scan"),
            Path.Combine(FileSystem.CacheDirectory, "WorkspaceThumbs"),
            Path.Combine(FileSystem.CacheDirectory, "PageOrganizerThumbs"),
            Path.Combine(FileSystem.CacheDirectory, "BatchInputs")
        })
        {
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    freed += new FileInfo(file).Length;
                    File.Delete(file);
                }
                catch (IOException) { }
            }
        }
        Changed?.Invoke();
        return freed;
    }

    public UsageStats GetStats()
    {
        List<HistoryEntry> entries;
        lock (_gate)
            entries = Load().ToList();

        long storage = Directory.Exists(OutputDirectory)
            ? Directory.EnumerateFiles(OutputDirectory).Sum(f => new FileInfo(f).Length)
            : 0;
        long saved = entries.Where(e => e.ToolId == "compress").Sum(e => Math.Max(0, e.InputBytes - e.OutputBytes));

        var top = entries.GroupBy(e => e.ToolId)
            .Select(g => (Title: g.First().ToolTitle, Glyph: g.First().Glyph, Count: g.Count()))
            .OrderByDescending(t => t.Count)
            .Take(5)
            .ToList();
        var max = top.Count > 0 ? top.Max(t => t.Count) : 1;

        return new UsageStats(entries.Count, entries.Sum(e => e.Outputs.Count), storage, saved,
            top.Select(t => new ToolUsage(t.Title, t.Glyph, t.Count, (double)t.Count / max)).ToList());
    }

    private List<HistoryEntry> Load()
    {
        if (_entries is not null)
            return _entries;
        try
        {
            _entries = File.Exists(_file)
                ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(_file)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _entries = [];
        }
        return _entries;
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(_entries, Json));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public class AppSettingsService
{
    public const string KeepHistoryKey = "keep_history";
    public const string HistoryRetentionDaysKey = "history_retention_days";
    private const string ProviderKey = "ai_provider";
    private const string ModelKey = "ai_model";
    private const string EndpointKey = "ai_endpoint";
    private const string AutoCleanKey = "auto_clean_cache";

    public static IReadOnlyList<string> Providers { get; } = ["OpenAI", "Google Gemini", "Azure OpenAI", "Ollama (local)"];

    public static string DefaultModel(string provider) => provider switch
    {
        "OpenAI" => "gpt-4o-mini",
        "Google Gemini" => "gemini-1.5-flash",
        "Azure OpenAI" => "your-deployment-name",
        _ => "llama3.1"
    };

    public static string DefaultEndpoint(string provider) => provider switch
    {
        "OpenAI" => "https://api.openai.com/v1",
        "Google Gemini" => "https://generativelanguage.googleapis.com/v1beta",
        "Azure OpenAI" => "https://<resource>.openai.azure.com",
        _ => "http://localhost:11434"
    };

    public bool KeepHistory
    {
        get => Preferences.Default.Get(KeepHistoryKey, true);
        set => Preferences.Default.Set(KeepHistoryKey, value);
    }

    public bool AutoCleanCache
    {
        get => Preferences.Default.Get(AutoCleanKey, true);
        set => Preferences.Default.Set(AutoCleanKey, value);
    }

    public int HistoryRetentionDays
    {
        get => Preferences.Default.Get(HistoryRetentionDaysKey, 0);
        set
        {
            if (value is not (0 or 7 or 30 or 90))
                throw new ArgumentOutOfRangeException(nameof(value), "History retention must be Never, 7, 30, or 90 days.");
            Preferences.Default.Set(HistoryRetentionDaysKey, value);
        }
    }

    public string Provider
    {
        get => Preferences.Default.Get(ProviderKey, Providers[0]);
        set => Preferences.Default.Set(ProviderKey, value);
    }

    public string Model(string provider) => Preferences.Default.Get($"{ModelKey}_{provider}", DefaultModel(provider));
    public void SetModel(string provider, string value) => Preferences.Default.Set($"{ModelKey}_{provider}", value.Trim());

    public string Endpoint(string provider) => Preferences.Default.Get($"{EndpointKey}_{provider}", DefaultEndpoint(provider));
    public void SetEndpoint(string provider, string value) => Preferences.Default.Set($"{EndpointKey}_{provider}", value.Trim());

    public async Task<string> GetApiKeyAsync(string provider)
    {
        try
        {
            return await SecureStorage.Default.GetAsync($"ai_key_{provider}") ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public async Task SetApiKeyAsync(string provider, string key)
    {
        var name = $"ai_key_{provider}";
        if (string.IsNullOrWhiteSpace(key))
            SecureStorage.Default.Remove(name);
        else
            await SecureStorage.Default.SetAsync(name, key.Trim());
    }
}
