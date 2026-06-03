using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class QueryExecutionPage : ContentPage
{
    public QueryExecutionPage(QueryExecutionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
