using MauiApp.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SkiaSharp;

namespace MauiApp.Services;

public class PdfOptimizeService : IPdfOptimizeService
{
    private const int MinImageBytes = 20 * 1024;

    public string Compress(string input, CompressionLevel level, string outputDir)
    {
        var (quality, maxSide) = level switch
        {
            CompressionLevel.Low => (85, 2600),
            CompressionLevel.Medium => (70, 1700),
            _ => (50, 1100)
        };

        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        foreach (var obj in doc.Internals.GetAllObjects())
        {
            if (obj is PdfDictionary dict && dict.Stream is not null)
                TryRecompress(dict, quality, maxSide);
        }

        doc.Options.CompressContentStreams = true;
        doc.Options.NoCompression = false;
        doc.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;

        var output = PdfDrawing.Save(doc, outputDir, $"{PdfOrganizeService.Name(input)}_compressed");

        // Never hand back something bigger than what the user started with.
        if (new FileInfo(output).Length >= new FileInfo(input).Length)
            File.Copy(input, output, overwrite: true);
        return output;
    }

    private static void TryRecompress(PdfDictionary dict, int quality, int maxSide)
    {
        try
        {
            var elements = dict.Elements;
            if (elements.GetName("/Subtype") != "/Image"
                || elements.GetName("/ColorSpace") != "/DeviceRGB"
                || elements.ContainsKey("/Mask")
                || !IsPlainDct(elements["/Filter"]))
                return;

            var original = dict.Stream.Value;
            if (original is null || original.Length < MinImageBytes)
                return;

            using var bmp = SKBitmap.Decode(original);
            if (bmp is null)
                return;

            using var scaled = ImageTools.Downscale(bmp.Copy(), maxSide);
            byte[]? best = null;
            foreach (var q in new[] { quality, quality - 15, quality - 30 })
            {
                var candidate = ImageTools.Encode(scaled, SKEncodedImageFormat.Jpeg, Math.Max(15, q));
                if (candidate.Length < original.Length * 0.95)
                {
                    best = candidate;
                    break;
                }
            }
            if (best is null)
                return;

            dict.Stream.Value = best;
            elements.SetInteger("/Width", scaled.Width);
            elements.SetInteger("/Height", scaled.Height);
            elements.SetInteger("/BitsPerComponent", 8);
            elements.Remove("/DecodeParms");
        }
        catch
        {
            // Leave images we can't safely re-encode untouched.
        }
    }

    private static bool IsPlainDct(PdfItem? filter) => filter switch
    {
        PdfName name => name.Value == "/DCTDecode",
        PdfArray { Elements.Count: 1 } array => array.Elements[0] is PdfName n && n.Value == "/DCTDecode",
        _ => false
    };
}
