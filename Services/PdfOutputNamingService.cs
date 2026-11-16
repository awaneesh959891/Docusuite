namespace MauiApp.Services;

public sealed class PdfOutputNamingService
{
    private const string PatternPreferenceKey = "pdf_output_name_pattern";
    public const string DefaultPattern = "{OriginalName}_{Operation}_{Date}_{Counter}";

    public string Pattern
    {
        get => Preferences.Default.Get(PatternPreferenceKey, DefaultPattern);
        set => Preferences.Default.Set(PatternPreferenceKey, value);
    }

    public static string? Validate(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;
        var expanded = pattern.Replace("{OriginalName}", "document", StringComparison.OrdinalIgnoreCase)
            .Replace("{Operation}", "compress", StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", "20261116", StringComparison.OrdinalIgnoreCase)
            .Replace("{Counter}", "001", StringComparison.OrdinalIgnoreCase);
        return expanded.Contains('{') || expanded.Contains('}')
            ? "Use only {OriginalName}, {Operation}, {Date}, and {Counter} in the output-name pattern."
            : null;
    }

    public IReadOnlyList<string> Apply(IReadOnlyList<string> outputs, string sourcePath, string operationId, string pattern, int counterOffset = 0)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return outputs;
        if (Validate(pattern) is { } problem)
            throw new InvalidOperationException(problem);

        var originalName = Path.GetFileNameWithoutExtension(sourcePath);
        var operation = string.Join('_', operationId.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*".ToCharArray()).ToHashSet();
        var renamed = new List<string>(outputs.Count);
        for (var i = 0; i < outputs.Count; i++)
        {
            var name = pattern.Replace("{OriginalName}", originalName, StringComparison.OrdinalIgnoreCase)
                .Replace("{Operation}", operation, StringComparison.OrdinalIgnoreCase)
                .Replace("{Date}", DateTime.Now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
                .Replace("{Counter}", (i + counterOffset + 1).ToString("D3"), StringComparison.OrdinalIgnoreCase);

            name = new string(name.Where(c => !char.IsControl(c) && !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(name))
                name = "document_output";

            var extension = Path.GetExtension(outputs[i]);
            var target = UniquePath(HistoryService.OutputDirectory, name, extension);
            File.Move(outputs[i], target);
            renamed.Add(target);
        }
        return renamed;
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        var target = Path.Combine(directory, name + extension);
        for (var index = 1; File.Exists(target); index++)
            target = Path.Combine(directory, $"{name} ({index}){extension}");
        return target;
    }
}
