using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Models;
using RegistrationDemo.Views;
using SQLiteXM;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for preferences and terms acceptance (Step 3 of registration).
/// </summary>
[QueryProperty(nameof(DraftId), "DraftId")]
public partial class PreferencesViewModel : BaseViewModel
{
    [ObservableProperty]
    private long draftId;

    [ObservableProperty]
    private bool acceptedTerms;

    [ObservableProperty]
    private bool enableNotifications;

    [ObservableProperty]
    private string referralCode = string.Empty;

    public PreferencesViewModel()
    {
        Title = "Preferences";
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
            // Create LINQ context for Session database (using statement ensures proper disposal)
            await using (var context = new SxmTransaction("Session"))
            {

                // Query for the specific draft by ID using LINQ
                var draft = context.GetTable<RegistrationDraft>()
                    .FirstOrDefault(d => d.id == DraftId);

                if (draft != null)
                {
                    AcceptedTerms = draft.AcceptedTerms;
                    EnableNotifications = draft.EnableNotifications;
                    ReferralCode = draft.ReferralCode ?? string.Empty;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading draft: {ex.Message}";
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

            // Create LINQ context for Session database to load the registration draft
            await using (var context = new SxmTransaction("Session"))
            {
                var draft = context.GetTable<RegistrationDraft>()
                    .FirstOrDefault(d => d.id == DraftId);

                if (draft == null)
                {
                    ErrorMessage = "Registration session not found. Please start over.";
                    return;
                }

                // Update draft properties with user preferences
                draft.AcceptedTerms = AcceptedTerms;
                draft.EnableNotifications = EnableNotifications;
                draft.ReferralCode = string.IsNullOrWhiteSpace(ReferralCode) ? null : ReferralCode.Trim().ToUpper();
                draft.CompletedStep = 3;
                draft.LastUpdated = DateTime.UtcNow;

                // SaveAsync automatically updates the existing entity (detected by non-zero id)
                await draft.SaveAsync();


                // Navigate to review
                await Shell.Current.GoToAsync(nameof(ReviewPage), new Dictionary<string, object>
                {
                    { "DraftId", draft.id }
                });
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
        if (!AcceptedTerms)
        {
            ErrorMessage = "You must accept the terms and conditions to continue.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
