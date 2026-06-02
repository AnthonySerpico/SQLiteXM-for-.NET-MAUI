using RegistrationDemo.ViewModels;

namespace RegistrationDemo.Views;

public partial class EmailPasswordPage : ContentPage
{
    public EmailPasswordPage(EmailPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
