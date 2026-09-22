# SQLiteXM for .NET MAUI

[![NuGet](https://img.shields.io/nuget/v/SQLiteXM.svg)](https://www.nuget.org/packages/SQLiteXM/)   [![Documentation](https://img.shields.io/badge/Documentation-Guide-blue)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)


SQLiteXM is a high-performance, entity-first ORM for SQLite designed specifically for .NET MAUI applications.

---

## Why SQLiteXM?

We asked ourselves: “What would a deliberately designed SQLite persistence layer for modern .NET/MAUI 
applications look like—one that supports SQL and LINQ while still providing convenient entities, schema 
evolution, transactions, mapping, persistence methods, and UI binding?”

The result is SQLiteXM.

| Capability | SQLiteXM |
|------------|----------|
| Designed specifically for .NET MAUI + SQLite | ✅ |
| AOT/IL Trimming Safe — works with MAUI's default Release trimming, no linker configuration needed ([details](#aot-trimming)) | ✅ |
| Mobile-optimized database initialization — idempotent, concurrency-safe startup from any entry point | ✅ |
| LINQ query support | ✅ |
| Raw SQL Support | ✅ |
| Automatic entity-to-table mapping | ✅ |
| Built-in schema evolution | ✅ |
| SQLite PRAGMAS are first-class initialization options | ✅ |
| Multiple SQLite Database Support | ✅ |
| Full Transaction Support — explicit and ambient transaction patterns | ✅ |
| Handles mobile lifecycle events — app suspend/resume | ✅ |
| Entities are MAUI binding-ready with INotifyPropertyChanged support | ✅ |
| Async-first design — supports non-blocking UI patterns | ✅ |
| Minimal configuration — no migration files, no DbContext setup | ✅ |
| Automated Test Coverage | 345 tests |

---

## 📖 Documentation

See the **[Documentation Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)** to find the right guide for where you are in your project.

---

## 🎮 Try SQLiteXM with the Query Gallery Demo

**Want to see SQLiteXM in action?** Download the pre-built **Query Gallery Demo** application:

This is a ready-to-run MAUI Windows application that showcases SQLiteXM through working query examples organized into 12 categories. 


**[📥 Download QueryGalleryDemo_Windows.zip](https://querygallerydemo.s3.us-east-1.amazonaws.com/QueryGalleryDemo_Windows.zip)**  
This demo runs completely self-contained. Simply extract the ZIP file on Windows and run `QueryGalleryDemo.exe` to explore LINQ queries, joins, aggregations, transactions, and more.

**Features:**
- ✅ 110+ working query examples across 12 categories - LINQ, SQL, Bulk Insert, and Benchmarks
- ✅ Live code execution with performance metrics
- ✅ Realistic music database (~25,000 records)

Want more details? See the [Query Gallery Demo](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/querygallery-demo.md)

---

## 🎯 SQLiteXM Quick Start (4 Minutes)

### 1. Define Your Entities

Create classes that inherit from `SxmEntity`:

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    public string? Name { get; set; }

    public int Age { get; set; }

    public DateTime CreatedAt { get; set; }

    [Index]
    public string? Email { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Post : SxmEntity
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    [ForeignKey(ForeignTable = nameof(User))]
    public long UserId { get; set; }
}
```

#### What's happening?

* `SxmEntity` marks the class as a database-mapped entity
* `[Table]` defines schema behavior for the entity
* `[Index]` declares a database index on the property
* `[ForeignKey]` defines relational constraints between entities

The schema is created when the database is initialized with `StartInitialization(...)` (step 3).

---

### 2. Create `SqlStatements.json` File

Place this file in `Resources/Raw` (Build Action: `MauiAsset`):

```json
{
  "databases": [
    {
      "database": "MyAppDatabase",
      "isDefault": true
    }
  ]
}
```

#### What's happening?

* Defines the SQLite database configuration for the application
* `database` specifies the SQLite database name
* `isDefault` assigns the default database for entities without an explicit database assignment
* The configuration is loaded during initialization to establish database connections and schema management

---

### 3. Initialize SQLiteXM

Once your entities and database configuration are defined, you're ready to initialize the database. 

```csharp
    // Create an array containing all the entities used by your application
    Type[] applicationEntities = new Type[]
    {
        typeof(User), 
        typeof(Post)
    };

    // Open the SqlStatements.json configuration file from the application package
    Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    // Start database initialization in the background
    // SQLiteXM takes ownership of 'sqlStatementsStream' and ensures proper disposal.
    SxmDatabase.StartInitialization(sqlStatementsStream, databaseOptions: null, applicationEntities);
```

Call `SxmDatabase.StartInitialization(...)` once during application startup. A good place is 
in MauiProgram.cs right after calling `MauiApp.CreateBuilder()`. `StartInitialization` returns immediately 
without blocking - initialization runs in the background.

---

### 4. Verifying Database Initialization Has Completed

Before the *first* use of the database, anywhere in your app, call:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

`EnsureReadyAsync` only needs to be called once. It waits for the database initialization task started by 
`StartInitialization` to complete, guaranteeing that the database is fully initialized and ready for use.

---

### 5. Start Reading and Writing Data

Once initialization is complete, SQLiteXM is ready for normal 
application use. You can create and save entities, query and modify data using LINQ or 
SQL, and begin using transactions.

```csharp
// 'User' inherits from SxmEntity and is automatically mapped to a database table
var user = new User
{
    Name = "Alice",
    Age = 0,
    Email = "alice@example.com",
    CreatedAt = DateTime.UtcNow
};

// Insert 'user' into the database
// 'Age' is initially set to 0; the record is updated below.
await user.SaveAsync();

await using (var ctx = new SxmTransaction())
{
    // LINQ — query the user to modify.
    var existingUser = ctx.GetTable<User>().FirstOrDefault(u => u.Name == "Alice");

    // Embedded SQL — execute SQL within the same transaction.
    await ctx.RunStatementAsync("UPDATE User SET LastLogin = CURRENT_TIMESTAMP WHERE Name == 'Alice'");

    // Entity DML — persist the change.
    // Uses the active transaction.
    existingUser.Age = 25;
    await existingUser.SaveAsync();

} // <-- Automatically commits transaction on dispose if no errors occurred
```

#### What's happening?

* `SaveAsync()` inserts a new entity or updates an existing entity based on its primary key
* `SxmTransaction` starts a new transaction scope for database operations
* All database operations within the transaction participate in the same commit scope
* LINQ executes against the database and returns an entity instance
* `RunStatementAsync()` executes embedded SQL within the active transaction
* Entity instances can be modified and persisted using the same `SaveAsync`


---

## 🧪 Testing

SQLiteXM includes a comprehensive test suite with **300+ tests** covering real-world scenarios.

### Performance Benchmarks (from test suite)

| Operation | Time | Details |
|-----------|------|---------|
| 10,000 row insert (transacted) | 0.45s | Using explicit transaction |
| 10,000 row bulk insert | 0.25s | Transacted bulk insert |
| 50,000 row query | 14ms | With index |
| Complex LINQ (20K rows) | 12ms | Joins + filters |
| 100 concurrent writes | 1.2s | Thread-safe operations |

Benchmark results are environment-dependent and are provided as indicative results from the project's test suite rather than universal performance guarantees.

### Test Coverage

The `SQLiteXM.Tests` project contains **345 tests**, run against both **.NET 8** and **.NET 9** (690 test executions), all passing.

| Area | What is covered | Test class | Tests |
|------|-----------------|------------|------:|
| **Schema & Initialization** | Entity registration, table creation, `[Table]` / `[Column]` options | `EntityInitializationTests` | 13 |
| | Property-to-column mapping and type handling | `EntityMappingTests` | 4 |
| | Fail-fast validation of invalid entity definitions | `FailFastTests` | 5 |
| | `StartInitialization` / `EnsureReadyAsync` idempotency, concurrency, and failure propagation | `InitializationPatternTests` | 11 |
| | Initialization of a 75-table × 50-column schema (timing) | `LargeSchemaInitializationBenchmarkTests` | 1 |
| **Schema Evolution** | Add column, index and trigger creation, foreign keys, system columns | `EntityMigrationTests` | 17 |
| | Single- and multi-step `[Rename]` column renames | `ColumnRenameTests` | 10 |
| | Drop column; add / remove / modify indexes and triggers; combined changes; unsupported changes; failed-migration state and retry | `SchemaEvolutionTests` | 24 |
| | `[DropTable]` / explicit table drops | `DropTableTests` | 22 |
| **Entity DML & Bulk Insert** | `SaveAsync` / `DeleteAsync` insert, update, delete | `EntityCrudTests` | 9 |
| | `SxmSql.BulkInsertAsync`, `SxmTransaction.BulkInsertAsync`, and LINQ `BulkInsertAsync` — id/synchId population, batch sizes, validation, rollback, unique-index violations | `BulkInsertTests` | 70 |
| **LINQ** | `GetTable<T>()`, `Where`, `OrderBy`, `Select`, `First`, `Count` | `LinqContextTests` | 7 |
| | Joins, grouping, aggregation, paging, async materialization | `AdvancedLinqTests` | 12 |
| | LINQ inside `SxmTransaction` — commit and rollback | `LinqTransactionTests` | 6 |
| | Bulk `Set(...).UpdateAsync()` and `DeleteAsync()` | `BulkLinqOperationsTests` | 12 |
| | Every example in [LINQ Queries](./Docs/linq-queries.md), executed as a test | `LinqQueryDocumentationTests` | 43 |
| **Transactions** | Commit, rollback, atomicity, ambient transaction enlistment and nesting | `TransactionTests` | 7 |
| | Mixed LINQ + entity DML + SQL in one transaction; fault behavior; recovery; multiple commits | `TransactionPatternTests` | 14 |
| | Mixed unit-of-work commit / rollback and faulted-context write skipping | `MixedUnitOfWorkTests` | 5 |
| **Multi-Database** | Entities and SQL across multiple named databases | `MultiDatabaseTests` | 11 |
| | LINQ across multiple named databases | `MultiDatabaseLinqTests` | 18 |
| | Cross-database performance and isolation | `MultiDatabasePerformanceTests` | 10 |
| **Connections & Concurrency** | Shared-connection locking, contention, and timeouts | `SharedConnectionTests` | 7 |
| | `RunWorkersAsync` concurrent connection workers | `ConnectionManagerWorkerTests` | 7 |
| | **Total** | | **345** |

---

## 📚 Sample Applications

SQLiteXM includes **three sample applications** to help you learn:

### 1. QueryGalleryDemo (Comprehensive) ⭐
An **interactive query explorer** with 100+ examples. 
**Features**: Syntax highlighting, runnable examples, execution timing, result visualization. 

<details>
<summary>📖 Query Gallery Details</summary>

- ✅ Basic Queries 10 - simple select, where, order by
- 🔗 Relationships 8 - join queries, navigation
- 📊 Aggregations 10 - count, sum, group by, avg
- 📦 Advanced LINQ 11 - complex queries, paging
- 🎯 Raw SQL 15 - direct SQL execution
- 📈 Performance 9 - large data sets, benchmarks
- 🔄 Many-to-Many 8 - junction tables, relationships
- 💾 Transactions 6 - atomic operations, rollback
- ⚡ Parameterized Queries 6 - prevent SQL injection
- 💾 Data modification 8 - insert, update, delete examples
</details>

📂 **[View Query Gallery Demo](Samples/QueryGalleryDemo/)**


### 2. RegistrationDemo (Simple)
Basic user registration showing entity definition, save/query, and data binding.

📂 **[View Registration Demo](Samples/RegistrationDemo/)**

### 3. DirectBindingDemo (Simple)
CollectionView binding with CRUD operations and UI updates.

📂 **[View Direct Binding Demo](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/tree/master/Samples/DirectBindingDemo)**


---

## 📦 Installation

```bash
dotnet add package SQLiteXM
```

Or install via the [NuGet Package Manager](https://www.nuget.org/packages/SQLiteXM/)

---

## 🛠️ Requirements

- .NET MAUI Project

**Platforms:** iOS, Android, macOS, Windows (any .NET MAUI supported platform)

---

<a id="aot-trimming"></a>
## ✂️ AOT and IL Trimming Safe — Out of the Box

.NET MAUI trims your app in Release builds, and AOT-compiles it on iOS and Mac Catalyst. Reflection-based ORMs 
often break here: entity properties are trimmed away and mappings fail at runtime — but only on a device, never 
in Debug. The usual fix is to sprinkle `[Preserve]` or `[DynamicDependency]` attributes on your entities, add a 
`TrimmerRootDescriptor.xml`, or turn trimming off. That work lands on you.

SQLiteXM removes that burden:

- **Zero trim/AOT analyzer warnings.** SQLiteXM is built with `IsTrimmable` and `IsAotCompatible` and compiles 
  cleanly under the .NET trim and AOT analyzers.
- **Your entities are preserved automatically.** Every SQLiteXM API that accepts an entity type is annotated with 
  `DynamicallyAccessedMembers`, so the trimmer keeps your entity classes intact without any attributes, linker 
  files, or project changes on your side.
- **Works with MAUI's defaults.** Create a MAUI project, add SQLiteXM, define your entities, build in Release. 
  Nothing to configure. Verified against MAUI's default Release trimming on .NET 8 and .NET 9.

As with any MAUI project, run your Release build on a device or emulator before shipping — Debug builds do not 
trim, so that is the only place trimming behavior can be observed.

---

## 📄 License

MIT License - see [LICENSE](./LICENSE.txt) for details.

---

## 🙏 Acknowledgments

- SQLite provider via **[Microsoft.Data.Sqlite](https://github.com/dotnet/efcore)** (MIT License)
- LINQ support via **[LinqToDB](https://github.com/linq2db/linq2db)** (MIT License)

Inspired by **Entity Framework Core**, **Dapper**, and **SQLite-net**

---

**Made with ❤️ for the .NET MAUI community**
