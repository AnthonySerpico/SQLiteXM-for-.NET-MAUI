# Multi-Database Support Implementation Plan for SQLiteXM

## Executive Summary

**Current State:** SQLiteXM is architected with single-database assumptions but has good foundational infrastructure for multi-database support.

**Assessment:** ✅ **Well-positioned** - The codebase has 70-80% of the infrastructure needed. Key components already support multiple databases; the main work is restructuring the SQL Statements file format and initialization flow.

**Estimated Effort:** Medium (2-3 weeks for a single developer)

**Breaking Changes:** Yes - SQL Statements file format will change (but backward compatibility is achievable)

---

## Current Architecture Analysis

### What Already Supports Multiple Databases ✅

1. **`SxmDatabaseOptions`** - Already associates options with database names via `AddDatabaseName()`
   ```csharp
   SxmDatabaseOptions.AddDatabaseName(databaseOptions, SxmProcessSQLStatements.DatabaseName);
   ```

2. **`SxmSqlStatements`** - Triggers are already keyed by database name
   ```csharp
   internal static ConcurrentDictionary<string, List<TriggerDefinition>> TriggerStatements 
	   = new ConcurrentDictionary<string, List<TriggerDefinition>>(StringComparer.Ordinal);
   ```

3. **`SxmDatabaseDescriptor`** - Uses `ConcurrentBag<string>` to track multiple databases
   ```csharp
   private static ConcurrentBag<string> _dbDescriptors = new();
   ```

4. **Entity `[Table]` Attribute** - Already has `Database` property!
   ```csharp
   [Table(IsColumnAttributeRequired = false, Database = "secondary_db")]
   public class AnalyticsLog : SxmEntity { }
   ```

5. **Connection APIs** - Accept optional database name parameters everywhere

---

### What Blocks Multiple Databases ❌

1. **`SxmProcessSQLStatements` - Single Database Parser**
   - Static properties: `_databaseName`, `_isDefaultDatabase`, `_versionNumber`
   - Only parses ONE database per file
   ```csharp
   // Lines 26-40 in SxmProcessSQLStatements.cs
   private static string _databaseName = string.Empty;
   private static bool _isDefaultDatabase = false;
   ```

2. **`SxmDatabase.InitializeAsync()` - Single Pass Initialization**
   - Calls `ParseSqlStatementsFile()` once
   - Builds schema for ONE database
   - Sets `_initialized = true` preventing re-initialization
   ```csharp
   // Lines 8569-10437 in SxmDatabase.cs
   if (_initialized)
	   return;  // ⚠️ Blocks multiple database initialization
   ```

3. **SQL Statements File Format** - Single Database Structure
   ```json
   {
	 "database": "single_db_name",  // ⚠️ Only ONE database
	 "isDefault": true,
	 "insert": [ ... ],
	 "select": [ ... ]
   }
   ```

4. **Default Database Logic** - Singleton Pattern
   ```csharp
   // SxmDatabaseDescriptor.cs line 78-79
   if (SxmDatabaseDescriptor.DefaultDatabase != null)
	   throw new ArgumentException("There can only be one default database.");
   ```

---

## Proposed Solution: Multiple Databases Support

### Design Philosophy

**Goal:** Support multiple databases while:
1. ✅ Maintaining backward compatibility (single-database files still work)
2. ✅ Keeping the API simple
3. ✅ Preserving existing entity/connection infrastructure
4. ✅ Allowing incremental adoption

---

### 1. New SQL Statements File Format

#### Multi-Database Format (New)

```json
{
  "databases": [
	{
	  "database": "main_app_db",
	  "isDefault": true,
	  "version": 1,

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
	  ],

	  "update": [ ... ],
	  "delete": [ ... ],
	  "trigger": [ ... ]
	},
	{
	  "database": "analytics_db",
	  "isDefault": false,
	  "version": 1,

	  "insert": [
		{
		  "Statement Name": "insertEvent",
		  "Table Name": "Events",
		  "Statement": "INSERT INTO Events (name, timestamp) VALUES (@name, @timestamp)"
		}
	  ],

	  "select": [
		{
		  "Statement Name": "getEventsByDate",
		  "Table Name": "Events",
		  "Statement": "SELECT * FROM Events WHERE date = @p0"
		}
	  ]
	}
  ]
}
```

#### Backward-Compatible (Existing Format Still Works)

```json
{
  "database": "myapp_db",
  "isDefault": true,
  "insert": [ ... ],
  "select": [ ... ]
}
```

**Auto-Detection Logic:**
- If root has `"databases"` array → Multi-database mode
- If root has `"database"` string → Single-database mode (legacy)

---

### 2. Modified Components

#### A. `SxmProcessSQLStatements` - Support Multiple Databases

**Change from:**
```csharp
// Current: Single static fields
private static string _databaseName = string.Empty;
private static bool _isDefaultDatabase = false;
private static long _versionNumber = 0;
```

**Change to:**
```csharp
// New: List of database descriptors
private static List<DatabaseDescriptor> _databases = new();

internal class DatabaseDescriptor
{
	public string Name { get; set; } = string.Empty;
	public bool IsDefault { get; set; }
	public long Version { get; set; }
}

// Properties for backward compatibility
internal static string DatabaseName => _databases.FirstOrDefault()?.Name ?? string.Empty;
internal static bool IsDefaultDatabase => _databases.Any(d => d.IsDefault);
```

**New Methods:**
```csharp
internal static List<DatabaseDescriptor> GetAllDatabases() => _databases;

internal static string? GetDefaultDatabaseName() 
	=> _databases.FirstOrDefault(d => d.IsDefault)?.Name;
```

---

#### B. `SxmDatabase.InitializeAsync()` - Multi-Pass Initialization

**Current:**
```csharp
public static async Task InitializeAsync(Stream stream, SxmDatabaseOptions? databaseOptions = null)
{
	await _initGate.WaitAsync();
	try
	{
		if (_initialized)
			return;  // ⚠️ Blocks re-initialization

		await ParseSqlStatementsFile(stream, SqlStatementsFileType.Unknown);

		SxmDatabaseOptions.AddDatabaseName(databaseOptions, SxmProcessSQLStatements.DatabaseName);
		await SxmDatabase.BuildSchemaAsync();

		_initialized = true;
	}
	finally
	{
		_initGate.Release();
	}
}
```

**Proposed:**
```csharp
// New: Track initialized databases instead of single flag
private static readonly ConcurrentBag<string> _initializedDatabases = new();

public static async Task InitializeAsync(
	Stream stream, 
	SxmDatabaseOptions? databaseOptions = null)
{
	await _initGate.WaitAsync();
	try
	{
		// Parse statements file (supports single or multi-database format)
		await ParseSqlStatementsFile(stream, SqlStatementsFileType.Unknown);

		// Get all databases from parsed file
		var databases = SxmProcessSQLStatements.GetAllDatabases();

		if (databases.Count == 0)
			throw new InvalidOperationException("No databases found in SQL statements file");

		// Validate exactly one default database
		var defaultDbs = databases.Where(d => d.IsDefault).ToList();
		if (defaultDbs.Count == 0)
			throw new InvalidOperationException("No default database specified");
		if (defaultDbs.Count > 1)
			throw new InvalidOperationException($"Multiple default databases specified: {string.Join(", ", defaultDbs.Select(d => d.Name))}");

		// Initialize each database
		foreach (var dbDescriptor in databases)
		{
			// Skip if already initialized
			if (_initializedDatabases.Contains(dbDescriptor.Name))
				continue;

			// Associate options with this database
			SxmDatabaseOptions.AddDatabaseName(databaseOptions, dbDescriptor.Name);

			// Build schema for this database
			await BuildSchemaAsync(dbDescriptor.Name);

			// Mark as initialized
			_initializedDatabases.Add(dbDescriptor.Name);
		}
	}
	finally
	{
		_initGate.Release();
	}
}
```

---

#### C. Statement Name Collision Handling

**Problem:** Two databases might have statements with the same name

**Option 1: Require Unique Names Globally (Simpler)**
```csharp
// Validation during parsing
if (InsertStatements.ContainsKey(statementName))
	throw new InvalidOperationException(
		$"Statement name '{statementName}' is already registered. " +
		$"Statement names must be unique across all databases.");
```

**Option 2: Namespace by Database (More Flexible)**
```csharp
// Statement names become "database.statementName"
InsertStatements.Add("main_db.insertUser", definition);
InsertStatements.Add("analytics_db.insertUser", definition);

// Usage:
await SxmStatement.InsertAsync<User>("main_db.insertUser", user);
```

**Recommendation:** Start with **Option 1** (unique names) for simplicity, add Option 2 later if needed.

---

### 3. Usage Examples

#### Multi-Database SQL Statements File

```json
{
  "databases": [
	{
	  "database": "app_db",
	  "isDefault": true,

	  "insert": [
		{
		  "Statement Name": "insertUser",
		  "Table Name": "Users",
		  "Statement": "INSERT INTO Users (name) VALUES (@name)"
		}
	  ],

	  "select": [
		{
		  "Statement Name": "getUserById",
		  "Table Name": "Users",
		  "Statement": "SELECT * FROM Users WHERE id = @p0"
		}
	  ]
	},
	{
	  "database": "analytics_db",
	  "isDefault": false,

	  "insert": [
		{
		  "Statement Name": "logEvent",
		  "Table Name": "Events",
		  "Statement": "INSERT INTO Events (type, data) VALUES (@type, @data)"
		}
	  ],

	  "select": [
		{
		  "Statement Name": "getRecentEvents",
		  "Table Name": "Events",
		  "Statement": "SELECT * FROM Events WHERE timestamp > @p0"
		}
	  ]
	}
  ]
}
```

#### Initialization

```csharp
// In MauiProgram.cs
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		// Initialize both databases from single file
		InitializeDatabaseAsync().GetAwaiter().GetResult();

		return builder.Build();
	}

	private static async Task InitializeDatabaseAsync()
	{
		using Stream stream = await FileSystem.OpenAppPackageFileAsync(
			"SqlStatements.json"
		);

		var options = new SxmDatabaseOptions
		{
			ForeignKeys = true,
			JournalModeOption = SxmJournalMode.Wal
		};

		// ✅ This now initializes BOTH databases
		await SxmDatabase.InitializeAsync(stream, options);

		// Register entities for app_db (default)
		await SxmDatabase.RegisterEntitiesAsync(
			typeof(User),
			typeof(Order)
		);

		// Register entities for analytics_db
		await SxmDatabase.RegisterEntitiesAsync(
			typeof(AnalyticsEvent)
		);
	}
}
```

#### Entity Usage

```csharp
// User entity - uses default database (app_db)
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
	public string? Name { get; set; }
}

// AnalyticsEvent entity - explicitly uses analytics_db
[Table(IsColumnAttributeRequired = false, Database = "analytics_db")]
public class AnalyticsEvent : SxmEntity
{
	public string? Type { get; set; }
	public string? Data { get; set; }
}

// Usage
var user = new User { Name = "John" };
await user.SaveAsync();  // ✅ Saves to app_db (default)

var event = new AnalyticsEvent { Type = "login", Data = "{}" };
await event.SaveAsync();  // ✅ Saves to analytics_db (explicit)
```

#### Named Statement Usage

```csharp
// Uses statement from app_db (default)
var users = await SxmStatement.SelectAsync<User>(
	"getUserById",
	new List<object> { 42 }
);

// Uses statement from analytics_db (explicit database parameter)
var events = await SxmStatement.SelectAsync<AnalyticsEvent>(
	"getRecentEvents",
	new List<object> { DateTime.UtcNow.AddDays(-7) },
	"analytics_db"  // ← Specify database
);
```

---

## Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1)

**Tasks:**
1. ✅ Modify `SxmProcessSQLStatements` to support list of databases
2. ✅ Add new JSON schema classes for multi-database format
3. ✅ Implement auto-detection (single vs multi-database format)
4. ✅ Update parser to handle both formats
5. ✅ Add unit tests for backward compatibility

**Files to Modify:**
- `SxmProcessSQLStatements.cs`
- `SxmSerialization.cs` (JSON/XML schema classes)

---

### Phase 2: Initialization Flow (Week 2)

**Tasks:**
1. ✅ Replace `_initialized` flag with `_initializedDatabases` collection
2. ✅ Modify `InitializeAsync()` to iterate databases
3. ✅ Update `BuildSchemaAsync()` to accept database name
4. ✅ Ensure `SxmDatabaseOptions` applies per-database
5. ✅ Handle default database validation
6. ✅ Add integration tests

**Files to Modify:**
- `SxmDatabase.cs`
- `SxmDatabaseDescriptor.cs`

---

### Phase 3: Statement Registration (Week 2-3)

**Tasks:**
1. ✅ Decide on statement name collision strategy
2. ✅ Update statement registration to handle multiple databases
3. ✅ Ensure triggers are properly scoped by database
4. ✅ Update error messages with database context
5. ✅ Add validation for duplicate statement names

**Files to Modify:**
- `SxmSqlStatements.cs`

---

### Phase 4: Documentation & Testing (Week 3)

**Tasks:**
1. ✅ Update SQL Statements file documentation
2. ✅ Update initialization documentation
3. ✅ Create migration guide for existing apps
4. ✅ Add comprehensive integration tests
5. ✅ Update example apps

**Files to Create/Modify:**
- `docs/MultiDatabaseSupport.md` (new)
- `docs/SqlStatementsFile.md` (update)
- `docs/DatabaseInitialization.md` (update)
- `docs/MigrationGuide.md` (new)

---

## Backward Compatibility Strategy

### Auto-Detection Algorithm

```csharp
internal static bool Parse(Stream stream, SqlStatementsFileType fileType)
{
	// ... existing code ...

	// After deserializing JSON/XML:
	if (rootJson.databases != null && rootJson.databases.Length > 0)
	{
		// NEW: Multi-database format
		ProcessMultiDatabaseJson(rootJson);
	}
	else if (!string.IsNullOrEmpty(rootJson.database))
	{
		// LEGACY: Single-database format
		ProcessSingleDatabaseJson(rootJson);
	}
	else
	{
		throw new ArgumentException(
			"Invalid SQL statements file: must contain either 'database' or 'databases' property");
	}
}
```

### Migration Path

**Existing apps (no changes needed):**
```json
{
  "database": "myapp_db",
  "isDefault": true,
  "insert": [ ... ]
}
```
✅ Continues to work unchanged

**New apps (multi-database):**
```json
{
  "databases": [
	{ "database": "db1", "isDefault": true, "insert": [...] },
	{ "database": "db2", "isDefault": false, "insert": [...] }
  ]
}
```
✅ New format enables multiple databases

---

## Risks & Mitigation

### Risk 1: Breaking Changes
**Impact:** Existing code might break  
**Mitigation:** 
- ✅ Maintain backward compatibility with auto-detection
- ✅ Extensive testing of legacy format
- ✅ Provide migration tool/script

### Risk 2: Statement Name Collisions
**Impact:** Same statement name in multiple databases  
**Mitigation:**
- ✅ Start with global uniqueness requirement
- ✅ Clear error messages
- ✅ Document best practices (prefix by purpose)

### Risk 3: Complexity
**Impact:** Harder to understand/debug  
**Mitigation:**
- ✅ Excellent documentation
- ✅ Clear error messages with database context
- ✅ Examples in docs

### Risk 4: Performance
**Impact:** Initialization time increases  
**Mitigation:**
- ✅ Parallel schema building where possible
- ✅ Continue connection pooling
- ✅ Benchmark before/after

---

## Testing Strategy

### Unit Tests
- ✅ Parse single-database format (backward compat)
- ✅ Parse multi-database format
- ✅ Detect invalid formats
- ✅ Validate default database rules
- ✅ Test statement name collision detection

### Integration Tests
- ✅ Initialize multiple databases
- ✅ Create entities in different databases
- ✅ Execute statements across databases
- ✅ Test transactions (same database only)
- ✅ Test entity registration per database

### Performance Tests
- ✅ Initialization time: 1 db vs 3 dbs
- ✅ Connection pool behavior
- ✅ Concurrent operations across databases

---

## Recommended Implementation Order

### Priority 1: Must-Have (Minimal Viable Multi-DB)
1. ✅ Multi-database SQL Statements file format
2. ✅ Parser changes (`SxmProcessSQLStatements`)
3. ✅ Initialization changes (`SxmDatabase.InitializeAsync`)
4. ✅ Backward compatibility
5. ✅ Basic documentation

### Priority 2: Should-Have (Production Ready)
6. ✅ Comprehensive error messages
7. ✅ Statement name collision handling
8. ✅ Integration tests
9. ✅ Migration guide

### Priority 3: Nice-to-Have (Polish)
10. ⚠️ Statement namespacing (db.statementName)
11. ⚠️ Per-database options override
12. ⚠️ Cross-database query support
13. ⚠️ Database aliasing

---

## Code Readiness Assessment

### ✅ Well-Positioned Areas (70%)

1. **Connection Infrastructure** - Already accepts database names everywhere
2. **Entity System** - `[Table(Database = "...")]` attribute exists
3. **Options System** - Already associates options per database
4. **Trigger System** - Already keyed by database name
5. **Error Handling** - Database context in exceptions

### ⚠️ Needs Modification (30%)

1. **Parser** - Single-database assumption
2. **Initialization** - Single-pass, `_initialized` flag
3. **Statement Registration** - No collision detection
4. **Documentation** - Single-database examples

---

## Conclusion

**Overall Assessment:** ✅ **SQLiteXM is WELL-POSITIONED for multi-database support**

**Key Strengths:**
- 70-80% of infrastructure already supports multiple databases
- Entity system already has database specification capability
- Connection/statement APIs already accept database parameters
- Good separation of concerns

**Recommended Approach:**
1. Start with multi-database SQL Statements file format
2. Maintain backward compatibility via auto-detection
3. Require globally unique statement names initially
4. Implement in 3-week sprint following the roadmap above

**Expected Outcome:**
- ✅ Clean, backward-compatible API
- ✅ Minimal breaking changes
- ✅ Clear migration path for existing apps
- ✅ Foundation for future enhancements

---

## Next Steps

1. **Validate this design** with stakeholders
2. **Create prototype** of new SQL Statements format
3. **Implement Phase 1** (parser changes)
4. **Test backward compatibility** thoroughly
5. **Iterate** based on feedback

---

**Document Version:** 1.0  
**Date:** 2026  
**Author:** SQLiteXM Analysis
