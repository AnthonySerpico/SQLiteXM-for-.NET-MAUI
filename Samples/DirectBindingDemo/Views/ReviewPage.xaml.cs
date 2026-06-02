using DirectBindingDemo.ViewModels;

namespace DirectBindingDemo.Views;

public partial class ReviewPage : ContentPage
{
    public ReviewPage(ReviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
