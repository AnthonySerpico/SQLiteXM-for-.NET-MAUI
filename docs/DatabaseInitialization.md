# Database Initialization Guide

## Overview

Database initialization is the critical first step in using SQLiteXM. The initialization process:

1. **Parses your SQL Statements file** to discover database configuration and named statements
2. **Applies PRAGMA settings** from `SxmDatabaseOptions` to configure SQLite behavior
3. **Creates or migrates the database schema** including system tables
4. **Registers statement definitions** for use throughout your application

**⚠️ Important:** Initialization must complete before creating any entities or executing any database operations.

## Table of Contents

- [Quick Start](#quick-start)
- [Initialization Methods](#initialization-methods)
- [Initialization Workflow](#initialization-workflow)
- [Complete Initialization Example](#complete-initialization-example)
- [Entity Registration](#entity-registration)
- [Initialization Best Practices](#initialization-best-practices)
- [Troubleshooting](#troubleshooting)

---

## Quick Start

### Minimal Initialization (.NET MAUI)

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		// ✅ Initialize database BEFORE building the app
		InitializeDatabaseAsync().GetAwaiter().GetResult();

		return builder.Build();
	}

	private static async Task InitializeDatabaseAsync()
	{
		// Load SQL statements file from Resources/Raw
		using Stream stream = await FileSystem.OpenAppPackageFileAsync(
			"SqlStatements.json"
		);

		// Initialize database
		await SxmDatabase.InitializeAsync(stream);

		// Register entity schemas
		await SxmDatabase.RegisterEntitiesAsync(
			typeof(User),
			typeof(Order),
			typeof(Product)
		);
	}
}
```

---

## Initialization Methods

`SxmDatabase` provides two overloads for initialization:

### Method 1: Initialize from Stream

**Best for:** .NET MAUI apps with embedded resources

```csharp
public static async Task InitializeAsync(
	Stream stream,
	SxmDatabaseOptions? databaseOptions = null
)
```

**Parameters:**
- `stream` - Open, readable stream containing SQL statements JSON/XML
- `databaseOptions` - Optional configuration (PRAGMA settings, connection pooling, etc.)

**Example:**

```csharp
using Stream stream = await FileSystem.OpenAppPackageFileAsync(
	"SqlStatements.json"
);

var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal
};

await SxmDatabase.InitializeAsync(stream, options);
```

---

### Method 2: Initialize from File Path

**Best for:** Desktop apps, file system access

```csharp
public static async Task InitializeAsync(
	string sqlStatementsFileName,
	SxmDatabaseOptions? databaseOptions = null
)
```

**Parameters:**
- `sqlStatementsFileName` - Absolute or relative path to SQL statements file
- `databaseOptions` - Optional configuration

**Example:**

```csharp
// Relative path (resolved against AppContext.BaseDirectory)
await SxmDatabase.InitializeAsync("SqlStatements.json");

// Absolute path
await SxmDatabase.InitializeAsync(
	@"C:\MyApp\Config\SqlStatements.json"
);

// With options
await SxmDatabase.InitializeAsync(
	"SqlStatements.json",
	new SxmDatabaseOptions { ForeignKeys = true }
);
```

---

## Initialization Workflow

The initialization process follows these steps:

### Step 1: Parse SQL Statements File

SQLiteXM reads your SQL Statements JSON/XML file and extracts:

- Database name and default status
- Named INSERT, SELECT, UPDATE, DELETE statements
- Trigger definitions
- Optional schema version number

**SQL Statements File (SqlStatements.json):**

```json
{
  "database": "myapp_database",
  "isDefault": true,

  "insert": [
	{
	  "Statement Name": "insertUser",
	  "Table Name": "Users",
	  "Statement": "INSERT INTO Users (name, email) VALUES (@name, @email)"
	}
  ],

  "select": [
	{
	  "Statement Name": "getUserById",
	  "Table Name": "Users",
	  "Statement": "SELECT * FROM Users WHERE id = @p0"
	}
  ]
}
```

---

### Step 2: Validate Options

If you provided `SxmDatabaseOptions`, SQLiteXM validates all settings:

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Normal,
	BusyTimeout = 5000,
	CacheSize = 2048
};

await SxmDatabase.InitializeAsync(stream, options);
```

**Validation includes:**
- ✅ PRAGMA values are valid
- ✅ Timeouts are positive
- ✅ Cache sizes are reasonable
- ⚠️ Warnings logged for problematic combinations (e.g., WAL + Full sync)

---

### Step 3: Set Database Folder

The database file location is determined:

```csharp
var options = new SxmDatabaseOptions
{
	// Override default database folder
	DatabaseFolderOverride = Environment.GetFolderPath(
		Environment.SpecialFolder.MyDocuments
	)
};
```

**Default Locations:**

- **Without override:** Platform-specific app data folder
- **With override:** Your custom path

---

### Step 4: Create/Upgrade Schema

SQLiteXM creates internal system tables:

- `_systemCloudSynchDescriptor` - Tracks synchronized tables
- `_systemCloudSynch` - Queues sync operations

It also:
- Checks schema version (`PRAGMA user_version`)
- Applies any pending migrations
- Creates/updates triggers

---

### Step 5: Associate Options with Database

The options you provide are associated with the database name so they apply to all future connections:

```csharp
// Options apply to all connections to "myapp_database"
await SxmDatabase.InitializeAsync(stream, options);

// Later: these automatically use the configured options
using var connection = new SxmConnection("myapp_database");
```

---

### Step 6: Mark as Initialized

A static flag prevents duplicate initialization:

```csharp
// ✅ First call - initializes
await SxmDatabase.InitializeAsync(stream, options);

// ✅ Second call - returns immediately (idempotent)
await SxmDatabase.InitializeAsync(stream, options);
```

---

## Complete Initialization Example

Here's a production-ready initialization setup for a .NET MAUI app:

```csharp
using Microsoft.Maui;
using SQLiteXM;

namespace MyMauiApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// ===== Initialize Database =====
		InitializeDatabaseAsync().GetAwaiter().GetResult();

		return builder.Build();
	}

	private static async Task InitializeDatabaseAsync()
	{
		try
		{
			// ===== Step 1: Configure Database Options =====
			var databaseOptions = new SxmDatabaseOptions
			{
				// SQLite PRAGMA configuration
				ForeignKeys = true,
				JournalModeOption = SxmJournalMode.Wal,
				SynchronousModeOption = SxmSynchronousMode.Normal,
				BusyTimeout = 5000,
				CacheSize = 2048,
				WalAutoCheckpoint = 250,
				TempStore = SxmTempStore.Memory,

				// WAL checkpoint control
				CheckPointConnection = CheckPointConnection.MaxSize,
				CheckPointWalMaxSize = 32,

				// Connection management
				DefaultTimeout = 30,
				EnableConnectionPooling = true,
				EnableLogging = true,

				// Custom database location
				DatabaseFolderOverride = Environment.GetFolderPath(
					Environment.SpecialFolder.MyDocuments
				)
			};

			// ===== Step 2: Add Lifecycle Hooks =====

			// Verify settings after connection opens
			databaseOptions.OnConnectionOpened(connection =>
			{
				using var cmd = connection.CreateCommand();

				// Verify foreign keys are enabled
				cmd.CommandText = "PRAGMA foreign_keys";
				long? fkEnabled = (long?)cmd.ExecuteScalar();
				if (fkEnabled != 1)
				{
					throw new InvalidOperationException(
						"Foreign keys failed to enable"
					);
				}

				Console.WriteLine("✅ Database connection opened successfully");
			});

			// Log connection closures
			databaseOptions.OnConnectionClosed(() =>
			{
				Console.WriteLine("📊 Connection closed");
			});

			// ===== Step 3: Load SQL Statements File =====

			using Stream stream = await FileSystem.OpenAppPackageFileAsync(
				"SqlStatements.json"
			).ConfigureAwait(false);

			// ===== Step 4: Initialize Database =====

			await SxmDatabase.InitializeAsync(stream, databaseOptions);

			Console.WriteLine("✅ Database initialized");

			// ===== Step 5: Register Entity Schemas =====

			await SxmDatabase.RegisterEntitiesAsync(
				typeof(User),
				typeof(Order),
				typeof(Product),
				typeof(Customer),
				typeof(OrderItem)
			);

			Console.WriteLine("✅ Entity schemas registered");
			Console.WriteLine("🎉 Database ready for use");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
			throw;
		}
	}
}
```

---

## Entity Registration

After initialization, register your entity types to create their database schemas.

### Why Register Entities?

Entity registration:
- ✅ Creates tables based on your entity classes
- ✅ Creates indexes (standard and unique)
- ✅ Applies foreign key constraints
- ✅ Creates triggers from the SQL Statements file
- ✅ Validates schema consistency

### Registration Syntax

```csharp
await SxmDatabase.RegisterEntitiesAsync(
	typeof(EntityType1),
	typeof(EntityType2),
	typeof(EntityType3)
);
```

### Complete Example

```csharp
// Define entities
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
	public string? Name { get; set; }
	public string? Email { get; set; }
	public DateTime CreatedAt { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	[ForeignKey(foreignTable: "User")]
	public long UserFK { get; set; }

	public decimal TotalAmount { get; set; }
	public DateTime OrderDate { get; set; }
	public string? Status { get; set; }
}

// Register entities
await SxmDatabase.RegisterEntitiesAsync(
	typeof(User),
	typeof(Order)
);

// Now you can use entities
var user = new User
{
	Name = "John Doe",
	Email = "john@example.com",
	CreatedAt = DateTime.UtcNow
};
await user.SaveAsync();

var order = new Order
{
	UserFK = user.id,
	TotalAmount = 99.99m,
	OrderDate = DateTime.UtcNow,
	Status = "pending"
};
await order.SaveAsync();
```

### Registration Order

**Foreign Key Dependencies:**

If `Order` has a foreign key to `User`, the order doesn't matter:

```csharp
// ✅ Both work
await SxmDatabase.RegisterEntitiesAsync(typeof(User), typeof(Order));
await SxmDatabase.RegisterEntitiesAsync(typeof(Order), typeof(User));
```

SQLiteXM handles dependency order automatically.

---

## Initialization Best Practices

### 1. **Initialize Early**

Initialize in `MauiProgram.cs` or `App.xaml.cs` before any database operations:

```csharp
// ✅ Good - Initialize before app starts
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		// Initialize here
		InitializeDatabaseAsync().GetAwaiter().GetResult();

		return builder.Build();
	}
}

// ❌ Bad - Initialize lazily on first use
// (entities may be constructed before initialization completes)
```

---

### 2. **Use Lifecycle Hooks for Validation**

```csharp
var options = new SxmDatabaseOptions
{
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal
};

// ✅ Validate settings were applied
options.OnConnectionOpened(connection =>
{
	using var cmd = connection.CreateCommand();

	cmd.CommandText = "PRAGMA journal_mode";
	string? journalMode = (string?)cmd.ExecuteScalar();

	if (!journalMode?.Equals("wal", StringComparison.OrdinalIgnoreCase) ?? true)
	{
		throw new InvalidOperationException($"Expected WAL, got: {journalMode}");
	}
});
```

---

### 3. **Handle Initialization Errors**

```csharp
private static async Task InitializeDatabaseAsync()
{
	try
	{
		using Stream stream = await FileSystem.OpenAppPackageFileAsync(
			"SqlStatements.json"
		);

		await SxmDatabase.InitializeAsync(stream, databaseOptions);
		await SxmDatabase.RegisterEntitiesAsync(typeof(User), typeof(Order));
	}
	catch (FileNotFoundException ex)
	{
		Console.WriteLine($"SQL Statements file not found: {ex.Message}");
		throw;
	}
	catch (InvalidOperationException ex)
	{
		Console.WriteLine($"Database options validation failed: {ex.Message}");
		throw;
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Unexpected initialization error: {ex.Message}");
		throw;
	}
}
```

---

### 4. **Configure for Platform**

```csharp
var databaseOptions = new SxmDatabaseOptions
{
	// ===== Mobile-optimized settings =====
#if ANDROID || IOS
	CacheSize = 1024,              // 1 MB cache
	CheckPointWalMaxSize = 32,     // 32 KB WAL limit
	BusyTimeout = 3000,            // 3 second timeout
#else
	// ===== Desktop-optimized settings =====
	CacheSize = 8192,              // 8 MB cache
	CheckPointWalMaxSize = 256,    // 256 KB WAL limit
	BusyTimeout = 10000,           // 10 second timeout
#endif

	// ===== Common settings =====
	ForeignKeys = true,
	JournalModeOption = SxmJournalMode.Wal,
	SynchronousModeOption = SxmSynchronousMode.Normal
};
```

---

### 5. **Synchronous Initialization**

Initialization must complete synchronously in `MauiProgram.cs`:

```csharp
// ✅ Correct - Wait for completion
InitializeDatabaseAsync().GetAwaiter().GetResult();

// ❌ Wrong - Fire and forget
_ = InitializeDatabaseAsync();  // App may start before init completes
```

---

### 6. **Register All Entities Together**

```csharp
// ✅ Good - Register all entities in one call
await SxmDatabase.RegisterEntitiesAsync(
	typeof(User),
	typeof(Order),
	typeof(Product),
	typeof(Customer),
	typeof(OrderItem)
);

// ❌ Bad - Multiple registration calls
await SxmDatabase.RegisterEntitiesAsync(typeof(User));
await SxmDatabase.RegisterEntitiesAsync(typeof(Order));
await SxmDatabase.RegisterEntitiesAsync(typeof(Product));
```

---

### 7. **Embed SQL Statements File**

Ensure your SQL Statements file is included in the app package:

**MAUI Project (.csproj):**

```xml
<ItemGroup>
  <MauiAsset Include="Resources\Raw\SqlStatements.json" />
</ItemGroup>
```

**File Structure:**

```
MyMauiApp/
├── Resources/
│   └── Raw/
│       └── SqlStatements.json  ← Here
└── MauiProgram.cs
```

---

## Troubleshooting

### "SQLiteXM has not been initialized"

**Error Message:**
```
InvalidOperationException: SQLiteXM has not been initialized. 
Call SxmDatabase.InitializeAsync(...) before instantiating entity classes.
```

**Cause:** Trying to create entities or execute queries before initialization

**Solution:** Ensure initialization completes before any database usage:

```csharp
// ✅ Correct order
await SxmDatabase.InitializeAsync(stream, options);
await SxmDatabase.RegisterEntitiesAsync(typeof(User));

var user = new User { Name = "John" };  // Now OK

// ❌ Wrong order
var user = new User { Name = "John" };  // THROWS ERROR

await SxmDatabase.InitializeAsync(stream, options);
```

---

### "Entity type has not been registered"

**Error Message:**
```
InvalidOperationException: Entity type 'User' has not been registered.
Schema registration is required before creating entity instances.
```

**Cause:** Entity type not included in `RegisterEntitiesAsync`

**Solution:** Register the entity type:

```csharp
// Register the entity
await SxmDatabase.RegisterEntitiesAsync(typeof(User));

// Now you can create instances
var user = new User();
```

---

### "SQL statements file not found"

**Error Message:**
```
FileNotFoundException: The SQL statements file 'SqlStatements.json' could not be found.
```

**Cause:** File not embedded or incorrect path

**Solution:**

1. **Check file location:**
   ```
   MyMauiApp/Resources/Raw/SqlStatements.json
   ```

2. **Verify .csproj includes it:**
   ```xml
   <MauiAsset Include="Resources\Raw\SqlStatements.json" />
   ```

3. **Clean and rebuild:**
   ```bash
   dotnet clean
   dotnet build
   ```

---

### "Unable to set PRAGMA"

**Error Message:**
```
InvalidOperationException: SQLiteXM connection failed. 
Unable to set PRAGMA journal_mode to 'WAL'. Actual mode is 'DELETE'.
```

**Cause:** Platform doesn't support requested PRAGMA (e.g., WAL on network drive)

**Solution:** Adjust options for platform:

```csharp
var options = new SxmDatabaseOptions
{
	// Use DELETE mode instead of WAL
	JournalModeOption = SxmJournalMode.Delete,
	SynchronousModeOption = SxmSynchronousMode.Full
};
```

---

### "Warning: Unassigned trigger(s) detected"

**Warning Message:**
```
Warning: Unassigned trigger(s) detected
Check that trigger source table names match registered entity table names.
  [1] Unknown Table: 'UserRecord'
	  Trigger SQL: CREATE TRIGGER updateCustomer ...
```

**Cause:** Trigger references a table that wasn't registered

**Solution:** Ensure trigger table name matches registered entity:

**SQL Statements File:**
```json
{
  "trigger": [
	{
	  "Table Name": "User",  // Must match entity table name
	  "Statement": "CREATE TRIGGER updateTimestamp ..."
	}
  ]
}
```

**Entity:**
```csharp
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity  // Table name is "User"
{
	// ...
}
```

**Registration:**
```csharp
await SxmDatabase.RegisterEntitiesAsync(typeof(User));
```

---

## Initialization Checklist

Before your app starts, ensure:

- ✅ SQL Statements file exists in `Resources/Raw/`
- ✅ SQL Statements file is marked as `MauiAsset`
- ✅ SQL Statements file has `database` and `isDefault` properties
- ✅ `SxmDatabase.InitializeAsync()` called in `MauiProgram.cs`
- ✅ Initialization completes synchronously (`.GetAwaiter().GetResult()`)
- ✅ `RegisterEntitiesAsync()` called with all entity types
- ✅ Database options validated and appropriate for platform
- ✅ Lifecycle hooks added for diagnostics (if needed)
- ✅ Error handling in place for initialization failures

---

## Related Documentation

- [SQL Statements File Guide](./SqlStatementsFile.md)
- [SxmDatabaseOptions Configuration](./SxmDatabaseOptions.md)
- [Entity Registration](./EntityRegistration.md)
- [SxmEntity Base Class](./SxmEntity.md)

---

## Version Information

- **SQLiteXM Version:** 1.0+
- **Last Updated:** 2026
- **Compatibility:** .NET 8, .NET 9, .NET MAUI
