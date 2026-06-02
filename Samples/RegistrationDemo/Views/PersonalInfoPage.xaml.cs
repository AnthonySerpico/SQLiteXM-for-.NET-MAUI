using RegistrationDemo.ViewModels;

namespace RegistrationDemo.Views;

public partial class PersonalInfoPage : ContentPage
{
    public PersonalInfoPage(PersonalInfoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
