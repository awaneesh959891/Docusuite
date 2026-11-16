using MauiApp.Models;
using MauiApp.PageModels;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class PageOrganizerPage : ContentPage
{
    private readonly PageOrganizerViewModel _model;

    public PageOrganizerPage(string filePath, PdfPageOrganizerService organizer, HistoryService history, PdfOutputNamingService outputNaming)
    {
        InitializeComponent();
        _model = new PageOrganizerViewModel(filePath, organizer, history, outputNaming);
        BindingContext = _model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _model.LoadAsync();
    }

    private async void OnClose(object? sender, EventArgs e) => await Navigation.PopModalAsync();

    private void OnPageDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is Element { BindingContext: EditablePdfPage page })
            e.Data.Properties["Page"] = page;
    }

    private void OnPageDrop(object? sender, DropEventArgs e)
    {
        if (sender is Element { BindingContext: EditablePdfPage target } &&
            e.Data.Properties.TryGetValue("Page", out var value) && value is EditablePdfPage source)
            _model.MovePage(source, target);
    }
}
