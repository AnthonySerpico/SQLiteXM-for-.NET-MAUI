# SQLiteXM API Reference

This document lists every public type and member in the SQLiteXM library, organized by what
you use them for. It is a lookup reference, not a tutorial — each entry gives the signature,
a short description, and a pointer to the guide that explains the feature in depth.

All types live in the `SQLiteXM` namespace unless stated otherwise.

---

## Contents

**Initialization and lifecycle**
- [SxmDatabase](#sxmdatabase)
- [SxmDatabaseOptions](#sxmdatabaseoptions)
- [SxmLifecycleManager](#sxmlifecyclemanager)

**Entities and schema**
- [SxmEntity](#sxmentity)
- [Entity attributes](#entity-attributes)
  - [TableAttribute](#tableattribute)
  - [ColumnAttribute](#columnattribute)
  - [NotColumnAttribute](#notcolumnattribute)
  - [RenameAttribute](#renameattribute)
  - [IndexAttribute](#indexattribute)
  - [UniqueIndexAttribute](#uniqueindexattribute)
  - [ForeignKeyAttribute](#foreignkeyattribute)
  - [RequiredNotNullAttribute](#requirednotnullattribute)
  - [TriggerAttribute](#triggerattribute)

**Reading and writing data**
- [SxmTransaction](#sxmtransaction)
- [SxmTable&lt;T&gt;](#sxmtablet)
- [SxmLinqExtensions](#sxmlinqextensions)
  - [Materialization](#materialization)
  - [Element and existence checks](#element-and-existence-checks)
  - [Aggregates](#aggregates)
  - [Dictionaries](#dictionaries)
  - [Bulk insert](#bulk-insert)
  - [Bulk update](#bulk-update)
  - [Bulk delete](#bulk-delete)
- [SxmUpdateSet&lt;T&gt;](#sxmupdatesett)
- [SxmSql](#sxmsql)
<!-- - [SxmStore](#sxmstore) -->

**Errors**
- [SxmException](#sxmexception)
- [SxmExceptionExtensions](#sxmexceptionextensions)

**Enumerations**
- [SxmErrorCode](#sxmerrorcode)
- [DataType](#datatype)
- [ForeignKeyDeleteAction](#foreignkeydeleteaction)
- [SxmJournalMode](#sxmjournalmode)
- [SxmSynchronousMode](#sxmsynchronousmode)
- [SxmTempStore](#sxmtempstore)
- [CheckPointConnection](#checkpointconnection)

**Delegates**
- [ConnectionOpenedInterceptor](#connectionopenedinterceptor)
- [ConnectionClosedInterceptor](#connectionclosedinterceptor)
- [SchemaSynchronizedInterceptor](#schemasynchronizedinterceptor)
- [SxmSchemaSynchronizedEventArgs](#sxmschemasynchronizedeventargs)

---

## Initialization and lifecycle

### SxmDatabase

```csharp
public static class SxmDatabase
```

Entry point for configuring and initializing SQLiteXM. Most applications call
`StartInitialization` once at startup and `EnsureReadyAsync` before first use.

📖 Guides: [Getting Started](./getting-started.md), [Basic Initialization](./basic-initialization.md),
[Advanced Initialization Patterns](./advanced-initialization-patterns.md)

| Member | Description |
|---|---|
| `static void StartInitialization(Stream sqlStatementsStream, SxmDatabaseOptions? databaseOptions, params Type[] entities)` | Starts database initialization and entity registration on a background task and returns immediately. SQLiteXM takes ownership of the stream and disposes it when initialization finishes. Call once, early in startup. |
| `static Task EnsureReadyAsync()` | Returns a task that completes when the work started by `StartInitialization` has finished, or faults with the exception that caused initialization to fail. Never blocks the calling thread; safe to await from UI code. |
| `static Task InitializeAsync(Stream stream, SxmDatabaseOptions? databaseOptions = null)` | Lower-level alternative to `StartInitialization`. Reads `SqlStatements.json`, applies options, and prepares the database(s). Must be followed by `RegisterEntitiesAsync`. |
| `static Task RegisterEntitiesAsync(params Type[] entityTypes)` | Registers entity types and creates or evolves their tables, indexes, triggers, and constraints. |
| `static Task RegisterEntityAsync<TEntity>()` where `TEntity : SxmEntity` | Generic, single-type form of `RegisterEntitiesAsync`. |

> ✏️ **Note:** `StartInitialization` and `RegisterEntitiesAsync` are annotated with
> `[RequiresUnreferencedCode]`. Entity preservation under trimming and Native AOT is guaranteed
> by annotations on `SxmEntity`, so you may suppress `IL2026` at the call site. See
> [Basic Initialization](./basic-initialization.md) for details.

---

### SxmDatabaseOptions

```csharp
public sealed class SxmDatabaseOptions
```

Immutable configuration object passed to `StartInitialization` or `InitializeAsync`. All
properties are nullable and set via object-initializer syntax; `null` means "leave the SQLite
default in place."

📖 Guide: [Applying Database Options](./database-configuration-options.md)

| Property | Type | Description |
|---|---|---|
| `JournalModeOption` | `SxmJournalMode?` | SQLite journal mode (`PRAGMA journal_mode`). |
| `SynchronousModeOption` | `SxmSynchronousMode?` | Durability vs. speed trade-off (`PRAGMA synchronous`). |
| `ForeignKeys` | `bool?` | Enables foreign-key enforcement (`PRAGMA foreign_keys`). |
| `BusyTimeout` | `long?` | Milliseconds to wait on a locked database before failing (`PRAGMA busy_timeout`). |
| `CacheSize` | `long?` | Page cache size in KB (`PRAGMA cache_size`). |
| `WalAutoCheckpoint` | `long?` | WAL auto-checkpoint threshold in pages (`PRAGMA wal_autocheckpoint`). |
| `TempStore` | `SxmTempStore?` | Where temporary tables and indexes are stored (`PRAGMA temp_store`). |
| `EnableConnectionPooling` | `bool?` | Whether connections are pooled and reused. |
| `EnableLogging` | `bool?` | Whether SQLiteXM writes diagnostic log output. |
| `DefaultTimeout` | `int?` | Default command timeout in seconds. |
| `CheckPointConnection` | `CheckPointConnection?` | When a WAL checkpoint is performed in relation to connection lifetime. |
| `CheckPointWalMaxSize` | `int?` | Maximum WAL size in KB before a checkpoint is triggered (used with `CheckPointConnection.MaxSize`). |
| `DatabaseFolderOverride` | `string?` | Folder in which to create the database file, overriding the platform default. |

| Method | Description |
|---|---|
| `void OnConnectionOpened(ConnectionOpenedInterceptor connectionOpenedInterceptor)` | Registers a callback that receives the raw `SqliteConnection` each time a connection is opened. Useful for verifying PRAGMA settings or applying custom ones. |
| `void OnConnectionClosed(ConnectionClosedInterceptor connectionClosedInterceptor)` | Registers a callback invoked each time a connection is closed. |
| `void OnSchemaSynchronized(SchemaSynchronizedInterceptor schemaSynchronizedInterceptor)` | Registers a callback invoked after each entity table is created or synchronized. It receives the exact DDL statements SQLiteXM executed (CREATE TABLE, ADD/RENAME/DROP COLUMN, CREATE/DROP INDEX, CREATE TRIGGER). Useful for migration audit logs and diagnostics. |

---

### SxmLifecycleManager

```csharp
public static class SxmLifecycleManager
```

Optional integration with MAUI's application lifecycle. Call `OnSleep` and `OnResume` from your
`App` class to reduce the chance of a database write being interrupted when the OS suspends the app.

📖 Guide: [Application Lifecycle](./application-lifecycle.md)

| Member | Description |
|---|---|
| `static TimeSpan SuspendGracePeriod { get; set; }` | How long `OnSleep` waits for in-flight operations to finish before fully blocking. Default: **5 seconds**. Throws `ArgumentOutOfRangeException` if set to a negative value. |
| `static void OnSleep()` | Blocks new database operations, then waits for `SuspendGracePeriod`. Blocks the calling thread for the duration of the grace period. No-op if already suspended. |
| `static void OnResume()` | Lifts the block applied by `OnSleep`. No-op if not suspended. |

While suspended, any attempt to open a connection throws `SxmException` with
`ErrorCode == SxmErrorCode.ConnectionBlockedBackgrounded`.

---

## Entities and schema

### SxmEntity

```csharp
public abstract class SxmEntity : INotifyPropertyChanged
```

Base class for every entity. Deriving from `SxmEntity` gives a class an auto-managed primary key,
self-persistence methods, and change notification for MAUI data binding.

📖 Guides: [Defining Entities](./defining-entities.md), [Working with Data](./working-with-data.md),
[INotifyPropertyChanged](./inotifypropertychanged.md)

| Member | Description |
|---|---|
| `virtual long id { get; }` | Primary key, mapped to SQLite's `INTEGER PRIMARY KEY` column named `id`. `0` means the entity has not yet been saved. Assigned by the database on insert; the setter is internal. |
| `virtual Guid? synchId { get; }` | Optional synchronization identifier stored as a BLOB. Managed by SQLiteXM; read-only to application code. |
| `Task SaveAsync()` | Inserts the row if `id == 0`, otherwise performs an in-place `UPDATE`. Runs standalone as its own transaction, or enlists in an active `SxmTransaction`. |
| `Task DeleteAsync()` | Deletes the row identified by `id`. No-op if the row does not exist. Enlists in an active `SxmTransaction` if one is open. |
| `event PropertyChangedEventHandler? PropertyChanged` | Raised when a mapped property changes. Enables direct binding of entities in MAUI UI. |

---

### Entity attributes

Attributes control how an entity class is mapped to a SQLite table.

📖 Guide: [Defining Entities](./defining-entities.md)

#### TableAttribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class TableAttribute : LinqToDB.Mapping.TableAttribute
```

Marks a class as a database table. Inherits from LINQ to DB's `TableAttribute`, so the commonly
used options come from the base class.

| Constructor / Property | Description |
|---|---|
| `TableAttribute()` | Maps to a table with the same name as the class. |
| `TableAttribute(string tableName)` | Maps to the named table. |
| `Database` (inherited) | Name of the database (from `SqlStatements.json`) this table belongs to. Omit to use the default database. |
| `IsColumnAttributeRequired` (inherited) | When `false`, every public property is mapped as a column without needing `[Column]`. When `true` (the LINQ to DB default), only `[Column]`-decorated properties are mapped. |

#### ColumnAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute : LinqToDB.Mapping.ColumnAttribute
```

Maps a property to a column and optionally overrides the storage type.

| Member | Description |
|---|---|
| `ColumnAttribute()` | Maps the property using default type mapping. |
| `new DataType DataType { get; set; }` | Overrides the SQLite storage type. For example, `DataType.Text` stores a `DateTime` as an ISO-8601 string instead of ticks. See [DataType](#datatype) and [Supported Data Types](./supported-data-types.md). |

#### NotColumnAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class NotColumnAttribute : LinqToDB.Mapping.NotColumnAttribute
```

Excludes a public property from column mapping. Useful when `IsColumnAttributeRequired = false`
and you have computed or UI-only properties.

#### RenameAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class RenameAttribute : Attribute
```

Records the previous name(s) of a column so SQLiteXM can rename it during schema evolution instead
of dropping and recreating it.

📖 Guide: [Schema Evolution](./schema-evolution.md)

| Member | Description |
|---|---|
| `RenameAttribute(string oldName)` | One previous name. |
| `RenameAttribute(params string[] oldNames)` | Several previous names, for a column renamed more than once. |
| `string[] OldNames { get; }` | The recorded previous names. |

#### IndexAttribute

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class IndexAttribute : Attribute
```

Creates a non-unique index. On a property it indexes that column; on a class it creates a
composite index over the named columns.

| Member | Description |
|---|---|
| `IndexAttribute()` | Property-level: index this column. |
| `IndexAttribute(string indexField)` | Class-level: single-column index on the named property. |
| `IndexAttribute(params string[] indexFields)` | Class-level: composite index over the named properties, in order. |
| `string[] IndexFields { get; set; }` | Columns included in the index. |
| `string IndexName { get; set; }` | Optional explicit index name. Generated if empty. |

#### UniqueIndexAttribute

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public class UniqueIndexAttribute : Attribute
```

Identical to `IndexAttribute` but creates a `UNIQUE` index. Same constructors and properties.

#### ForeignKeyAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class ForeignKeyAttribute : Attribute
```

Declares that a property is a foreign key referencing another table's `id` column.

📖 Guide: [Loading Related Data](./loading-related-data.md)

| Member | Description |
|---|---|
| `ForeignKeyAttribute()` | Foreign key with the table name to be set via `ForeignTable`. |
| `ForeignKeyAttribute(string foreignTable)` | Foreign key referencing the named table. Typically `nameof(ParentEntity)`. |
| `string ForeignTable { get; set; }` | The referenced table name. |
| `ForeignKeyDeleteAction OnDelete { get; set; }` | What happens to this row when the referenced parent row is deleted. Default: `None`. See [ForeignKeyDeleteAction](#foreignkeydeleteaction). |

#### RequiredNotNullAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class RequiredNotNullAttribute : Attribute
```

Adds a `NOT NULL` constraint to the column and supplies a default so existing rows remain valid
when the column is added to an existing table.

| Member | Description |
|---|---|
| `RequiredNotNullAttribute()` | `NOT NULL` with no explicit default. |
| `RequiredNotNullAttribute(object defaultValue)` | `NOT NULL` with the given default. |
| `object? DefaultValue { get; set; }` | The default value applied to existing rows. |

#### TriggerAttribute

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class TriggerAttribute : Attribute
```

Attaches a `CREATE TRIGGER` statement to the entity's table; it is executed during registration.

| Member | Description |
|---|---|
| `TriggerAttribute(string triggerSql)` | The full `CREATE TRIGGER ...` SQL. |
| `string TriggerSql { get; set; }` | The trigger SQL. |

---

## Reading and writing data

### SxmTransaction

```csharp
public class SxmTransaction : IDisposable, IAsyncDisposable
```

A unit of work. Everything executed through an `SxmTransaction` — LINQ, entity `SaveAsync` /
`DeleteAsync`, bulk inserts, and SQL — runs on the same connection inside the same SQLite
transaction. Disposing the instance **commits** if no operation failed and **rolls back** if one did.
Always create it with `await using`.

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	// ...
}
```

📖 Guides: [Working with Data](./working-with-data.md), [LINQ Queries](./linq-queries.md),
[Concurrency](./concurrency.md)

| Member | Description |
|---|---|
| `SxmTransaction(string? databaseName = null)` | Opens a transaction on the named database, or the default database if `null`. If an ambient transaction already exists on the current async flow, this instance joins it instead of creating a new one. Throws `InvalidOperationException` if an ambient transaction exists for a *different* database. |
| `SxmTable<T> GetTable<T>()` where `T : class` | Returns the LINQ-queryable table for entity type `T`, bound to this transaction. |
| `Task<int> BulkInsertAsync<T>(IReadOnlyList<T> entities, int statementCount = 20, CancellationToken cancellationToken = default)` where `T : SxmEntity` | Inserts many new entities using multi-row `INSERT` statements inside this transaction. All entities must have `id == 0`. Returns the number of rows inserted. |
a SQLiteXM operation in the transaction failed or was cancelled. While faulted, writes are skipped, `CommitTransactionAsync` throws, and disposal rolls back. Exceptions from your own code do not set it. Reset to `false` by `RollbackTransactionAsync`. Nested transactions share the same value. See [Advanced Transaction Processing](./advanced-transaction-processing.md). |
| `Task CommitTransactionAsync()` | Optionally commits early while keeping the connection. Subsequent writes start a new transaction. Throws `InvalidOperationException` if a previous operation on this context failed — call `RollbackTransactionAsync` instead. |
| `Task RollbackTransactionAsync()` | Discards uncommitted work and clears the faulted state (`IsFaulted` becomes `false`) so the context can be reused. |
| `ValueTask DisposeAsync()` | Commits or rolls back (see above), then releases the connection. Preferred over `Dispose()`. |
| `void Dispose()` | Synchronous equivalent of `DisposeAsync`. |

**SQL inside the transaction**

Eight `RunStatementAsync` overloads mirror those on `SxmSql` but run inside this transaction. `sqlOrStatementName` may be embedded SQL or the name of a statement defined in `SqlStatements.json`.

| Overload | Returns |
|---|---|
| `RunStatementAsync(string sqlOrStatementName)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync<T>(string sqlOrStatementName, T userObjectParameters)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync(string sqlOrStatementName, List<object> sqlStatementParameters)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName)` | `Task<List<TResult>>` |
| `RunStatementAsync<T, TResult>(string sqlOrStatementName, T userObjectParameters)` | `Task<List<TResult>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters)` | `Task<List<TResult>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName, List<object> sqlStatementParameters)` | `Task<List<TResult>>` |

`TResult` must be a class with a parameterless constructor; result columns are mapped to its
public properties by name. The `T userObjectParameters` overloads take any object whose public
properties become named parameters (`@PropertyName`). `List<object>` supplies positional
parameters (`@p0`, `@p1`, …).

---

### SxmTable&lt;T&gt;

```csharp
public sealed class SxmTable<T> : IQueryable<T> where T : class
```

The LINQ root for an entity type, obtained from `SxmTransaction.GetTable<T>()`. Standard LINQ
operators (`Where`, `OrderBy`, `Select`, `Join`, `GroupBy`, …) work on it directly; the async
extension methods in [SxmLinqExtensions](#sxmlinqextensions) execute the query.

📖 Guides: [LINQ Queries](./linq-queries.md), [Loading Related Data](./loading-related-data.md)

| Member | Description |
|---|---|
| `SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty?>> navigationProperty)` | Eagerly loads the related entity for a navigation property backed by `[ForeignKey]`. Throws `ArgumentException` if the entity has more than one foreign key to the same target table (use a join instead). |
| `SxmTable<T> LoadWith(params Expression<Func<T, object?>>[] navigationProperties)` | Same, for several navigation properties at once. |
| `Type ElementType`, `Expression Expression`, `IQueryProvider Provider` | `IQueryable` plumbing. |
| `IEnumerator<T> GetEnumerator()` | Synchronous enumeration. Prefer the async materialization methods. |

> ✏️ The public constructor `SxmTable(IQueryable<T> inner, SxmTransaction? context = null)` exists
> for infrastructure use. Application code should obtain tables from `GetTable<T>()`.

---

### SxmLinqExtensions

```csharp
public static class SxmLinqExtensions
```

Async execution methods for `SxmTable<T>` and `IQueryable<T>`. Every method below has two
overloads — one on `SxmTable<T>` and one on `IQueryable<T>` — so it works whether you call it on
the table directly or after `Where`, `OrderBy`, `Select`, etc. All accept an optional
`CancellationToken cancellationToken = default` as the last parameter (omitted from the tables for
brevity).

📖 Guide: [LINQ Queries](./linq-queries.md)

#### Materialization

| Method | Returns | Description |
|---|---|---|
| `ToListAsync<T>()` | `Task<List<T>>` | All rows as a list. |
| `ToArrayAsync<T>()` | `Task<T[]>` | All rows as an array. |

#### Element and existence checks

| Method | Returns | Description |
|---|---|---|
| `FirstAsync<T>()` | `Task<T>` | First row; throws if none. |
| `FirstAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<T>` | First row matching the predicate; throws if none. |
| `FirstOrDefaultAsync<T>()` | `Task<T?>` | First row, or `null`. |
| `FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<T?>` | First match, or `null`. |
| `SingleAsync<T>()` | `Task<T>` | The only row; throws if zero or more than one. |
| `SingleAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<T>` | The only match; throws if zero or more than one. |
| `SingleOrDefaultAsync<T>()` | `Task<T?>` | The only row, or `null`; throws if more than one. |
| `SingleOrDefaultAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<T?>` | The only match, or `null`; throws if more than one. |
| `AnyAsync<T>()` | `Task<bool>` | `true` if any rows exist. |
| `AnyAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<bool>` | `true` if any row matches. |
| `AllAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<bool>` | `true` if every row matches. |
| `ContainsAsync<T>(T item)` | `Task<bool>` | `true` if the query contains the given entity. |

#### Aggregates

| Method | Returns | Description |
|---|---|---|
| `CountAsync<T>()` | `Task<int>` | Row count. |
| `CountAsync<T>(Expression<Func<T, bool>> predicate)` | `Task<int>` | Count of matching rows. |
| `LongCountAsync<T>()` | `Task<long>` | Row count as `long`. |
| `MaxAsync<T, TResult>(Expression<Func<T, TResult>> selector)` | `Task<TResult?>` | Maximum of the selected value. |
| `MinAsync<T, TResult>(Expression<Func<T, TResult>> selector)` | `Task<TResult?>` | Minimum of the selected value. |
| `SumAsync<T>(Expression<Func<T, int>> selector)` | `Task<int>` | Sum. Overloads exist for `int`, `int?`, `long`, `long?`, `float`, `float?`, `double`, `double?`, `decimal`, `decimal?`, each returning the matching type. |
| `AverageAsync<T>(Expression<Func<T, double>> selector)` | `Task<double>` | Average. Overloads exist for `double`, `double?`, `float`, `float?`, `decimal`, `decimal?`, each returning the matching type. |

#### Dictionaries

| Method | Returns | Description |
|---|---|---|
| `ToDictionaryAsync<T, TKey>(Expression<Func<T, TKey>> keySelector)` | `Task<Dictionary<TKey, T>>` | Rows keyed by the selected value. |
| `ToDictionaryAsync<T, TKey, TElement>(Expression<Func<T, TKey>> keySelector, Expression<Func<T, TElement>> elementSelector)` | `Task<Dictionary<TKey, TElement>>` | Projected values keyed by the selected key. |

#### Bulk insert

| Method | Returns | Description |
|---|---|---|
| `BulkInsertAsync<T>(this SxmTable<T> table, IReadOnlyList<T> entities, int batchRows = 20)` where `T : SxmEntity` | `Task<int>` | Inserts many new entities inside the table's transaction using multi-row `INSERT`. Equivalent to `SxmTransaction.BulkInsertAsync`. `SxmTable<T>` only. |

#### Bulk update

`Set(...)` begins a bulk `UPDATE` that is executed by `UpdateAsync()` on the returned
[SxmUpdateSet&lt;T&gt;](#sxmupdatesett). Chain `Where` before `Set` to limit the affected rows.

| Method | Returns | Description |
|---|---|---|
| `Set<T, TProp>(Expression<Func<T, TProp>> setter, TProp value)` | `SxmUpdateSet<T>` | Set a column to a constant. |
| `Set<T, TProp>(Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)` | `SxmUpdateSet<T>` | Set a column from an expression over the row (e.g. `o => o.Amount * 1.1m`). |

```csharp
int updated = await ctx.GetTable<Order>()
	.Where(o => o.Status == "Cancelled")
	.Set(o => o.Amount, 0m)
	.UpdateAsync();
```

#### Bulk delete

| Method | Returns | Description |
|---|---|---|
| `DeleteAsync<T>()` | `Task<int>` | Deletes every row matched by the query in a single `DELETE` statement. Returns the number of rows deleted. |

```csharp
int deleted = await ctx.GetTable<Order>()
	.Where(o => o.Status == "Cancelled")
	.DeleteAsync();
```

> ✏️ `DumpProviderCandidates(string methodName)` is a public diagnostic helper that writes
> LINQ provider method candidates to the log. It is not needed in application code.

---

### SxmUpdateSet&lt;T&gt;

```csharp
public sealed class SxmUpdateSet<T> where T : class
```

Builder returned by `Set(...)`. Accumulates column assignments and executes them as one `UPDATE`.

| Member | Description |
|---|---|
| `SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, TProp value)` | Add another constant assignment. |
| `SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)` | Add another expression assignment. |
| `Task<int> UpdateAsync(CancellationToken cancellationToken = default)` | Execute the `UPDATE`. Returns the number of rows affected. |

---

### SxmSql

```csharp
public class SxmSql
```

Static methods for standalone operations — each call is its own transaction that commits on
success and rolls back on failure. If called while an `SxmTransaction` is active on the current
async flow, the operation enlists in that transaction instead.

📖 Guides: [Working with Data](./working-with-data.md), [SQL Statement File](./sql-statement-file.md)

| Member | Description |
|---|---|
| `static Task<int> BulkInsertAsync<T>(List<T> entities, int statementCount = 20, string? databaseName = default, CancellationToken cancellationToken = default)` where `T : SxmEntity` | Inserts many new entities using multi-row `INSERT` statements in a self-contained transaction. All entities must have `id == 0` and be the same runtime type. Not supported on tables whose triggers insert back into the same table. |
| `static Task DropTableAsync(string tableName, string? dbName = default)` | Drops the table if it exists. Throws `SxmException` with `ErrorCode == TableHasDependents` if another table has a foreign key referencing it; drop dependents first. |

**`RunStatementAsync` overloads**

Same eight shapes as on `SxmTransaction`, plus an optional trailing `string? databaseName = default`
parameter on each to target a non-default database.

| Overload | Returns |
|---|---|
| `RunStatementAsync(string sqlOrStatementName, string? databaseName = default)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync<T>(string sqlOrStatementName, T userObjectParameters, string? databaseName = default)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = default)` | `Task<List<Dictionary<string, object?>>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName, string? databaseName = default)` | `Task<List<TResult>>` |
| `RunStatementAsync<T, TResult>(string sqlOrStatementName, T userObjectParameters, string? databaseName = default)` | `Task<List<TResult>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default)` | `Task<List<TResult>>` |
| `RunStatementAsync<TResult>(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = default)` | `Task<List<TResult>>` |

```csharp
Dictionary<string, object?> parameters = new Dictionary<string, object?> { ["CustomerId"] = 42L };
List<Order> orders = await SxmSql.RunStatementAsync<Order>(
	"SELECT id, Product, Amount FROM `Order` WHERE CustomerId = @CustomerId", parameters);
```

---

<!-- ### SxmStore

```csharp
public static class SxmStore
```

A simple key/value store backed by a table in the **default** database. Useful for settings and
small pieces of state that should live alongside your data and participate in its transactions.

Supported value types: `string`, `bool`, `byte`, `short`, `int`, `long`, `float`, `double`,
`decimal`, `char`. For other types (e.g. `DateTime`), convert to one of these first.

| Member | Description |
|---|---|
| `static Task PutAsync<T>(string key, T value)` | Stores or replaces the value for `key`. A `null` value is stored and is distinguishable from a missing key. Throws `ArgumentException` for an unsupported `T`. |
| `static Task<T?> GetAsync<T>(string key, T? defaultValue = null)` where `T : struct` | Reads a value type. Returns `defaultValue` if the key is missing; returns `null` if the key exists with a stored `null`. |
| `static Task<T?> GetAsync<T>(string key, T? defaultValue = null)` where `T : class` | Reads a reference type (`string`). Same missing/null semantics. |
| `static Task<Type?> GetTypeAsync(string key)` | Returns the CLR type stored under `key`, or `null` if the key is missing. |
| `static Task<bool> ContainsKeyAsync(string key)` | `true` if the key exists (even if its value is `null`). |
| `static Task<bool> RemoveAsync(string key)` | Removes the key. Returns `true` if it existed. |
| `static Task ClearAsync()` | Removes all keys. |

> ⚠️ `SxmStore` may be used outside any transaction or inside an `SxmTransaction` on the default
> database. Using it inside a transaction on a *non-default* database throws
> `InvalidOperationException`.

```csharp
await SxmStore.PutAsync("fontSize", 14);
int fontSize = await SxmStore.GetAsync<int>("fontSize") ?? 12;
```

--- -->

## Errors

### SxmException

```csharp
public sealed class SxmException : Exception
```

The exception SQLiteXM throws for failures it detects or wraps. Provider errors from
`Microsoft.Data.Sqlite` (`SqliteException`) are **not** wrapped — they propagate unchanged and
must be caught separately.

📖 Guide: [Error Handling](./error-handling.md)

| Member | Description |
|---|---|
| `SxmErrorCode ErrorCode { get; }` | Strongly typed, stable code identifying the cause. This is the supported way to branch on a failure. Declared as `SxmDefines.SxmErrorCode`. |
| `string Context { get; }` | Human-readable description of what SQLiteXM was doing (database, table, statement). Never `null`. For logging only — do not branch on it. |

```csharp
try
{
	await customer.SaveAsync();
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded)
{
	// App is suspended; queue for retry.
}
```

### SxmExceptionExtensions

```csharp
public static class SxmExceptionExtensions
```

| Member | Description |
|---|---|
| `static string GetSxmContext(this Exception ex)` | Returns the SQLiteXM operation context attached to *any* exception, including a pass-through `SqliteException`. Returns `string.Empty` if none was recorded. Never `null`. |

---

## Enumerations

### SxmErrorCode

Declared as `SxmDefines.SxmErrorCode`. Reported through `SxmException.ErrorCode`.

| Value | Meaning |
|---|---|
| `MissingSQL` | A named statement was requested but no SQL text was found for it. |
| `LockDb` | The database could not be locked. |
| `NoDatabaseExists` | The named database is not defined in `SqlStatements.json`. |
| `UnknownSynchCommand` | An unrecognized synchronization command was encountered. |
| `UnknownSqlStatement` | The named SQL statement does not exist. |
| `InvalidDBName` | The database name is invalid. |
| `DbVersionFormatError` | The database version string could not be parsed. |
| `AcquireLease` | A connection lease could not be acquired. |
| `ConnectionBlockedBackgrounded` | A connection was refused because `SxmLifecycleManager.OnSleep` is in effect. |
| `TableHasDependents` | `DropTableAsync` refused because other tables reference this one via foreign keys. |
| `ConnectionFailure` | Opening or using a connection failed. |
| `LockFailure` | A lock could not be obtained. |
| `QueryFailure` | A query failed to execute. |
| `SchemaFailure` | Creating or evolving the schema failed. |
| `DataConversionFailure` | A value could not be converted between CLR and SQLite types. |
| `MappingFailure` | Result columns could not be mapped to the target type. |
| `TransactionFailure` | Creating or completing a transaction failed. |

### DataType

Used with `[Column(DataType = ...)]` to override storage type. See
[Supported Data Types](./supported-data-types.md) for how each CLR type maps by default.

`Default`, `Text`, `NVarChar`, `VarChar`, `Char`, `NChar`, `Int16`, `Int32`, `Int64`, `UInt16`,
`UInt32`, `UInt64`, `Boolean`, `Guid`, `Single`, `Double`, `Decimal`, `DateTime`, `Date`, `Time`,
`Binary`, `Blob`, `VarBinary`, `Long`

### ForeignKeyDeleteAction

Used with `[ForeignKey(..., OnDelete = ...)]`.

| Value | SQLite behavior when the parent row is deleted |
|---|---|
| `None` | No explicit clause; SQLite default (`NO ACTION`). |
| `Cascade` | Delete the child rows too. |
| `SetNull` | Set the foreign key column to `NULL`. |
| `SetDefault` | Set the foreign key column to its default. |
| `Restrict` | Refuse the delete while child rows exist. |
| `NoAction` | Defer the check to the end of the statement. |

### SxmJournalMode

`SxmDatabaseOptions.JournalModeOption` — maps to `PRAGMA journal_mode`.

`Delete`, `Truncate`, `Persist`, `Memory`, `Wal`, `Off`

### SxmSynchronousMode

`SxmDatabaseOptions.SynchronousModeOption` — maps to `PRAGMA synchronous`.

`Off`, `Normal`, `Full`, `Extra`

### SxmTempStore

`SxmDatabaseOptions.TempStore` — maps to `PRAGMA temp_store`.

`Default`, `File`, `Memory`

### CheckPointConnection

`SxmDatabaseOptions.CheckPointConnection` — controls when a WAL checkpoint runs.

| Value | Meaning |
|---|---|
| `Off` | SQLiteXM never forces a checkpoint; SQLite's auto-checkpoint still applies. |
| `OnConnectionClose` | Checkpoint each time a connection closes. |
| `MaxSize` | Checkpoint when the WAL exceeds `CheckPointWalMaxSize` KB. |

---

## Delegates

### ConnectionOpenedInterceptor

```csharp
public delegate void ConnectionOpenedInterceptor(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection);
```

Callback registered with `SxmDatabaseOptions.OnConnectionOpened`. Receives the raw open connection.

### ConnectionClosedInterceptor

```csharp
public delegate void ConnectionClosedInterceptor();
```

Callback registered with `SxmDatabaseOptions.OnConnectionClosed`.

### SchemaSynchronizedInterceptor

```csharp
public delegate void SchemaSynchronizedInterceptor(SxmSchemaSynchronizedEventArgs args);
```

Callback registered with `SxmDatabaseOptions.OnSchemaSynchronized`. Invoked once per entity table after
`RegisterEntitiesAsync` (or initialization with entity types) has compared the class to the table and run
any DDL needed. Handlers run synchronously; an exception thrown by a handler fails that entity's registration.

### SxmSchemaSynchronizedEventArgs

```csharp
public sealed class SxmSchemaSynchronizedEventArgs
```

| Property | Type | Description |
|---|---|---|
| `DatabaseName` | `string` | Database that owns the table. |
| `TableName` | `string` | Table name (always the entity's CLR class name). |
| `TableCreated` | `bool` | `true` if the table was created from scratch; `false` if an existing table was synchronized. |
| `DdlStatements` | `IReadOnlyList<string>` | The DDL statements executed, in order. Empty when the table already matched the entity. |
| `Duration` | `TimeSpan` | How long the create/synchronize pass took. |

```csharp
SxmDatabaseOptions options = new SxmDatabaseOptions();
options.OnSchemaSynchronized(args =>
{
    foreach (string ddl in args.DdlStatements)
        Console.WriteLine($"[{args.TableName}] {ddl}");
});
await SxmDatabase.InitializeAsync(stream, options);
```

---

## Related Documentation

- [Getting Started](./getting-started.md)
- [Defining Entities](./defining-entities.md)
- [Working with Data](./working-with-data.md)
- [LINQ Queries](./linq-queries.md)
- [Error Handling](./error-handling.md)
- [Applying Database Options](./database-configuration-options.md)
