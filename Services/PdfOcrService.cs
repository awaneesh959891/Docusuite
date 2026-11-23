using System.Text;

#if ANDROID
using Android.Graphics;
using Google.MLKit.Vision.Common;
using Google.MLKit.Vision.Text;
#endif

namespace MauiApp.Services;

public sealed class PdfOcrService : IPdfOcrService
{
    public async Task<string> ExtractTextAsync(string input, string outputDirectory)
    {
#if ANDROID
        if (!File.Exists(input))
            throw new FileNotFoundException("The selected PDF could not be found.", input);

        Directory.CreateDirectory(outputDirectory);
        var baseName = Path.GetFileNameWithoutExtension(input);
        var output = UniquePath(outputDirectory, $"{baseName}_ocr", ".txt");
        var temporary = output + ".tmp";
        using var recognizer = TextRecognition.GetClient(new TextRecognizerOptions.Builder().Build());
        try
        {
            var pageCount = await Task.Run(() => PdfPageRenderService.GetPageCount(input));
            if (pageCount < 1 || pageCount > 300)
                throw new InvalidOperationException("OCR supports PDF files containing 1 to 300 pages.");

            await using (var writer = new StreamWriter(new FileStream(temporary, FileMode.CreateNew,
                             FileAccess.Write, FileShare.None), new UTF8Encoding(false)))
            {
                for (var index = 0; index < pageCount; index++)
                {
                    var pageIndex = index;
                    var bytes = await Task.Run(() => PdfPageRenderService.RenderPage(input, pageIndex, 1800));
                    using var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length)
                        ?? throw new InvalidDataException($"Page {index + 1} could not be decoded for OCR.");
                    using var image = InputImage.FromBitmap(bitmap, 0);
                    var result = await recognizer.Process(image);
                    await writer.WriteLineAsync($"--- Page {index + 1} ---");
                    await writer.WriteLineAsync(result.Text ?? string.Empty);
                    await writer.WriteLineAsync();
                }
            }

            File.Move(temporary, output);
            return output;
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            throw;
        }
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Offline OCR is currently supported on Android only.");
#endif
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        var path = Path.Combine(directory, name + extension);
        for (var index = 1; File.Exists(path); index++)
            path = Path.Combine(directory, $"{name} ({index}){extension}");
        return path;
    }
}
