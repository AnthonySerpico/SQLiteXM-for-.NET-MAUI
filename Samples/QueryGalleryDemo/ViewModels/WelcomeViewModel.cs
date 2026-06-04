using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryGalleryDemo.Services;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// ViewModel for the welcome/startup page that handles database seeding
/// </summary>
public partial class WelcomeViewModel : BaseViewModel
{
    private readonly DatabaseSeeder _databaseSeeder;

    [ObservableProperty]
    private string seedingStatus = "Checking database...";

    [ObservableProperty]
    private bool isSeeding = true;

    [ObservableProperty]
    private bool seedingComplete = false;

    [ObservableProperty]
    private double progressValue = 0;

    [ObservableProperty]
    private string progressText = "0%";

    // Fields to hold current progress state (accessed from background thread)
    private string _currentStatus = "Checking database...";
    private double _currentProgress = 0;
    private readonly object _progressLock = new object();

    public WelcomeViewModel(DatabaseSeeder databaseSeeder)
    {
        _databaseSeeder = databaseSeeder;
        Title = "Query Gallery Demo";
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure UI updates are visible
            await Task.Delay(100);

            // Always register entities first (required for queries to work)
            SeedingStatus = "Registering entities...";
            ProgressValue = 0.05;
            ProgressText = "5%";
            await Task.Delay(100); // Give UI time to render

            System.Diagnostics.Debug.WriteLine("WelcomeViewModel: Starting RegisterEntitiesAsync");
            await _databaseSeeder.RegisterEntitiesAsync();

            // Test if UI updates work at all
            SeedingStatus = "Checking database...";
            ProgressValue = 0.0;
            ProgressText = "0%";
            await Task.Delay(500); // Pause so user can see this change
            System.Diagnostics.Debug.WriteLine($"Updated to 'Checking database...' 8% - did UI update?");

            // Check if the database actually exists, not just the preference flag
            System.Diagnostics.Debug.WriteLine("WelcomeViewModel: Checking if seeding needed");
            var needsSeeding = await _databaseSeeder.CheckIfSeedingNeededAsync();
            System.Diagnostics.Debug.WriteLine($"WelcomeViewModel: Needs seeding = {needsSeeding}");

            if (needsSeeding)
            {
                // Need to seed the database
                IsSeeding = true;

                // Progress callback that updates UI properly
                Action<(string status, double progress)> progressAction = update =>
                {
                    System.Diagnostics.Debug.WriteLine($"Progress update received: {update.status} - {update.progress:P0}");

                    // Dispatch to main thread without blocking
                    _ = MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        SeedingStatus = update.status;
                        ProgressValue = update.progress;
                        ProgressText = $"{(int)(update.progress * 100)}%";
                        System.Diagnostics.Debug.WriteLine($"UI properties set to: {SeedingStatus} - {ProgressValue:P0} - {ProgressText}");
                    });
                };

                System.Diagnostics.Debug.WriteLine("WelcomeViewModel: About to call SeedDatabaseAsync with progress callback");

                // Run seeding on background thread so UI stays responsive
                await Task.Run(async () => await _databaseSeeder.SeedDatabaseAsync(progressAction));

                System.Diagnostics.Debug.WriteLine("WelcomeViewModel: SeedDatabaseAsync completed");

                // Small delay to ensure last UI update is processed
                await Task.Delay(500);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WelcomeViewModel: Database already populated, skipping seeding");
                SeedingStatus = "Database already populated!";
                ProgressValue = 1.0;
                ProgressText = "100%";

                // Show the completed state briefly so user can see it
                await Task.Delay(1000);
            }

            // Ensure these updates happen on the UI thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsSeeding = false;
                SeedingComplete = true;
                System.Diagnostics.Debug.WriteLine("WelcomeViewModel: Initialization complete - UI updated");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WelcomeViewModel ERROR: {ex.Message}");
            IsSeeding = false;
            ErrorMessage = $"Error initializing database: {ex.Message}";
            SeedingStatus = "Error occurred during initialization.";
        }
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        await Shell.Current.GoToAsync("QueryMenuPage");
    }
}
