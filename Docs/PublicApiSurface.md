# SQLiteXM — Public API Surface

Snapshot of every `public` type and member exposed by the **SQLiteXM** library assembly
(excludes `Samples/`, `SQLiteXM.Tests/`, and per-platform `PlatformClass1.cs` stubs).

Legend: 🟢 = entry point · 🟦 = configuration · 🟣 = entity / mapping · 🟠 = LINQ · 🔴 = diagnostics

---

## 1. Type map (bird's-eye view)

```
SQLiteXM (namespace)
├── 🟢  SxmDatabase                       static  — bootstrap & entity registration
├── 🟢  SxmSql                            static-like — stand-alone named SQL execution
├── 🟢  SxmTransaction                      class   — transactional unit of work
├── 🟦  SxmDatabaseOptions                sealed  — init-time configuration
├── 🟦  ConnectionOpenedInterceptor       delegate — post-open callback
├── 🟦  ConnectionClosedInterceptor       delegate — post-close callback
├── 🟣  SxmEntity                         class   — base class for entities
├── 🟣  TableAttribute                    class   — [Table]
├── 🟣  ColumnAttribute                   class   — [Column]
├── 🟣  NotColumnAttribute                class   — [NotColumn]
├── 🟣  RenameAttribute                   class   — [Rename]
├── 🟣  IndexAttribute                    class   — [Index]
├── 🟣  UniqueIndexAttribute              class   — [UniqueIndex]
├── 🟣  TriggerAttribute                  class   — [Trigger]
├── 🟣  RequiredNotNullAttribute          class   — [RequiredNotNull]
├── 🟣  ForeignKeyAttribute               class   — [ForeignKey]
├── 🟠  SxmTable<T>                       sealed  — IQueryable<T> wrapper
├── 🟠  SxmUpdateSet<T>                   sealed  — fluent bulk-update builder
├── 🟠  SxmLinqExtensions                 static  — async LINQ + bulk update/delete
├── 🔴  SxmException                      sealed  — library exception
├── 🔴  SxmLifecycleManager               static  — MAUI lifecycle hooks
└── enums
	├── DataType, ForeignKeyDeleteAction
	├── SxmJournalMode, SxmSynchronousMode, SxmTempStore, CheckPointConnection
	└── SxmDefines.SxmErrorCode          (nested; surfaced via SxmException.Data)
```

---

## 2. Entry points

### 🟢 `SxmDatabase` — `static class`

| Member | Signature |
|---|---|
| `InitializeAsync` | `Task InitializeAsync(Stream stream, SxmDatabaseOptions? databaseOptions = null)` |
| `RegisterEntitiesAsync` | `Task RegisterEntitiesAsync(params Type[] entityTypes)` |

### 🟢 `SxmSql` — stand-alone named-SQL entry point

| Member | Signature |
|---|---|
| `DropTableAsync` | `Task DropTableAsync(string tableName, string? dbName = null, bool force = false)` |
| `RunStatementAsync` | `Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlOrStatementName, T userObjectParameters, string? databaseName = null)` |
| `RunStatementAsync` | `Task<List<TResult>> RunStatementAsync<TResult>(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = null)` |
| `RunStatementAsync` | `Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlOrStatementName, T userObjectParameters, string? databaseName = null)` |
| `RunStatementAsync` | `Task<List<TResult>> RunStatementAsync<TResult>(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = null)` |
| `RunStatementAsync` | `Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = null)` |
| `RunStatementAsync` | `Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = null)` |

### 🟢 `SxmTransaction : IDisposable, IAsyncDisposable`

Transactional unit-of-work combining LINQ, entity DML, and raw SQL.

| Kind | Signature |
|---|---|
| ctor | `SxmTransaction(string? databaseName = null)` |
| Table | `SxmTable<T> GetTable<T>() where T : class` |
| DML | `Task<int> InsertAsync<T>(T entity, CancellationToken ct = default)` |
| DML | `Task<int> UpdateAsync<T>(T entity, CancellationToken ct = default)` |
| DML | `Task<int> DeleteAsync<T>(T entity, CancellationToken ct = default)` |
| DML | `Task<int> InsertOrReplaceAsync<T>(T entity, CancellationToken ct = default)` — real SQLite INSERT OR REPLACE |
| Txn | `Task CommitTransactionAsync()` |
| Txn | `Task RollbackTransactionAsync()` |
| SQL | `Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params object?[] parameters)` |
| SQL | `Task<List<TResult>> RunStatementAsync<T, TResult>(string name, T userObjectParameters)` |
| SQL | `Task<List<TResult>> RunStatementAsync<TResult>(string name, Dictionary<string, object?> parameters)` |
| SQL | `Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string name, T userObjectParameters)` |
| SQL | `Task<List<Dictionary<string, object?>>> RunStatementAsync(string name, Dictionary<string, object?> parameters)` |
| SQL | `Task<List<TResult>> RunStatementAsync<TResult>(string name, List<object> parameters)` |
| SQL | `Task<List<Dictionary<string, object?>>> RunStatementAsync(string name, List<object> parameters)` |
| Lifetime | `void Dispose()` / `ValueTask DisposeAsync()` |

---

## 3. Configuration

### 🟦 `SxmDatabaseOptions` — `sealed class` (record-like, `init`-only)

| Property | Type |
|---|---|
| `CheckPointConnection` | `CheckPointConnection?` |
| `CheckPointWalMaxSize` | `int?` |
| `BusyTimeout` | `long?` |
| `CacheSize` | `long?` |
| `WalAutoCheckpoint` | `long?` |
| `EnableConnectionPooling` | `bool?` |
| `EnableLogging` | `bool?` |
| `DefaultTimeout` | `int?` |
| `ForeignKeys` | `bool?` |
| `TempStore` | `SxmTempStore?` |
| `DatabaseFolderOverride` | `string?` |
| `JournalModeOption` | `SxmJournalMode?` |
| `SynchronousModeOption` | `SxmSynchronousMode?` |

| Method | Signature |
|---|---|
| `OnConnectionOpened` | `void OnConnectionOpened(ConnectionOpenedInterceptor handler)` |
| `OnConnectionClosed` | `void OnConnectionClosed(ConnectionClosedInterceptor handler)` |

### 🟦 Delegates

```csharp
public delegate void ConnectionOpenedInterceptor(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection);
public delegate void ConnectionClosedInterceptor();
```

---

## 4. Entity & mapping

### 🟣 `SxmEntity : INotifyPropertyChanged`

| Member | Signature |
|---|---|
| ctor | `SxmEntity()` |
| Property | `virtual long id { get; set; }` |
| Property | `virtual Guid? synchId { get; internal set; }` |
| Event | `event PropertyChangedEventHandler? PropertyChanged` |
| Method | `Task SaveAsync()` |
| Method | `Task DeleteAsync()` |

### 🟣 Attributes

| Type | Notable public members |
|---|---|
| `TableAttribute` : `LinqToDB.Mapping.TableAttribute` | `TableAttribute()`, `TableAttribute(string tableName)` |
| `ColumnAttribute` : `LinqToDB.Mapping.ColumnAttribute` | `ColumnAttribute()`, `new DataType DataType { get; set; }` |
| `NotColumnAttribute` : `LinqToDB.Mapping.NotColumnAttribute` | `NotColumnAttribute()` |
| `RenameAttribute` : `Attribute` | `string[] OldNames { get; }`, `RenameAttribute(string oldName)`, `RenameAttribute(params string[] oldNames)` |
| `IndexAttribute` : `Attribute, IIndexProperties` | `string[] IndexFields`, `string IndexName`, three ctors |
| `UniqueIndexAttribute` : `Attribute, IIndexProperties` | `string[] IndexFields`, `string IndexName`, three ctors |
| `TriggerAttribute` : `Attribute` | `string TriggerSql`, `TriggerAttribute(string triggerSql)` |
| `RequiredNotNullAttribute` : `Attribute` | (marker) |
| `ForeignKeyAttribute` : `Attribute` | (see source for constructor params + `OnDelete`) |

---

## 5. LINQ / query surface

### 🟠 `SxmTable<T> : IQueryable<T>` — `sealed class`

| Member | Signature |
|---|---|
| ctor | `SxmTable(IQueryable<T> inner, SxmTransaction? context = null)` |
| `LoadWith` | `SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty?>> navigationProperty)` |
| `LoadWith` | `SxmTable<T> LoadWith(params Expression<Func<T, object?>>[] navigationProperties)` |
| IQueryable | `Type ElementType`, `Expression Expression`, `IQueryProvider Provider`, `IEnumerator<T> GetEnumerator()` |
| Override | `string? ToString()` |

### 🟠 `SxmUpdateSet<T>` — `sealed class`

| Member | Signature |
|---|---|
| `Set` | `SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, TProp value)` |
| `Set` | `SxmUpdateSet<T> Set<TProp>(Expression<Func<T, TProp>> setter, Expression<Func<T, TProp>> expression)` |
| `UpdateAsync` | `Task<int> UpdateAsync(CancellationToken ct = default)` |

### 🟠 `SxmLinqExtensions` — `static class` (extension methods)

Two symmetric sets: one for `SxmTable<T>`, one for `IQueryable<T>`.

| Category | Members |
|---|---|
| Materialization | `ToListAsync`, `ToArrayAsync` |
| Single results | `FirstAsync`, `FirstOrDefaultAsync`, `SingleAsync`, `SingleOrDefaultAsync` |
| Predicates | `AnyAsync`, `AllAsync`, `ContainsAsync` |
| Aggregates | `CountAsync`, `LongCountAsync`, `MinAsync`, `MaxAsync`, `AverageAsync` (double/float + nullable overloads) |
| Bulk mutation | `Set(...)` (starts fluent update), `UpdateAsync` (via `SxmUpdateSet`), `DeleteAsync` |
| Diagnostics | `DumpProviderCandidates(string methodName)` |

---

## 6. Diagnostics & lifecycle

### 🔴 `SxmException : Exception` — `sealed`

Carries library error metadata under `Data["sxmErrorCode"]` and (for wrapped `SqliteException`) `Data["sqliteErrorCode"]`.

**Consumers only *catch* this type — they cannot construct or subclass it.** The class is `sealed` and all constructors are `internal`. This guarantees that any `SxmException` a caller sees was produced by the library and carries the documented metadata.

### 🔴 `SxmLifecycleManager` — `static class`

| Member | Signature |
|---|---|
| `SuspendGracePeriod` | `static TimeSpan SuspendGracePeriod { get; set; }` |
| `OnSleepAsync` | `static Task OnSleepAsync()` |
| `OnResume` | `static void OnResume()` |

---

## 7. Public enums

| Enum | Purpose |
|---|---|
| `DataType` | Used with `[Column(DataType = …)]`; mirrors `LinqToDB.DataType` |
| `ForeignKeyDeleteAction` | Used with `[ForeignKey(OnDelete = …)]` |
| `SxmJournalMode` | `SxmDatabaseOptions.JournalModeOption` |
| `SxmSynchronousMode` | `SxmDatabaseOptions.SynchronousModeOption` |
| `SxmTempStore` | `SxmDatabaseOptions.TempStore` |
| `CheckPointConnection` | `SxmDatabaseOptions.CheckPointConnection` |
| `SxmDefines.SxmErrorCode` | Surfaced via `SxmException.Data["sxmErrorCode"]` |

`SxmDefines` (static class) also exposes public `readonly int` constants: `NoCloudSync`, `CloudSync`, `CloudMove`.

---

## 8. Surface totals

| Category | Count |
|---|---|
| Public types (classes + sealed) | 17 |
| Public delegates | 2 |
| Public enums | 7 |
| Public methods on `SxmTransaction` | 17 |
| Public methods on `SxmSql` (all `RunStatementAsync` + `DropTableAsync`) | 7 |
| Public extension methods in `SxmLinqExtensions` | ~35 (mirrored across `SxmTable<T>` / `IQueryable<T>`) |
