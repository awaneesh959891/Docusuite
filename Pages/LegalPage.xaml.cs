using MauiApp.Services;

namespace MauiApp.Pages;

public partial class LegalPage : ContentPage
{
    public LegalPage()
    {
        InitializeComponent();
        VersionLabel.Text = $"DocSuite {AppInfo.Current.VersionString}  •  Privacy notice {LegalService.CurrentVersion}";
        DisclaimerLabel.Text = LegalService.Disclaimer;
        PrivacyLabel.Text = LegalService.PrivacySummary;
        DataLabel.Text = LegalService.DataDetails;
        RetentionLabel.Text = LegalService.Retention;
        TermsLabel.Text = LegalService.Terms;
    }
}
