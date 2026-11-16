namespace MauiApp.Models;

public enum PdfToolCategory
{
    Organize,
    Optimize,
    ConvertToPdf,
    ConvertFromPdf,
    Edit,
    Security,
    AiProductivity
}

public enum PdfToolKind
{
    Merge,
    Split,
    RemovePages,
    ExtractPages,
    Rotate,
    Copy,
    ImagesToPdf,
    PdfToPng,
    Compress,
    PageNumbers,
    Watermark,
    Protect,
    Unlock,
    Sign,
    Scan,
    ComingSoon
}

public class PdfTool
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }
    public required string AccentColor { get; init; }
    public required PdfToolCategory Category { get; init; }
    public PdfToolKind Kind { get; init; } = PdfToolKind.ComingSoon;
    public bool IsAvailable => Kind != PdfToolKind.ComingSoon;
    public double CardOpacity => IsAvailable ? 1.0 : 0.55;
}

public class ToolCard(PdfTool tool, string? lockedReason)
{
    public PdfTool Tool { get; } = tool;
    public string Title => Tool.Title;
    public string Description => IsLocked ? "Unavailable right now" : Tool.Description;
    public string Glyph => IsLocked ? "🔒" : Tool.Glyph;
    public string AccentColor => IsLocked ? "#94A3B8" : Tool.AccentColor;
    public string? LockedReason { get; } = lockedReason;
    public bool IsLocked => LockedReason is not null;
    public double CardOpacity => IsLocked || !Tool.IsAvailable ? 0.55 : 1.0;
}

public class PdfToolGroup : List<ToolCard>
{
    public string Name { get; }

    public PdfToolGroup(string name, IEnumerable<ToolCard> tools) : base(tools) => Name = name;
}
