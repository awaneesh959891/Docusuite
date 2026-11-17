using Microsoft.Extensions.DependencyInjection;

namespace MauiApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Obsidian Precision is a dark-first design; the Light button in the menu still overrides it.
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var services = IPlatformApplication.Current?.Services;
            if (services?.GetService<Services.AppSettingsService>() is { AutoCleanCache: true })
                _ = Task.Run(() => services.GetService<Services.HistoryService>()?.ClearCache());

            Page root;
            if (!Services.LegalService.IsAccepted)
                root = services!.GetRequiredService<Pages.ConsentPage>();
            else if (services?.GetService<Services.AuthService>() is { IsSignedIn: false })
                root = services.GetRequiredService<Pages.LoginPage>();
            else
                root = new AppShell();

            return new Window(root);
        }
    }
}