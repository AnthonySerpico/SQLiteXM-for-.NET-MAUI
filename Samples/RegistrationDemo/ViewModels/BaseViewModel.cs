using CommunityToolkit.Mvvm.ComponentModel;

namespace RegistrationDemo.ViewModels;

/// <summary>
/// Base class for all ViewModels providing common functionality.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? title;

    [ObservableProperty]
    private string? errorMessage;

    public void ClearError()
    {
        ErrorMessage = null;
    }
}
