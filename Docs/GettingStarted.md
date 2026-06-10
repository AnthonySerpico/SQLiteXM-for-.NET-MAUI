# Getting Started with SQLiteXM

Welcome to SQLiteXM! This guide will help you go from zero to a working .NET MAUI app with SQLite persistence in just a few minutes.

## Table of Contents
- [Why SQLiteXM?](#why-sqlitexm)
- [Installation](#installation)
- [5-Minute Quick Start](#5-minute-quick-start)
- [Sample Apps](#sample-apps)
- [What's Next?](#whats-next)

---

## Why SQLiteXM?

SQLiteXM is a **high-performance, entity-first ORM** designed specifically for .NET MAUI and mobile applications. If you've used Entity Framework Core or SQLite-net, you'll feel right at home—but with benefits tailored for mobile:

### ✨ Key Benefits

**🚀 Zero Configuration**
- No `DbContext` setup required
- Tables auto-create from your entity classes
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
private async Task InitializeSQLiteXMAsync()
{
    // Load the SqlStatements.json file from Resources/Raw
    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    // Configure advanced SQLite options
    SxmDatabaseOptions databaseOptions = new SxmDatabaseOptions()
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
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(TodoItem),
            typeof(User)
        );
    }
}
```

</details>

**🎯 Clean API**
- Inherit from `SxmEntity` and you're done
- Intuitive attributes: `[CreateIndex]`, `[CreateForeignKey]`, etc.
- Explicit transaction support when you need it

**🔒 Production-Ready**
- 182 comprehensive tests covering real-world scenarios
- Thread-safe concurrent operations
- Multi-database support for advanced use cases

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
| **AOT/Trimmer support** | ✅ | ⚠️ Limited | ✅ | ✅ |
| **Documentation** | ✅ Excellent | ✅ Excellent | ⚠️ Basic | ⚠️ Minimal |
| **Interactive examples** | ✅ Query Gallery | ❌ | ❌ | ❌ |
| **Learning curve** | Easy | Steep | Easy | Medium |

**Perfect for:**
- .NET MAUI mobile apps (iOS, Android, macOS, Windows)
- Offline-first applications
- Apps that need local data persistence
- Developers who want EF-style LINQ without the complexity

---

## Installation

### Via NuGet (Recommended)

```bash
# Install the SQLiteXM package
dotnet add package SQLiteXM
```

Or add it via Visual Studio:
1. Right-click your project → **Manage NuGet Packages**
2. Search for **SQLiteXM**
3. Click **Install**

### Manual Reference

If you're working with the source code:

```xml
<ProjectReference Include="..\SQLiteXMCL\SQLiteXM.csproj" />
```

### Dependencies

SQLiteXM automatically includes:
- **Microsoft.Data.Sqlite** (SQLite engine)
- **LinqToDB** (LINQ query support)

No additional packages needed!

---

## 5-Minute Quick Start

Let's build a simple todo app to demonstrate SQLiteXM's core features.

### Step 1: Define Your Entity

Create a new class that inherits from `SxmEntity`:

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class TodoItem : SxmEntity
{
	public string? Title { get; set; }
	public string? Description { get; set; }
	public bool IsCompleted { get; set; }
	public DateTime CreatedAt { get; set; }

	[CreateIndex]
	public DateTime DueDate { get; set; }
}
```

**What just happened?**
- `SxmEntity` gives you automatic `id` and `SynchId` (GUID) properties
- `[Table(IsColumnAttributeRequired = false)]` means all properties are automatically persisted
- `[CreateIndex]` on `DueDate` creates an index for fast queries
- The database table will be created automatically during initialization

### Step 2: Initialize SQLiteXM

In your `App.xaml.cs`, add initialization:

```csharp
public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Initialize SQLiteXM once at app startup
		InitializeSQLiteXMAsync().GetAwaiter().GetResult();

		MainPage = new AppShell();
	}

	private async Task InitializeSQLiteXMAsync()
	{
		try
		{
			// Load the SqlStatements.json file from Resources/Raw
			using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

			if (stream != null)
			{
				await SxmDatabase.InitializeAsync(stream);

				// Register entity types for schema creation/migration
				await SxmDatabase.RegisterEntitiesAsync(typeof(TodoItem));
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing SQLiteXM: {ex.Message}");
		}
	}
}
```

**Create `SqlStatements.json`** in your project under **Resources/Raw/** folder (set **Build Action: MauiAsset**):

```json
{
  "database": "TodoApp",
  "isDefault": true,
  "version": 1
}
```

**What's happening?**
- `FileSystem.OpenAppPackageFileAsync()` loads the JSON from your app package (works on all platforms)
- `SxmDatabase.InitializeAsync(stream)` parses the database configuration
- `RegisterEntitiesAsync()` registers your entity types for schema creation and migration
- The database file is created automatically in the platform-specific app data folder
- Tables are created by `RegisterEntitiesAsync()`

**💡 Pro Tip:** For multiple entities, register them all at once:
```csharp
await SxmDatabase.RegisterEntitiesAsync(
	typeof(TodoItem),
	typeof(User),
	typeof(Category)
);
```

### Step 3: Create (Insert) Data

```csharp
// Create a new todo item
var todo = new TodoItem
{
	Title = "Buy groceries",
	Description = "Milk, eggs, bread",
	IsCompleted = false,
	CreatedAt = DateTime.UtcNow,
	DueDate = DateTime.UtcNow.AddDays(1)
};

// Save it!
await todo.SaveAsync();

// The ID is now populated
Console.WriteLine($"Created todo with ID: {todo.id}");
```

### Step 4: Read (Query) Data

```csharp
// Using LINQ queries
using var context = new SxmLinqDbContext("TodoApp");

// Get all incomplete todos
var incompleteTodos = context.GetTable<TodoItem>()
	.Where(t => !t.IsCompleted)
	.OrderBy(t => t.DueDate)
	.ToList();

// Get todos due today
var today = DateTime.UtcNow.Date;
var todaysTodos = context.GetTable<TodoItem>()
	.Where(t => t.DueDate >= today && t.DueDate < today.AddDays(1))
	.ToList();

// Get a single todo by ID
var specificTodo = context.GetTable<TodoItem>()
	.FirstOrDefault(t => t.id == 1);
```

**💡 Pro Tip**: The `SxmLinqDbContext` is lightweight—create it when you need it, dispose when done.

### Step 5: Update Data

```csharp
// Modify an existing todo
todo.IsCompleted = true;
await todo.SaveAsync();

// Or update multiple properties
todo.Title = "Buy groceries (updated)";
todo.Description = "Milk, eggs, bread, butter";
await todo.SaveAsync();
```

**How it works**: SQLiteXM tracks whether an entity has an `id`. If it does, `SaveAsync()` performs an UPDATE instead of an INSERT.

### Step 6: Delete Data

```csharp
// Delete a todo
await todo.DeleteAsync();

// Verify it's gone
var deleted = context.GetTable<TodoItem>()
	.FirstOrDefault(t => t.id == todo.id);

// deleted will be null
```

---

## Sample Apps

SQLiteXM includes **three sample applications** to help you learn:

### 1. **RegistrationDemo** (Simple)
A basic user registration form demonstrating:
- Entity definition
- Save and query operations
- Data binding in MAUI

📂 Location: `Samples/RegistrationDemo/`

**Perfect for**: First-time users wanting a minimal working example.

---

### 2. **DirectBindingDemo** (Simple)
Shows how to bind SQLite data directly to MAUI UI controls:
- CollectionView binding
- Observable collections
- CRUD with UI updates

📂 Location: `Samples/DirectBindingDemo/`

**Perfect for**: Learning data binding patterns with SQLiteXM.

---

### 3. **QueryGalleryDemo** (Comprehensive) ⭐

An **interactive query explorer** with 90+ real-world examples organized by category:

**Query Categories**:
- 📄 **Basic Queries** - Simple SELECT, WHERE, ORDER BY
- 🔗 **Relationships** - JOIN queries, navigation
- 📊 **Aggregations** - COUNT, SUM, AVG, GROUP BY
- ⚡ **Advanced LINQ** - Complex queries, paging
- 💻 **Raw SQL** - Custom SQL from JSON
- 🚀 **Performance** - Large datasets, benchmarks
- 🔄 **Many-to-Many** - Junction tables, playlist/track relationships
- 🔒 **Transactions** - Atomic operations, rollback
- 🔐 **Parameterized Queries** - SQL injection prevention
- 🔧 **Data Modification** - INSERT, UPDATE, DELETE operations

**Features**:
- Syntax-highlighted code display
- Runnable examples with real data
- Code explanations and comments
- Execution timing
- Result visualization

📂 Location: `Samples/QueryGalleryDemo/`  
📖 [Read the Query Gallery README](../Samples/QueryGalleryDemo/README.md)

**Perfect for**: Learning advanced querying patterns and best practices.

---

## What's Next?

Now that you have SQLiteXM running, explore these topics:

### 📚 Core Documentation
- **[Defining Your Data](DefiningYourData.md)** - Entities, attributes, indexes, foreign keys
- **[Querying Data](QueryingData.md)** - LINQ patterns, joins, aggregations
- **[Saving Data](SavingData.md)** - Insert, update, delete, batch operations
- **[Transactions](Transactions.md)** - Explicit transactions, rollback, best practices
- **[Multi-Database](MultiDatabase.md)** - Work with multiple SQLite databases

### 🎯 Specific Scenarios
- **[Performance](Performance.md)** - Optimization tips and benchmarks
- **[Testing Your App](TESTING.md)** - How to write tests with SQLiteXM
- **[Advanced Topics](Advanced.md)** - Thread safety, concurrency, troubleshooting

### 🚀 Advanced Topics
- **[iOS Deployment](iOS-Deployment.md)** - AOT/trimming configuration for iOS apps
- **[Database Options](SxmDatabaseOptions.md)** - Complete SxmDatabaseOptions reference
- **[SQL Statements File](SqlStatementsFile.md)** - SqlStatements.json configuration guide

### 💡 Interactive Learning
- **[Query Gallery Demo](../Samples/QueryGalleryDemo/)** - 90+ runnable query examples
- **[Test Suite](../SQLiteXM.Tests/)** - 182 tests showing real-world patterns

---

## Need Help?

- 🐛 **Found a bug?** [Report it on GitHub](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/issues)
- 💬 **Have a question?** [Start a discussion](https://github.com/AnthonySerpico/SQLiteXM-for-.NET-MAUI/discussions)
- 📖 **Want more examples?** Check out the [Query Gallery Demo](../Samples/QueryGalleryDemo/)

---

**Welcome to the SQLiteXM community! 🎉**

Next: [Defining Your Data →](DefiningYourData.md)
