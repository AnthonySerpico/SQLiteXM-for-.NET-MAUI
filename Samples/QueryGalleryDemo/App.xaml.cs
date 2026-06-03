using SQLiteXM;

namespace QueryGalleryDemo;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Add global exception handlers to catch Release mode crashes
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("Unhandled Exception", ex);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("Unobserved Task Exception", e.Exception);
        e.SetObserved(); // Prevent the app from crashing
    }

    private void LogException(string source, Exception ex)
    {
        var message = $"{source}: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
        System.Diagnostics.Debug.WriteLine(message);
        Console.WriteLine(message);

        // Try to show an alert on the main thread
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (MainPage != null)
                {
                    await MainPage.DisplayAlert("Error", $"{source}\n\n{ex.Message}", "OK");
                }
            }
            catch
            {
                // If we can't show the alert, at least we logged it
            }
        });
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Create window with a loading indicator first
        var window = new Window(new LoadingPage());

        // Initialize SQLiteXM, then show the main shell
        Task.Run(async () =>
        {
            await InitializeSQLiteXMAsync();

            // Once initialized, switch to the main shell on the UI thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                window.Page = new AppShell();
            });
        });

        return window;
    }

    private async Task InitializeSQLiteXMAsync()
    {
        try
        {
            // Load the SqlStatements.json file from Resources/Raw
            using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

            if (stream != null)
            {
                await SxmDatabase.InitializeAsync(stream, new SxmDatabaseOptions
                {
                    EnableLogging = false
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing SQLiteXM: {ex.Message}");
        }
    }

    // Simple loading page to show while initializing
    private class LoadingPage : ContentPage
    {
        public LoadingPage()
        {
            Content = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new ActivityIndicator { IsRunning = true, Color = Colors.Blue },
                    new Label { Text = "Initializing SQLiteXM...", Margin = new Thickness(0, 20, 0, 0) }
                }
            };
        }
    }
}
