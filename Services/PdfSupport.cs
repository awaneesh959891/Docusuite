using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MauiApp.Services;

// PDFsharp (core build) has no system fonts on mobile, so fonts are supplied from app assets.
public sealed class PdfFontResolver : IFontResolver
{
    public const string Regular = "regular";
    public const string Bold = "bold";

    private readonly Func<string, byte[]> _loader;
    private readonly Dictionary<string, byte[]> _cache = [];

    private PdfFontResolver(Func<string, byte[]> loader) => _loader = loader;

    // faceName is "regular" or "bold"; the loader maps it to font bytes.
    public static void Install(Func<string, byte[]> loader)
    {
        if (GlobalFontSettings.FontResolver is null)
            GlobalFontSettings.FontResolver = new PdfFontResolver(loader);
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? Bold : Regular);

    public byte[]? GetFont(string faceName)
    {
        lock (_cache)
        {
            if (!_cache.TryGetValue(faceName, out var data))
                _cache[faceName] = data = _loader(faceName);
            return data;
        }
    }
}

public static class PdfErrors
{
    public static string Friendly(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("password", StringComparison.OrdinalIgnoreCase))
            return "This PDF is password protected. Use Unlock PDF first, or enter the correct password.";
        if (ex is PdfReaderException)
            return "This file couldn't be read as a PDF. It may be damaged or not a PDF.";
        return message;
    }
}

internal static class PdfDrawing
{
    public static XFont Font(double size, bool bold = false) =>
        new("App", size, bold ? XFontStyleEx.Bold : XFontStyleEx.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));

    // Maps drawing coordinates onto the page as the user sees it, honouring /Rotate. Returns the visible size.
    public static (double Width, double Height) BeginVisible(XGraphics gfx, PdfPage page)
    {
        double w = page.Width.Point, h = page.Height.Point;
        switch (((page.Rotate % 360) + 360) % 360)
        {
            case 90:
                gfx.TranslateTransform(0, h);
                gfx.RotateTransform(-90);
                return (h, w);
            case 180:
                gfx.TranslateTransform(w, h);
                gfx.RotateTransform(180);
                return (w, h);
            case 270:
                gfx.TranslateTransform(w, 0);
                gfx.RotateTransform(90);
                return (h, w);
            default:
                return (w, h);
        }
    }

    public static string Save(PdfDocument doc, string outputDir, string baseName) =>
        PdfOrganizeService.Save(doc, outputDir, baseName);

    public static XImage ImageFromBytes(byte[] data, List<IDisposable> keepAlive)
    {
        var stream = new MemoryStream(data);
        keepAlive.Add(stream);
        var image = XImage.FromStream(stream);
        keepAlive.Add(image);
        return image;
    }

    public static void DisposeAll(List<IDisposable> items)
    {
        foreach (var item in items)
            item.Dispose();
    }
}
