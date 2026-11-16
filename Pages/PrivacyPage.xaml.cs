using MauiApp.PageModels;

namespace MauiApp.Pages;

public partial class PrivacyPage : ContentPage
{
    public PrivacyPage(PrivacyViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
