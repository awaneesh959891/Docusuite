using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace MauiApp
{
    public static class MauiProgram
    {
        public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
        {
            var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
                    {
                        if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
                        {
                            contentPanel.IsTabStop = true;
                        }
                    });
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();

            builder.Services.AddSingleton<IPdfOrganizeService, PdfOrganizeService>();
            builder.Services.AddSingleton<IPdfEditService, PdfEditService>();
            builder.Services.AddSingleton<IPdfConvertService, PdfConvertService>();
            builder.Services.AddSingleton<IPdfOptimizeService, PdfOptimizeService>();
            builder.Services.AddSingleton<IPdfSecurityService, PdfSecurityService>();
            builder.Services.AddSingleton<IScanService, ScanService>();
            builder.Services.AddSingleton<HistoryService>();
            builder.Services.AddSingleton<AppSettingsService>();
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<FeatureService>();
            builder.Services.AddSingleton<DocumentWorkspaceService>();
            builder.Services.AddSingleton<PdfPageOrganizerService>();
            builder.Services.AddSingleton<PdfPresetService>();
            builder.Services.AddSingleton<PdfOutputNamingService>();
            builder.Services.AddTransient<BatchViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<ConsentPage>();
            builder.Services.AddTransient<LegalPage>();
            builder.Services.AddTransient<FeaturesViewModel>();
            builder.Services.AddTransient<FeaturesPage>();
            builder.Services.AddSingleton<AdminViewModel>();
            builder.Services.AddTransient<AdminPage>();

            PdfFontResolver.Install(face =>
            {
                var file = face == "bold" ? "pdf-bold.ttf" : "pdf-regular.ttf";
                using var stream = FileSystem.OpenAppPackageFileAsync(file).GetAwaiter().GetResult();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            });

            builder.Services.AddSingleton<HomeViewModel>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddSingleton<DocumentsViewModel>();
            builder.Services.AddTransient<DocumentsPage>();
            builder.Services.AddTransient<PrivacyViewModel>();
            builder.Services.AddTransient<PrivacyPage>();
            builder.Services.AddTransient<BatchPage>();
            builder.Services.AddTransientWithShellRoute<ToolPage, ToolViewModel>("tool");
            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

            return builder.Build();
        }
    }
}
