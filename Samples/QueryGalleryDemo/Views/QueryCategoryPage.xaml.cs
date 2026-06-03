using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class QueryCategoryPage : ContentPage
{
    public QueryCategoryPage(QueryCategoryViewModel viewModel)
    {
        System.Diagnostics.Debug.WriteLine("QueryCategoryPage constructor called");
        InitializeComponent();
        BindingContext = viewModel;
        System.Diagnostics.Debug.WriteLine($"BindingContext set, QueryExamples count: {viewModel.QueryExamples.Count}");
    }
}
