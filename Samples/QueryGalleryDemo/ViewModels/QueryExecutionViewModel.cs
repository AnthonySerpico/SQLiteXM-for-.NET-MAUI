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

        using var context = new SxmLinqDbContext("Chinook");

        return QueryExample?.Id switch
        {
            "basic_1" => context.GetTable<Artist>().OrderBy(a => a.Name).ToList(),
            "basic_2" => context.GetTable<Genre>().OrderBy(g => g.Name).ToList(),
            "basic_3" => ExecuteBasic3(),
            "basic_4" => context.GetTable<Artist>().Where(a => a.Name.Contains("Zeppelin")).ToList(),
            "basic_5" => context.GetTable<Track>().Where(t => t.UnitPrice >= 0.99m && t.UnitPrice <= 1.49m).OrderBy(t => t.UnitPrice).ThenBy(t => t.Name).Take(100).ToList(),
            "basic_6" => context.GetTable<Track>().OrderByDescending(t => t.UnitPrice).ThenBy(t => t.Name).Take(10).ToList(),
            "basic_7" => context.GetTable<Track>().Where(t => t.Milliseconds >= 180000 && t.Milliseconds <= 300000).OrderBy(t => t.Milliseconds).Take(100).ToList(),
            "basic_8" => context.GetTable<Artist>().Where(a => a.Name.ToLower().Contains("led")).OrderBy(a => a.Name).ToList(),
            "basic_9" => context.GetTable<Track>().Where(t => t.Composer != null && t.Composer != "").OrderBy(t => t.Composer).ThenBy(t => t.Name).Take(100).ToList(),
            "basic_10" => context.GetTable<MediaType>().OrderBy(m => m.Name).ToList(),

            "rel_1" => ExecuteRel1(),
            "rel_2" => ExecuteRel2(),
            "rel_3" => ExecuteRel3(),
            "rel_4" => ExecuteRel4(),
            "rel_5" => ExecuteRel5(),
            "rel_6" => ExecuteRel6(),
            "rel_7" => ExecuteRel7(),
            "rel_8" => ExecuteRel8(),

            "agg_1" => ExecuteAgg1(),
            "agg_2" => ExecuteAgg2(),
            "agg_3" => ExecuteAgg3(),
            "agg_4" => ExecuteAgg4(),
            "agg_5" => ExecuteAgg5(),
            "agg_6" => ExecuteAgg6(),
            "agg_7" => ExecuteAgg7(),
            "agg_8" => ExecuteAgg8(),
            "agg_9" => ExecuteAgg9(),
            "agg_10" => ExecuteAgg10(),

            "adv_1" => context.GetTable<Track>().OrderBy(t => t.Name).Skip(0).Take(20).ToList(),
            "adv_2" => ExecuteAdv2(),
            "adv_3" => ExecuteAdv3(),
            "adv_4" => ExecuteAdv4(),
            "adv_5" => ExecuteAdv5(),
            "adv_6" => ExecuteAdv6(),
            "adv_7" => ExecuteAdv7(),
            "adv_8" => ExecuteAdv8(),
            "adv_9" => ExecuteAdv9(),
            "adv_10" => ExecuteAdv10(),
            "adv_11" => ExecuteAdv11(),

            "perf_1" => context.GetTable<Track>().OrderBy(t => t.Name).Take(1000).ToList(),
            "perf_2" => ExecutePerf2(),
            "perf_3" => context.GetTable<Track>().OrderBy(t => t.id).Skip(20).Take(20).ToList(),
            "perf_4" => context.GetTable<Track>().Select(t => new { t.id, t.Name, t.UnitPrice }).Take(100).ToList(),
            "perf_5" => ExecutePerf5(),
            "perf_6" => ExecutePerf6(),
            "perf_7" => ExecutePerf7(),
            "perf_8" => ExecutePerf8(),

            "m2m_1" => ExecuteM2M1(),
            "m2m_2" => ExecuteM2M2(),
            "m2m_3" => ExecuteM2M3(),
            "m2m_4" => ExecuteM2M4(),
            "m2m_5" => ExecuteM2M5(),
            "m2m_6" => ExecuteM2M6(),
            "m2m_7" => ExecuteM2M7(),
            "m2m_8" => ExecuteM2M8(),

            "trans_1" => await ExecuteTrans1Async(),
            "trans_2" => await ExecuteTrans2Async(),
            "trans_3" => await ExecuteTrans3Async(),
            "trans_4" => await ExecuteTrans4Async(),
            "trans_5" => await ExecuteTrans5Async(),
            "trans_6" => await ExecuteTrans6Async(),

            "param_1" => ExecuteParam1(),
            "param_2" => ExecuteParam2(),
            "param_3" => ExecuteParam3(),
            "param_4" => ExecuteParam4(),
            "param_5" => ExecuteParam5(),
            "param_6" => ExecuteParam6(),

            "mod_1" => await ExecuteMod1Async(),
            "mod_2" => await ExecuteMod2Async(),
            "mod_3" => await ExecuteMod3Async(),
            "mod_4" => await ExecuteMod4Async(),
            "mod_5" => await ExecuteMod5Async(),
            "mod_6" => await ExecuteMod6Async(),
            "mod_7" => await ExecuteMod7Async(),
            "mod_8" => await ExecuteMod8Async(),

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
        var track = context.GetTable<Track>().FirstOrDefault(t => t.Name.Contains("Track"));
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

    // Relationship queries
    private object ExecuteRel4()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from customer in context.GetTable<Customer>()
                join employee in context.GetTable<Employee>() on customer.SupportRepId equals employee.id into empGroup
                from employee in empGroup.DefaultIfEmpty()
                orderby customer.LastName, customer.FirstName
                select new
                {
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    customer.Email,
                    SupportRep = employee != null ? employee.FirstName + " " + employee.LastName : "None",
                    SupportRepEmail = employee != null ? employee.Email : ""
                })
                .Take(50)
                .ToList();
    }

    private object ExecuteRel5()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from emp in context.GetTable<Employee>()
                join manager in context.GetTable<Employee>() on emp.ReportsTo equals manager.id into mgrGroup
                from manager in mgrGroup.DefaultIfEmpty()
                orderby emp.LastName, emp.FirstName
                select new
                {
                    EmployeeName = emp.FirstName + " " + emp.LastName,
                    emp.Title,
                    ManagerName = manager != null ? manager.FirstName + " " + manager.LastName : "No Manager",
                    ManagerTitle = manager != null ? manager.Title : ""
                })
                .ToList();
    }

    private object ExecuteRel6()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoice in context.GetTable<Invoice>()
                join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                orderby invoice.InvoiceDate descending
                select new
                {
                    invoice.id,
                    invoice.InvoiceDate,
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    customer.Country,
                    invoice.Total
                })
                .Take(100)
                .ToList();
    }

    private object ExecuteRel7()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
                from genre in genreGroup.DefaultIfEmpty()
                join mediaType in context.GetTable<MediaType>() on track.MediaTypeId equals mediaType.id into mtGroup
                from mediaType in mtGroup.DefaultIfEmpty()
                orderby artist.Name, album.Title, track.TrackNumber
                select new
                {
                    TrackName = track.Name,
                    ArtistName = artist.Name,
                    AlbumTitle = album.Title,
                    GenreName = genre != null ? genre.Name : "Unknown",
                    MediaTypeName = mediaType != null ? mediaType.Name : "Unknown",
                    track.UnitPrice,
                    DurationMinutes = track.Milliseconds / 1000.0 / 60.0
                })
                .Take(50)
                .ToList();
    }

    private object ExecuteRel8()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                select new 
                { 
                    track.Name, 
                    Composer = track.Composer ?? "No Composer"
                })
                .Take(50)
                .ToList();
    }

    // Aggregation queries
    private object ExecuteAgg4()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoice in context.GetTable<Invoice>()
                join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                group invoice by new { customer.id, CustomerName = customer.FirstName + " " + customer.LastName, customer.Country } into g
                select new
                {
                    Customer = g.Key.CustomerName,
                    Country = g.Key.Country,
                    TotalSpent = g.Sum(i => i.Total),
                    InvoiceCount = g.Count()
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(20)
                .ToList();
    }

    private object ExecuteAgg5()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoiceLine in context.GetTable<InvoiceLine>()
                join track in context.GetTable<Track>() on invoiceLine.TrackId equals track.id
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                group invoiceLine by new { genre.id, genre.Name } into g
                select new
                {
                    Genre = g.Key.Name,
                    TotalRevenue = g.Sum(il => il.UnitPrice * il.Quantity),
                    UnitsSold = g.Sum(il => il.Quantity)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();
    }

    private object ExecuteAgg6()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoice in context.GetTable<Invoice>()
                join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                group invoice by customer.Country into g
                select new
                {
                    Country = g.Key,
                    AvgInvoiceTotal = g.Average(i => i.Total),
                    InvoiceCount = g.Count(),
                    TotalRevenue = g.Sum(i => i.Total)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ToList();
    }

    private object ExecuteAgg7()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var tracks = context.GetTable<Track>().ToList();
        return new List<object>
        {
            new
            {
                MinPrice = tracks.Min(t => t.UnitPrice),
                MaxPrice = tracks.Max(t => t.UnitPrice),
                AvgPrice = tracks.Average(t => t.UnitPrice),
                TotalTracks = tracks.Count
            }
        };
    }

    private object ExecuteAgg8()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var dbTimer = Stopwatch.StartNew();
        // Optimized: Pre-aggregate invoice data first, then join with customers
        // This avoids multiple Where() iterations in the SELECT projection
        var customerStats = (from invoice in context.GetTable<Invoice>()
                            group invoice by invoice.CustomerId into g
                            select new
                            {
                                CustomerId = g.Key,
                                TotalSpent = g.Sum(i => i.Total),
                                OrderCount = g.Count(),
                                AvgOrderValue = g.Average(i => i.Total)
                            }).ToList();

        var customers = context.GetTable<Customer>().ToList();
        dbTimer.Stop();
        System.Diagnostics.Debug.WriteLine($"[agg_8] Database query time: {dbTimer.ElapsedMilliseconds}ms");

        return (from stat in customerStats
                join customer in customers on stat.CustomerId equals customer.id
                select new
                {
                    Customer = customer.FirstName + " " + customer.LastName,
                    Country = customer.Country,
                    TotalSpent = stat.TotalSpent,
                    OrderCount = stat.OrderCount,
                    AvgOrderValue = stat.AvgOrderValue
                })
                .OrderByDescending(x => x.TotalSpent)
                .Take(30)
                .ToList();
    }

    private object ExecuteAgg9()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                group track by new { album.id, album.Title, artist.Name } into g
                select new
                {
                    AlbumTitle = g.Key.Title,
                    ArtistName = g.Key.Name,
                    TrackCount = g.Count(),
                    TotalDurationMinutes = g.Sum(t => t.Milliseconds) / 1000.0 / 60.0,
                    AvgTrackDuration = g.Average(t => t.Milliseconds) / 1000.0 / 60.0
                })
                .OrderByDescending(x => x.TrackCount)
                .Take(30)
                .ToList();
    }

    private object ExecuteAgg10()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoiceLine in context.GetTable<InvoiceLine>()
                join track in context.GetTable<Track>() on invoiceLine.TrackId equals track.id
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                group invoiceLine by new { artist.id, artist.Name } into g
                select new
                {
                    ArtistName = g.Key.Name,
                    TotalRevenue = g.Sum(il => il.UnitPrice * il.Quantity),
                    TracksSold = g.Sum(il => il.Quantity)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(20)
                .ToList();
    }

    // Advanced LINQ queries
    private object ExecuteAdv3()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return context.GetTable<Track>()
            .Where(t => (t.UnitPrice >= 1.0m && t.Milliseconds >= 180000) || 
                        (t.UnitPrice < 1.0m && t.Milliseconds < 180000))
            .OrderBy(t => t.UnitPrice)
            .Take(50)
            .ToList();
    }

    private object ExecuteAdv4()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var artistIds = context.GetTable<Artist>()
            .OrderBy(a => a.Name)
            .Take(50)
            .Select(a => a.id)
            .ToList();

        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                where artistIds.Contains(album.ArtistId)
                orderby track.Name
                select track)
                .Take(100)
                .ToList();
    }

    private object ExecuteAdv5()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from customer in context.GetTable<Customer>()
                let invoiceCount = (from invoice in context.GetTable<Invoice>()
                                   where invoice.CustomerId == customer.id
                                   select invoice).Count()
                orderby invoiceCount
                select new
                {
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    customer.Country,
                    InvoiceCount = invoiceCount
                })
                .Take(50)
                .ToList();
    }

    private object ExecuteAdv6()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from album in context.GetTable<Album>()
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                group album by new { artist.id, artist.Name } into g
                orderby g.Count() descending
                select new
                {
                    ArtistName = g.Key.Name,
                    AlbumCount = g.Count()
                })
                .Take(50)
                .ToList();
    }

    private object ExecuteAdv7()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var tracks = context.GetTable<Track>().ToList();
        return new List<object>
        {
            new
            {
                CheapTracks = tracks.Count(t => t.UnitPrice < 1.0m),
                MidPriceTracks = tracks.Count(t => t.UnitPrice >= 1.0m && t.UnitPrice < 1.5m),
                ExpensiveTracks = tracks.Count(t => t.UnitPrice >= 1.5m),
                AvgPrice = tracks.Average(t => t.UnitPrice),
                MaxPrice = tracks.Max(t => t.UnitPrice),
                MinPrice = tracks.Min(t => t.UnitPrice)
            }
        };
    }

    private object ExecuteAdv8()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var dbTimer = Stopwatch.StartNew();
        // SQLite doesn't support CROSS/OUTER APPLY, so materialize first
        var tracksWithGenre = (from track in context.GetTable<Track>()
                               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                               select new
                               {
                                   GenreId = genre.id,
                                   GenreName = genre.Name,
                                   TrackName = track.Name,
                                   Milliseconds = track.Milliseconds
                               }).ToList();
        dbTimer.Stop();
        System.Diagnostics.Debug.WriteLine($"[adv_8] Database query time: {dbTimer.ElapsedMilliseconds}ms");

        // Now perform Top-3 per group in memory
        return tracksWithGenre
                .GroupBy(t => new { t.GenreId, t.GenreName })
                .SelectMany(g => g.OrderByDescending(t => t.Milliseconds).Take(3)
                    .Select(track => new
                    {
                        Genre = g.Key.GenreName,
                        TrackName = track.TrackName,
                        DurationMinutes = track.Milliseconds / 1000.0 / 60.0
                    }))
                .OrderBy(x => x.Genre)
                .ThenByDescending(x => x.DurationMinutes)
                .ToList();
    }

    private object ExecuteAdv9()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from invoice in context.GetTable<Invoice>()
                join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                orderby invoice.InvoiceDate descending
                select new
                {
                    invoice.InvoiceDate,
                    CustomerName = customer.FirstName + " " + customer.LastName,
                    invoice.Total,
                    DaysAgo = (DateTime.Now - invoice.InvoiceDate).Days
                })
                .Take(100)
                .ToList();
    }

    private object ExecuteAdv10()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return context.GetTable<Artist>()
            .Select(a => new
            {
                OriginalName = a.Name,
                UpperName = a.Name.ToUpper(),
                LowerName = a.Name.ToLower(),
                FirstThreeChars = a.Name.Length >= 3 ? a.Name.Substring(0, 3) : a.Name,
                NameLength = a.Name.Length
            })
            .OrderBy(a => a.OriginalName)
            .Take(20)
            .ToList();
    }

    private object ExecuteAdv11()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var artistNames = context.GetTable<Artist>()
            .Select(a => new { Name = a.Name, Type = "Artist" })
            .Take(10);

        var albumTitles = context.GetTable<Album>()
            .Select(a => new { Name = a.Title, Type = "Album" })
            .Take(10);

        return artistNames.Union(albumTitles)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToList();
    }

    // Performance queries
    private object ExecutePerf5()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var expensiveAlbums = context.GetTable<Album>()
            .Where(a => a.Title.StartsWith("A"))
            .Take(50);

        return (from album in expensiveAlbums
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                select new { album.Title, artist.Name })
                .ToList();
    }

    private object ExecutePerf6()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var hasExpensiveTracks = context.GetTable<Track>().Any(t => t.UnitPrice > 1.50m);
        var expensiveCount = context.GetTable<Track>().Count(t => t.UnitPrice > 1.50m);

        return new List<object>
        {
            new { HasExpensiveTracks = hasExpensiveTracks, Count = expensiveCount }
        };
    }

    private object ExecutePerf7()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                select new 
                { 
                    TrackName = track.Name, 
                    AlbumTitle = album.Title 
                })
                .Take(100)
                .ToList();
    }

    private object ExecutePerf8()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return context.GetTable<Customer>()
            .Select(c => c.Country)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    // Many-to-Many queries
    private object ExecuteM2M4()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var dbTimer = Stopwatch.StartNew();
        // Materialize the grouped data first - SQLite can't translate Distinct().Count() in projection
        var trackPlaylistGroups = (from pt in context.GetTable<PlaylistTrack>()
                                   join track in context.GetTable<Track>() on pt.TrackId equals track.id
                                   select new
                                   {
                                       TrackId = track.id,
                                       TrackName = track.Name,
                                       PlaylistId = pt.PlaylistId
                                   }).ToList();
        dbTimer.Stop();
        System.Diagnostics.Debug.WriteLine($"[m2m_4] Database query time: {dbTimer.ElapsedMilliseconds}ms");

        // Now perform distinct count in memory
        return trackPlaylistGroups
                .GroupBy(x => new { x.TrackId, x.TrackName })
                .Select(g => new
                {
                    TrackName = g.Key.TrackName,
                    PlaylistCount = g.Select(x => x.PlaylistId).Distinct().Count()
                })
                .Where(x => x.PlaylistCount > 1)
                .OrderByDescending(x => x.PlaylistCount)
                .Take(30)
                .ToList();
    }

    private object ExecuteM2M5()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var dbTimer = Stopwatch.StartNew();
        // Materialize the joins first - avoid hanging on Distinct().Count() in projection
        var trackData = (from pt in context.GetTable<PlaylistTrack>()
                        join track in context.GetTable<Track>() on pt.TrackId equals track.id
                        join album in context.GetTable<Album>() on track.AlbumId equals album.id
                        join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                        select new
                        {
                            TrackId = track.id,
                            TrackName = track.Name,
                            ArtistName = artist.Name,
                            PlaylistId = pt.PlaylistId
                        }).ToList();
        dbTimer.Stop();
        System.Diagnostics.Debug.WriteLine($"[m2m_5] Database query time: {dbTimer.ElapsedMilliseconds}ms");

        // Perform distinct count in memory
        return trackData
                .GroupBy(x => new { x.TrackId, x.TrackName, x.ArtistName })
                .Select(g => new
                {
                    TrackName = g.Key.TrackName,
                    ArtistName = g.Key.ArtistName,
                    PlaylistCount = g.Select(x => x.PlaylistId).Distinct().Count()
                })
                .OrderByDescending(x => x.PlaylistCount)
                .Take(20)
                .ToList();
    }

    private object ExecuteM2M6()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var dbTimer = Stopwatch.StartNew();
        // Materialize playlist track counts (10k playlist tracks / 50 playlists = ~200 avg)
        var playlistCounts = (from pt in context.GetTable<PlaylistTrack>()
                             group pt by pt.PlaylistId into g
                             select new
                             {
                                 PlaylistId = g.Key,
                                 TrackCount = g.Count()
                             }).ToList();

        var playlists = context.GetTable<Playlist>().ToList();
        dbTimer.Stop();
        System.Diagnostics.Debug.WriteLine($"[m2m_6] Database query time: {dbTimer.ElapsedMilliseconds}ms");

        // Join in memory and filter for playlists with fewer than 250 tracks
        return (from pc in playlistCounts
                join p in playlists on pc.PlaylistId equals p.id
                where pc.TrackCount < 250
                orderby pc.TrackCount
                select new
                {
                    Name = p.Name,
                    TrackCount = pc.TrackCount
                })
                .Take(20)
                .ToList();
    }

    private object ExecuteM2M7()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return new List<object>
        {
            new { Message = "Pattern: Create PlaylistTrack with both IDs and SaveAsync()" }
        };
    }

    private object ExecuteM2M8()
    {
        using var context = new SxmLinqDbContext("Chinook");
        return (from pt1 in context.GetTable<PlaylistTrack>()
                join pt2 in context.GetTable<PlaylistTrack>() on pt1.TrackId equals pt2.TrackId
                where pt1.PlaylistId < pt2.PlaylistId
                join p1 in context.GetTable<Playlist>() on pt1.PlaylistId equals p1.id
                join p2 in context.GetTable<Playlist>() on pt2.PlaylistId equals p2.id
                group pt1 by new { Playlist1Name = p1.Name, Playlist2Name = p2.Name } into g
                select new
                {
                    Playlist1 = g.Key.Playlist1Name,
                    Playlist2 = g.Key.Playlist2Name,
                    SharedTracks = g.Count()
                })
                .OrderByDescending(x => x.SharedTracks)
                .Take(10)
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

    // Transaction examples
    private async Task<object> ExecuteTrans1Async()
    {
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        try
        {
            var invoice = new Invoice
            {
                CustomerId = 1,
                InvoiceDate = DateTime.Now,
                BillingAddress = "123 Demo St",
                BillingCity = "Portland",
                BillingCountry = "USA",
                Total = 5.97m
            };
            await invoice.SaveAsync(transaction);

            var line1 = new InvoiceLine { InvoiceId = invoice.id, TrackId = 1, UnitPrice = 1.99m, Quantity = 1 };
            await line1.SaveAsync(transaction);

            var line2 = new InvoiceLine { InvoiceId = invoice.id, TrackId = 2, UnitPrice = 1.99m, Quantity = 2 };
            await line2.SaveAsync(transaction);

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, InvoiceId = invoice.id, TotalAmount = invoice.Total, LineCount = 2 } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }

    private async Task<object> ExecuteTrans2Async()
    {
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        try
        {
            var artist = new Artist { Name = "Transaction Test Artist" };
            await artist.SaveAsync(transaction);

            var album = new Album { Title = "Test Album", ArtistId = artist.id };
            await album.SaveAsync(transaction);

            throw new Exception("Simulated error - all changes will be rolled back");

#pragma warning disable CS0162
            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
            return new[] { new { Success = true } };
#pragma warning restore CS0162
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message, Note = "All changes were rolled back" } };
        }
    }

    private async Task<object> ExecuteTrans3Async()
    {
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        try
        {
            var insertedCount = 0;
            var startTime = DateTime.Now;

            for (int i = 1; i <= 50; i++)
            {
                var track = new Track
                {
                    Name = $"Batch Track {i}",
                    AlbumId = 1,
                    MediaTypeId = 1,
                    GenreId = 1,
                    Milliseconds = 180000,
                    UnitPrice = 0.99m
                };
                await track.SaveAsync(transaction);
                insertedCount++;
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            return new[] { new { Success = true, TracksInserted = insertedCount, ElapsedMs = elapsed, Note = "All inserts in single transaction" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }

    private async Task<object> ExecuteTrans4Async()
    {
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        using var context = new SxmLinqDbContext("Chinook");
        try
        {
            var artist = context.GetTable<Artist>().First();
            var albums = context.GetTable<Album>().Where(a => a.ArtistId == artist.id).Take(3).ToList();

            var originalName = artist.Name;
            artist.Name = artist.Name + " (Updated)";
            await artist.SaveAsync(transaction);

            foreach (var album in albums)
            {
                album.Title = album.Title + " [Remastered]";
                await album.SaveAsync(transaction);
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, ArtistOriginal = originalName, ArtistUpdated = artist.Name, AlbumsUpdated = albums.Count } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }

    private async Task<object> ExecuteTrans5Async()
    {
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        using var context = new SxmLinqDbContext("Chinook");
        try
        {
            var playlist = new Playlist { Name = $"Transaction Demo Playlist {DateTime.Now:HHmmss}" };
            await playlist.SaveAsync(transaction);

            var topTracks = context.GetTable<Track>().OrderBy(t => t.Name).Take(10).ToList();

            var trackCount = 0;
            foreach (var track in topTracks)
            {
                var pt = new PlaylistTrack { PlaylistId = playlist.id, TrackId = track.id };
                await pt.SaveAsync(transaction);
                trackCount++;
            }

            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, PlaylistId = playlist.id, PlaylistName = playlist.Name, TracksAdded = trackCount, Note = "All operations committed together" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }

    private async Task<object> ExecuteTrans6Async()
    {
        var results = new List<object>();

        var start1 = DateTime.Now;
        for (int i = 1; i <= 20; i++)
        {
            var track = new Track { Name = $"No-Transaction Track {i}", AlbumId = 1, MediaTypeId = 1, GenreId = 1, Milliseconds = 180000, UnitPrice = 0.99m };
            await track.SaveAsync();
        }
        var noTransTime = (DateTime.Now - start1).TotalMilliseconds;

        var start2 = DateTime.Now;
        await using (var transaction = SxmSqlTransaction.Create("Chinook"))
        {
            for (int i = 1; i <= 20; i++)
            {
                var track = new Track { Name = $"Transaction Track {i}", AlbumId = 1, MediaTypeId = 1, GenreId = 1, Milliseconds = 180000, UnitPrice = 0.99m };
                await track.SaveAsync(transaction);
            }
            // Commit transaction. The explicit CommitTransactionAsync() call is optional
            // but considered good practice. Without it, the transaction will AUTO-COMMIT
            // on Dispose (If No Errors)
            await transaction.CommitTransactionAsync();
        }
        var transTime = (DateTime.Now - start2).TotalMilliseconds;

        results.Add(new { Method = "Without Transaction", Inserts = 20, TimeMs = noTransTime });
        results.Add(new { Method = "With Transaction", Inserts = 20, TimeMs = transTime, SpeedupFactor = Math.Round(noTransTime / transTime, 2) });

        return results;
    }

    // Parameterized query examples
    private object ExecuteParam1()
    {
        using var context = new SxmLinqDbContext("Chinook");
        string searchTerm = "Love";
        return context.GetTable<Track>().Where(t => t.Name.Contains(searchTerm)).OrderBy(t => t.Name).Take(20).ToList();
    }

    private object ExecuteParam2()
    {
        using var context = new SxmLinqDbContext("Chinook");
        decimal minPrice = 0.99m;
        decimal maxPrice = 1.49m;
        return context.GetTable<Track>()
            .Where(t => t.UnitPrice >= minPrice && t.UnitPrice <= maxPrice)
            .OrderBy(t => t.UnitPrice).ThenBy(t => t.Name).Take(50)
            .Select(t => new { t.Name, t.UnitPrice, DurationMinutes = t.Milliseconds / 1000.0 / 60.0 })
            .ToList();
    }

    private object ExecuteParam3()
    {
        using var context = new SxmLinqDbContext("Chinook");

        var startDate = DateTime.Now.AddYears(-3);
        var endDate = DateTime.Now;

        // Two-phase approach: DateTime comparisons in WHERE clauses with JOINs don't translate
        // correctly due to linq2db's expression tree handling of custom type conversions.
        // Phase 1: Fetch all invoices with customer data from database
        var invoicesWithCustomers = (from invoice in context.GetTable<Invoice>()
                                      join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                                      select new
                                      {
                                          InvoiceId = invoice.id,
                                          Date = invoice.InvoiceDate,
                                          Customer = customer.FirstName + " " + customer.LastName,
                                          Total = invoice.Total
                                      }).ToList();

        // Phase 2: Filter by date in memory
        return invoicesWithCustomers
            .Where(i => i.Date >= startDate && i.Date <= endDate)
            .OrderByDescending(i => i.Date)
            .Take(30)
            .ToList();
    }

    private object ExecuteParam4()
    {
        using var context = new SxmLinqDbContext("Chinook");
        string artistSearchTerm = "Led";
        int genreId = 1;
        decimal maxPrice = 1.50m;
        return (from track in context.GetTable<Track>()
                join album in context.GetTable<Album>() on track.AlbumId equals album.id
                join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                where artist.Name.Contains(artistSearchTerm) && track.GenreId == genreId && track.UnitPrice <= maxPrice
                orderby track.Name
                select new { Track = track.Name, Artist = artist.Name, Genre = genre.Name, Price = track.UnitPrice })
                .Take(30).ToList();
    }

    private object ExecuteParam5()
    {
        using var context = new SxmLinqDbContext("Chinook");
        string? artistFilter = "Led";  // Search for "Led" (Led Zeppelin)
        decimal? minDuration = 180000;

        var query = context.GetTable<Track>()
            .Join(context.GetTable<Album>(), t => t.AlbumId, a => a.id, (t, a) => new { Track = t, Album = a })
            .Join(context.GetTable<Artist>(), ta => ta.Album.ArtistId, ar => ar.id, (ta, ar) => new { ta.Track, ta.Album, Artist = ar });

        if (!string.IsNullOrEmpty(artistFilter))
            query = query.Where(x => x.Artist.Name.Contains(artistFilter));
        if (minDuration.HasValue)
            query = query.Where(x => x.Track.Milliseconds >= minDuration.Value);

        return query.OrderBy(x => x.Track.Name).Take(30)
            .Select(x => new { Track = x.Track.Name, Artist = x.Artist.Name, DurationMinutes = x.Track.Milliseconds / 1000.0 / 60.0 })
            .ToList();
    }

    private object ExecuteParam6()
    {
        using var context = new SxmLinqDbContext("Chinook");
        string pattern = "Track";  // Searches for "Track" in track names
        return context.GetTable<Track>().Where(t => t.Name.Contains(pattern)).OrderBy(t => t.Name).Take(30)
            .Select(t => new { TrackName = t.Name, t.UnitPrice, DurationSeconds = t.Milliseconds / 1000 })
            .ToList();
    }

    // Data modification examples
    private async Task<object> ExecuteMod1Async()
    {
        var track = new Track
        {
            Name = $"New Demo Track {DateTime.Now:HHmmss}",
            AlbumId = 1,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "Demo Composer",
            Milliseconds = 240000,
            Bytes = 4000000,
            UnitPrice = 1.29m,
            TrackNumber = 1
        };
        await track.SaveAsync();
        return new[] { new { Success = true, TrackId = track.id, TrackName = track.Name, Message = "Track inserted successfully" } };
    }

    private async Task<object> ExecuteMod2Async()
    {
        var artist = new Artist { Name = $"New Artist {DateTime.Now:HHmmss}" };
        await artist.SaveAsync();
        var artistId = artist.id;

        var album = new Album { Title = "Debut Album", ArtistId = artistId };
        await album.SaveAsync();

        return new[] { new { ArtistId = artistId, ArtistName = artist.Name, AlbumId = album.id, AlbumTitle = album.Title, Message = "Artist and Album created with auto-generated IDs" } };
    }

    private async Task<object> ExecuteMod3Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var track = context.GetTable<Track>().First();
        var originalPrice = track.UnitPrice;
        track.UnitPrice = 1.99m;
        await track.SaveAsync();
        return new[] { new { TrackId = track.id, TrackName = track.Name, OriginalPrice = originalPrice, NewPrice = track.UnitPrice, Message = "Price updated successfully" } };
    }

    private async Task<object> ExecuteMod4Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var cheapTracks = context.GetTable<Track>().Where(t => t.UnitPrice < 1.00m).Take(10).ToList();
        var updateCount = 0;
        foreach (var track in cheapTracks)
        {
            track.UnitPrice = 1.29m;
            await track.SaveAsync();
            updateCount++;
        }
        return new[] { new { TracksUpdated = updateCount, NewPrice = 1.29m, Message = $"Updated {updateCount} tracks to new price" } };
    }

    private async Task<object> ExecuteMod5Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var rockGenre = context.GetTable<Genre>().FirstOrDefault(g => g.Name.Contains("Rock"));
        if (rockGenre != null)
        {
            var rockTracks = context.GetTable<Track>().Where(t => t.GenreId == rockGenre.id).Take(20).ToList();
            var updateCount = 0;
            foreach (var track in rockTracks)
            {
                track.UnitPrice = track.UnitPrice * 1.10m;
                await track.SaveAsync();
                updateCount++;
            }
            return new[] { new { Genre = rockGenre.Name, TracksUpdated = updateCount, PriceIncrease = "10%", Message = $"Updated {updateCount} rock tracks" } };
        }
        return new[] { new { Message = "Rock genre not found" } };
    }

    private async Task<object> ExecuteMod6Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var playlist = context.GetTable<Playlist>().FirstOrDefault(p => p.Name.Contains("Demo"));
        if (playlist != null)
        {
            var playlistName = playlist.Name;
            await playlist.DeleteAsync();
            return new[] { new { Success = true, DeletedPlaylist = playlistName, Message = "Playlist deleted successfully" } };
        }
        return new[] { new { Success = false, Message = "No demo playlist found to delete" } };
    }

    private async Task<object> ExecuteMod7Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        var oldPlaylists = context.GetTable<Playlist>().Where(p => p.Name.Contains("Old") || p.Name.Contains("Demo")).Take(5).ToList();
        var deleteCount = 0;
        foreach (var playlist in oldPlaylists)
        {
            await playlist.DeleteAsync();
            deleteCount++;
        }
        return new[] { new { PlaylistsDeleted = deleteCount, Message = $"Deleted {deleteCount} old playlists" } };
    }

    private async Task<object> ExecuteMod8Async()
    {
        using var context = new SxmLinqDbContext("Chinook");
        await using var transaction = SxmSqlTransaction.Create("Chinook");
        try
        {
            var playlist = context.GetTable<Playlist>().FirstOrDefault(p => p.Name.Contains("Test"));
            if (playlist != null)
            {
                var playlistTracks = context.GetTable<PlaylistTrack>().Where(pt => pt.PlaylistId == playlist.id).ToList();
                var trackCount = playlistTracks.Count;
                foreach (var pt in playlistTracks)
                {
                    await pt.DeleteAsync(transaction);
                }
                await playlist.DeleteAsync(transaction);
                // Commit transaction. The explicit CommitTransactionAsync() call is optional
                // but considered good practice. Without it, the transaction will AUTO-COMMIT
                // on Dispose (If No Errors)
                await transaction.CommitTransactionAsync();
                return new[] { new { Success = true, PlaylistName = playlist.Name, TracksRemoved = trackCount, Message = "Playlist and tracks deleted in transaction" } };
            }
            return new[] { new { Success = false, Message = "Playlist not found" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

