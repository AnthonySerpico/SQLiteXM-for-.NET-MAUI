using CommunityToolkit.Mvvm.ComponentModel;

namespace DirectBindingDemo.ViewModels;

/// <summary>
/// Base class for all ViewModels with common properties and functionality.
/// </summary>
public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    protected void ClearError()
    {
        ErrorMessage = string.Empty;
    }
}
