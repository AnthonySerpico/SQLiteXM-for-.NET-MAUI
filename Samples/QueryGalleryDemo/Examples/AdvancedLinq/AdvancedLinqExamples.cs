using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.AdvancedLinq;

[QueryExample(
    id: "adv_1",
    name: "Paging with Skip/Take",
    description: "Implement pagination for large result sets",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define page size (20 records per page)
2. Specify page number (1-based)
3. OrderBy ensures consistent ordering
4. Skip calculates offset: (page-1) x size
5. Take limits to page size
6. Execute and return one page

**Key Concepts:**
- Pagination is essential for large datasets
- Skip/Take pattern is standard for paging
- MUST have OrderBy for predictable results
- Formula: skip (pageNumber-1) x pageSize records
- Common in web APIs and list views
""")]
internal sealed class Adv1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");

        int pageSize = 20;
        int pageNumber = 1; // Change to test different pages
        var tracks = context.GetTable<Track>()
            .OrderBy(t => t.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "adv_2",
    name: "Multiple ORDER BY",
    description: "Sort by multiple columns with different directions",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Track -> Album -> Artist
2. Sort by artist name (primary)
3. Then by album title (secondary)
4. Then by track number (tertiary)
5. All sorts are ascending
6. Take first 100 results
7. Return sorted list

**Key Concepts:**
- Multiple ORDER BY creates hierarchical sorting
- Order matters: first sort is primary, then secondary, etc.
- Essential for album/track listings in correct order
- TrackNumber ensures songs play in intended sequence
- This is how music players organize tracks
""")]
internal sealed class Adv2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from track in context.GetTable<Track>()
                       join album in context.GetTable<Album>() on track.AlbumId equals album.id
                       join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                       orderby artist.Name ascending, album.Title ascending, track.TrackNumber ascending
                       select new { artist.Name, album.Title, track.TrackNumber, TrackName = track.Name })
                      .Take(100)
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "adv_3",
    name: "Complex WHERE with Multiple Conditions",
    description: "Combine multiple filter conditions with AND/OR",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Track table
2. Apply complex WHERE with nested conditions
3. First group: expensive ($1+) AND long (3+ min)
4. OR second group: cheap (<$1) AND short (<3 min)
5. Parentheses control logic grouping
6. Sort by price
7. Take 50 matches

**Key Concepts:**
- AND (&&) requires both conditions true
- OR (||) requires at least one condition true
- Parentheses control evaluation order
""")]
internal sealed class Adv3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var tracks = context.GetTable<Track>()
            .Where(t => (t.UnitPrice >= 1.0m && t.Milliseconds >= 180000) ||
                        (t.UnitPrice < 1.0m && t.Milliseconds < 180000))
            .OrderBy(t => t.UnitPrice)
            .Take(50)
            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "adv_4",
    name: "Subquery - IN Operator",
    description: "Use subquery results to filter another query",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. First query: get top 50 artist IDs
2. Materialize to list with ToList()
3. Second query: get tracks
4. JOIN to albums
5. Filter using Contains() - SQL IN operator
6. Only tracks from those 50 artists pass
7. Take 100 tracks

**Key Concepts:**
- Two-phase query pattern
- Contains() translates to SQL IN clause
- Subquery results used to filter main query
""")]
internal sealed class Adv4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");

        var artistIds = context.GetTable<Artist>()
            .OrderBy(a => a.Name)
            .Take(50)
            .Select(a => a.id)
            .ToList();

        var tracks = (from track in context.GetTable<Track>()
                      join album in context.GetTable<Album>() on track.AlbumId equals album.id
                      where artistIds.Contains(album.ArtistId)
                      orderby track.Name
                      select track)
                     .Take(100)
                     .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "adv_5",
    name: "Subquery with Count",
    description: "Count related records for each customer",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Customer table
2. Use 'let' to define subquery variable
3. Subquery counts invoices per customer
4. Sort by invoice count
5. Project customer info + count
6. Take first 50 customers

**Key Concepts:**
- 'let' keyword defines intermediate values
- Correlated subquery references outer query
""")]
internal sealed class Adv5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var customerInvoiceCounts = (from customer in context.GetTable<Customer>()
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
        return Task.FromResult<object>(customerInvoiceCounts);
    }
}

[QueryExample(
    id: "adv_6",
    name: "HAVING Clause (Filter Groups)",
    description: "Group by artist and show album counts",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Album with Artist
2. GROUP BY artist
3. Order by album count descending
4. Take top 50 artists

**Key Concepts:**
- OrderBy after grouping sorts aggregates
- LINQ doesn't have explicit HAVING; use OrderBy/Where on groups
""")]
internal sealed class Adv6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from album in context.GetTable<Album>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "adv_7",
    name: "Conditional Aggregates",
    description: "Count tracks with different price ranges",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Load all tracks into memory
2. Count tracks in cheap/mid/expensive ranges
3. Calculate average, min, max prices
4. Return single summary object

**Key Concepts:**
- Conditional aggregates: Count() with predicate
- Multiple aggregates over same dataset
""")]
internal sealed class Adv7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var tracks = context.GetTable<Track>().ToList();
        var priceAnalysis = new
        {
            CheapTracks = tracks.Count(t => t.UnitPrice < 1.0m),
            MidPriceTracks = tracks.Count(t => t.UnitPrice >= 1.0m && t.UnitPrice < 1.5m),
            ExpensiveTracks = tracks.Count(t => t.UnitPrice >= 1.5m),
            AvgPrice = tracks.Average(t => t.UnitPrice),
            MaxPrice = tracks.Max(t => t.UnitPrice),
            MinPrice = tracks.Min(t => t.UnitPrice)
        };
        return Task.FromResult<object>(new[] { priceAnalysis });
    }
}

[QueryExample(
    id: "adv_8",
    name: "Top N per Group",
    description: "Get top 3 longest tracks per genre (SQLite-compatible)",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Track with Genre and materialize
2. GroupBy genre in memory
3. For each group, OrderByDescending and Take(3)
4. SelectMany flattens back to list

**Key Concepts:**
- SQLite lacks window functions; workaround with in-memory grouping
- GroupBy + SelectMany pattern for partitioned results
""")]
internal sealed class Adv8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
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

        var results = tracksWithGenre
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "adv_9",
    name: "Date Range Queries",
    description: "Query invoices with date filtering",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Invoice with Customer
2. Sort by InvoiceDate descending (newest first)
3. Calculate DaysAgo using DateTime arithmetic
4. Take 100 most recent invoices

**Key Concepts:**
- Date arithmetic: DateTime.Now - invoice date
- .Days property gives difference in days
""")]
internal sealed class Adv9Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var recentInvoices = (from invoice in context.GetTable<Invoice>()
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
        return Task.FromResult<object>(recentInvoices);
    }
}

[QueryExample(
    id: "adv_10",
    name: "String Manipulation",
    description: "Use string functions - UPPER, LOWER, TRIM, SUBSTRING",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Artist table
2. Project with string transformations (ToUpper, ToLower, Substring, Length)
3. Sort by original name
4. Take 20 artists

**Key Concepts:**
- String functions translate to SQL equivalents
- Common for data cleaning and formatting
""")]
internal sealed class Adv10Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var artists = context.GetTable<Artist>()
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
        return Task.FromResult<object>(artists);
    }
}

[QueryExample(
    id: "adv_11",
    name: "UNION - Combine Results",
    description: "Combine artists and album titles into one list",
    category: QueryCategory.AdvancedLinq,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Select 10 artists with type label
2. Select 10 albums with type label
3. Union() combines and dedupes
4. Sort by Type, then Name

**Key Concepts:**
- UNION combines multiple queries into one result
- Union() removes duplicates; Concat() keeps all
""")]
internal sealed class Adv11Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var artistNames = context.GetTable<Artist>()
            .Select(a => new { Name = a.Name, Type = "Artist" })
            .Take(10);

        var albumTitles = context.GetTable<Album>()
            .Select(a => new { Name = a.Title, Type = "Album" })
            .Take(10);

        var combined = artistNames.Union(albumTitles)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToList();
        return Task.FromResult<object>(combined);
    }
}
