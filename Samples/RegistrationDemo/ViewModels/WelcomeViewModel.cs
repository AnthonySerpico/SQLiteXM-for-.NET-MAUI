using CommunityToolkit.Mvvm.Input;
using RegistrationDemo.Views;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// ViewModel for the welcome/landing page.
/// </summary>
public partial class WelcomeViewModel : BaseViewModel
{
    public WelcomeViewModel()
    {
        Title = "Welcome";
    }

    [RelayCommand]
    private async Task StartRegistrationAsync()
    {
        await Shell.Current.GoToAsync(nameof(EmailPasswordPage));
    }
}
