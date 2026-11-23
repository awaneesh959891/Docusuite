using PDFtoImage;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace MauiApp.Services;

public sealed class PdfRecoveryService : IPdfRecoveryService
{
    private const int MaximumPages = 300;
    private const int RenderWidth = 1400;
    private const double OutputPageWidth = 595;

    public string RebuildFromRenderedPages(string input, string outputDirectory)
    {
        if (!File.Exists(input))
            throw new FileNotFoundException("The selected PDF could not be found.", input);

        int pageCount;
        using (var stream = File.OpenRead(input))
            pageCount = Conversion.GetPageCount(stream);
        if (pageCount < 1)
            throw new InvalidDataException("The PDF renderer did not find any recoverable pages.");
        if (pageCount > MaximumPages)
            throw new InvalidOperationException($"Recovery is limited to {MaximumPages} pages at a time.");

        Directory.CreateDirectory(outputDirectory);
        var outputPath = PdfOrganizeService.UniquePath(outputDirectory,
            $"{PdfOrganizeService.Name(input)}_repaired");

        try
        {
            using (var output = new PdfDocument())
            {
                for (var index = 0; index < pageCount; index++)
                {
                    byte[] imageBytes;
                    using (var stream = File.OpenRead(input))
                    using (var bitmap = Conversion.ToImage(stream, new Index(index),
                               options: new RenderOptions(Width: RenderWidth)))
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var encoded = image.Encode(SKEncodedImageFormat.Png, 100))
                        imageBytes = encoded.ToArray();

                    var resources = new List<IDisposable>();
                    try
                    {
                        var pageImage = PdfDrawing.ImageFromBytes(imageBytes, resources);
                        var pageHeight = OutputPageWidth * pageImage.PixelHeight / Math.Max(1.0, pageImage.PixelWidth);
                        var page = output.AddPage();
                        page.Width = XUnit.FromPoint(OutputPageWidth);
                        page.Height = XUnit.FromPoint(pageHeight);
                        using var graphics = XGraphics.FromPdfPage(page);
                        graphics.DrawRectangle(XBrushes.White, 0, 0, page.Width.Point, page.Height.Point);
                        graphics.DrawImage(pageImage, 0, 0, page.Width.Point, page.Height.Point);
                    }
                    finally
                    {
                        PdfDrawing.DisposeAll(resources);
                    }
                }
                output.Save(outputPath);
            }

            using var validation = PdfReader.Open(outputPath, PdfDocumentOpenMode.Import);
            if (validation.PageCount != pageCount)
                throw new InvalidDataException("The rebuilt file did not pass page-count validation.");
            return outputPath;
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
    }
}
