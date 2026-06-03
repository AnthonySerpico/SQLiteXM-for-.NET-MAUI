using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryGalleryDemo.Models;
using SQLiteXM;
using System.Diagnostics;

namespace QueryGalleryDemo.ViewModels;

/// <summary>
/// ViewModel for executing queries and displaying split-view results
/// </summary>
[QueryProperty(nameof(QueryExample), "QueryExample")]
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

    partial void OnQueryExampleChanged(QueryExample? value)
    {
        if (value != null)
        {
            Title = value.Name;
            FormattedCode = value.Code;
            HasResults = false;
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

            // Execute the query based on type
            object? results = QueryExample.Type == QueryType.RawSql 
                ? await ExecuteRawSqlQueryAsync()
                : await ExecuteLinqQueryAsync();

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
                        sb.AppendLine($"────── Record {count} ──────");

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
                            var type = item?.GetType();
                            if (type != null)
                            {
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

        using var context = new SxmLinqDbContext("Chinook");

        return QueryExample?.Id switch
        {
            "basic_1" => context.GetTable<Artist>().OrderBy(a => a.Name).ToList(),
            "basic_2" => context.GetTable<Genre>().OrderBy(g => g.Name).ToList(),
            "basic_3" => ExecuteBasic3(),
            "basic_4" => context.GetTable<Artist>().Where(a => a.Name.Contains("Zeppelin")).ToList(),
            "basic_5" => context.GetTable<Track>().Where(t => t.UnitPrice >= 0.99m && t.UnitPrice <= 1.49m).OrderBy(t => t.UnitPrice).ThenBy(t => t.Name).Take(100).ToList(),

            "rel_1" => ExecuteRel1(),
            "rel_2" => ExecuteRel2(),
            "rel_3" => ExecuteRel3(),

            "agg_1" => ExecuteAgg1(),
            "agg_2" => ExecuteAgg2(),
            "agg_3" => ExecuteAgg3(),

            "adv_1" => context.GetTable<Track>().OrderBy(t => t.Name).Skip(0).Take(20).ToList(),
            "adv_2" => ExecuteAdv2(),
            "adv_3" => context.GetTable<Track>().Where(t => (t.UnitPrice > 1.0m && t.Milliseconds > 300000) || (t.Name.Contains("Love") || t.Name.Contains("Rock"))).OrderBy(t => t.UnitPrice).Take(50).ToList(),

            "perf_1" => context.GetTable<Track>().OrderBy(t => t.Name).Take(1000).ToList(),
            "perf_2" => ExecutePerf2(),

            "m2m_1" => ExecuteM2M1(),
            "m2m_2" => ExecuteM2M2(),
            "m2m_3" => ExecuteM2M3(),

            _ => new List<object>()
        };
    }

    private object ExecuteBasic3()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var rockGenre = context.GetTable<Genre>().FirstOrDefault(g => g.Name == "Rock");
        if (rockGenre != null)
        {
            return context.GetTable<Track>().Where(t => t.GenreId == rockGenre.id).OrderBy(t => t.Name).Take(50).ToList();
        }
        return new List<Track>();
    }

    private object ExecuteRel1()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                orderby track.Name
                select new { track.Name, AlbumTitle = album.Title, track.Milliseconds })
                .Take(50)
                .ToList();
    }

    private object ExecuteRel2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from album in context.GetTable<Album>()
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                orderby artist.Name, album.Title
                select new { album.Title, ArtistName = artist.Name, album.id })
                .ToList();
    }

    private object ExecuteRel3()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
                from genre in genreGroup.DefaultIfEmpty()
                orderby artist.Name, album.Title
                select new 
                { 
                    TrackName = track.Name,
                    AlbumTitle = album.Title,
                    ArtistName = artist.Name,
                    GenreName = genre != null ? genre.Name : "Unknown",
                    track.Milliseconds
                })
                .Take(100)
                .ToList();
    }

    private object ExecuteAgg1()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
                from genre in genreGroup.DefaultIfEmpty()
                group track by new { GenreId = genre != null ? genre.id : 0, GenreName = genre != null ? genre.Name : "Unknown" } into g
                select new 
                { 
                    Genre = g.Key.GenreName,
                    TrackCount = g.Count(),
                    TotalDurationMinutes = g.Sum(t => t.Milliseconds) / 1000 / 60
                })
                .OrderByDescending(x => x.TrackCount)
                .ToList();
    }

    private object ExecuteAgg2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from album in context.GetTable<Album>()
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                group album by new { artist.id, artist.Name } into g
                select new 
                { 
                    ArtistName = g.Key.Name,
                    AlbumCount = g.Count()
                })
                .OrderByDescending(x => x.AlbumCount)
                .Take(20)
                .ToList();
    }

    private object ExecuteAgg3()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                group track by new { genre.id, genre.Name } into g
                select new 
                { 
                    Genre = g.Key.Name,
                    AvgDurationMinutes = g.Average(t => t.Milliseconds) / 1000.0 / 60.0,
                    TrackCount = g.Count()
                })
                .OrderByDescending(x => x.AvgDurationMinutes)
                .ToList();
    }

    private object ExecuteAdv2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                orderby artist.Name ascending, album.Title ascending, track.TrackNumber ascending
                select new { artist.Name, album.Title, track.TrackNumber, TrackName = track.Name })
                .Take(100)
                .ToList();
    }

    private object ExecutePerf2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoiceLine in context.GetTable<InvoiceLine>()
                join invoice in context.GetTable<Invoice>() on invoiceLine.InvoiceId equals invoice.id
                join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                join track in context.GetTable<Track>() on invoiceLine.TrackId equals track.id
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                where customer.Country == "USA"
                select new 
                { 
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    TrackName = track.Name,
                    AlbumTitle = album.Title,
                    invoiceLine.Quantity,
                    invoiceLine.UnitPrice
                })
                .Take(500)
                .ToList();
    }

    private object ExecuteM2M1()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var playlist = context.GetTable<Playlist>().FirstOrDefault(p => p.Name.Contains("Music"));
        if (playlist != null)
        {
            return (from pt in context.GetTable<PlaylistTrack>()
                    join track in context.GetTable<Track>() on pt.TrackId equals track.id
                    where pt.PlaylistId == playlist.id
                    orderby track.Name
                    select track)
                    .Take(50)
                    .ToList();
        }
        return new List<Track>();
    }

    private object ExecuteM2M2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var track = context.GetTable<Track>().FirstOrDefault(t => t.Name.Contains("Rock"));
        if (track != null)
        {
            return (from pt in context.GetTable<PlaylistTrack>()
                    join playlist in context.GetTable<Playlist>() on pt.PlaylistId equals playlist.id
                    where pt.TrackId == track.id
                    select playlist)
                    .ToList();
        }
        return new List<Playlist>();
    }

    private object ExecuteM2M3()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from playlist in context.GetTable<Playlist>()
                join pt in context.GetTable<PlaylistTrack>() on playlist.id equals pt.PlaylistId into playlistTracks
                from pt in playlistTracks.DefaultIfEmpty()
                join track in context.GetTable<Track>() on pt.TrackId equals track.id into tracks
                from track in tracks.DefaultIfEmpty()
                group track by new { playlist.id, playlist.Name } into g
                select new 
                { 
                    PlaylistName = g.Key.Name,
                    TrackCount = g.Count(t => t != null),
                    TotalDurationMinutes = g.Where(t => t != null).Sum(t => t.Milliseconds) / 1000 / 60
                })
                .OrderByDescending(x => x.TrackCount)
                .ToList();
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
            _ => null
        };

        if (statementName != null)
        {
            // Use SxmStatement.SelectAsync - returns List<Dictionary<string, object?>>
            var results = await SxmStatement.SelectAsync(statementName, new Dictionary<string, object?>(), "Chinook");
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
            sb.AppendLine($"────── Record {i + 1} ──────");

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
}
