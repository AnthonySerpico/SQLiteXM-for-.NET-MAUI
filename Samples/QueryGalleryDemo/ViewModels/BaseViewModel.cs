using CommunityToolkit.Mvvm.ComponentModel;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// Base ViewModel providing common functionality for all ViewModels
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
