# SQLiteXM for .NET MAUI

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/LICENSE.txt)   [![Documentation](https://img.shields.io/badge/Documentation-Guide-blue)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)   [![Release Notes](https://img.shields.io/badge/Release%20Notes-Changelog-green)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/releases) [![Build & Test (.NET 8 & 10)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/actions/workflows/dotnet-tests.yml/badge.svg)](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/actions/workflows/dotnet-tests.yml)


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
| Thread-safe, idempotent database startup, callable from any entry point | ✅ |
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
| Automated Test Coverage | 690+ tests |

---

## 📦 Installation

```bash
dotnet add package SQLiteXM
```

Or install via the [NuGet Package Manager](https://www.nuget.org/packages/SQLiteXM/)

---

## 📖 Documentation

See the **[Documentation Guide](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/README.md)** to find the right guide for where you are in your project.

---

## 🎮 Try SQLiteXM with the Query Gallery Demo

📥 **Want to see SQLiteXM in action?** Download the pre-built **[Query Gallery Demo](https://querygallerydemo.s3.us-east-1.amazonaws.com/QueryGalleryDemo_Windows.zip)** application. This is a ready-to-run MAUI Windows application that showcases SQLiteXM through working query examples organized into 13 categories. This demo runs completely self-contained. Simply extract the ZIP file on Windows and run `QueryGalleryDemo.exe` to explore.

**Features:**
- ✅ 125+ working query examples across 13 categories - LINQ, SQL, Bulk Insert, Benchmarks, and more.
- ✅ Live code execution with performance metrics
- ✅ Realistic music database (~25,000 records) 


🖼️ **[Preview Query Gallery Screenshot](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/Docs/images/query-gallery-categories.png)**

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

    public DateTime? LastLogin { get; set; }

    [Index]
    public string? Email { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Post : SxmEntity
{
    public string? Title { get; set; }

    public string? Content { get; set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; set; }
}
```

#### What's happening?

* `SxmEntity` marks the class as a database-mapped entity
* `[Table]` defines schema behavior for the entity
* `[Index]` declares a database index on the property
* `[ForeignKey]` defines relational constraints between entities
* A nullable property such as `DateTime? LastLogin` maps to a column that allows `NULL`

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
User user = new User
{
    Name = "Alice",
    Age = 0,
    Email = "alice@example.com",
    CreatedAt = DateTime.UtcNow
};

// Insert 'user' into the database
// 'Age' is initially set to 0; the record is updated below.
await user.SaveAsync();

await using (SxmTransaction ctx = new SxmTransaction())
{
    // LINQ — query the user to modify.
    User? existingUser = await ctx.GetTable<User>().FirstOrDefaultAsync(u => u.Name == "Alice");

    if (existingUser != null)
    {
        // Embedded SQL — execute SQL within the same transaction.
        // Values are passed as parameters rather than built into the SQL text.
        Dictionary<string, object?> loginParms = new Dictionary<string, object?>
        {
            ["LastLogin"] = DateTime.UtcNow,
            ["Name"]      = "Alice"
        };

        await ctx.RunStatementAsync(
            "UPDATE User SET LastLogin = @LastLogin WHERE Name = @Name", loginParms);

        // Entity DML — persist the change.
        // Uses the active transaction.
        existingUser.Age = 25;
        await existingUser.SaveAsync();
    }

} // <-- Automatically commits transaction on dispose if no errors occurred
```

#### What's happening?

* `SaveAsync()` inserts a new entity or updates an existing entity based on its primary key
* `SxmTransaction` starts a new transaction scope for database operations
* All database operations within the transaction participate in the same commit scope
* LINQ executes against the database and returns an entity instance, or `null` when no row matches
* `RunStatementAsync()` executes embedded SQL within the active transaction, binding values as
parameters rather than concatenating them into the SQL text
* Entity instances can be modified and persisted using the same `SaveAsync`


---

## 🧪 Testing

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

SQLiteXM is verified by **690+ automated tests** across 37 test suites covering LINQ queries, bulk operations, schema migrations, concurrency, multi-database routing, transactions, and data types—executed against both **.NET 8** and **.NET 10** (1,380+ total test executions).

📋 **[View the Full Test Coverage Breakdown on GitHub](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/SQLiteXM.Tests/README.md)**

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
  Nothing to configure. Verified against MAUI's default Release trimming on .NET 8 and .NET 10.

As with any MAUI project, run your Release build on a device or emulator before shipping — Debug builds do not 
trim, so that is the only place trimming behavior can be observed.

---

## 📄 License

MIT License - see [LICENSE](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/blob/master/LICENSE.txt) for details.

---

## 🙏 Acknowledgments

- SQLite provider via **[Microsoft.Data.Sqlite](https://github.com/dotnet/efcore)** (MIT License)
- LINQ support via **[LinqToDB](https://github.com/linq2db/linq2db)** (MIT License)

Inspired by **Entity Framework Core**, **Dapper**, and **SQLite-net**

---

**Made with ❤️ for the .NET MAUI community**
