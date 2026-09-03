using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Performance;

[QueryExample(
    id: "perf_1",
    name: "Query 1000+ Tracks",
    description: "Performance test with large result set",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Track table
2. Sort by name (consistent ordering)
3. Limit to 1000 rows
4. Execute query and materialize results

**Performance Tips:**
- Take() limits result set size
- OrderBy ensures predictable results
- For very large sets, consider pagination
""")]
internal sealed class Perf1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var tracks = ctx.GetTable<Track>()
            .OrderBy(t => t.Name)
            .Take(1000)
            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "perf_2",
    name: "Complex Multi-Table Join",
    description: "JOIN 5 tables with filtering",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with InvoiceLine (transactional data)
2. JOIN 4 additional tables
3. Filter to USA customers only
4. Project combined data
5. Limit to 500 results

**Performance Tips:**
- WHERE clause filters early
- SQLite handles joins efficiently with indexes
- Take() prevents unbounded result sets
""")]
internal sealed class Perf2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from invoiceLine in ctx.GetTable<InvoiceLine>()
                       join invoice in ctx.GetTable<Invoice>() on invoiceLine.InvoiceId equals invoice.id
                       join customer in ctx.GetTable<Customer>() on invoice.CustomerId equals customer.id
                       join track in ctx.GetTable<Track>() on invoiceLine.TrackId equals track.id
                       join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "perf_3",
    name: "Pagination with Skip/Take",
    description: "Efficiently paginate through large result sets",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define page size (20 items)
2. Calculate offset
3. OrderBy ensures consistent ordering
4. Skip + Take return one page

**Performance Tips:**
- Always use OrderBy before Skip/Take
- SQLite translates to LIMIT/OFFSET
""")]
internal sealed class Perf3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        int pageNumber = 2;
        int pageSize = 20;
        var page = ctx.GetTable<Track>()
            .OrderBy(t => t.id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<object>(page);
    }
}

[QueryExample(
    id: "perf_4",
    name: "Select Only Required Columns",
    description: "Reduce data transfer by projecting only needed fields",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Track table
2. Project only 3 columns
3. Take 100 records

**Performance Tips:**
- SELECT only needed columns reduces I/O
- Smaller result sets = less memory
- Faster network transfer
""")]
internal sealed class Perf4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var lightweightTracks = ctx.GetTable<Track>()
            .Select(t => new { t.id, t.Name, t.UnitPrice })
            .Take(100)
            .ToList();
        return Task.FromResult<object>(lightweightTracks);
    }
}

[QueryExample(
    id: "perf_5",
    name: "Early Filtering",
    description: "Filter before joining for better performance",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Filter albums first (titles starting with 'A')
2. Limit to 50 albums
3. Join to Artist table
4. Project result

**Performance Tips:**
- Filter early reduces join size
- WHERE before JOIN is a key optimization
""")]
internal sealed class Perf5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var expensiveAlbums = ctx.GetTable<Album>()
            .Where(a => a.Title.StartsWith("A"))
            .Take(50);

        var results = (from album in expensiveAlbums
                       join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
                       select new { album.Title, artist.Name })
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "perf_6",
    name: "Count Performance",
    description: "Compare Count() vs Any() for existence checks",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Any() checks if at least one track > $1.50
2. Count() counts all matching tracks
3. Both results returned together

**Performance Tips:**
- Any() is faster for existence checks
- Any() stops at first match
- Count() must scan all matching rows
""")]
internal sealed class Perf6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var hasExpensiveTracks = ctx.GetTable<Track>().Any(t => t.UnitPrice > 1.50m);
        var expensiveCount = ctx.GetTable<Track>().Count(t => t.UnitPrice > 1.50m);

        var result = new List<object>
        {
            new { HasExpensiveTracks = hasExpensiveTracks, Count = expensiveCount }
        };
        return Task.FromResult<object>(result);
    }
}

[QueryExample(
    id: "perf_7",
    name: "Avoid N+1 Queries",
    description: "Use joins instead of multiple queries",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Single query with JOIN
2. Fetch tracks and albums together
3. Project combined result
4. Take 100 results

**Performance Tips:**
- Avoid N+1 problem
- JOIN executes in single database round-trip
""")]
internal sealed class Perf7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var tracksWithAlbums = (from track in ctx.GetTable<Track>()
                                join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
                                select new
                                {
                                    TrackName = track.Name,
                                    AlbumTitle = album.Title
                                })
                               .Take(100)
                               .ToList();
        return Task.FromResult<object>(tracksWithAlbums);
    }
}

[QueryExample(
    id: "perf_8",
    name: "Efficient Distinct",
    description: "Get unique values efficiently",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Select Country column only
2. Apply Distinct()
3. Sort alphabetically

**Performance Tips:**
- Select single column before Distinct() is efficient
- SQLite translates to SELECT DISTINCT
""")]
internal sealed class Perf8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var uniqueCountries = ctx.GetTable<Customer>()
            .Select(c => c.Country)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        return Task.FromResult<object>(uniqueCountries);
    }
}

[QueryExample(
    id: "perf_9",
    name: "Foreign Key Index Performance",
    description: "Demonstrates why indexes on foreign keys matter",
    category: QueryCategory.Performance,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start stopwatch timer
2. Query tracks joined with albums
3. Filter albums by title prefix
4. Index on Track.AlbumId accelerates the JOIN

**Why Indexes Matter:**
- Without index on AlbumId: SQLite scans entire Track table (slow)
- With index (IFK_Track_AlbumId): SQLite finds matching tracks instantly
- JOIN performance improves dramatically with proper indexing
""")]
internal sealed class Perf9Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var tracksForAlbums = (from track in ctx.GetTable<Track>()
                               join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
                               where album.Title.StartsWith("A")
                               select new { track.Name, album.Title })
                              .Take(200)
                              .ToList();

        sw.Stop();
        var elapsedMs = sw.ElapsedMilliseconds;

        var result = new List<object>
        {
            new
            {
                ResultCount = tracksForAlbums.Count,
                ElapsedMs = elapsedMs,
                Message = $"Query completed in {elapsedMs}ms using indexed foreign key"
            }
        };
        return Task.FromResult<object>(result);
    }
}
