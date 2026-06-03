using CommunityToolkit.Mvvm.Input;
using QueryGalleryDemo.Models;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// ViewModel for the main query category menu
/// </summary>
public partial class QueryMenuViewModel : BaseViewModel
{
    public QueryMenuViewModel()
    {
        Title = "Query Categories";
    }

    [RelayCommand]
    private async Task NavigateToCategoryAsync(string category)
    {
        // Pass the category string directly as a query parameter
        await Shell.Current.GoToAsync($"QueryCategoryPage?CategoryString={category}");
    }
}
