using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace MauiApp.Services;

public static class PageRangeParser
{
    // Accepts "1-3,5,8-" (1-based). Open ends mean first/last page. Order is preserved.
    public static List<int> Parse(string text, int pageCount)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(text))
            return result;

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.AddRange(ParseSegment(part, pageCount));
        }
        return result;
    }

    public static List<int> ParseSegment(string part, int pageCount)
    {
        var bounds = part.Split('-', StringSplitOptions.TrimEntries);
        int start, end;
        if (bounds.Length == 1)
        {
            start = end = ParsePage(bounds[0], pageCount);
        }
        else if (bounds.Length == 2)
        {
            start = bounds[0].Length == 0 ? 1 : ParsePage(bounds[0], pageCount);
            end = bounds[1].Length == 0 ? pageCount : ParsePage(bounds[1], pageCount);
        }
        else
        {
            throw new FormatException($"Invalid page range '{part}'.");
        }

        if (start > end)
            throw new FormatException($"Invalid page range '{part}'.");

        return Enumerable.Range(start, end - start + 1).ToList();
    }

    private static int ParsePage(string s, int pageCount)
    {
        if (!int.TryParse(s, out var page))
            throw new FormatException($"'{s}' is not a valid page number.");
        if (page < 1 || page > pageCount)
            throw new FormatException($"Page {page} is outside the document (1-{pageCount}).");
        return page;
    }
}

public class PdfOrganizeService : IPdfOrganizeService
{
    public int GetPageCount(string input)
    {
        using var doc = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    public string Merge(IReadOnlyList<string> inputs, string outputDir)
    {
        if (inputs.Count < 2)
            throw new InvalidOperationException("Select at least two PDF files to merge.");

        using var output = new PdfDocument();
        foreach (var path in inputs)
        {
            using var source = PdfReader.Open(path, PdfDocumentOpenMode.Import);
            foreach (PdfPage page in source.Pages)
                output.AddPage(page);
        }
        return Save(output, outputDir, "merged");
    }

    public IReadOnlyList<string> SplitByRanges(string input, string ranges, string outputDir)
    {
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var segments = ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
            throw new InvalidOperationException("Enter at least one page range, e.g. 1-3, 5, 7-9.");

        var files = new List<string>();
        foreach (var segment in segments)
        {
            var pages = PageRangeParser.ParseSegment(segment, source.PageCount);
            files.Add(SavePages(source, pages, outputDir, $"{Name(input)}_{segment.Replace(' ', '_')}"));
        }
        return files;
    }

    public IReadOnlyList<string> SplitEvery(string input, int pagesPerFile, string outputDir)
    {
        if (pagesPerFile < 1)
            throw new InvalidOperationException("Pages per file must be at least 1.");

        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var files = new List<string>();
        for (int start = 1; start <= source.PageCount; start += pagesPerFile)
        {
            var end = Math.Min(start + pagesPerFile - 1, source.PageCount);
            var pages = Enumerable.Range(start, end - start + 1).ToList();
            files.Add(SavePages(source, pages, outputDir, $"{Name(input)}_{start}-{end}"));
        }
        return files;
    }

    public string RemovePages(string input, string ranges, string outputDir)
    {
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var toRemove = PageRangeParser.Parse(ranges, source.PageCount).ToHashSet();
        if (toRemove.Count == 0)
            throw new InvalidOperationException("Enter the pages to remove, e.g. 2, 4-6.");

        var keep = Enumerable.Range(1, source.PageCount).Where(p => !toRemove.Contains(p)).ToList();
        if (keep.Count == 0)
            throw new InvalidOperationException("You can't remove every page of the document.");

        return SavePages(source, keep, outputDir, $"{Name(input)}_removed");
    }

    public string ExtractPages(string input, string ranges, string outputDir)
    {
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
        var pages = PageRangeParser.Parse(ranges, source.PageCount);
        if (pages.Count == 0)
            throw new InvalidOperationException("Enter the pages to extract, e.g. 1-3, 7.");

        return SavePages(source, pages, outputDir, $"{Name(input)}_extract");
    }

    public string Copy(string input, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var target = UniquePath(outputDir, $"{Name(input)}_copy");
        File.Copy(input, target);
        return target;
    }

    private static string SavePages(PdfDocument source, IEnumerable<int> pages, string outputDir, string baseName)
    {
        using var output = new PdfDocument();
        foreach (var number in pages)
            output.AddPage(source.Pages[number - 1]);
        return Save(output, outputDir, baseName);
    }

    internal static string Save(PdfDocument doc, string outputDir, string baseName)
    {
        Directory.CreateDirectory(outputDir);
        var path = UniquePath(outputDir, baseName);
        doc.Save(path);
        return path;
    }

    internal static string Name(string path) => Path.GetFileNameWithoutExtension(path);

    internal static string UniquePath(string dir, string baseName)
    {
        var path = Path.Combine(dir, baseName + ".pdf");
        for (int i = 1; File.Exists(path); i++)
            path = Path.Combine(dir, $"{baseName} ({i}).pdf");
        return path;
    }
}

