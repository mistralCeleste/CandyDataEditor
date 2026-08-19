using CandyDataEditor.Services;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
namespace CandyDataEditor
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddScoped<HttpClient>();
            builder.Services.AddSingleton<GameDictionaryService>();
            builder.Services.AddSingleton<SqliteEditorConfig>();
            builder.Services.AddSingleton<SqliteDataService>();
            builder.Services.AddSingleton<FileDialogService>();
            builder.Services.AddSingleton<FontLigatureService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
