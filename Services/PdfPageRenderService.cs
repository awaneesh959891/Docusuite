using PDFtoImage;
using SkiaSharp;

#if ANDROID
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
#endif

namespace MauiApp.Services;

public static class PdfPageRenderService
{
    public static int GetPageCount(string path)
    {
#if ANDROID
        using var descriptor = ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly);
        using var renderer = new PdfRenderer(descriptor);
        return renderer.PageCount;
#else
        using var stream = File.OpenRead(path);
        return Conversion.GetPageCount(stream);
#endif
    }

    public static byte[] RenderPage(string path, int pageIndex, int width)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Render width must be positive.");

#if ANDROID
        using var descriptor = ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly);
        using var renderer = new PdfRenderer(descriptor);
        if (pageIndex < 0 || pageIndex >= renderer.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "The requested PDF page does not exist.");

        using var page = renderer.OpenPage(pageIndex);
        var height = Math.Max(1, (int)Math.Round(width * page.Height / (double)page.Width));
        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);

        using var output = new MemoryStream();
        if (!bitmap.Compress(Bitmap.CompressFormat.Png, 100, output))
            throw new InvalidOperationException("The PDF page could not be encoded as an image.");
        return output.ToArray();
#else
        using var stream = File.OpenRead(path);
        using var bitmap = Conversion.ToImage(stream, new Index(pageIndex), options: new RenderOptions(Width: width));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
#endif
    }

    public static IReadOnlyList<string> ExportPngPages(string path, string outputDirectory, int width = 1600) =>
        ExportPages(path, outputDirectory, width, SKEncodedImageFormat.Png, 100, ".png");

    public static IReadOnlyList<string> ExportJpgPages(string path, string outputDirectory, int width = 1600, int quality = 92) =>
        ExportPages(path, outputDirectory, width, SKEncodedImageFormat.Jpeg, Math.Clamp(quality, 1, 100), ".jpg");

    private static IReadOnlyList<string> ExportPages(string path, string outputDirectory, int width,
        SKEncodedImageFormat format, int quality, string extension)
    {
        Directory.CreateDirectory(outputDirectory);
        var pageCount = GetPageCount(path);
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var outputs = new List<string>(pageCount);
        for (var index = 0; index < pageCount; index++)
        {
            var target = UniquePath(outputDirectory, $"{name}_page_{index + 1:D3}", extension);
            var rendered = RenderPage(path, index, width);
            if (format == SKEncodedImageFormat.Png)
                File.WriteAllBytes(target, rendered);
            else
            {
                using var bitmap = SKBitmap.Decode(rendered)
                    ?? throw new InvalidOperationException($"Page {index + 1} could not be decoded for JPEG export.");
                File.WriteAllBytes(target, ImageTools.Encode(bitmap, format, quality));
            }
            outputs.Add(target);
        }
        return outputs;
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        var path = System.IO.Path.Combine(directory, name + extension);
        for (var index = 1; File.Exists(path); index++)
            path = System.IO.Path.Combine(directory, $"{name} ({index}){extension}");
        return path;
    }
}
