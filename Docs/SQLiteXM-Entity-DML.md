# Entity DML — Save, Update, and Delete on `SxmEntity`

SQLiteXM lets any class that inherits from `SxmEntity` to persist, update, and delete **itself** with a single method call. There is no `DbContext` to configure, no separate insert/update/delete APIs to choose between, and no transaction plumbing to pass around — the same call works identically inside and outside a transaction.

This guide covers the entity self-persistence API, the automatic INSERT-vs-UPDATE decision, deletion semantics, and how entities automatically participate in an ambient `SxmSqlTransaction`.

> 💡 This document is the entity-centric companion to [Embedded SQL Query Support](SQLiteXM-SQL-Support.md) and [Named SQL Statements](SQLiteXM-Named-Statements.md). Under the hood, entity DML is executed through the same `RunStatementAsync` pipeline those guides describe — using named INSERT/UPDATE/DELETE statements that SQLiteXM generates and registers when your entity types are registered.

---

## Table of Contents

- [What Is an Entity?](#what-is-an-entity)
- [The Entity DML Methods](#the-entity-dml-methods)
- [`SaveAsync` — Insert or Update in One Call](#saveasync--insert-or-update-in-one-call)
- [`DeleteAsync` — Remove the Row](#deleteasync--remove-the-row)
- [Running Entity DML Outside a Transaction](#running-entity-dml-outside-a-transaction)
- [Running Entity DML Inside a Transaction](#running-entity-dml-inside-a-transaction)
- [Ambient Transactions — How Enlistment Works](#ambient-transactions--how-enlistment-works)
- [Mixing Entity DML with `RunStatementAsync`](#mixing-entity-dml-with-runstatementasync)
- [Error Handling and Rollback](#error-handling-and-rollback)
- [Best Practices](#best-practices)

---

## What Is an Entity?

An **entity** is any class that inherits from `SxmEntity`. Once you register the type at startup via `SxmDatabase.RegisterEntitiesAsync(...)`, SQLiteXM:

- Creates or updates its backing table.
- Generates and registers the named `INSERT`, `UPDATE`, and `DELETE` statements it needs.
- Wires the primary key (`id`) so newly inserted rows are populated automatically after save.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
	public string? Name { get; set; }
	public int Age { get; set; }

	[Index]
	public string? Email { get; set; }
}
```

From that point on, an instance can persist itself:

```csharp
var user = new User { Name = "Alice", Age = 30, Email = "alice@example.com" };
await user.SaveAsync();
```

For entity modeling details, see [Getting Started](GettingStarted.md) and the entity attribute reference linked from it.

---

## The Entity DML Methods

Every `SxmEntity` exposes the following instance methods:

| Method | Purpose |
|---|---|
| `SaveAsync()` | Inserts if the row is new; updates if it exists. Automatically participates in the ambient transaction, if any. |
| `DeleteAsync()` | Deletes the row. Automatically participates in the ambient transaction, if any. No-op if the row does not exist. |

There is intentionally **no** `UpdateAsync` or `InsertAsync` on the public entity surface — `SaveAsync` covers both cases.

---

## `SaveAsync` — Insert or Update in One Call

`SaveAsync` decides between `INSERT` and `UPDATE` based on whether the row already exists in the database:

- **New entity** → SQLiteXM runs the generated `INSERT`, then populates the entity's `id` from SQLite's `last_insert_rowid()`.
- **Existing entity** → SQLiteXM runs the generated `UPDATE` in place. Existing rows are **not** deleted-and-reinserted, so triggers, foreign keys, and the entity's identity are preserved.

```csharp
// Insert
var user = new User { Name = "Alice", Age = 30 };
await user.SaveAsync();
Console.WriteLine(user.id);      // populated after insert

// Update
user.Age = 31;
await user.SaveAsync();          // same call — becomes an UPDATE
```

---

## `DeleteAsync` — Remove the Row

`DeleteAsync` removes the row that corresponds to the current entity instance.

```csharp
await user.DeleteAsync();
```

Behavior notes:

- If the row does not exist in the database, `DeleteAsync` is a no-op.
- The in-memory entity object survives — only the database row is removed.
- Foreign-key constraints (when enabled) still apply. SQLite may reject the delete if related child rows exist and no `ON DELETE` cascade/action was declared. See the entity attribute reference for `[ForeignKey]` and `ForeignKeyDeleteAction`.
- The entity can be persisted again by calling SaveAsync(), which recreates a corresponding database row. A new `id` is assigned, so the entity is no longer considered the same row as before.

---

## Running Entity DML Outside a Transaction

When no transaction is active, every entity DML call is its own atomic unit of work. It commits on success and has no effect on failure.

```csharp
var user = new User { Name = "Alice", Age = 30 };
await user.SaveAsync();         // standalone insert; commits immediately

user.Age = 31;
await user.SaveAsync();         // standalone update

await user.DeleteAsync();       // standalone delete
```

This is the right choice when a call site performs a single logical write, or when subsequent writes are independent of each other.

---

## Running Entity DML Inside a Transaction

To group multiple writes into a single atomic unit of work, wrap them in an `SxmSqlTransaction`:

```csharp
await using var tx = SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();
await order.SaveAsync();
await orderItem.SaveAsync();

// Auto-commit on successful dispose.
```

Key points:

- **Nothing changes at the call site.** `SaveAsync` and `DeleteAsync` auto-detect the transaction via the ambient context (see below); you don't pass it in.
- **Auto-commit on clean dispose.** Reaching the end of the `await using` block without an exception commits.
- **Auto-rollback on exception.** If any call throws, disposal rolls back the transaction and undoes every write inside it.
- **Manual commit / rollback are optional.** `await tx.CommitTransactionAsync()` or `await tx.RollbackTransactionAsync()` are supported for cases where a business rule — not an exception — determines the outcome.

```csharp
await using var tx = SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();

if (!businessRulePassed)
	await tx.RollbackTransactionAsync();
else
	await tx.CommitTransactionAsync();   // optional; dispose would also commit
```

---

## Ambient Transactions — How Enlistment Works

`SaveAsync` and `DeleteAsync` look up the current ambient transaction internally:

- If an `SxmSqlTransaction` is active on the current async context, the entity DML call **enlists in it automatically**.
- If no transaction is active, the call runs standalone on its own connection.

This means the **same line of code** works both inside and outside a transaction:

```csharp
public async Task SavePairAsync(User a, User b)
{
	await a.SaveAsync();
	await b.SaveAsync();
}

// Called standalone — each save commits independently
await SavePairAsync(u1, u2);

// Called inside a transaction — both saves participate in the transaction
await using (var tx = SxmSqlTransaction.Create("MyApp"))
{
	await SavePairAsync(u1, u2);
}   // commits both, or rolls both back on exception
```

No parameter changes, no overload switching, no "transaction-aware" variants of your methods — the ambient context handles it.

---

## Mixing Entity DML with `RunStatementAsync`

Entity DML and direct SQL execution share the same underlying transaction:

```csharp
await using var tx = SxmSqlTransaction.Create("MyApp");
await user.SaveAsync();                          // entity DML

string updateSql = "UPDATE User SET LastLoginUtc = @utc WHERE id = @id";
Dictionary<string, object?> updateParams = new Dictionary<string, object?>
{
	{ "utc", DateTime.UtcNow },
	{ "id",  user.id }
};

await tx.RunStatementAsync(updateSql, updateParams);
await order.SaveAsync();                         // entity DML

// All three commit together, or all three roll back.
```

You can freely mix entity self-persistence, embedded SQL, and named SQL statements inside the same transaction. See:

- [Embedded SQL Query Support](SQLiteXM-SQL-Support.md)
- [Named SQL Statements](SQLiteXM-Named-Statements.md)

---

## Error Handling and Rollback

The simplest pattern is to let exceptions propagate and rely on `await using` for rollback:

```csharp
try
{
	await using var tx = SxmSqlTransaction.Create("MyApp");

	await user.SaveAsync();
	await order.SaveAsync();
}
catch (Exception ex)
{
	Logger.LogError(ex, "Save failed; transaction rolled back automatically.");
}
```

Behavior inside a failing transaction:

- The first exception propagates.
- Disposal rolls back every write performed in the scope.
- If additional statements would have run after the throw, they are short-circuited.

Outside a transaction, only the single failing call is affected; earlier successful calls are already committed.

---

## Best Practices

- **Prefer `SaveAsync`** as the single call for both inserts and updates.
- **Use `await using SxmSqlTransaction.Create(...)`** for any group of writes that must succeed or fail together.
- **Let the ambient transaction do the work.** The same entity method calls run correctly inside or outside a transaction, so the same code is reusable in both contexts.
- **Populate `id` from `SaveAsync`, don't guess.** After inserting a new entity, use `entity.id` — it's set for you.
- **Don't call `DeleteAsync` in a loop when you can delete via a named/embedded SQL statement.** For bulk deletes, a single `DELETE ... WHERE ...` via `RunStatementAsync` is dramatically faster.
- **Enable foreign keys deliberately.** When `ForeignKeys = true`, `DeleteAsync` will fail if related child rows exist without a declared cascade/action.
- **Keep transactions short.** Open, do the writes, dispose. Long-lived transactions block other writers.
