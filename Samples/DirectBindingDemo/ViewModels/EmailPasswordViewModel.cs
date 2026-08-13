using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectBindingDemo.Models;
using DirectBindingDemo.Services;
using SQLiteXM;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// ViewModel for email/password entry (Step 1 of registration).
/// 
/// KEY PATTERN: DIRECT ENTITY BINDING
/// ===================================
/// Unlike RegistrationDemo which uses intermediate ViewModel properties,
/// this ViewModel exposes the User entity DIRECTLY via CurrentUser property.
/// 
/// The XAML binds to CurrentUser.Email and CurrentUser.Password (in a real app).
/// Changes flow directly to/from the entity thanks to SxmEntity's INotifyPropertyChanged.
/// 
/// BENEFITS:
/// - No field copying between ViewModel and entity
/// - Single source of truth (the entity)
/// - Computed properties (FullName, Age) work automatically
/// - Less code, fewer bugs
/// </summary>
public partial class EmailPasswordViewModel : BaseViewModel
{
    /// <summary>
    /// The User entity being created/edited.
    /// XAML binds directly to this: {Binding CurrentUser.Email}
    /// </summary>
    [ObservableProperty]
    private User currentUser = new User();

    /// <summary>
    /// Plain-text password (for UI binding only, not stored).
    /// In production, this should be a SecureString or handled more securely.
    /// </summary>
    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    public EmailPasswordViewModel()
    {
        Title = "Email & Password";
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        ClearError();

        // Validate input
        if (!ValidateInput())
            return;

        try
        {
            IsBusy = true;

            // Check if email already exists in the database
            await using (var context = new SxmTransaction("AppData"))
            {
                bool emailExists = context.GetTable<User>()
                    .Any(u => u.Email == CurrentUser.Email!.Trim().ToLower());

                if (emailExists)
                {
                    ErrorMessage = "This email is already registered.";
                    return;
                }

                // Hash the password and store in entity
                CurrentUser.PasswordHash = PasswordHasher.HashPassword(Password);
                CurrentUser.Email = CurrentUser.Email!.Trim().ToLower();
                CurrentUser.CreatedAt = DateTime.UtcNow;
                CurrentUser.LastLoginAt = DateTime.UtcNow;

                // Save the user entity to the database
                // This is a "draft save" - the user isn't complete yet, but we persist progress
                await CurrentUser.SaveAsync();

                // Navigate to next page, passing the user ID
                await Shell.Current.GoToAsync($"PersonalInfoPage?UserId={CurrentUser.id}");
            }
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
        if (string.IsNullOrWhiteSpace(CurrentUser.Email))
        {
            ErrorMessage = "Email is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters.";
            return false;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return false;
        }

        return true;
    }
}
