# Executing DML in SQLiteXM — Entities, SQL, and Transactional Blocks

SQLiteXM lets you write to a database in three different ways: directly through an **entity instance**, through a single **SQL statement** call, or inside a **transactional block** that groups many statements — of any kind — into one atomic unit of work. All three paths share the same connection management, the same statement pipeline, and the same commit / rollback semantics, so moving from a one-line insert to a multi-statement transaction is a change in *shape*, not in *API*.

This guide walks that spectrum end to end: from the simplest single-statement calls (`entity.SaveAsync()` and `SxmSql.RunStatementAsync(...)`) up to the fully-transactional `SxmTransaction` block that unifies entity DML, named SQL, embedded SQL, and LINQ on one connection and one transaction.

> ?? This document is a road map. Each individual surface has its own detailed guide: [Entity DML](SQLiteXM-Entity-DML.md), [Embedded SQL](SQLiteXM-SQL-Support.md), [Named SQL Statements](SQLiteXM-Named-Statements.md), and [LINQ Support](SQLiteXM-LINQ-Support.md). The value of *this* document is showing how they fit together and when to reach for which.

---

## Table of Contents

- [Concepts & Conventions](#concepts--conventions)
- [The Two Execution Modes](#the-two-execution-modes)
- [Standalone SQL — `SxmSql.RunStatementAsync`](#standalone-sql--sxmsqlrunstatementasync)
- [Standalone Entity DML — `SaveAsync` / `DeleteAsync`](#standalone-entity-dml--saveasync--deleteasync)
- [Transactional Blocks — `SxmTransaction`](#transactional-blocks--SxmTransaction)
- [Mixing Everything in One Transaction](#mixing-everything-in-one-transaction)
- [Error Handling and Rollback](#error-handling-and-rollback)
- [Choosing the Right API](#choosing-the-right-api)
- [Best Practices](#best-practices)

---

## Concepts & Conventions

> ?? **DML in this document** means data-modifying or data-returning statements — `SELECT`, `INSERT`, `UPDATE`, and `DELETE` — regardless of whether the caller expresses them as an entity method, a SQL string, a named statement, or a LINQ query. Schema (DDL) operations are out of scope here.

> ?? **Prerequisites.** All examples assume that `SxmDatabase.InitializeAsync(...)` has been called once at application startup with your `SqlStatements.json` file, and that `SxmDatabase.RegisterEntitiesAsync(...)` has registered every entity type your code will use. See [Getting Started](GettingStarted.md).

> ?? **Example entities.** Throughout this guide the examples use two minimal illustrative entities:
>
> ```csharp
> [Table(IsColumnAttributeRequired = false)]
> public class Customer : SxmEntity
> {
>     public string? Name  { get; set; }
>     public string? Email { get; set; }
> }
>
> [Table(IsColumnAttributeRequired = false)]
> public class Order : SxmEntity
> {
>     public long    CustomerId { get; set; }
>     public string? Product    { get; set; }
>     public decimal Amount     { get; set; }
> }
> ```
>
> Every entity inherits `SxmEntity` and has an auto-managed `id` primary-key column.

---

## The Two Execution Modes

There are only two execution modes to remember.

- **Standalone.** A single call is its own atomic unit of work. SQLiteXM opens a transaction, runs the statement, and commits (or rolls back on failure) before the call returns. Neither the caller nor any surrounding code sees the transaction. This is the mode used by `entity.SaveAsync()`, `entity.DeleteAsync()`, and every overload of `SxmSql.RunStatementAsync(...)` when they are not called from inside an `SxmTransaction` block.
- **Transactional block.** Many calls — of any kind — are grouped inside `await using var ctx = new SxmTransaction(...);`. The block auto-commits on clean disposal and auto-rolls back on exception. Entity DML, `RunStatementAsync`, and LINQ inside the block all run on the *same* connection and the *same* transaction, and either all commit together or all roll back together.

> ?? The same line of code that saves an entity standalone — `await customer.SaveAsync();` — participates in an `SxmTransaction` transaction automatically when written inside one. Nothing at the call site changes. The choice between standalone and transactional is made once, at the surrounding scope, not repeated at every call.

---

## Standalone SQL — `SxmSql.RunStatementAsync`

`SxmSql.RunStatementAsync` runs a single SQL statement and returns its results immediately. Each call is its own atomic unit of work: SQLiteXM auto-commits on success and auto-rolls back on failure. The caller never sees a transaction.

### Quick start — embedded SQL

The simplest way to run a statement is to pass the SQL text directly to `RunStatementAsync`. This is called *embedded* SQL because the statement lives inline in your C# code.

**(a) Untyped result, no parameters:**

```csharp
var rows = await SxmSql.RunStatementAsync("SELECT id, Name FROM Customer");
```

Each row is a `Dictionary<string, object?>` keyed by column name — handy for ad-hoc queries.

**(b) Typed result, no parameters:**

```csharp
List<Customer> all = await SxmSql.RunStatementAsync<Customer>(
    "SELECT id, Name, Email FROM Customer");
```

Each row is mapped onto a `Customer` instance by column name.

**(c) Typed result, dictionary parameters:**

```csharp
var parms = new Dictionary<string, object?> { ["MinAmount"] = 100m };
List<Order> big = await SxmSql.RunStatementAsync<Order>(
    "SELECT id, Product, Amount FROM `Order` WHERE Amount >= @MinAmount",
    parms);
```

Parameters use `@Name` placeholders and are supplied as a dictionary or as a positional `List<object>`.

> ?? The user-object parameter shape (see the overload table below) is **not** available for embedded SQL. Use the dictionary or positional-list overloads for parameterized embedded statements.

For the full embedded SQL reference, see [Embedded SQL](SQLiteXM-SQL-Support.md).

### The eight overloads

`RunStatementAsync` comes in eight overloads — the product of **two result shapes** and **four parameter shapes**.

**Result shapes:**

- **Typed.** `Task<List<TResult>>`, where each row is mapped onto an instance of your POCO. Requires `TResult : class, new()`.
- **Untyped.** `Task<List<Dictionary<string, object?>>>`, where each row is a column-name ? value dictionary.

**Parameter shapes:**

| # | 2nd argument | Meaning |
|---|---|---|
| 1 | *(none)* | The statement takes no parameters. |
| 2 | `T userObjectParameters` | Property values on the supplied object provide the parameters, matched by name to the target table's columns. **Named statements only** — cannot be combined with embedded SQL. |
| 3 | `Dictionary<string, object?>` | Named parameters. |
| 4 | `List<object>` | Positional parameters, matched to the statement's placeholders in order. |

**Typed result — `Task<List<TResult>>` where `TResult : class, new()`:**

```csharp
static Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName,
    string? databaseName = default);

static Task<List<TResult>> RunStatementAsync<T, TResult>(
    string sqlOrStatementName,
    T userObjectParameters,
    string? databaseName = default);

static Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName,
    Dictionary<string, object?> sqlStatementParameters,
    string? databaseName = default);

static Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName,
    List<object> sqlStatementParameters,
    string? databaseName = default);
```

**Untyped result — `Task<List<Dictionary<string, object?>>>`:**

```csharp
static Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName,
    string? databaseName = default);

static Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(
    string sqlOrStatementName,
    T userObjectParameters,
    string? databaseName = default);

static Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName,
    Dictionary<string, object?> sqlStatementParameters,
    string? databaseName = default);

static Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName,
    List<object> sqlStatementParameters,
    string? databaseName = default);
```

> ?? Every overload accepts an optional trailing `databaseName`. Omit it to run against the default database; provide it to target a specific named database. See [Multi-Database Support](multi_database.md).

### Named SQL statements

The first argument to every overload is called `sqlOrStatementName`. If SQLiteXM finds a matching entry in the statements registry loaded from your `SqlStatements.json` file, it runs that *named* statement; otherwise it falls back to parsing the string as embedded SQL.

Named statements let you keep SQL out of your C# code, reuse the same statement from many call sites, and take advantage of the user-object parameter shape.

Assume `GetAllCustomers` is defined in `SqlStatements.json` as `SELECT id, Name, Email FROM Customer`. The call site is then simply:

```csharp
List<Customer> all = await SxmSql.RunStatementAsync<Customer>("GetAllCustomers");
```

Named statements also unlock the user-object parameter shape, where an object's property values supply the parameters by name:

```csharp
var probe = new Customer { Email = "ada@analytical.io" };
List<Customer> matches =
    await SxmSql.RunStatementAsync<Customer, Customer>("GetCustomerByEmail", probe);
```

For the full named-statement reference, see [Named SQL Statements](SQLiteXM-Named-Statements.md).

---

## Standalone Entity DML — `SaveAsync` / `DeleteAsync`

Every class that inherits from `SxmEntity` can persist and remove itself with a single method call:

| Method | Purpose |
|---|---|
| `SaveAsync()` | Inserts the row if it does not yet exist; updates it in place if it does. |
| `DeleteAsync()` | Removes the row identified by the entity's primary key. No-op if the row does not exist. |

The entity knows which table and which database it belongs to, so nothing about the target has to be passed in.

```csharp
// INSERT — id is 0, so SaveAsync performs an insert and populates id.
var customer = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" };
await customer.SaveAsync();

// UPDATE — same object, same call site; SaveAsync detects the existing row.
customer.Email = "ada@analytical.io";
await customer.SaveAsync();

// DELETE — removes the row; the in-memory object remains.
await customer.DeleteAsync();
```

> ?? `SaveAsync` is an in-place `UPDATE`, not `INSERT OR REPLACE`. Triggers, foreign keys, and the entity's existing `id` are preserved when a row is updated.

> ?? There is intentionally no `InsertAsync` or `UpdateAsync` on the public entity surface. `SaveAsync` covers both cases so call sites do not need to know or care which one happens.

Each call is atomic on its own. It commits if the statement succeeds and rolls back if it throws, without any commit / rollback calls in the caller's code.

For the full entity DML reference, see [Entity DML](SQLiteXM-Entity-DML.md).

---

## Transactional Blocks — `SxmTransaction`

Use `SxmTransaction` when you need multiple statements to succeed or fail as a single unit of work, or when you want to combine LINQ with SQL and entity DML in one atomic operation.

An `SxmTransaction`:

- Opens a single connection and a single SQLite transaction that live for the duration of the block.
- Registers itself as the **ambient** transaction, so any `entity.SaveAsync()` / `entity.DeleteAsync()` call inside the block enlists automatically.
- **Auto-commits** the transaction on clean disposal.
- **Auto-rolls back** the transaction if any statement inside the block throws.

Because commit and rollback are automatic, most code never calls `CommitTransactionAsync` or `RollbackTransactionAsync` explicitly. They are available for the cases that need finer control.

### Creating a context

Always create the context with `await using` so that disposal — and therefore commit or rollback — is asynchronous:

```csharp
await using (var ctx = new SxmTransaction())
{
    // ... statements ...
}   // <- auto-commit here (auto-rollback if an exception escaped)
```

The database targeted by the context is fixed at construction. Pass a database name to run the entire block against a non-default database:

```csharp
await using (var ctx = new SxmTransaction("Archive"))
{
    // Everything inside runs against the "Archive" database.
}
```

Because the database is fixed by the context, the `RunStatementAsync` overloads on `SxmTransaction` do **not** take a `databaseName` argument.

### Entity DML inside a context

`SaveAsync()` and `DeleteAsync()` detect the ambient context automatically. The call site is identical to the standalone case — no transaction argument, no enlistment method:

```csharp
await using (var ctx = new SxmTransaction())
{
    var customer = new Customer { Name = "Grace Hopper" };
    await customer.SaveAsync();          // enlists in the context transaction

    var order = new Order
    {
        CustomerId = customer.id,
        Product    = "Compiler",
        Amount     = 42m
    };
    await order.SaveAsync();             // same transaction

}   // both rows commit together
```

### SQL inside a context — `RunStatementAsync`

`SxmTransaction` exposes its own set of `RunStatementAsync` overloads that mirror the eight on `SxmSql`, with two differences:

- There is no `databaseName` parameter — the database is fixed by the context.
- The statement runs inside the context's transaction.

**Typed result — `Task<List<TResult>>` where `TResult : class, new()`:**

```csharp
Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName);

Task<List<TResult>> RunStatementAsync<T, TResult>(
    string sqlOrStatementName,
    T userObjectParameters);

Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName,
    Dictionary<string, object?> sqlStatementParameters);

Task<List<TResult>> RunStatementAsync<TResult>(
    string sqlOrStatementName,
    List<object> sqlStatementParameters);
```

**Untyped result — `Task<List<Dictionary<string, object?>>>`:**

```csharp
Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName);

Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(
    string sqlOrStatementName,
    T userObjectParameters);

Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName,
    Dictionary<string, object?> sqlStatementParameters);

Task<List<Dictionary<string, object?>>> RunStatementAsync(
    string sqlOrStatementName,
    List<object> sqlStatementParameters);
```

The parameter- and result-shape rules from the standalone form apply unchanged, including the rule that embedded SQL cannot be combined with a user-object parameter.

```csharp
await using (var ctx = new SxmTransaction())
{
    var parms = new Dictionary<string, object?> { ["Status"] = "PENDING" };
    List<Order> pending = await ctx.RunStatementAsync<Order>("GetOrdersByStatus", parms);

    foreach (var o in pending)
    {
        o.Product = o.Product + " (processed)";
        await o.SaveAsync();                 // same transaction as the read above
    }
}
```

### LINQ inside a context

Inside an `SxmTransaction`, `ctx.GetTable<T>()` returns a queryable table backed by the context's connection and transaction. Standard LINQ operators, along with the async and bulk-write extensions provided by SQLiteXM, all run inside the same transaction as everything else in the block:

```csharp
await using (var ctx = new SxmTransaction())
{
    var vips = ctx.GetTable<Customer>()
                  .Where(c => c.Email!.EndsWith("@vip.com"))
                  .ToList();
}
```

For the full LINQ reference, see [LINQ Support](SQLiteXM-LINQ-Support.md).

### Manual commit and rollback (optional)

Auto-commit on dispose is the recommended pattern. When you need to end a transaction earlier than that — for example, to release locks before a long computation — you can call the context's transaction methods explicitly:

```csharp
await using (var ctx = new SxmTransaction())
{
    await DoFirstBatchAsync(ctx);
    await ctx.CommitTransactionAsync();   // ends the first transaction

    await DoSecondBatchAsync(ctx);
}   // a new transaction started implicitly by the second batch is auto-committed here
```

`RollbackTransactionAsync()` discards the current transaction's work and also clears the context's faulted state, so the context can be reused for subsequent work.

---

## Mixing Everything in One Transaction

The defining feature of `SxmTransaction` is that a single block can **freely mix all three statement kinds** — entity DML, SQL (named or embedded), and LINQ — and every statement participates in the same transaction. The block either commits every change together or rolls back every change together.

```csharp
await using (var ctx = new SxmTransaction())
{
    // 1) LINQ read
    var customer = ctx.GetTable<Customer>()
                      .First(c => c.Email == "grace@example.com");

    // 2) Named SQL read
    var parms = new Dictionary<string, object?> { ["CustomerId"] = customer.id };
    List<Order> theirOrders =
        await ctx.RunStatementAsync<Order>("GetOrdersForCustomer", parms);

    // 3) Entity DML writes
    foreach (var order in theirOrders)
    {
        order.Amount *= 1.10m;               // 10% price adjustment
        await order.SaveAsync();
    }

    // 4) Embedded SQL write
    var auditParms = new Dictionary<string, object?>
    {
        ["CustomerId"] = customer.id,
        ["Note"]       = "Prices adjusted"
    };
    await ctx.RunStatementAsync(
        "INSERT INTO AuditLog (CustomerId, Note) VALUES (@CustomerId, @Note)",
        auditParms);

}   // one commit for all four steps (or one rollback if anything threw)
```

> ?? This is exactly the same pattern you would use in a business-logic method that needs a consistent read, followed by conditional writes, followed by an audit entry — all as a single atomic operation.

---

## Error Handling and Rollback

SQLiteXM's DML APIs propagate exceptions rather than returning error codes. The recommended pattern is a standard `try` / `catch` around either the single call or the entire `await using` block.

### Standalone calls

For `entity.SaveAsync()`, `entity.DeleteAsync()`, and `SxmSql.RunStatementAsync(...)`, an exception aborts the internal auto-transaction — the write has already been rolled back by the time your `catch` runs:

```csharp
try
{
    await customer.SaveAsync();
}
catch (Exception ex)
{
    // The insert/update was rolled back. Log and recover as appropriate.
    logger.LogError(ex, "Failed to save customer.");
}
```

### Inside an `SxmTransaction` block

Inside a context, an exception on any statement:

1. Marks the context as **faulted**.
2. Causes any *subsequent* write attempts in the same block to be silently skipped, preventing cascading errors from statements that depend on the failed one.
3. Causes the transaction to be **rolled back automatically** when the context is disposed.

The typical pattern is to wrap the entire `await using` block:

```csharp
try
{
    await using var ctx = new SxmTransaction();

    await DoWorkAsync(ctx);
    // No manual commit needed — dispose commits on success, rolls back on failure.
}
catch (Exception ex)
{
    logger.LogError(ex, "Transactional work failed and was rolled back.");
}
```

If you want to recover a faulted context and reuse it — instead of letting it dispose — call `RollbackTransactionAsync()`. That discards the failed transaction, clears the faulted flag, and lets subsequent statements start a fresh transaction on the same context.

### Exceptions to know about

| Exception | When it is thrown |
|---|---|
| `ArgumentException` | Using the user-object parameter overload with embedded SQL. Use the dictionary or positional-list overload instead. |
| `InvalidOperationException` | Calling `CommitTransactionAsync()` on a faulted `SxmTransaction` (call `RollbackTransactionAsync()` first); missing generated INSERT/UPDATE/DELETE statement for an entity type; database-name mismatch when a context tries to join an ambient transaction that is bound to a different database. |
| `ObjectDisposedException` | Using an `SxmTransaction` after it has been disposed. |

Statement-level failures (constraint violations, syntax errors, and so on) surface as SQLite exceptions, unchanged and unwrapped where practical.

---

## Choosing the Right API

| Situation | Recommended API | Why |
|---|---|---|
| Insert, update, or delete a single entity. | `entity.SaveAsync()` / `entity.DeleteAsync()` | Smallest possible surface. The entity already knows its table and database. |
| Run one named or embedded SQL statement and get results back. | `SxmSql.RunStatementAsync(...)` | Single-statement work with auto-commit. Accepts an optional `databaseName`. |
| Two or more statements that must succeed or fail together. | `SxmTransaction` block | Auto-commit on clean dispose, auto-rollback on exception. |
| Combine LINQ with SQL and/or entity DML in one transaction. | `SxmTransaction` block | The only place all three kinds share a transaction. |
| One block of work targeted at a non-default database. | `new SxmTransaction("OtherDb")` | Fixes the database once for every statement inside. |
| Ad-hoc query against a non-default database, no transaction needed. | `SxmSql.RunStatementAsync(..., databaseName: "OtherDb")` | Only the standalone form takes a per-call `databaseName`. |
| Work spanning multiple databases in one operation. | Not supported in a single transaction. | Use separate `SxmTransaction` blocks (or standalone calls) per database. |

---

## Best Practices

- **Start simple.** If the work is a single write or a single read, use `entity.SaveAsync()` / `entity.DeleteAsync()` or `SxmSql.RunStatementAsync(...)`. There is no benefit to wrapping single statements in an `SxmTransaction`.
- **Reach for `SxmTransaction` when work is compound.** Any time correctness requires that two or more statements succeed or fail together, put them inside one `await using` block.
- **Prefer `await using` for context blocks.** Synchronous `using` still works but will not asynchronously commit or roll back on disposal.
- **Do not commit manually unless you have a reason.** Auto-commit on dispose is the intended pattern. Manual `CommitTransactionAsync` / `RollbackTransactionAsync` are for advanced scenarios (batch boundaries, business-rule aborts, and similar).
- **Wrap the block, not each statement.** Inside a context, individual failures already short-circuit the remaining writes. A single `try` / `catch` around the whole `await using` block is usually enough.
- **Do not mix databases in one context.** An `SxmTransaction` is bound to one database. If you need to touch a second database, open a separate context for it.
- **Keep contexts short-lived.** A context holds a database connection and, once any write has run, an open transaction. Open it, do the work, dispose it.

---

## See Also

- [Entity DML](SQLiteXM-Entity-DML.md)
- [Embedded SQL Query Support](SQLiteXM-SQL-Support.md)
- [Named SQL Statements](SQLiteXM-Named-Statements.md)
- [LINQ Support](SQLiteXM-LINQ-Support.md)
- [Multi-Database Support](multi_database.md)
- [Defining Entities](defining_entities.md)
