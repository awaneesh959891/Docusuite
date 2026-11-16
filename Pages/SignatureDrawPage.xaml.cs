using MauiApp.Models;
using MauiApp.Pages.Controls;

namespace MauiApp.Pages;

public partial class SignatureDrawPage : ContentPage
{
    private readonly StrokeCollection _strokes;
    private readonly Action _onDone;
    private readonly SignaturePad _pad;
    private readonly int _initialStrokeCount;

    public SignatureDrawPage(StrokeCollection strokes, Action onDone, bool blueInk)
    {
        InitializeComponent();
        _strokes = strokes;
        _onDone = onDone;
        _initialStrokeCount = strokes.Strokes.Count;
        _pad = new SignaturePad(strokes, blueInk ? Color.FromArgb("#1F3A93") : Colors.Black);
        PadHost.Content = _pad;
    }

    private void OnClear(object? sender, EventArgs e) => _strokes.Clear();

    private async void OnCancel(object? sender, EventArgs e)
    {
        // Discard what was drawn in this session; a previously accepted signature stays.
        _strokes.TruncateTo(_initialStrokeCount);
        await Close();
    }

    private async void OnDone(object? sender, EventArgs e)
    {
        _onDone();
        await Close();
    }

    private async Task Close()
    {
        _pad.Detach();
        await Navigation.PopModalAsync();
    }
}
