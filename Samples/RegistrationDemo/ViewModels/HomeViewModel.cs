using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Models;
using RegistrationDemo.Views;
using SQLiteXM;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for the home page after successful registration.
/// </summary>
[QueryProperty(nameof(UserId), "UserId")]
public partial class HomeViewModel : BaseViewModel
{
    [ObservableProperty]
    private long userId;

    [ObservableProperty]
    private string welcomeMessage = string.Empty;

    [ObservableProperty]
    private string userDetails = string.Empty;

    [ObservableProperty]
    private string databaseInfo = string.Empty;

    public HomeViewModel()
    {
        Title = "Welcome!";
    }

    partial void OnUserIdChanged(long value)
    {
        if (value > 0)
        {
            _ = LoadUserAsync();
        }
    }

    private async Task LoadUserAsync()
    {
        try
        {
            // Create LINQ context for UserData database (using statement ensures proper disposal)
            await using (var context = new SxmLinqDbContext("UserData"))
            {
                // Query for the specific user by ID using LINQ - demonstrates read-only query pattern
                var user = context.GetTable<User>()
                    .FirstOrDefault(u => u.id == UserId);

                if (user != null)
                {
                    WelcomeMessage = $"Welcome, {user.FullName}!";

                    var details = $"Email: {user.Email}\n";
                    details += $"Age: {user.Age ?? 0}\n";
                    details += $"Registered: {user.CreatedAt:g}";
                    UserDetails = details;

                    DatabaseInfo = $"✅ User stored in: UserData database\n" +
                                  $"✅ Database location: {FileSystem.AppDataDirectory}";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading user: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartOverAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(WelcomePage)}");
    }
}
