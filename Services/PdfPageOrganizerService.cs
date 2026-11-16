using System.Security.Cryptography;
using System.Text;
using MauiApp.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MauiApp.Services;

public sealed class PdfPageOrganizerService
{
    private readonly string _thumbnailDirectory = Path.Combine(FileSystem.CacheDirectory, "PageOrganizerThumbs");

    public int GetPageCount(string path)
    {
        using var document = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    public string RenderPage(string path, int pageIndex, DateTime sourceModifiedUtc)
    {
        Directory.CreateDirectory(_thumbnailDirectory);
        var keyData = Encoding.UTF8.GetBytes($"{path}|{sourceModifiedUtc.Ticks}|{pageIndex}");
        var key = Convert.ToHexString(SHA256.HashData(keyData));
        var target = Path.Combine(_thumbnailDirectory, key + ".png");
        if (File.Exists(target))
            return target;

        var thumbnail = PdfPageRenderService.RenderPage(path, pageIndex, 480);
        File.WriteAllBytes(target, thumbnail);
        return target;
    }

    public string SaveCopy(string sourcePath, IReadOnlyList<EditablePdfPage> pages)
    {
        if (pages.Count == 0)
            throw new InvalidOperationException("Keep at least one page in the document.");

        using var source = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();

        foreach (var item in pages)
        {
            if (item.OriginalIndex < 0 || item.OriginalIndex >= source.PageCount)
                throw new InvalidOperationException("A page reference is no longer valid. Reopen the original PDF and try again.");

            var page = output.AddPage(source.Pages[item.OriginalIndex]);
            page.Rotate = ((page.Rotate + item.Rotation) % 360 + 360) % 360;
        }

        return PdfOrganizeService.Save(output, HistoryService.OutputDirectory,
            $"{PdfOrganizeService.Name(sourcePath)}_edited");
    }
}
