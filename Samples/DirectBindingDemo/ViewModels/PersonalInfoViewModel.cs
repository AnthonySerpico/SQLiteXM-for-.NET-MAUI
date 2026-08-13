using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectBindingDemo.Models;
using SQLiteXM;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// ViewModel for personal information entry (Step 2 of registration).
/// 
/// DIRECT BINDING PATTERN:
/// =======================
/// This ViewModel loads the existing User entity from the database (by ID)
/// and exposes it via CurrentUser. The XAML binds directly to:
/// - CurrentUser.FirstName
/// - CurrentUser.LastName
/// - CurrentUser.DateOfBirth
/// 
/// As the user types, changes flow directly to the entity properties via SetProperty().
/// When we call SaveAsync(), the entity is updated in the database.
/// 
/// COMPUTED PROPERTY DEMO:
/// =======================
/// If you bind to CurrentUser.FullName in the UI, it will automatically update
/// as the user types FirstName or LastName!
/// </summary>
[QueryProperty(nameof(UserId), "UserId")]
public partial class PersonalInfoViewModel : BaseViewModel
{
    [ObservableProperty]
    private long userId;

    [ObservableProperty]
    private User currentUser = new User();

    [ObservableProperty]
    private DateTime minimumDate = DateTime.Now.AddYears(-120);

    [ObservableProperty]
    private DateTime maximumDate = DateTime.Now.AddYears(-13);

    public PersonalInfoViewModel()
    {
        Title = "Personal Information";
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
            // Load the existing User entity from the database
            await using (var context = new SxmTransaction("AppData"))
            {
                var user = context.GetTable<User>()
                    .FirstOrDefault(u => u.id == UserId);

                if (user != null)
                {
                    // Set the entity as CurrentUser
                    // Now XAML bindings will read/write directly to this entity
                    CurrentUser = user;

                    // Set default DateOfBirth if not already set
                    if (CurrentUser.DateOfBirth == null)
                        CurrentUser.DateOfBirth = DateTime.Now.AddYears(-25);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading user: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        ClearError();

        if (!ValidateInput())
            return;

        try
        {
            IsBusy = true;

            // Save the updated entity
            // Note: No field copying needed! The entity already has the latest values
            // from the UI bindings.
            await CurrentUser.SaveAsync();

            // Navigate to next page
            await Shell.Current.GoToAsync($"PreferencesPage?UserId={CurrentUser.id}");
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

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(CurrentUser.FirstName))
        {
            ErrorMessage = "First name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentUser.LastName))
        {
            ErrorMessage = "Last name is required.";
            return false;
        }

        if (CurrentUser.DateOfBirth == null)
        {
            ErrorMessage = "Date of birth is required.";
            return false;
        }

        return true;
    }
}
