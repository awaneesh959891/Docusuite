namespace MauiApp.Models;

public sealed record RecycleBinDocument(string FilePath, string FileName, DateTime DeletedUtc, long SizeBytes)
{
    public string DeletedLabel => DeletedUtc.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
    public string Details => $"{Formatting.Bytes(SizeBytes)} • deleted {DeletedLabel}";
}
