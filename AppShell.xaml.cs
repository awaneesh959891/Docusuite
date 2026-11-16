using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Font = Microsoft.Maui.Font;

namespace MauiApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            var auth = IPlatformApplication.Current!.Services.GetRequiredService<Services.AuthService>();
            var isAdmin = auth.IsAdmin;
            UserLabel.Text = auth.Current is { } u ? $"{u.DisplayName}\n{u.Email}" : string.Empty;
            UserLabel.HorizontalTextAlignment = TextAlignment.Center;

            // Template sample pages and the admin tools are for administrators only.
            foreach (var item in new[] { AdminItem, FeaturesItem, DashboardItem, ProjectsItem, ManageItem })
                item.FlyoutItemIsVisible = isAdmin;
        }

        private void OnSignOut(object? sender, EventArgs e)
        {
            var services = IPlatformApplication.Current!.Services;
            services.GetRequiredService<Services.AuthService>().SignOut();
            if (Application.Current?.Windows.FirstOrDefault() is { } window)
                window.Page = services.GetRequiredService<Pages.LoginPage>();
        }
        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }

        private void OnLightClicked(object? sender, EventArgs e) => Application.Current!.UserAppTheme = AppTheme.Light;

        private void OnDarkClicked(object? sender, EventArgs e) => Application.Current!.UserAppTheme = AppTheme.Dark;
    }
}
