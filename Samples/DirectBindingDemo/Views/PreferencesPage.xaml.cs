using DirectBindingDemo.ViewModels;

namespace DirectBindingDemo.Views;

public partial class PreferencesPage : ContentPage
{
    public PreferencesPage(PreferencesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
