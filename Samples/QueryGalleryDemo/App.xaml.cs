using SQLiteXM;

namespace QueryGalleryDemo;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
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
