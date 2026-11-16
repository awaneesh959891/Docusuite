namespace MauiApp.Pages;

public partial class ToolPage : ContentPage
{
    public ToolPage(ToolViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
