using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp.Models;

public sealed partial class EditablePdfPage : ObservableObject
{
    public int OriginalIndex { get; init; }
    [ObservableProperty] private int _rotation;
    public string? ThumbnailPath { get; init; }
    [ObservableProperty] private bool _isSelected;

    public int PageNumber => OriginalIndex + 1;
}
