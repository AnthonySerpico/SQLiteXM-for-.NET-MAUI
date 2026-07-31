# Embedded SQL Query Support in SQLiteXM

SQLiteXM provides comprehensive support for executing SQL statements that are embedded directly in your code. This guide explains how to use direct SQL queries, from simple examples to complex transaction scenarios.

---

## Table of Contents

- [Concepts & Conventions](#concepts--conventions)
- [Overview](#overview)
- [The Two Execution Contexts](#the-two-execution-contexts)
- [One Method for All DML Statements](#one-method-for-all-dml-statements)
- [Overload Matrix](#overload-matrix)
- [Standalone Execution — `SxmStatement.RunStatementAsync`](#standalone-execution--sxmstatementrunstatementasync)
- [Transactional Execution — `SxmSqlTransaction.RunStatementAsync`](#transactional-execution--sxmsqltransactionrunstatementasync)
- [Working with Parameters](#working-with-parameters)
- [Result Type Handling](#result-type-handling)
- [Best Practices](#best-practices)

---

## Concepts & Conventions

> 💡 **SQLiteXM supports all SQLite DML statements.** SQLiteXM passes the SQL you provide directly to SQLite as-is, so any DML statement (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) can use the full expressive power of SQLite. This includes joins, subqueries, common table expressions (CTEs), aggregate functions, window functions, views, `UNION` queries, `INSERT … ON CONFLICT`, `RETURNING`, and any other SQLite-supported SQL feature that appears **inside** a DML statement.

> 💡 **About DTOs.** Throughout this guide, we use the term **DTO (Data Transfer Object)** to refer to simple classes that hold data. These are plain C# classes with properties, used to pass parameters into queries or to receive query results. You can also use your entity classes (classes that inherit from `SxmEntity`) in the same way. Neither the parameter DTO nor the result DTO needs to inherit from `SxmEntity`.

> 💡 **Property-to-parameter mapping.** DTO parameter properties map to SQL parameter names **without the leading `@`**, and the match is **case-sensitive**. DTO result properties map to returned column names by name (also case-sensitive). Result columns without a matching property are ignored; properties without a matching column keep their default value.

---

## Overview

`RunStatementAsync` is the single entry point for executing embedded **DML** SQL statements in SQLiteXM. The same method executes:

- `SELECT` — returns the selected rows.
- `INSERT` — returns nothing by default; returns the inserted row(s) when the SQL includes a `RETURNING` clause.
- `UPDATE` — returns nothing by default; returns the updated row(s) when the SQL includes a `RETURNING` clause.
- `DELETE` — returns nothing by default; returns the deleted row(s) when the SQL includes a `RETURNING` clause.

> 💡 The RETURNING clause is part of standard SQL. It causes an INSERT, UPDATE, or DELETE statement to return the affected rows, producing a result set similar to a SELECT.

`RunStatementAsync` is available in two execution contexts, and the one you use depends on what you're trying to do:

- **`SxmStatement.RunStatementAsync`** — a static method for running a single statement on its own.
- **`SxmSqlTransaction.RunStatementAsync`** — an instance method for running several statements together as one atomic unit of work.

If you're not sure which one fits your situation, this table maps common scenarios to the right choice:

| Situation | Use | Why |
|---|---|---|
| A single statement, for example, a read or a single write. | `SxmStatement.RunStatementAsync` | No coordination needed; the statement commits on its own. |
| Two or more statements that must all succeed together. | `SxmSqlTransaction.RunStatementAsync` | The transaction commits as a whole or rolls back as a whole. |
| Read some rows, decide, then write — all on the same connection. | `SxmSqlTransaction.RunStatementAsync` | Keeps the read and the write inside one atomic scope, preventing anyone else from changing the data in between. |
| Batch insert / update where any failure should undo the rest. | `SxmSqlTransaction.RunStatementAsync` | The first exception short-circuits the remaining calls and suppresses commit; you get all-or-nothing without try/catch around each statement. |
| Ad-hoc query against a non-default database. | `SxmStatement.RunStatementAsync` with the `databaseName` argument | Only the standalone form accepts `databaseName`; the transaction fixes its database at creation time. |

Aside from **when** you use each form, they behave the same way:

- `SxmStatement.RunStatementAsync` takes an optional `databaseName` argument. `SxmSqlTransaction.RunStatementAsync` does not — the transaction is already tied to a specific database.
- `SxmSqlTransaction.RunStatementAsync` participates in the enclosing transaction: it commits on successful dispose, rolls back on exception, and silently skips subsequent statements once any statement has thrown.

---

## The Two Execution Contexts

### Standalone

Each call is an independent unit of work. If the statement succeeds, its effects are persisted immediately. If it throws, only that statement is undone.

```csharp
string sql = "SELECT id, Name, Age FROM Users WHERE Age > @minAge";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 }
};

List<UserDto> users = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters, databaseName: null);
```

> 💡 Every static `SxmStatement.RunStatementAsync` overload accepts an optional third parameter `databaseName` (type `string?`). If provided, the statement executes on that named database; if omitted, it runs on the default database defined in your `SqlStatements.json` configuration.

### Transactional

Multiple statements are grouped into a single atomic unit. All succeed together, or all are rolled back together.

```csharp
string insertSql = "INSERT INTO Users (Name, Age) VALUES (@name, @age)";
Dictionary<string, object?> insertParams = new Dictionary<string, object?>
{
    { "name", "Alice" },
    { "age",  28 }
};

string updateSql = "UPDATE Settings SET LastUserName = @name WHERE id = 1";
Dictionary<string, object?> updateParams = new Dictionary<string, object?>
{
    { "name", "Alice" }
};

await using SxmSqlTransaction tx = SxmSqlTransaction.Create(databaseName: "AppData");

await tx.RunStatementAsync(insertSql, insertParams);
await tx.RunStatementAsync(updateSql, updateParams);

// Auto-commit on dispose (no exceptions were thrown).
```

> 💡 The transactional overloads do not take a database name — the transaction runs on the database of the connection it was created with.


**Key behavior of the transactional form:**

- Created by calling `SxmSqlTransaction.Create(databaseName)` (sync, private connection) or `await SxmSqlTransaction.CreateAsync(conn)` (async, works with shared connections).
- Registers itself as the **ambient** transaction. Nesting is not permitted — SQLite itself allows only one active transaction per connection, and `SxmSqlTransaction` reflects that by refusing to create a new ambient transaction while one is already active.
- On `DisposeAsync`, commits automatically if no error was encountered; otherwise the transaction is not committed and SQLite rolls back.
- Once any `RunStatementAsync` call throws, subsequent `RunStatementAsync` calls on the same transaction are **skipped silently** (they return an empty list) and auto-commit is suppressed. This lets you write straight-line code without a try/catch around every call.
- Always use `await using` so `DisposeAsync` runs.


> 💡 The first argument to `RunStatementAsync` can also be the **`Statement Name`** of a query declared in `SqlStatements.json`. Everything in this guide — overloads, parameter styles, result mapping, and transactions — applies identically to both forms. See [Named SQL Statements via `SqlStatements.json`](SQLiteXM-Named-Statements.md) for the JSON-driven workflow.


---

## One Method, for All DML Statements

This section shows the same method — `RunStatementAsync` — being used against each of the four DML verbs.

All examples share this schema and DTO:

```sql
CREATE TABLE Users (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT,
    Age     INTEGER,
    Country TEXT
);
```

```csharp
public class UserDto
{
    public int id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Country { get; set; }
}
```

### SELECT

A `SELECT` naturally returns rows. Use a typed overload to get a `List<UserDto>` or the untyped overload to get raw dictionaries.

```csharp
string sql = "SELECT id, Name, Age, Country FROM Users WHERE Age > @minAge";
Dictionary<string, object?> parameters = new Dictionary<string, object?> { { "minAge", 18 } };

// Typed:
List<UserDto> users = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

// Untyped:
List<Dictionary<string, object?>> rows = await SxmStatement.RunStatementAsync(sql, parameters);
```

### INSERT

Without a `RETURNING` clause, an `INSERT` returns an empty list. You can ignore the return value.

```csharp
string sql = "INSERT INTO Users (Name, Age, Country) VALUES (@name, @age, @country)";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name",    "Alice" },
    { "age",     28 },
    { "country", "USA" }
};

await SxmStatement.RunStatementAsync(sql, parameters);
```

With a `RETURNING` clause, an `INSERT` returns the inserted row(s) — including any auto-generated columns like `id`. Because a single `INSERT` can insert multiple rows (for example via `INSERT … SELECT …` or a multi-row `VALUES` list), the result is always a `List`, even when only one row is inserted.

```csharp
string sql = "INSERT INTO Users (Name, Age, Country) " +
             "VALUES (@name, @age, @country) RETURNING *";

// Typed:
Dictionary<string, object?> aliceParams = new Dictionary<string, object?>
{
    { "name",    "Alice" },
    { "age",     28 },
    { "country", "USA" }
};

List<UserDto> inserted = await SxmStatement.RunStatementAsync<UserDto>(sql, aliceParams);
UserDto newUser = inserted[0];
Console.WriteLine($"Created user id={newUser.id}, Name={newUser.Name}");

// Untyped:
Dictionary<string, object?> bobParams = new Dictionary<string, object?>
{
    { "name",    "Bob" },
    { "age",     35 },
    { "country", "CA" }
};

List<Dictionary<string, object?>> insertedRows = await SxmStatement.RunStatementAsync(sql, bobParams);
int newId = (int)(long)insertedRows[0]["id"]!;
```

Multi-row `INSERT` also returns every inserted row:

```csharp
string sql = "INSERT INTO Users (Name, Age, Country) " +
             "VALUES (@n1, @a1, @c1), (@n2, @a2, @c2) RETURNING *";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "n1", "Carol" }, { "a1", 40 }, { "c1", "UK" },
    { "n2", "Dan"   }, { "a2", 22 }, { "c2", "US" }
};

List<UserDto> inserted = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

Console.WriteLine($"Inserted {inserted.Count} users.");
```

### UPDATE

Without a `RETURNING` clause, an `UPDATE` returns an empty list.

```csharp
string sql = "UPDATE Users SET Age = @age WHERE id = @id";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "age", 29 },
    { "id",  1 }
};

await SxmStatement.RunStatementAsync(sql, parameters);
```

With a `RETURNING` clause, an `UPDATE` returns every row it modified. This is useful for auditing, for showing the caller what actually changed, and for cases where the `WHERE` clause may match zero, one, or many rows.

```csharp
string sql = "UPDATE Users SET Country = @country WHERE Country = @oldCountry RETURNING *";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "country",    "United States" },
    { "oldCountry", "USA" }
};

List<UserDto> updated = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

Console.WriteLine($"Updated {updated.Count} rows.");
foreach (UserDto u in updated)
{
    Console.WriteLine($"  id={u.id} Name={u.Name} Country={u.Country}");
}
```

Because the result is a `List`, an `UPDATE` that matches nothing simply returns an empty list — no null checks required.

### DELETE

Without a `RETURNING` clause, a `DELETE` returns an empty list.

```csharp
string sql = "DELETE FROM Users WHERE id = @id";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "id", 42 }
};

await SxmStatement.RunStatementAsync(sql, parameters);
```

With a `RETURNING` clause, a `DELETE` returns every row it removed. This is often the cleanest way to capture "what did I just delete?" without a preceding `SELECT`.

```csharp
string sql = "DELETE FROM Users WHERE Age < @minAge RETURNING *";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 }
};

List<UserDto> deleted = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

Console.WriteLine($"Deleted {deleted.Count} underage users.");
foreach (UserDto u in deleted)
{
    Console.WriteLine($"  Removed id={u.id} Name={u.Name}");
}
```

### Summary of return-value semantics

| Statement | Without `RETURNING` | With `RETURNING` Clause |
|---|---|---|
| `SELECT` | Rows selected (always) | n/a |
| `INSERT` | Empty list | Inserted row(s) |
| `UPDATE` | Empty list | Updated row(s) — one entry per matched row |
| `DELETE` | Empty list | Deleted row(s) — one entry per matched row |

Because the return type is always a `List`, you never need `null` checks. An empty list simply means "no rows were produced by this statement" — for a `SELECT` that's a legitimate empty result set; for an `INSERT`/`UPDATE`/`DELETE` without `RETURNING` it's the norm; for an `UPDATE`/`DELETE` *with* `RETURNING` it means the `WHERE` clause matched nothing.

---

## Overload Matrix

Both `SxmStatement` (static) and `SxmSqlTransaction` (instance) expose the same six overload shapes. Pick a row by how you want to pass parameters and a column by how you want to consume results.

| Parameter shape | Typed result (`List<TResult>`) | Raw result (`List<Dictionary<string, object?>>`) |
|---|---|---|
| **DTO** (`T userObjectParameters`) | `RunStatementAsync<T, TResult>(sql, dto[, databaseName])` | `RunStatementAsync<T>(sql, dto[, databaseName])` |
| **Dictionary** (`Dictionary<string, object?>`) | `RunStatementAsync<TResult>(sql, dict[, databaseName])` | `RunStatementAsync(sql, dict[, databaseName])` |
| **Positional** (`List<object>` with `@p0`, `@p1`, …) | `RunStatementAsync<TResult>(sql, list[, databaseName])` | `RunStatementAsync(sql, list[, databaseName])` |

The `[, databaseName]` argument exists only on the static `SxmStatement` overloads.

---

## Standalone Execution — `SxmStatement.RunStatementAsync`

The examples below use the same `User` table for consistency:

```sql
CREATE TABLE Users (
    id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT,
    Age     INTEGER,
    Country TEXT
);
```

And the same DTOs:

```csharp
public class UserSearchParams
{
    public int minAge { get; set; }
    public string? country { get; set; }
}

public class UserDto
{
    public int id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Country { get; set; }
}
```

### DTO parameters → typed results

The most type-safe form. Both the parameter object and the result rows are strongly typed.

```csharp
string sql = "SELECT id, Name, Age, Country FROM Users " +
             "WHERE Age > @minAge AND Country = @country";

UserSearchParams parameters = new UserSearchParams { minAge = 18, country = "USA" };

List<UserDto> users = await SxmStatement.RunStatementAsync<UserSearchParams, UserDto>(sql, parameters);

foreach (UserDto user in users)
{
    Console.WriteLine($"{user.Name} (Age {user.Age}) from {user.Country}");
}

// Target a specific database:
List<UserDto> archived =
    await SxmStatement.RunStatementAsync<UserSearchParams, UserDto>(sql, parameters, "ArchiveDB");
```

**Ideal when** you have a stable parameter shape and a stable result shape you can express as classes.

### DTO parameters → dictionary results

Typed parameters, flexible result handling. A common use is `INSERT … RETURNING *` where the input has a fixed shape but you want to consume the returned row generically.

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? country { get; set; }
}

string sql = "INSERT INTO Users (Name, Age, Country) " +
             "VALUES (@name, @age, @country) RETURNING *";

NewUserParams parameters = new NewUserParams { name = "Alice", age = 28, country = "USA" };

List<Dictionary<string, object?>> inserted =
    await SxmStatement.RunStatementAsync<NewUserParams>(sql, parameters);

int newId = (int)(long)inserted[0]["id"]!;
Console.WriteLine($"Created user id={newId}");
```

### Dictionary parameters → typed results

Best for dynamic parameter construction (e.g., building a search filter at runtime) while still consuming typed results.

```csharp
string sql = "SELECT id, Name, Age, Country FROM Users " +
             "WHERE Age > @minAge AND Country = @country";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 },
    { "country", "USA" }
};

List<UserDto> users = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

// Target a specific database:
List<UserDto> archived =
    await SxmStatement.RunStatementAsync<UserDto>(sql, parameters, "ArchiveDB");
```

### Dictionary parameters → dictionary results

The most flexible form. Neither side is compile-time typed.

```csharp
string sql = "SELECT id, Name, Age FROM Users WHERE Age > @minAge";
Dictionary<string, object?> parameters = new Dictionary<string, object?> { { "minAge", 18 } };

List<Dictionary<string, object?>> rows = await SxmStatement.RunStatementAsync(sql, parameters);

foreach (Dictionary<string, object?> row in rows)
{
    Console.WriteLine($"{row["Name"]} is {(long)row["Age"]!} years old");
}
```

### Positional parameters → typed results

For short queries where naming parameters is overkill. Use `@p0`, `@p1`, `@p2`, … in the SQL and pass values in order via `List<object>`.

```csharp
string sql = "SELECT id, Name, Age FROM Users WHERE Age > @p0 AND Country = @p1";
List<object> parameters = new List<object> { 18, "USA" };

List<UserDto> users = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);
```

### Positional parameters → dictionary results

Same overload works for a mutation with `RETURNING`, a bare `DELETE`, or any other statement that doesn't need typed results.

```csharp
// UPDATE ... RETURNING captures the row that changed:
string updateSql = "UPDATE Users SET Age = @p0 WHERE id = @p1 RETURNING *";
List<Dictionary<string, object?>> updated =
    await SxmStatement.RunStatementAsync(updateSql, new List<object> { 29, 1 });

// DELETE with no RETURNING returns an empty list — just ignore it:
string deleteSql = "DELETE FROM Users WHERE id = @p0";
await SxmStatement.RunStatementAsync(deleteSql, new List<object> { 42 });
```

**Queries with no parameters** still require a parameter argument — pass an empty collection:

```csharp
List<UserDto> all = await SxmStatement.RunStatementAsync<UserDto>(
    "SELECT id, Name, Age FROM Users",
    new List<object>());
```

---

## Transactional Execution — `SxmSqlTransaction.RunStatementAsync`

Use a transaction whenever two or more embedded SQL statements must succeed or fail as a group.

### Creating a transaction

Two factories are available:

```csharp
// Synchronous: creates a private (non-shared) connection to the named database.
SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

// Asynchronous: attaches to an existing SxmConnection.
// Required when the connection is shared (a lock is acquired asynchronously).
SxmConnection conn = new SxmConnection("AppData", shared: true);
await using SxmSqlTransaction tx = await SxmSqlTransaction.CreateAsync(conn);
```

Always dispose with `await using` so the transaction can commit (or clean up) properly.

### Auto-commit and auto-rollback

The transactional pattern is designed for straight-line code:

```csharp
string insertSql = "INSERT INTO Accounts (Name, Balance) VALUES (@name, @balance)";
Dictionary<string, object?> insertParams = new Dictionary<string, object?>
{
    { "name",    "Savings" },
    { "balance", 1000 }
};

string updateSql = "UPDATE Accounts SET Balance = Balance - @amount WHERE Name = @name";
Dictionary<string, object?> updateParams = new Dictionary<string, object?>
{
    { "amount", 100 },
    { "name",   "Checking" }
};

await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

await tx.RunStatementAsync(insertSql, insertParams);
await tx.RunStatementAsync(updateSql, updateParams);

// Reaching the end of the using block with no exceptions → commits.
// Throwing anywhere in between → the transaction is not committed; SQLite rolls back.
```

If one statement throws, subsequent `RunStatementAsync` calls on the **same** transaction return an empty list without executing. This makes error handling optional at each step:

```csharp
try
{
    await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");
    await tx.RunStatementAsync(sql1, params1);
    await tx.RunStatementAsync(sql2, params2); // skipped if sql1 threw
    // Auto-commit only reached if both succeeded.
}
catch (Exception ex)
{
    // Rolled back automatically; log or surface as needed.
    Console.WriteLine($"Transaction failed: {ex.Message}");
}
```

### Multiple statements in one transaction

Any mix of `SELECT`, `INSERT`, `UPDATE`, and `DELETE` (DML) works — they all flow through the same `RunStatementAsync`.

```csharp
public class OrderDto
{
    public int id { get; set; }
    public int UserId { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

// 1. Insert an order and get the generated row back.
string orderSql = "INSERT INTO Orders (UserId, Total, CreatedAt) " +
                  "VALUES (@userId, @total, @createdAt) RETURNING *";

Dictionary<string, object?> orderParams = new Dictionary<string, object?>
{
    { "userId",    1 },
    { "total",     149.99m },
    { "createdAt", DateTime.UtcNow }
};

List<OrderDto> inserted = await tx.RunStatementAsync<OrderDto>(orderSql, orderParams);
OrderDto order = inserted[0];

// 2. Insert two order items using the generated order id.
string itemSql = "INSERT INTO OrderItems (OrderId, ProductId, Quantity) " +
                 "VALUES (@orderId, @productId, @qty)";

Dictionary<string, object?> item1 = new Dictionary<string, object?>
{
    { "orderId",   order.id },
    { "productId", 101 },
    { "qty",       2 }
};
await tx.RunStatementAsync(itemSql, item1);

Dictionary<string, object?> item2 = new Dictionary<string, object?>
{
    { "orderId",   order.id },
    { "productId", 102 },
    { "qty",       1 }
};
await tx.RunStatementAsync(itemSql, item2);

// 3. Decrement inventory.
string invSql = "UPDATE Products SET Stock = Stock - @qty WHERE id = @productId";

Dictionary<string, object?> inv1 = new Dictionary<string, object?>
{
    { "qty",       2 },
    { "productId", 101 }
};
await tx.RunStatementAsync(invSql, inv1);

Dictionary<string, object?> inv2 = new Dictionary<string, object?>
{
    { "qty",       1 },
    { "productId", 102 }
};
await tx.RunStatementAsync(invSql, inv2);

// End of using: everything commits atomically.
```

### Reading, deciding, then writing

Because `RunStatementAsync` returns results, you can read inside the transaction, make a decision, and then write — all under the same lock and atomicity guarantee.

```csharp
public class BalanceRow { public decimal Balance { get; set; } }

string readSql  = "SELECT Balance FROM Accounts WHERE id = @id";
string writeSql = "UPDATE Accounts SET Balance = Balance - @amount WHERE id = @id";

Dictionary<string, object?> readParams = new Dictionary<string, object?>
{
    { "id", 1 }
};

Dictionary<string, object?> writeParams = new Dictionary<string, object?>
{
    { "amount", 100m },
    { "id",     1 }
};

await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

List<BalanceRow> rows = await tx.RunStatementAsync<BalanceRow>(readSql, readParams);

if (rows.Count == 0 || rows[0].Balance < 100m)
{
    throw new InvalidOperationException("Insufficient funds");
}

await tx.RunStatementAsync(writeSql, writeParams);
```

### Batch insert with typed results

Insert many records under one transaction and collect the returned rows.

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
}

List<NewUserParams> incoming = new List<NewUserParams>
{
    new NewUserParams { name = "John Doe",  age = 30, email = "john@example.com" },
    new NewUserParams { name = "Jane Smith", age = 25, email = "jane@example.com" },
    new NewUserParams { name = "Bob Wilson", age = 35, email = "bob@example.com"  }
};

string sql = "INSERT INTO Users (Name, Age, Email) " +
             "VALUES (@name, @age, @email) RETURNING *";

await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

List<UserDto> inserted = new List<UserDto>();
foreach (NewUserParams p in incoming)
{
    List<UserDto> rows = await tx.RunStatementAsync<NewUserParams, UserDto>(sql, p);
    inserted.AddRange(rows);
}

Console.WriteLine($"Inserted {inserted.Count} users atomically.");
```

**Why do this inside a transaction:**
- **Atomicity** — all inserts commit together or none do.
- **Performance** — dramatically fewer fsyncs than the same inserts run standalone.

### Explicit commit

Auto-commit-on-dispose is the recommended pattern, but you can commit explicitly if you need to end the SQL transaction earlier while continuing to hold the connection:

```csharp
await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

await tx.RunStatementAsync(sql1, params1);
await tx.RunStatementAsync(sql2, params2);

await tx.CommitTransactionAsync();

// The connection lock is still held by the transaction wrapper until DisposeAsync.
// Additional RunStatementAsync calls after an explicit commit start a new SQLite transaction.
```

---

## Working with Parameters

### Positional (`@p0`, `@p1`, …)

```csharp
string sql = "SELECT * FROM Users WHERE Age > @p0 AND Country = @p1";
List<object> parameters = new List<object> { 18, "USA" };
```

- ✅ Concise for short queries.
- ❌ Easy to get the order wrong with many parameters.

### Named (`@minAge`, `@country`, …) via `Dictionary`

```csharp
string sql = "SELECT * FROM Users WHERE Age > @minAge AND Country = @country";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 },
    { "country", "USA" }
};
```

- ✅ Self-documenting; order-independent.
- ✅ Case-sensitive keys — must match the `@name` used in the SQL, without the `@`.

### Named via DTO

```csharp
public class UserSearchParams
{
    public int minAge { get; set; }
    public string? country { get; set; }
}
```

- ✅ Reusable across queries with the same shape.
- ✅ Compile-time checked; refactor-friendly.
- ✅ Property names must match `@name` in the SQL, without the `@` (case-sensitive).

### Supported parameter types

SQLiteXM binds the common CLR types to SQLite parameters automatically. The comments below name the SQLite **storage class** each value ends up in — those storage classes (TEXT, INTEGER, REAL, BLOB, NULL) are SQLite's, not SQLiteXM's. `bool` and `DateTime` don't have native SQLite types, so SQLiteXM follows SQLite's own convention: booleans stored as `0`/`1` INTEGER, dates stored as ISO 8601 TEXT.

```csharp
List<object> parameters = new List<object>
{
    "string value",         // TEXT
    42,                     // INTEGER
    3.14,                   // REAL
    true,                   // INTEGER (1 / 0)
    DateTime.UtcNow,        // TEXT (ISO 8601)
    new byte[] { 1, 2, 3 }, // BLOB
};
```

### NULL values

Pass `null` (in a `Dictionary<string, object?>` or DTO property) or `DBNull.Value` (in a `List<object>`) for SQL `NULL`:

```csharp
await SxmStatement.RunStatementAsync(
    "INSERT INTO Users (Name, Email, Phone) VALUES (@p0, @p1, @p2)",
    new List<object> { "John", "john@example.com", DBNull.Value });
```

---

## Result Type Handling

SQLite has a small set of storage classes (INTEGER, REAL, TEXT, BLOB, NULL). When you consume rows as `Dictionary<string, object?>`, you are seeing those raw values:

```csharp
Dictionary<string, object?> row = rows[0];

int id = (int)(long)row["id"]!;                          // INTEGER → long
bool isActive = ((long)row["IsActive"]!) != 0;           // stored as 0/1
DateTime created = DateTime.Parse((string)row["CreatedAt"]!); // TEXT (ISO 8601)
byte[] data = (byte[])row["Payload"]!;                   // BLOB
```

When you use the typed overloads (`RunStatementAsync<TResult>` / `RunStatementAsync<T, TResult>`), SQLiteXM performs the conversion for you based on the target property type, so this boilerplate goes away.

---

## Best Practices

### 1. Always parameterize

**❌ Never concatenate user input into SQL:**
```csharp
string sql = $"SELECT * FROM Users WHERE Name = '{userInput}'"; // SQL injection risk
```

**✅ Use parameters:**
```csharp
await SxmStatement.RunStatementAsync(
    "SELECT * FROM Users WHERE Name = @p0",
    new List<object> { userInput });
```

### 2. Prefer typed results

Typed results catch mistakes at compile time and eliminate manual casts:

```csharp
List<UserDto> users = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);
```

Reach for dictionary results only when the shape truly is dynamic.

### 3. Group related writes in a transaction

If two or more statements must succeed or fail together, use `SxmSqlTransaction` rather than sequential standalone calls:

```csharp
await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");
await tx.RunStatementAsync(sql1, params1);
await tx.RunStatementAsync(sql2, params2);
// Atomic.
```

Sequential standalone calls give up atomicity — a failure between them leaves the database half-updated.

### 4. Use `RETURNING *` on inserts when you need the generated row

SQLite supports `RETURNING` on `INSERT`, `UPDATE`, and `DELETE`. Combined with a typed overload, you get the persisted record back — including auto-generated columns like `id` — without a second round-trip.

```csharp
string sql = "INSERT INTO Users (Name, Age) VALUES (@name, @age) RETURNING *";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name", "Alice" },
    { "age",  28 }
};

List<UserDto> inserted = await SxmStatement.RunStatementAsync<UserDto>(sql, parameters);

int newId = inserted[0].id;
```

### 5. Always `await using` a transaction

```csharp
// ✅
await using SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");

// ❌ Easy to forget disposal, and a missed dispose means no commit and a held connection lock.
SxmSqlTransaction tx = SxmSqlTransaction.Create("AppData");
```

### 6. Name the database when you use more than one

Standalone calls default to the database configured in `SqlStatements.json`. When you have multiple databases, pass `databaseName` explicitly to avoid accidental cross-database queries:

```csharp
List<UserDto> live    = await SxmStatement.RunStatementAsync<UserDto>(sql, p);
List<UserDto> archive = await SxmStatement.RunStatementAsync<UserDto>(sql, p, "ArchiveDB");
```

For transactions, choose the database when you create the transaction:

```csharp
await using SxmSqlTransaction tx = SxmSqlTransaction.Create("ArchiveDB");
```

### 7. Don't nest transactions

SQLite itself does not support nested transactions — a single connection can have only one transaction active at a time. `SxmSqlTransaction` reflects that by registering itself as the ambient transaction and refusing to create another while one is already active. This is not a SQLiteXM restriction; it is a property of the underlying engine.

If you find yourself wanting to nest, restructure the calling code so a single transaction spans the whole unit of work, or complete and dispose the outer transaction before starting a new one. (SQLite's `SAVEPOINT` feature offers partial-rollback semantics within a single transaction, but it is not a true nested transaction and is outside the scope of this guide.)

---

## Summary

- All embedded SQL in SQLiteXM flows through a single method name: **`RunStatementAsync`**.
- Use the **static** `SxmStatement.RunStatementAsync` for one-off statements.
- Use the **instance** `SxmSqlTransaction.RunStatementAsync` when multiple statements must be atomic.
- Pick an overload by combining a parameter shape (**DTO**, **Dictionary**, **positional list**) with a result shape (**typed** or **dictionary**) — see the [Overload Matrix](#overload-matrix).
- Always parameterize, prefer typed results, and always `await using` transactions.

For entity-oriented data access and LINQ, see the companion guides:

- [Getting Started](GettingStarted.md)
- [LINQ Support](QUERYING_DATA.md)
- [Relationships](RELATIONSHIPS.md)

