using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectBindingDemo.Models;
using SQLiteXM;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// ViewModel for user preferences (Step 3 of registration).
/// 
/// DIRECT BINDING WITH RELATED ENTITIES:
/// ======================================
/// This ViewModel demonstrates binding to TWO entities:
/// - CurrentUser (already exists)
/// - CurrentPreferences (will be created)
/// 
/// Both entities expose their properties via SetProperty(), enabling direct binding.
/// </summary>
[QueryProperty(nameof(UserId), "UserId")]
public partial class PreferencesViewModel : BaseViewModel
{
    [ObservableProperty]
    private long userId;

    [ObservableProperty]
    private User currentUser = new User();

    [ObservableProperty]
    private UserPreferences currentPreferences = new UserPreferences();

    [ObservableProperty]
    private string referralCode = string.Empty;

    public PreferencesViewModel()
    {
        Title = "Preferences";
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
            await using (var context = new SxmTransaction("AppData"))
            {
                var user = context.GetTable<User>()
                    .FirstOrDefault(u => u.id == UserId);

                if (user != null)
                {
                    CurrentUser = user;

                    // Initialize preferences for this user
                    CurrentPreferences.UserId = user.id;
                    CurrentPreferences.CreatedAt = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        ClearError();

        try
        {
            IsBusy = true;

            // Set referral code if provided
            CurrentPreferences.ReferralCode = string.IsNullOrWhiteSpace(ReferralCode) 
                ? null 
                : ReferralCode.Trim().ToUpper();

            // Note: We don't save here - we'll save both User and Preferences
            // in a transaction on the Review page

            // Navigate to review page
            await Shell.Current.GoToAsync($"ReviewPage?UserId={CurrentUser.id}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
