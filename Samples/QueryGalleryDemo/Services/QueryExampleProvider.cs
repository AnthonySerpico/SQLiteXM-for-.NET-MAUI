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

return artists;",
                Explanation = @"**How It Works:**
1. Create a database context for the 'Chinook' database
2. Get the Artist table using GetTable<Artist>()
3. Sort all artists alphabetically by Name using OrderBy()
4. Execute the query and convert results to a List
5. Return the list of Artist objects

**Key Concepts:**
• This is the simplest type of LINQ query - retrieving all records from a single table
• OrderBy() translates to SQL ORDER BY clause
• ToList() executes the query and materializes results in memory
• The using statement ensures the database context is properly disposed"
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

return genres;",
                Explanation = @"**How It Works:**
1. Create a context connected to the Chinook database
2. Access the Genre table via GetTable<Genre>()
3. Order genres alphabetically by name
4. Materialize results into a list
5. Return the complete list of genres

**Key Concepts:**
• Same pattern as basic_1 but with a different table
• GetTable<T>() provides strongly-typed access to database tables
• Method chaining (.OrderBy().ToList()) is a core LINQ pattern
• Query execution is deferred until ToList() is called"
            },
            new QueryExample
            {
                Id = "basic_3",
                Name = "Filter Tracks by Genre",
                Description = "Get all Rock tracks using WHERE clause with JOIN",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");

// Single-query approach using JOIN
var rockTracks = (from track in context.GetTable<Track>()
                  join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
                  where genre.Name == ""Rock""
                  orderby track.Name
                  select track)
                  .Take(50)
                  .ToList();

return rockTracks;",
                Explanation = @"**How It Works:**
1. Start with the Track table
2. JOIN to Genre table using GenreId foreign key
3. Filter for tracks where genre name equals 'Rock'
4. Sort results by track name
5. Limit to first 50 results using Take()
6. Execute and return the list

**Key Concepts:**
• Uses LINQ query syntax (from...join...where) instead of method syntax
• JOIN connects related tables using foreign key relationships
• WHERE clause (where) filters data before returning
• Take() limits results - important for large datasets
• This is more efficient than loading all tracks and filtering in memory"
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

return artists;",
                Explanation = @"**How It Works:**
1. Get the Artist table
2. Filter using Where() to find artists whose name contains 'Zeppelin'
3. Contains() performs a partial string match
4. Execute query with ToList()
5. Return matching artists

**Key Concepts:**
• Contains() translates to SQL LIKE '%Zeppelin%' (wildcard search)
• WHERE clause filters rows at the database level, not in memory
• Partial string matching is useful for search functionality
• This is case-sensitive in SQLite by default
• More efficient than loading all records and filtering with C# code"
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

return tracks;",
                Explanation = @"**How It Works:**
1. Access the Track table
2. Filter for tracks with price between $0.99 and $1.49 using compound condition
3. Sort primarily by UnitPrice (low to high)
4. Then sort by Name (secondary sort for tracks with same price)
5. Limit to 100 results
6. Execute and return

**Key Concepts:**
• Compound WHERE conditions use && (AND) operator
• ThenBy() creates a secondary sort (ORDER BY price, name)
• The 'm' suffix (0.99m) specifies decimal literals for currency
• Range queries are common for filtering by price, date, or other numeric values
• Multi-level sorting ensures predictable result ordering"
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

return expensiveTracks;",
                Explanation = @"**How It Works:**
1. Get all tracks from the Track table
2. Sort by UnitPrice in descending order (highest first)
3. Apply secondary sort by Name (alphabetically)
4. Take only the first 10 results
5. Execute and return the top 10 list

**Key Concepts:**
• OrderByDescending() sorts in reverse (high to low)
• 'Top N' pattern: sort descending + Take(N)
• ThenBy() always sorts ascending (even after OrderByDescending)
• This avoids loading all tracks into memory - database does the sorting
• Take() should always come after sorting for meaningful results"
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

return tracks;",
                Explanation = @"**How It Works:**
1. Calculate min/max duration in milliseconds (3 and 5 minutes)
2. Access Track table
3. Filter tracks within the duration range
4. Sort by duration (shortest to longest)
5. Limit to 100 tracks
6. Execute and return

**Key Concepts:**
• Duration is stored in milliseconds in the database
• Variables (minMs, maxMs) are evaluated before the query runs
• Range filtering is a common pattern for numeric/date fields
• Tracks are stored with Milliseconds field for precise duration
• Converting minutes to milliseconds: minutes × 60 × 1000"
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

return artists;",
                Explanation = @"**How It Works:**
1. Define search term in lowercase ('led')
2. Access Artist table
3. Convert both database Name and search term to lowercase
4. Use Contains() to find matches
5. Sort results alphabetically
6. Execute and return

**Key Concepts:**
• ToLower() ensures case-insensitive matching
• 'led' will match 'Led Zeppelin', 'LED', 'LeD', etc.
• Both sides of comparison are lowercased for consistency
• SQLite string comparison is case-sensitive by default (unlike SQL Server)
• This pattern is essential for user-friendly search functionality"
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

return tracksWithComposer;",
                Explanation = @"**How It Works:**
1. Access Track table
2. Filter for tracks where Composer is not null AND not empty string
3. Sort primarily by Composer name
4. Then sort by Track name
5. Limit to 100 results
6. Execute and return

**Key Concepts:**
• NULL checking in databases - some fields may be NULL (no value)
• Must check both null and empty string for completeness
• != null translates to IS NOT NULL in SQL
• Composer is optional in Chinook database
• This pattern filters out 'missing data' records"
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

return mediaTypes;",
                Explanation = @"**How It Works:**
1. Access MediaType table
2. Sort media types alphabetically by Name
3. Retrieve all records as a list
4. Return the complete list

**Key Concepts:**
• MediaType table already contains unique values (it's a lookup table)
• Distinct() is not needed here because table structure ensures uniqueness
• Lookup/reference tables typically store unique categorical values
• OrderBy() ensures consistent, predictable ordering
• This is an example of querying reference data"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Track table
2. JOIN to Album table using AlbumId foreign key
3. Sort results by track name
4. Project selected fields into anonymous type
5. Limit to 50 records
6. Execute and return

**Key Concepts:**
• INNER JOIN - only returns tracks that have a matching album
• Foreign key relationship: Track.AlbumId references Album.id
• Anonymous types (new {...}) let you shape query results
• JOIN operations happen at the database level for efficiency
• This demonstrates one-to-many relationship (one album, many tracks)"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Album table
2. JOIN to Artist table using ArtistId foreign key
3. Sort by artist name first, then album title
4. Project fields into result object
5. Retrieve all matching records
6. Return the list

**Key Concepts:**
• Multi-level sorting: orderby artist, then album (ORDER BY artist, title)
• Foreign key Album.ArtistId → Artist.id
• No Take() means all records are returned (use carefully with large datasets)
• Result shows hierarchical data (artist → albums)
• This is the reverse direction of rel_1 (parent → children)"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Track table
2. JOIN to Album (required)
3. JOIN Album to Artist (required)
4. LEFT JOIN to Genre (optional, using DefaultIfEmpty)
5. Sort by artist, album, track number
6. Project combined data from all tables
7. Take 100 and execute

**Key Concepts:**
• Chain multiple JOINs to traverse relationships: Track → Album → Artist
• LEFT JOIN (into...DefaultIfEmpty) includes tracks even if genre is null
• Null-coalescing: genre != null ? genre.Name : 'Unknown'
• Three-level sort creates hierarchical ordering
• This shows how to navigate complex relational data structures"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Customer table
2. LEFT JOIN to Employee via SupportRepId
3. Sort by customer last name, first name
4. Build result with customer + support rep info
5. Handle null employees with conditional logic
6. Take 50 and return

**Key Concepts:**
• LEFT JOIN ensures all customers are shown (even without support rep)
• SupportRepId may be NULL for some customers
• String concatenation builds full names
• Conditional expressions handle missing relationships
• This is common for optional foreign keys"
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

return results;",
                Explanation = @"**How It Works:**
1. Access Employee table
2. SELF-JOIN: join Employee to itself via ReportsTo field
3. Use LEFT JOIN to handle top-level managers (no boss)
4. Sort by employee name
5. Project employee + manager info
6. Return organizational hierarchy

**Key Concepts:**
• Self-join: table joined to itself to model hierarchical data
• ReportsTo is a foreign key pointing to another Employee.id
• Common pattern for org charts, threaded comments, category trees
• Top-level employees have ReportsTo = NULL
• This shows parent-child relationships within same table"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Invoice table
2. JOIN to Customer table via CustomerId
3. Sort by invoice date (newest first)
4. Project invoice and customer fields together
5. Limit to 100 most recent
6. Execute and return

**Key Concepts:**
• INNER JOIN ensures invoices only returned if customer exists
• OrderByDescending sorts newest to oldest (descending date)
• Combines transactional data (invoice) with reference data (customer)
• Common pattern for order/customer, ticket/user relationships
• Useful for reporting and invoice history displays"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with Track
2. JOIN to Album (required)
3. JOIN to Artist via Album (required)
4. LEFT JOIN to Genre (optional)
5. LEFT JOIN to MediaType (optional)
6. Sort hierarchically (artist → album → track)
7. Project all related data
8. Take 50 and return

**Key Concepts:**
• Complex multi-table JOIN showing complete track metadata
• Mix of INNER and LEFT JOINs as needed
• Calculated field: Milliseconds converted to minutes
• Demonstrates navigating deep relationship chains
• This pattern is common for 'detail view' queries"
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

return leftJoinResults;",
                Explanation = @"**How It Works:**
1. Select from Track table
2. Project Name and Composer fields
3. Use null-coalescing operator (??) to handle null Composer
4. Take 50 records
5. Execute and return

**Key Concepts:**
• NULL handling: ?? operator provides default value when field is null
• This demonstrates data with optional fields
• Composer can be NULL in many tracks
• INNER JOIN would exclude these tracks; this approach includes all
• Important pattern for working with incomplete/optional data"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Track with Genre (LEFT JOIN)
2. GROUP BY genre (id and name)
3. Count tracks in each group
4. Sum total duration for each genre
5. Sort by track count (most popular first)
6. Return aggregated results

**Key Concepts:**
• GROUP BY collapses rows into groups for aggregation
• COUNT() counts rows in each group
• SUM() totals a numeric field across grouped rows
• Multiple aggregates can be calculated together
• This pattern is essential for reporting and analytics"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Album with Artist
2. GROUP BY artist (id and name)
3. Count albums in each group
4. Sort by album count descending
5. Take top 20 most prolific artists
6. Return results

**Key Concepts:**
• Simple GROUP BY + COUNT pattern for tallying records
• Grouping by composite key (id + name) ensures uniqueness
• OrderByDescending + Take = 'Top N' query
• Useful for leaderboards, rankings, popularity metrics
• This is one of the most common SQL patterns"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Track with Genre
2. GROUP BY genre
3. Calculate Average() duration in each group
4. Convert milliseconds to minutes
5. Also count tracks per genre
6. Sort by average duration
7. Return all results

**Key Concepts:**
• AVERAGE() aggregate computes mean value
• Calculated fields work within aggregates
• Combining multiple aggregates (Average + Count)
• Useful for statistical analysis and comparisons
• Shows which genres tend to have longer/shorter tracks"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Invoice with Customer
2. GROUP BY customer (id, name, country)
3. SUM invoice totals for each customer
4. COUNT invoices per customer
5. Sort by total spent (highest first)
6. Take top 20 customers
7. Return results

**Key Concepts:**
• SUM() aggregates monetary values
• Business intelligence pattern: customer lifetime value
• Grouping by multiple fields (composite key)
• Combining SUM + COUNT for richer insights
• Critical for sales reporting and customer analytics"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with InvoiceLine (transaction detail)
2. JOIN to Track, then to Genre
3. GROUP BY genre
4. Calculate revenue: SUM(price × quantity)
5. Also SUM units sold
6. Sort by revenue descending
7. Return genre sales report

**Key Concepts:**
• Calculated aggregates: SUM(price × quantity)
• Multi-table JOIN for business intelligence
• Revenue analysis by category
• Essential for sales and marketing insights
• Shows which genres are most profitable"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Invoice with Customer
2. GROUP BY country
3. Calculate Average invoice total per country
4. Count invoices per country
5. SUM total revenue per country
6. Sort by total revenue
7. Return country-level metrics

**Key Concepts:**
• Geographical aggregation for market analysis
• Multiple aggregates: AVG, COUNT, SUM together
• Useful for regional sales comparisons
• Identifies high-value vs high-volume markets
• Common in business dashboards"
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
return new[] { summary };",
                Explanation = @"**How It Works:**
1. Load all tracks into memory (ToList)
2. Calculate MIN price using Min()
3. Calculate MAX price using Max()
4. Calculate AVG price using Average()
5. Count total tracks
6. Return single summary object

**Key Concepts:**
• MIN() and MAX() find extreme values
• Aggregate functions without GROUP BY return single result
• In-memory aggregation after ToList()
• Useful for dataset summaries and ranges
• Shows price spread and distribution"
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

return results;",
                Explanation = @"**How It Works:**
1. First query: aggregate invoice data by customer
2. Calculate SUM, COUNT, AVG per customer
3. Materialize stats with ToList()
4. Second query: load customers
5. JOIN stats with customers in memory
6. Sort by total spent, take top 30
7. Return enriched customer statistics

**Key Concepts:**
• Two-phase aggregation for complex reports
• In-memory JOIN after separate queries
• Performance optimization: aggregate first, then join
• Combines multiple aggregates per customer
• Pattern for comprehensive customer analytics"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Track → Album → Artist
2. GROUP BY album (id, title, artist name)
3. COUNT tracks per album
4. SUM total duration, AVG track duration
5. Convert milliseconds to minutes
6. Sort by track count (largest albums first)
7. Take top 30 albums

**Key Concepts:**
• Composite grouping key with multiple fields
• Multiple aggregates: COUNT, SUM, AVG together
• Useful for album catalog analysis
• Shows album 'size' metrics
• Helps identify compilation albums vs regular releases"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with InvoiceLine (actual sales data)
2. JOIN to Track → Album → Artist chain
3. GROUP BY artist
4. SUM revenue: UnitPrice × Quantity per line
5. SUM total units sold
6. Sort by revenue (highest earners first)
7. Take top 20 artists

**Key Concepts:**
• Revenue calculation: price × quantity
• Aggregating transactional data (invoice lines)
• Following relationship chain to get artist from sales
• Critical for royalty/revenue reporting
• Shows bestselling artists by dollar amount"
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

return tracks;",
                Explanation = @"**How It Works:**
1. Define page size (20 records per page)
2. Specify page number (1-based)
3. OrderBy ensures consistent ordering
4. Skip calculates offset: (page-1) × size
5. Take limits to page size
6. Execute and return one page

**Key Concepts:**
• Pagination is essential for large datasets
• Skip/Take pattern is standard for paging
• MUST have OrderBy for predictable results
• Formula: skip (pageNumber-1) × pageSize records
• Common in web APIs and list views"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Track → Album → Artist
2. Sort by artist name (primary)
3. Then by album title (secondary)
4. Then by track number (tertiary)
5. All sorts are ascending
6. Take first 100 results
7. Return sorted list

**Key Concepts:**
• Multiple ORDER BY creates hierarchical sorting
• Order matters: first sort is primary, then secondary, etc.
• Essential for album/track listings in correct order
• TrackNumber ensures songs play in intended sequence
• This is how music players organize tracks"
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

return tracks;",
                Explanation = @"**How It Works:**
1. Access Track table
2. Apply complex WHERE with nested conditions
3. First group: expensive ($1+) AND long (3+ min)
4. OR second group: cheap (<$1) AND short (<3 min)
5. Parentheses control logic grouping
6. Sort by price
7. Take 50 matches

**Key Concepts:**
• AND (&&) requires both conditions true
• OR (||) requires at least one condition true
• Parentheses control evaluation order
• This finds 'consistent' tracks: expensive+long OR cheap+short
• Essential for complex business logic filtering"
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

return tracks;",
                Explanation = @"**How It Works:**
1. First query: get top 50 artist IDs
2. Materialize to list with ToList()
3. Second query: get tracks
4. JOIN to albums
5. Filter using Contains() - SQL IN operator
6. Only tracks from those 50 artists pass
7. Take 100 tracks

**Key Concepts:**
• Two-phase query pattern
• Contains() translates to SQL IN clause
• Subquery results used to filter main query
• Useful when filtering logic is complex
• Common pattern for dependent queries"
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

return customerInvoiceCounts;",
                Explanation = @"**How It Works:**
1. Start with Customer table
2. Use 'let' to define subquery variable
3. Subquery counts invoices per customer
4. Sort by invoice count
5. Project customer info + count
6. Take first 50 customers
7. Execute and return

**Key Concepts:**
• 'let' keyword defines intermediate values
• Subquery executed for each customer
• Correlated subquery: references outer query's customer.id
• Useful for showing counts alongside main data
• Common in dashboard/reporting queries"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Album with Artist
2. GROUP BY artist
3. Order by album count (most prolific artists first)
4. Select artist name and count
5. Take top 50 artists
6. Execute and return

**Key Concepts:**
• OrderBy after grouping filters/sorts aggregates
• Equivalent to SQL HAVING in many cases
• LINQ doesn't have explicit HAVING - use OrderBy/Where on groups
• Shows most prolific artists
• Common for 'top N' leaderboard queries"
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

return new[] { priceAnalysis };",
                Explanation = @"**How It Works:**
1. Load all tracks into memory
2. Count tracks in 'cheap' price range
3. Count tracks in 'mid' price range
4. Count tracks in 'expensive' range
5. Calculate average price
6. Find min and max prices
7. Return single summary object

**Key Concepts:**
• Conditional aggregates: Count() with predicate
• Multiple aggregates over same dataset
• In-memory LINQ after ToList()
• Useful for price distribution analysis
• Common in business intelligence/reporting"
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

return results;",
                Explanation = @"**How It Works:**
1. JOIN Track with Genre and fetch data
2. Materialize with ToList() (SQLite doesn't support windowing)
3. GROUP BY genre in memory
4. For each group, OrderByDescending and Take(3)
5. SelectMany flattens groups back to list
6. Sort final results by genre, then duration
7. Return top 3 per genre

**Key Concepts:**
• 'Top N per group' is a common SQL pattern
• SQLite lacks window functions; workaround with in-memory grouping
• GroupBy + SelectMany pattern for partitioned results
• Two-phase: fetch data, then process in memory
• Essential for leaderboards, rankings per category"
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

return recentInvoices;",
                Explanation = @"**How It Works:**
1. JOIN Invoice with Customer
2. Sort by InvoiceDate descending (newest first)
3. Select invoice date, customer name, total
4. Calculate 'DaysAgo' using DateTime arithmetic
5. Take 100 most recent invoices
6. Execute and return

**Key Concepts:**
• Date arithmetic: DateTime.Now - invoice date
• .Days property gives difference in days
• OrderByDescending for newest-first sorting
• Useful for 'recent activity' views
• Common in dashboards and activity feeds"
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

return artists;",
                Explanation = @"**How It Works:**
1. Access Artist table
2. Project with string transformations:
   - ToUpper() converts to uppercase
   - ToLower() converts to lowercase
   - Substring(0, 3) extracts first 3 characters
   - Length property gets string length
3. Sort by original name
4. Take 20 artists
5. Execute and return

**Key Concepts:**
• String functions translate to SQL equivalents
• ToUpper/ToLower useful for normalization
• Substring for text extraction
• Conditional (ternary) operator handles edge cases
• Common for data cleaning and formatting"
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

return combined;",
                Explanation = @"**How It Works:**
1. First query: select 10 artists with type label
2. Second query: select 10 albums with type label
3. Union() combines both result sets
4. Union removes duplicates (unlike Concat)
5. Sort by Type, then by Name
6. Execute and return combined list

**Key Concepts:**
• UNION combines multiple queries into one result
• Both queries must have same structure/shape
• Union() removes duplicates; Concat() keeps all
• Useful for heterogeneous searches
• Common in 'search everything' features"
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
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetAllArtistsRaw"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAllArtistsRaw"),
                Explanation = @"**How It Works:**
1. SQL query loaded from SqlStatements.json file
2. RunStatementAsync runs raw SQL
3. Results mapped to Artist entity type
4. Returns strongly-typed list of Artist objects

**Key Concepts:**
• Raw SQL allows full SQLite feature access
• SQL statements stored in JSON for easy maintenance
• Type mapping: SQL rows → C# entities
• Useful when LINQ limitations exist
• Best for complex queries LINQ can't express"
            },
            new QueryExample
            {
                Id = "raw_2",
                Name = "Tracks with Album/Artist (Raw SQL)",
                Description = "Complex JOIN query from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetTracksWithArtistAlbum"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithArtistAlbum"),
                Explanation = @"**How It Works:**
1. Load SQL with multiple INNER JOINs
2. Use dynamic type for flexible result shape
3. Execute joins across Track, Album, Artist
4. Return anonymous objects with mixed properties

**Key Concepts:**
• dynamic allows flexible result shapes
• Raw SQL handles complex joins easily
• No entity mapping required for ad-hoc queries
• Good for reporting/analytics queries
• Trade-off: lose compile-time type safety"
            },
            new QueryExample
            {
                Id = "raw_3",
                Name = "Top Selling Tracks (Raw SQL)",
                Description = "Aggregation query with sales data from JSON",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetTopSellingTracks"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopSellingTracks"),
                Explanation = @"**How It Works:**
1. SQL aggregates invoice line data
2. GROUP BY to summarize by track
3. COUNT/SUM calculate sales metrics
4. ORDER BY + LIMIT for top N
5. Return ranked results

**Key Concepts:**
• Aggregation functions: COUNT, SUM
• GROUP BY groups rows for summarization
• Raw SQL great for analytics
• LIMIT controls result size
• Common sales reporting pattern"
            },
            new QueryExample
            {
                Id = "raw_4",
                Name = "Customer Purchase Statistics",
                Description = "LEFT JOIN with aggregations for customer analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetCustomerPurchaseStats"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomerPurchaseStats"),
                Explanation = @"**How It Works:**
1. LEFT JOIN ensures all customers included
2. Aggregate purchase data per customer
3. Calculate total spent, order count
4. Handle NULL values for customers with no purchases
5. Return complete customer profile

**Key Concepts:**
• LEFT JOIN includes rows even without matches
• COALESCE/IFNULL handle NULLs gracefully
• Aggregates work with GROUP BY
• Common in customer analytics
• Raw SQL simplifies outer join logic"
            },
            new QueryExample
            {
                Id = "raw_5",
                Name = "Genre Popularity Analysis",
                Description = "GROUP BY with calculated fields",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetGenrePopularity"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetGenrePopularity"),
                Explanation = @"**How It Works:**
1. Join Track and Genre tables
2. GROUP BY genre to aggregate metrics
3. COUNT tracks per genre
4. Calculate average price
5. Order by popularity (track count)

**Key Concepts:**
• GROUP BY creates one row per genre
• COUNT(*) counts rows in each group
• AVG() calculates mean values
• Useful for popularity/trending analysis
• Raw SQL simplifies grouping logic"
            },
            new QueryExample
            {
                Id = "raw_6",
                Name = "Playlist Details with Duration",
                Description = "Multiple LEFT JOINs with SUM aggregation",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetPlaylistDetails"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistDetails"),
                Explanation = @"**How It Works:**
1. Start with Playlist table
2. LEFT JOIN through junction to tracks
3. SUM track durations per playlist
4. COUNT tracks in each playlist
5. Include playlists with zero tracks

**Key Concepts:**
• Multiple LEFT JOINs chain relationships
• SUM() aggregates numeric values
• GROUP BY playlist to summarize
• Handles many-to-many via junction table
• NULL-safe aggregation"
            },
            new QueryExample
            {
                Id = "raw_7",
                Name = "Artist Revenue Report",
                Description = "Complex multi-table JOIN with COALESCE",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetArtistRevenue"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetArtistRevenue"),
                Explanation = @"**How It Works:**
1. Join Artist → Album → Track → InvoiceLine
2. Sum revenue from all sales
3. COALESCE provides default for no sales
4. GROUP BY artist to aggregate
5. ORDER BY revenue descending

**Key Concepts:**
• Multi-table joins trace relationships
• COALESCE(value, 0) handles NULLs
• SUM() calculates total revenue
• Common financial reporting pattern
• Raw SQL simplifies deep joins"
            },
            new QueryExample
            {
                Id = "raw_8",
                Name = "Expensive Tracks by Genre (Subquery)",
                Description = "WHERE clause with subquery for average comparison",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetExpensiveTracksByGenre"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetExpensiveTracksByGenre"),
                Explanation = @"**How It Works:**
1. Subquery calculates AVG price per genre
2. Outer query compares track price to avg
3. Filter tracks above their genre's average
4. Return tracks that are 'expensive' for their genre

**Key Concepts:**
• Subquery in WHERE clause
• Correlated subquery uses outer table
• Compares individual vs. group aggregate
• Complex logic hard to express in LINQ
• Raw SQL enables advanced filtering"
            },
            new QueryExample
            {
                Id = "raw_9",
                Name = "Country Statistics (Nested Query)",
                Description = "Subquery in FROM clause with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetCustomersByCountryWithStats"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomersByCountryWithStats"),
                Explanation = @"**How It Works:**
1. Subquery in FROM becomes derived table
2. Inner query aggregates by country
3. Outer query can further process results
4. Multiple aggregates: COUNT, SUM, AVG
5. Return country-level statistics

**Key Concepts:**
• Derived table (subquery as table source)
• Two-stage aggregation possible
• Complex analytics patterns
• Common in business intelligence
• LINQ struggles with nested aggregates"
            },
            new QueryExample
            {
                Id = "raw_10",
                Name = "Monthly Revenue Trend",
                Description = "Date functions with GROUP BY for time series analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetMonthlyRevenueTrend"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetMonthlyRevenueTrend"),
                Explanation = @"**How It Works:**
1. Extract year/month from invoice date
2. GROUP BY year and month
3. SUM revenue within each period
4. ORDER BY time for trend analysis
5. Return time series data

**Key Concepts:**
• Date/time functions: strftime, YEAR, MONTH
• Time-based grouping
• Revenue trending over time
• Common in dashboards/reports
• SQLite date handling via functions"
            },
            new QueryExample
            {
                Id = "raw_11",
                Name = "Top Customers with Full Details",
                Description = "String concatenation, HAVING clause, multiple aggregates",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetTopCustomersWithDetails"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopCustomersWithDetails"),
                Explanation = @"**How It Works:**
1. Concatenate first + last name
2. Aggregate purchase metrics per customer
3. HAVING filters groups (not rows)
4. Return only high-value customers
5. ORDER BY total spent

**Key Concepts:**
• String concatenation: || operator
• HAVING filters after GROUP BY
• WHERE filters before, HAVING filters after
• Common in CRM/loyalty analysis
• Multiple aggregates per group"
            },
            new QueryExample
            {
                Id = "raw_12",
                Name = "Tracks with Price Tier (CASE)",
                Description = "CASE expression for conditional categorization",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetTracksWithPriceTier"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithPriceTier"),
                Explanation = @"**How It Works:**
1. CASE expression evaluates conditions
2. Assign tier based on price range
3. Returns 'Budget', 'Standard', 'Premium'
4. Computed column in SELECT
5. Useful for categorization logic

**Key Concepts:**
• CASE = SQL's if-then-else
• Conditional computed columns
• Categorizes data into buckets
• Hard to express in LINQ projections
• Common in pricing/tier analysis"
            },
            new QueryExample
            {
                Id = "raw_13",
                Name = "Album Completion Analysis",
                Description = "Complex aggregation with HAVING filter",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetAlbumCompletion"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAlbumCompletion"),
                Explanation = @"**How It Works:**
1. Count tracks per album
2. Calculate total duration
3. AVG price across album
4. HAVING filters for 'complete' albums (10+ tracks)
5. Return only substantial albums

**Key Concepts:**
• HAVING with COUNT threshold
• Multiple aggregates in one query
• Filter aggregated results
• Useful for quality/completeness checks
• Raw SQL simplifies complex HAVING"
            },
            new QueryExample
            {
                Id = "raw_14",
                Name = "Employee Performance Report",
                Description = "Self-join with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetEmployeePerformance"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetEmployeePerformance"),
                Explanation = @"**How It Works:**
1. Self-join Employee to Employee (manager)
2. Aggregate sales per employee
3. Include manager name via self-join
4. Calculate employee metrics
5. Return hierarchical sales report

**Key Concepts:**
• Self-join: table joins to itself
• Hierarchical data (employee/manager)
• LEFT JOIN handles employees without managers
• Common in org chart queries
• LINQ self-joins are complex"
            },
            new QueryExample
            {
                Id = "raw_15",
                Name = "Playlist Popularity Metrics",
                Description = "Multiple DISTINCT aggregations for variety analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmStatement.RunStatementAsync(""GetPlaylistPopularity"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistPopularity"),
                Explanation = @"**How It Works:**
1. Count total tracks in playlist
2. COUNT(DISTINCT) unique artists
3. COUNT(DISTINCT) unique genres
4. Measure playlist diversity
5. Return variety metrics

**Key Concepts:**
• COUNT(DISTINCT) eliminates duplicates
• Multiple DISTINCT counts in one query
• Measures data variety/diversity
• Common in content analysis
• LINQ DISTINCT in aggregates is tricky"
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

return tracks;",
                Explanation = @"**How It Works:**
1. Access Track table
2. Sort by name (consistent ordering)
3. Limit to 1000 rows
4. Execute query and materialize results

**Performance Tips:**
• Take() limits result set size
• OrderBy ensures predictable results
• For very large sets, consider pagination
• SQLiteXM translates efficiently to SQL LIMIT
• Baseline for measuring query performance"
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

return results;",
                Explanation = @"**How It Works:**
1. Start with InvoiceLine (transactional data)
2. JOIN 4 additional tables
3. Filter to USA customers only
4. Project combined data
5. Limit to 500 results

**Performance Tips:**
• WHERE clause filters early in the query
• SQLite handles joins efficiently with indexes
• Take() prevents unbounded result sets
• Consider indexed columns for joins
• 5-table join is a realistic complexity test"
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

return page;",
                Explanation = @"**How It Works:**
1. Define page size (20 items)
2. Calculate offset for page 2
3. OrderBy ensures consistent ordering
4. Skip first 20 records
5. Take next 20 records
6. Return one page of data

**Performance Tips:**
• Always use OrderBy before Skip/Take
• SQLite translates to LIMIT/OFFSET
• Ideal for infinite scroll or paged lists
• Reduces memory usage vs loading all data
• Standard pattern for web APIs"
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

return lightweightTracks;",
                Explanation = @"**How It Works:**
1. Access Track table
2. Project only 3 columns (id, Name, UnitPrice)
3. Omit unnecessary columns (Composer, Milliseconds, etc.)
4. Take 100 records
5. Materialize lightweight results

**Performance Tips:**
• SELECT only needed columns reduces I/O
• Smaller result sets = less memory
• Faster network transfer in distributed apps
• SQLiteXM generates optimal SELECT
• Critical for mobile/bandwidth-constrained scenarios"
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

return results;",
                Explanation = @"**How It Works:**
1. Filter albums first (titles starting with 'A')
2. Limit to 50 albums
3. Then join to Artist table
4. Project combined result
5. Execute and return

**Performance Tips:**
• Filter early reduces join size
• Smaller dataset = faster joins
• WHERE before JOIN is a key optimization
• SQLite optimizer benefits from this pattern
• Avoids processing unnecessary rows"
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

return new[] { new { HasExpensiveTracks = hasExpensiveTracks, Count = expensiveCount } };",
                Explanation = @"**How It Works:**
1. Any() checks if at least one track > $1.50
2. Returns true/false immediately when found
3. Count() actually counts all matching tracks
4. Returns total count
5. Both results returned together

**Performance Tips:**
• Any() is faster for existence checks
• Any() stops at first match
• Count() must scan all matching rows
• Use Any() when you only need yes/no
• Use Count() when you need the actual number"
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

return tracksWithAlbums;",
                Explanation = @"**How It Works:**
1. Single query with JOIN
2. Fetch tracks and albums together
3. Project combined result
4. Take 100 results
5. Execute once

**Performance Tips:**
• Avoid N+1 problem: 1 query for tracks + N queries for albums
• JOIN executes in single database round-trip
• Dramatically faster than loops with queries inside
• Essential for good ORM performance
• SQLiteXM makes joins easy with LINQ"
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

return uniqueCountries;",
                Explanation = @"**How It Works:**
1. Select Country column only
2. Apply Distinct() to remove duplicates
3. Sort alphabetically
4. Execute and return unique list

**Performance Tips:**
• Select single column before Distinct() is efficient
• SQLite translates to SELECT DISTINCT
• Reduces result set size significantly
• Perfect for dropdown lists, filters
• OrderBy provides user-friendly sorting"
            },
            new QueryExample
            {
                Id = "perf_9",
                Name = "Foreign Key Index Performance",
                Description = "Demonstrates why indexes on foreign keys matter",
                Category = QueryCategory.Performance,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");

var sw = System.Diagnostics.Stopwatch.StartNew();

// This query benefits from indexed foreign keys
var tracksForAlbums = (from track in context.GetTable<Track>()
                       join album in context.GetTable<Album>() on track.AlbumId equals album.id
                       where album.Title.StartsWith(""A"")
                       select new { track.Name, album.Title })
                       .Take(200)
                       .ToList();

sw.Stop();
var elapsedMs = sw.ElapsedMilliseconds;

return new[] { new { 
    ResultCount = tracksForAlbums.Count, 
    ElapsedMs = elapsedMs,
    Message = $""Query completed in {elapsedMs}ms using indexed foreign key""
} };",
                Explanation = @"**How It Works:**
1. Start stopwatch timer
2. Query tracks joined with albums
3. Filter albums by title prefix
4. Index on Track.AlbumId accelerates the JOIN
5. Return timing and result count

**Why Indexes Matter:**
• Without index on AlbumId: SQLite scans entire Track table for each album (slow)
• With index (IFK_Track_AlbumId): SQLite uses index to find matching tracks instantly
• Standard Chinook schema includes indexes on ALL foreign keys
• JOIN performance improves dramatically with proper indexing
• This query would be 10-100x slower without the index on large datasets

**Chinook Database Indexes:**
• IFK_Album_ArtistId - Album lookups by artist
• IFK_Track_AlbumId - Track lookups by album
• IFK_Track_GenreId - Track lookups by genre
• IFK_Track_MediaTypeId - Track lookups by media type
• IFK_InvoiceLine_InvoiceId - Line items by invoice
• IFK_InvoiceLine_TrackId - Line items by track
• IFK_PlaylistTrack_PlaylistId - Playlist tracks
• IFK_PlaylistTrack_TrackId - Track playlists
• IFK_Invoice_CustomerId - Invoices by customer
• IFK_Customer_SupportRepId - Customers by rep
• IFK_Employee_ReportsTo - Employee hierarchy

**Real-World Impact:**
• Indexed foreign keys are essential for OLTP systems
• Makes JOIN queries scale with data volume
• Critical for relationship navigation
• Referential integrity checks are also faster"
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
                Description = "Query many-to-many relationship through junction table with single query",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");

// Single-query approach: JOIN Playlist, PlaylistTrack, and Track
var tracks = (from playlist in context.GetTable<Playlist>()
              join pt in context.GetTable<PlaylistTrack>() on playlist.id equals pt.PlaylistId
              join track in context.GetTable<Track>() on pt.TrackId equals track.id
              where playlist.Name.Contains(""Music"")
              orderby track.Name
              select track)
              .Take(50)
              .ToList();

return tracks;",
                Explanation = @"**How It Works:**
1. Start with Playlist table
2. JOIN to PlaylistTrack (junction table)
3. JOIN to Track table
4. Filter playlists by name containing 'Music'
5. Sort tracks alphabetically
6. Take 50 tracks

**Key Concepts:**
• Many-to-many requires junction table (PlaylistTrack)
• Two joins navigate the relationship
• Junction table links Playlist ↔ Track
• Single query is efficient
• Standard pattern for M:N relationships"
            },
            new QueryExample
            {
                Id = "m2m_2",
                Name = "Playlists Containing Track",
                Description = "Reverse query: find all playlists with a specific track using single query",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");

// Single-query approach: JOIN Track, PlaylistTrack, and Playlist
var playlists = (from track in context.GetTable<Track>()
                 join pt in context.GetTable<PlaylistTrack>() on track.id equals pt.TrackId
                 join playlist in context.GetTable<Playlist>() on pt.PlaylistId equals playlist.id
                 where track.Name.Contains(""Track"")
                 select playlist)
                 .ToList();

return playlists;",
                Explanation = @"**How It Works:**
1. Start with Track table
2. JOIN to PlaylistTrack (junction)
3. JOIN to Playlist table
4. Filter tracks by name
5. Return all matching playlists

**Key Concepts:**
• Reverse navigation of M:N relationship
• Same junction table, different direction
• Track → PlaylistTrack → Playlist
• Finds 'where is this track used?'
• Bi-directional querying capability"
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

return results;",
                Explanation = @"**How It Works:**
1. LEFT JOIN Playlist to PlaylistTrack
2. LEFT JOIN to Track
3. GROUP BY playlist
4. COUNT tracks (excluding nulls)
5. SUM duration (excluding nulls)
6. Sort by track count

**Key Concepts:**
• LEFT JOIN with DefaultIfEmpty()
• Aggregation across M:N relationship
• Null-safe counting and summing
• Shows playlist size metrics
• Useful for catalog analytics"
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

return sharedTracks;",
                Explanation = @"**How It Works:**
1. Fetch track-playlist pairs from junction
2. Materialize to memory (SQLite limitation)
3. GROUP BY track
4. COUNT distinct playlists per track
5. Filter tracks in 2+ playlists
6. Sort by popularity
7. Take top 30

**Key Concepts:**
• Two-phase query for SQLite compatibility
• In-memory Distinct().Count()
• Finds 'popular' tracks across playlists
• M:N analysis pattern
• Useful for cross-reference metrics"
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

return popularTracks;",
                Explanation = @"**How It Works:**
1. JOIN PlaylistTrack → Track → Album → Artist
2. Fetch all relationships to memory
3. GROUP BY track and artist
4. COUNT distinct playlists per track
5. Sort by playlist count (most popular first)
6. Take top 20 tracks

**Key Concepts:**
• Multi-table join through M:N relationship
• Two-phase for SQLite performance
• Shows 'most featured' tracks
• Includes artist context
• Common in recommendation systems"
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

return smallPlaylists;",
                Explanation = @"**How It Works:**
1. Count tracks per playlist from junction table
2. Materialize counts
3. Load playlists
4. JOIN in memory
5. Filter playlists with < 250 tracks
6. Sort by track count (smallest first)
7. Take 20 results

**Key Concepts:**
• Two-phase query: aggregate then filter
• GROUP BY on junction table
• In-memory join for flexibility
• HAVING-like filter with WHERE after grouping
• Finds 'small' or curated playlists"
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
return new[] { new { Message = ""No data to demo with"" } };",
                Explanation = @"**How It Works (Pattern):**
1. Get Playlist ID
2. Get Track ID
3. Create new PlaylistTrack junction record
4. Set both foreign keys
5. Call SaveAsync() to persist

**Key Concepts:**
• M:N 'add relationship' = insert into junction table
• No modification to Playlist or Track entities
• Only junction record is created
• SQLiteXM handles foreign key validation
• Common pattern for associating entities"
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

return playlistPairs;",
                Explanation = @"**How It Works:**
1. Self-join PlaylistTrack on TrackId
2. Filter where PlaylistId1 < PlaylistId2 (avoid duplicates)
3. JOIN to Playlist table twice for names
4. GROUP BY playlist pair
5. COUNT shared tracks
6. Sort by shared count (most overlap first)
7. Take top 10 pairs

**Key Concepts:**
• Self-join pattern on junction table
• Finds M:N overlap/similarity
• < condition avoids duplicate pairs
• Useful for 'similar playlists' features
• Common in recommendation algorithms"
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
                                                    Code = @"await using var transaction = new SxmLinqDbContext(""Chinook"");
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
        }",
                                                    Explanation = @"**How It Works:**
1. Create SxmLinqDbContext (await using for auto-dispose)
2. Insert invoice record
3. Get generated invoice.id
4. Insert invoice lines referencing invoice.id
5. CommitTransactionAsync() or auto-commit on dispose
6. On error, transaction auto-rolls back

**Key Concepts:**
• ACID transactions ensure all-or-nothing
• Multiple inserts execute atomically
• SaveAsync(transaction) ties operation to transaction
• Auto-rollback on exception
• Critical for data integrity with related records"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_2",
                                                    Name = "Transaction Rollback on Error",
                                                    Description = "Demonstrate automatic rollback when error occurs",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = new SxmLinqDbContext(""Chinook"");
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
}",
                                                    Explanation = @"**How It Works:**
1. Begin transaction
2. Insert artist successfully
3. Insert album successfully
4. Exception is thrown
5. Control jumps to catch block
6. Transaction auto-rolls back on dispose
7. Neither artist nor album persists

**Key Concepts:**
• Automatic rollback on uncaught exceptions
• 'await using' ensures proper cleanup
• All operations undone if ANY fails
• Database remains consistent
• No explicit RollbackAsync() needed"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_3",
                                                    Name = "Batch Insert with Transaction",
                                                    Description = "Efficiently insert multiple tracks in one transaction",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = new SxmLinqDbContext(""Chinook"");
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
                                    }",
                                                    Explanation = @"**How It Works:**
1. Begin transaction
2. Loop 50 times
3. Each SaveAsync() adds to transaction
4. All inserts buffered
5. CommitTransactionAsync() writes all at once
6. Measure total time

**Key Concepts:**
• Transactions dramatically improve batch insert speed
• Single commit vs 50 individual commits
• Reduces disk I/O and locking overhead
• Can be 10-100x faster than individual saves
• Essential for bulk data operations"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_4",
                                                    Name = "Update Multiple Tables in Transaction",
                                                    Description = "Update artist and all their albums atomically",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = new SxmLinqDbContext(""Chinook"");
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
}",
                                                    Explanation = @"**How It Works:**
1. Begin transaction
2. Query artist and their albums
3. Update artist name
4. Loop through albums
5. Update each album title
6. Commit all updates atomically
7. On error, all updates roll back

**Key Concepts:**
• Multi-table updates in single transaction
• Ensures consistency across related tables
• All updates succeed together or fail together
• Prevents partial updates
• Critical for maintaining referential integrity"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "trans_5",
                                                    Name = "Complex Multi-Table Transaction",
                                                    Description = "Create playlist, add tracks, update statistics",
                                                    Category = QueryCategory.Transactions,
                                                    Type = QueryType.Linq,
                                                    Code = @"await using var transaction = new SxmLinqDbContext(""Chinook"");
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
                                    }",
                                                    Explanation = @"**How It Works:**
1. Begin transaction
2. Create new playlist (get generated ID)
3. Query top 10 tracks
4. Loop: insert PlaylistTrack junction records
5. All 11 inserts (1 playlist + 10 junction) atomic
6. Commit once

**Key Concepts:**
• Complex workflow with multiple steps
• Parent record created first
• Generated ID used in child records
• M:N relationship populated atomically
• Real-world pattern for composite operations"
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
await using (var transaction = new SxmLinqDbContext(""Chinook""))
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Method 1: 20 inserts without transaction (20 commits)
2. Measure time
3. Method 2: 20 inserts with transaction (1 commit)
4. Measure time
5. Calculate speedup factor
6. Return comparison

**Key Concepts:**
• Transactions provide massive performance gains
• Without transaction: each save = disk write
• With transaction: batch all writes
• Typical speedup: 10-50x faster
• Always use transactions for batch operations"
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Define search parameter (user input)
2. Use Contains() in LINQ WHERE clause
3. SQLiteXM generates parameterized SQL
4. Search term treated as data, not code
5. Order and limit results

**Key Concepts:**
• Parameterized queries prevent SQL injection
• User input never concatenated into SQL string
• SQLiteXM handles parameter binding automatically
• Contains() translates to SQL LIKE '%value%'
• Essential for secure user-facing search features"
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Define min and max price parameters
2. Filter with >= and <= comparisons
3. Both parameters safely bound
4. Sort by price, then name
5. Project needed fields
6. Return top 50

**Key Concepts:**
• Range queries with two parameters
• Both bounds safely parameterized
• Common UI pattern: price sliders, filters
• ThenBy for secondary sort
• Projection reduces data transfer"
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

// Single-query approach using Ticks comparison
// Since DateTime is stored as Ticks (Int64), we can compare using .Ticks property
var results = (from invoice in context.GetTable<Invoice>()
               join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
               where invoice.InvoiceDate.Ticks >= startDate.Ticks 
                  && invoice.InvoiceDate.Ticks <= endDate.Ticks
               orderby invoice.InvoiceDate descending
               select new
               {
                   InvoiceId = invoice.id,
                   Date = invoice.InvoiceDate,
                   Customer = customer.FirstName + "" "" + customer.LastName,
                   Total = invoice.Total
               })
               .Take(30)
               .ToList();

return results;",
                                                    Explanation = @"**How It Works:**
1. Define start and end date parameters
2. Compare using .Ticks property (Int64)
3. SQLiteXM translates to safe SQL comparison
4. JOIN customer for display names
5. Sort newest first
6. Return 30 recent invoices

**Key Concepts:**
• DateTime stored as Ticks in SQLite
• Range filtering with date parameters
• Safe date comparisons
• Common for reporting and time-based queries
• JOIN adds customer context"
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Define three filter parameters (artist, genre, price)
2. JOIN 4 tables together
3. Apply all three WHERE conditions (AND)
4. All parameters safely bound
5. Sort and limit results

**Key Concepts:**
• Multiple parameters in single query
• Complex filtering with AND logic
• All inputs parameterized automatically
• Common advanced search pattern
• Shows power of combining filters"
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Define nullable optional parameters
2. Build base query
3. Conditionally add WHERE clauses
4. Only filter if parameter provided (not null)
5. Execute final query

**Key Concepts:**
• Nullable types (string?, decimal?) for optional filters
• Conditional query building
• Dynamic WHERE based on user input
• HasValue check for nullable value types
• Common pattern for advanced search UIs"
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

return results;",
                                                    Explanation = @"**How It Works:**
1. Define search pattern parameter
2. Use Contains() for wildcard search
3. Pattern safely parameterized
4. SQLiteXM translates to LIKE '%pattern%'
5. Return matching tracks

**Key Concepts:**
• Contains() → SQL LIKE with wildcards
• Safe wildcard searching
• Pattern is treated as data, not SQL
• No SQL injection risk
• Common for text search features"
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
} };",
                                                    Explanation = @"**How It Works:**
1. Create new Track instance
2. Set all required properties
3. Call SaveAsync() to insert
4. ID is auto-generated after save
5. Return confirmation with new ID

**Key Concepts:**
• Create entity, set properties, save pattern
• Primary key auto-populated after SaveAsync()
• Foreign keys (AlbumId) must reference existing records
• Timestamp in name ensures uniqueness
• Basic CRUD: Create"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_2",
                                                    Name = "Insert and Get Generated ID",
                                                    Description = "Insert related records and retrieve auto-generated IDs in transaction",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"// Use transaction to ensure both inserts succeed or both fail
await using var transaction = new SxmLinqDbContext(""Chinook"");
try
{
    // Create new artist
    var artist = new Artist 
    { 
        Name = $""New Artist {DateTime.Now:HHmmss}""
    };
    await artist.SaveAsync(transaction);

    // ID is automatically populated after save
    var artistId = artist.id;

    // Now create album using the new artist's ID
    var album = new Album
    {
        Title = ""Debut Album"",
        ArtistId = artistId
    };
    await album.SaveAsync(transaction);

    // Commit transaction. The explicit CommitTransactionAsync() call is optional
    // but considered good practice. Without it, the transaction will AUTO-COMMIT
    // on Dispose (If No Errors)
    await transaction.CommitTransactionAsync();

    return new[] { new 
    { 
        ArtistId = artistId,
        ArtistName = artist.Name,
        AlbumId = album.id,
        AlbumTitle = album.Title,
        Message = ""Artist and Album created atomically with auto-generated IDs""
    } };
}
catch (Exception ex)
{
    return new[] { new { Success = false, Error = ex.Message } };
}",
                                                    Explanation = @"**How It Works:**
1. Start transaction
2. Insert Artist, get auto-generated ID
3. Use Artist ID as foreign key in Album
4. Insert Album
5. Commit both inserts atomically

**Key Concepts:**
• Auto-generated IDs available immediately after SaveAsync()
• Transaction ensures both succeed or both fail
• Common parent-child insert pattern
• Foreign key relationship enforced
• Demonstrates ID propagation"
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
} };",
                                                    Explanation = @"**How It Works:**
1. Load existing track entity
2. Modify property (UnitPrice)
3. Call SaveAsync() to persist change
4. Only modified field updated in DB
5. Return before/after values

**Key Concepts:**
• Load, modify, save pattern
• Only changed properties updated
• No explicit UPDATE SQL needed
• SaveAsync() generates UPDATE statement
• Basic CRUD: Update"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_4",
                                                    Name = "Conditional Update",
                                                    Description = "Update records matching specific criteria using transaction",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

// Find all tracks under $1.00
var cheapTracks = context.GetTable<Track>()
    .Where(t => t.UnitPrice < 1.00m)
    .Take(10)
    .ToList();

// Use transaction for better performance - commit all updates together
await using var transaction = new SxmLinqDbContext(""Chinook"");
try
{
    var updateCount = 0;
    foreach (var track in cheapTracks)
    {
        track.UnitPrice = 1.29m; // Increase price
        await track.SaveAsync();
        updateCount++;
    }

    // Commit transaction. The explicit CommitTransactionAsync() call is optional
    // but considered good practice. Without it, the transaction will AUTO-COMMIT
    // on Dispose (If No Errors)
    await transaction.CommitTransactionAsync();

    return new[] { new 
    { 
        TracksUpdated = updateCount,
        NewPrice = 1.29m,
        Message = $""Updated {updateCount} tracks to new price (in single transaction)""
    } };
}
catch (Exception ex)
{
    return new[] { new { Success = false, Error = ex.Message } };
}",
                                                    Explanation = @"**How It Works:**
1. Query for tracks matching criteria
2. Start transaction
3. Loop through results, modify each
4. SaveAsync() on each within transaction
5. Commit all updates atomically

**Key Concepts:**
• Bulk update via iteration
• Transaction improves performance
• All updates succeed or all fail
• WHERE clause filters targets
• Common batch update pattern"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_5",
                                                    Name = "Bulk Price Increase by Genre",
                                                    Description = "Update all tracks in a specific genre using single query and transaction",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

// Single-query approach: JOIN Track and Genre directly
// Note: The query does maximum filtering/joining in the database.
// However, UPDATE operations require loading entities, modifying properties,
// and saving individually - this cannot be done in a LINQ SELECT query.
var results = (from track in context.GetTable<Track>()
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
               where genre.Name.Contains(""Rock"")
               select new { Track = track, GenreName = genre.Name })
               .Take(20)
               .ToList();

var updateCount = 0;
var genreName = results.FirstOrDefault()?.GenreName ?? ""Rock"";

// Use transaction for better performance - commit all updates together
await using var transaction = new SxmLinqDbContext(""Chinook"");
try
{
    foreach (var item in results)
    {
        // Increase price by 10%
        item.Track.UnitPrice = item.Track.UnitPrice * 1.10m;
        await item.Track.SaveAsync();
        updateCount++;
    }

    // Commit transaction. The explicit CommitTransactionAsync() call is optional
    // but considered good practice. Without it, the transaction will AUTO-COMMIT
    // on Dispose (If No Errors)
    await transaction.CommitTransactionAsync();

    return new[] { new 
    { 
        Genre = genreName,
        TracksUpdated = updateCount,
        PriceIncrease = ""10%"",
        Message = $""Updated {updateCount} rock tracks (in single transaction)""
    } };
}
catch (Exception ex)
{
    return new[] { new 
    { 
        Success = false,
        Error = ex.Message
    } };
}",
                                                    Explanation = @"**How It Works:**
1. JOIN Track and Genre in single query
2. Filter by genre name
3. Load entities with related data
4. Loop, calculate 10% increase
5. Save all in transaction

**Key Concepts:**
• Complex query with JOIN + WHERE
• Calculated updates (percentage)
• Transaction for batch performance
• Demonstrates query + bulk update
• Common business logic pattern"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_6",
                                                    Name = "Delete Single Record",
                                                    Description = "Remove a single playlist from the database",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"// First, create a temporary playlist for deletion demo
                                    // (ensures we have a playlist without tracks to safely delete)
                                    var tempPlaylist = new Playlist 
                                    { 
                                        Name = $""SingleDeleteDemo-{DateTime.Now:HHmmss}""
                                    };
                                    await tempPlaylist.SaveAsync();

                                    // Verify the ID was assigned after save
                                    var savedId = tempPlaylist.id;

                                    // Now find and delete the playlist we just created using the ID
                                    using var context = new SxmLinqDbContext(""Chinook"");
                                    var playlist = context.GetTable<Playlist>()
                                        .FirstOrDefault(p => p.id == savedId);

                                    if (playlist != null)
                                    {
                                        var playlistName = playlist.Name;
                                        await playlist.DeleteAsync();

                                        return new[] { new 
                                        { 
                                            Success = true,
                                            DeletedPlaylist = playlistName,
                                            SavedId = savedId,
                                            Message = ""Playlist deleted successfully""
                                        } };
                                    }

                                    return new[] { new 
                                    { 
                                        Success = false,
                                        SavedId = savedId,
                                        Message = $""Playlist with ID {savedId} not found after save""
                                    } };",
                                                    Explanation = @"**How It Works:**
1. Create temporary playlist for demo
2. Save and capture auto-generated ID
3. Load entity by ID
4. Call DeleteAsync() to remove
5. Return confirmation

**Key Concepts:**
• Create test data for safe demo
• Load entity before delete
• DeleteAsync() generates DELETE SQL
• ID used to verify correct record
• Basic CRUD: Delete"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_7",
                                                    Name = "Conditional Delete",
                                                    Description = "Delete multiple records matching criteria using transaction",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"// First, create some temporary playlists for deletion demo
                                    // (ensures we have playlists without tracks to safely delete)
                                    var timestamp = DateTime.Now.Ticks; // Use Ticks for uniqueness
                                    var savedIds = new List<long>();

                                    for (int i = 1; i <= 3; i++)
                                    {
                                        var playlist = new Playlist 
                                        { 
                                            Name = $""BulkDeleteDemo-{timestamp}-{i}""
                                        };
                                        await playlist.SaveAsync();
                                        savedIds.Add(playlist.id);
                                    }

                                    // Now find and delete playlists using the saved IDs
                                    using var context = new SxmLinqDbContext(""Chinook"");
                                    var playlistsToDelete = context.GetTable<Playlist>()
                                        .Where(p => savedIds.Contains(p.id))
                                        .ToList();

                                    // Use transaction for better performance and atomicity - all deletes succeed or all fail
                                    await using var transaction = new SxmLinqDbContext(""Chinook"");
                                    try
                                    {
                                        var deleteCount = 0;
                                        foreach (var playlist in playlistsToDelete)
                                        {
                                            await playlist.DeleteAsync();
                                            deleteCount++;
                                        }

                                        // Commit transaction. The explicit CommitTransactionAsync() call is optional
                                        // but considered good practice. Without it, the transaction will AUTO-COMMIT
                                        // on Dispose (If No Errors)
                                        await transaction.CommitTransactionAsync();

                                        return new[] { new 
                                        { 
                                            PlaylistsCreated = savedIds.Count,
                                            PlaylistsDeleted = deleteCount,
                                            SavedIds = string.Join("", "", savedIds),
                                            Message = $""Created {savedIds.Count} and deleted {deleteCount} temporary playlists (in single transaction)""
                                        } };
                                    }
                                    catch (Exception ex)
                                    {
                                        return new[] { new { Success = false, Error = ex.Message } };
                                    }",
                                                    Explanation = @"**How It Works:**
1. Create 3 temporary playlists
2. Save and capture their IDs
3. Query playlists using Contains(ids)
4. Delete all in transaction
5. All deletes succeed or all fail

**Key Concepts:**
• Bulk delete via iteration
• Transaction ensures atomicity
• Contains() generates SQL IN clause
• Common bulk cleanup pattern
• Demonstrates transactional deletes"
                                                },
                                                new QueryExample
                                                {
                                                    Id = "mod_8",
                                                    Name = "Delete with Related Records",
                                                    Description = "Delete playlist after removing its tracks first",
                                                    Category = QueryCategory.DataModification,
                                                    Type = QueryType.Linq,
                                                    Code = @"// First, create a temporary playlist with tracks for deletion demo
var tempPlaylist = new Playlist 
{ 
    Name = $""RelatedDeleteDemo-{DateTime.Now:HHmmss}""
};
await tempPlaylist.SaveAsync();
var playlistId = tempPlaylist.id;

// Add some tracks to the playlist (use existing track IDs 1, 2, 3)
var trackIds = new[] { 1, 2, 3 };
foreach (var trackId in trackIds)
{
    var playlistTrack = new PlaylistTrack
    {
        PlaylistId = playlistId,
        TrackId = trackId
    };
    await playlistTrack.SaveAsync();
}

// Now demonstrate deletion with related records
using var context = new SxmLinqDbContext(""Chinook"");
await using var transaction = new SxmLinqDbContext(""Chinook"");

try
{
    // Load the playlist we just created
    var playlist = context.GetTable<Playlist>()
        .FirstOrDefault(p => p.id == playlistId);

    if (playlist != null)
    {
        // First, delete all playlist-track relationships
        var playlistTracks = context.GetTable<PlaylistTrack>()
            .Where(pt => pt.PlaylistId == playlist.id)
            .ToList();

        var trackCount = playlistTracks.Count;
        foreach (var pt in playlistTracks)
        {
            await pt.DeleteAsync();
        }

        // Now delete the playlist itself
        await playlist.DeleteAsync();

        // Commit transaction. The explicit CommitTransactionAsync() call is optional
        // but considered good practice. Without it, the transaction will AUTO-COMMIT
        // on Dispose (If No Errors)
        await transaction.CommitTransactionAsync();

        return new[] { new 
        { 
            Success = true,
            PlaylistName = playlist.Name,
            TracksRemoved = trackCount,
            Message = $""Created playlist with {trackCount} tracks, then deleted all in transaction""
        } };
    }

    return new[] { new { Success = false, Message = ""Playlist not found after creation"" } };
}
catch (Exception ex)
{
    return new[] { new { Success = false, Error = ex.Message } };
}",
                                                    Explanation = @"**How It Works:**
1. Start transaction
2. Load playlist and related tracks
3. Delete PlaylistTrack join records first
4. Then delete Playlist itself
5. Commit all changes atomically

**Key Concepts:**
• Foreign key constraint handling
• Delete child records before parent
• Transaction ensures referential integrity
• All deletes succeed or all fail
• Demonstrates cascading delete pattern"
                                                }
                                            };
                                        }
                                    }

