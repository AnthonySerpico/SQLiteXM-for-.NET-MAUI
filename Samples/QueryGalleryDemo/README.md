# QueryGalleryDemo - Comprehensive SQLiteXM Query Showcase

**A production-grade demonstration of SQLiteXM's querying capabilities using a Chinook-style music database**

## 🚀 Quick Start

1. **Build and Run**: Open the solution in Visual Studio 2022+ and run the QueryGalleryDemo project
2. **First Launch**: Welcome screen shows automatic database seeding progress (~5-10 seconds)
3. **Browse Categories**: Tap any of the 10 category cards on the main menu
4. **Run Queries**: Select a query, view the code, tap "Run Query" to see live results
5. **Compare Performance**: Execution time and record count displayed for each query

**Supported Platforms**: Android, iOS, Mac Catalyst, Windows

---

## 📋 Overview

QueryGalleryDemo is an advanced sample application that showcases the full range of SQLiteXM's query capabilities through a rich, categorized gallery of 50+ working examples. The app demonstrates:

- **Chinook-style Schema**: ~25,000 records across 11 related tables (Artists, Albums, Tracks, Genres, Playlists, Customers, Invoices, etc.)
- **LINQ Query Provider**: Type-safe, composable queries with full IntelliSense support
- **Raw SQL Execution**: Custom SQL statements loaded from JSON configuration
- **Transaction Management**: Atomic operations with commit/rollback patterns
- **Data Modification**: INSERT, UPDATE, DELETE operations with bulk support
- **Split-View Execution**: Side-by-side code and results display with performance metrics
- **First-Run Seeding**: Automatic database population with realistic test data

## 🎯 Learning Objectives

This sample teaches developers:

1. **Basic SQLite Operations**: SELECT, WHERE, ORDER BY, LIKE queries
2. **Relationship Navigation**: INNER JOIN, LEFT JOIN, multi-table queries
3. **Aggregation Functions**: COUNT, SUM, AVG, GROUP BY patterns
4. **Advanced LINQ**: Pagination (Skip/Take), complex sorting, compound WHERE clauses
5. **Many-to-Many Relationships**: Junction table queries and bidirectional navigation
6. **Transaction Patterns**: Atomic operations, commit/rollback, error handling
7. **Parameterized Queries**: SQL injection prevention and safe parameter passing
8. **Data Modification**: INSERT, UPDATE, DELETE with bulk operations and deferred execution
9. **Raw SQL Integration**: Loading and executing custom SQL from configuration files
10. **Performance Optimization**: Measuring execution time, large result set handling

## 🏗️ Architecture

### Database Schema

The Chinook-style schema includes:

```
- Artist (275 artists)
- Album (347 albums)
- Track (3,503 tracks)
- Genre (25 genres)
- MediaType (5 media types)
- Playlist (18 playlists)
- PlaylistTrack (8,715 junction records - many-to-many)
- Employee (8 employees)
- Customer (59 customers)
- Invoice (412 invoices)
- InvoiceLine (2,240 line items)

Total: ~25,000 records demonstrating realistic relational data
```

### Application Flow

```
WelcomePage (seeding progress)
	↓
QueryMenuPage (10 category cards)
	↓
QueryCategoryPage (list of queries in selected category)
	↓
QueryExecutionPage (split-view: code + results)
```

### Technology Stack

- **.NET 9 MAUI**: Cross-platform UI framework
- **SQLiteXM**: ORM with LINQ provider and raw SQL support
- **CommunityToolkit.Mvvm 8.4.0**: MVVM infrastructure ([ObservableProperty], [RelayCommand])
- **Chinook Database Pattern**: Industry-standard sample schema for learning

## 📂 Project Structure

```
QueryGalleryDemo/
├── Models/                      # 11 Chinook-style entities + query models
│   ├── Artist.cs               # [Table(Database = "Chinook")]
│   ├── Album.cs                # Foreign key to Artist, compound indexes
│   ├── Track.cs                # Foreign keys to Album, Genre, MediaType, compound indexes
│   ├── Genre.cs
│   ├── MediaType.cs
│   ├── Playlist.cs
│   ├── PlaylistTrack.cs        # Many-to-many junction table with unique compound index
│   ├── Customer.cs             # Compound indexes on Country, LastName, FirstName
│   ├── Employee.cs
│   ├── Invoice.cs              # Compound index on CustomerId, InvoiceDate
│   ├── InvoiceLine.cs          # Compound indexes for query optimization
│   ├── QueryCategory.cs        # Enum for 10 categories
│   ├── QueryExample.cs         # Query definition model
│   └── QueryResult.cs          # Result wrapper model
├── Services/
│   ├── DatabaseSeeder.cs       # First-run data population (~25K records)
│   ├── QueryExampleProvider.cs # 50+ categorized query examples
│   └── NavigationService.cs    # Page navigation helper
├── ViewModels/
│   ├── BaseViewModel.cs        # Shared MVVM base
│   ├── WelcomeViewModel.cs     # Seeding progress management
│   ├── QueryMenuViewModel.cs   # Category navigation
│   ├── QueryCategoryViewModel.cs   # Query list per category
│   └── QueryExecutionViewModel.cs  # Query execution + results
├── Views/
│   ├── WelcomePage.xaml        # Startup / seeding UI
│   ├── QueryMenuPage.xaml      # 10-category grid menu
│   ├── QueryCategoryPage.xaml  # Query list for selected category
│   └── QueryExecutionPage.xaml # Split-view code + results
├── Converters/
│   ├── StringNotEmptyConverter.cs
│   └── InvertedBoolConverter.cs
├── Resources/Raw/
│   └── SqlStatements.json      # Chinook database + raw SQL examples
└── App.xaml                    # Global resources and converters
```

## 🔍 Query Categories

### 1. **Basic Queries** (7 examples)
- Get All Artists
- Get All Genres
- Filter Tracks by Genre
- Find Artist by Name (LIKE search)
- Get Tracks by Price Range (WHERE with range)
- Search Artists by Partial Name
- Filter Multiple Genres (OR conditions)

### 2. **Relationships** (8 examples)
- Tracks with Album Info (INNER JOIN)
- Albums with Artist Names (INNER JOIN)
- Complete Track Information (multi-table JOIN with LEFT JOIN)
- Tracks with Genre and MediaType (3-table JOIN)
- Albums with Track Count (GROUP BY with JOIN)
- Artist Revenue Analysis (complex multi-join)
- Customer Purchase History (invoice chain)
- Employee Sales Performance (hierarchical relationships)

### 3. **Aggregations** (6 examples)
- Count Tracks by Genre (GROUP BY + COUNT)
- Album Count by Artist (GROUP BY)
- Average Track Duration by Genre (GROUP BY + AVG)
- Total Revenue by Artist (SUM + multi-table JOIN)
- Customer Spending Statistics (AVG, MAX, MIN)
- Genre Popularity Analysis (multiple aggregations)

### 4. **Advanced LINQ** (7 examples)
- Paging with Skip/Take (pagination pattern)
- Multiple ORDER BY (complex sorting)
- Complex WHERE with Multiple Conditions (AND/OR logic)
- Dynamic Filtering (conditional query building)
- Subquery Pattern (nested queries)
- Case-Insensitive Search (collation handling)
- Top N Per Group (advanced grouping)

### 5. **Raw SQL** (6 examples)
- Get All Artists (raw SQL from JSON)
- Tracks with Album/Artist (complex JOIN in raw SQL)
- Top Selling Tracks (aggregation via raw SQL)
- Custom Report Query (dynamic SQL)
- Performance Comparison (LINQ vs SQL)
- Stored Procedure Pattern (parameter-based SQL)

### 6. **Performance** (5 examples)
- Query 1000+ Tracks (large result set handling)
- Complex Multi-Table Join (5-table query with timing)
- Index Optimization Demo (compound index benefits)
- Batch Query Operations (bulk reads)
- Memory-Efficient Pagination (streaming results)

### 7. **Many-to-Many** (8 examples)
- Tracks in a Playlist (junction table query)
- Playlists Containing Track (reverse junction query)
- Playlist Statistics (aggregation through junction table)
- Tracks Shared Between Playlists (advanced junction analysis)
- Popular Tracks in Playlists (multi-join with grouping)
- Playlists with Few Tracks (filtered aggregation)
- Add Track to Playlist (junction insert pattern)
- Playlist Overlap Analysis (self-join on junction table)

### 8. **Transactions** (5 examples)
- Simple Transaction (commit/rollback pattern)
- Multi-Table Transaction (atomic operations across tables)
- Bulk Insert with Transaction (batch operations)
- Transaction Rollback on Error (error handling)
- Nested Transaction Pattern (savepoints)

### 9. **Parameterized Queries** (4 examples)
- Safe Parameter Binding (SQL injection prevention)
- Multiple Parameter Query (complex filters)
- Dynamic Parameter Lists (IN clause handling)
- Parameterized Raw SQL (mixing parameters with custom SQL)

### 10. **Data Modification** (6 examples)
- Insert New Record (single insert)
- Bulk Insert Pattern (batch operations)
- Update Single Record (targeted update)
- Bulk Update with Filter (Set().UpdateAsync() pattern)
- Delete Record (single delete)
- Bulk Delete Pattern (filtered batch delete)

## 💡 Key Features Demonstrated

### Compound Index Optimization
Models use compound indexes for query performance optimization:

```csharp
[Table(Database = "Chinook")]
[Index("PlaylistId", "TrackId")]
[Index("TrackId", "PlaylistId")]
[UniqueIndex("PlaylistId", "TrackId")]
public class PlaylistTrack : SxmBaseEntity
{
	[ForeignKey(typeof(Playlist))]
	public long PlaylistId { get; set; }

	[ForeignKey(typeof(Track))]
	public long TrackId { get; set; }
}
```

### Direct Entity Binding
All models use `[Table(Database = "Chinook")]` to target the shared music database.

```csharp
[Table(Database = "Chinook")]
[Index("AlbumId", "GenreId")]
[Index("GenreId", "UnitPrice")]
[Index("AlbumId", "TrackNumber")]
public class Track : SxmBaseEntity
{
	public string Name { get; set; } = string.Empty;

	[ForeignKey(typeof(Album))]
	[Index]
	public long AlbumId { get; set; }

	[ForeignKey(typeof(Genre))]
	[Index]
	public long? GenreId { get; set; }

	public int Milliseconds { get; set; }
	public decimal UnitPrice { get; set; }
}
```

### LINQ Query Provider

SQLiteXM translates LINQ expressions to optimized SQLite queries:

```csharp
await using (var context = new SxmLinqDbContext("Chinook"))

var results = (from track in context.GetTable<Track>()
			   join album in context.GetTable<Album>() on track.AlbumId equals album.id
			   join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
			   where artist.Name.Contains("Rock")
			   orderby track.Name
			   select new { track.Name, album.Title, artist.Name })
			   .Take(50)
			   .ToList();
```

### Bulk Data Modification with Deferred Execution

SQLiteXM supports efficient bulk updates using the deferred execution pattern:

```csharp
await using (var context = new SxmLinqDbContext("Chinook"))

// Queue bulk update (not executed yet)
await context.GetTable<Track>()
	.Where(t => t.Name.Contains("Rock"))
	.Set(t => t.UnitPrice, 1.99m)
	.UpdateAsync();

// Execute all queued operations in a single transaction
await context.SubmitChangesAsync();
```

### Transaction Management

Atomic operations with commit/rollback patterns:

```csharp
await using (var context = new SxmLinqDbContext("Chinook"))

try
{
	// Queue multiple operations
	await context.GetTable<Track>()
		.Where(t => t.GenreId == 1)
		.Set(t => t.UnitPrice, 0.99m)
		.UpdateAsync();

	var newArtist = new Artist { Name = "New Artist" };
	context.InsertOnSubmit(newArtist);

	// Commit all changes atomically
	await context.SubmitChangesAsync();
}
catch (Exception ex)
{
	// Transaction automatically rolls back on exception
	Console.WriteLine($"Transaction failed: {ex.Message}");
}
```

### Raw SQL Execution

Load SQL statements from `SqlStatements.json` and execute with type-safe results:

```csharp
var results = await SxmStatement.RunStatementAsync("GetAllArtistsRaw", new Dictionary<string, object?>());
var dynamic = await SxmStatement.RunStatementAsync("GetTopSellingTracks", new Dictionary<string, object?>());
```

### Parameterized Queries

Safe parameter binding prevents SQL injection:

```csharp
await using (var context = new SxmLinqDbContext("Chinook"))

string searchTerm = userInput; // User-provided input
var results = context.GetTable<Artist>()
	.Where(a => a.Name.Contains(searchTerm)) // Safe parameter binding
	.ToList();
```

### Split-View Execution UI

The QueryExecutionPage displays:
- 📝 **Query Code Pane**: Syntax-highlighted C# code showing the exact query
- ▶ **Run Query Button**: Execute the query and measure performance
- 📊 **Results Pane**: Formatted query results with record count
- ⏱️ **Execution Metrics**: Millisecond timing for performance comparison

### First-Run Seeding

DatabaseSeeder automatically populates the database on first launch:
- Uses `Preferences` to track seeding status
- Generates realistic hierarchical data (Artist → Album → Track)
- Creates many-to-many relationships (Playlist ↔ Track)
- Seeds ~25,000 records with progress feedback

```csharp
if (!await DatabaseSeeder.IsDatabaseSeededAsync())
{
	await DatabaseSeeder.SeedDatabaseAsync();
}
```

## 🚀 Running the Sample

1. **Build and Run**: The app targets .NET 9 MAUI (Android, iOS, Mac Catalyst, Windows)
2. **First Launch**: Welcome screen shows seeding progress (~5-10 seconds)
3. **Explore Categories**: Tap any of the 7 category cards on the menu
4. **Run Queries**: Select a query, view the code, tap "Run Query" to see results
5. **Compare Performance**: Execution time is shown for each query

## 🎓 Educational Value

### Comparison with Other Samples

| Feature | RegistrationDemo | DirectBindingDemo | **QueryGalleryDemo** |
|---------|------------------|-------------------|---------------------|
| **Focus** | Multi-step forms | Direct entity binding | **Comprehensive query gallery** |
| **Database** | 2 databases | 1 database | **1 large database** |
| **Records** | User-generated | User-generated | **~25,000 seeded** |
| **Tables** | 2 tables | 2 tables | **11 tables** |
| **Relationships** | Simple 1:1 | Simple 1:1 | **Complex: 1:M + M:M** |
| **Query Patterns** | Basic CRUD | Basic queries | **50+ examples** |
| **Categories** | N/A | N/A | **10 categories** |
| **Raw SQL** | No | No | **Yes (6 examples)** |
| **Transactions** | No | No | **Yes (5 examples)** |
| **Bulk Operations** | No | No | **Yes (DML examples)** |
| **Compound Indexes** | No | No | **Yes (optimized)** |
| **Performance** | Not emphasized | Not emphasized | **Measured + displayed** |

### What Makes This Sample Unique

1. **Real-World Schema**: Chinook is the industry-standard sample database for learning SQL
2. **Scale**: ~25K records demonstrate realistic query performance
3. **Breadth**: Covers basic → advanced → raw SQL → many-to-many → transactions → DML
4. **Interactive**: Run queries live, see code + results + timing
5. **Educational**: Each query is documented with name, description, category, explanation, and type
6. **Performance Focus**: Compound indexes on key tables optimize common query patterns
7. **Transaction Patterns**: Demonstrates atomic operations and deferred execution
8. **Security**: Parameterized query examples show SQL injection prevention

## 📖 Learning Path

**Recommended exploration order:**

1. **Start with Basic Queries** → Understand WHERE, ORDER BY, LIKE
2. **Move to Relationships** → Learn INNER JOIN and LEFT JOIN
3. **Try Aggregations** → Master COUNT, SUM, AVG, GROUP BY
4. **Explore Advanced LINQ** → Pagination, complex sorting, compound conditions
5. **Compare Raw SQL** → See how custom SQL integrates with LINQ
6. **Test Performance** → Measure large result sets and complex joins
7. **Master Many-to-Many** → Junction tables and bidirectional navigation
8. **Learn Transactions** → Atomic operations, commit/rollback patterns
9. **Practice Parameterized Queries** → Safe parameter binding techniques
10. **Apply Data Modification** → INSERT, UPDATE, DELETE with bulk operations

## 🔧 Customization

### Adding New Query Examples

1. Open `Services/QueryExampleProvider.cs`
2. Add to the appropriate category method (e.g., `GetBasicQueryExamples()`)
3. Provide Id, Name, Description, Category, Type, Code, and Explanation
4. The query will automatically appear in the app

Example:
```csharp
new QueryExample
{
    Id = "basic_8",
    Name = "My Custom Query",
    Description = "Description of what this query does",
    Category = QueryCategory.Basic,
    Type = QueryType.Linq,
    Code = @"using var context = new SxmLinqDbContext(""Chinook"");

var results = context.GetTable<Artist>()
    .Where(a => a.Name.StartsWith(""A""))
    .ToList();
return results;",
    Explanation = @"**How It Works:**
1. Step-by-step explanation
2. Key concepts

**Key Concepts:**
• Important points
• Technical details"
}
```

### Adding New Categories

1. Add to `Models/QueryCategory.cs` enum
2. Create a new Frame in `Views/QueryMenuPage.xaml` with appropriate icon and description
3. Add category method in `QueryExampleProvider.cs` (e.g., `GetMyCategoryExamples()`)
4. Update `GetAllExamples()` and `GetExamplesByCategory()` in `QueryExampleProvider.cs`
5. Handle navigation in `QueryMenuViewModel.NavigateToCategoryCommand`

### Customizing the Schema

1. Modify entities in `Models/` (add properties, indexes, constraints)
2. Update `DatabaseSeeder.cs` seeding logic to populate new fields
3. Adjust queries in `QueryExampleProvider.cs` to use new schema
4. Update `SqlStatements.json` for raw SQL changes

### Adding Compound Indexes

Add class-level or property-level index attributes to optimize queries:

```csharp
[Table(Database = "Chinook")]
[Index("AlbumId", "GenreId")]  // Compound index for multi-column queries
public class Track : SxmBaseEntity
{
    [ForeignKey(typeof(Album))]
    [Index]  // Single-column index
    public long AlbumId { get; set; }

    // ... other properties
}
```

## 🎯 Best Practices Demonstrated

✅ **Separation of Concerns**: Models, Services, ViewModels, Views clearly separated  
✅ **MVVM Pattern**: CommunityToolkit.Mvvm for clean, testable ViewModels  
✅ **Async/Await**: All database operations are async  
✅ **Error Handling**: Try-catch blocks with user-friendly error messages  
✅ **Resource Management**: `using` statements for DbContext disposal  
✅ **Performance Measurement**: Stopwatch timing for query execution  
✅ **Data Validation**: Foreign key relationships enforced via attributes  
✅ **Configuration-Driven**: Raw SQL loaded from external JSON file  
✅ **Compound Indexes**: Performance optimization through multi-column indexes  
✅ **Transaction Safety**: Atomic operations with automatic rollback on error  
✅ **SQL Injection Prevention**: Parameterized queries for user input  
✅ **Deferred Execution**: Bulk operations queued and committed together  
✅ **Code Reusability**: Base ViewModel and shared navigation service  

## 🆚 When to Use This Pattern

**Use QueryGalleryDemo patterns when you need:**
- Complex relational queries across multiple tables
- Performance measurement and optimization
- Mix of LINQ and raw SQL
- Many-to-many relationships
- Large datasets (thousands of records)
- Transaction management with commit/rollback
- Bulk data modification operations
- Compound index optimization
- Educational / demo applications

**Consider simpler patterns (DirectBindingDemo) when you need:**
- Simple CRUD operations
- Small datasets (<100 records)
- Single-table queries
- Rapid prototyping
- No transaction requirements

## 📚 Additional Resources

- **SQLiteXM Documentation**: [GitHub Repository](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI)
- **Chinook Database**: Industry-standard sample schema for SQL learning
- **LINQ Query Syntax**: [Microsoft Docs](https://learn.microsoft.com/dotnet/csharp/linq/)
- **.NET MAUI**: [Official Documentation](https://learn.microsoft.com/dotnet/maui/)

## 🤝 Contributing

This is a sample application. For SQLiteXM library contributions, see the main repository.

---

**Built with SQLiteXM** • Demonstrating real-world database patterns for .NET MAUI applications
