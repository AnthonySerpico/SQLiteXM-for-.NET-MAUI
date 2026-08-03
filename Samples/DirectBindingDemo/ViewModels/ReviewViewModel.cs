using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectBindingDemo.Models;
using SQLiteXM;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// ViewModel for reviewing and completing registration (Step 4).
/// 
/// TRANSACTION PATTERN WITH DIRECT BINDING:
/// =========================================
/// This ViewModel loads the User entity and creates UserPreferences,
/// then saves both in a transaction to ensure atomicity.
/// 
/// The UI binds directly to CurrentUser.FullName, CurrentUser.Email, etc.
/// to display the computed and stored values.
/// </summary>
[QueryProperty(nameof(UserId), "UserId")]
public partial class ReviewViewModel : BaseViewModel
{
    [ObservableProperty]
    private long userId;

    [ObservableProperty]
    private User currentUser = new User();

    [ObservableProperty]
    private UserPreferences currentPreferences = new UserPreferences();

    [ObservableProperty]
    private string notificationsStatus = string.Empty;

    public ReviewViewModel()
    {
        Title = "Review & Complete";
    }

    partial void OnUserIdChanged(long value)
    {
        if (value > 0)
        {
            _ = LoadDataAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            await using (var context = new SxmDbContext("AppData"))
            {
                var user = context.GetTable<User>()
                    .FirstOrDefault(u => u.id == UserId);

                if (user != null)
                {
                    CurrentUser = user;

                    // Initialize preferences (not saved yet)
                    CurrentPreferences = new UserPreferences
                    {
                        UserId = user.id,
                        EnableNotifications = false, // Will be set from UI binding
                        CreatedAt = DateTime.UtcNow
                    };

                    UpdateNotificationsStatus();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }

    partial void OnCurrentPreferencesChanged(UserPreferences value)
    {
        UpdateNotificationsStatus();
    }

    private void UpdateNotificationsStatus()
    {
        NotificationsStatus = CurrentPreferences.EnableNotifications ? "Enabled" : "Disabled";
    }

    [RelayCommand]
    private async Task CompleteRegistrationAsync()
    {
        ClearError();

        try
        {
            IsBusy = true;

            // Save User and UserPreferences in a transaction
            // This ensures both are saved atomically or both fail
            await using (SxmDbContext transaction = new SxmDbContext())
            {
                try
                { 
                    // Save the user (update with final data)
                    await CurrentUser.SaveAsync();

                    // Save the preferences
                    await CurrentPreferences.SaveAsync();

                    // Commit the transaction
                    await transaction.CommitTransactionAsync();
                }
                catch
                {
                    // Transaction automatically rolls back on dispose
                    throw;
                }
            }

            // Navigate to home page with the user ID
            // Use absolute navigation with ".." to pop the navigation stack
            await Shell.Current.GoToAsync($"//WelcomePage/HomePage?UserId={CurrentUser.id}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error completing registration: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
