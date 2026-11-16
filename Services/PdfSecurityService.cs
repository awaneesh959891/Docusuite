using MauiApp.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace MauiApp.Services;

public class PdfSecurityService : IPdfSecurityService
{
    public bool IsEncrypted(string input)
    {
        try
        {
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            return doc.SecuritySettings.IsEncrypted;
        }
        catch (PdfReaderException)
        {
            return true;
        }
    }

    public string Protect(string input, string userPassword, string? ownerPassword, EncryptionStrength strength,
        PdfPermissions permissions, string outputDir)
    {
        if (string.IsNullOrEmpty(userPassword))
            throw new InvalidOperationException("Enter a password to protect the PDF.");
        if (IsEncrypted(input))
            throw new InvalidOperationException("This PDF is already protected. Unlock it first.");

        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        if (strength == EncryptionStrength.Aes256)
            doc.SecurityHandler.SetEncryptionToV5(true);
        else
            doc.SecurityHandler.SetEncryptionToV4UsingAES(true);

        var security = doc.SecuritySettings;
        security.UserPassword = userPassword;
        security.OwnerPassword = string.IsNullOrEmpty(ownerPassword) ? userPassword + "#owner" : ownerPassword;
        security.PermitPrint = permissions.AllowPrint;
        security.PermitFullQualityPrint = permissions.AllowPrint;
        security.PermitExtractContent = permissions.AllowCopy;
        security.PermitModifyDocument = permissions.AllowEdit;
        security.PermitAssembleDocument = permissions.AllowEdit;
        security.PermitAnnotations = permissions.AllowAnnotate;
        security.PermitFormsFill = permissions.AllowAnnotate;
        return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_protected");
    }

    public string Unlock(string input, string? password, string outputDir)
    {
        password ??= string.Empty;
        var name = $"{PdfOrganizeService.Name(input)}_unlocked";

        // The owner password allows in-place modification; a user password only allows reading,
        // in which case the pages are copied into a fresh, unencrypted document.
        try
        {
            using var doc = PdfReader.Open(input, password, PdfDocumentOpenMode.Modify);
            doc.SecurityHandler.SetEncryptionToNoneAndResetPasswords();
            return PdfDrawing.Save(doc, outputDir, name);
        }
        catch (PdfReaderException)
        {
        }

        try
        {
            using var source = PdfReader.Open(input, password, PdfDocumentOpenMode.Import);
            using var copy = new PdfDocument();
            copy.Info.Title = source.Info.Title;
            copy.Info.Author = source.Info.Author;
            foreach (PdfPage page in source.Pages)
                copy.AddPage(page);
            return PdfDrawing.Save(copy, outputDir, name);
        }
        catch (PdfReaderException)
        {
            throw new InvalidOperationException(string.IsNullOrEmpty(password)
                ? "This PDF needs a password. Enter it and try again."
                : "That password is incorrect.");
        }
    }

    // Visual signature stamp with an optional audit line. Not a cryptographic (X.509) signature.
    public string Sign(string input, SignOptions o, string outputDir)
    {
        if (o.SignaturePng.Length == 0)
            throw new InvalidOperationException("Create a signature first.");

        var keepAlive = new List<IDisposable>();
        try
        {
            using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
            var signature = PdfDrawing.ImageFromBytes(o.SignaturePng, keepAlive);
            var font = PdfDrawing.Font(7);
            var brush = new XSolidBrush(XColor.FromArgb(255, 70, 70, 70));

            var pages = o.AllPages
                ? Enumerable.Range(1, doc.PageCount).ToList()
                : [o.Page <= 0 ? doc.PageCount : o.Page];
            if (pages.Any(p => p < 1 || p > doc.PageCount))
                throw new InvalidOperationException($"Page {o.Page} is outside the document (1-{doc.PageCount}).");

            var stamp = new List<string>();
            if (!string.IsNullOrWhiteSpace(o.SignerName))
                stamp.Add($"Signed by {o.SignerName.Trim()}");
            if (o.AddTimestamp)
                stamp.Add(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"));
            var caption = string.Join("  •  ", stamp);

            foreach (var number in pages)
            {
                var page = doc.Pages[number - 1];
                using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                var (w, h) = PdfDrawing.BeginVisible(gfx, page);

                const double margin = 36;
                var captionH = caption.Length > 0 ? 12 : 0;
                var width = Math.Max(40, w * Math.Clamp(o.WidthPercent, 8, 80) / 100);
                var height = width * signature.PixelHeight / signature.PixelWidth;
                var maxH = h * 0.25;
                if (height > maxH)
                {
                    width *= maxH / height;
                    height = maxH;
                }

                var x = o.Position switch
                {
                    SignaturePosition.BottomLeft or SignaturePosition.TopLeft => margin,
                    SignaturePosition.BottomRight or SignaturePosition.TopRight => w - margin - width,
                    _ => (w - width) / 2
                };
                var y = o.Position switch
                {
                    SignaturePosition.TopLeft or SignaturePosition.TopCenter or SignaturePosition.TopRight => margin,
                    SignaturePosition.Center => (h - height - captionH) / 2,
                    _ => h - margin - height - captionH
                };

                gfx.DrawImage(signature, x, y, width, height);
                if (caption.Length > 0)
                {
                    var textW = gfx.MeasureString(caption, font).Width;
                    gfx.DrawString(caption, font, brush, new XPoint(x + Math.Max(0, (width - textW) / 2), y + height + 2), XStringFormats.TopLeft);
                }
            }
            return PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_signed");
        }
        finally
        {
            PdfDrawing.DisposeAll(keepAlive);
        }
    }
}

public class ScanService : IScanService
{
    public IReadOnlyList<string> Process(IReadOnlyList<string> images, ScanFilter filter, bool autoCrop, string workDir)
    {
        Directory.CreateDirectory(workDir);
        var outputs = new List<string>();
        foreach (var path in images)
        {
            var bmp = ImageTools.Downscale(ImageTools.DecodeOriented(File.ReadAllBytes(path)), 2200);
            if (autoCrop)
                bmp = ImageTools.AutoCrop(bmp);
            bmp = ImageTools.ApplyFilter(bmp, filter);

            var target = Path.Combine(workDir, $"scan_{Guid.NewGuid():N}.jpg");
            File.WriteAllBytes(target, ImageTools.Encode(bmp, SkiaSharp.SKEncodedImageFormat.Jpeg, 88));
            bmp.Dispose();
            outputs.Add(target);
        }
        return outputs;
    }
}
