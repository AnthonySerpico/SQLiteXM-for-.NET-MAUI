using QueryGalleryDemo.ViewModels;

namespace QueryGalleryDemo.Views;

public partial class QueryExecutionPage : ContentPage
{
    private readonly QueryExecutionViewModel _viewModel;

    public QueryExecutionPage(QueryExecutionViewModel viewModel)
    {
        System.Diagnostics.Debug.WriteLine("QueryExecutionPage constructor called");
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        System.Diagnostics.Debug.WriteLine("QueryExecutionPage initialized");
    }

    protected override void OnAppearing()
    {
        System.Diagnostics.Debug.WriteLine("QueryExecutionPage.OnAppearing called");
        base.OnAppearing();

        try
        {
            _viewModel.LoadSelectedQuery();
            System.Diagnostics.Debug.WriteLine("LoadSelectedQuery completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");

            // Show error to user
            Dispatcher.Dispatch(async () =>
            {
                await DisplayAlert("Error", $"Failed to load query: {ex.Message}", "OK");
            });
        }
    }
}
