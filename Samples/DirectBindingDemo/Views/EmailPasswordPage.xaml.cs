using DirectBindingDemo.ViewModels;

namespace DirectBindingDemo.Views;

public partial class EmailPasswordPage : ContentPage
{
    public EmailPasswordPage(EmailPasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
