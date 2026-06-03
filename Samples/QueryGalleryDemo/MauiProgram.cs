using Microsoft.Extensions.Logging;
using QueryGalleryDemo.Services;
using QueryGalleryDemo.ViewModels;
using QueryGalleryDemo.Views;

namespace QueryGalleryDemo;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register Services
        builder.Services.AddSingleton<DatabaseSeeder>();

        // Register ViewModels
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<QueryMenuViewModel>();
        builder.Services.AddTransient<QueryCategoryViewModel>();
        builder.Services.AddTransient<QueryExecutionViewModel>();

        // Register Views
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<QueryMenuPage>();
        builder.Services.AddTransient<QueryCategoryPage>();
        builder.Services.AddTransient<QueryExecutionPage>();

        return builder.Build();
    }
}
