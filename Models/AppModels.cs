namespace MauiApp.Models;

public sealed class StrokeCollection
{
    private readonly List<List<(float X, float Y)>> _strokes = [];

    public event Action? Changed;

    public IReadOnlyList<IReadOnlyList<(float X, float Y)>> Strokes => _strokes;
    public bool HasStrokes => _strokes.Any(s => s.Count > 0);

    public void Begin(float x, float y)
    {
        _strokes.Add([(x, y)]);
        Changed?.Invoke();
    }

    public void Extend(float x, float y)
    {
        if (_strokes.Count == 0)
            return;
        _strokes[^1].Add((x, y));
        Changed?.Invoke();
    }

    public void Clear()
    {
        _strokes.Clear();
        Changed?.Invoke();
    }

    public void TruncateTo(int count)
    {
        if (_strokes.Count > count)
            _strokes.RemoveRange(count, _strokes.Count - count);
        Changed?.Invoke();
    }
}

public sealed class HistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ToolId { get; set; } = string.Empty;
    public string ToolTitle { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Glyph { get; set; } = "📄";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public long InputBytes { get; set; }
    public long OutputBytes { get; set; }
    public List<string> Outputs { get; set; } = [];

    public string When => string.IsNullOrEmpty(UserEmail) ? Timestamp.ToString("MMM d, yyyy  h:mm tt") : $"{Timestamp:MMM d, yyyy  h:mm tt}  •  {UserEmail}";
    public string Summary => Outputs.Count == 1
        ? Path.GetFileName(Outputs[0])
        : $"{Outputs.Count} files";
    public bool OutputsExist => Outputs.Any(File.Exists);
}

public sealed record ToolUsage(string Title, string Glyph, int Count, double Fraction);

public sealed record UsageStats(int TotalOperations, int FilesCreated, long StorageBytes, long BytesSaved,
    IReadOnlyList<ToolUsage> TopTools);
