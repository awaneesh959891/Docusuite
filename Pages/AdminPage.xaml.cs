using MauiApp.PageModels;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class AdminPage : ContentPage
{
    private readonly AdminViewModel _vm;
    private readonly AuthService _auth;

    public AdminPage(AdminViewModel vm, AuthService auth)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _auth = auth;
    }

    private async void OnFeatureControlClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//features");

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_auth.IsAdmin)
        {
            await Shell.Current.GoToAsync("//home");
            return;
        }
        _vm.Refresh();
    }
}
