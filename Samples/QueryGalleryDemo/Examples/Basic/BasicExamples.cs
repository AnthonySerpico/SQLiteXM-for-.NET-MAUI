using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Basic;

[QueryExample(
    id: "basic_1",
    name: "Get All Artists",
    description: "Simple SELECT query to retrieve all artists",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create a database context for the 'Chinook' database
2. Get the Artist table using GetTable<Artist>()
3. Sort all artists alphabetically by Name using OrderBy()
4. Execute the query and convert results to a List
5. Return the list of Artist objects

**Key Concepts:**
- This is the simplest type of LINQ query - retrieving all records from a single table
- OrderBy() translates to SQL ORDER BY clause
- ToList() executes the query and materializes results in memory
- The using statement ensures the database context is properly disposed
""")]
internal sealed class Basic1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var artists = context.GetTable<Artist>().OrderBy(a => a.Name).ToList();
        return Task.FromResult<object>(artists);
    }
}

[QueryExample(
    id: "basic_2",
    name: "Get All Genres",
    description: "Retrieve all music genres ordered by name",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create a context connected to the Chinook database
2. Access the Genre table via GetTable<Genre>()
3. Order genres alphabetically by name
4. Materialize results into a list
5. Return the complete list of genres

**Key Concepts:**
- Same pattern as basic_1 but with a different table
- GetTable<T>() provides strongly-typed access to database tables
- Method chaining (.OrderBy().ToList()) is a core LINQ pattern
- Query execution is deferred until ToList() is called
""")]
internal sealed class Basic2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var genres = context.GetTable<Genre>().OrderBy(g => g.Name).ToList();
        return Task.FromResult<object>(genres);
    }
}

[QueryExample(
    id: "basic_3",
    name: "Filter Tracks by Genre",
    description: "Get all Rock tracks by filtering on the Rock genre id",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Open an SxmTransaction for Chinook
2. Look up the Rock genre with FirstOrDefault()
3. Query tracks whose GenreId matches
4. Sort by track name and Take(50)
5. Return the list

**Key Concepts:**
- FirstOrDefault() safely handles the case where 'Rock' is not present
- Filtering by a resolved id keeps the generated SQL simple
- Take() caps result size for a fast preview
""")]
internal sealed class Basic3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var rockGenre = context.GetTable<Genre>().FirstOrDefault(g => g.Name == "Rock");
        if (rockGenre == null)
            return Task.FromResult<object>(new List<Track>());
        var tracks = context.GetTable<Track>()
                            .Where(t => t.GenreId == rockGenre.id)
                            .OrderBy(t => t.Name)
                            .Take(50)
                            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "basic_4",
    name: "Find Artist by Name",
    description: "Search for a specific artist using LIKE",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Get the Artist table
2. Filter using Where() to find artists whose name contains 'Zeppelin'
3. Contains() performs a partial string match
4. Execute query with ToList()
5. Return matching artists

**Key Concepts:**
- Contains() translates to SQL LIKE '%Zeppelin%'
- WHERE clause filters rows at the database level, not in memory
- Partial string matching is useful for search functionality
""")]
internal sealed class Basic4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var artists = context.GetTable<Artist>()
                             .Where(a => a.Name.Contains("Zeppelin"))
                             .ToList();
        return Task.FromResult<object>(artists);
    }
}

[QueryExample(
    id: "basic_5",
    name: "Get Tracks by Price Range",
    description: "Filter tracks between $0.99 and $1.49",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access the Track table
2. Filter for tracks with price between $0.99 and $1.49 using compound condition
3. Sort primarily by UnitPrice (low to high)
4. Then sort by Name (secondary sort for tracks with same price)
5. Limit to 100 results

**Key Concepts:**
- Compound WHERE conditions use && (AND) operator
- ThenBy() creates a secondary sort (ORDER BY price, name)
- The 'm' suffix (0.99m) specifies decimal literals for currency
""")]
internal sealed class Basic5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var tracks = context.GetTable<Track>()
                            .Where(t => t.UnitPrice >= 0.99m && t.UnitPrice <= 1.49m)
                            .OrderBy(t => t.UnitPrice)
                            .ThenBy(t => t.Name)
                            .Take(100)
                            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "basic_6",
    name: "Top 10 Most Expensive Tracks",
    description: "Get the highest priced tracks using OrderByDescending",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Get all tracks from the Track table
2. Sort by UnitPrice in descending order (highest first)
3. Apply secondary sort by Name (alphabetically)
4. Take only the first 10 results

**Key Concepts:**
- OrderByDescending() sorts in reverse (high to low)
- 'Top N' pattern: sort descending + Take(N)
- ThenBy() always sorts ascending (even after OrderByDescending)
""")]
internal sealed class Basic6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var tracks = context.GetTable<Track>()
                            .OrderByDescending(t => t.UnitPrice)
                            .ThenBy(t => t.Name)
                            .Take(10)
                            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "basic_7",
    name: "Tracks by Duration Range",
    description: "Find tracks between 3-5 minutes long",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Calculate min/max duration in milliseconds (3 and 5 minutes)
2. Access Track table
3. Filter tracks within the duration range
4. Sort by duration (shortest to longest)
5. Limit to 100 tracks

**Key Concepts:**
- Duration is stored in milliseconds in the database
- Range filtering is a common pattern for numeric/date fields
- Converting minutes to milliseconds: minutes x 60 x 1000
""")]
internal sealed class Basic7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        int minMs = 3 * 60 * 1000;
        int maxMs = 5 * 60 * 1000;
        var tracks = context.GetTable<Track>()
                            .Where(t => t.Milliseconds >= minMs && t.Milliseconds <= maxMs)
                            .OrderBy(t => t.Milliseconds)
                            .Take(100)
                            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "basic_8",
    name: "Case-Insensitive Search",
    description: "Search for artists regardless of case",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Define search term in lowercase ('led')
2. Access Artist table
3. Convert both database Name and search term to lowercase
4. Use Contains() to find matches
5. Sort results alphabetically

**Key Concepts:**
- ToLower() ensures case-insensitive matching
- Both sides of the comparison are lowercased for consistency
- SQLite string comparison is case-sensitive by default (unlike SQL Server)
""")]
internal sealed class Basic8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        string searchTerm = "led";
        var artists = context.GetTable<Artist>()
                             .Where(a => a.Name.ToLower().Contains(searchTerm.ToLower()))
                             .OrderBy(a => a.Name)
                             .ToList();
        return Task.FromResult<object>(artists);
    }
}

[QueryExample(
    id: "basic_9",
    name: "Tracks with Composer",
    description: "Filter tracks that have a composer (NOT NULL)",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access Track table
2. Filter for tracks where Composer is not null AND not empty string
3. Sort primarily by Composer name
4. Then sort by Track name
5. Limit to 100 results

**Key Concepts:**
- NULL checking in databases - some fields may be NULL (no value)
- Must check both null and empty string for completeness
- != null translates to IS NOT NULL in SQL
""")]
internal sealed class Basic9Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var tracks = context.GetTable<Track>()
                            .Where(t => t.Composer != null && t.Composer != "")
                            .OrderBy(t => t.Composer)
                            .ThenBy(t => t.Name)
                            .Take(100)
                            .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "basic_10",
    name: "Distinct Media Types",
    description: "Get unique media types using Distinct",
    category: QueryCategory.Basic,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Access MediaType table
2. Sort media types alphabetically by Name
3. Retrieve all records as a list

**Key Concepts:**
- MediaType table already contains unique values (it's a lookup table)
- Distinct() is not needed here because table structure ensures uniqueness
- OrderBy() ensures consistent, predictable ordering
""")]
internal sealed class Basic10Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmTransaction("Chinook");
        var mediaTypes = context.GetTable<MediaType>().OrderBy(m => m.Name).ToList();
        return Task.FromResult<object>(mediaTypes);
    }
}
