using MauiApp.PageModels;

namespace MauiApp.Pages;

public partial class BatchPage : ContentPage
{
    public BatchPage(BatchViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
