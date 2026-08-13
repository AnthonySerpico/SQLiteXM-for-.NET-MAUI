using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Models;
using RegistrationDemo.Views;
using SQLiteXM;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for personal information entry (Step 2 of registration).
/// </summary>
[QueryProperty(nameof(DraftId), "DraftId")]
public partial class PersonalInfoViewModel : BaseViewModel
{
    [ObservableProperty]
    private long draftId;

    [ObservableProperty]
    private string firstName = string.Empty;

    [ObservableProperty]
    private string lastName = string.Empty;

    [ObservableProperty]
    private DateTime dateOfBirth = DateTime.Now.AddYears(-25);

    [ObservableProperty]
    private DateTime minimumDate = DateTime.Now.AddYears(-120);

    [ObservableProperty]
    private DateTime maximumDate = DateTime.Now.AddYears(-13);

    public PersonalInfoViewModel()
    {
        Title = "Personal Information";
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
                    FirstName = draft.FirstName ?? string.Empty;
                    LastName = draft.LastName ?? string.Empty;
                    if (draft.DateOfBirth.HasValue)
                        DateOfBirth = draft.DateOfBirth.Value;
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

                // Update draft properties with user input
                draft.FirstName = FirstName.Trim();
                draft.LastName = LastName.Trim();
                draft.DateOfBirth = DateOfBirth;
                draft.CompletedStep = 2;
                draft.LastUpdated = DateTime.UtcNow;

                // SaveAsync automatically updates the existing entity (detected by non-zero id)
                await draft.SaveAsync();

                // Navigate to next step
                await Shell.Current.GoToAsync(nameof(PreferencesPage), new Dictionary<string, object>
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
        if (string.IsNullOrWhiteSpace(FirstName))
        {
            ErrorMessage = "Please enter your first name.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LastName))
        {
            ErrorMessage = "Please enter your last name.";
            return false;
        }

        var age = DateTime.Today.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > DateTime.Today.AddYears(-age)) age--;

        if (age < 13)
        {
            ErrorMessage = "You must be at least 13 years old to register.";
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
