using MauiApp.PageModels;

namespace MauiApp.Pages;

public partial class DocumentsPage : ContentPage
{
    private readonly DocumentsViewModel _model;

    public DocumentsPage(DocumentsViewModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = model;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _model.RefreshCommand.Execute(null);
    }
}
