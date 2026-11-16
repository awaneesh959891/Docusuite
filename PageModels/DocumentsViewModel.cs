using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp.Models;
using MauiApp.Services;

namespace MauiApp.PageModels;

public partial class DocumentsViewModel : ObservableObject
{
    private readonly DocumentWorkspaceService _workspace;
    private readonly HistoryService _history;
    private readonly PdfPageOrganizerService _pageOrganizer;
    private readonly PdfOutputNamingService _outputNaming;

    public DocumentsViewModel(DocumentWorkspaceService workspace, HistoryService history, PdfPageOrganizerService pageOrganizer, PdfOutputNamingService outputNaming)
    {
        _workspace = workspace;
        _history = history;
        _pageOrganizer = pageOrganizer;
        _outputNaming = outputNaming;

        SortOptions = ["Modified", "Name", "Size", "Page count"];
        TagOptions = _workspace.AvailableTags;

        _history.Changed += () => MainThread.BeginInvokeOnMainThread(Load);
        Load();
    }

    public ObservableCollection<WorkspaceDocument> Documents { get; } = [];
    public ObservableCollection<WorkspaceDocument> RecentDocuments { get; } = [];
    public ObservableCollection<ContinueEditingItem> ContinueEditing { get; } = [];
    public ObservableCollection<RecycleBinDocument> RecycleBin { get; } = [];

    public IReadOnlyList<string> SortOptions { get; }
    public IReadOnlyList<string> TagOptions { get; }

    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isGridMode;
    [ObservableProperty] private bool _isSelectionMode;
    [ObservableProperty] private int _sortIndex;
    [ObservableProperty] private int _tagIndex;
    [ObservableProperty] private bool _showFavoritesOnly;
    [ObservableProperty] private bool _showPinnedOnly;

    public bool IsListMode => !IsGridMode;
    public bool HasDocuments => Documents.Count > 0;
    public bool HasNoDocuments => !HasDocuments;
    public bool HasRecentDocuments => RecentDocuments.Count > 0;
    public bool HasContinueEditing => ContinueEditing.Count > 0;
    public bool HasRecycleBin => RecycleBin.Count > 0;
    public int SelectedCount => Documents.Count(d => d.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public string ViewToggleLabel => IsGridMode ? "List" : "Grid";

    partial void OnSearchTextChanged(string value) => Load();
    partial void OnSortIndexChanged(int value) => Load();
    partial void OnTagIndexChanged(int value) => Load();
    partial void OnShowFavoritesOnlyChanged(bool value) => Load();
    partial void OnShowPinnedOnlyChanged(bool value) => Load();
    partial void OnIsGridModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsListMode));
        OnPropertyChanged(nameof(ViewToggleLabel));
    }

    partial void OnIsSelectionModeChanged(bool value)
    {
        if (!value)
            ClearSelection();
    }

    [RelayCommand]
    private async Task AddPdfAsync()
    {
        try
        {
            var picks = await FilePickerHelper.PickAsync(new PickOptions
            {
                PickerTitle = "Add documents",
                FileTypes = FilePickerFileType.Pdf
            }, multiple: true);

            if (picks.Count == 0)
                return;

            await _workspace.ImportAsync(picks);
            Load();
            Status = $"Imported {picks.Count} document{(picks.Count == 1 ? string.Empty : "s")}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not import files: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CreateFolderAsync()
    {
        var name = await Shell.Current.DisplayPromptAsync("New folder", "Enter folder name", "Create", "Cancel", "Folder");
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            _workspace.CreateFolder(name.Trim());
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not create folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Refresh() => Load();

    [RelayCommand]
    private void ToggleSelectionMode() => IsSelectionMode = !IsSelectionMode;

    [RelayCommand]
    private void ToggleView() => IsGridMode = !IsGridMode;

    [RelayCommand]
    private async Task EditPagesAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;
        if (!doc.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || doc.IsEncrypted)
        {
            Status = "Page editing requires an unprotected PDF.";
            return;
        }

        await Shell.Current.Navigation.PushModalAsync(new Pages.PageOrganizerPage(doc.FilePath, _pageOrganizer, _history, _outputNaming));
    }

    [RelayCommand]
    private async Task OpenOrToggleAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        if (IsSelectionMode)
        {
            doc.IsSelected = !doc.IsSelected;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            return;
        }

        _workspace.MarkOpened(doc.FilePath);

        try
        {
            if (doc.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && !doc.IsEncrypted)
                await Shell.Current.Navigation.PushModalAsync(new Pages.PdfPreviewPage(doc.FilePath, doc.FileName));
            else if (await FileActions.OpenAsync(doc.FilePath) is { } error)
                Status = error;
        }
        catch (Exception ex)
        {
            Status = $"Could not open file: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ToggleFavorite(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        doc.IsFavorite = !doc.IsFavorite;
        _workspace.SetFavorite(doc.FilePath, doc.IsFavorite);
    }

    [RelayCommand]
    private void TogglePinned(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        doc.IsPinned = !doc.IsPinned;
        _workspace.SetPinned(doc.FilePath, doc.IsPinned);
    }

    [RelayCommand]
    private void ChangeTag(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        var next = doc.Tag switch
        {
            "Personal" => "Office",
            "Office" => "Bills",
            "Bills" => "IDs",
            "IDs" => "Other",
            _ => "Personal"
        };

        doc.Tag = next;
        _workspace.SetTag(doc.FilePath, next);
    }

    [RelayCommand]
    private async Task RenameAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        var name = await Shell.Current.DisplayPromptAsync("Rename", "New file name", "Rename", "Cancel",
            Path.GetFileNameWithoutExtension(doc.FileName));
        if (string.IsNullOrWhiteSpace(name))
            return;

        try
        {
            await _workspace.RenameAsync(doc.FilePath, name.Trim());
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not rename: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DuplicateAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        try
        {
            await _workspace.DuplicateAsync(doc.FilePath);
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not duplicate: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task MoveAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        var folders = _workspace.Folders();
        var target = await Shell.Current.DisplayActionSheet("Move to folder", "Cancel", null, folders.ToArray());
        if (string.IsNullOrEmpty(target) || target == "Cancel")
            return;

        try
        {
            await _workspace.MoveAsync(doc.FilePath, target);
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not move: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ShareAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        if (await FileActions.ShareAsync(doc.FilePath) is { } error)
            Status = error;
    }

    [RelayCommand]
    private async Task DeleteAsync(WorkspaceDocument? doc)
    {
        if (doc is null)
            return;

        if (!await Shell.Current.DisplayAlertAsync("Delete file?", "Move this file to DocSuite recycle bin?", "Delete", "Cancel"))
            return;

        try
        {
            await _workspace.DeleteAsync([doc.FilePath], permanent: false);
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not delete: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BatchDeleteAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).Select(d => d.FilePath).ToList();
        if (selected.Count == 0)
            return;

        if (!await Shell.Current.DisplayAlertAsync("Delete selected files?", "Move selected files to DocSuite recycle bin?", "Delete", "Cancel"))
            return;

        try
        {
            await _workspace.DeleteAsync(selected, permanent: false);
            IsSelectionMode = false;
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not delete files: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BatchShareAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).Select(d => new ShareFile(d.FilePath)).ToList();
        if (selected.Count == 0)
            return;

        try
        {
            await Share.Default.RequestAsync(new ShareMultipleFilesRequest
            {
                Title = "Share documents",
                Files = selected
            });
        }
        catch (Exception ex)
        {
            Status = $"Could not share files: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BatchFavorite()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        foreach (var doc in selected)
        {
            doc.IsFavorite = true;
            _workspace.SetFavorite(doc.FilePath, true);
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RecycleBinDocument? item)
    {
        if (item is null)
            return;

        try
        {
            await _workspace.RestoreAsync(item.FilePath);
            Load();
            Status = $"Restored {item.FileName}.";
        }
        catch (Exception ex)
        {
            Status = $"Could not restore file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeletePermanentlyAsync(RecycleBinDocument? item)
    {
        if (item is null || !await Shell.Current.DisplayAlertAsync("Delete permanently?", $"{item.FileName} cannot be recovered after this.", "Delete permanently", "Cancel"))
            return;

        try
        {
            await _workspace.DeletePermanentlyAsync(item.FilePath);
            Load();
        }
        catch (Exception ex)
        {
            Status = $"Could not permanently delete file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenContinueAsync(ContinueEditingItem? item)
    {
        if (item is null || !File.Exists(item.FilePath))
            return;

        if (item.FilePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            await Shell.Current.Navigation.PushModalAsync(new Pages.PdfPreviewPage(item.FilePath, item.ToolTitle));
        else if (await FileActions.OpenAsync(item.FilePath) is { } error)
            Status = error;
    }

    private void Load()
    {
        var allDocuments = _workspace.LoadDocuments();
        var docs = allDocuments.AsEnumerable();

        if (SearchText.Trim().Length > 0)
        {
            var q = SearchText.Trim();
            docs = docs.Where(d => d.FileName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (ShowFavoritesOnly)
            docs = docs.Where(d => d.IsFavorite);

        if (ShowPinnedOnly)
            docs = docs.Where(d => d.IsPinned);

        var tag = TagOptions[Math.Clamp(TagIndex, 0, TagOptions.Count - 1)];
        if (!tag.Equals("All", StringComparison.OrdinalIgnoreCase))
            docs = docs.Where(d => d.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));

        docs = SortIndex switch
        {
            1 => docs.OrderByDescending(d => d.IsPinned).ThenBy(d => d.FileName),
            2 => docs.OrderByDescending(d => d.IsPinned).ThenByDescending(d => d.SizeBytes),
            3 => docs.OrderByDescending(d => d.IsPinned).ThenByDescending(d => d.PageCount),
            _ => docs.OrderByDescending(d => d.IsPinned).ThenByDescending(d => d.ModifiedUtc)
        };

        RecentDocuments.Clear();
        foreach (var recent in allDocuments
            .Where(d => d.LastOpenedUtc.HasValue)
            .OrderByDescending(d => d.LastOpenedUtc)
            .Take(8))
            RecentDocuments.Add(recent);

        Documents.Clear();
        foreach (var doc in docs)
        {
            doc.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(WorkspaceDocument.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedCount));
                    OnPropertyChanged(nameof(HasSelection));
                }
            };
            Documents.Add(doc);
        }

        ContinueEditing.Clear();
        foreach (var item in _workspace.ContinueEditing(_history))
            ContinueEditing.Add(item);

        RecycleBin.Clear();
        foreach (var item in _workspace.LoadRecycleBin())
            RecycleBin.Add(item);

        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasNoDocuments));
        OnPropertyChanged(nameof(HasRecentDocuments));
        OnPropertyChanged(nameof(HasContinueEditing));
        OnPropertyChanged(nameof(HasRecycleBin));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void ClearSelection()
    {
        foreach (var doc in Documents)
            doc.IsSelected = false;

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }
}
