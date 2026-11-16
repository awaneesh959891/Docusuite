using MauiApp.Models;

namespace MauiApp.Services;

public interface IPdfOrganizeService
{
    string Merge(IReadOnlyList<string> inputs, string outputDir);
    IReadOnlyList<string> SplitByRanges(string input, string ranges, string outputDir);
    IReadOnlyList<string> SplitEvery(string input, int pagesPerFile, string outputDir);
    string RemovePages(string input, string ranges, string outputDir);
    string ExtractPages(string input, string ranges, string outputDir);
    string Copy(string input, string outputDir);
    int GetPageCount(string input);
}

public interface IPdfEditService
{
    // pageRanges: empty means all pages
    string Rotate(string input, int degrees, string pageRanges, string outputDir);
    string AddPageNumbers(string input, PageNumberOptions options, string outputDir);
    string AddWatermark(string input, WatermarkOptions options, string outputDir);
}

public interface IPdfOptimizeService
{
    string Compress(string input, CompressionLevel level, string outputDir);
}

public interface IPdfSecurityService
{
    bool IsEncrypted(string input);
    string Protect(string input, string userPassword, string? ownerPassword, EncryptionStrength strength, PdfPermissions permissions, string outputDir);
    string Unlock(string input, string? password, string outputDir);
    string Sign(string input, SignOptions options, string outputDir);
}

public interface IPdfConvertService
{
    string ImagesToPdf(IReadOnlyList<string> images, PageSizeOption size, bool landscape, double marginPoints, string outputDir);
}

public interface IScanService
{
    // Returns paths of processed JPEG files, in the same order as the inputs.
    IReadOnlyList<string> Process(IReadOnlyList<string> images, ScanFilter filter, bool autoCrop, string workDir);
}

public enum PageSizeOption
{
    A4,
    Letter,
    FitToImage
}
