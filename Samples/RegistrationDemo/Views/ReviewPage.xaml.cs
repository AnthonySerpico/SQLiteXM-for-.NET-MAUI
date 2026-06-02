using RegistrationDemo.ViewModels;

namespace RegistrationDemo.Views;

public partial class ReviewPage : ContentPage
{
    public ReviewPage(ReviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
