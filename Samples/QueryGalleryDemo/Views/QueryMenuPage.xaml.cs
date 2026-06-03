using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class QueryMenuPage : ContentPage
{
    public QueryMenuPage(QueryMenuViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
