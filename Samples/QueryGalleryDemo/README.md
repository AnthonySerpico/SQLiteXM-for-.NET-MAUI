# QueryGalleryDemo - Comprehensive SQLiteXM Query Showcase

**A production-grade demonstration of SQLiteXM's querying capabilities using a Chinook-style music database**

## 📋 Overview

QueryGalleryDemo is an advanced sample application that showcases the full range of SQLiteXM's query capabilities through a rich, categorized gallery of 30+ working examples. The app demonstrates:

- **Chinook-style Schema**: ~25,000 records across 11 related tables (Artists, Albums, Tracks, Genres, Playlists, Customers, Invoices, etc.)
- **LINQ Query Provider**: Type-safe, composable queries with full IntelliSense support
- **Raw SQL Execution**: Custom SQL statements loaded from JSON configuration
- **Split-View Execution**: Side-by-side code and results display with performance metrics
- **First-Run Seeding**: Automatic database population with realistic test data

## 🎯 Learning Objectives

This sample teaches developers:

1. **Basic SQLite Operations**: SELECT, WHERE, ORDER BY, LIKE queries
2. **Relationship Navigation**: INNER JOIN, LEFT JOIN, multi-table queries
3. **Aggregation Functions**: COUNT, SUM, AVG, GROUP BY patterns
4. **Advanced LINQ**: Pagination (Skip/Take), complex sorting, compound WHERE clauses
5. **Many-to-Many Relationships**: Junction table queries and bidirectional navigation
6. **Raw SQL Integration**: Loading and executing custom SQL from configuration files
7. **Performance Optimization**: Measuring execution time, large result set handling

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
QueryMenuPage (7 category cards)
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
├── Models/                      # 11 Chinook-style entities
│   ├── Artist.cs               # [Table(Database = "Chinook")]
│   ├── Album.cs                # Foreign key to Artist
│   ├── Track.cs                # Foreign keys to Album, Genre, MediaType
│   ├── Genre.cs
│   ├── MediaType.cs
│   ├── Playlist.cs
│   ├── PlaylistTrack.cs        # Many-to-many junction table
│   ├── Customer.cs
│   ├── Employee.cs
│   ├── Invoice.cs
│   └── InvoiceLine.cs
├── Services/
│   ├── DatabaseSeeder.cs       # First-run data population (~25K records)
│   └── QueryExampleProvider.cs # 30+ categorized query examples
├── ViewModels/
│   ├── BaseViewModel.cs        # Shared MVVM base
│   ├── WelcomeViewModel.cs     # Seeding progress management
│   ├── QueryMenuViewModel.cs   # Category navigation
│   ├── QueryCategoryViewModel.cs   # Query list per category
│   └── QueryExecutionViewModel.cs  # Query execution + results
├── Views/
│   ├── WelcomePage.xaml        # Startup / seeding UI
│   ├── QueryMenuPage.xaml      # 7-category grid menu
│   ├── QueryCategoryPage.xaml  # Query list for selected category
│   └── QueryExecutionPage.xaml # Split-view code + results
├── Converters/
│   ├── StringNotEmptyConverter.cs
│   └── InvertedBoolConverter.cs
├── Resources/Raw/
│   └── SqlStatements.json      # Chinook database + 6 raw SQL examples
└── App.xaml                    # Global resources and converters
```

## 🔍 Query Categories

### 1. **Basic Queries** (5 examples)
- Get All Artists
- Get All Genres
- Filter Tracks by Genre
- Find Artist by Name (LIKE search)
- Get Tracks by Price Range (WHERE with range)

### 2. **Relationships** (3 examples)
- Tracks with Album Info (INNER JOIN)
- Albums with Artist Names (INNER JOIN)
- Complete Track Information (multi-table JOIN with LEFT JOIN)

### 3. **Aggregations** (3 examples)
- Count Tracks by Genre (GROUP BY + COUNT)
- Album Count by Artist (GROUP BY)
- Average Track Duration by Genre (GROUP BY + AVG)

### 4. **Advanced LINQ** (3 examples)
- Paging with Skip/Take (pagination pattern)
- Multiple ORDER BY (complex sorting)
- Complex WHERE with Multiple Conditions (AND/OR logic)

### 5. **Raw SQL** (3 examples)
- Get All Artists (raw SQL from JSON)
- Tracks with Album/Artist (complex JOIN in raw SQL)
- Top Selling Tracks (aggregation via raw SQL)

### 6. **Performance** (2 examples)
- Query 1000+ Tracks (large result set handling)
- Complex Multi-Table Join (5-table query with timing)

### 7. **Many-to-Many** (3 examples)
- Tracks in a Playlist (junction table query)
- Playlists Containing Track (reverse junction query)
- Playlist Statistics (aggregation through junction table)

## 💡 Key Features Demonstrated

### Direct Entity Binding
All models use `[Table(Database = "Chinook")]` to target the shared music database.

```csharp
[Table(Database = "Chinook")]
public class Track : SxmBaseEntity
{
	public string Name { get; set; } = string.Empty;

	[ForeignKey(typeof(Album))]
	public long AlbumId { get; set; }

	[ForeignKey(typeof(Genre))]
	public long? GenreId { get; set; }

	public int Milliseconds { get; set; }
	public decimal UnitPrice { get; set; }
}
```

### LINQ Query Provider

SQLiteXM translates LINQ expressions to optimized SQLite queries:

```csharp
using var context = new SxmLinqDbContext("Chinook");

var results = (from track in context.GetTable<Track>()
			   join album in context.GetTable<Album>() on track.AlbumId equals album.id
			   join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
			   where artist.Name.Contains("Rock")
			   orderby track.Name
			   select new { track.Name, album.Title, artist.Name })
			   .Take(50)
			   .ToList();
```

### Raw SQL Execution

Load SQL statements from `SqlStatements.json` and execute with type-safe results:

```csharp
var results = await SxmDatabase.ExecuteQueryAsync<Artist>("GetAllArtistsRaw");
var dynamic = await SxmDatabase.ExecuteQueryAsync<dynamic>("GetTopSellingTracks");
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
| **Focus** | Multi-step forms | Direct entity binding | **Query gallery** |
| **Database** | 2 databases | 1 database | **1 large database** |
| **Records** | User-generated | User-generated | **~25,000 seeded** |
| **Tables** | 2 tables | 2 tables | **11 tables** |
| **Relationships** | Simple 1:1 | Simple 1:1 | **Complex: 1:M + M:M** |
| **Query Patterns** | Basic CRUD | Basic queries | **30+ examples** |
| **Raw SQL** | No | No | **Yes (6 examples)** |
| **Performance** | Not emphasized | Not emphasized | **Measured + displayed** |

### What Makes This Sample Unique

1. **Real-World Schema**: Chinook is the industry-standard sample database for learning SQL
2. **Scale**: ~25K records demonstrate realistic query performance
3. **Breadth**: Covers basic → advanced → raw SQL → many-to-many patterns
4. **Interactive**: Run queries live, see code + results + timing
5. **Educational**: Each query is documented with name, description, category, and type

## 📖 Learning Path

**Recommended exploration order:**

1. **Start with Basic Queries** → Understand WHERE, ORDER BY, LIKE
2. **Move to Relationships** → Learn INNER JOIN and LEFT JOIN
3. **Try Aggregations** → Master COUNT, SUM, AVG, GROUP BY
4. **Explore Advanced LINQ** → Pagination, complex sorting, compound conditions
5. **Compare Raw SQL** → See how custom SQL integrates with LINQ
6. **Test Performance** → Measure large result sets and complex joins
7. **Master Many-to-Many** → Junction tables and bidirectional navigation

## 🔧 Customization

### Adding New Query Examples

1. Open `Services/QueryExampleProvider.cs`
2. Add to the appropriate category method (e.g., `GetBasicQueryExamples()`)
3. Provide Id, Name, Description, Category, Type, and Code
4. Implement execution logic in `QueryExecutionViewModel.cs`

### Adding New Categories

1. Add to `Models/QueryCategory.cs` enum
2. Create a new card in `Views/QueryMenuPage.xaml`
3. Add category method in `QueryExampleProvider.cs`
4. Update `QueryMenuViewModel.NavigateToCategoryCommand`

### Customizing the Schema

1. Modify entities in `Models/`
2. Update `DatabaseSeeder.cs` seeding logic
3. Adjust queries in `QueryExampleProvider.cs`
4. Update `SqlStatements.json` for raw SQL changes

## 🎯 Best Practices Demonstrated

✅ **Separation of Concerns**: Models, Services, ViewModels, Views clearly separated  
✅ **MVVM Pattern**: CommunityToolkit.Mvvm for clean, testable ViewModels  
✅ **Async/Await**: All database operations are async  
✅ **Error Handling**: Try-catch blocks with user-friendly error messages  
✅ **Resource Management**: `using` statements for DbContext disposal  
✅ **Performance Measurement**: Stopwatch timing for query execution  
✅ **Data Validation**: Foreign key relationships enforced via attributes  
✅ **Configuration-Driven**: Raw SQL loaded from external JSON file  

## 🆚 When to Use This Pattern

**Use QueryGalleryDemo patterns when you need:**
- Complex relational queries across multiple tables
- Performance measurement and optimization
- Mix of LINQ and raw SQL
- Many-to-many relationships
- Large datasets (thousands of records)
- Educational / demo applications

**Consider simpler patterns (DirectBindingDemo) when you need:**
- Simple CRUD operations
- Small datasets (<100 records)
- Single-table queries
- Rapid prototyping

## 📚 Additional Resources

- **SQLiteXM Documentation**: [GitHub Repository](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI)
- **Chinook Database**: Industry-standard sample schema for SQL learning
- **LINQ Query Syntax**: [Microsoft Docs](https://learn.microsoft.com/dotnet/csharp/linq/)
- **.NET MAUI**: [Official Documentation](https://learn.microsoft.com/dotnet/maui/)

## 🤝 Contributing

This is a sample application. For SQLiteXM library contributions, see the main repository.

---

**Built with SQLiteXM** • Demonstrating real-world database patterns for .NET MAUI applications
