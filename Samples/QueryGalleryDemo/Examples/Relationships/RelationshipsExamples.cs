using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Relationships;

[QueryExample(
    id: "rel_1",
    name: "Tracks with Album Info",
    description: "JOIN tracks with their album information",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Track table
2. JOIN to Album table using AlbumId foreign key
3. Sort results by track name
4. Project selected fields into anonymous type
5. Limit to 50 records

**Key Concepts:**
- INNER JOIN - only returns tracks that have a matching album
- Foreign key relationship: Track.AlbumId references Album.id
- Anonymous types (new {...}) let you shape query results
- JOIN operations happen at the database level for efficiency
""")]
internal sealed class Rel1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from track in ctx.GetTable<Track>()
                       join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
                       orderby track.Name
                       select new { track.Name, AlbumTitle = album.Title, track.Milliseconds })
                      .Take(50)
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_2",
    name: "Albums with Artist Names",
    description: "JOIN albums with their artist information",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Album table
2. JOIN to Artist table using ArtistId foreign key
3. Sort by artist name first, then album title
4. Project fields into result object

**Key Concepts:**
- Multi-level sorting: orderby artist, then album (ORDER BY artist, title)
- Foreign key Album.ArtistId -> Artist.id
- Result shows hierarchical data (artist -> albums)
""")]
internal sealed class Rel2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from album in ctx.GetTable<Album>()
                       join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
                       orderby artist.Name, album.Title
                       select new { album.Title, ArtistName = artist.Name, album.id })
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_3",
    name: "Complete Track Information",
    description: "JOIN tracks with album, artist, and genre",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Track table
2. JOIN to Album (required)
3. JOIN Album to Artist (required)
4. LEFT JOIN to Genre (optional, using DefaultIfEmpty)
5. Sort by artist, album, track number

**Key Concepts:**
- Chain multiple JOINs to traverse relationships: Track -> Album -> Artist
- LEFT JOIN (into...DefaultIfEmpty) includes tracks even if genre is null
- Null-coalescing: genre != null ? genre.Name : 'Unknown'
""")]
internal sealed class Rel3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from track in ctx.GetTable<Track>()
                       join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
                       join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
                       join genre in ctx.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_4",
    name: "Customers with Support Rep",
    description: "JOIN customers with their assigned employee",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Customer table
2. LEFT JOIN to Employee via SupportRepId
3. Sort by customer last name, first name
4. Build result with customer + support rep info
5. Handle null employees with conditional logic

**Key Concepts:**
- LEFT JOIN ensures all customers are shown (even without support rep)
- SupportRepId may be NULL for some customers
- Conditional expressions handle missing relationships
""")]
internal sealed class Rel4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from customer in ctx.GetTable<Customer>()
                       join employee in ctx.GetTable<Employee>() on customer.SupportRepId equals employee.id into empGroup
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_5",
    name: "Employee Hierarchy",
    description: "Self-join to show employee reporting structure",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Employee table
2. SELF-JOIN: join Employee to itself via ReportsTo field
3. Use LEFT JOIN to handle top-level managers (no boss)
4. Sort by employee name
5. Project employee + manager info

**Key Concepts:**
- Self-join: table joined to itself to model hierarchical data
- ReportsTo is a foreign key pointing to another Employee.id
- Top-level employees have ReportsTo = NULL
""")]
internal sealed class Rel5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from emp in ctx.GetTable<Employee>()
                       join manager in ctx.GetTable<Employee>() on emp.ReportsTo equals manager.id into mgrGroup
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_6",
    name: "Invoice with Customer Details",
    description: "JOIN invoices with customer information",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Invoice table
2. JOIN to Customer table via CustomerId
3. Sort by invoice date (newest first)
4. Project invoice and customer fields together
5. Limit to 100 most recent

**Key Concepts:**
- INNER JOIN ensures invoices only returned if customer exists
- OrderByDescending sorts newest to oldest (descending date)
- Combines transactional data (invoice) with reference data (customer)
""")]
internal sealed class Rel6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from invoice in ctx.GetTable<Invoice>()
                       join customer in ctx.GetTable<Customer>() on invoice.CustomerId equals customer.id
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_7",
    name: "Track with All Related Entities",
    description: "JOIN track with genre, media type, album, and artist",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Track
2. JOIN to Album (required)
3. JOIN to Artist via Album (required)
4. LEFT JOIN to Genre (optional)
5. LEFT JOIN to MediaType (optional)

**Key Concepts:**
- Complex multi-table JOIN showing complete track metadata
- Mix of INNER and LEFT JOINs as needed
- Calculated field: Milliseconds converted to minutes
""")]
internal sealed class Rel7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from track in ctx.GetTable<Track>()
                       join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
                       join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
                       join genre in ctx.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
                       from genre in genreGroup.DefaultIfEmpty()
                       join mediaType in ctx.GetTable<MediaType>() on track.MediaTypeId equals mediaType.id into mtGroup
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
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "rel_8",
    name: "Handling Optional Columns (NULL Composer)",
    description: "Project a nullable column with a default via ??",
    category: QueryCategory.Relationships,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Select from Track table
2. Project Name and Composer fields
3. Use null-coalescing operator (??) to handle null Composer
4. Take 50 records

**Key Concepts:**
- NULL handling: ?? operator provides default value when field is null
- Demonstrates data with optional fields
- Composer can be NULL in many tracks
""")]
internal sealed class Rel8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var ctx = new SxmTransaction("Chinook");
        var results = (from track in ctx.GetTable<Track>()
                       select new
                       {
                           track.Name,
                           Composer = track.Composer ?? "No Composer"
                       })
                      .Take(50)
                      .ToList();
        return Task.FromResult<object>(results);
    }
}
