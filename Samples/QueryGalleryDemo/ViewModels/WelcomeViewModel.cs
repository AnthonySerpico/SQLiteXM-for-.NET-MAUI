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

    public WelcomeViewModel(DatabaseSeeder databaseSeeder)
    {
        _databaseSeeder = databaseSeeder;
        Title = "QueryGallery Demo";
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Always register entities first (required for queries to work)
            SeedingStatus = "Registering entities...";
            await _databaseSeeder.RegisterEntitiesAsync();

            var isSeeded = await _databaseSeeder.IsDatabaseSeededAsync();

            if (!isSeeded)
            {
                // Need to seed the database
                IsSeeding = true;
                var progress = new Progress<string>(status =>
                {
                    SeedingStatus = status;
                });

                await _databaseSeeder.SeedDatabaseAsync(progress);
            }
            else
            {
                SeedingStatus = "Database already populated!";
            }

            IsSeeding = false;
            SeedingComplete = true;
        }
        catch (Exception ex)
        {
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
