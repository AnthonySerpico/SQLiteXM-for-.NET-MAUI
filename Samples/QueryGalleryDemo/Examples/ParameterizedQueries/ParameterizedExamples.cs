using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.ParameterizedQueries;

[QueryExample(
    id: "param_1",
    name: "Search by Name Parameter",
    description: "Find tracks matching a search term (safe from SQL injection)",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define search parameter (user input)
2. Use Contains() in LINQ WHERE clause
3. SQLiteXM generates parameterized SQL
4. Search term treated as data, not code
5. Order and limit results

**Key Concepts:**
- Parameterized queries prevent SQL injection
- User input never concatenated into SQL string
- SQLiteXM handles parameter binding automatically
- Contains() translates to SQL LIKE '%value%'
- Essential for secure user-facing search features
""")]
internal sealed class Param1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        string searchTerm = "Love";
        return Task.FromResult<object>(
            context.GetTable<Track>().Where(t => t.Name.Contains(searchTerm)).OrderBy(t => t.Name).Take(20).ToList());
    }
}

[QueryExample(
    id: "param_2",
    name: "Price Range Filter",
    description: "Find tracks within a price range using parameters",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define min and max price parameters
2. Filter with >= and <= comparisons
3. Both parameters safely bound
4. Sort by price, then name
5. Project needed fields
6. Return top 50

**Key Concepts:**
- Range queries with two parameters
- Both bounds safely parameterized
- Common UI pattern: price sliders, filters
- ThenBy for secondary sort
- Projection reduces data transfer
""")]
internal sealed class Param2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        decimal minPrice = 0.99m;
        decimal maxPrice = 1.49m;
        return Task.FromResult<object>(context.GetTable<Track>()
            .Where(t => t.UnitPrice >= minPrice && t.UnitPrice <= maxPrice)
            .OrderBy(t => t.UnitPrice).ThenBy(t => t.Name).Take(50)
            .Select(t => new { t.Name, t.UnitPrice, DurationMinutes = t.Milliseconds / 1000.0 / 60.0 })
            .ToList());
    }
}

[QueryExample(
    id: "param_3",
    name: "Date Range Query",
    description: "Find invoices within a date range (demonstrates safe parameterization)",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define start and end date parameters
2. Compare using .Ticks property (Int64)
3. SQLiteXM translates to safe SQL comparison
4. JOIN customer for display names
5. Sort newest first
6. Return 30 recent invoices

**Key Concepts:**
- DateTime stored as Ticks in SQLite
- Range filtering with date parameters
- Safe date comparisons
- Common for reporting and time-based queries
- JOIN adds customer context
""")]
internal sealed class Param3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var startDate = DateTime.Now.AddYears(-3);
        var endDate = DateTime.Now;

        var results = (from invoice in context.GetTable<Invoice>()
                       join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                       where invoice.InvoiceDate.Ticks >= startDate.Ticks
                          && invoice.InvoiceDate.Ticks <= endDate.Ticks
                       orderby invoice.InvoiceDate descending
                       select new
                       {
                           InvoiceId = invoice.id,
                           Date = invoice.InvoiceDate,
                           Customer = customer.FirstName + " " + customer.LastName,
                           Total = invoice.Total
                       })
                      .Take(30)
                      .ToList();

        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "param_4",
    name: "Multiple Search Parameters",
    description: "Search with artist, genre, and price filters",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define multiple parameters (artist, genre, price)
2. JOIN Track, Album, Artist, Genre
3. Compose WHERE clauses safely
4. Sort by track name
5. Project and take 30 results

**Key Concepts:**
- Multi-parameter search with joins
- All parameters safely bound
- Composable query building
- Common e-commerce search pattern
""")]
internal sealed class Param4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        string artistSearchTerm = "Led";
        int genreId = 1;
        decimal maxPrice = 1.50m;
        var results = (from track in context.GetTable<Track>()
                       join album in context.GetTable<Album>() on track.AlbumId equals album.id
                       join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                       join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                       where artist.Name.Contains(artistSearchTerm) && track.GenreId == genreId && track.UnitPrice <= maxPrice
                       orderby track.Name
                       select new { Track = track.Name, Artist = artist.Name, Genre = genre.Name, Price = track.UnitPrice })
                      .Take(30).ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "param_5",
    name: "Optional Parameter Query",
    description: "Build query dynamically with optional filters",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with base JOIN query
2. Conditionally add WHERE clauses if parameter has value
3. Each conditional Where composes onto IQueryable
4. Sort and project
5. Return 30 results

**Key Concepts:**
- Optional filters via conditional composition
- Composable IQueryable chain
- Null-safe parameter handling
- Common in flexible search UIs
""")]
internal sealed class Param5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        string? artistFilter = "Led";
        decimal? minDuration = 180000;

        var query = context.GetTable<Track>()
            .Join(context.GetTable<Album>(), t => t.AlbumId, a => a.id, (t, a) => new { Track = t, Album = a })
            .Join(context.GetTable<Artist>(), ta => ta.Album.ArtistId, ar => ar.id, (ta, ar) => new { ta.Track, ta.Album, Artist = ar });

        if (!string.IsNullOrEmpty(artistFilter))
            query = query.Where(x => x.Artist.Name.Contains(artistFilter));
        if (minDuration.HasValue)
            query = query.Where(x => x.Track.Milliseconds >= minDuration.Value);

        var results = query.OrderBy(x => x.Track.Name).Take(30)
            .Select(x => new { Track = x.Track.Name, Artist = x.Artist.Name, DurationMinutes = x.Track.Milliseconds / 1000.0 / 60.0 })
            .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "param_6",
    name: "LIKE Pattern Search",
    description: "Safe wildcard search with parameterized Contains()",
    category: QueryCategory.ParameterizedQueries,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define search pattern parameter
2. Use Contains() for wildcard search
3. Pattern safely parameterized
4. SQLiteXM translates to LIKE '%pattern%'
5. Return matching tracks

**Key Concepts:**
- Contains() -> SQL LIKE with wildcards
- Safe wildcard searching
- Pattern is treated as data, not SQL
- No SQL injection risk
- Common for text search features
""")]
internal sealed class Param6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        string pattern = "Track";
        var results = context.GetTable<Track>().Where(t => t.Name.Contains(pattern)).OrderBy(t => t.Name).Take(30)
            .Select(t => new { TrackName = t.Name, t.UnitPrice, DurationSeconds = t.Milliseconds / 1000 })
            .ToList();
        return Task.FromResult<object>(results);
    }
}
