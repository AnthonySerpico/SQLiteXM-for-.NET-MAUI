# DDL Removal Verification Report

**Date:** January 2025  
**Objective:** Verify complete removal of DDL (Table/Index/Alter) support from SqlStatements files  
**Outcome:** ⚠️ **INCOMPLETE** - 3 issues found

---

## ✅ **What Was Correctly Removed:**

### 1. **`SxmProcessSQLStatements.cs` - Parsing Logic**
**Lines 209-219 (JSON)** and **Lines 255-267 (XML)** ✅ **REMOVED**

**Before:**
```csharp
if (rootJson?.Table != default)
	foreach (Dictionary<string, string> tableEntry in rootJson.Table)
		SxmSqlStatements.AddTableDefinition(...);

if (rootJson?.Index != default) ...
if (rootJson?.Alter != default) ...
```

**After:**
```csharp
// ✅ Clean - only Insert/Select/Update/Delete/Trigger remain
if (rootJson?.Delete != default)
	foreach (Dictionary<string, string> deleteEntry in rootJson.Delete)
		SxmSqlStatements.AddDeleteDefinition(...);
```

**Status:** ✅ **COMPLETE**

---

### 2. **`SxmDatabase.cs` - DDL Processing in `BuildSchemaAsync()`**
**Lines 407-447** ✅ **REMOVED**

**Before:**
```csharp
// INTERNAL IMPLEMENTATION DETAIL: ... DO NOT use undocumented configuration...
if (SxmSqlStatements.TableCreateStatements != default(...) && ...)
{
	Hashtable tableNamesMap = new();
	foreach (string DatabaseNameTableName in SxmSqlStatements.TableCreateStatements.Keys)
	{
		// ... complex DDL processing
	}
}
```

**After:**
```csharp
await CreateSystemTablesAsync(databaseName).ConfigureFalse();
sxmConnection = new SxmConnection(databaseName, shared: true);
await DropTriggersAsync(sxmConnection, new List<string>()).ConfigureFalse();
await StoreDbVersionNumberAsync(...).ConfigureFalse();
await SxmAssociationMapper.InitializeAssociationsAsync(databaseName).ConfigureFalse();
```

**Status:** ✅ **COMPLETE** - Clean, minimal initialization flow

---

### 3. **`SxmSerialization.cs` - Schema Classes**
**Table/Index/Alter classes** ✅ **NOT PRESENT**

Checked `RootJson` and `RootXml`:
```csharp
public class RootJson
{
	public string? database { get; set; }
	public bool isDefault { get; set; }
	public long version { get; set; }

	// ✅ Only runtime statement collections remain
	public List<Dictionary<string, string>>? Insert { get; set; }
	public List<Dictionary<string, string>>? Select { get; set; }
	public List<Dictionary<string, string>>? Update { get; set; }
	public List<Dictionary<string, string>>? Delete { get; set; }
	public List<Dictionary<string, string>>? Trigger { get; set; }

	// ✅ NO Table, Index, or Alter properties
}
```

**Status:** ✅ **COMPLETE**

---

## ❌ **What Still Needs to Be Removed:**

### **Issue 1: `SxmSqlStatements.cs` - DDL Dictionaries and Methods**

#### **Line 14 - `TableCreateStatements` Dictionary**
```csharp
internal static Dictionary<string, TableDefinition>? TableCreateStatements = new Dictionary<string, TableDefinition>();
```
❌ **SHOULD BE REMOVED** - This is the core DDL storage that should not exist

---

#### **Lines 165-188 - `AddTableDefinition()` Methods**
```csharp
internal static void AddTableDefinition(string dbAndTableName, string tableSQL)
{
	dbAndTableName = dbAndTableName.Trim();
	tableSQL = tableSQL.Trim();
	AddTableDefinition(dbAndTableName, tableSQL, SxmDefines.NoCloudSync);
}

internal static void AddTableDefinition(string dbAndTableName, string tableSQL, int cloudPush)
{
	dbAndTableName = dbAndTableName.Trim();
	tableSQL = tableSQL.Trim();

	if (TableCreateStatements == null)
		TableCreateStatements = new Dictionary<string, TableDefinition>();

	TableCreateStatements.Add(dbAndTableName, new TableDefinition(tableSQL, cloudPush));
}
```
❌ **SHOULD BE REMOVED** - No longer called from anywhere

---

#### **Lines 193-200 - `RemoveTableDefinitions()` Method**
```csharp
internal static void RemoveTableDefinitions()
{
	if (TableCreateStatements != default(Dictionary<string, TableDefinition>))
	{
		TableCreateStatements.Clear();
		TableCreateStatements = default(Dictionary<string, TableDefinition>);
	}
}
```
❌ **SHOULD BE REMOVED** - No longer needed

---

#### **Lines 210-217 - `ClearStatementTables()` References**
```csharp
internal static void ClearStatementTables()
{
	if (TableCreateStatements != default(Dictionary<string, TableDefinition>))
	{
		TableCreateStatements?.Clear();
		TableCreateStatements = default(Dictionary<string, TableDefinition>)!;
	}
}
```
❌ **SHOULD BE UPDATED** - Remove `TableCreateStatements` references

---

#### **Lines 224-238 - `ResetForTesting()` References**
```csharp
#if DEBUG
internal static void ResetForTesting()
{
	TableCreateStatements?.Clear();  // ❌ Remove
	TriggerStatements?.Clear();
	InsertStatements?.Clear();
	SelectStatements?.Clear();
	UpdateStatements?.Clear();
	DeleteStatements?.Clear();

	TableCreateStatements = new Dictionary<string, TableDefinition>();  // ❌ Remove
	TriggerStatements = new ConcurrentDictionary<string, List<TriggerDefinition>>(StringComparer.Ordinal);
	InsertStatements = new Dictionary<string, InsertDefinition>();
	SelectStatements = new Dictionary<string, SelectDefinition>();
	UpdateStatements = new Dictionary<string, UpdateDefinition>();
	DeleteStatements = new Dictionary<string, DeleteDefinition>();
}
#endif
```
❌ **SHOULD BE UPDATED** - Remove `TableCreateStatements` lines

---

### **Issue 2: `SxmDatabase.cs` - Orphaned DDL Helper Methods**

These methods are now **dead code** because the calling code in `BuildSchemaAsync()` was removed:

#### **Lines 579-617 - `CreateTableAsync()`**
```csharp
internal static async Task CreateTableAsync(string? databaseName, string tableName)
{
	if (databaseName == null)
		return;

	string[] parts = { databaseName, tableName };
	string key = string.Format("{0}.{1}", databaseName, tableName);

	try
	{
		SxmConnection? sxmConnection = new SxmConnection(databaseName);
		if (!await DoesTableExistAsync(tableName, sxmConnection).ConfigureFalse())
		{
			Hashtable tableNamesMap = new Hashtable();
			TableDefinition? tableDefinition = SxmSqlStatements.TableCreateStatements![key] as TableDefinition;  // ❌ Uses DDL

			await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
			{
				await sxmTransaction.ExecuteTableStatementAsync(tableDefinition.TableSQL).ConfigureFalse();
				// ...
			}
		}
	}
	// ...
}
```
❌ **SHOULD BE REMOVED** - Uses `TableCreateStatements`, no longer called

**Search Result:** 0 references to `CreateTableAsync` in codebase (confirmed dead code)

---

#### **Lines 900-910 - `InsertIntoSystemCloudSyncDescriptorAsync()`**
```csharp
private static async Task InsertIntoSystemCloudSyncDescriptorAsync(string key, string databaseName, string tableName, SxmUTransaction sxmTransaction)
{
	if (SxmSqlStatements.TableCreateStatements == null || !SxmSqlStatements.TableCreateStatements.TryGetValue(key, out TableDefinition? tableDefinition) || tableDefinition == null)
		throw new InvalidOperationException($"Table definition not found for key: {key}");

	List<object> parameterValues = new List<object>();
	parameterValues.Add(databaseName);
	parameterValues.Add(tableName);
	parameterValues.Add(tableDefinition.CloudSynch);  // ❌ Uses DDL CloudSynch flag
	await sxmTransaction.ExecuteSystemUpdateDirectAsync("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues).ConfigureFalse();
}
```
❌ **SHOULD BE REMOVED OR REFACTORED**
- Currently references `TableCreateStatements`
- Called from `CreateTableAsync()` (dead code) and `ApplyCreateTableStatementAsync()` (removed)
- Likely dead code now

---

#### **Lines 933-965 - `CreateCloudSynchTriggersAsync()`**
```csharp
private static async Task CreateCloudSynchTriggersAsync(string key, SxmUTransaction sxmTransaction)
{
	string[] parts = key.Split('.');
	string databaseName = parts[0];
	string databaseTable = parts[1];

	TableDefinition? tableDefinition = SxmSqlStatements.TableCreateStatements?[key] as TableDefinition;  // ❌ Uses DDL

	if (tableDefinition?.CloudSynch == SxmDefines.CloudSync || tableDefinition?.CloudSynch == SxmDefines.CloudMove)
	{
		// Create cloud sync triggers...
	}
}
```
❌ **SHOULD BE REMOVED** - Uses `TableCreateStatements`, no longer called

**Search Result:** 1 reference in `CreateTableAsync()` (dead code), 0 active callers

---

### **Issue 3: `SxmDatabase.cs` - DDL-Related Helper Methods**

These were used by the removed DDL processing block and are now **orphaned**:

#### **`ApplyCreateTableStatementAsync()`** - **NOT FOUND** ✅ (already removed)
#### **`ApplyDropTableStatementAsync()`** - **NOT FOUND** ✅ (already removed)
#### **`ApplyAlterTableStatementsAsync()`** - **NOT FOUND** ✅ (already removed)
#### **`ApplyIndexTableStatementsAsync()`** - **NOT FOUND** ✅ (already removed)
#### **`ApplyTriggerTableStatementsAsync()`** - **NOT FOUND** ✅ (already removed)
#### **`DoesTableExistAsync(string, SxmConnection, Hashtable)`** - Needs verification

Let me check if the three-parameter `DoesTableExistAsync` is still present:

---

## 📋 **Recommended Action Plan:**

### **Step 1: Remove `TableCreateStatements` Infrastructure**

**File:** `SxmSqlStatements.cs`

1. **Line 14** - Remove field:
```csharp
// DELETE THIS LINE:
internal static Dictionary<string, TableDefinition>? TableCreateStatements = new Dictionary<string, TableDefinition>();
```

2. **Lines 165-188** - Remove both `AddTableDefinition()` overloads

3. **Lines 193-200** - Remove `RemoveTableDefinitions()`

4. **Lines 210-217** - Update `ClearStatementTables()`:
```csharp
internal static void ClearStatementTables()
{
	// ✅ REMOVE all TableCreateStatements references
	// Method can be kept for consistency or removed entirely if empty
}
```

5. **Lines 224-238** - Update `ResetForTesting()`:
```csharp
#if DEBUG
internal static void ResetForTesting()
{
	// TableCreateStatements?.Clear();  ❌ REMOVE
	TriggerStatements?.Clear();
	InsertStatements?.Clear();
	SelectStatements?.Clear();
	UpdateStatements?.Clear();
	DeleteStatements?.Clear();

	// TableCreateStatements = new Dictionary<string, TableDefinition>();  ❌ REMOVE
	TriggerStatements = new ConcurrentDictionary<string, List<TriggerDefinition>>(StringComparer.Ordinal);
	InsertStatements = new Dictionary<string, InsertDefinition>();
	SelectStatements = new Dictionary<string, SelectDefinition>();
	UpdateStatements = new Dictionary<string, UpdateDefinition>();
	DeleteStatements = new Dictionary<string, DeleteDefinition>();
}
#endif
```

---

### **Step 2: Remove Dead Code in `SxmDatabase.cs`**

1. **Lines 579-617** - Remove entire `CreateTableAsync()` method
2. **Lines 900-910** - Remove `InsertIntoSystemCloudSyncDescriptorAsync()`
3. **Lines 933-965** - Remove `CreateCloudSynchTriggersAsync()`

---

### **Step 3: Verify No Remaining References**

After removal, search for:
- `TableCreateStatements`
- `AddTableDefinition`
- `RemoveTableDefinitions`
- `CreateTableAsync`
- `InsertIntoSystemCloudSyncDescriptorAsync`
- `CreateCloudSynchTriggersAsync`

All should return **0 results**.

---

### **Step 4: Final Build & Test**

1. Run full solution build
2. Run all unit tests (especially initialization tests)
3. Verify no compilation errors
4. Check that entity-driven schema creation still works

---

## 🎯 **Summary:**

| Component | Status | Action Needed |
|-----------|--------|---------------|
| **`SxmProcessSQLStatements.cs`** JSON parsing | ✅ Complete | None |
| **`SxmProcessSQLStatements.cs`** XML parsing | ✅ Complete | None |
| **`SxmDatabase.cs`** DDL processing block | ✅ Complete | None |
| **`SxmSerialization.cs`** schema classes | ✅ Complete | None |
| **`SxmSqlStatements.cs`** dictionaries/methods | ❌ Incomplete | Remove 5 items |
| **`SxmDatabase.cs`** orphaned DDL helpers | ❌ Incomplete | Remove 3 methods |

---

## ✅ **After Cleanup, You Will Have:**

1. **Clean SqlStatements file format** - Only runtime queries (Insert/Select/Update/Delete/Trigger)
2. **Single schema source of truth** - Entity attributes via `SxmSchemaRegistration`
3. **No DDL confusion** - Clear architectural intent
4. **Smaller codebase** - ~300-400 lines removed
5. **Easier maintenance** - One less system to support

---

**Next Steps:** Would you like me to proceed with the removal of the remaining DDL infrastructure?
