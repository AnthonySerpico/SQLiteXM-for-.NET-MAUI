using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Aggregations;

[QueryExample(
    id: "agg_1",
    name: "Count Tracks by Genre",
    description: "GROUP BY with COUNT aggregate",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Track with Genre (LEFT JOIN)
2. GROUP BY genre (id and name)
3. Count tracks in each group
4. Sum total duration for each genre
5. Sort by track count (most popular first)

**Key Concepts:**
- GROUP BY collapses rows into groups for aggregation
- COUNT() counts rows in each group
- SUM() totals a numeric field across grouped rows
- Multiple aggregates can be calculated together
""")]
internal sealed class Agg1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from track in context.GetTable<Track>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_2",
    name: "Album Count by Artist",
    description: "Count how many albums each artist has",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Album with Artist
2. GROUP BY artist (id and name)
3. Count albums in each group
4. Sort by album count descending
5. Take top 20 most prolific artists

**Key Concepts:**
- Simple GROUP BY + COUNT pattern for tallying records
- Grouping by composite key (id + name) ensures uniqueness
- OrderByDescending + Take = 'Top N' query
""")]
internal sealed class Agg2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from album in context.GetTable<Album>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_3",
    name: "Average Track Duration by Genre",
    description: "Calculate average track length per genre",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Track with Genre
2. GROUP BY genre
3. Calculate Average() duration in each group
4. Convert milliseconds to minutes
5. Also count tracks per genre

**Key Concepts:**
- AVERAGE() aggregate computes mean value
- Calculated fields work within aggregates
- Combining multiple aggregates (Average + Count)
""")]
internal sealed class Agg3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from track in context.GetTable<Track>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_4",
    name: "Total Revenue by Customer",
    description: "Calculate total spending per customer",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Invoice with Customer
2. GROUP BY customer (id, name, country)
3. SUM invoice totals for each customer
4. COUNT invoices per customer
5. Sort by total spent (highest first)

**Key Concepts:**
- SUM() aggregates monetary values
- Business intelligence pattern: customer lifetime value
- Grouping by multiple fields (composite key)
""")]
internal sealed class Agg4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from invoice in context.GetTable<Invoice>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_5",
    name: "Sales by Genre",
    description: "Calculate total revenue for each music genre",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with InvoiceLine (transaction detail)
2. JOIN to Track, then to Genre
3. GROUP BY genre
4. Calculate revenue: SUM(price x quantity)
5. Also SUM units sold

**Key Concepts:**
- Calculated aggregates: SUM(price x quantity)
- Multi-table JOIN for business intelligence
- Revenue analysis by category
""")]
internal sealed class Agg5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from invoiceLine in context.GetTable<InvoiceLine>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_6",
    name: "Average Invoice Total",
    description: "Calculate average invoice amount by country",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Invoice with Customer
2. GROUP BY country
3. Calculate Average invoice total per country
4. Count invoices per country
5. SUM total revenue per country

**Key Concepts:**
- Geographical aggregation for market analysis
- Multiple aggregates: AVG, COUNT, SUM together
- Useful for regional sales comparisons
""")]
internal sealed class Agg6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from invoice in context.GetTable<Invoice>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_7",
    name: "MIN/MAX Track Prices",
    description: "Find cheapest and most expensive tracks",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Load all tracks into memory (ToList)
2. Calculate MIN price using Min()
3. Calculate MAX price using Max()
4. Calculate AVG price using Average()
5. Count total tracks

**Key Concepts:**
- MIN() and MAX() find extreme values
- Aggregate functions without GROUP BY return single result
- In-memory aggregation after ToList()
""")]
internal sealed class Agg7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var tracks = context.GetTable<Track>().ToList();
        var summary = new
        {
            MinPrice = tracks.Min(t => t.UnitPrice),
            MaxPrice = tracks.Max(t => t.UnitPrice),
            AvgPrice = tracks.Average(t => t.UnitPrice),
            TotalTracks = tracks.Count
        };
        return Task.FromResult<object>(new[] { summary });
    }
}

[QueryExample(
    id: "agg_8",
    name: "Customer Purchase Statistics",
    description: "Detailed customer purchasing patterns (optimized)",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. First query: aggregate invoice data by customer
2. Calculate SUM, COUNT, AVG per customer
3. Materialize stats with ToList()
4. Second query: load customers
5. JOIN stats with customers in memory

**Key Concepts:**
- Two-phase aggregation for complex reports
- In-memory JOIN after separate queries
- Performance optimization: aggregate first, then join
""")]
internal sealed class Agg8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
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

        var results = (from stat in customerStats
                       join customer in customers on stat.CustomerId equals customer.id
                       select new
                       {
                           Customer = customer.FirstName + " " + customer.LastName,
                           Country = customer.Country,
                           stat.TotalSpent,
                           stat.OrderCount,
                           stat.AvgOrderValue
                       })
                      .OrderByDescending(x => x.TotalSpent)
                      .Take(30)
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_9",
    name: "Tracks per Album Statistics",
    description: "Analyze album sizes (track count distribution)",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN Track -> Album -> Artist
2. GROUP BY album (id, title, artist name)
3. COUNT tracks per album
4. SUM total duration, AVG track duration
5. Sort by track count (largest albums first)

**Key Concepts:**
- Composite grouping key with multiple fields
- Multiple aggregates: COUNT, SUM, AVG together
- Useful for album catalog analysis
""")]
internal sealed class Agg9Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from track in context.GetTable<Track>()
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "agg_10",
    name: "Revenue by Artist",
    description: "Calculate total sales for each artist",
    category: QueryCategory.Aggregations,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with InvoiceLine (actual sales data)
2. JOIN to Track -> Album -> Artist chain
3. GROUP BY artist
4. SUM revenue: UnitPrice x Quantity per line
5. SUM total units

**Key Concepts:**
- Deep join chain: InvoiceLine -> Track -> Album -> Artist
- Calculated aggregate: UnitPrice * Quantity per row summed
- Top N artists by revenue
""")]
internal sealed class Agg10Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from invoiceLine in context.GetTable<InvoiceLine>()
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
        return Task.FromResult<object>(results);
    }
}
