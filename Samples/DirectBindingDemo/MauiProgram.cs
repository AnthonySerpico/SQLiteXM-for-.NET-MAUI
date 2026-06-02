using Microsoft.Extensions.Logging;
using DirectBindingDemo.ViewModels;
using DirectBindingDemo.Views;
using SQLiteXM;

namespace DirectBindingDemo;

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

        // Register ViewModels
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<EmailPasswordViewModel>();
        builder.Services.AddTransient<PersonalInfoViewModel>();
        builder.Services.AddTransient<PreferencesViewModel>();
        builder.Services.AddTransient<ReviewViewModel>();
        builder.Services.AddTransient<HomeViewModel>();

        // Register Views
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<EmailPasswordPage>();
        builder.Services.AddTransient<PersonalInfoPage>();
        builder.Services.AddTransient<PreferencesPage>();
        builder.Services.AddTransient<ReviewPage>();
        builder.Services.AddTransient<HomePage>();

        // Initialize SQLiteXM database
        Task.Run(InitializeDatabaseAsync).GetAwaiter().GetResult();

        return builder.Build();
    }

    private static async Task InitializeDatabaseAsync()
    {
        // Initialize SQLiteXM with schema from SqlStatements.json
        await using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
        await SxmDatabase.InitializeAsync(stream);

        // Register entity types for schema management
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DirectBindingDemo.Models.User),
            typeof(DirectBindingDemo.Models.UserPreferences)
        );
    }
}
