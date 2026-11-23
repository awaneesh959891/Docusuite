using MauiApp.Models;

namespace MauiApp.Services;

public static class ToolCatalog
{
    private const string Red = "#EF4444", Orange = "#F97316", Green = "#10B981", Blue = "#3B82F6",
        Purple = "#8B5CF6", Pink = "#EC4899", Teal = "#14B8A6", Amber = "#F59E0B", Indigo = "#6366F1";

    public static IReadOnlyList<PdfTool> All { get; } = Build();

    public static PdfTool? Find(string id) => All.FirstOrDefault(t => t.Id == id);

    public static string CategoryTitle(PdfToolCategory c) => c switch
    {
        PdfToolCategory.Organize => "Organize PDF",
        PdfToolCategory.Optimize => "Optimize PDF",
        PdfToolCategory.ConvertToPdf => "Convert to PDF",
        PdfToolCategory.ConvertFromPdf => "Convert from PDF",
        PdfToolCategory.Edit => "Edit PDF",
        PdfToolCategory.Security => "PDF Security",
        _ => "AI & Productivity"
    };

    private static PdfTool T(string id, string title, string desc, string glyph, string color, PdfToolCategory cat,
        PdfToolKind kind = PdfToolKind.ComingSoon) =>
        new() { Id = id, Title = title, Description = desc, Glyph = glyph, AccentColor = color, Category = cat, Kind = kind };

    private static List<PdfTool> Build() =>
    [
        T("merge", "Merge PDF", "Combine files in your order", "🔗", Red, PdfToolCategory.Organize, PdfToolKind.Merge),
        T("split", "Split PDF", "By ranges or fixed page count", "✂️", Orange, PdfToolCategory.Organize, PdfToolKind.Split),
        T("remove", "Remove Pages", "Delete unwanted pages", "🗑️", Pink, PdfToolCategory.Organize, PdfToolKind.RemovePages),
        T("extract", "Extract Pages", "Pull pages into a new file", "📤", Green, PdfToolCategory.Organize, PdfToolKind.ExtractPages),
        T("organize", "Organize PDF", "Reorder, rotate, duplicate, delete and save pages as a copy", "🧩", Indigo, PdfToolCategory.Organize, PdfToolKind.Organize),
        T("scan", "Scan to PDF", "Camera capture & crop", "📷", Teal, PdfToolCategory.Organize, PdfToolKind.Scan),

                T("compress", "Compress PDF", "Shrink file size", "🗜️", Green, PdfToolCategory.Optimize, PdfToolKind.Compress),
        T("repair", "Repair PDF", "Rebuild pages that the local PDF renderer can still recover", "🛠️", Amber, PdfToolCategory.Optimize, PdfToolKind.Repair),
        T("ocr", "OCR PDF", "Recognize text offline on Android and save it as a text file", "🔍", Blue, PdfToolCategory.Optimize, PdfToolKind.Ocr),

        T("jpg2pdf", "JPG to PDF", "Images into a document", "🖼️", Amber, PdfToolCategory.ConvertToPdf, PdfToolKind.ImagesToPdf),
        T("word2pdf", "Word to PDF", "DOC & DOCX", "📘", Blue, PdfToolCategory.ConvertToPdf),
        T("ppt2pdf", "PowerPoint to PDF", "PPT & PPTX", "📙", Orange, PdfToolCategory.ConvertToPdf),
        T("excel2pdf", "Excel to PDF", "XLS & XLSX", "📗", Green, PdfToolCategory.ConvertToPdf),
        T("html2pdf", "HTML to PDF", "Pages & URLs", "🌐", Purple, PdfToolCategory.ConvertToPdf),

        T("pdf2png", "PDF to PNG", "Export each page as a high-quality image", "🏞️", Amber, PdfToolCategory.ConvertFromPdf, PdfToolKind.PdfToPng),
        T("pdf2jpg", "PDF to JPG", "Export each page as a JPEG image", "🖼️", Orange, PdfToolCategory.ConvertFromPdf, PdfToolKind.PdfToJpg),
        T("pdf2word", "PDF to Word", "Editable DOCX", "📘", Blue, PdfToolCategory.ConvertFromPdf),
        T("pdf2ppt", "PDF to PowerPoint", "Editable slides", "📙", Orange, PdfToolCategory.ConvertFromPdf),
        T("pdf2excel", "PDF to Excel", "Tables to XLSX", "📗", Green, PdfToolCategory.ConvertFromPdf),
        T("pdf2pdfa", "PDF to PDF/A", "Archival format", "🏛️", Indigo, PdfToolCategory.ConvertFromPdf),
        T("pdf2md", "PDF to Markdown", "Clean .md text", "📝", Teal, PdfToolCategory.ConvertFromPdf),

        T("edit", "Edit PDF", "Text, shapes & annotations", "✏️", Purple, PdfToolCategory.Edit),
        T("rotate", "Rotate PDF", "90°, 180° or 270°", "🔄", Blue, PdfToolCategory.Edit, PdfToolKind.Rotate),
        T("numbers", "Page Numbers", "Add numbering", "🔢", Teal, PdfToolCategory.Edit, PdfToolKind.PageNumbers),
        T("watermark", "Watermark", "Text or logo stamp", "💧", Cyan(), PdfToolCategory.Edit, PdfToolKind.Watermark),
        T("crop", "Crop PDF", "Visually trim margins; content outside remains recoverable", "📐", Orange, PdfToolCategory.Edit, PdfToolKind.Crop),
        T("forms", "PDF Forms", "Create & fill forms", "📋", Green, PdfToolCategory.Edit),

        T("metadata", "Clear Basic Metadata", "Remove common document info fields only", "🧹", Teal, PdfToolCategory.Security, PdfToolKind.ClearMetadata),
        T("unlock", "Unlock PDF", "Remove passwords", "🔓", Green, PdfToolCategory.Security, PdfToolKind.Unlock),
        T("protect", "Protect PDF", "AES encryption", "🔒", Red, PdfToolCategory.Security, PdfToolKind.Protect),
        T("sign", "Sign PDF", "Draw, type or upload", "🖋️", Indigo, PdfToolCategory.Security, PdfToolKind.Sign),
        T("redact", "Redact PDF", "Permanently black out", "⬛", Gray(), PdfToolCategory.Security),
        T("compare", "Compare PDF", "Spot the differences", "⚖️", Purple, PdfToolCategory.Security),

        T("summarize", "AI Summarizer", "Key takeaways", "✨", Purple, PdfToolCategory.AiProductivity),
        T("translate", "Translate PDF", "Keep the layout", "🌍", Blue, PdfToolCategory.AiProductivity),
        T("workflows", "Workflows", "Chain tools in one tap", "⚡", Amber, PdfToolCategory.AiProductivity),
        T("copy", "Copy PDF", "Duplicate before editing", "📄", Teal, PdfToolCategory.AiProductivity, PdfToolKind.Copy),
    ];

    private static string Cyan() => "#06B6D4";
    private static string Gray() => "#64748B";
}
