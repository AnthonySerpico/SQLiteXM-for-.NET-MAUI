# SQLiteXM Database Configuration and Initialization

## Introduction

This guide walks through the details of configuring and initializing SQLiteXM. 
There is particular focus on SQLiteXM initialization options used to configure SQLiteXM 
and influence the operation of the SQLite database.

---

## Table of Contents

Below is a complete reference of all available options and recommended configurations.
<br>

- [Recommended Starting Configuration](#recommended-starting-configuration)
- [SQLite PRAGMA Configuration](#sqlite-pragma-configuration)
- [Connection Management](#connection-management)
- [Lifecycle Hooks](#lifecycle-hooks)
- [Database Path Customization](#database-path-customization)
- [Complete Configuration Example](#complete-configuration-example)
- [Best Practices](#best-practices)

---

# 1. Initializing SQLiteXM

As we learned in the [`Getting Started Guide`](./GettingStarted.md), initialization is performed once, typically during application startup.
`InitializeDatabaseAsync` below shows a typical initialization sequence for SQLiteXM.



```csharp
public static async Task InitializeDatabaseAsync()
{
    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    await SxmDatabase.InitializeAsync(stream, databaseOptions: null);
    await SxmDatabase.RegisterEntitiesAsync(typeof(User));
}
```

The second parameter of `InitializeAsync()` is an optional `SxmDatabaseOptions` instance used to 
customize the operation of SQLiteXM and the SQLite database. This will be the focus of the remainder of this guide.



`SxmDatabaseOptions` provides a high-level, type-safe configuration surface for SQLite database initialization in SQLiteXM. This class allows you to configure SQLite PRAGMA settings, connection pooling, lifecycle hooks, and other database behaviors without writing raw SQL commands.

If no options are supplied, SQLiteXM uses its default configuration and SQLite's built-in defaults where applicable.


## Database Options

SQLiteXM provides many options for controlling database behavior. Some of the more commonly used settings include:

| Option | Purpose |
|----------|----------|
| ForeignKeys | Enables SQLite foreign key enforcement |
| JournalModeOption | Controls journaling mode (WAL is recommended for most apps) |
| SynchronousModeOption | Balances performance versus durability |
| BusyTimeout | Specifies how long SQLite waits for locked resources |
| EnableConnectionPooling | Enables connection reuse for improved performance |
| EnableLogging | Enables SQLiteXM logging and diagnostics |
| DatabaseFolderOverride | Overrides the default database storage location |

---

## Recommended Starting Configuration

These three settings provide a good balance of safety and performance for most applications.
```csharp
var options = new SxmDatabaseOptions
{
    ForeignKeys = true,
    JournalModeOption = SxmJournalMode.Wal,
    SynchronousModeOption = SxmSynchronousMode.Normal
};
```


<br>
Below is an example that includes every available option:

```csharp

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
        CheckPointWalMaxSize = 256,

        // ✅ Connection pooling
        DefaultTimeout = 5,
        EnableConnectionPooling = true,

        // ✅ Logging control
        EnableLogging = true,

        // ✅ Database path customization
        DatabaseFolderOverride = Environment.GetFolderPath(
                                 Environment.SpecialFolder.MyDocuments)
    };

    // ✅ Connection opened lifecycle hooks
    databaseOptions.OnConnectionOpened(connection =>
    {
        using Microsoft.Data.Sqlite.SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA synchronous";
        long? synchronous = (long?) cmd.ExecuteScalar();
        if (synchronous == null)
        {
            throw new InvalidOperationException($"SQLiteXM connection failed. 
                                        Unable to read PRAGMA synchronous.");
        }
    });

    // ✅ Connection closed lifecycle hooks
    databaseOptions.OnConnectionClosed(() =>
    {
    });

    await SxmDatabase.InitializeAsync(stream, databaseOptions);
```

---

## SQLite PRAGMA Configuration

### Foreign Key Enforcement

**Property:** `ForeignKeys`  
**Type:** `bool?`  
**Default:** `null` (SQLite default is OFF)

Enables or disables foreign key constraint enforcement.

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true  // Enforce referential integrity
};
```

**When to use:**
- ✅ Enable when you need referential integrity and are using foreign keys in your schema.
- ❌ Disable when you manage integrity manually or don't use foreign keys.

---

### Journal Mode

**Property:** `JournalModeOption`  
**Type:** `SxmJournalMode?`  
**Default:** `null`

Controls SQLite's journaling mechanism for transaction durability and concurrency.

**Available Modes:**
- `SxmJournalMode.Wal` - Write-Ahead Logging (recommended for most apps)
- `SxmJournalMode.Delete` - Traditional rollback journal
- `SxmJournalMode.Truncate` - Truncate journal instead of deleting
- `SxmJournalMode.Persist` - Keep journal file
- `SxmJournalMode.Memory` - In-memory journaling
- `SxmJournalMode.Off` - No journaling (dangerous!)

```csharp
var options = new SxmDatabaseOptions
{
	JournalModeOption = SxmJournalMode.Wal  // Best for concurrent access
};
```

**WAL Mode Benefits:**
- Multiple readers don't block writers
- Better concurrency for read-heavy workloads
- Faster in most scenarios

**When to use:**
- ✅ `Wal` - Most .NET MAUI apps (default recommendation)
- ⚠️ `Delete` - Network file systems (WAL not supported)
- ❌ `Off` - Never in production (no ACID guarantees)

---

### Synchronous Mode

**Property:** `SynchronousModeOption`  
**Type:** `SxmSynchronousMode?`  
**Default:** `null`

Controls how aggressively SQLite syncs data to disk.

**Available Modes:**
- `SxmSynchronousMode.Off` (0) - No syncing (fastest, risk of corruption)
- `SxmSynchronousMode.Normal` (1) - Sync at critical moments (recommended with WAL)
- `SxmSynchronousMode.Full` (2) - Sync after every operation (slowest, most durable)
- `SxmSynchronousMode.Extra` (3) - Extra paranoid mode

```csharp
var options = new SxmDatabaseOptions
{
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Normal  // Good balance
};
```

**When to use:**
- ✅ `Normal` with WAL - Recommended for most apps
- ⚠️ `Full` - When absolute durability is required (slower)
- ❌ `Off` - Only for temporary/cache databases

---

### Busy Timeout

**Property:** `BusyTimeout`  
**Type:** `long?` (milliseconds)  
**Default:** `null`

How long to wait when the database is locked before returning SQLITE_BUSY.

```csharp
var options = new SxmDatabaseOptions
{
	BusyTimeout = 5000  // Wait up to 5 seconds
};
```

**Recommendations:**
- **Single-threaded apps:** 500-1000ms
- **Multi-threaded apps:** 3000-5000ms
- **High concurrency:** 10000ms+

---

### Cache Size

**Property:** `CacheSize`  
**Type:** `long?` (kilobytes)  
**Default:** `null`

Amount of memory SQLite uses for caching database pages.

```csharp
var options = new SxmDatabaseOptions
{
	CacheSize = 2048  // 2 MB cache (2048 KB)
};
```

**Sizing Guidelines:**
- **Small databases (<10MB):** 512 KB - 1 MB
- **Medium databases (10-100MB):** 2 MB - 8 MB
- **Large databases (>100MB):** 16 MB - 64 MB

**Note:** SQLite uses negative values to specify KB. SQLiteXM automatically converts your positive KB value.

---

### WAL Auto-Checkpoint

**Property:** `WalAutoCheckpoint`  
**Type:** `long?` (pages)  
**Default:** `null` (SQLite default is 1000)

Number of pages in WAL file before automatic checkpoint.

```csharp
var options = new SxmDatabaseOptions
{
	WalAutoCheckpoint = 1000  // Checkpoint every 1000 pages (~4MB)
};
```

**Recommendations:**
- **Mobile apps:** 250-500 pages (keep WAL small)
- **Desktop apps:** 1000-2000 pages
- **High-write apps:** 5000+ pages (checkpoint less frequently)

---

### Temp Store

**Property:** `TempStore`  
**Type:** `SxmTempStore?`  
**Default:** `null`

Where SQLite stores temporary tables and indices.

**Available Options:**
- `SxmTempStore.Default` (0) - Use compile-time default
- `SxmTempStore.File` (1) - Store on disk
- `SxmTempStore.Memory` (2) - Store in RAM (faster)

```csharp
var options = new SxmDatabaseOptions
{
	TempStore = SxmTempStore.Memory  // Faster temp operations
};
```

**When to use:**
- ✅ `Memory` - Most modern devices have sufficient RAM
- ⚠️ `File` - Low-memory devices or very large temp data

---

## Connection Management

### Connection Pooling

**Property:** `EnableConnectionPooling`  
**Type:** `bool?`  
**Default:** `true`

Enables reuse of database connections instead of creating new ones.

```csharp
var options = new SxmDatabaseOptions
{
	EnableConnectionPooling = true  // Reuse connections (recommended)
};
```

**Benefits:**
- Faster connection acquisition
- Reduced overhead
- Better performance under load

**When to disable:**
- Debugging connection issues
- Special connection requirements per operation

---

### Default Timeout

**Property:** `DefaultTimeout`  
**Type:** `int?` (seconds)  
**Default:** `null`

Default command timeout for database operations.

```csharp
var options = new SxmDatabaseOptions
{
	DefaultTimeout = 30  // 30 second command timeout
};
```

---

### Logging

**Property:** `EnableLogging`  
**Type:** `bool?`  
**Default:** `true`

Enables SQLiteXM internal logging.

```csharp
var options = new SxmDatabaseOptions
{
	EnableLogging = true  // Log database operations
};
```

---

## WAL Checkpoint Control

SQLiteXM provides fine-grained control over when and how WAL checkpointing occurs.

### CheckPointConnection

**Property:** `CheckPointConnection`  
**Type:** `CheckPointConnection?`  
**Default:** `null`

Controls automatic checkpoint behavior.

**Options:**
- `CheckPointConnection.Off` - No automatic checkpointing
- `CheckPointConnection.OnConnectionClose` - Checkpoint when connection closes
- `CheckPointConnection.MaxSize` - Checkpoint when WAL exceeds size limit

```csharp
var options = new SxmDatabaseOptions
{
	JournalModeOption = SxmJournalMode.Wal,
	CheckPointConnection = CheckPointConnection.MaxSize,
	CheckPointWalMaxSize = 256  // Checkpoint when WAL > 256 KB
};
```

### CheckPointWalMaxSize

**Property:** `CheckPointWalMaxSize`  
**Type:** `int?` (kilobytes)  
**Default:** `null`

Maximum WAL file size before triggering a checkpoint (used with `CheckPointConnection.MaxSize`).

```csharp
var options = new SxmDatabaseOptions
{
	CheckPointConnection = CheckPointConnection.MaxSize,
	CheckPointWalMaxSize = 256  // 256 KB limit
};
```

**Recommendations:**
- **Mobile apps:** 64 KB - 256 KB (keep storage usage low)
- **Desktop apps:** 256 KB - 1 MB

---

## Lifecycle Hooks

SQLiteXM allows you to hook into connection lifecycle events for custom logic.

Most applications will never need lifecycle hooks. They are intended for advanced scenarios where custom logic should run whenever a SQLite connection is opened or closed.

### OnConnectionOpened

Executes custom code after a connection is opened and PRAGMA settings are applied.

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal
};

// Add opened hook
options.OnConnectionOpened(connection =>
{
	// Verify PRAGMA settings
	using var cmd = connection.CreateCommand();
	cmd.CommandText = "PRAGMA foreign_keys";
	long? fkEnabled = (long?)cmd.ExecuteScalar();

	if (fkEnabled != 1)
	{
		throw new InvalidOperationException("Foreign keys not enabled!");
	}

	Console.WriteLine("Connection opened successfully");
});
```

**Use cases:**
- Verify PRAGMA settings
- Log connection events
- Initialize connection-specific state
- Register custom SQLite functions

---

### OnConnectionClosed

Executes custom code after a connection is closed.

```csharp
options.OnConnectionClosed(() =>
{
	Console.WriteLine("Connection closed");
	// Cleanup, logging, metrics, etc.
});
```

**Use cases:**
- Cleanup resources
- Log connection lifetime
- Update metrics
- Trigger maintenance tasks

---

## Database Path Customization

### DatabaseFolderOverride

**Property:** `DatabaseFolderOverride`  
**Type:** `string?`  
**Default:** `null` (Environment.SpecialFolder.LocalApplicationData)

Override the default database file location.

```csharp
var options = new SxmDatabaseOptions
{
	DatabaseFolderOverride = Environment.GetFolderPath(
		Environment.SpecialFolder.MyDocuments
	)
};
```

**Common Locations:**

```csharp
// User's documents folder
DatabaseFolderOverride = Environment.GetFolderPath(
	Environment.SpecialFolder.MyDocuments
)

// Application data folder
DatabaseFolderOverride = Environment.GetFolderPath(
	Environment.SpecialFolder.ApplicationData
)

// Local application data (roaming disabled)
DatabaseFolderOverride = Environment.GetFolderPath(
	Environment.SpecialFolder.LocalApplicationData
)

// Custom path
DatabaseFolderOverride = Path.Combine(
	FileSystem.AppDataDirectory, 
	"databases"
)
```

---

## Complete Configuration Example

Here's a production-ready configuration for a .NET MAUI mobile app:

```csharp
using SQLiteXM;
using Microsoft.Data.Sqlite;

public async Task InitializeDatabaseAsync()
{
	var options = new SxmDatabaseOptions
	{
		// ===== SQLite PRAGMA Configuration =====

		// Enable foreign key constraints for referential integrity
		ForeignKeys = true,

		// Use WAL mode for better concurrency
		JournalModeOption = SxmJournalMode.Wal,

		// Normal sync mode (good balance with WAL)
		SynchronousModeOption = SxmSynchronousMode.Normal,

		// Wait up to 5 seconds if database is locked
		BusyTimeout = 5000,

		// 2 MB cache for better performance
		CacheSize = 2048,

		// Auto-checkpoint every 250 pages (~1 MB)
		WalAutoCheckpoint = 250,

		// Keep temp data in memory (faster)
		TempStore = SxmTempStore.Memory,

		// ===== WAL Checkpoint Control =====

		// Checkpoint when WAL exceeds size limit
		CheckPointConnection = CheckPointConnection.MaxSize,

		// Trigger checkpoint at 256 KB
		CheckPointWalMaxSize = 256,

		// ===== Connection Management =====

		// Enable connection pooling (better performance)
		EnableConnectionPooling = true,

		// 30 second command timeout
		DefaultTimeout = 30,

		// Enable logging for diagnostics
		EnableLogging = true,

		// ===== Database Path Customization =====

		// Store database in user's documents folder
		DatabaseFolderOverride = Environment.GetFolderPath(
			Environment.SpecialFolder.MyDocuments
		)
	};

	// ===== Connection Lifecycle Hooks =====

	// Verify PRAGMA settings after connection opens
	options.OnConnectionOpened(connection =>
	{
		using var cmd = connection.CreateCommand();

		// Verify foreign keys are enabled
		cmd.CommandText = "PRAGMA foreign_keys";
		long? fkEnabled = (long?)cmd.ExecuteScalar();
		if (fkEnabled != 1)
		{
			throw new InvalidOperationException(
				"Failed to enable foreign key constraints"
			);
		}

		// Verify WAL mode is active
		cmd.CommandText = "PRAGMA journal_mode";
		string? journalMode = (string?)cmd.ExecuteScalar();
		if (!journalMode?.Equals("wal", StringComparison.OrdinalIgnoreCase) ?? true)
		{
			throw new InvalidOperationException(
				$"Expected WAL mode, got: {journalMode}"
			);
		}

		Console.WriteLine("✅ Database connection initialized successfully");
	});

	// Log when connections close
	options.OnConnectionClosed(() =>
	{
		Console.WriteLine("📊 Connection closed");
	});

	// ===== Initialize Database =====

	// Load SQL statements from embedded resource
	using Stream stream = await FileSystem.OpenAppPackageFileAsync(
		"SqlStatements.json"
	).ConfigureAwait(false);

	await SxmDatabase.InitializeAsync(stream, options);

	// Register your entity schemas
	await SxmDatabase.RegisterEntitiesAsync(
		typeof(Customer),
		typeof(Order),
		typeof(Product)
	);

	Console.WriteLine("✅ Database initialized with custom options");
}
```

---

## Best Practices

### 1. **Choose the Right Journal Mode**

```csharp
// ✅ Recommended for mobile apps
JournalModeOption = SxmJournalMode.Wal

// ⚠️ Only if WAL is not supported
JournalModeOption = SxmJournalMode.Delete
```

### 2. **Pair WAL with Normal Synchronous**

```csharp
// ✅ Good balance of performance and durability
JournalModeOption = SxmJournalMode.Wal,
SynchronousModeOption = SxmSynchronousMode.Normal
```

### 3. **Enable Foreign Keys**

```csharp
// ✅ Protect data integrity
ForeignKeys = true
```

### 4. **Configure Busy Timeout**

```csharp
// ✅ Avoid SQLITE_BUSY errors
BusyTimeout = 5000  // 5 seconds
```

### 5. **Control WAL Growth**

```csharp
// ✅ Keep WAL file size manageable on mobile
CheckPointConnection = CheckPointConnection.MaxSize,
CheckPointWalMaxSize = 256  // 256 KB
```

### 6. **Verify Settings with Hooks**

```csharp
// ✅ Catch configuration issues early
options.OnConnectionOpened(connection =>
{
	// Verify critical PRAGMA settings
});
```

### 7. **Size Cache Appropriately**

```csharp
// Small database (< 10 MB)
CacheSize = 512  // 512 KB

// Medium database (10-100 MB)
CacheSize = 2048  // 2 MB

// Large database (> 100 MB)
CacheSize = 8192  // 8 MB
```

---

## Configuration Profiles

### Mobile-Optimized Configuration

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Normal,
	BusyTimeout = 5000,
	CacheSize = 2048,
	WalAutoCheckpoint = 250,
	TempStore = SxmTempStore.Memory,
	CheckPointConnection = CheckPointConnection.MaxSize,
	CheckPointWalMaxSize = 256,
	EnableConnectionPooling = true,
	EnableLogging = true
};
```

### High-Performance Configuration (Desktop)

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Normal,
	BusyTimeout = 10000,
	CacheSize = 16384,  // 16 MB
	WalAutoCheckpoint = 5000,
	TempStore = SxmTempStore.Memory,
	CheckPointConnection = CheckPointConnection.MaxSize,
	CheckPointWalMaxSize = 1024,  // 1 MB
	EnableConnectionPooling = true,
	DefaultTimeout = 60
};
```

### Maximum Durability Configuration

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Full,  // Maximum safety
	BusyTimeout = 30000,
	CacheSize = 4096,
	WalAutoCheckpoint = 100,  // Checkpoint frequently
	CheckPointConnection = CheckPointConnection.OnConnectionClose,
	EnableConnectionPooling = true
};
```

