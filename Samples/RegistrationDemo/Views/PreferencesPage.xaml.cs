using RegistrationDemo.ViewModels;

namespace RegistrationDemo.Views;

public partial class PreferencesPage : ContentPage
{
    public PreferencesPage(PreferencesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
