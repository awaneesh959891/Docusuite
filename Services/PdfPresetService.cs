using System.Text.Json;
using MauiApp.Models;

namespace MauiApp.Services;

public sealed class PdfOperationPreset
{
    public string Name { get; set; } = string.Empty;
    public string ToolId { get; set; } = string.Empty;
    public int SplitModeIndex { get; set; }
    public int PagesPerFile { get; set; } = 1;
    public string Ranges { get; set; } = string.Empty;
    public int RotationIndex { get; set; }
    public int PageSizeIndex { get; set; }
    public bool Landscape { get; set; }
    public double Margin { get; set; } = 20;
    public int CompressionIndex { get; set; } = 1;
    public double CropMarginPercent { get; set; } = 10;
    public int NumberPositionIndex { get; set; }
    public int NumberFormatIndex { get; set; }
    public int StartNumber { get; set; } = 1;
    public double NumberFontSize { get; set; } = 12;
    public bool SkipFirstPage { get; set; }
    public string WatermarkText { get; set; } = "CONFIDENTIAL";
    public double WatermarkOpacity { get; set; } = 25;
    public double WatermarkRotation { get; set; } = 45;
    public double WatermarkFontSize { get; set; } = 64;
    public bool WatermarkTile { get; set; }
    public int StrengthIndex { get; set; }
    public bool AllowPrint { get; set; } = true;
    public bool AllowCopy { get; set; } = true;
    public bool AllowEdit { get; set; } = true;
    public bool AllowAnnotate { get; set; } = true;
    public int ScanFilterIndex { get; set; } = 1;
    public bool AutoCrop { get; set; } = true;
}

public sealed class PdfPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(FileSystem.AppDataDirectory, "pdf-presets.json");
    private List<PdfOperationPreset>? _presets;

    public IReadOnlyList<PdfOperationPreset> ForTool(string toolId)
    {
        lock (_gate)
            return Load().Where(p => p.ToolId == toolId).OrderBy(p => p.Name).ToList();
    }

    public void Save(PdfOperationPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name) || string.IsNullOrWhiteSpace(preset.ToolId))
            throw new InvalidOperationException("A preset name and tool are required.");

        lock (_gate)
        {
            var presets = Load();
            var existing = presets.FindIndex(p => p.ToolId == preset.ToolId &&
                string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                presets[existing] = preset;
            else
                presets.Add(preset);
            Persist();
        }
    }

    public bool Delete(string toolId, string name)
    {
        lock (_gate)
        {
            var removed = Load().RemoveAll(p => p.ToolId == toolId &&
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                Persist();
            return removed;
        }
    }

    private List<PdfOperationPreset> Load()
    {
        if (_presets is not null)
            return _presets;
        try
        {
            _presets = File.Exists(_file)
                ? JsonSerializer.Deserialize<List<PdfOperationPreset>>(File.ReadAllText(_file)) ?? []
                : [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Saved PDF presets could not be read. Remove or repair the local pdf-presets.json file.", ex);
        }
        return _presets;
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var temporary = _file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_presets, JsonOptions));
        File.Move(temporary, _file, overwrite: true);
    }
}
