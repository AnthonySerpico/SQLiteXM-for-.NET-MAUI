using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Models;
using RegistrationDemo.Views;
using SQLiteXM;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for reviewing registration details and completing registration (Step 4).
/// </summary>
[QueryProperty(nameof(DraftId), "DraftId")]
public partial class ReviewViewModel : BaseViewModel
{
    [ObservableProperty]
    private long draftId;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private string dateOfBirth = string.Empty;

    [ObservableProperty]
    private string notifications = string.Empty;

    [ObservableProperty]
    private string referralCode = string.Empty;

    public ReviewViewModel()
    {
        Title = "Review & Complete";
    }

    partial void OnDraftIdChanged(long value)
    {
        if (value > 0)
        {
            _ = LoadDraftAsync();
        }
    }

    private async Task LoadDraftAsync()
    {
        try
        {
            await using (var context = new SxmDbContext("Session"))
            {
                var draft = context.GetTable<RegistrationDraft>()
                    .FirstOrDefault(d => d.id == DraftId);

                if (draft != null)
                {
                    Email = draft.Email ?? "";
                    FullName = $"{draft.FirstName} {draft.LastName}".Trim();
                    DateOfBirth = draft.DateOfBirth?.ToString("MMMM d, yyyy") ?? "";
                    Notifications = draft.EnableNotifications ? "Enabled" : "Disabled";
                    ReferralCode = string.IsNullOrWhiteSpace(draft.ReferralCode) ? "None" : draft.ReferralCode;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading draft: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CompleteRegistrationAsync()
    {
        ClearError();

        try
        {
            IsBusy = true;
            RegistrationDraft? draft;
            User? user = default;

            // Load draft
            await using (var sessionContext = new SxmDbContext("Session"))
            {
                draft = sessionContext.GetTable<RegistrationDraft>().FirstOrDefault(d => d.id == DraftId);

                if (draft == null)
                {
                    ErrorMessage = "Registration session not found. Please start over.";
                    return;
                }

            }
            // ==================================================================================
            // TRANSACTION SCOPE: User and UserPreferences saved atomically to UserData database
            // ==================================================================================
            await using (SxmDbContext transaction = new SxmDbContext())
            {
                try
                {
                    // Create user
                    user = new User
                    {
                        Email = draft.Email,
                        PasswordHash = draft.PasswordHash,
                        FirstName = draft.FirstName,
                        LastName = draft.LastName,
                        DateOfBirth = draft.DateOfBirth,
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow
                    };

                    await user.SaveAsync();

                    // Create preferences
                    var preferences = new UserPreferences
                    {
                        UserId = user.id,
                        EnableNotifications = draft.EnableNotifications,
                        ReferralCode = draft.ReferralCode,
                        CreatedAt = DateTime.UtcNow
                    };

                    await preferences.SaveAsync();

                    // Commit transaction. The explicit CommitTransactionAsync() call is optional
                    // but considered good practice. Without it, the transaction will AUTO-COMMIT
                    // on Dispose (If No Errors)
                    await transaction.CommitTransactionAsync();
                }
                catch
                {
                    // Transaction will be automatically rolled back on dispose.
                    // Explicit RollbackTransactionAsync() is NOT needed here because:
                    // 1. We're re-throwing the exception (not handling it)
                    // 2. DisposeAsync will safely rollback when the block exits
                    // 3. This avoids potential exception masking if rollback itself fails
                    //
                    // Use explicit rollback only when you handle the exception and continue execution.
                    throw;
                }
            }

            // ==================================================================================
            // END TRANSACTION SCOPE - Operations below execute outside the transaction
            // ==================================================================================

            // Delete draft from session database (different database, outside transaction)
            await draft.DeleteAsync();


            // Navigate to home - clear navigation stack
            await Shell.Current.GoToAsync($"//WelcomePage/{nameof(HomePage)}", new Dictionary<string, object>
                {
                    { "UserId", user.id }
                });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Registration failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
