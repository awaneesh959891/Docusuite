using MauiApp.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MauiApp.Services;

public class PdfEditService : IPdfEditService
{
    public string Rotate(string input, int degrees, string pageRanges, string outputDir)
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        foreach (var number in Targets(pageRanges, doc.PageCount))
        {
            var page = doc.Pages[number - 1];
            page.Rotate = (((page.Rotate + degrees) % 360) + 360) % 360;
        }
        return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_rotated");
    }

    public string AddPageNumbers(string input, PageNumberOptions o, string outputDir)
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var font = PdfDrawing.Font(o.FontSize);
        var brush = new XSolidBrush(XColors.Black);
        var targets = Targets(o.PageRanges, doc.PageCount);

        var counter = o.StartNumber;
        var numbered = targets.Count(n => !(o.SkipFirstPage && n == 1));
        var lastNumber = o.StartNumber + numbered - 1;
        foreach (var number in Enumerable.Range(1, doc.PageCount))
        {
            if (!targets.Contains(number))
                continue;
            if (o.SkipFirstPage && number == 1)
                continue;

            var text = (string.IsNullOrWhiteSpace(o.Format) ? "{n}" : o.Format)
                .Replace("{n}", counter.ToString())
                .Replace("{total}", lastNumber.ToString());
            counter++;

            var page = doc.Pages[number - 1];
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var (w, h) = PdfDrawing.BeginVisible(gfx, page);
            var size = gfx.MeasureString(text, font);

            var x = o.Position switch
            {
                NumberPosition.BottomLeft or NumberPosition.TopLeft => o.Margin,
                NumberPosition.BottomRight or NumberPosition.TopRight => w - o.Margin - size.Width,
                _ => (w - size.Width) / 2
            };
            var y = o.Position is NumberPosition.TopLeft or NumberPosition.TopCenter or NumberPosition.TopRight
                ? o.Margin
                : h - o.Margin - size.Height;

            gfx.DrawString(text, font, brush, new XPoint(x, y), XStringFormats.TopLeft);
        }
        return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_numbered");
    }

    public string AddWatermark(string input, WatermarkOptions o, string outputDir)
    {
        var hasImage = o.ImageBytes is { Length: > 0 };
        if (!hasImage && string.IsNullOrWhiteSpace(o.Text))
            throw new InvalidOperationException("Enter watermark text or choose a logo image.");

        var keepAlive = new List<IDisposable>();
        try
        {
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var opacity = Math.Clamp(o.Opacity, 0.02, 1.0);
            XImage? logo = hasImage ? PdfDrawing.ImageFromBytes(ImageTools.WatermarkImage(o.ImageBytes!, opacity), keepAlive) : null;
            var font = PdfDrawing.Font(Math.Max(6, o.FontSize), bold: true);
            var brush = new XSolidBrush(XColor.FromArgb((int)(opacity * 255), 128, 128, 128));
            var targets = Targets(o.PageRanges, doc.PageCount);

            foreach (var number in targets)
            {
                var page = doc.Pages[number - 1];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var (w, h) = PdfDrawing.BeginVisible(gfx, page);

                double itemW, itemH;
                if (logo is not null)
                {
                    itemW = Math.Max(20, w * Math.Clamp(o.ImageWidthPercent, 5, 100) / 100);
                    itemH = itemW * logo.PixelHeight / logo.PixelWidth;
                }
                else
                {
                    var size = gfx.MeasureString(o.Text, font);
                    itemW = size.Width;
                    itemH = size.Height;
                }

                gfx.TranslateTransform(w / 2, h / 2);
                gfx.RotateTransform(-o.Rotation);

                void Draw(double cx, double cy)
                {
                    if (logo is not null)
                        gfx.DrawImage(logo, cx - itemW / 2, cy - itemH / 2, itemW, itemH);
                    else
                        gfx.DrawString(o.Text, font, brush, new XPoint(cx - itemW / 2, cy - itemH / 2), XStringFormats.TopLeft);
                }

                if (!o.Tile)
                {
                    Draw(0, 0);
                    continue;
                }

                var stepX = itemW * 1.5;
                var stepY = itemH * 2.5;
                var half = Math.Sqrt(w * w + h * h) / 2;
                var row = 0;
                for (var y = -half; y <= half; y += stepY, row++)
                    for (var x = -half + (row % 2 == 0 ? 0 : stepX / 2); x <= half; x += stepX)
                        Draw(x, y);
            }
            return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_watermarked");
        }
        finally
        {
            PdfDrawing.DisposeAll(keepAlive);
        }
    }

    public string Crop(string input, double marginPercent, string pageRanges, string outputDir)
    {
        var margin = Math.Clamp(marginPercent, 0, 40) / 100.0;
        if (margin <= 0)
            throw new InvalidOperationException("Choose a crop margin greater than zero.");

        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        var targets = Targets(pageRanges, doc.PageCount);
        if (targets.Count == 0)
            throw new InvalidOperationException("Select at least one page to crop.");

        foreach (var number in targets)
        {
            var page = doc.Pages[number - 1];
            var box = page.EffectiveCropBoxReadOnly;
            if (box.IsEmpty)
                box = page.MediaBoxReadOnly;
            if (box.IsEmpty || box.Width <= 0 || box.Height <= 0)
                throw new InvalidOperationException($"Page {number} has no usable page bounds.");

            var insetX = box.Width * margin;
            var insetY = box.Height * margin;
            page.CropBox = new PdfRectangle(
                new XPoint(box.X1 + insetX, box.Y1 + insetY),
                new XPoint(box.X2 - insetX, box.Y2 - insetY));
        }

        return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_cropped");
    }

    public string ClearBasicMetadata(string input, string outputDir)
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        foreach (var key in new[] { "/Title", "/Author", "/Subject", "/Keywords", "/Creator", "/Producer", "/CreationDate", "/ModDate", "/Trapped" })
            doc.Info.Elements.Remove(key);

        doc.Internals.Catalog.Elements.Remove("/Metadata");
        foreach (var page in doc.Pages)
            page.Elements.Remove("/Metadata");

        return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_metadata_cleared");
    }

    private static List<int> Targets(string ranges, int pageCount) =>
        string.IsNullOrWhiteSpace(ranges)
            ? Enumerable.Range(1, pageCount).ToList()
            : PageRangeParser.Parse(ranges, pageCount).Distinct().Order().ToList();
}

public class PdfConvertService : IPdfConvertService
{
    public string ImagesToPdf(IReadOnlyList<string> images, PageSizeOption size, bool landscape, double marginPoints, string outputDir)
    {
        if (images.Count == 0)
            throw new InvalidOperationException("Select at least one image.");

        var keepAlive = new List<IDisposable>();
        try
        {
            using var doc = new PdfDocument();
            foreach (var path in images)
            {
                var image = PdfDrawing.ImageFromBytes(ImageTools.NormalizeForPdf(path), keepAlive);
                var page = doc.AddPage();
                var margin = marginPoints;

                double imgW = image.PixelWidth * 72.0 / Math.Max(1, image.HorizontalResolution);
                double imgH = image.PixelHeight * 72.0 / Math.Max(1, image.VerticalResolution);

                if (size == PageSizeOption.FitToImage)
                {
                    var shrink = Math.Min(1.0, 1200.0 / Math.Max(imgW, imgH));
                    imgW *= shrink;
                    imgH *= shrink;
                    page.Width = XUnit.FromPoint(imgW);
                    page.Height = XUnit.FromPoint(imgH);
                    margin = 0;
                }
                else
                {
                    page.Size = size == PageSizeOption.A4 ? PdfSharp.PageSize.A4 : PdfSharp.PageSize.Letter;
                    page.Orientation = landscape ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
                }

                var availW = Math.Max(10, page.Width.Point - 2 * margin);
                var availH = Math.Max(10, page.Height.Point - 2 * margin);
                var scale = Math.Min(availW / imgW, availH / imgH);
                var w = imgW * scale;
                var h = imgH * scale;

                using var gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, (page.Width.Point - w) / 2, (page.Height.Point - h) / 2, w, h);
            }
            return PdfDrawing.Save(doc, outputDir, "images");
        }
        finally
        {
            PdfDrawing.DisposeAll(keepAlive);
        }
    }
}
