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
            if (Enum.TryParse<QueryCategory>(value, true, out var parsedCategory))
            {
                Category = parsedCategory;
                LoadQueries();
                UpdateTitle();
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
        var examples = QueryExampleProvider.GetExamplesByCategory(Category);
        QueryExamples = new ObservableCollection<QueryExample>(examples);
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
        await Shell.Current.GoToAsync("QueryExecutionPage", new Dictionary<string, object>
        {
            { "QueryExample", query }
        });
    }
}
