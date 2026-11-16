using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class PageOrganizerViewModel : ObservableObject
{
    private readonly PdfPageOrganizerService _organizer;
    private readonly HistoryService _history;
    private readonly PdfOutputNamingService _outputNaming;
    private readonly string _sourcePath;
    private readonly Stack<List<PageSnapshot>> _undo = new();
    private readonly Stack<List<PageSnapshot>> _redo = new();

    private sealed record PageSnapshot(int OriginalIndex, int Rotation, string? ThumbnailPath);

    public PageOrganizerViewModel(string sourcePath, PdfPageOrganizerService organizer, HistoryService history, PdfOutputNamingService outputNaming)
    {
        _sourcePath = sourcePath;
        _organizer = organizer;
        _history = history;
        _outputNaming = outputNaming;
        Title = Path.GetFileName(sourcePath);
    }

    public ObservableCollection<EditablePdfPage> Pages { get; } = [];
    public string Title { get; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private int _pageCount;
    [ObservableProperty] private string? _lastOutputPath;

    public bool HasOutput => !string.IsNullOrEmpty(LastOutputPath);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsNotBusy => !IsBusy;
    public int SelectedCount => Pages.Count(p => p.IsSelected);
    public bool HasSelection => SelectedCount > 0;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));
    partial void OnLastOutputPathChanged(string? value) => OnPropertyChanged(nameof(HasOutput));

    public async Task LoadAsync()
    {
        if (Pages.Count > 0 || IsBusy)
            return;

        IsBusy = true;
        Status = "Loading page previews…";
        try
        {
            var modified = File.GetLastWriteTimeUtc(_sourcePath);
            var count = await Task.Run(() => _organizer.GetPageCount(_sourcePath));
            var thumbs = new List<string?>(count);
            string? previewProblem = null;

            for (var index = 0; index < count; index++)
            {
                var pageIndex = index;
                try
                {
                    thumbs.Add(await Task.Run(() => _organizer.RenderPage(_sourcePath, pageIndex, modified)));
                }
                catch (Exception ex)
                {
                    thumbs.Add(null);
                    previewProblem ??= ex.Message;
                }
            }

            Pages.Clear();
            for (var index = 0; index < count; index++)
            {
                var page = new EditablePdfPage { OriginalIndex = index, ThumbnailPath = thumbs[index] };
                page.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(EditablePdfPage.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedCount));
                        OnPropertyChanged(nameof(HasSelection));
                    }
                };
                Pages.Add(page);
            }

            PageCount = count;
            Status = previewProblem is null
                ? $"{count} pages. Changes are saved as a new copy."
                : $"{count} pages. Preview unavailable on this device: {previewProblem} Page operations remain available.";
        }
        catch (Exception ex)
        {
            Status = $"Could not load this PDF: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RaiseHistoryState();
        }
    }

    [RelayCommand]
    private void MoveUp(EditablePdfPage? page)
    {
        var index = page is null ? -1 : Pages.IndexOf(page);
        if (index <= 0)
            return;

        SaveUndo();
        Pages.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown(EditablePdfPage? page)
    {
        var index = page is null ? -1 : Pages.IndexOf(page);
        if (index < 0 || index >= Pages.Count - 1)
            return;

        SaveUndo();
        Pages.Move(index, index + 1);
    }

    public void MovePage(EditablePdfPage source, EditablePdfPage target)
    {
        var sourceIndex = Pages.IndexOf(source);
        var targetIndex = Pages.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return;

        SaveUndo();
        Pages.Move(sourceIndex, targetIndex);
    }

    [RelayCommand]
    private void ReverseOrder()
    {
        if (Pages.Count < 2)
            return;

        SaveUndo();
        for (var index = 0; index < Pages.Count - 1; index++)
            Pages.Move(Pages.Count - 1, index);
        Status = "Page order reversed. Save as a copy to keep the change.";
    }

    [RelayCommand]
    private void RotateSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        SaveUndo();
        foreach (var page in selected)
            page.Rotation = (page.Rotation + 90) % 360;
        Status = $"Rotated {selected.Count} selected page{(selected.Count == 1 ? string.Empty : "s")} clockwise.";
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
            return;
        if (selected.Count == Pages.Count)
        {
            Status = "Keep at least one page in the document.";
            return;
        }

        SaveUndo();
        foreach (var page in selected)
            Pages.Remove(page);
        PageCount = Pages.Count;
        Status = $"Removed {selected.Count} page{(selected.Count == 1 ? string.Empty : "s")} from the working copy.";
    }

    [RelayCommand]
    private void DuplicateSelected()
    {
        var selected = Pages.Select((page, index) => (page, index)).Where(x => x.page.IsSelected).ToList();
        if (selected.Count == 0)
            return;

        SaveUndo();
        var inserted = 0;
        foreach (var (page, originalPosition) in selected)
        {
            var copy = new EditablePdfPage
            {
                OriginalIndex = page.OriginalIndex,
                Rotation = page.Rotation,
                ThumbnailPath = page.ThumbnailPath
            };
            copy.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EditablePdfPage.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(HasSelection));
                }
            };
            Pages.Insert(originalPosition + inserted + 1, copy);
            inserted++;
        }
        PageCount = Pages.Count;
        Status = $"Duplicated {selected.Count} page{(selected.Count == 1 ? string.Empty : "s")}.";
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undo.Count == 0)
            return;

        _redo.Push(Snapshot());
        Restore(_undo.Pop());
        Status = "Last page change undone.";
        RaiseHistoryState();
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redo.Count == 0)
            return;

        _undo.Push(Snapshot());
        Restore(_redo.Pop());
        Status = "Page change restored.";
        RaiseHistoryState();
    }

    [RelayCommand]
    private async Task SaveCopyAsync()
    {
        if (IsBusy || Pages.Count == 0)
            return;

        IsBusy = true;
        Status = "Saving edited copy…";
        try
        {
            var pageSnapshot = Pages.Select(p => new EditablePdfPage
            {
                OriginalIndex = p.OriginalIndex,
                Rotation = p.Rotation
            }).ToList();
            if (PdfOutputNamingService.Validate(_outputNaming.Pattern) is { } namingProblem)
            {
                Status = namingProblem;
                return;
            }
            var inputBytes = new FileInfo(_sourcePath).Length;
            var pattern = _outputNaming.Pattern.Trim();
            var output = await Task.Run(() =>
            {
                var savedCopy = _organizer.SaveCopy(_sourcePath, pageSnapshot);
                return _outputNaming.Apply([savedCopy], _sourcePath, "organize", pattern)[0];
            });
            var outputBytes = new FileInfo(output).Length;
            LastOutputPath = output;
            _history.Add(new HistoryEntry
            {
                ToolId = "organize",
                ToolTitle = "Page Organizer",
                Glyph = "🧩",
                InputBytes = inputBytes,
                OutputBytes = outputBytes,
                Outputs = [output]
            });
            Status = $"Saved {Path.GetFileName(output)} • {Formatting.Bytes(outputBytes)}";
        }
        catch (Exception ex)
        {
            Status = $"Could not save edited PDF: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenOutputAsync()
    {
        if (LastOutputPath is { } path && await FileActions.OpenAsync(path) is { } error)
            Status = error;
    }

    [RelayCommand]
    private async Task PreviewOutputAsync()
    {
        if (LastOutputPath is { } path)
            await Shell.Current.Navigation.PushModalAsync(new Pages.PdfPreviewPage(path, Path.GetFileName(path)));
    }

    [RelayCommand]
    private async Task ShareOutputAsync()
    {
        if (LastOutputPath is { } path && await FileActions.ShareAsync(path) is { } error)
            Status = error;
    }

    private void SaveUndo()
    {
        _undo.Push(Snapshot());
        _redo.Clear();
        RaiseHistoryState();
    }

    private List<PageSnapshot> Snapshot() => Pages.Select(p => new PageSnapshot(p.OriginalIndex, p.Rotation, p.ThumbnailPath)).ToList();

    private void Restore(List<PageSnapshot> snapshot)
    {
        Pages.Clear();
        foreach (var item in snapshot)
        {
            var page = new EditablePdfPage
            {
                OriginalIndex = item.OriginalIndex,
                Rotation = item.Rotation,
                ThumbnailPath = item.ThumbnailPath
            };
            page.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EditablePdfPage.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(HasSelection));
                }
            };
            Pages.Add(page);
        }
        PageCount = Pages.Count;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void RaiseHistoryState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }
}
