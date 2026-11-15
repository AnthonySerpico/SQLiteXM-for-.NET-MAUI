using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.LoadWith;

// ---------------------------------------------------------------------------------------------
// LoadWith (eager loading) examples.
//
// Every example in this file relies on the [NotColumn] navigation properties declared on the
// Chinook models (Track.Album, Album.Artist, Invoice.Customer, Customer.SupportRep, etc.).
// A navigation property is NOT a database column. It is an in-memory slot that LoadWith fills
// with the related row when the query runs. Without LoadWith the slot stays null.
//
// Rule of thumb: call LoadWith on the table FIRST, then Where / OrderBy / Take. LoadWith is a
// method on SxmTable<T>; once Where or OrderBy has run, the query is a plain IQueryable<T>
// that no longer exposes LoadWith.
// ---------------------------------------------------------------------------------------------

[QueryExample(
    id: "lw_1",
    name: "Basic LoadWith",
    description: "Load tracks with their Album filled in by a single LoadWith call",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with the Track table
2. Call LoadWith(t => t.Album) to request the related Album row for every track
3. Take 25 tracks and materialize them with ToListAsync
4. Read track.Album?.Title directly - no second query is needed

**Key Concepts:**
- Track.Album is a [NotColumn] navigation property; it is not stored in the database
- Track.AlbumId is the [ForeignKey] column that LoadWith uses to find the matching Album
- Without LoadWith, track.Album would be null on every row
- LoadWith is a many-to-one lookup: many tracks point at one album
""")]
internal sealed class Lw1Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> tracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Album)
            .Take(25)
            .ToListAsync();

        var results = tracks.Select(t => new
        {
            Track = t.Name,
            t.AlbumId,
            AlbumTitle = t.Album?.Title ?? "(not loaded)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_2",
    name: "With vs. Without LoadWith",
    description: "Run the same query twice to see what LoadWith actually changes",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Query the first 5 tracks WITHOUT LoadWith and read track.Album
2. Query the same 5 tracks WITH LoadWith(t => t.Album) and read track.Album again
3. Show both results side by side

**Key Concepts:**
- A navigation property is just a plain C# property; nothing fills it automatically
- Without LoadWith the property is null even though AlbumId has a valid value
- With LoadWith the related row is fetched as part of the same query
- This is the difference between a foreign key (a number) and a loaded relationship (an object)
""")]
internal sealed class Lw2Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        // Without LoadWith: Album stays null.
        List<Track> plain = await ctx.GetTable<Track>()
            .OrderBy(t => t.id)
            .Take(5)
            .ToListAsync();

        // With LoadWith: Album is populated.
        List<Track> loaded = await ctx.GetTable<Track>()
            .LoadWith(t => t.Album)
            .OrderBy(t => t.id)
            .Take(5)
            .ToListAsync();

        var results = new List<object>();

        foreach (Track t in plain)
            results.Add(new { Mode = "Without LoadWith", Track = t.Name, AlbumLoaded = t.Album != null, AlbumTitle = t.Album?.Title ?? "(null)" });

        foreach (Track t in loaded)
            results.Add(new { Mode = "With LoadWith", Track = t.Name, AlbumLoaded = t.Album != null, AlbumTitle = t.Album?.Title ?? "(null)" });

        return results;
    }
}

[QueryExample(
    id: "lw_3",
    name: "Multiple Properties (params)",
    description: "Load Album, Genre, and MediaType in one LoadWith call",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with the Track table
2. Pass three navigation properties to a single LoadWith call
3. Materialize 25 tracks
4. Read all three related entities from each track

**Key Concepts:**
- The params overload accepts any number of navigation property expressions
- Each one is a different relationship: Track -> Album, Track -> Genre, Track -> MediaType
- All three are allowed because each foreign key points at a DIFFERENT table
- Equivalent to chaining three separate LoadWith calls (see the next example)
""")]
internal sealed class Lw3Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> tracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Album, t => t.Genre, t => t.MediaType)
            .Take(25)
            .ToListAsync();

        var results = tracks.Select(t => new
        {
            Track = t.Name,
            Album = t.Album?.Title ?? "(null)",
            Genre = t.Genre?.Name ?? "(null)",
            MediaType = t.MediaType?.Name ?? "(null)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_4",
    name: "Chained LoadWith Calls",
    description: "Chain LoadWith calls one after another instead of using params",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with the InvoiceLine table
2. Call LoadWith(l => l.Track), then LoadWith(l => l.Invoice) on the result
3. Materialize 25 invoice lines
4. Read both related entities

**Key Concepts:**
- Each LoadWith returns a new SxmTable<T>, so the calls can be chained
- Chaining and the params overload produce the same query; pick whichever reads better
- Useful when the set of relationships to load is decided step by step in code
""")]
internal sealed class Lw4Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<InvoiceLine> lines = await ctx.GetTable<InvoiceLine>()
            .LoadWith(l => l.Track)
            .LoadWith(l => l.Invoice)
            .Take(25)
            .ToListAsync();

        var results = lines.Select(l => new
        {
            LineId = l.id,
            Track = l.Track?.Name ?? "(null)",
            InvoiceDate = l.Invoice?.InvoiceDate,
            InvoiceTotal = l.Invoice?.Total,
            l.Quantity,
            l.UnitPrice
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_5",
    name: "LoadWith + Where + OrderBy",
    description: "Combine eager loading with filtering, sorting, and paging",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Call LoadWith(t => t.Genre) FIRST, while the query is still an SxmTable<Track>
2. Filter to tracks longer than 5 minutes
3. Sort by duration descending
4. Take the top 20

**Key Concepts:**
- LoadWith must come before Where / OrderBy / Take
- After Where or OrderBy the query is an IQueryable<Track>, which has no LoadWith method
- All operators run in the database; only the 20 matching tracks (and their genres) are fetched
- This is the everyday shape of a LoadWith query
""")]
internal sealed class Lw5Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> tracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Genre)
            .Where(t => t.Milliseconds > 300000)
            .OrderByDescending(t => t.Milliseconds)
            .Take(20)
            .ToListAsync();

        var results = tracks.Select(t => new
        {
            Track = t.Name,
            Genre = t.Genre?.Name ?? "(null)",
            Minutes = Math.Round(t.Milliseconds / 60000.0, 2)
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_6",
    name: "Filter on the Loaded Entity",
    description: "Use the navigation property inside Where to filter by the related row",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(t => t.Genre) so Genre is available
2. In Where, compare t.Genre!.Name to "Rock"
3. Sort by track name and take 25

**Key Concepts:**
- Once a navigation property is registered, you can reference it in Where
- The filter is translated into SQL as a JOIN plus a WHERE on the Genre table
- This replaces a manual join when all you need is to filter by a related column
- The ! (null-forgiving) operator tells the C# compiler the property is fine to dereference in the expression
""")]
internal sealed class Lw6Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> rockTracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Genre)
            .Where(t => t.Genre!.Name == "Rock")
            .OrderBy(t => t.Name)
            .Take(25)
            .ToListAsync();

        var results = rockTracks.Select(t => new
        {
            Track = t.Name,
            Genre = t.Genre?.Name,
            t.UnitPrice
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_7",
    name: "Nullable FK Leaves Property Null",
    description: "Customers without a support rep get SupportRep == null, not an error",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(c => c.SupportRep) on the Customer table
2. Materialize 30 customers
3. Report whether each customer's SupportRep was loaded

**Key Concepts:**
- Customer.SupportRepId is nullable (long?) - not every customer has a rep
- When the foreign key is NULL, LoadWith leaves the navigation property null
- The customer row is still returned; LoadWith never drops rows or throws for a missing match
- Always use ?. or a null check when reading a navigation property
""")]
internal sealed class Lw7Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Customer> customers = await ctx.GetTable<Customer>()
            .LoadWith(c => c.SupportRep)
            .OrderBy(c => c.LastName)
            .Take(30)
            .ToListAsync();

        var results = customers.Select(c => new
        {
            Customer = c.FirstName + " " + c.LastName,
            c.SupportRepId,
            RepLoaded = c.SupportRep != null,
            SupportRep = c.SupportRep != null ? c.SupportRep.FirstName + " " + c.SupportRep.LastName : "(none)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_8",
    name: "Keep Only Rows With a Match",
    description: "Filter with Where(x => x.Nav != null) to exclude rows whose relationship is missing",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Count all customers
2. Count customers where SupportRep != null, using LoadWith + Where
3. Return both numbers plus the first few matched customers

**Key Concepts:**
- By default LoadWith behaves like a LEFT JOIN: unmatched rows are kept with a null property
- Adding Where(c => c.SupportRep != null) turns it into an INNER JOIN
- The filter runs in SQL, so unmatched rows are never transferred to the app
- Compare the two counts to see how many rows the filter removed
""")]
internal sealed class Lw8Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        int allCustomers = await ctx.GetTable<Customer>().CountAsync();

        int withRep = await ctx.GetTable<Customer>()
            .LoadWith(c => c.SupportRep)
            .Where(c => c.SupportRep != null)
            .CountAsync();

        List<Customer> sample = await ctx.GetTable<Customer>()
            .LoadWith(c => c.SupportRep)
            .Where(c => c.SupportRep != null)
            .OrderBy(c => c.LastName)
            .Take(10)
            .ToListAsync();

        var results = new List<object>
        {
            new { Summary = "All customers", Count = allCustomers },
            new { Summary = "Customers with a support rep", Count = withRep },
            new { Summary = "Customers with NO support rep", Count = allCustomers - withRep }
        };

        foreach (Customer c in sample)
            results.Add(new { Customer = c.FirstName + " " + c.LastName, SupportRep = c.SupportRep!.FirstName + " " + c.SupportRep.LastName });

        return results;
    }
}

[QueryExample(
    id: "lw_9",
    name: "Self-Referencing Relationship",
    description: "Load each employee's Manager, where Manager is another Employee",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(e => e.Manager) on the Employee table
2. Materialize all employees sorted by last name
3. Show each employee and the manager they report to

**Key Concepts:**
- Employee.ReportsTo is a foreign key back to the same Employee table
- Employee.Manager is a navigation property of type Employee
- LoadWith handles self-references exactly like any other relationship
- Top-level managers have ReportsTo == null, so their Manager stays null
""")]
internal sealed class Lw9Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Employee> employees = await ctx.GetTable<Employee>()
            .LoadWith(e => e.Manager)
            .OrderBy(e => e.LastName)
            .ToListAsync();

        var results = employees.Select(e => new
        {
            Employee = e.FirstName + " " + e.LastName,
            e.Title,
            e.ReportsTo,
            Manager = e.Manager != null ? e.Manager.FirstName + " " + e.Manager.LastName : "(top level)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_10",
    name: "Nested Path (Two Levels)",
    description: "Load Track -> Album -> Artist with a nested navigation path",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(t => t.Album!.Artist) asks for the Album AND the Album's Artist
2. Materialize 25 tracks
3. Walk the chain track.Album?.Artist?.Name

**Key Concepts:**
- A nested path loads every hop along the way, not just the last one
- Track.Album is loaded first, then Album.Artist for each loaded album
- The first hop (Album) must be a registered navigation property on Track
- Use ?. at every level when reading, because any hop may be null
""")]
internal sealed class Lw10Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> tracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Album!.Artist)
            .OrderBy(t => t.Name)
            .Take(25)
            .ToListAsync();

        var results = tracks.Select(t => new
        {
            Track = t.Name,
            Album = t.Album?.Title ?? "(null)",
            Artist = t.Album?.Artist?.Name ?? "(null)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_11",
    name: "Nested Path Through a Junction Table",
    description: "PlaylistTrack -> Track -> Album -> Artist in a single query",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with PlaylistTrack, the junction table of the many-to-many Playlist/Track relationship
2. LoadWith(pt => pt.Playlist) for the playlist name
3. LoadWith(pt => pt.Track!.Album!.Artist) for a three-hop chain
4. Materialize 25 rows and flatten the chain

**Key Concepts:**
- A junction row has two foreign keys; each can be loaded independently
- Nested paths can be as deep as the navigation properties allow
- The result is a flat row per playlist entry with Playlist, Track, Album, and Artist names
- Compare with the Many-to-Many category, which does this with explicit joins
""")]
internal sealed class Lw11Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<PlaylistTrack> entries = await ctx.GetTable<PlaylistTrack>()
            .LoadWith(pt => pt.Playlist)
            .LoadWith(pt => pt.Track!.Album!.Artist)
            .OrderBy(pt => pt.PlaylistId)
            .Take(25)
            .ToListAsync();

        var results = entries.Select(pt => new
        {
            Playlist = pt.Playlist?.Name ?? "(null)",
            Track = pt.Track?.Name ?? "(null)",
            Album = pt.Track?.Album?.Title ?? "(null)",
            Artist = pt.Track?.Album?.Artist?.Name ?? "(null)"
        }).ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_12",
    name: "Single Result with FirstOrDefaultAsync",
    description: "LoadWith works with single-row materializers, not just ToListAsync",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(i => i.Customer) on the Invoice table
2. Sort by Total descending and call FirstOrDefaultAsync
3. Return the single largest invoice with its customer already loaded

**Key Concepts:**
- LoadWith composes with FirstOrDefaultAsync, FirstAsync, SingleAsync, CountAsync, and the rest
- The related Customer is fetched in the same round trip as the invoice
- Handy for detail pages: one call gives you the record and the data you display alongside it
""")]
internal sealed class Lw12Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        Invoice? biggest = await ctx.GetTable<Invoice>()
            .LoadWith(i => i.Customer)
            .OrderByDescending(i => i.Total)
            .FirstOrDefaultAsync();

        if (biggest == null)
            return new[] { new { Message = "No invoices found" } };

        return new[]
        {
            new
            {
                InvoiceId = biggest.id,
                biggest.InvoiceDate,
                biggest.Total,
                biggest.BillingCountry,
                Customer = biggest.Customer != null ? biggest.Customer.FirstName + " " + biggest.Customer.LastName : "(null)",
                Email = biggest.Customer?.Email ?? "(null)"
            }
        };
    }
}

[QueryExample(
    id: "lw_13",
    name: "Group In Memory After LoadWith",
    description: "Load tracks with Genre, then group and aggregate the results in C#",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith(t => t.Genre) and fetch 500 tracks
2. Group the loaded list by t.Genre?.Name using LINQ-to-Objects
3. Count tracks and average the price per genre

**Key Concepts:**
- Once the data is in memory, the navigation property is just a normal object
- Grouping by a related entity's column is trivial when that entity is already loaded
- For very large tables, prefer a database-side GROUP BY (see the Aggregations category)
- This pattern is ideal when you already need the rows for display and want a quick summary too
""")]
internal sealed class Lw13Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        List<Track> tracks = await ctx.GetTable<Track>()
            .LoadWith(t => t.Genre)
            .Take(500)
            .ToListAsync();

        var results = tracks
            .GroupBy(t => t.Genre?.Name ?? "(no genre)")
            .Select(g => new
            {
                Genre = g.Key,
                TrackCount = g.Count(),
                AveragePrice = Math.Round(g.Average(t => t.UnitPrice), 2),
                TotalMinutes = Math.Round(g.Sum(t => t.Milliseconds) / 60000.0, 1)
            })
            .OrderByDescending(x => x.TrackCount)
            .ToList();

        return results;
    }
}

[QueryExample(
    id: "lw_14",
    name: "Validation: Not a Navigation Property",
    description: "Passing a plain column to LoadWith throws immediately with a clear message",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Deliberately call LoadWith(t => t.Name) - Name is an ordinary string column
2. Catch the ArgumentException
3. Display the error message SQLiteXM produced

**Key Concepts:**
- LoadWith validates the expression the moment you call it, not when the query runs
- The message names the entity, the offending property, and lists the navigation properties that ARE available
- This catches typos and misunderstandings at the call site instead of deep inside query execution
- The query never reaches the database
""")]
internal sealed class Lw14Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        string outcome;
        try
        {
            // Track.Name is a mapped column, not a [NotColumn] navigation property.
            ctx.GetTable<Track>().LoadWith(t => t.Name);
            outcome = "Unexpected: no exception was thrown";
        }
        catch (ArgumentException ex)
        {
            outcome = ex.Message;
        }

        return new[]
        {
            new { Attempted = "LoadWith(t => t.Name)", Result = outcome }
        };
    }
}

[QueryExample(
    id: "lw_15",
    name: "LoadWith vs. Explicit Join",
    description: "Same data two ways: LoadWith keeps entities, a join projects a flat shape",
    category: QueryCategory.LoadWith,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LoadWith: fetch 10 albums with Artist loaded; the result is List<Album> with Album.Artist set
2. Join: fetch the same 10 albums with an explicit join and project into an anonymous type
3. Return both result sets labelled by approach

**Key Concepts:**
- LoadWith returns real entity objects you can pass around, edit, and SaveAsync
- An explicit join returns a custom shape and is the right tool for reports and projections
- Both are translated to SQL; neither does per-row lookups
- Use an explicit join when an entity has two foreign keys to the same table (LoadWith does not support that case)
""")]
internal sealed class Lw15Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        // Approach 1: LoadWith - full entities with the Artist attached.
        List<Album> albums = await ctx.GetTable<Album>()
            .LoadWith(a => a.Artist)
            .OrderBy(a => a.Title)
            .Take(10)
            .ToListAsync();

        // Approach 2: explicit join - flat projection.
        var joined = (from album in ctx.GetTable<Album>()
                      join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
                      orderby album.Title
                      select new { album.Title, ArtistName = artist.Name })
                     .Take(10)
                     .ToList();

        var results = new List<object>();

        foreach (Album a in albums)
            results.Add(new { Approach = "LoadWith", Album = a.Title, Artist = a.Artist?.Name ?? "(null)" });

        foreach (var j in joined)
            results.Add(new { Approach = "Explicit join", Album = j.Title, Artist = j.ArtistName });

        return results;
    }
}
