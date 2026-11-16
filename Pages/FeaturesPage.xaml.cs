using MauiApp.PageModels;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class FeaturesPage : ContentPage
{
    private readonly FeaturesViewModel _vm;
    private readonly AuthService _auth;

    public FeaturesPage(FeaturesViewModel vm, AuthService auth)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_auth.IsAdmin)
        {
            await Shell.Current.GoToAsync("//home");
            return;
        }
        _vm.Load();
    }
}
