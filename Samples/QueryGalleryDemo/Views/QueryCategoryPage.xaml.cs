using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class QueryCategoryPage : ContentPage
{
    public QueryCategoryPage(QueryCategoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
