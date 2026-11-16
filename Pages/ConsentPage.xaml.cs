using Microsoft.Extensions.DependencyInjection;
using MauiApp.Services;

namespace MauiApp.Pages;

public partial class ConsentPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ConsentPage(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        PrivacyLabel.Text = LegalService.PrivacySummary;
        DisclaimerLabel.Text = LegalService.Disclaimer;
        TermsLabel.Text = LegalService.Terms;
    }

    private void OnCheckedChanged(object? sender, CheckedChangedEventArgs e) => ContinueButton.IsEnabled = e.Value;

    private void OnContinue(object? sender, EventArgs e)
    {
        if (!AcceptCheck.IsChecked)
            return;
        LegalService.Accept();
        if (Application.Current?.Windows.FirstOrDefault() is { } window)
            window.Page = _services.GetRequiredService<AuthService>().IsSignedIn
                ? new AppShell()
                : _services.GetRequiredService<LoginPage>();
    }

    private void OnExit(object? sender, EventArgs e) => Application.Current?.Quit();
}
