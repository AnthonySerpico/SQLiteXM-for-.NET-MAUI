using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Models;
using RegistrationDemo.Services;
using RegistrationDemo.Views;
using SQLiteXM;
using System.Text.RegularExpressions;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for email and password entry (Step 1 of registration).
/// </summary>
public partial class EmailPasswordViewModel : BaseViewModel
{
    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    public EmailPasswordViewModel()
    {
        Title = "Create Account";
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

            // Create LINQ context for UserData database to check for existing email
            await using (var context = new SxmDbContext("UserData"))
            {

                // Query to verify email uniqueness - demonstrates LINQ query pattern
                bool emailExists = context.GetTable<User>()
                    .Any(u => u.Email == Email.Trim().ToLower());

                if (emailExists)
                {
                    ErrorMessage = "This email is already registered.";
                    return;
                }
            }

            // Get or create a registration draft in the Session database
            RegistrationDraft draft = await GetOrCreateDraftAsync();

            // Update draft properties with user input
            draft.Email = Email.Trim().ToLower();
            draft.PasswordHash = PasswordHasher.HashPassword(Password);
            draft.CompletedStep = 1;
            draft.LastUpdated = DateTime.UtcNow;

            // SaveAsync automatically updates the existing entity (detected by non-zero id)
            await draft.SaveAsync();

            // Navigate to next step
            await Shell.Current.GoToAsync(nameof(PersonalInfoPage), new Dictionary<string, object>
            {
                { "DraftId", draft.id }
            });
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
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Please enter your email address.";
            return false;
        }

        if (!IsValidEmail(Email))
        {
            ErrorMessage = "Please enter a valid email address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter a password.";
            return false;
        }

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters long.";
            return false;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return false;
        }

        return true;
    }

    private static bool IsValidEmail(string email)
    {
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        return emailRegex.IsMatch(email);
    }

    private static async Task<RegistrationDraft> GetOrCreateDraftAsync()
    {
        // Create LINQ context for Session database (using statement ensures proper disposal)
        await using (var context = new SxmDbContext("Session"))
        {
            // Query for most recent draft using LINQ - demonstrates SQLiteXM's LINQ provider
            var existing = context.GetTable<RegistrationDraft>()
                .OrderByDescending(d => d.StartedAt)
                .FirstOrDefault();

            if (existing != null)
                return existing;
        }

        // Create new draft entity
        var draft = new RegistrationDraft
        {
            StartedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        // SaveAsync automatically inserts new entities (no explicit Insert call needed)
        await draft.SaveAsync();
        return draft;
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
