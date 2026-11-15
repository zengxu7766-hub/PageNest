using CommunityToolkit.Maui;
using MauiBlazorToolkit.Extensions;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using SwashbucklerDiary.Maui.Essentials;
using SwashbucklerDiary.Maui.Extensions;
using SwashbucklerDiary.Rcl.Extensions;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiBlazorToolkit(options =>
                {
                    options.TitleBar = true;
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                })
                .ConfigureEssentials(essentials =>
                {
                    essentials.UseVersionTracking()
                              .OnAppAction(AppActionsHelper.HandleAppActions)
                              ;
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif
            builder.Services.AddMasaBlazorConfig();

            builder.Services.AddSerilogConfig(SQLiteConstants.LogDatabasePath);

            builder.Services.AddSqlSugarConfig(SQLiteConstants.ConnectionString, SQLiteConstants.PrivacyConnectionString, SQLiteConstants.LogConnectionString);

            builder.Services.AddDependencyInjection();

            builder.Services.AddMauiExceptionHandler();

            var app = builder.Build();

            // Start the MCP server if enabled in settings. Apply() never throws,
            // so a bad port or busy socket cannot prevent the app from launching.
            app.Services.GetRequiredService<IMcpService>().Apply();

            return app;
        }
    }
}