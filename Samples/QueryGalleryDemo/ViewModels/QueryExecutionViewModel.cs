using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryGalleryDemo.Models;
using QueryGalleryDemo.Services;
using SQLiteXM;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// ViewModel for executing queries and displaying split-view results
/// </summary>
public partial class QueryExecutionViewModel : BaseViewModel
{
    [ObservableProperty]
    private QueryExample? queryExample;

    [ObservableProperty]
    private string formattedCode = string.Empty;

    [ObservableProperty]
    private object? queryResults;

    private string _formattedResults = string.Empty;
    public string FormattedResults
    {
        get => _formattedResults;
        set => SetProperty(ref _formattedResults, value);
    }

    [ObservableProperty]
    private int recordCount;

    [ObservableProperty]
    private long executionTimeMs;

    [ObservableProperty]
    private bool hasResults;

    // Called when the page appears to load the selected query
    public void LoadSelectedQuery()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("LoadSelectedQuery called");
            var query = NavigationService.GetSelectedQuery();
            if (query != null)
            {
                System.Diagnostics.Debug.WriteLine($"Loading query: {query.Id} - {query.Name}");
                QueryExample = query;
                System.Diagnostics.Debug.WriteLine("Query loaded successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Warning: No query found in NavigationService");
                ErrorMessage = "No query selected";

                // Show alert to user
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Error", 
                            "No query was selected. The navigation service returned null.", 
                            "OK");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading query: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            ErrorMessage = $"Failed to load query: {ex.Message}";

            // Show alert to user
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Error", 
                        $"Failed to load query: {ex.Message}\n\nType: {ex.GetType().Name}", 
                        "OK");
                }
            });
        }
    }

    partial void OnQueryExampleChanged(QueryExample? value)
    {
        try
        {
            if (value != null)
            {
                System.Diagnostics.Debug.WriteLine($"QueryExample changed: {value.Name}");
                Title = value.Name;
                FormattedCode = value.Code;
                HasResults = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnQueryExampleChanged: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            ErrorMessage = $"Failed to update query details: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        if (string.IsNullOrEmpty(QueryExample?.Code))
            return;

        try
        {
            await Clipboard.SetTextAsync(QueryExample.Code);

            // Optional: Show brief confirmation to user
            await Shell.Current.DisplayAlert("Copied", "Code copied to clipboard!", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying to clipboard: {ex.Message}");
            ErrorMessage = "Failed to copy code to clipboard.";
        }
    }

    [RelayCommand]
    private async Task RunQueryAsync()
    {
        if (QueryExample == null) return;

        ClearError();
        IsBusy = true;
        HasResults = false;

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Execute the query based on type. Generator-emitted runners (single source of
            // truth: display code == executed code) take priority over the legacy switch.
            object? results;
            if (QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.Runners.TryGetValue(QueryExample.Id, out var runnerFactory))
            {
                results = await runnerFactory().RunAsync();
            }
            else
            {
                results = QueryExample.Type == QueryType.RawSql
                    ? await ExecuteRawSqlQueryAsync()
                    : await ExecuteLinqQueryAsync();
            }

            stopwatch.Stop();

            // Set results
            QueryResults = results;
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            // Count and format results
            if (results is System.Collections.IEnumerable enumerable and not string)
            {
                // Format directly from the enumerable without converting to List<object>
                var sb = new System.Text.StringBuilder();
                int count = 0;

                foreach (var item in enumerable)
                {
                    count++;
                    if (count <= 50) // Only format first 50 for display
                    {
                        sb.AppendLine($"------ Record {count} ------");

                        // Debug output
                        System.Diagnostics.Debug.WriteLine($"Item {count} type: {item?.GetType().FullName}");

                        // Handle different types
                        if (item is IDictionary<string, object?> dict)
                        {
                            System.Diagnostics.Debug.WriteLine($"Dictionary with {dict.Count} keys: {string.Join(", ", dict.Keys)}");
                            foreach (var kvp in dict)
                            {
                                var displayValue = FormatValue(kvp.Value);
                                sb.AppendLine($"  {kvp.Key}: {displayValue}");
                            }
                        }
                        else if (item is System.Dynamic.ExpandoObject expando)
                        {
                            var expandoDict = (IDictionary<string, object?>)expando;
                            foreach (var kvp in expandoDict)
                            {
                                var displayValue = FormatValue(kvp.Value);
                                sb.AppendLine($"  {kvp.Key}: {displayValue}");
                            }
                        }
                        else
                        {
                            // Use reflection for regular objects
                            FormatObjectProperties(item, sb);
                        }

                        sb.AppendLine();
                    }
                }

                if (count > 50)
                {
                    sb.AppendLine($"... and {count - 50} more records");
                }

                RecordCount = count;
                FormattedResults = count > 0 ? sb.ToString() : "No results returned.";
                HasResults = true;
            }
            else
            {
                RecordCount = results != null ? 1 : 0;
                FormattedResults = results?.ToString() ?? "No results";
                HasResults = true;
            }

            HasResults = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Query Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<object?> ExecuteLinqQueryAsync()
    {
        // This is a simplified execution - in reality, we'd need to dynamically compile
        // For the demo, we'll execute predefined queries based on the query ID

        await using (var context = new SxmDbContext("Chinook"))

        return QueryExample?.Id switch
        {
            // basic_* are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // rel_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // agg_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // adv_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // perf_*  are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // m2m_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // trans_* are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // param_* are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // mod_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.
            // mix_*   are executed via GeneratedQueryExamples.Runners in RunQueryAsync.

            _ => new List<object>()
        };
    }



    private async Task<object?> ExecuteRawSqlQueryAsync()
    {
        if (QueryExample == null) return null;

        // Extract the statement name from the code
        // Looking for pattern: SelectAsync<Type>("StatementName")
        var statementName = QueryExample.Id switch
        {
            "raw_1" => "GetAllArtistsRaw",
            "raw_2" => "GetTracksWithArtistAlbum",
            "raw_3" => "GetTopSellingTracks",
            "raw_4" => "GetCustomerPurchaseStats",
            "raw_5" => "GetGenrePopularity",
            "raw_6" => "GetPlaylistDetails",
            "raw_7" => "GetArtistRevenue",
            "raw_8" => "GetExpensiveTracksByGenre",
            "raw_9" => "GetCustomersByCountryWithStats",
            "raw_10" => "GetMonthlyRevenueTrend",
            "raw_11" => "GetTopCustomersWithDetails",
            "raw_12" => "GetTracksWithPriceTier",
            "raw_13" => "GetAlbumCompletion",
            "raw_14" => "GetEmployeePerformance",
            "raw_15" => "GetPlaylistPopularity",
            _ => null
        };

        if (statementName != null)
        {
            // Use SxmSql.RunStatementAsync - returns List<Dictionary<string, object?>>
            var results = await SxmSql.RunStatementAsync(statementName, new Dictionary<string, object?>(), "Chinook");
            System.Diagnostics.Debug.WriteLine($"Raw SQL returned type: {results?.GetType().FullName}");
            return results;
        }

        return null;
    }

    /// <summary>
    /// Formats query results into a human-readable string
    /// </summary>
    private string FormatResults(List<object> results)
    {
        if (results == null || results.Count == 0)
            return "No results returned.";

        var sb = new System.Text.StringBuilder();

        // Take first few results to display (limit to 50 for performance)
        var displayResults = results.Take(50).ToList();

        for (int i = 0; i < displayResults.Count; i++)
        {
            var item = displayResults[i];
            sb.AppendLine($"------ Record {i + 1} ------");

            // Debug output
            System.Diagnostics.Debug.WriteLine($"Formatting item {i + 1}, type: {item?.GetType().FullName}");

            // Handle different types: Dictionary (from raw SQL), ExpandoObject, or regular objects
            if (item is IDictionary<string, object?> dict)
            {
                // Handle dictionary-based objects (raw SQL results)
                foreach (var kvp in dict)
                {
                    var displayValue = FormatValue(kvp.Value);
                    sb.AppendLine($"  {kvp.Key}: {displayValue}");
                }
            }
            else if (item is System.Dynamic.ExpandoObject expando)
            {
                // ExpandoObject implements IDictionary<string, object?>
                var expandoDict = (IDictionary<string, object?>)expando;
                foreach (var kvp in expandoDict)
                {
                    var displayValue = FormatValue(kvp.Value);
                    sb.AppendLine($"  {kvp.Key}: {displayValue}");
                }
            }
            else
            {
                // Use reflection for regular objects
                FormatObjectProperties(item, sb);
            }

            sb.AppendLine();
        }

        if (results.Count > 50)
        {
            sb.AppendLine($"... and {results.Count - 50} more records");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single value for display
    /// </summary>
    private string FormatValue(object? value)
    {
        return value switch
        {
            null => "(null)",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            decimal dec => dec.ToString("N2"),
            double dbl => dbl.ToString("N2"),
            float flt => flt.ToString("N2"),
            _ => value.ToString() ?? "(null)"
        };
    }

    /// <summary>
    /// Formats object properties using reflection. The DynamicallyAccessedMembers attribute
    /// tells the linker to preserve the properties of the object type.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Artist))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Album))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Track))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Genre))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Playlist))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(PlaylistTrack))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Customer))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Invoice))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(InvoiceLine))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Employee))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(MediaType))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "All model types are preserved via DynamicDependency attributes")]
    private void FormatObjectProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] object? item, System.Text.StringBuilder sb)
    {
        if (item == null) return;

        var type = item.GetType();
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(item);
                var displayValue = FormatValue(value);
                sb.AppendLine($"  {prop.Name}: {displayValue}");
            }
            catch
            {
                sb.AppendLine($"  {prop.Name}: (error reading value)");
            }
        }
    }


    // MixedContext examples (mix_1 .. mix_10) are now IQueryExampleRunner classes under
    // Samples/QueryGalleryDemo/Examples/Mixed/. The runner-table lookup in RunQueryAsync
    // short-circuits before this switch, so no dispatch entries are needed here either.
}
