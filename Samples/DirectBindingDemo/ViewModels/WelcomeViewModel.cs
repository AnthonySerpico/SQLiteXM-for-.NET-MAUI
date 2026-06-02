using CommunityToolkit.Mvvm.Input;

namespace DirectBindingDemo.ViewModels;

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
        await Shell.Current.GoToAsync("EmailPasswordPage");
    }
}
