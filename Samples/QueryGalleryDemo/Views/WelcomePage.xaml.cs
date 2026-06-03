using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class WelcomePage : ContentPage
{
    private readonly WelcomeViewModel _viewModel;

    public WelcomePage(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("WelcomePage: OnAppearing called");
        await _viewModel.InitializeAsync();
        System.Diagnostics.Debug.WriteLine("WelcomePage: InitializeAsync completed");
    }
}
