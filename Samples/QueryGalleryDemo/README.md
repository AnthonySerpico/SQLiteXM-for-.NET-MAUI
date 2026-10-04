# QueryGalleryDemo - Comprehensive SQLiteXM Query Showcase

**A production-grade demonstration of SQLiteXM's querying capabilities using a Chinook-style music database**

## 🚀 Quick Start

**[📥 Download QueryGalleryDemo_Windows.zip](https://querygallerydemo.s3.us-east-1.amazonaws.com/QueryGalleryDemo_Windows.zip)**  
This demo runs completely self-contained. Simply extract the ZIP file on Windows and run `QueryGalleryDemo.exe`.

---

## 📋 Overview

QueryGalleryDemo is an advanced sample application that showcases the full range of SQLiteXM's
query capabilities through a categorized gallery of **112 working examples**. The app demonstrates:

- **Chinook-style Schema**: ~25,000 records across 11 related tables (Artists, Albums, Tracks, Genres, Playlists, Customers, Invoices, etc.)
- **LINQ Query Provider**: Type-safe, composable queries with full IntelliSense support
- **Named SQL**: Raw SQL statements loaded by name from a JSON configuration file
- **Embedded SQL**: Ad-hoc SQL strings executed directly inside a transaction context
- **Entity DML**: Insert / update / delete through tracked entity objects (DML = Data Manipulation Language)
- **Mixed Context**: All four styles above combined inside a single `SxmTransaction` unit of work
- **Benchmarks**: Side-by-side timing comparisons of competing data-access strategies
- **Compile-Time Code Display**: A source generator guarantees the code shown is the code executed
- **First-Run Seeding**: Automatic database population with realistic, related test data

### What makes this sample different

Most gallery apps store the example code as a string and the executable code separately, so the two
drift apart over time. QueryGalleryDemo solves this with a **Roslyn source generator** (a compiler
plug-in that writes extra C# during the build). Each example is a real class whose `RunAsync` method
is genuinely executed; the generator reads that method's body at compile time and turns it into the
text shown on screen. **Displayed code and executed code can never diverge.**

## 🎯 Learning Objectives

This sample teaches developers:

1. **Basic SQLite Operations**: SELECT, WHERE, ORDER BY, LIKE, DISTINCT
2. **Relationship Navigation**: INNER JOIN, LEFT JOIN, multi-table and self-referencing queries
3. **Aggregation Functions**: COUNT, SUM, AVG, MIN, MAX, GROUP BY
4. **Advanced LINQ**: Paging, subqueries, HAVING, conditional aggregates, UNION, top-N-per-group
5. **Named / Raw SQL**: Executing SQL defined in `SqlStatements.json` with full SQLite feature access
6. **Many-to-Many Relationships**: Junction table queries and bidirectional navigation
7. **Transaction Patterns**: Atomic operations, explicit commit, rollback, auto-rollback on exception
8. **Parameterized Queries**: SQL-injection-safe parameter binding
9. **Data Modification**: Insert, update, delete including generated-ID retrieval and cascade behaviour
10. **Mixed Context Work**: Sharing one connection and one transaction across LINQ, SQL and entity DML
11. **Performance Engineering**: Indexing, projection, pagination strategy, bulk insert, query caching

## 🏗️ Architecture

### Database Schema

The Chinook-style schema is seeded with the following volumes:

| Table | Rows Seeded | Notes |
|-------|-------------|-------|
| Genre | 25 | Fixed reference data |
| MediaType | 10 | Fixed reference data |
| Artist | 200 | 24 well-known names + 176 generated |
| Album | 400 | Each linked to an Artist |
| Track | 3,500 | Linked to Album, Genre, MediaType |
| Playlist | 50 | |
| PlaylistTrack | 10,000 | Many-to-many junction rows |
| Employee | 50 | Self-referencing manager hierarchy |
| Customer | 500 | Each assigned a support rep (Employee) |
| Invoice | 2,000 | Linked to Customer |
| InvoiceLine | 8,000 | Linked to Invoice and Track |
| **Total** | **~24,700** | Seeded in 11 progress-reported steps |

### Application Flow

```
WelcomePage (entity registration + seeding progress)
	↓
QueryMenuPage (12 category cards)
	↓
QueryCategoryPage (list of examples in the selected category)
	↓
QueryExecutionPage (code + explanation + results + metrics)
```

### Technology Stack

- **.NET 9 MAUI**: Cross-platform UI framework (`net9.0-android`, `net9.0-ios`, `net9.0-maccatalyst`, `net9.0-windows10.0.19041.0`)
- **SQLiteXM 1.3.0**: ORM with LINQ provider, named SQL and transaction support
- **CommunityToolkit.Mvvm 8.4.0**: MVVM infrastructure (`[ObservableProperty]`, `[RelayCommand]`)
- **QueryGalleryDemo.SourceGenerators**: Local Roslyn generator project referenced as an analyzer
- **Default trimming settings**: The project applies **no** trimming or AOT overrides, demonstrating that SQLiteXM needs no linker configuration

## 📂 Project Structure

```
QueryGalleryDemo/
├── Examples/                        # 97 attribute-declared, self-executing examples
│   ├── QueryExampleAttribute.cs    # [QueryExample] metadata + IQueryExampleRunner
│   ├── Basic/BasicExamples.cs                      (10)
│   ├── Relationships/RelationshipsExamples.cs       (8)
│   ├── Aggregations/AggregationsExamples.cs        (10)
│   ├── AdvancedLinq/AdvancedLinqExamples.cs        (11)
│   ├── Performance/PerformanceExamples.cs           (9)
│   ├── ManyToMany/ManyToManyExamples.cs             (8)
│   ├── Transactions/TransactionsExamples.cs         (6)
│   ├── ParameterizedQueries/ParameterizedExamples.cs(6)
│   ├── DataModification/DataModificationExamples.cs (8)
│   ├── Benchmarks/BenchmarksExamples.cs            (11)
│   └── Mixed/Mix1Example.cs … Mix10Example.cs      (10)
├── Models/                          # 11 Chinook entities + gallery metadata models
│   ├── Artist.cs, Album.cs, Track.cs, Genre.cs, MediaType.cs
│   ├── Playlist.cs, PlaylistTrack.cs               # Many-to-many junction
│   ├── Customer.cs, Employee.cs, Invoice.cs, InvoiceLine.cs
│   ├── QueryCategory.cs             # Enum of the 12 categories
│   ├── QueryExample.cs              # Example metadata + QueryType enum
│   └── QueryResult.cs               # Result wrapper model
├── Services/
│   ├── DatabaseSeeder.cs            # Entity registration + first-run data population
│   ├── QueryExampleProvider.cs      # Merges generated examples with the 15 raw-SQL examples
│   └── NavigationService.cs         # Page navigation helper
├── ViewModels/
│   ├── BaseViewModel.cs
│   ├── WelcomeViewModel.cs          # Registration + seeding progress
│   ├── QueryMenuViewModel.cs        # Category navigation
│   ├── QueryCategoryViewModel.cs    # Example list per category
│   └── QueryExecutionViewModel.cs   # Execution, timing, result formatting
├── Views/
│   ├── WelcomePage.xaml             # Startup / seeding UI
│   ├── QueryMenuPage.xaml           # Category grid menu
│   ├── QueryCategoryPage.xaml       # Example list for selected category
│   └── QueryExecutionPage.xaml      # Code + explanation + results + metrics
├── Converters/Converters.cs
└── Resources/Raw/SqlStatements.json # Chinook database registration + 15 named SQL statements
```

## 🔍 Query Categories

112 examples across 12 categories. Eleven categories are generated from `[QueryExample]` classes;
the **Raw SQL** category is built at runtime in `QueryExampleProvider` from `SqlStatements.json`.

| # | Category | Examples | Primary QueryType |
|---|----------|---------:|-------------------|
| 1 | Basic | 10 | Linq |
| 2 | Relationships | 8 | Linq |
| 3 | Aggregations | 10 | Linq |
| 4 | Advanced LINQ | 11 | Linq |
| 5 | Raw SQL | 15 | RawSql |
| 6 | Performance | 9 | Linq |
| 7 | Many-to-Many | 8 | Linq |
| 8 | Transactions | 6 | Entity_DML / Mixed |
| 9 | Parameterized Queries | 6 | Linq |
| 10 | Data Modification | 8 | Entity_DML / Mixed |
| 11 | Mixed Context | 10 | Mixed |
| 12 | Benchmarks | 11 | Mixed |
| | **Total** | **112** | |

### 1. Basic (10)
Get All Artists · Get All Genres · Filter Tracks by Genre · Find Artist by Name · Get Tracks by
Price Range · Top 10 Most Expensive Tracks · Tracks by Duration Range · Case-Insensitive Search ·
Tracks with Composer · Distinct Media Types

### 2. Relationships (8)
Tracks with Album Info · Albums with Artist Names · Complete Track Information · Customers with
Support Rep · Employee Hierarchy · Invoice with Customer Details · Track with All Related Entities ·
Handling Optional Columns (NULL Composer)

### 3. Aggregations (10)
Count Tracks by Genre · Album Count by Artist · Average Track Duration by Genre · Total Revenue by
Customer · Sales by Genre · Average Invoice Total · MIN/MAX Track Prices · Customer Purchase
Statistics · Tracks per Album Statistics · Revenue by Artist

### 4. Advanced LINQ (11)
Paging with Skip/Take · Multiple ORDER BY · Complex WHERE with Multiple Conditions · Subquery (IN
operator) · Subquery with Count · HAVING Clause · Conditional Aggregates · Top N per Group · Date
Range Queries · String Manipulation · UNION

### 5. Raw SQL (15)
Generated from the named statements in `Resources/Raw/SqlStatements.json`:
GetAllArtistsRaw · GetTracksWithArtistAlbum · GetTopSellingTracks · GetCustomerPurchaseStats ·
GetGenrePopularity · GetPlaylistDetails · GetArtistRevenue · GetExpensiveTracksByGenre ·
GetCustomersByCountryWithStats · GetMonthlyRevenueTrend · GetTopCustomersWithDetails ·
GetTracksWithPriceTier · GetAlbumCompletion · GetEmployeePerformance · GetPlaylistPopularity

These examples also surface the underlying SQL text on screen via `QueryExample.ActualSqlStatement`.

### 6. Performance (9)
Query 1000+ Tracks · Complex Multi-Table Join · Pagination with Skip/Take · Select Only Required
Columns · Early Filtering · Count Performance · Avoid N+1 Queries · Efficient Distinct · Foreign Key
Index Performance

### 7. Many-to-Many (8)
Tracks in a Playlist · Playlists Containing Track · Playlist Statistics · Tracks Shared Between
Playlists · Popular Tracks in Playlists · Playlists with Few Tracks · Add Track to Playlist ·
Playlist Overlap Analysis

### 8. Transactions (6)
Basic Transaction (Insert Invoice with Lines) · Transaction Rollback on Error · Batch Insert with
Transaction · Update Multiple Tables in Transaction · Complex Multi-Table Transaction · Transaction
vs No Transaction Performance

### 9. Parameterized Queries (6)
Search by Name Parameter · Price Range Filter · Date Range Query · Multiple Search Parameters ·
Optional Parameter Query · LIKE Pattern Search

### 10. Data Modification (8)
Insert New Track · Insert and Get Generated ID · Update Track Price · Conditional Update · Update
With Related Data · Delete Single Record · Conditional Delete · Delete with Related Records

### 11. Mixed Context (10)
LINQ + Named SQL (read-only) · LINQ read + Entity DML + Rollback · Entity DML + Embedded SQL verify ·
Named SQL feeds LINQ · LINQ read + Entity DML + Embedded UPDATE · Explicit Commit mid-context ·
Rollback discards mixed work · Auto-rollback on exception · Three-way read (LINQ + Named SQL +
Embedded SQL) · End-to-end unit of work

### 12. Benchmarks (11)
Insert: Single Transaction vs. Individual Inserts vs. BulkInsertAsync · Insert: Transaction Batch
Size Sweep · Read: LINQ vs. Named SQL vs. Embedded SQL · Filter: Indexed vs. Non-Indexed Column ·
Projection: Full Entity vs. Select Columns · Pagination: OFFSET vs. Keyset · Aggregation: In SQL vs.
In Memory · Existence: `Any()` vs. `Count() > 0` vs. `FirstOrDefault()` · Update: Entity Round-Trip
vs. Set-Based · Cold vs. Warm (query cache effect) · Insert at Scale: `SaveAsync` loop vs. LINQ
`BulkInsertAsync` vs. `SxmSql.BulkInsertAsync`

## 💡 Key Features Demonstrated

### Self-Describing Examples (`[QueryExample]` + source generator)

An example is a small class that carries its own metadata and implements `IQueryExampleRunner`:

```csharp
[QueryExample(
	id: "mix_1",
	name: "LINQ + Named SQL (read-only)",
	description: "Run a LINQ query and a named SQL statement inside the same SxmTransaction",
	category: QueryCategory.MixedContext,
	type: QueryType.Mixed,
	explanation: """
	**How It Works:**
	1. Open an SxmTransaction for the Chinook database
	2. Issue a LINQ query on ctx.GetTable<Genre>()
	3. Call ctx.RunStatementAsync with a named statement
	""")]
internal sealed class Mix1Example : IQueryExampleRunner
{
	public async Task<object> RunAsync()
	{
		await using var ctx = new SxmTransaction("Chinook");

		var genreNames = ctx.GetTable<Genre>()
							.OrderBy(g => g.Name)
							.Select(g => g.Name)
							.ToList();

		var popularity = await ctx.RunStatementAsync("GetGenrePopularity");

		return new[]
		{
			new { Step = "LINQ Genres",           Count = genreNames.Count },
			new { Step = "Named GenrePopularity", Count = popularity.Count }
		};
	}
}
```

At build time `QueryExampleGenerator` emits `GeneratedQueryExamples.g.cs` containing:

- `GeneratedQueryExamples.All` — an `IReadOnlyList<QueryExample>` of metadata, where `Code` is the
  verbatim, re-indented body of each `RunAsync` method
- `GeneratedQueryExamples.Runners` — an `IReadOnlyDictionary<string, Func<IQueryExampleRunner>>`
  factory registry keyed by example `Id`

`QueryExampleProvider` filters `All` by category (and appends the 15 raw-SQL examples), while
`QueryExecutionViewModel` resolves the matching runner from `Runners` to execute it.

### Entity Model and Index Attributes

All 11 entities derive from `SxmEntity` and target the shared `"Chinook"` database. Compound and
single-column indexes are declared declaratively:

```csharp
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[Index("AlbumId", "GenreId")]
[Index("GenreId", "UnitPrice")]
[Index("AlbumId", "TrackNumber")]
public class Track : SxmEntity
{
	[Required]
	[Index]
	public string Name { get; set; } = string.Empty;

	[ForeignKey(foreignTable: nameof(Album))]
	[Index]
	public long? AlbumId { get; set; }

	[ForeignKey(foreignTable: nameof(Genre))]
	[Index]
	public long? GenreId { get; set; }

	[Required]
	public int Milliseconds { get; set; }
}
```

The junction table adds a uniqueness guarantee and cascade deletes:

```csharp
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[UniqueIndex("PlaylistId", "TrackId")]
[Index("TrackId", "PlaylistId")]
public class PlaylistTrack : SxmEntity
{
	[Required]
	[ForeignKey(foreignTable: nameof(Playlist), OnDelete = ForeignKeyDeleteAction.Cascade)]
	[Index]
	public long PlaylistId { get; set; }

	[Required]
	[ForeignKey(foreignTable: nameof(Track), OnDelete = ForeignKeyDeleteAction.Cascade)]
	[Index]
	public long TrackId { get; set; }
}
```

### Entity Registration

Entities are registered once at startup, before any query runs:

```csharp
await SxmDatabase.RegisterEntitiesAsync(
	typeof(Genre), typeof(MediaType), typeof(Artist), typeof(Album), typeof(Track),
	typeof(Playlist), typeof(PlaylistTrack), typeof(Employee), typeof(Customer),
	typeof(Invoice), typeof(InvoiceLine));
```

`MauiProgram.cs` only configures MAUI services (fonts, logging, DI); SQLiteXM registration happens in
`DatabaseSeeder.RegisterEntitiesAsync()`, invoked by `WelcomeViewModel`.

### LINQ Query Provider

SQLiteXM translates LINQ expressions into SQLite SQL:

```csharp
await using var ctx = new SxmTransaction("Chinook");

var results = (from track in ctx.GetTable<Track>()
			   join album in ctx.GetTable<Album>() on track.AlbumId equals album.id
			   join artist in ctx.GetTable<Artist>() on album.ArtistId equals artist.id
			   where artist.Name.Contains("Rock")
			   orderby track.Name
			   select new { track.Name, Album = album.Title, Artist = artist.Name })
			  .Take(50)
			  .ToList();
```

### Named SQL from JSON

`Resources/Raw/SqlStatements.json` declares the `Chinook` database (as the default) and 15 named
SELECT statements. They are invoked by name, either standalone or inside a transaction context:

```csharp
await using var ctx = new SxmTransaction("Chinook");

var rows = await ctx.RunStatementAsync("GetTopSellingTracks");
```

Keeping SQL in JSON means it can be reviewed, edited and versioned without recompiling.

### Mixed-Style Unit of Work

The Mixed Context category is the headline feature: a single `SxmTransaction` hosts LINQ reads,
named SQL, embedded SQL strings and tracked entity DML on one connection, with one commit or
rollback. Read-only work never opens a SQLite transaction, so there is no unnecessary locking.
`await using` guarantees asynchronous disposal, and an unhandled exception rolls the work back
automatically.

### Execution UI

`QueryExecutionPage` displays, for the selected example:

- 📝 **Code pane** — the generated `FormattedCode` (identical to what runs)
- 🧾 **SQL pane** — `ActualSqlStatement`, shown only for raw-SQL examples
- 📘 **Explanation pane** — the educational text from the attribute
- ▶ **Run Query button** — executes the example's `RunAsync`
- 📊 **Results pane** — `FormattedResults` with `Records: {n}` and `Time: {n}ms`
- ⚠ **Error pane** — friendly message when an example throws

### First-Run Seeding

`DatabaseSeeder` decides whether to seed by counting existing `Track` and `PlaylistTrack` rows
(rather than relying on a preference flag), so a deleted or partially built database re-seeds
correctly. Seeding runs in 11 progress-reported steps and builds realistic hierarchical data
(Artist → Album → Track) plus many-to-many links (Playlist ↔ Track).

`DatabaseSeeder` is injected into `WelcomeViewModel` through dependency injection:

```csharp
await _databaseSeeder.RegisterEntitiesAsync();

var needsSeeding = await _databaseSeeder.CheckIfSeedingNeededAsync();
if (needsSeeding)
{
	await _databaseSeeder.SeedDatabaseAsync(update =>
	{
		// update.status is a message, update.progress is 0.0 - 1.0
	});
}
```

## 📖 Learning Path

**Recommended exploration order:**

1. **Basic** → WHERE, ORDER BY, LIKE, DISTINCT
2. **Relationships** → INNER JOIN, LEFT JOIN, self-referencing joins
3. **Aggregations** → COUNT, SUM, AVG, MIN, MAX, GROUP BY
4. **Advanced LINQ** → Paging, subqueries, HAVING, UNION, top-N-per-group
5. **Raw SQL** → Named statements and when to drop below LINQ
6. **Parameterized Queries** → Safe binding of user input
7. **Many-to-Many** → Junction tables and bidirectional navigation
8. **Data Modification** → Insert, update, delete and generated IDs
9. **Transactions** → Atomicity, explicit commit, rollback, auto-rollback
10. **Mixed Context** → Combining every style in one unit of work
11. **Performance** → Indexing, projection and pagination strategy
12. **Benchmarks** → Measure the trade-offs for yourself

## 🔧 Customization

### Adding a New Example

1. Create a class in the relevant `Examples/<Category>/` folder
2. Decorate it with `[QueryExample(id, name, description, category, type, explanation)]`
3. Implement `IQueryExampleRunner.RunAsync()` and return the data to display
4. Rebuild — the source generator registers it automatically, and the example appears in the app

Ids follow a `prefix_number` convention (`basic_11`, `mix_11`, …) and are sorted naturally, so
numeric suffixes order correctly without zero padding.

### Adding a New Raw SQL Example

1. Add the statement to the `select` array in `Resources/Raw/SqlStatements.json`
2. Add a matching `QueryExample` entry to `GetRawSqlExamples()` in `QueryExampleProvider.cs`,
   setting `ActualSqlStatement` from the loaded statement dictionary

### Adding a New Category

1. Add the value to the `QueryCategory` enum in `Models/QueryCategory.cs`
2. Add a card to `Views/QueryMenuPage.xaml`
3. Add a `Get…Examples()` method in `QueryExampleProvider.cs` that filters
   `GeneratedQueryExamples.All` by the new category
4. Wire it into `GetAllExamples()` and `GetExamplesByCategory()`

### Customizing the Schema

1. Modify entities in `Models/` (properties, indexes, foreign keys)
2. Update `DatabaseSeeder.cs` to populate the new fields
3. Update affected examples and `SqlStatements.json`
4. Delete the app's database (or uninstall the app) so seeding reruns

## 🎯 Best Practices Demonstrated

✅ **Code/display parity**: Source generator removes any chance of stale sample code
✅ **Separation of Concerns**: Examples, Models, Services, ViewModels, Views clearly separated
✅ **MVVM Pattern**: CommunityToolkit.Mvvm for clean, testable ViewModels
✅ **Async/Await**: All database operations are asynchronous
✅ **Resource Management**: `await using` for asynchronous context disposal
✅ **Error Handling**: Failures surface as readable messages, not crashes
✅ **Performance Measurement**: Stopwatch timing and dedicated benchmark comparisons
✅ **Declarative Schema**: Foreign keys, cascades, unique and compound indexes via attributes
✅ **Configuration-Driven SQL**: Named statements stored in external JSON
✅ **Transaction Safety**: Explicit commit/rollback plus auto-rollback on exception
✅ **SQL Injection Prevention**: Parameterized queries for all user input
✅ **Trim-Safe by Default**: No linker or AOT overrides required

## 🆚 When to Use This Pattern

**Use QueryGalleryDemo patterns when you need:**
- Complex relational queries across multiple tables
- A mix of LINQ, named SQL, embedded SQL and entity DML in one unit of work
- Measured performance optimization
- Many-to-many relationships
- Large datasets (thousands of records)
- Transaction management with commit/rollback
- Educational or demo applications

**Consider simpler patterns (DirectBindingDemo) when you need:**
- Simple CRUD operations
- Small datasets (<100 records)
- Single-table queries
- Rapid prototyping with no transaction requirements

## 📚 Additional Resources

- **SQLiteXM Documentation**: [GitHub Repository](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI)
- **Chinook Database**: Industry-standard sample schema for SQL learning
- **LINQ Query Syntax**: [Microsoft Docs](https://learn.microsoft.com/dotnet/csharp/linq/)
- **.NET MAUI**: [Official Documentation](https://learn.microsoft.com/dotnet/maui/)

## 🤝 Contributing

This is a sample application. For SQLiteXM library contributions, see the main repository.

---

**Built with SQLiteXM** • Demonstrating real-world database patterns for .NET MAUI applications
