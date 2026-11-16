using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class BatchViewModel : ObservableObject
{
    private readonly IPdfOptimizeService _optimize;
    private readonly IPdfEditService _edit;
    private readonly IPdfSecurityService _security;
    private readonly HistoryService _history;
    private readonly AuthService _auth;
    private readonly PdfOutputNamingService _outputNaming;
    private CancellationTokenSource? _cancellation;

    public BatchViewModel(IPdfOptimizeService optimize, IPdfEditService edit, IPdfSecurityService security,
        HistoryService history, AuthService auth, PdfOutputNamingService outputNaming)
    {
        _optimize = optimize;
        _edit = edit;
        _security = security;
        _history = history;
        _auth = auth;
        _outputNaming = outputNaming;
        OutputNamePattern = _outputNaming.Pattern;
        OperationOptions = ["Compress", "Watermark", "Protect", "Unlock", "Rotate", "Add page numbers"];
        CompressionOptions = ["Low – best quality", "Medium – recommended", "High – smallest file"];
    }

    public ObservableCollection<BatchQueueItem> Queue { get; } = [];
    public IReadOnlyList<string> OperationOptions { get; }
    public IReadOnlyList<string> CompressionOptions { get; }

    [ObservableProperty] private int _operationIndex;
    [ObservableProperty] private int _compressionIndex = 1;
    [ObservableProperty] private string _watermarkText = "CONFIDENTIAL";
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _outputNamePattern = PdfOutputNamingService.DefaultPattern;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _status = string.Empty;

    public bool IsNotBusy => !IsBusy;
    public bool HasFiles => Queue.Count > 0;
    public bool NeedsPassword => OperationIndex is 2 or 3;
    public bool NeedsWatermark => OperationIndex == 1;
    public bool NeedsCompression => OperationIndex == 0;
    public bool HasFailed => Queue.Any(i => i.State == BatchItemState.Failed);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnOutputNamePatternChanged(string value) => _outputNaming.Pattern = value;
    partial void OnOperationIndexChanged(int value)
    {
        OnPropertyChanged(nameof(NeedsPassword));
        OnPropertyChanged(nameof(NeedsWatermark));
        OnPropertyChanged(nameof(NeedsCompression));
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        try
        {
            var picks = await FilePickerHelper.PickAsync(new PickOptions
            {
                PickerTitle = "Choose PDFs for batch processing",
                FileTypes = FilePickerFileType.Pdf
            }, multiple: true);

            foreach (var file in picks)
            {
                var dir = Path.Combine(FileSystem.CacheDirectory, "BatchInputs");
                Directory.CreateDirectory(dir);
                var target = UniquePath(dir, Path.GetFileName(file.FileName));
                await using var input = await file.OpenReadAsync();
                await using var output = File.Create(target);
                await input.CopyToAsync(output);
                Queue.Add(new BatchQueueItem { FilePath = target });
            }

            OnPropertyChanged(nameof(HasFiles));
            Status = picks.Count == 0 ? Status : $"Added {picks.Count} PDF file{(picks.Count == 1 ? string.Empty : "s")}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not add files: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Remove(BatchQueueItem? item)
    {
        if (item is null || item.State == BatchItemState.Processing)
            return;

        Queue.Remove(item);
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasFailed));
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (IsBusy)
            return;

        var items = Queue.Where(i => i.State is BatchItemState.Queued or BatchItemState.Failed).ToList();
        if (items.Count == 0)
        {
            Status = "Add files to the queue first.";
            return;
        }

        if (OperationIndex is 2 or 3 && string.IsNullOrWhiteSpace(Password))
        {
            Status = "Enter the password required for this operation.";
            return;
        }
        if (OperationIndex == 1 && string.IsNullOrWhiteSpace(WatermarkText))
        {
            Status = "Enter watermark text.";
            return;
        }
        if (PdfOutputNamingService.Validate(OutputNamePattern) is { } namingProblem)
        {
            Status = namingProblem;
            return;
        }

        var operation = (BatchOperation)OperationIndex;
        var compression = (CompressionLevel)CompressionIndex;
        var watermark = WatermarkText.Trim();
        var password = Password;
        var outputPattern = OutputNamePattern.Trim();
        var outputs = new List<string>();
        var inputBytes = items.Sum(i => new FileInfo(i.FilePath).Length);
        var outputBytes = 0L;

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        Progress = 0;
        Status = "Processing queue…";

        try
        {
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (cancellation.IsCancellationRequested)
                {
                    item.State = BatchItemState.Cancelled;
                    item.Status = "Cancelled";
                    continue;
                }

                item.State = BatchItemState.Processing;
                item.Status = "Processing…";
                try
                {
                    var output = await Task.Run(() =>
                    {
                        var result = ProcessOne(item.FilePath, operation, compression, watermark, password);
                        return _outputNaming.Apply([result], item.FilePath, operation.ToString(), outputPattern, index)[0];
                    });
                    item.OutputPath = output;
                    item.State = BatchItemState.Completed;
                    item.Status = $"Completed • {Formatting.Bytes(new FileInfo(output).Length)}";
                    outputs.Add(output);
                    outputBytes += new FileInfo(output).Length;
                }
                catch (Exception ex)
                {
                    item.State = BatchItemState.Failed;
                    item.Status = ex.Message;
                }

                Progress = (index + 1.0) / items.Count;
            }

            if (outputs.Count > 0)
            {
                _history.Add(new HistoryEntry
                {
                    ToolId = $"batch-{operation.ToString().ToLowerInvariant()}",
                    ToolTitle = $"Batch {OperationOptions[OperationIndex]}",
                    UserEmail = _auth.Current?.Email ?? string.Empty,
                    Glyph = "📚",
                    InputBytes = inputBytes,
                    OutputBytes = outputBytes,
                    Outputs = outputs
                });
            }

            var completed = items.Count(i => i.State == BatchItemState.Completed);
            var failed = items.Count(i => i.State == BatchItemState.Failed);
            var cancelled = items.Count(i => i.State == BatchItemState.Cancelled);
            Status = $"Batch finished: {completed} completed, {failed} failed, {cancelled} cancelled.";
        }
        finally
        {
            IsBusy = false;
            cancellation.Dispose();
            _cancellation = null;
            OnPropertyChanged(nameof(HasFailed));
        }
    }

    [RelayCommand]
    private void Cancel() => _cancellation?.Cancel();

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        foreach (var item in Queue.Where(i => i.State == BatchItemState.Failed))
        {
            item.State = BatchItemState.Queued;
            item.Status = "Queued";
        }
        await ProcessAsync();
    }

    [RelayCommand]
    private async Task OpenOutputAsync(BatchQueueItem? item)
    {
        if (item?.OutputPath is { } path && await FileActions.OpenAsync(path) is { } error)
            Status = error;
    }

    private string ProcessOne(string input, BatchOperation operation, CompressionLevel compression, string watermark, string password)
    {
        Directory.CreateDirectory(HistoryService.OutputDirectory);
        return operation switch
        {
            BatchOperation.Compress => _optimize.Compress(input, compression, HistoryService.OutputDirectory),
            BatchOperation.Watermark => _edit.AddWatermark(input, new WatermarkOptions(Text: watermark), HistoryService.OutputDirectory),
            BatchOperation.Protect => _security.Protect(input, password, null, EncryptionStrength.Aes256, new PdfPermissions(), HistoryService.OutputDirectory),
            BatchOperation.Unlock => _security.Unlock(input, password, HistoryService.OutputDirectory),
            BatchOperation.Rotate => _edit.Rotate(input, 90, string.Empty, HistoryService.OutputDirectory),
            BatchOperation.AddPageNumbers => _edit.AddPageNumbers(input, new PageNumberOptions(), HistoryService.OutputDirectory),
            _ => throw new NotSupportedException("The selected batch operation is not supported.")
        };
    }

    private static string UniquePath(string directory, string fileName)
    {
        var target = Path.Combine(directory, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var index = 1; File.Exists(target); index++)
            target = Path.Combine(directory, $"{stem} ({index}){ext}");
        return target;
    }
}
