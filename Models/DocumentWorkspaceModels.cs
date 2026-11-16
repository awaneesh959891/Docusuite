using CommunityToolkit.Mvvm.ComponentModel;
using MauiApp.Services;

namespace MauiApp.Models;

public enum DocumentSortOption
{
    ModifiedNewest,
    Name,
    Size,
    PageCount
}

public sealed partial class WorkspaceDocument : ObservableObject
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string FolderName { get; init; }
    public required DateTime ModifiedUtc { get; init; }
    public required long SizeBytes { get; init; }
    public required int PageCount { get; init; }
    public required bool IsEncrypted { get; init; }
    public DateTime? LastOpenedUtc { get; init; }
    public string? ThumbnailPath { get; init; }

    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _tag = "Other";

    public string Glyph => Path.GetExtension(FilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "📕" : "🖼️";
    public string ModifiedLabel => ModifiedUtc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
    public string Details =>
        $"{PageCount} page{(PageCount == 1 ? string.Empty : "s")} • {Formatting.Bytes(SizeBytes)}" +
        (IsEncrypted ? " • 🔒" : string.Empty);
}

public sealed record ContinueEditingItem(
    string ToolTitle,
    string FilePath,
    DateTime Timestamp,
    string Glyph)
{
    public string FileName => Path.GetFileName(FilePath);
    public string When => Timestamp.ToLocalTime().ToString("MMM d, h:mm tt");
}
