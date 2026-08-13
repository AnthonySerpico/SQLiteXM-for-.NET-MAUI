using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectBindingDemo.Models;
using SQLiteXM;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// ViewModel for the home page after successful registration.
/// Demonstrates loading and displaying a User entity with computed properties.
/// </summary>
[QueryProperty(nameof(UserId), "UserId")]
public partial class HomeViewModel : BaseViewModel
{
    [ObservableProperty]
    private long userId;

    [ObservableProperty]
    private User currentUser = new User();

    [ObservableProperty]
    private string welcomeMessage = string.Empty;

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
            await using (var context = new SxmTransaction("AppData"))
            {
                var user = context.GetTable<User>()
                    .FirstOrDefault(u => u.id == UserId);

                if (user != null)
                {
                    CurrentUser = user;

                    // Demonstrate computed property usage
                    WelcomeMessage = $"Welcome, {CurrentUser.FullName}!";

                    DatabaseInfo = $"✅ User stored in: AppData database\n" +
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
        await Shell.Current.GoToAsync("///WelcomePage");
    }
}
