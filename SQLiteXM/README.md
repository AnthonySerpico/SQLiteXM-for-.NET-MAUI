# SQLiteXM for .NET MAUI

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
| Entity-first architecture with built-in persistence methods | ✅ |
| AOT & IL Trimming Safe — no manual linker configuration needed | ✅ |
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
| Automated Test Coverage | 240 tests |

---

## 📖 Documentation

See the **[Documentation Guide](Docs/README.md)** to find the right guide for where you are in your project.

## 🎯 Quick Start (2 Minutes)

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

Once your entities and configuration are defined, initialize SQLiteXM in your application startup code.

```csharp
public static async Task InitializeDatabaseAsync()
{
    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

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

Call `InitializeDatabaseAsync()` once during application startup before performing database operations

---

### 4. Start Reading and Writing Data

Once initialization and entity registration are complete, SQLiteXM is ready for normal 
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

SQLiteXM includes a comprehensive test suite with **240 tests** covering real-world scenarios.

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
| Bulk LINQ Operations | 11 tests | ✅ 100% |
| Transactions | 7 tests | ✅ 100% |
| Multi-Database | 11 tests | ✅ 100% |
| Multi-Database LINQ | 18 tests | ✅ 100% |
| Multi-Database Performance | 10 tests | ✅ 100% |
| LINQ documentation tests| 43 tests | ✅ 100% |
| Drop Table | 22 tests | ✅ 100% |
| Column Rename | 10 tests | ✅ 100% |
| Shared Connections | 7 tests | ✅ 100% |
| Connection Workers | 7 tests | ✅ 100% |
| Submit Changes | 4 tests | ✅ 100% |
| Fail-Fast Validation | 5 tests | ✅ 100% |
| Mixed Operation Transactions | 13 tests | ✅ 100% |
| **Total** | **240 tests** | **✅ 100%** |

### Performance Benchmarks (from test suite)

| Operation | Time | Details |
|-----------|------|---------|
| 10,000 row insert (transacted) | 0.45s | Using explicit transaction |
| 50,000 row query | 14ms | With index |
| Complex LINQ (20K rows) | 12ms | Joins + filters |
| 100 concurrent writes | 1.2s | Thread-safe operations |

Benchmark results are environment-dependent and are provided as indicative results from the project's test suite rather than universal performance guarantees.

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

## 📦 Installation

```bash
# Via NuGet (coming soon)
dotnet add package SQLiteXM

# Or reference the project directly
```

---

## 🛠️ Requirements

- .NET MAUI Project

**Platforms:** iOS, Android, macOS, Windows (any .NET MAUI supported platform)

---

## 📄 License

MIT License - see [LICENSE](./LICENSE.txt) for details.

## 🙏 Acknowledgments

- Built on **Microsoft.Data.Sqlite**
- LINQ support via **LinqToDB**
- Inspired by **Entity Framework Core**, **Dapper**, and **SQLite-net**

---

**Made with ❤️ for the .NET MAUI community**
