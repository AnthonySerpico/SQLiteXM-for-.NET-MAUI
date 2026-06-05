using QueryGalleryDemo.Models;

namespace QueryGalleryDemo.Services;

/// <summary>
/// Provides all query examples organized by category
/// </summary>
public static class QueryExampleProvider
{
    public static List<QueryExample> GetAllExamples()
    {
        var examples = new List<QueryExample>();

        // Add examples from all categories
        examples.AddRange(GetBasicQueryExamples());
        examples.AddRange(GetRelationshipQueryExamples());
        examples.AddRange(GetAggregationQueryExamples());
        examples.AddRange(GetAdvancedLinqExamples());
        examples.AddRange(GetRawSqlExamples());
        examples.AddRange(GetPerformanceExamples());
        examples.AddRange(GetManyToManyExamples());
        examples.AddRange(GetTransactionExamples());
        examples.AddRange(GetParameterizedQueryExamples());
        examples.AddRange(GetDataModificationExamples());

        return examples;
    }

    public static List<QueryExample> GetExamplesByCategory(QueryCategory category)
    {
        return category switch
        {
            QueryCategory.Basic => GetBasicQueryExamples(),
            QueryCategory.Relationships => GetRelationshipQueryExamples(),
            QueryCategory.Aggregations => GetAggregationQueryExamples(),
            QueryCategory.AdvancedLinq => GetAdvancedLinqExamples(),
            QueryCategory.RawSql => GetRawSqlExamples(),
            QueryCategory.Performance => GetPerformanceExamples(),
            QueryCategory.ManyToMany => GetManyToManyExamples(),
            QueryCategory.Transactions => GetTransactionExamples(),
            QueryCategory.ParameterizedQueries => GetParameterizedQueryExamples(),
            QueryCategory.DataModification => GetDataModificationExamples(),
            _ => new List<QueryExample>()
        };
    }

    private static List<QueryExample> GetBasicQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "basic_1",
                Name = "Get All Artists",
                Description = "Simple SELECT query to retrieve all artists",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var artists = context.GetTable<Artist>()
    .OrderBy(a => a.Name)
    .ToList();
return artists;"
            },
            new QueryExample
            {
                Id = "basic_2",
                Name = "Get All Genres",
                Description = "Retrieve all music genres ordered by name",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var genres = context.GetTable<Genre>()
    .OrderBy(g => g.Name)
    .ToList();
return genres;"
            },
            new QueryExample
            {
                Id = "basic_3",
                Name = "Filter Tracks by Genre",
                Description = "Get all Rock tracks using WHERE clause",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var rockGenre = context.GetTable<Genre>()
    .FirstOrDefault(g => g.Name == ""Rock"");

if (rockGenre != null)
{
    var rockTracks = context.GetTable<Track>()
        .Where(t => t.GenreId == rockGenre.id)
        .OrderBy(t => t.Name)
        .Take(50)
        .ToList();
    return rockTracks;
}
return new List<Track>();"
            },
            new QueryExample
            {
                Id = "basic_4",
                Name = "Find Artist by Name",
                Description = "Search for a specific artist using LIKE",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var artists = context.GetTable<Artist>()
    .Where(a => a.Name.Contains(""Zeppelin""))
    .ToList();
return artists;"
            },
            new QueryExample
            {
                Id = "basic_5",
                Name = "Get Tracks by Price Range",
                Description = "Filter tracks between $0.99 and $1.49",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>()
    .Where(t => t.UnitPrice >= 0.99m && t.UnitPrice <= 1.49m)
    .OrderBy(t => t.UnitPrice)
    .ThenBy(t => t.Name)
    .Take(100)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "basic_6",
                Name = "Top 10 Most Expensive Tracks",
                Description = "Get the highest priced tracks using OrderByDescending",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var expensiveTracks = context.GetTable<Track>()
    .OrderByDescending(t => t.UnitPrice)
    .ThenBy(t => t.Name)
    .Take(10)
    .ToList();
return expensiveTracks;"
            },
            new QueryExample
            {
                Id = "basic_7",
                Name = "Tracks by Duration Range",
                Description = "Find tracks between 3-5 minutes long",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var minMs = 3 * 60 * 1000; // 3 minutes
var maxMs = 5 * 60 * 1000; // 5 minutes
var tracks = context.GetTable<Track>()
    .Where(t => t.Milliseconds >= minMs && t.Milliseconds <= maxMs)
    .OrderBy(t => t.Milliseconds)
    .Take(100)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "basic_8",
                Name = "Case-Insensitive Search",
                Description = "Search for artists regardless of case",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
string searchTerm = ""led""; // Will match ""Led Zeppelin""
var artists = context.GetTable<Artist>()
    .Where(a => a.Name.ToLower().Contains(searchTerm.ToLower()))
    .OrderBy(a => a.Name)
    .ToList();
return artists;"
            },
            new QueryExample
            {
                Id = "basic_9",
                Name = "Tracks with Composer",
                Description = "Filter tracks that have a composer (NOT NULL)",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracksWithComposer = context.GetTable<Track>()
    .Where(t => t.Composer != null && t.Composer != """")
    .OrderBy(t => t.Composer)
    .ThenBy(t => t.Name)
    .Take(100)
    .ToList();
return tracksWithComposer;"
            },
            new QueryExample
            {
                Id = "basic_10",
                Name = "Distinct Media Types",
                Description = "Get unique media types using Distinct",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var mediaTypes = context.GetTable<MediaType>()
    .OrderBy(m => m.Name)
    .ToList();
return mediaTypes;"
            }
        };
    }

    private static List<QueryExample> GetRelationshipQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "rel_1",
                Name = "Tracks with Album Info",
                Description = "JOIN tracks with their album information",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               orderby track.Name
               select new { track.Name, AlbumTitle = album.Title, track.Milliseconds })
               .Take(50)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_2",
                Name = "Albums with Artist Names",
                Description = "JOIN albums with their artist information",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from album in context.GetTable<Album>()
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               orderby artist.Name, album.Title
               select new { album.Title, ArtistName = artist.Name, album.id })
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_3",
                Name = "Complete Track Information",
                Description = "JOIN tracks with album, artist, and genre",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
               from genre in genreGroup.DefaultIfEmpty()
               orderby artist.Name, album.Title, track.TrackNumber
               select new 
               { 
                   TrackName = track.Name,
                   AlbumTitle = album.Title,
                   ArtistName = artist.Name,
                   GenreName = genre != null ? genre.Name : ""Unknown"",
                   track.Milliseconds
               })
               .Take(100)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_4",
                Name = "Customers with Support Rep",
                Description = "JOIN customers with their assigned employee",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from customer in context.GetTable<Customer>()
               join employee in context.GetTable<Employee>() on customer.SupportRepId equals employee.id into empGroup
               from employee in empGroup.DefaultIfEmpty()
               orderby customer.LastName, customer.FirstName
               select new
               {
                   CustomerName = customer.FirstName + "" "" + customer.LastName,
                   customer.Email,
                   SupportRep = employee != null ? employee.FirstName + "" "" + employee.LastName : ""None"",
                   SupportRepEmail = employee != null ? employee.Email : """"
               })
               .Take(50)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_5",
                Name = "Employee Hierarchy",
                Description = "Self-join to show employee reporting structure",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from emp in context.GetTable<Employee>()
               join manager in context.GetTable<Employee>() on emp.ReportsTo equals manager.id into mgrGroup
               from manager in mgrGroup.DefaultIfEmpty()
               orderby emp.LastName, emp.FirstName
               select new
               {
                   EmployeeName = emp.FirstName + "" "" + emp.LastName,
                   emp.Title,
                   ManagerName = manager != null ? manager.FirstName + "" "" + manager.LastName : ""No Manager"",
                   ManagerTitle = manager != null ? manager.Title : """"
               })
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_6",
                Name = "Invoice with Customer Details",
                Description = "JOIN invoices with customer information",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from invoice in context.GetTable<Invoice>()
               join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
               orderby invoice.InvoiceDate descending
               select new
               {
                   invoice.id,
                   invoice.InvoiceDate,
                   CustomerName = customer.FirstName + "" "" + customer.LastName,
                   customer.Country,
                   invoice.Total
               })
               .Take(100)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_7",
                Name = "Track with All Related Entities",
                Description = "JOIN track with genre, media type, album, and artist",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
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
                   GenreName = genre != null ? genre.Name : ""Unknown"",
                   MediaTypeName = mediaType != null ? mediaType.Name : ""Unknown"",
                   track.UnitPrice,
                   DurationMinutes = track.Milliseconds / 1000.0 / 60.0
               })
               .Take(50)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_8",
                Name = "LEFT JOIN vs INNER JOIN",
                Description = "Compare LEFT JOIN (includes nulls) vs INNER JOIN",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// LEFT JOIN - includes tracks even without composer
var leftJoinResults = (from track in context.GetTable<Track>()
                       select new 
                       { 
                           track.Name, 
                           Composer = track.Composer ?? ""No Composer""
                       })
                       .Take(50)
                       .ToList();
return leftJoinResults;"
            }
        };
    }

    private static List<QueryExample> GetAggregationQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "agg_1",
                Name = "Count Tracks by Genre",
                Description = "GROUP BY with COUNT aggregate",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
               from genre in genreGroup.DefaultIfEmpty()
               group track by new { GenreId = genre != null ? genre.id : 0, GenreName = genre != null ? genre.Name : ""Unknown"" } into g
               select new 
               { 
                   Genre = g.Key.GenreName,
                   TrackCount = g.Count(),
                   TotalDurationMinutes = g.Sum(t => t.Milliseconds) / 1000 / 60
               })
               .OrderByDescending(x => x.TrackCount)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "agg_2",
                Name = "Album Count by Artist",
                Description = "Count how many albums each artist has",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_3",
                Name = "Average Track Duration by Genre",
                Description = "Calculate average track length per genre",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_4",
                Name = "Total Revenue by Customer",
                Description = "Calculate total spending per customer",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from invoice in context.GetTable<Invoice>()
               join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
               group invoice by new { customer.id, CustomerName = customer.FirstName + "" "" + customer.LastName, customer.Country } into g
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_5",
                Name = "Sales by Genre",
                Description = "Calculate total revenue for each music genre",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_6",
                Name = "Average Invoice Total",
                Description = "Calculate average invoice amount overall and by country",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_7",
                Name = "MIN/MAX Track Prices",
                Description = "Find cheapest and most expensive tracks",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>().ToList();
var summary = new
{
    MinPrice = tracks.Min(t => t.UnitPrice),
    MaxPrice = tracks.Max(t => t.UnitPrice),
    AvgPrice = tracks.Average(t => t.UnitPrice),
    TotalTracks = tracks.Count
};
return new[] { summary };"
            },
            new QueryExample
            {
                Id = "agg_8",
                Name = "Customer Purchase Statistics",
                Description = "Detailed customer purchasing patterns (optimized)",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// Optimized: Pre-aggregate invoice data first
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
                  Customer = customer.FirstName + "" "" + customer.LastName,
                  Country = customer.Country,
                  stat.TotalSpent,
                  stat.OrderCount,
                  stat.AvgOrderValue
              })
              .OrderByDescending(x => x.TotalSpent)
              .Take(30)
              .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "agg_9",
                Name = "Tracks per Album Statistics",
                Description = "Analyze album sizes (track count distribution)",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "agg_10",
                Name = "Revenue by Artist",
                Description = "Calculate total sales for each artist",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            }
        };
    }

    private static List<QueryExample> GetAdvancedLinqExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "adv_1",
                Name = "Paging with Skip/Take",
                Description = "Implement pagination for large result sets",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
int pageSize = 20;
int pageNumber = 1; // Change to test different pages
var tracks = context.GetTable<Track>()
    .OrderBy(t => t.Name)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "adv_2",
                Name = "Multiple ORDER BY",
                Description = "Sort by multiple columns with different directions",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               orderby artist.Name ascending, album.Title ascending, track.TrackNumber ascending
               select new { artist.Name, album.Title, track.TrackNumber, TrackName = track.Name })
               .Take(100)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "adv_3",
                Name = "Complex WHERE with Multiple Conditions",
                Description = "Combine multiple filter conditions with AND/OR",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// Show AND and OR combinations
var tracks = context.GetTable<Track>()
    .Where(t => (t.UnitPrice >= 1.0m && t.Milliseconds >= 180000) || 
                (t.UnitPrice < 1.0m && t.Milliseconds < 180000))
    .OrderBy(t => t.UnitPrice)
    .Take(50)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "adv_4",
                Name = "Subquery - IN Operator",
                Description = "Use subquery results to filter another query",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// Get first 50 artists
var artistIds = context.GetTable<Artist>()
    .OrderBy(a => a.Name)
    .Take(50)
    .Select(a => a.id)
    .ToList();

// Use those IDs to filter tracks
var tracks = (from track in context.GetTable<Track>()
              join album in context.GetTable<Album>() on track.AlbumId equals album.id
              where artistIds.Contains(album.ArtistId)
              orderby track.Name
              select track)
              .Take(100)
              .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "adv_5",
                Name = "Subquery with Count",
                Description = "Count related records for each customer",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// Show customers with their invoice counts
var customerInvoiceCounts = (from customer in context.GetTable<Customer>()
                             let invoiceCount = (from invoice in context.GetTable<Invoice>()
                                                where invoice.CustomerId == customer.id
                                                select invoice).Count()
                             orderby invoiceCount
                             select new
                             {
                                 CustomerName = customer.FirstName + "" "" + customer.LastName,
                                 customer.Country,
                                 InvoiceCount = invoiceCount
                             })
                             .Take(50)
                             .ToList();
return customerInvoiceCounts;"
            },
            new QueryExample
            {
                Id = "adv_6",
                Name = "HAVING Clause (Filter Groups)",
                Description = "Group by artist and show album counts",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            },
            new QueryExample
            {
                Id = "adv_7",
                Name = "Conditional Aggregates",
                Description = "Count tracks with different price ranges",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return new[] { priceAnalysis };"
            },
            new QueryExample
            {
                Id = "adv_8",
                Name = "Top N per Group",
                Description = "Get top 3 longest tracks per genre (SQLite-compatible)",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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

// Perform Top-3 per group in memory
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
return results;"
            },
            new QueryExample
            {
                Id = "adv_9",
                Name = "Date Range Queries",
                Description = "Query invoices with date filtering",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
// Show most recent invoices
var recentInvoices = (from invoice in context.GetTable<Invoice>()
                      join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
                      orderby invoice.InvoiceDate descending
                      select new
                      {
                          invoice.InvoiceDate,
                          CustomerName = customer.FirstName + "" "" + customer.LastName,
                          invoice.Total,
                          DaysAgo = (DateTime.Now - invoice.InvoiceDate).Days
                      })
                      .Take(100)
                      .ToList();
return recentInvoices;"
            },
            new QueryExample
            {
                Id = "adv_10",
                Name = "String Manipulation",
                Description = "Use string functions - UPPER, LOWER, TRIM, SUBSTRING",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return artists;"
            },
            new QueryExample
            {
                Id = "adv_11",
                Name = "UNION - Combine Results",
                Description = "Combine artists and album titles into one list",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var artistNames = context.GetTable<Artist>()
    .Select(a => new { Name = a.Name, Type = ""Artist"" })
    .Take(10);

var albumTitles = context.GetTable<Album>()
    .Select(a => new { Name = a.Title, Type = ""Album"" })
    .Take(10);

var combined = artistNames.Union(albumTitles)
    .OrderBy(x => x.Type)
    .ThenBy(x => x.Name)
    .ToList();
return combined;"
            }
        };
    }

    private static List<QueryExample> GetRawSqlExamples()
    {
        // Load SQL statements from JSON file
        var sqlStatements = LoadSqlStatementsFromJson();

        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "raw_1",
                Name = "Get All Artists (Raw SQL)",
                Description = "Execute raw SQL from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<Artist>(""GetAllArtistsRaw"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAllArtistsRaw")
            },
            new QueryExample
            {
                Id = "raw_2",
                Name = "Tracks with Album/Artist (Raw SQL)",
                Description = "Complex JOIN query from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTracksWithArtistAlbum"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithArtistAlbum")
            },
            new QueryExample
            {
                Id = "raw_3",
                Name = "Top Selling Tracks (Raw SQL)",
                Description = "Aggregation query with sales data from JSON",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTopSellingTracks"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopSellingTracks")
            },
            new QueryExample
            {
                Id = "raw_4",
                Name = "Customer Purchase Statistics",
                Description = "LEFT JOIN with aggregations for customer analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetCustomerPurchaseStats"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomerPurchaseStats")
            },
            new QueryExample
            {
                Id = "raw_5",
                Name = "Genre Popularity Analysis",
                Description = "GROUP BY with calculated fields",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetGenrePopularity"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetGenrePopularity")
            },
            new QueryExample
            {
                Id = "raw_6",
                Name = "Playlist Details with Duration",
                Description = "Multiple LEFT JOINs with SUM aggregation",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetPlaylistDetails"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistDetails")
            },
            new QueryExample
            {
                Id = "raw_7",
                Name = "Artist Revenue Report",
                Description = "Complex multi-table JOIN with COALESCE",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetArtistRevenue"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetArtistRevenue")
            },
            new QueryExample
            {
                Id = "raw_8",
                Name = "Expensive Tracks by Genre (Subquery)",
                Description = "WHERE clause with subquery for average comparison",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetExpensiveTracksByGenre"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetExpensiveTracksByGenre")
            },
            new QueryExample
            {
                Id = "raw_9",
                Name = "Country Statistics (Nested Query)",
                Description = "Subquery in FROM clause with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetCustomersByCountryWithStats"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomersByCountryWithStats")
            },
            new QueryExample
            {
                Id = "raw_10",
                Name = "Monthly Revenue Trend",
                Description = "Date functions with GROUP BY for time series analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetMonthlyRevenueTrend"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetMonthlyRevenueTrend")
            },
            new QueryExample
            {
                Id = "raw_11",
                Name = "Top Customers with Full Details",
                Description = "String concatenation, HAVING clause, multiple aggregates",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTopCustomersWithDetails"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopCustomersWithDetails")
            },
            new QueryExample
            {
                Id = "raw_12",
                Name = "Tracks with Price Tier (CASE)",
                Description = "CASE expression for conditional categorization",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTracksWithPriceTier"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithPriceTier")
            },
            new QueryExample
            {
                Id = "raw_13",
                Name = "Album Completion Analysis",
                Description = "Complex aggregation with HAVING filter",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetAlbumCompletion"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAlbumCompletion")
            },
            new QueryExample
            {
                Id = "raw_14",
                Name = "Employee Performance Report",
                Description = "Self-join with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetEmployeePerformance"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetEmployeePerformance")
            },
            new QueryExample
            {
                Id = "raw_15",
                Name = "Playlist Popularity Metrics",
                Description = "Multiple DISTINCT aggregations for variety analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetPlaylistPopularity"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistPopularity")
            }
        };
    }

    /// <summary>
    /// Loads SQL statements from the SqlStatements.json file
    /// </summary>
    private static Dictionary<string, string> LoadSqlStatementsFromJson()
    {
        var statements = new Dictionary<string, string>();

        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("SqlStatements.json").Result;
            if (stream != null)
            {
                using var reader = new System.IO.StreamReader(stream);
                var json = reader.ReadToEnd();
                var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("select", out var selectArray))
                {
                    foreach (var item in selectArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("Statement Name", out var nameElement) &&
                            item.TryGetProperty("Statement", out var statementElement))
                        {
                            statements[nameElement.GetString() ?? ""] = statementElement.GetString() ?? "";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading SQL statements: {ex.Message}");
        }

        return statements;
    }

    private static List<QueryExample> GetPerformanceExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "perf_1",
                Name = "Query 1000+ Tracks",
                Description = "Performance test with large result set",
                Category = QueryCategory.Performance,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>()
    .OrderBy(t => t.Name)
    .Take(1000)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "perf_2",
                Name = "Complex Multi-Table Join",
                Description = "JOIN 5 tables with filtering",
                Category = QueryCategory.Performance,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from invoiceLine in context.GetTable<InvoiceLine>()
               join invoice in context.GetTable<Invoice>() on invoiceLine.InvoiceId equals invoice.id
               join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
               join track in context.GetTable<Track>() on invoiceLine.TrackId equals track.id
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               where customer.Country == ""USA""
               select new 
               { 
                   CustomerName = customer.FirstName + "" "" + customer.LastName,
                   TrackName = track.Name,
                   AlbumTitle = album.Title,
                   invoiceLine.Quantity,
                   invoiceLine.UnitPrice
               })
                               .Take(500)
                               .ToList();
               return results;"
                           },
                           new QueryExample
                           {
                               Id = "perf_3",
                               Name = "Pagination with Skip/Take",
                               Description = "Efficiently paginate through large result sets",
                               Category = QueryCategory.Performance,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               int pageNumber = 2;
               int pageSize = 20;
               var page = context.GetTable<Track>()
                   .OrderBy(t => t.id)
                   .Skip((pageNumber - 1) * pageSize)
                   .Take(pageSize)
                   .ToList();
               return page;"
                           },
                           new QueryExample
                           {
                               Id = "perf_4",
                               Name = "Select Only Required Columns",
                               Description = "Reduce data transfer by projecting only needed fields",
                               Category = QueryCategory.Performance,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               // Faster: select only what you need
               var lightweightTracks = context.GetTable<Track>()
                   .Select(t => new { t.id, t.Name, t.UnitPrice })
                   .Take(100)
                   .ToList();
               return lightweightTracks;"
                           },
                           new QueryExample
                           {
                               Id = "perf_5",
                               Name = "Early Filtering",
                               Description = "Filter before joining for better performance",
                               Category = QueryCategory.Performance,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               // Good: filter albums first, then join
               var expensiveAlbums = context.GetTable<Album>()
                   .Where(a => a.Title.StartsWith(""A""))
                   .Take(50);

               var results = (from album in expensiveAlbums
                              join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                              select new { album.Title, artist.Name })
                              .ToList();
               return results;"
                           },
                                       new QueryExample
                                       {
                                           Id = "perf_6",
                                           Name = "Count Performance",
                                           Description = "Compare Count() vs Any() for existence checks",
                                           Category = QueryCategory.Performance,
                                           Type = QueryType.Linq,
                                           Code = @"using var context = new SxmLinqDbContext(""Chinook"");
                           // Faster: use Any() instead of Count() > 0 for existence
                           var hasExpensiveTracks = context.GetTable<Track>()
                               .Any(t => t.UnitPrice > 1.50m);

                           var expensiveCount = context.GetTable<Track>()
                               .Count(t => t.UnitPrice > 1.50m);

                           return new[] { new { HasExpensiveTracks = hasExpensiveTracks, Count = expensiveCount } };"
                                       },
                           new QueryExample
                           {
                               Id = "perf_7",
                               Name = "Avoid N+1 Queries",
                               Description = "Use joins instead of multiple queries",
                               Category = QueryCategory.Performance,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               // Good: single query with join
               var tracksWithAlbums = (from track in context.GetTable<Track>()
                                       join album in context.GetTable<Album>() on track.AlbumId equals album.id
                                       select new 
                                       { 
                                           TrackName = track.Name, 
                                           AlbumTitle = album.Title 
                                       })
                                       .Take(100)
                                       .ToList();
               return tracksWithAlbums;"
                           },
                           new QueryExample
                           {
                               Id = "perf_8",
                               Name = "Efficient Distinct",
                               Description = "Get unique values efficiently",
                               Category = QueryCategory.Performance,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               var uniqueCountries = context.GetTable<Customer>()
                   .Select(c => c.Country)
                   .Distinct()
                   .OrderBy(c => c)
                   .ToList();
               return uniqueCountries;"
                           }
                       };
                   }

    private static List<QueryExample> GetManyToManyExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "m2m_1",
                Name = "Tracks in a Playlist",
                Description = "Query many-to-many relationship through junction table",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var playlist = context.GetTable<Playlist>()
    .FirstOrDefault(p => p.Name.Contains(""Music""));

if (playlist != null)
{
    var tracks = (from pt in context.GetTable<PlaylistTrack>()
                  join track in context.GetTable<Track>() on pt.TrackId equals track.id
                  where pt.PlaylistId == playlist.id
                  orderby track.Name
                  select track)
                  .Take(50)
                  .ToList();
    return tracks;
}
return new List<Track>();"
            },
            new QueryExample
            {
                Id = "m2m_2",
                Name = "Playlists Containing Track",
                Description = "Reverse query: find all playlists with a specific track",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var track = context.GetTable<Track>()
    .FirstOrDefault(t => t.Name.Contains(""Track""));

if (track != null)
{
    var playlists = (from pt in context.GetTable<PlaylistTrack>()
                     join playlist in context.GetTable<Playlist>() on pt.PlaylistId equals playlist.id
                     where pt.TrackId == track.id
                     select playlist)
                     .ToList();
    return playlists;
}
return new List<Playlist>();"
            },
            new QueryExample
            {
                Id = "m2m_3",
                Name = "Playlist Statistics",
                Description = "Aggregate data across many-to-many relationship",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from playlist in context.GetTable<Playlist>()
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
               return results;"
                           },
                                                       new QueryExample
                                                       {
                                                           Id = "m2m_4",
                                                           Name = "Tracks Shared Between Playlists",
                                                           Description = "Find tracks that appear in multiple playlists (SQLite-compatible)",
                                                           Category = QueryCategory.ManyToMany,
                                                           Type = QueryType.Linq,
                                                           Code = @"using var context = new SxmLinqDbContext(""Chinook"");
                           // Materialize first - SQLite can't translate Distinct().Count() in projection
                           var trackPlaylistGroups = (from pt in context.GetTable<PlaylistTrack>()
                                                     join track in context.GetTable<Track>() on pt.TrackId equals track.id
                                                     select new
                                                     {
                                                         TrackId = track.id,
                                                         TrackName = track.Name,
                                                         PlaylistId = pt.PlaylistId
                                                     }).ToList();

                           // Perform distinct count in memory
                           var sharedTracks = trackPlaylistGroups
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
                           return sharedTracks;"
                                                       },
                                                       new QueryExample
                                                       {
                                                           Id = "m2m_5",
                                                           Name = "Popular Tracks in Playlists",
                                                           Description = "Count how many playlists each track appears in (optimized)",
                                                           Category = QueryCategory.ManyToMany,
                                                           Type = QueryType.Linq,
                                                           Code = @"using var context = new SxmLinqDbContext(""Chinook"");
                           // Materialize joins first - avoid hanging on Distinct().Count()
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

                           // Perform distinct count in memory
                           var popularTracks = trackData
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
                           return popularTracks;"
                                                       },
                                                                               new QueryExample
                                                                               {
                                                                                   Id = "m2m_6",
                                                                                   Name = "Playlists with Few Tracks",
                                                                                   Description = "Find playlists with fewer than 250 tracks (optimized)",
                                                                                   Category = QueryCategory.ManyToMany,
                                                                                   Type = QueryType.Linq,
                                                                                   Code = @"using var context = new SxmLinqDbContext(""Chinook"");
                                       // Materialize playlist track counts first
                                       var playlistCounts = (from pt in context.GetTable<PlaylistTrack>()
                                                            group pt by pt.PlaylistId into g
                                                            select new
                                                            {
                                                                PlaylistId = g.Key,
                                                                TrackCount = g.Count()
                                                            }).ToList();

                                       var playlists = context.GetTable<Playlist>().ToList();

                                       // Join in memory and filter
                                       var smallPlaylists = (from pc in playlistCounts
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
                                       return smallPlaylists;"
                                                                               },
                           new QueryExample
                           {
                               Id = "m2m_7",
                               Name = "Add Track to Playlist",
                               Description = "Insert into junction table (many-to-many relationship)",
                               Category = QueryCategory.ManyToMany,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               // Note: This is a read-only demo, but this shows the pattern
               var playlist = context.GetTable<Playlist>().FirstOrDefault();
               var track = context.GetTable<Track>().FirstOrDefault();

               if (playlist != null && track != null)
               {
                   // In a real app: var newEntry = new PlaylistTrack { PlaylistId = playlist.id, TrackId = track.id };
                   // await newEntry.SaveAsync();
                   return new[] { new { Message = ""Pattern: Create PlaylistTrack with both IDs and SaveAsync()"" } };
               }
               return new[] { new { Message = ""No data to demo with"" } };"
                           },
                           new QueryExample
                           {
                               Id = "m2m_8",
                               Name = "Playlist Overlap Analysis",
                               Description = "Find which playlists share the most tracks",
                               Category = QueryCategory.ManyToMany,
                               Type = QueryType.Linq,
                               Code = @"using var context = new SxmLinqDbContext(""Chinook"");
               var playlistPairs = (from pt1 in context.GetTable<PlaylistTrack>()
                                    join pt2 in context.GetTable<PlaylistTrack>() on pt1.TrackId equals pt2.TrackId
                                    where pt1.PlaylistId < pt2.PlaylistId
                                    join p1 in context.GetTable<Playlist>() on pt1.PlaylistId equals p1.id
                                    join p2 in context.GetTable<Playlist>() on pt2.PlaylistId equals p2.id
                                    group pt1 by new { p1.Name, p2.Name } into g
                                    select new
                                    {
                                        Playlist1 = g.Key.Name,
                                        Playlist2 = g.Key.Name1,
                                        SharedTracks = g.Count()
                                    })
                                    .OrderByDescending(x => x.SharedTracks)
                                                                         .Take(10)
                                                                         .ToList();
                                                    return playlistPairs;"
                                                                }
                                                            };
                                                        }

                                        private static List<QueryExample> GetTransactionExamples()
                                        {
                                            return new List<QueryExample>
                                            {
                                                new QueryExample
                                                {
                                                    Id = "trans_1",
                                                    Name = "Basic Transaction - Insert Invoice with Lines",
                                                    Description = "Insert invoice + invoice lines atomically",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = SxmSqlTransaction.Create(""Chinook"");
                                    try 
                                    {
                                        // Create invoice
                                        var invoice = new Invoice 
                                        { 
                                            CustomerId = 1, 
                                            InvoiceDate = DateTime.Now, 
                                            BillingAddress = ""123 Demo St"",
                                            BillingCity = ""Portland"",
                                            BillingCountry = ""USA"",
                                            Total = 5.97m 
                                        };
                                        await invoice.SaveAsync(transaction);

                                        // Add invoice lines - all succeed or all fail
                                        var line1 = new InvoiceLine 
                                        { 
                                            InvoiceId = invoice.id, 
                                            TrackId = 1, 
                                            UnitPrice = 1.99m, 
                                            Quantity = 1 
                                        };
                                        await line1.SaveAsync(transaction);

                                        var line2 = new InvoiceLine 
                                        { 
                                            InvoiceId = invoice.id, 
                                            TrackId = 2, 
                                            UnitPrice = 1.99m, 
                                            Quantity = 2 
                                        };
                                        await line2.SaveAsync(transaction);

                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync();

                                        return new[] { new 
                                        { 
                                            Success = true, 
                                            InvoiceId = invoice.id, 
                                            TotalAmount = invoice.Total,
                                            LineCount = 2
                                        } };
                                    }
                                    catch (Exception ex)
                                    {
                                        // Transaction automatically rolls back on error
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_2",
                                                    Name = "Transaction Rollback on Error",
                                                    Description = "Demonstrate automatic rollback when error occurs",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = SxmSqlTransaction.Create(""Chinook"");
                                    try 
                                    {
                                        // Insert a valid artist
                                        var artist = new Artist { Name = ""Transaction Test Artist"" };
                                        await artist.SaveAsync(transaction);

                                        // Insert an album
                                        var album = new Album 
                                        { 
                                            Title = ""Test Album"", 
                                            ArtistId = artist.id 
                                        };
                                        await album.SaveAsync(transaction);

                                        // Simulate an error - this will cause rollback
                                        throw new Exception(""Simulated error - all changes will be rolled back"");

                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync(); // Never reached

                                        return new[] { new { Success = true } };
                                    }
                                    catch (Exception ex)
                                    {
                                        // Both artist and album inserts are automatically rolled back
                                        return new[] { new 
                                        { 
                                            Success = false, 
                                            Error = ex.Message,
                                            Note = ""All changes were rolled back"" 
                                        } };
                                    }"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_3",
                                                    Name = "Batch Insert with Transaction",
                                                    Description = "Efficiently insert multiple tracks in one transaction",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = SxmSqlTransaction.Create(""Chinook"");
                                    try 
                                    {
                                        var insertedCount = 0;
                                        var startTime = DateTime.Now;

                                        // Insert 50 tracks in a single transaction (fast!)
                                        for (int i = 1; i <= 50; i++)
                                        {
                                            var track = new Track
                                            {
                                                Name = $""Batch Track {i}"",
                                                AlbumId = 1, // Use existing album
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

                                        return new[] { new 
                                        { 
                                            Success = true, 
                                            TracksInserted = insertedCount,
                                            ElapsedMs = elapsed,
                                            Note = ""All inserts in single transaction""
                                        } };
                                    }
                                    catch (Exception ex)
                                    {
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_4",
                                                    Name = "Update Multiple Tables in Transaction",
                                                    Description = "Update artist and all their albums atomically",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = SxmSqlTransaction.Create(""Chinook"");
                                    using var context = new SxmLinqDbContext(""Chinook"");
                                    try 
                                    {
                                        // Find an artist and their albums
                                        var artist = context.GetTable<Artist>().First();
                                        var albums = context.GetTable<Album>()
                                            .Where(a => a.ArtistId == artist.id)
                                            .Take(3)
                                            .ToList();

                                        // Update artist name
                                        var originalName = artist.Name;
                                        artist.Name = artist.Name + "" (Updated)"";
                                        await artist.SaveAsync(transaction);

                                        // Update all album titles for this artist
                                        foreach (var album in albums)
                                        {
                                            album.Title = album.Title + "" [Remastered]"";
                                            await album.SaveAsync(transaction);
                                        }

                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync();

                                        return new[] { new 
                                        { 
                                            Success = true,
                                            ArtistOriginal = originalName,
                                            ArtistUpdated = artist.Name,
                                            AlbumsUpdated = albums.Count
                                        } };
                                    }
                                    catch (Exception ex)
                                    {
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_5",
                                                    Name = "Complex Multi-Table Transaction",
                                                    Description = "Create playlist, add tracks, update statistics",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = SxmSqlTransaction.Create(""Chinook"");
                                    using var context = new SxmLinqDbContext(""Chinook"");
                                    try 
                                    {
                                        // 1. Create new playlist
                                        var playlist = new Playlist 
                                        { 
                                            Name = $""Transaction Demo Playlist {DateTime.Now:HHmmss}""
                                        };
                                        await playlist.SaveAsync(transaction);

                                        // 2. Get top 10 tracks
                                        var topTracks = context.GetTable<Track>()
                                            .OrderBy(t => t.Name)
                                            .Take(10)
                                            .ToList();

                                        // 3. Add tracks to playlist
                                        var trackCount = 0;
                                        foreach (var track in topTracks)
                                        {
                                            var pt = new PlaylistTrack
                                            {
                                                PlaylistId = playlist.id,
                                                TrackId = track.id
                                            };
                                            await pt.SaveAsync(transaction);
                                            trackCount++;
                                        }

                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync();

                                        return new[] { new 
                                        { 
                                            Success = true,
                                            PlaylistId = playlist.id,
                                            PlaylistName = playlist.Name,
                                            TracksAdded = trackCount,
                                            Note = ""All operations committed together""
                                        } };
                                    }
                                    catch (Exception ex)
                                    {
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_6",
                                                    Name = "Transaction vs No Transaction Performance",
                                                    Description = "Compare performance: transaction vs individual saves",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"var results = new List<object>();

                                    // Method 1: Without transaction (slower)
                                    var start1 = DateTime.Now;
                                    for (int i = 1; i <= 20; i++)
                                    {
                                        var track = new Track
                                        {
                                            Name = $""No-Transaction Track {i}"",
                                            AlbumId = 1,
                                            MediaTypeId = 1,
                                            GenreId = 1,
                                            Milliseconds = 180000,
                                            UnitPrice = 0.99m
                                        };
                                        await track.SaveAsync(); // Individual commit per save
                                    }
                                    var noTransTime = (DateTime.Now - start1).TotalMilliseconds;

                                    // Method 2: With transaction (faster)
                                    var start2 = DateTime.Now;
                                    await using (var transaction = SxmSqlTransaction.Create(""Chinook""))
                                    {
                                        for (int i = 1; i <= 20; i++)
                                        {
                                            var track = new Track
                                            {
                                                Name = $""Transaction Track {i}"",
                                                AlbumId = 1,
                                                MediaTypeId = 1,
                                                GenreId = 1,
                                                Milliseconds = 180000,
                                                UnitPrice = 0.99m
                                            };
                                            await track.SaveAsync(transaction);
                                        }
                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync();
                                    }
                                    var transTime = (DateTime.Now - start2).TotalMilliseconds;

                                    results.Add(new 
                                    { 
                                        Method = ""Without Transaction"",
                                        Inserts = 20,
                                        TimeMs = noTransTime
                                    });
                                    results.Add(new 
                                    { 
                                        Method = ""With Transaction"",
                                        Inserts = 20,
                                        TimeMs = transTime,
                                        SpeedupFactor = Math.Round(noTransTime / transTime, 2)
                                    });

                                    return results;"
                                                }
                                            };
                                        }

                                        private static List<QueryExample> GetParameterizedQueryExamples()
                                        {
                                            return new List<QueryExample>
                                            {
                                                new QueryExample
                                                {
                                                    Id = "param_1",
                                                    Name = "Search by Name Parameter",
                                                    Description = "Find tracks matching a search term (safe from SQL injection)",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Parameter: user input (simulated)
                                    string searchTerm = ""Love"";

                                    // Safe parameterized query - searchTerm is treated as data, not SQL
                                    var results = context.GetTable<Track>()
                                        .Where(t => t.Name.Contains(searchTerm))
                                        .OrderBy(t => t.Name)
                                        .Take(20)
                                        .ToList();

                                    return results;"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "param_2",
                                                    Name = "Price Range Filter",
                                                    Description = "Find tracks within a price range using parameters",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Parameters: user-defined price range
                                    decimal minPrice = 0.99m;
                                    decimal maxPrice = 1.49m;

                                    var results = context.GetTable<Track>()
                                        .Where(t => t.UnitPrice >= minPrice && t.UnitPrice <= maxPrice)
                                        .OrderBy(t => t.UnitPrice)
                                        .ThenBy(t => t.Name)
                                        .Take(50)
                                        .Select(t => new
                                        {
                                            t.Name,
                                            t.UnitPrice,
                                            DurationMinutes = t.Milliseconds / 1000.0 / 60.0
                                        })
                                        .ToList();

                                    return results;"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "param_3",
                                                    Name = "Date Range Query",
                                                    Description = "Find invoices within a date range (demonstrates safe parameterization)",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

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
                                                                     Customer = customer.FirstName + "" "" + customer.LastName,
                                                                     Total = invoice.Total
                                                                 }).ToList();

                                    // Phase 2: Filter by date in memory
                                    var results = invoicesWithCustomers
                                        .Where(i => i.Date >= startDate && i.Date <= endDate)
                                        .OrderByDescending(i => i.Date)
                                        .Take(30)
                                        .ToList();

                                    return results;"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "param_4",
                                                    Name = "Multiple Search Parameters",
                                                    Description = "Search with artist, genre, and price filters",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Multiple parameters
                                    string artistSearchTerm = ""Led"";
                                    int genreId = 1; // Rock
                                    decimal maxPrice = 1.50m;

                                    var results = (from track in context.GetTable<Track>()
                                                   join album in context.GetTable<Album>() on track.AlbumId equals album.id
                                                   join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                                                   join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                                                   where artist.Name.Contains(artistSearchTerm)
                                                         && track.GenreId == genreId
                                                         && track.UnitPrice <= maxPrice
                                                   orderby track.Name
                                                   select new
                                                   {
                                                       Track = track.Name,
                                                       Artist = artist.Name,
                                                       Genre = genre.Name,
                                                       Price = track.UnitPrice
                                                   })
                                                   .Take(30)
                                                   .ToList();

                                    return results;"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "param_5",
                                                    Name = "Optional Parameter Handling",
                                                    Description = "Handle nullable/optional search parameters",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Optional parameters - null means 'don't filter'
                                    string? artistFilter = ""Led""; // Search for Led Zeppelin (exists in seeded data)
                                    decimal? minDuration = 180000; // milliseconds, Try: null to see all durations

                                    var query = context.GetTable<Track>()
                                        .Join(context.GetTable<Album>(), 
                                              t => t.AlbumId, 
                                              a => a.id, 
                                              (t, a) => new { Track = t, Album = a })
                                        .Join(context.GetTable<Artist>(),
                                              ta => ta.Album.ArtistId,
                                              ar => ar.id,
                                              (ta, ar) => new { ta.Track, ta.Album, Artist = ar });

                                    // Apply filters only if parameters are provided
                                    if (!string.IsNullOrEmpty(artistFilter))
                                        query = query.Where(x => x.Artist.Name.Contains(artistFilter));

                                    if (minDuration.HasValue)
                                        query = query.Where(x => x.Track.Milliseconds >= minDuration.Value);

                                    var results = query
                                        .OrderBy(x => x.Track.Name)
                                        .Take(30)
                                        .Select(x => new
                                        {
                                            Track = x.Track.Name,
                                            Artist = x.Artist.Name,
                                            DurationMinutes = x.Track.Milliseconds / 1000.0 / 60.0
                                        })
                                        .ToList();

                                    return results;"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "param_6",
                                                    Name = "LIKE Pattern Search",
                                                    Description = "Wildcard search using parameters",
                                                    Category = QueryCategory.ParameterizedQueries,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Pattern parameter - user defines the search pattern
                                    string pattern = ""Track""; // Searches for tracks containing 'Track'

                                    // Different search patterns:
                                    // ""Track"" - contains Track
                                    // ""The%"" - starts with The (if provider supports it)
                                    // ""%Live%"" - contains Live

                                    var results = context.GetTable<Track>()
                                        .Where(t => t.Name.Contains(pattern))
                                        .OrderBy(t => t.Name)
                                        .Take(30)
                                        .Select(t => new
                                        {
                                            TrackName = t.Name,
                                            t.UnitPrice,
                                            DurationSeconds = t.Milliseconds / 1000
                                        })
                                        .ToList();

                                    return results;"
                                                }
                                            };
                                        }

                                        private static List<QueryExample> GetDataModificationExamples()
                                        {
                                            return new List<QueryExample>
                                            {
                                                new QueryExample
                                                {
                                                    Id = "mod_1",
                                                    Name = "Insert New Track",
                                                    Description = "Add a single new track to the database",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"var track = new Track
                                    {
                                        Name = $""New Demo Track {DateTime.Now:HHmmss}"",
                                        AlbumId = 1, // Use existing album
                                        MediaTypeId = 1,
                                        GenreId = 1,
                                        Composer = ""Demo Composer"",
                                        Milliseconds = 240000, // 4 minutes
                                        Bytes = 4000000,
                                        UnitPrice = 1.29m,
                                        TrackNumber = 1
                                    };

                                    await track.SaveAsync();

                                    return new[] { new 
                                    { 
                                        Success = true,
                                        TrackId = track.id,
                                        TrackName = track.Name,
                                        Message = ""Track inserted successfully""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_2",
                                                    Name = "Insert and Get Generated ID",
                                                    Description = "Insert record and retrieve auto-generated ID",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"// Create new artist
                                    var artist = new Artist 
                                    { 
                                        Name = $""New Artist {DateTime.Now:HHmmss}""
                                    };
                                    await artist.SaveAsync();

                                    // ID is automatically populated after save
                                    var artistId = artist.id;

                                    // Now create album using the new artist's ID
                                    var album = new Album
                                    {
                                        Title = ""Debut Album"",
                                        ArtistId = artistId
                                    };
                                    await album.SaveAsync();

                                    return new[] { new 
                                    { 
                                        ArtistId = artistId,
                                        ArtistName = artist.Name,
                                        AlbumId = album.id,
                                        AlbumTitle = album.Title,
                                        Message = ""Artist and Album created with auto-generated IDs""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_3",
                                                    Name = "Update Track Price",
                                                    Description = "Modify a single field on existing record",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Find a track to update
                                    var track = context.GetTable<Track>().First();

                                    var originalPrice = track.UnitPrice;
                                    track.UnitPrice = 1.99m; // Update price

                                    await track.SaveAsync();

                                    return new[] { new 
                                    { 
                                        TrackId = track.id,
                                        TrackName = track.Name,
                                        OriginalPrice = originalPrice,
                                        NewPrice = track.UnitPrice,
                                        Message = ""Price updated successfully""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_4",
                                                    Name = "Conditional Update",
                                                    Description = "Update records matching specific criteria",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Find all tracks under $1.00
                                    var cheapTracks = context.GetTable<Track>()
                                        .Where(t => t.UnitPrice < 1.00m)
                                        .Take(10)
                                        .ToList();

                                    var updateCount = 0;
                                    foreach (var track in cheapTracks)
                                    {
                                        track.UnitPrice = 1.29m; // Increase price
                                        await track.SaveAsync();
                                        updateCount++;
                                    }

                                    return new[] { new 
                                    { 
                                        TracksUpdated = updateCount,
                                        NewPrice = 1.29m,
                                        Message = $""Updated {updateCount} tracks to new price""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_5",
                                                    Name = "Bulk Price Increase by Genre",
                                                    Description = "Update all tracks in a specific genre",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Find genre
                                    var rockGenre = context.GetTable<Genre>()
                                        .FirstOrDefault(g => g.Name.Contains(""Rock""));

                                    if (rockGenre != null)
                                    {
                                        // Get all rock tracks
                                        var rockTracks = context.GetTable<Track>()
                                            .Where(t => t.GenreId == rockGenre.id)
                                            .Take(20)
                                            .ToList();

                                        var updateCount = 0;
                                        foreach (var track in rockTracks)
                                        {
                                            // Increase price by 10%
                                            track.UnitPrice = track.UnitPrice * 1.10m;
                                            await track.SaveAsync();
                                            updateCount++;
                                        }

                                        return new[] { new 
                                        { 
                                            Genre = rockGenre.Name,
                                            TracksUpdated = updateCount,
                                            PriceIncrease = ""10%"",
                                            Message = $""Updated {updateCount} rock tracks""
                                        } };
                                    }

                                    return new[] { new { Message = ""Rock genre not found"" } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_6",
                                                    Name = "Delete Single Record",
                                                    Description = "Remove a playlist from the database",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Find a playlist to delete
                                    var playlist = context.GetTable<Playlist>()
                                        .FirstOrDefault(p => p.Name.Contains(""Demo""));

                                    if (playlist != null)
                                    {
                                        var playlistName = playlist.Name;
                                        await playlist.DeleteAsync();

                                        return new[] { new 
                                        { 
                                            Success = true,
                                            DeletedPlaylist = playlistName,
                                            Message = ""Playlist deleted successfully""
                                        } };
                                    }

                                    return new[] { new 
                                    { 
                                        Success = false,
                                        Message = ""No demo playlist found to delete""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_7",
                                                    Name = "Conditional Delete",
                                                    Description = "Delete records matching specific criteria",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

                                    // Find old playlists (simulated - find playlists with 'Old' in name)
                                    var oldPlaylists = context.GetTable<Playlist>()
                                        .Where(p => p.Name.Contains(""Old"") || p.Name.Contains(""Demo""))
                                        .Take(5)
                                        .ToList();

                                    var deleteCount = 0;
                                    foreach (var playlist in oldPlaylists)
                                    {
                                        await playlist.DeleteAsync();
                                        deleteCount++;
                                    }

                                    return new[] { new 
                                    { 
                                        PlaylistsDeleted = deleteCount,
                                        Message = $""Deleted {deleteCount} old playlists""
                                    } };"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_8",
                                                    Name = "Delete with Related Records",
                                                    Description = "Delete playlist after removing its tracks first",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");
                                    await using var transaction = SxmSqlTransaction.Create(""Chinook"");

                                    try
                                    {
                                        // Find playlist
                                        var playlist = context.GetTable<Playlist>()
                                            .FirstOrDefault(p => p.Name.Contains(""Test""));

                                        if (playlist != null)
                                        {
                                            // First, delete all playlist-track relationships
                                            var playlistTracks = context.GetTable<PlaylistTrack>()
                                                .Where(pt => pt.PlaylistId == playlist.id)
                                                .ToList();

                                            var trackCount = playlistTracks.Count;
                                            foreach (var pt in playlistTracks)
                                            {
                                                await pt.DeleteAsync(transaction);
                                            }

                                            // Now delete the playlist itself
                                            await playlist.DeleteAsync(transaction);

                                            // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                            // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                            // on Dispose (If No Errors)
                                            await transaction.CommitTransactionAsync();

                                            return new[] { new 
                                            { 
                                                Success = true,
                                                PlaylistName = playlist.Name,
                                                TracksRemoved = trackCount,
                                                Message = ""Playlist and tracks deleted in transaction""
                                            } };
                                        }

                                        return new[] { new { Success = false, Message = ""Playlist not found"" } };
                                    }
                                    catch (Exception ex)
                                    {
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }"
                                                }
                                            };
                                        }
                                    }

