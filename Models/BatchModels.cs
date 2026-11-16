using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp.Models;

public enum BatchOperation
{
    Compress,
    Watermark,
    Protect,
    Unlock,
    Rotate,
    AddPageNumbers
}

public enum BatchItemState
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public sealed partial class BatchQueueItem : ObservableObject
{
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);

    [ObservableProperty] private BatchItemState _state = BatchItemState.Queued;
    [ObservableProperty] private string _status = "Queued";
    [ObservableProperty] private string? _outputPath;

    public string StateLabel => State.ToString();
    public string QueueStatus => $"{StateLabel} • {Status}";
    public bool CanRemove => State != BatchItemState.Processing;
    public bool HasOutput => !string.IsNullOrEmpty(OutputPath);

    partial void OnStateChanged(BatchItemState value)
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(QueueStatus));
        OnPropertyChanged(nameof(CanRemove));
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(QueueStatus));
    partial void OnOutputPathChanged(string? value) => OnPropertyChanged(nameof(HasOutput));
}
