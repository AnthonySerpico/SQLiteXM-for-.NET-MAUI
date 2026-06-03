using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryGalleryDemo.Models;
using QueryGalleryDemo.Services;
using System.Collections.ObjectModel;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// ViewModel for displaying queries within a selected category
/// </summary>
[QueryProperty(nameof(CategoryString), "CategoryString")]
public partial class QueryCategoryViewModel : BaseViewModel
{
    [ObservableProperty]
    private QueryCategory category;

    [ObservableProperty]
    private ObservableCollection<QueryExample> queryExamples = new();

    // Handle category as string from navigation and convert to enum
    public string CategoryString
    {
        set
        {
            System.Diagnostics.Debug.WriteLine($"CategoryString setter called with value: {value}");
            if (Enum.TryParse<QueryCategory>(value, true, out var parsedCategory))
            {
                System.Diagnostics.Debug.WriteLine($"Successfully parsed category: {parsedCategory}");
                Category = parsedCategory;
                LoadQueries();
                UpdateTitle();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse category: {value}");
            }
        }
    }

    partial void OnCategoryChanged(QueryCategory value)
    {
        LoadQueries();
        UpdateTitle();
    }

    private void LoadQueries()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"LoadQueries called for category: {Category}");
            var examples = QueryExampleProvider.GetExamplesByCategory(Category);
            System.Diagnostics.Debug.WriteLine($"Found {examples.Count} examples");
            QueryExamples = new ObservableCollection<QueryExample>(examples);
            System.Diagnostics.Debug.WriteLine($"QueryExamples collection has {QueryExamples.Count} items");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading queries: {ex.Message}");
            ErrorMessage = $"Failed to load queries: {ex.Message}";
        }
    }

    private void UpdateTitle()
    {
        Title = Category switch
        {
            QueryCategory.Basic => "Basic Queries",
            QueryCategory.Relationships => "Relationship Queries",
            QueryCategory.Aggregations => "Aggregation Queries",
            QueryCategory.AdvancedLinq => "Advanced LINQ",
            QueryCategory.RawSql => "Raw SQL Examples",
            QueryCategory.Performance => "Performance Demos",
            QueryCategory.ManyToMany => "Many-to-Many Queries",
            _ => "Queries"
        };
    }

    [RelayCommand]
    private async Task SelectQueryAsync(QueryExample query)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"SelectQueryAsync called for: {query.Name}");

            // Store the query in a static service to avoid Shell navigation serialization issues
            NavigationService.SetSelectedQuery(query);

            System.Diagnostics.Debug.WriteLine($"Query stored in NavigationService: {query.Id}");

            await Shell.Current.GoToAsync("QueryExecutionPage");

            System.Diagnostics.Debug.WriteLine("Navigation completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            ErrorMessage = $"Navigation failed: {ex.Message}";

            // Show alert to user
            await Application.Current!.MainPage!.DisplayAlert(
                "Navigation Error", 
                $"Failed to open query: {ex.Message}\n\nType: {ex.GetType().Name}", 
                "OK");
        }
    }
}
