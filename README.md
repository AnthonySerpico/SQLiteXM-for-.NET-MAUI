# SQLiteXM for .NET MAUI

SQLiteXM is a high-performance, entity-first ORM for SQLite designed specifically for .NET MAUI and mobile applications.

Define entities, initialize once, and start querying with LINQ—without DbContext setup, migration folders, or EF Core complexity.

---

## Why SQLiteXM?

| Capability | SQLiteXM |
|------------|----------|
| Designed specifically for .NET MAUI + SQLite| ✅ |
| AOT & IL Trimming Safe | ✅ |
| Full LINQ Support | ✅ |
| Raw SQL Support | ✅ |
| Multi-Database Architecture | ✅ |
| Full Transaction Support - explicit and ambient transaction patterns | ✅ |
| Handles mobile lifecycle events - app suspend/resume | ✅ |
| Fine-grained SQLite PRAGMA control | ✅ |
| Direct binding support - entities are MAUI binding-ready | ✅ |
| Async-first design - supports non-blocking UI patterns | ✅ |
| Zero configuration - no migration files, no DbContext setup | ✅ |
| Automated Test Coverage | 184 tests |

---

### A Quick Look

```csharp
var user = new User
{
    Name = "Alice",
    Email = "alice@example.com"
};

await user.SaveAsync();

using var context = new SxmDbContext("MyApp");

var users = context.GetTable<User>()
    .Where(u => u.Name.StartsWith("A"))
    .OrderBy(u => u.Name)
    .ToList();
```

No DbContext.

No migration files.

No repository boilerplate.

Just entities, SQLite, and LINQ.

---

## ⚙️ AOT & Trimming Design

SQLiteXM is built for .NET MAUI apps targeting AOT and IL-trimmed Release builds. Manual linker configuration or per-model trimming annotations are not required.

Entity types are registered with full metadata preservation:
```csharp
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
```

This enables safe runtime reflection for property discovery, attribute mapping, and entity materialization in trimmed builds with no additional setup.

---

## 📦 Installation

```bash
# Via NuGet (coming soon)
dotnet add package SQLiteXM

# Or reference the project directly
```

---

## 🎯 Quick Start (2 Minutes)

> 🚀 Check out the comprehensive Getting Started guide for a detailed walkthrough!

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
* Schema is materialized when entities are registered via `RegisterEntitiesAsync`

---

### 2. Create `SqlStatements.json`

Place this file in `Resources/Raw/` (Build Action: `MauiAsset`):

```json
{
  "databases": [
    {
      "database": "MyApp",
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

<details>
<summary>📖 Example: Multi-Database Configuration</summary>

```csharp
// Define multiple databases in SqlStatements.json
{
  "databases": [
    {
      "database": "UserData",
      "isDefault": true
    },
    {
      "database": "AppCache",
      "isDefault": false
    }
  ]
}

// Create entities  - use 'Database' property to specify which database
[Table(IsColumnAttributeRequired = false, Database = "UserData")]
public class UserRecord : SxmEntity
{
    public string Name { get; set; }
    public string Email { get; set; }
}

[Table(IsColumnAttributeRequired = false, Database = "AppCache")]
public class CompletedItem : SxmEntity
{
    public string Task { get; set; }
    public DateTime CompletedAt { get; set; }
}
```

</details>


---

### 3. Initialize SQLiteXM

```csharp
public static async Task InitializeDatabaseAsync()
{
    using var stream =
        await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    await SxmDatabase.InitializeAsync(stream);

    await SxmDatabase.RegisterEntitiesAsync(
        typeof(User),
        typeof(Post));
}
```

#### What's happening?

* `OpenAppPackageFileAsync()` loads configuration from the application package
* `InitializeAsync()` creates and configures the database environment
* `RegisterEntitiesAsync()` registers entity types and creates or migrates tables as needed
* Call `InitializeDatabaseAsync()` once during application startup before performing database operations
<details>
<summary>📖 Example: Advanced SQLite Initialization</summary>

```csharp
public static async Task InitializeDatabaseAsync()
{
    // Configure advanced SQLite options
    var databaseOptions = new SxmDatabaseOptions()
    {
        // ✅ SQLite PRAGMA configuration
        ForeignKeys = true,
        JournalModeOption = SxmJournalMode.Wal,
        SynchronousModeOption = SxmSynchronousMode.Normal,
        BusyTimeout = 500,
        CacheSize = 57,
        WalAutoCheckpoint = 250,
        TempStore = SxmTempStore.Memory,

        // ✅ WAL checkpoint control
        CheckPointConnection = CheckPointConnection.MaxSize,
        CheckPointWalMaxSize = 32,

        // ✅ Connection pooling
        DefaultTimeout = 5,
        EnableConnectionPooling = true,

        // ✅ Logging control
        EnableLogging = true,

        // ✅ Database path customization
        DatabaseFolderOverride = FileSystem.AppDataDirectory
    };

    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    if (stream != null)
    {
        await SxmDatabase.InitializeAsync(stream, databaseOptions);

        await SxmDatabase.RegisterEntitiesAsync(
            typeof(User),
            typeof(Post));
    }
}
```

</details>


---

### 4. Use Your Entities

```csharp
var user = new User
{
    Name = "Alice",
    Age = 30,
    Email = "alice@example.com",
    CreatedAt = DateTime.UtcNow
};

await user.SaveAsync();

using var context = new SxmDbContext("MyApp");

var users = context.GetTable<User>()
    .Where(u => u.Age > 25)
    .OrderBy(u => u.Name)
    .ToList();

user.Age = 31;

await user.SaveAsync();

await user.DeleteAsync();
```

#### What's happening?

* `SaveAsync()` inserts or updates entities as needed
* `SxmDbContext` provides LINQ access to registered entities
* LINQ queries are translated into SQLite queries through LinqToDB
* Entity instances can be modified and persisted using the same API

---

### 5. Transactions

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

var user = new User
{
    Name = "Bob"
};

await user.SaveAsync(transaction);

var post = new Post
{
    Title = "Hello",
    UserId = user.id
};

await post.SaveAsync(transaction);

await transaction.CommitTransactionAsync();
```

#### What's happening?

* `SxmSqlTransaction` creates an explicit SQLite transaction
* All operations participate in the same commit scope
* `CommitTransactionAsync()` commits changes immediately
* Transactions can also automatically commit on disposal when no errors occur

```

💡 Want more examples? Explore the Query Gallery Demo with 90+ interactive examples, or dive into the full documentation.
```

---

## 🧪 Testing

SQLiteXM includes a comprehensive test suite with **184 tests** (183 passing, 1 intentionally skipped) covering real-world scenarios.

### Test Coverage

| Category | Tests | Status |
|----------|-------|--------|
| Entity CRUD | 11 tests | ✅ 100% |
| Entity Initialization | 13 tests | ✅ 100% |
| Entity Migration | 18 tests | ✅ 100% |
| Entity Mapping | 4 tests | ✅ 100% |
| LINQ Queries | 7 tests | ✅ 100% |
| Advanced LINQ | 12 tests | ✅ 100% |
| LINQ Transactions | 6 tests | ✅ 100% |
| Bulk LINQ Operations | 11 tests (1 skipped) | ✅ 100%* |
| Transactions | 7 tests | ✅ 100% |
| Multi-Database | 11 tests | ✅ 100% |
| Multi-Database LINQ | 18 tests | ✅ 100% |
| Multi-Database Performance | 10 tests | ✅ 100% |
| Drop Table | 22 tests | ✅ 100% |
| Column Rename | 10 tests | ✅ 100% |
| Shared Connections | 7 tests | ✅ 100% |
| Connection Workers | 7 tests | ✅ 100% |
| Submit Changes | 4 tests | ✅ 100% |
| Fail-Fast Validation | 5 tests | ✅ 100% |
| **Total** | **184 tests** | **✅ 100%** |

**\*Note:** 1 test intentionally skipped due to a known LINQ limitation (documented in test comments).

### Performance Benchmarks (from test suite)

| Operation | Time | Details |
|-----------|------|---------|
| 10,000 row insert (transacted) | 0.45s | Using explicit transaction |
| 50,000 row query | 14ms | With index |
| Complex LINQ (20K rows) | 12ms | Joins + filters |
| 100 concurrent writes | 1.2s | Thread-safe operations |

### Running Tests

```bash
cd SQLiteXM.Tests
dotnet test
```

For testing your own code with SQLiteXM, see the **[Testing Guide](docs/Advanced.md#testing-your-app)**.

---

## 📚 Sample Applications

SQLiteXM includes **three sample applications** to help you learn:

### 1. QueryGalleryDemo (Comprehensive) ⭐
An **interactive query explorer** with 90+ examples. 
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

📂 **[View Direct Binding Demo](Samples/DirectBindingDemo/)**


---


## 🏗️ Architecture

SQLiteXM uses a **static-first** design optimized for mobile:

- **SxmEntity** - Base class with reflection-driven schema creation
- **SxmInit** - One-time initialization coordinator
- **SxmConnection** - Lease-based connection manager with reentrancy
- **SxmSqlTransaction** - Transaction abstraction with explicit control
- **SxmDbContext** - LinqToDB integration for LINQ queries

**Learn more** in the **[Architecture Guide](docs/Advanced.md)** (coming soon).

---

## 📖 Documentation

### 🚀 Getting Started
- **[Getting Started Guide](docs/GettingStarted.md)** ⭐ **Start here!**
- **[Quick Start (2 minutes)](docs/GettingStarted.md#5-minute-quick-start)**
- **[Sample Apps](Samples/)**
- **[Query Gallery Demo](Samples/QueryGalleryDemo/)** - Interactive examples

### 📚 Core Guides
- **[Defining Your Data](docs/DefiningYourData.md)** - Entities, attributes, indexes, migrations
- **[Querying Data](docs/QueryingData.md)** - LINQ, joins, aggregations
- **[Saving Data](docs/SavingData.md)** - Insert, update, delete, batching
- **[Transactions](docs/Transactions.md)** - Transaction patterns and best practices

### 🎯 Advanced Topics
- **[Multi-Database Support](docs/MultiDatabase.md)** - Working with multiple databases
- **[Performance Guide](docs/Performance.md)** - Optimization tips and benchmarks
- **[Advanced Topics](docs/Advanced.md)** - Thread safety, testing, migrations, troubleshooting

### 📖 Complete Index
See **[docs/README.md](docs/README.md)** for the full documentation index.

---

## 🛠️ Requirements

- .NET 8.0 or later
- SQLite 3.x (via Microsoft.Data.Sqlite)
- LinqToDB 5.x (for LINQ support)

**Platforms:** iOS, Android, macOS, Windows (any .NET MAUI supported platform)

---

## 🤝 Contributing

Contributions welcome! Please read [CONTRIBUTING.md](./CONTRIBUTING.md) first.

## 📄 License

MIT License - see [LICENSE](./LICENSE) for details.

## 🙏 Acknowledgments

- Built on [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite)
- LINQ support via [LinqToDB](https://linq2db.github.io/)
- Inspired by Entity Framework Core, Dapper, and SQLite-net

## 📞 Support

- 🐛 [Report Issues](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/issues)
- 💬 [Discussions](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/discussions)
- 📧 Email: [Your contact]

---

**Made with ❤️ for the .NET MAUI community**
