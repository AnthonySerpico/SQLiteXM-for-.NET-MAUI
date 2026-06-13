# SQLiteXM for .NET MAUI

SQLiteXM is a high-performance, entity-first ORM for SQLite designed specifically for .NET MAUI and mobile applications.

Define entities, initialize once, and start querying with LINQ—without DbContext setup, migration folders, or EF Core complexity.

Built for mobile-first applications with AOT support, offline-first workflows, and direct MAUI data binding.

---

## ✨ What Makes SQLiteXM Different

### 🚀 Zero-Friction Setup
Entity-first design with no DbContext, no migrations, and no configuration ceremony. Define your models and start immediately.

### 📲 MAUI-Native Architecture
Built specifically for .NET MAUI apps with direct data binding, lifecycle awareness, and compatibility with AOT compilation and IL trimming in Release builds.

### 📝 Dual Query Power
Use full LINQ or raw SQL with a structured `statements.json` catalog. Choose the right tool per query without framework friction.

### 🗄️ Multi-Database Support
First-class support for multiple SQLite databases in a single app for clean data separation, caching layers, or tenant isolation.

### ⚙️ Full SQLite Control
Fine-grained control over SQLite behavior including PRAGMA configuration, transactions, indexes, triggers, and performance tuning.

### 📱 Mobile-First Performance Model
Designed for offline-first apps with fast startup, low memory usage, connection reuse, and async-safe execution patterns.

---

## 🔧 Core ORM Capabilities

Type-Safe Mapping — Strong CLR-to-SQLite type mapping with custom overrides

Attribute-Driven Schema — Intuitive attributes for tables, columns, indexes, foreign keys, triggers, and column rename

Full Transaction Support — Explicit and ambient transaction patterns with multi-database coordination

Async-First API — Modern async/await patterns throughout

---

## ⚙️ AOT & Trimming Design

SQLiteXM is designed for .NET MAUI applications targeting Release builds with AOT and trimming enabled.

Entity types registered with SQLiteXM do not require manual linker configuration or per-model trimming annotations.

Entity registration is explicitly annotated to preserve all metadata for runtime reflection:

```csharp
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
```
&nbsp;
## 📦 Installation

```bash
# Via NuGet (coming soon)
dotnet add package SQLiteXM

# Or reference the project directly
```

&nbsp;

# 🎯 Quick Start (2 Minutes)

## 1. Define Your Entities
> **🚀 Check out the **[comprehensive Getting Started guide](docs/GettingStarted.md)** for a detailed walkthrough!

Create classes that inherit from `SxmEntity`:

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; } // Stored as Unix milliseconds

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

### What's happening?

- `SxmEntity` marks the class as a database-mapped entity
- `[Table]` defines schema behavior for the entity
- `[Index]` declares a database index on the property
- `[ForeignKey]` defines relational constraints between entities
- Schema is materialized when entities are registered via `RegisterEntitiesAsync`

---

## 2. Create `SqlStatements.json`

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

### What's happening?

- Defines the SQLite database configuration for the application
- `database` specifies the SQLite file name (`MyApp`)
- `isDefault` assigns the default database for entities without an explicit database assignment
- This configuration is loaded during initialization to establish database connections and schema management

---

## 3. Initialize SQLiteXM

In `App.xaml.cs`:

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

### What's happening?

- `OpenAppPackageFileAsync()` loads the configuration from the application package
- `InitializeAsync()` creates and configures the database environment
- `RegisterEntitiesAsync()` registers entity types and creates or migrates tables as needed
- Call InitializeDatabaseAsync() once during application startup before performing database operations.

---

## 4. Use Your Entities

```csharp
var user = new User
{
    Name = "Alice",
    Age = 30,
    Email = "alice@example.com",
    CreatedAt = DateTime.UtcNow
};

await user.SaveAsync();

using var context = new SxmLinqDbContext("MyApp");

var users = context.GetTable<User>()
    .Where(u => u.Age > 25)
    .OrderBy(u => u.Name)
    .ToList();

user.Age = 31;
await user.SaveAsync();

await user.DeleteAsync();
```

### What's happening?

- `SaveAsync()` inserts or updates the entity as needed
- `SxmLinqDbContext` provides LINQ access to registered entities
- LINQ queries are translated into SQLite queries through LinqToDB
- Entity instances can be updated and persisted using the same API

---

## 5. Transactions

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

var user = new User { Name = "Bob" };
await user.SaveAsync(transaction);

var post = new Post
{
    Title = "Hello",
    UserId = user.id
};

await post.SaveAsync(transaction);

await transaction.CommitTransactionAsync();
```

### What's happening?

- `SxmSqlTransaction` creates an explicit SQLite transaction
- All operations executed with the transaction participate in the same commit scope
- `CommitTransactionAsync()` commits changes immediately
- If no errors occur, transactions can also automatically commit when disposed
- 
**💡 Want more examples?** Explore the **[Query Gallery Demo](Samples/QueryGalleryDemo/)** with 50+ interactive examples, or dive into the **[full documentation](docs/)**.

---

&nbsp;
## 🎨 What Makes SQLiteXM Special?

### 1. 📚 Extensive Documentation & Learning Tools

SQLiteXM comes with:

- **[Interactive Query Gallery](Samples/QueryGalleryDemo/)** - 90+ runnable LINQ and SQL examples with explanations
- **[Comprehensive guides](docs/)** - Detailed documentation for every feature
- **[3 sample apps](Samples/)** - From simple to advanced
- **[182 tests](SQLiteXM.Tests/)** - Real-world patterns you can learn from

**We believe good docs matter as much as good code.**

**🚀 Zero Configuration**
- No `DbContext` setup required
- Tables created from your entity classes during initialization
- No migration files to manage

**⚡ Mobile-Optimized**
- Static caching for fast startup
- Connection pooling for best performance
- Async-first design with proper `ConfigureAwait(false)`
- **AOT/Trimmer optimized** for iOS deployment
- **Mobile lifecycle management** (handles app suspend/resume)
- **Direct binding support** (entities are MAUI binding-ready)
- **Offline-first architecture** for mobile scenarios
- Low memory footprint with static caching

**🔗 Full LINQ Support**
- Write queries like `context.GetTable<User>().Where(u => u.Age > 25)`
- No need to drop down to raw SQL
- Powered by LinqToDB
- Full query capabilities (joins, aggregations, subqueries)

**🎯 Full SQL Support**
- **Full DML support** (INSERT, UPDATE, DELETE, SELECT)
- **SQL statements catalog** (`statements.json` for organized SQL management)
- **Embedded SQL in code** (when you need full control)
- **Named & positional parameters** (prevents SQL injection)
- **Mix raw SQL + LINQ** (use the best tool for each query)
- **Trigger support** via `[CreateTrigger]` attribute

**🔧 SQLite Optimized**
- **Fine-grained SQLite PRAGMA control** (WAL mode, foreign keys, synchronous mode)
- **Connection pooling configuration** (timeouts, pool size)
- **WAL checkpoint management** (automatic or manual control)
- **Performance tuning** (cache size, busy timeout, temp store)
- **Logging control** for debugging and diagnostics
- **Path customization** for database location

<details>
<summary>📖 Example: Advanced SQLite Configuration</summary>

```csharp
private async Task InitializeSQLiteXmAsync()
{
    // Load the SqlStatements.json file from Resources/Raw
    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

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

    if (stream != null)
    {
        await SxmDatabase.InitializeAsync(stream, databaseOptions);

        // Register entity types
        await SxmDatabase.RegisterEntitiesAsync(typeof(TodoItem),
                                                typeof(User));
    }
}
```

</details>

**🎯 Clean API**
- Inherit from `SxmEntity` and you're done
- Intuitive attributes: `[Index]`, `[ForeignKey]`, etc.
- Explicit transaction support when you need it

**🗄️ Multi-Database Support**
- Define and work with multiple SQLite databases in a single application
- Data isolation per database (user data, app cache, sync data)
- Database-specific entity registration
- Independent transaction contexts
- Perfect for tenant separation or domain organization

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

// Register entities
    await RegisterEntitiesAsync(
              typeof(UserRecord),    // Table created in UserData
              typeof(CompletedItem)  // Table created in AppCache
);

// Query from specific database
using var userContext = new SxmLinqDbContext("UserData");
var users = userContext.GetTable<UserRecord>().ToList();

using var cacheContext = new SxmLinqDbContext("AppCache");
var completedItems = cacheContext.GetTable<CompletedItem>().ToList();
```

</details>

### 🆚 How Does It Compare?

| Feature | SQLiteXM | EF Core | SQLite-net | Dapper |
|---------|----------|---------|------------|--------|
| **MAUI-optimized** | ✅ | ⚠️ General-purpose | ✅ | ⚠️ General-purpose |
| **Zero config** | ✅ | ❌ DbContext required | ✅ | ❌ Manual SQL |
| **Full LINQ** | ✅ | ✅ | ⚠️ Basic | ❌ |
| **Raw SQL + LINQ** | ✅ Both | ✅ Both | ⚠️ LINQ only | ⚠️ SQL only |
| **SQLite optimization** | ✅ Extensive | ⚠️ Basic | ⚠️ Basic | ⚠️ Basic |
| **Auto-migration** | ✅ | ✅ | ❌ | ❌ |
| **Async-first** | ✅ | ✅ | ⚠️ Partial | ✅ |
| **AOT/Trimmer support** | ✅ Designed for MAUI trimming| ⚠️ Limited | ✅ | ✅ |
| **Documentation** | ✅ Excellent | ✅ Excellent | ⚠️ Basic | ⚠️ Minimal |
| **Interactive examples** | ✅ Query Gallery | ❌ | ❌ | ❌ |
| **Learning curve** | Easy | Steep | Easy | Medium |

**Perfect for:**
- .NET MAUI mobile apps (iOS, Android, macOS, Windows)
- Offline-first applications
- Apps that need local data persistence
- Developers who want EF-style LINQ without the complexity

---

## 📚 Sample Applications

SQLiteXM includes **three sample applications** to help you learn:

### 1. RegistrationDemo (Simple)
Basic user registration showing entity definition, save/query, and data binding.

📂 [View Sample](Samples/RegistrationDemo/)

### 2. DirectBindingDemo (Simple)
CollectionView binding with CRUD operations and UI updates.

📂 [View Sample](Samples/DirectBindingDemo/)

### 3. QueryGalleryDemo (Comprehensive) ⭐

An **interactive query explorer** with 50+ examples:
- ✅ Basic Queries
- 🔗 Joins (Inner, Left, Cross)
- 📊 Aggregations
- 📦 Grouping
- 🎯 Subqueries
- 🔄 Many-to-Many
- 💾 Transactions
- ⚡ Bulk Operations

**Features**: Syntax highlighting, runnable examples, execution timing, result visualization.

📂 [View Sample](Samples/QueryGalleryDemo/) | 📖 [Read the docs](Samples/QueryGalleryDemo/README.md)

---

## 🧪 Testing

SQLiteXM includes a comprehensive test suite with **182 tests** covering real-world scenarios.

### Test Coverage

| Category | Tests | Status |
|----------|-------|--------|
| Entity CRUD | 11 tests | ✅ 100% |
| Transactions | 5 tests | ✅ 100% |
| LINQ Queries | 18 tests | ✅ 100% |
| Multi-Database | 11 tests | ✅ 100% |
| Migrations | 15 tests | ✅ 100% |
| Performance | 8 tests | ✅ 100% |
| Bulk Operations | 9 tests | ✅ 99%* |
| Concurrency | 8 tests | ✅ 100% |
| **Total** | **182 tests** | **✅ 99.5%** |

**\*Note:** 1 test intentionally skipped due to a known LinqToDB provider limitation (documented in test comments).

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

## 📊 Performance Highlights

### Real-World Benchmarks

**Transaction optimization matters:**
- ❌ 10,000 inserts without transaction: ~27 seconds
- ✅ 10,000 inserts with transaction: ~0.45 seconds
- **🚀 60x faster with proper transaction usage!**

**Query performance:**
- 50,000 row scan with index: ~14ms
- Complex LINQ with joins (20K rows): ~12ms
- Concurrent operations (100 writes): ~1.2s

**Learn more** in the **[Performance Guide](docs/Performance.md)**.

### Comparison Table

| Operation | SQLiteXM | EF Core | SQLite-net | Dapper |
|-----------|----------|---------|------------|--------|
| Entity Create | ⚡ Fast | 🐌 Slow | ⚡ Fast | ⚡ Fast |
| CRUD | ⚡ Fast | 🔶 Medium | ⚡ Fast | ⚡ Fast |
| LINQ Queries | ✅ Full support | ✅ Full support | ⚠️ Basic | ❌ None |
| Async Support | ✅ Native | ✅ Native | ⚠️ Partial | ✅ Native |
| Auto-Migration | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| Zero Config | ✅ Yes | ❌ DbContext | ✅ Yes | ❌ Manual SQL |
| Documentation | ✅ Excellent | ✅ Excellent | ⚠️ Basic | ⚠️ Minimal |

---

## 🏗️ Architecture

SQLiteXM uses a **static-first** design optimized for mobile:

- **SxmEntity** - Base class with reflection-driven schema creation
- **SxmInit** - One-time initialization coordinator
- **SxmConnection** - Lease-based connection manager with reentrancy
- **SxmSqlTransaction** - Transaction abstraction with explicit control
- **SxmLinqDbContext** - LinqToDB integration for LINQ queries

**Learn more** in the **[Architecture Guide](docs/Advanced.md)** (coming soon).

---

## 🆚 Comparison to Other ORMs

### vs. Entity Framework Core
- ✅ **Lighter weight** - No provider abstraction overhead
- ✅ **Faster startup** - No DbContext compilation
- ✅ **Mobile-optimized** - Static caching for entity metadata
- ✅ **Simpler setup** - No migrations folder or DbContext configuration
- ❌ **SQLite-only** - Not a general-purpose ORM

### vs. SQLite-net
- ✅ **Better async** - Proper `async`/`await` throughout
- ✅ **Full LINQ** - Complete query capabilities via LinqToDB
- ✅ **Explicit transactions** - Better control over transaction boundaries
- ✅ **More features** - Triggers, foreign keys, complex types
- ✅ **Better docs** - Interactive Query Gallery + comprehensive guides

### vs. Dapper
- ✅ **No manual SQL** - Entity-driven schema
- ✅ **Auto-migration** - Schema changes handled automatically
- ✅ **Type-safe** - Compile-time checking
- ✅ **LINQ queries** - No string concatenation
- ❌ **Less control** - Dapper gives you raw SQL access

**Learn more** in the **[Migration Guide](docs/Advanced.md#migration-from-other-orms)** (coming soon).

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
