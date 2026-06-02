using DirectBindingDemo.ViewModels;

namespace DirectBindingDemo.Views;

public partial class PersonalInfoPage : ContentPage
{
    public PersonalInfoPage(PersonalInfoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
