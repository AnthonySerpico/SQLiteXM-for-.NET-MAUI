# Error Handling

This guide explains how SQLiteXM reports failures, which exception types you can
expect, and how to write recovery logic that stays correct as the library evolves.

---

## The Short Version

SQLiteXM deliberately does **not** funnel every failure into a single exception type.
Three categories reach your code, and each one needs different handling:

| Category | Exception type | What it means |
|----------|----------------|---------------|
| Library operational failure | `SxmException` | SQLiteXM could not complete the operation. Branch on `ErrorCode`. |
| SQLite engine failure | `Microsoft.Data.Sqlite.SqliteException` | SQLite itself refused the operation. Inspect `SqliteErrorCode`. |
| Standard .NET exception | `OperationCanceledException`, `ArgumentException`, `InvalidOperationException`, and similar | Cancellation, usage errors, and runtime conditions. SQLiteXM does not wrap these. Handle exactly as you would anywhere else. |

The most important consequence: **catching `SxmException` alone is not sufficient.**
For example, a `SqliteException` will pass straight through it.

```csharp
try
{
	await customer.SaveAsync();
}
catch (SxmException ex)
{
	// Library-level failure.
}
catch (SqliteException ex)
{
	// SQLite engine failure — NOT wrapped, and not caught above.
}
```

---

## Why SQLite Exceptions Are Not Wrapped

`SqliteException` propagates unchanged, by design. It carries `SqliteErrorCode` and
`SqliteExtendedErrorCode`, and those values are what recovery decisions depend on —
retrying on `SQLITE_BUSY`, backing off on `SQLITE_LOCKED`, reporting a constraint
violation to the user. Wrapping it would either discard that detail or force you to
dig it back out of an inner exception.

```csharp
catch (SqliteException ex) when (ex.SqliteErrorCode == 5)   // SQLITE_BUSY
{
	// Safe to retry after a short delay.
}
```

Because SQLiteXM never produces this type itself, there is no SQLiteXM error code
for "a SQLite error occurred." If you need the engine's own detail, catch `SqliteException`.

---

## Branching on `SxmException`

`SxmException.ErrorCode` is the supported way to distinguish library failures. It is
strongly typed, and the same value is mirrored in `Data["sxmErrorCode"]` for logging.

```csharp
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.TableHasDependents)
{
	// Drop the referencing tables first.
}
```

Prefer exception filters (`when`) over `if` tests inside the `catch` body. A filter
that does not match leaves the exception travelling up the stack untouched, which
preserves the original stack trace for handlers above you.

Never branch on `ex.Message`. Message text is not part of the contract and may be
reworded in any release.

### Error Codes

Every value below is reachable. `SxmErrorCode` describes states the library can
actually produce — codes that became unreachable were removed in 2.0.0.

Codes fall into two groups. **Specific conditions** identify a precise, known
situation and usually tell you exactly what to fix. **Failure categories** classify
a wrapped operational failure by the kind of operation that failed; for these, the
category tells you *where* the failure happened and `InnerException` carries the
root cause.

#### Specific conditions

| Code | Meaning |
|------|---------|
| `MissingSQL` | A required SQL statement was not supplied. |
| `LockDb` | The database lock could not be acquired. |
| `NoDatabaseExists` | The named database does not exist. |
| `UnknownSynchCommand` | Unrecognized table synchronization command. |
| `UnknownSqlStatement` | The named statement is not in `SqlStatements.json`. |
| `InvalidDBName` | The database name is not valid. |
| `DbVersionFormatError` | The database version number is malformed. |
| `AcquireLease` | A shared connection is closing and cannot be acquired. |
| `ConnectionBlockedBackgrounded` | Connection creation blocked because the app is backgrounded. |
| `TableHasDependents` | The table is referenced by foreign keys and cannot be dropped. |

#### Failure categories

| Code | Meaning |
|------|---------|
| `ConnectionFailure` | A connection could not be created, released, or committed. |
| `LockFailure` | Acquiring or releasing a connection lock failed unexpectedly. |
| `QueryFailure` | A query or statement failed to execute. |
| `SchemaFailure` | A schema operation failed, such as building the schema, creating a table, or reading the database version. |
| `DataConversionFailure` | A database value could not be read or converted to the expected type, usually an entity/schema mismatch. |
| `MappingFailure` | An entity, column, or association could not be mapped. |
| `TransactionFailure` | A transaction could not be created or finalized. |

For any category code, `ex.Message` already carries the root-cause text from the
underlying exception, and `InnerException` remains available for the full detail:

```csharp
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.QueryFailure)
{
	logger.LogError(ex.InnerException, "Query failed: {Message}", ex.Message);
}
```

---

## Diagnostic Context

A SQLiteXM failure carries two distinct pieces of text, and they never hold the
same string:

- **`Message`** answers *what went wrong*. On a wrapped failure it is the message
  of the underlying exception, hoisted to the top level so the root cause is
  readable without unwrapping `InnerException`.
- **`Context`** answers *what SQLiteXM was doing*. For example, the database,
  table, or SQL statement involved.

`Context` is most valuable on the pass-through path, where a bare
`SqliteException` would otherwise tell you nothing about the operation that
surfaced it.

On an `SxmException`, read it through the `Context` property. On any other
exception type, use the `GetSxmContext()` extension method — it works on
everything SQLiteXM can surface, so you never need to know where the value is
stored:

```csharp
catch (SxmException ex)
{
	logger.LogError(ex, "SQLiteXM failure {Code}: {Context}", ex.ErrorCode, ex.Context);
}
catch (SqliteException ex)
{
	logger.LogError(ex, "SQLite failure {Code} during {Context}", ex.SqliteErrorCode, ex.GetSxmContext());
}
```

A few things to keep in mind:

- **Context is never null.** Both `Context` and `GetSxmContext()` return an empty
  string when nothing was recorded, so they can be logged or concatenated without
  a null check. An empty result means no context was attached — for example on a
  validation guard such as `TableHasDependents`, which is thrown directly rather
  than wrapped and whose message already names the table and its dependents.
- **The innermost context wins.** An existing value is never overwritten, so the
  most specific operation is the one you see.
- **Context is independent of logging.** Attaching context is done at the point
  of failure, not by the logger. Turning logging off does not remove it.
- **Do not branch on it.** The wording is free-form and may change. Use `ErrorCode`.

---

## What Can Each API Throw?

The tables below answer the question you actually have at a call site: *what is
worth catching here, and what would I do about it?*

**How to read them.** The **`SxmException` codes** column lists the `ErrorCode`
values worth catching — failures that are realistically likely and that you can
act on, whether that means fixing the code, telling the user, or retrying. The
**Also possible** column lists the other exception types that can reach you, and
is the reason you still want a general arm at the end. The **Notes** column
answers the question behind the question: *did anything change, and is my data
intact?*

Two things to keep in mind before using these tables:

- **Completeness varies by design.** Narrow APIs such as `DropTableAsync` list
  every `SxmException` code they can produce. Façade APIs such as `SaveAsync`
  run through the connection, query, and mapping layers, so their realistic
  failure surface is most of the enum. For those, the table names the codes you
  will actually hit and then says *any code from an inner operation*. That is a
  deliberate statement, not an omission — treat those lists as a starting point,
  not a closed set.
- **`SqliteException` is listed separately on purpose.** It is never wrapped, so
  it bypasses a `catch (SxmException)` arm entirely. Where it appears in **Also
  possible**, a `catch (SqliteException)` arm is the difference between handling
  the failure and missing it.

Cancellation is omitted from every row. Any `async` API here can throw
`OperationCanceledException` when you pass a token that is signalled, and the
handling is always the same — let it through.

### Startup and Initialization

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmDatabase.StartInitialization` | — | `ArgumentException` | Fire-and-forget. Initialization runs in the background and **failures do not surface here** — they surface at `EnsureReadyAsync`. Argument validation on the options is immediate. |
| `SxmDatabase.InitializeAsync` | `InvalidDBName`, `DbVersionFormatError`, `UnknownSynchCommand` — all mean a malformed SQL statements file<br>`SchemaFailure` | `ArgumentException`, `InvalidOperationException`, `SqliteException` | `InvalidOperationException` means the database name was already registered. `ArgumentException` comes from options validation. Both are setup bugs — fix the call, do not retry. |
| `SxmDatabase.EnsureReadyAsync` | Any code | `ArgumentException`
| `SxmDatabase.RegisterEntitiesAsync` | `SchemaFailure`, `MappingFailure` | `ArgumentException`, `InvalidOperationException`, `SqliteException` | `ArgumentException` means a type does not derive from `SxmEntity` or is abstract. `InvalidOperationException` means SQLiteXM is not initialized yet. Returns silently for a null or empty list. |
| `SxmSchemaRegistration.RegisterEntitySchemaAsync` | `SchemaFailure` | `ArgumentException`, `ArgumentNullException`, `InvalidDataException`, `InvalidOperationException`, `SqliteException` | `InvalidDataException` means the target database is missing or undefined; `InvalidOperationException` means an entity name collision. On failure the registration is rolled back, so a corrected retry is safe. |
| `SxmSchemaRegistration.IsSchemaRegistered` | — | — | Pure lookup. Does not throw. |

### Entities

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmEntity.SaveAsync` | `UnknownSqlStatement` (schema not registered), `DataConversionFailure` (entity and stored schema have diverged), `ConnectionBlockedBackgrounded` (app is backgrounded)<br><br>**Plus any code from the underlying connection, query, or mapping operation** | `InvalidOperationException`, `SqliteException` | Runs through connection acquisition, statement execution, and value mapping, so connection, lock, query and conversion failures all surface here. **Inside a faulted transaction this returns without doing anything rather than throwing** — a silent no-op, not a success. `SqliteException` is the arm that catches constraint violations. |
| `SxmEntity.DeleteAsync` | `QueryFailure`, `SchemaFailure`<br><br>**Plus any code from the underlying connection or query operation** | `SqliteException` | No-ops if the record does not exist, and no-ops inside a faulted transaction. A foreign-key violation arrives as `SqliteException`, not `SxmException`. |

### Queries and Statements

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmSql.RunStatementAsync` | `UnknownSqlStatement` (name not in the statements file), `MissingSQL` (no SQL supplied), `QueryFailure`<br><br>**Plus any code from connection acquisition** | `ArgumentException`, `SqliteException` | Connection acquisition happens first, so `LockDb`, `AcquireLease` and `ConnectionBlockedBackgrounded` can all precede the query itself. Constraint violations arrive as `SqliteException` — this is the most common place to need that arm. |
| `SxmTransaction.RunStatementAsync` | Same as `SxmSql.RunStatementAsync` | `ArgumentException`, `ObjectDisposedException`, `SqliteException` | Reuses the transaction's existing connection, so connection-acquisition codes are less likely. A failure marks the context faulted: subsequent writes no-op and `CommitTransactionAsync` throws until you call `RollbackTransactionAsync`. |
| `SxmTable<T>.LoadWith` / enumeration | `QueryFailure`, `DataConversionFailure`, `MappingFailure` | `SqliteException`, `InvalidOperationException`, `NotSupportedException` | **Execution is deferred.** `LoadWith` itself does almost nothing; failures surface when the query is enumerated or awaited, so put the `try` around the enumeration, not the query construction. `NotSupportedException` means an expression LinqToDB cannot translate. |

### Transactions

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmTransaction.CommitTransactionAsync` | `TransactionFailure` | `InvalidOperationException`, `ObjectDisposedException`, `SqliteException` | `InvalidOperationException` means an earlier operation on this context failed — call `RollbackTransactionAsync` instead. No-ops when no transaction is open. **If the commit throws, assume the work did not persist.** |
| `SxmTransaction.RollbackTransactionAsync` | `TransactionFailure` | `ObjectDisposedException`, `SqliteException` | Clears the faulted state even if the rollback fails, so the context is reusable afterwards. Safe to call when no transaction is open. |
| `SxmUTransaction.CommitTransactionAsync`<br>`SxmUTransaction.RollbackTransactionAsync` | `TransactionFailure` | `ArgumentNullException`, `SqliteException` | Same semantics as above across all attached databases. |
| `SxmUTransaction.AttachDatabaseAsync` | `NoDatabaseExists` — the name is not a registered database | `ArgumentNullException`, `InvalidOperationException`, `SqliteException` | Silently no-ops when asked to attach the connection's own database. The parameterless overload attaches every registered database and fails on the first problem. |
| `SxmUTransaction.DetachDatabaseAsync()` | — | `ArgumentNullException` | **Best-effort cleanup: per-database failures are swallowed** so every database gets a detach attempt. Does not report which detaches failed. Only the null-connection guard escapes. |
| `SxmUTransaction.DetachDatabaseAsync(string)` | `NoDatabaseExists` | `ArgumentNullException`, `InvalidOperationException`, `SqliteException` | The single-database overload **does** report failures, unlike the parameterless one. |

### Connections and Lifecycle

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmConnectionManager.RunWorkersAsync` | `AcquireLease` (connection is shutting down), `ConnectionBlockedBackgrounded`, `LockDb` | `ArgumentException`, `ArgumentNullException`, `SqliteException` | Whatever a worker throws propagates to the caller. `AcquireLease` during shutdown is expected, not a defect. |
| `SxmConnectionManager.ShutdownAsync` | — | `ArgumentNullException`, `SqliteException` | No-ops if the database is not found. Waits for in-flight operations before closing. |
| `SxmLifecycleManager.OnSleep`<br>`SxmLifecycleManager.OnResume` | — | — | **Deliberately do not throw.** Both are idempotent and safe to call from platform lifecycle callbacks without a `try`. See [Application Lifecycle](application-lifecycle.md). |

### Schema and Diagnostics

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmSql.DropTableAsync` | `TableHasDependents` — other tables reference this one<br>`QueryFailure`, `SchemaFailure` | `SqliteException` | Complete list. `TableHasDependents` is raised **before any DDL runs**, so the schema is untouched and the message names every dependent table in the order you need to drop them. |
| `SxmDatabaseDescriptor.GetWalFileSize` | — | `ArgumentException`, `InvalidOperationException` | Returns `0` when the WAL file does not exist rather than throwing. `InvalidOperationException` means the database folder was never configured. |

### Key-Value Store

Every `SxmStore` method shares one shape, so they are listed together.

| API | `SxmException` codes | Also possible | Notes |
|---|---|---|---|
| `SxmStore.GetTypeAsync`<br>`SxmStore.RemoveAsync`<br>`SxmStore.ContainsKeyAsync`<br>`SxmStore.ClearAsync` | `QueryFailure`<br><br>**Plus any code from connection acquisition** | `ArgumentNullException`, `InvalidOperationException`, `SqliteException` | `ArgumentNullException` means a null or empty key. `InvalidOperationException` means an ambient transaction is open against a different database. **Missing keys are not errors**: `GetTypeAsync` returns `null` and `RemoveAsync` returns `false`. |

---

## Cancellation and Usage Errors

These propagate unchanged so that normal .NET semantics hold:

- `OperationCanceledException` / `TaskCanceledException` — cooperative cancellation.
  Let these through; do not convert them into failures.
- `ArgumentException`, `InvalidOperationException`, `NotSupportedException` — these
  signal a bug in calling code. Fix the call, do not catch and continue.
- `OutOfMemoryException` — never wrapped. Allocating a wrapper while memory is
  exhausted could itself fail and replace a clear error with a confusing one.

```csharp
try
{
	await LoadAsync(cancellationToken);
}
catch (OperationCanceledException)
{
	return;     // Expected when the user navigates away.
}
```

Truly fatal conditions such as `StackOverflowException` are not listed above
because the runtime does not deliver them to managed `catch` blocks at all — the
process terminates. No library policy applies.

---

## A Complete Handler

Ordering matters: the most specific filters come first, and the general
`SxmException` catch comes after the targeted ones.

```csharp
try
{
	await SxmSql.DropTableAsync(nameof(Customer));
}
catch (OperationCanceledException)
{
	throw;                                      // Never swallow cancellation.
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.TableHasDependents)
{
	await ShowAsync("Remove the referencing tables first.");
}
catch (SxmException ex)
{
	logger.LogError(ex, "SQLiteXM failure {Code}: {Context}", ex.ErrorCode, ex.Context);
	throw;
}
catch (SqliteException ex)
{
	logger.LogError(ex, "SQLite failure {Code}", ex.SqliteErrorCode);
	throw;
}
```

---

## Related Reading

- [Application Lifecycle](application-lifecycle.md) — handling
  `ConnectionBlockedBackgrounded` during app backgrounding.
- [Schema Evolution](schema-evolution.md) — `TableHasDependents` and dependent-table
  drop ordering.
- [Concurrency](concurrency.md) — lock contention and `SQLITE_BUSY`.
