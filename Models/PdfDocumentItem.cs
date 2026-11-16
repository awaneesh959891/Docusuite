using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp.Models;

public partial class PdfDocumentItem : ObservableObject
{
    public required string FilePath { get; init; }
    public string FileName => DisplayName(FilePath);

    public static string DisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return name.Length > 33 && name[32] == '_' && name[..32].All(Uri.IsHexDigit) ? name[33..] : name;
    }
    public int PageCount { get; init; }
    public long SizeBytes { get; init; }
    public bool IsImage { get; init; }
    public bool IsEncrypted { get; init; }
    public string Glyph => IsImage ? "🖼️" : "📕";

    public string Details
    {
        get
        {
            var size = SizeBytes switch
            {
                < 1024 => $"{SizeBytes} B",
                < 1024 * 1024 => $"{SizeBytes / 1024.0:0.#} KB",
                _ => $"{SizeBytes / 1024.0 / 1024.0:0.#} MB"
            };
            return IsEncrypted ? $"🔒 Password protected • {size}"
                : IsImage ? size : $"{PageCount} page{(PageCount == 1 ? "" : "s")} • {size}";
        }
    }
}
