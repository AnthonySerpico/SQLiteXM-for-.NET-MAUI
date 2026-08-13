# Executing DML in SQLiteXM — Entities, SQL, and Transactional Blocks

SQLiteXM lets you read and write to a database in multiple ways: 
1. Directly using an **entity instance** — we call this entity DML. 
2. Executing a single **SQL statement** — embedded SQL or named SQL
3. Inside a **transactional block** that executes multiple LINQ statements as one atomic unit of work. 
4. Inside a **transactional block** that can execute all of the above together as one atomic unit of work. 

This guide walks that spectrum end to end: from the simplest single-statement calls (`entity.SaveAsync()` and `SxmSql.RunStatementAsync(...)`) up to the fully-transactional `SxmTransaction` block that unifies entity DML, named SQL, embedded SQL, and LINQ on one transaction.


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

>💡 Database operations in this guide means statements that modify data, return data, or when using a `RETURNING` clause, do both — `SELECT`, `INSERT`, `UPDATE`, and `DELETE`, regardless of whether they're expressed as LINQ, SQL, or entity DML.

> 💡  **Prerequisites.** All examples assume that `SxmDatabase.InitializeAsync(...)` has been called once at application startup with your `SqlStatements.json` file, and that `SxmDatabase.RegisterEntitiesAsync(...)` has registered every entity type your code will use. See [Getting Started](GettingStarted.md).

> 💡 **Example entities.** Throughout this guide the examples use two minimal illustrative entities:
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
> User entities inherit from `SxmEntity` and have an auto-managed `id` primary-key column.

---

## The Two Execution Modes

There are only two execution modes to remember.

- **Standalone.** A single call is its own atomic unit of work. In standalone mode, SQLiteXM opens a transaction, runs a statement, and commits (or rolls back on failure) before the call returns. Neither the caller nor any surrounding code sees the transaction. This is the mode used by entity DML — `entity.SaveAsync()` `entity.DeleteAsync()` — and every overload of the static `SxmSql.RunStatementAsync(...)` method.
<br>&nbsp;</br>
- **Transactional block.** Many calls — of any kind — are grouped inside `await using var ctx = new SxmTransaction(...);`. The block auto-commits on clean disposal and auto-rolls back on exception. LINQ, entity DML, and `RunStatementAsync` inside the block all run on the *same* connection and the *same* transaction, and either all commit together or all roll back together.

---

## Standalone Entity DML — `SaveAsync` / `DeleteAsync`

Every class that inherits from `SxmEntity` can save and delete itself from the database with a single method call:

| Method | Purpose |
|---|---|
| `SaveAsync()` | Inserts the row if it does not yet exist; updates it in place if it does. |
| `DeleteAsync()` | Removes the row identified by the entity's primary key. No-op if the row does not exist. |

The entity knows which table and which database it belongs to, so nothing about the entity database or table needs to be passed in.

```csharp
// INSERT — id is 0, so SaveAsync performs an insert and populates id.
Customer customer = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" };
await customer.SaveAsync();

// UPDATE — same object, same call site; SaveAsync detects the existing row.
customer.Email = "ada@analytical.io";
await customer.SaveAsync();

// DELETE — removes the row; the in-memory object remains.
await customer.DeleteAsync();
```

Each call is atomic on its own. It commits if the statement succeeds and rolls back if it throws, without any commit / rollback calls in the caller's code.

> 💡 There is intentionally no `InsertAsync` or `UpdateAsync` on the public entity surface. `SaveAsync` covers both cases so call sites do not need to know or care which one happens.

> 💡 When updating, `SaveAsync` performs an in-place `UPDATE`, not `INSERT OR REPLACE`. Triggers, foreign keys, and the entity's existing `id` are preserved when a row is updated.

---


## Standalone SQL — `SxmSql.RunStatementAsync`

The static method `SxmSql.RunStatementAsync` runs a single SQL statement and returns its results immediately. Each call is its own atomic unit of work: SQLiteXM auto-commits on success and auto-rolls back on failure. The caller never sees a transaction.

### Quick start — embedded SQL

The simplest way to use `SxmSql.RunStatementAsync` is to pass the SQL text directly to the `RunStatementAsync` method. This is called *embedded* SQL because the statement lives inline in your C# code. See the three example below.

(1) Dictionary result, no SQL parameters:

```csharp
List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync("SELECT id, Name FROM Customer");
```

The example above shows a simple SQL SELECT that does not take any parameters. It returns a `List` of `Dictionary<string, object?>` where each Dictionary in the List represents a single row returned by the query, keyed by column name.

(2) Typed result, no SQL parameters:
```csharp
List<Customer> all = await SxmSql.RunStatementAsync<Customer>("SELECT id, Name, Email FROM Customer");
```

The example above shows another simple SQL SELECT. It returns a `List` of `Customer` objects where each Customer in the List represents a single row returned by the query. Columns are mapped to public properties by name.


**(3) Typed result, dictionary parameters:**

```csharp
var parms = new Dictionary<string, object?> { ["MinAmount"] = 100m };

List<Order> big = await SxmSql.RunStatementAsync<Order>("SELECT id, Product, Amount FROM `Order` WHERE Amount >= @MinAmount", parms);
```

The example above shows a SQL SELECT that takes one named parameter; `@MinAmount`. It returns a `List` of `Order` objects where each `Order` in the List represents a single row returned by the query.


### The eight overloads

`RunStatementAsync` has eight overloads: two result types × four SQL parameter options.

**Result types:**

- **Typed.** `Task<List<TResult>>` — Returns a list of your entity or model objects, with one instance per database row. Each column value is automatically mapped to a matching property. Your result type must be a class with a parameterless constructor.
<br>&nbsp;</br>
- **Untyped.** `Task<List<Dictionary<string, object?>>>` — Returns a list of Dictionaries, with one Dictionary per database row. Each dictionary maps column names to their values. Useful for ad-hoc queries or dynamic schemas.

**Typed result overloads:**

```csharp
// The SQL statement takes no parameters.
static Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, string? databaseName = default);

// `userObjectParameters` object provides the SQL parameters, public properties are matched by name to the SQL parameters in the statement.
static Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default);

// `sqlStatementParameters` Dictionary provides the SQL parameters, keyed by name to the SQL parameters in the statement.
static Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default);

// `sqlStatementParameters` List provides the SQL parameters, matched by position to the statement's placeholders.
static Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default);
```

**Untyped result overloads:**

```csharp
// The SQL statement takes no parameters.
static Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, string? databaseName = default);

// `userObjectParameters` object provides the SQL parameters, public properties are matched by name to the SQL parameters in the statement
static Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlStatementName, T userObjectParameters, string? databaseName = default);

// `sqlStatementParameters` Dictionary provides the SQL parameters, keyed by name to the SQL parameters in the statement.
static Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default);

// `sqlStatementParameters` List provides the SQL parameters, matched by position to the statement's placeholders.
static Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default);
```

> 💡 Every overload accepts an optional trailing `databaseName`. Omit it to run against the default database; provide it to target a specific named database. See [Multi-Database Support](multi_database.md).

### Using named SQL statements with `SxmSql.RunStatementAsync`

The first argument to every `RunStatementAsync` overload is called `sqlStatementName`. This argument is processed in the following order:
1. Conceptually, the SQL statement is looked up by name in the `SqlStatements.json` file of the project. If a statement with that name exists, its contents are used as the SQL statement.
2. If no statement with that name exists, the argument is treated as the SQL statement itself.

Named statements let you keep SQL out of your C# code allowing reuse of the same statement from multiple call sites. It also organizes your SQL in a single, easy to audit location. Every `RunStatementAsync` overload supports named statements.

Assume `GetAllCustomers` is defined in `SqlStatements.json` as `SELECT id, Name, Email FROM Customer`. The call site is then simply:

```csharp
List<Customer> all = await SxmSql.RunStatementAsync<Customer>("GetAllCustomers");
```

In the example below, assume `GetCustomerByEmail` is defined in `SqlStatements.json` as `SELECT * FROM Customer WHERE Email = @Email`.

```csharp
Customer probe = new Customer { Email = "ada@analytical.io" };
List<Customer> matches = await SxmSql.RunStatementAsync<Customer, Customer>("GetCustomerByEmail", probe); 
```

For the full named-statement reference, see [Named SQL Statements](SQLiteXM-Named-Statements.md).

---

## Transactional Blocks — `SxmTransaction`

Use `SxmTransaction` when you wish to run LINQ statements or when you want to combine LINQ, SQL, and entity DML in one atomic operation.

An `SxmTransaction`:

- Opens a single connection and a single SQLite transaction that live for the duration of the block.
- Registers itself as the **ambient** transaction, so any `entity.SaveAsync()` / `entity.DeleteAsync()` call inside the block enlists automatically.
- **Auto-commits** the transaction on clean disposal.
- **Auto-rolls back** the transaction if any statement inside the block throws.

Because commit and rollback are automatic, most code never calls `CommitTransactionAsync` or `RollbackTransactionAsync` explicitly. They are available for the cases that need finer control.

### Creating a transaction

Always create the transactionwith `await using` so that disposal — and therefore commit or rollback — is asynchronous:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    // ... statements ...
}   // <- auto-commit here (auto-rollback if an exception escaped)
```

The database targeted by the transactionis fixed at construction. Pass a database name to run the entire block against a non-default database:

```csharp
await using (SxmTransaction ctx = new SxmTransaction("Archive"))
{
    // Everything inside runs against the "Archive" database.
}
```

### Entity DML inside a transaction

`SaveAsync()` and `DeleteAsync()` detect the ambient transactionautomatically. The call site is identical to the standalone case — no transaction argument, no enlistment method:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    Customer customer = new Customer { Name = "Grace Hopper" };
    await customer.SaveAsync();          // enlists in the transaction

    Order order = new Order
    {
        CustomerId = customer.id,
        Product    = "Compiler",
        Amount     = 42m
    };
    await order.SaveAsync();             // same transaction

}   // both rows commit together
```

> 💡 The same line of code that saves an entity standalone — `await customer.SaveAsync();` — participates in an `SxmTransaction` transaction automatically when written inside one. Nothing at the call site changes. The choice between standalone and transactional is made once, at the surrounding scope, not repeated at every call.


### SQL inside a transaction— `RunStatementAsync`

`SxmTransaction` exposes its own set of `RunStatementAsync` overloads that mirror the eight in `SxmSql`, with two differences:

- There is no `databaseName` parameter — Because the database is fixed by the transaction, the `RunStatementAsync` overloads in `SxmTransaction` do **not** take a `databaseName` argument.
- The statement runs inside the transaction.

Both embedded SQL and named SQL statements are fully supported. The call site is identical to the standalone case, except that the transaction is used instead of `SxmSql`:

**Typed result overloads:**

```csharp
// The SQL statement takes no parameters.
Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName);

// `userObjectParameters` object provides the SQL parameters, public properties are matched by name to the SQL parameters in the statement
Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlStatementName, T userObjectParameters);

// `sqlStatementParameters` Dictionary provides the SQL parameters, keyed by name to the SQL parameters in the statement.
Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters);

// `sqlStatementParameters` List provides the SQL parameters, matched by position to the statement's placeholders.
Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters);
```

**Untyped result overloads:**

```csharp
// The SQL statement takes no parameters.
Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName);

// `userObjectParameters` object provides the SQL parameters, public properties are matched by name to the SQL parameters in the statement.
Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlStatementName, T userObjectParameters);

// `sqlStatementParameters` Dictionary provides the SQL parameters, keyed by name to the SQL parameters in the statement.
Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters);

// `sqlStatementParameters` List provides the SQL parameters, matched by position to the statement's placeholders.
Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, List<object> sqlStatementParameters);
```

Named SQL example:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    Dictionary<string, object?> parms = new Dictionary<string, object?> { ["Status"] = "PENDING" };
    List<Order> pending = await ctx.RunStatementAsync<Order>("GetOrdersByStatus", parms);

    foreach (Order o in pending)
    {
        o.Product = o.Product + " (processed)";
        await o.SaveAsync();                 // same transaction as the read above
    }
}
```

### LINQ inside a transaction

Inside an `SxmTransaction`, `ctx.GetTable<T>()` returns a queryable table backed by the transaction's connection and transaction. Standard LINQ operators, along with the async and bulk-write extensions provided by SQLiteXM, all run inside the same transaction as everything else in the block:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    List<Customer> vips = ctx.GetTable<Customer>().Where(c => c.Email!.EndsWith("@vip.com")).ToList();
}
```

For the full LINQ reference, see [LINQ Support](SQLiteXM-LINQ-Support.md).

### Manual commit and rollback (optional)

Auto-commit on dispose is the recommended pattern. When you need to end a transaction earlier than that — for example, to release locks before a long computation — you can call the transaction's methods explicitly:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    await DoFirstBatchAsync(ctx);
    await ctx.CommitTransactionAsync();   // ends the first transaction

    await DoSecondBatchAsync(ctx);
}   // a new transaction started implicitly by the second batch is auto-committed here
```

`RollbackTransactionAsync()` discards the current transaction's work and also clears the transaction's faulted state, so the transaction can be reused for subsequent work.

---

## Mixing Everything in One Transaction

The defining feature of `SxmTransaction` is that a single block can **freely mix all three statement kinds** — entity DML, SQL (named or embedded), and LINQ — and every statement participates in the same transaction. The block either commits every change together or rolls back every change together.

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
    // 1) LINQ read
    Customer customer = ctx.GetTable<Customer>().First(c => c.Email == "grace@example.com");

    // 2) Named SQL read
    Dictionary<string, object?> parms = new Dictionary<string, object?> { ["CustomerId"] = customer.id };
    List<Order> theirOrders =
        await ctx.RunStatementAsync<Order>("GetOrdersForCustomer", parms);

    // 3) Entity DML writes
    foreach (Order order in theirOrders)
    {
        order.Amount *= 1.10m;               // 10% price adjustment
        await order.SaveAsync();
    }

    // 4) Embedded SQL write
    Dictionary<string, object?> auditParms = new Dictionary<string, object?>
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

Inside a transaction, an exception on any statement:

1. Marks the transactionas **faulted**.
2. Causes any *subsequent* write attempts in the same block to be silently skipped, preventing cascading errors from statements that depend on the failed one.
3. Causes the transaction to be **rolled back automatically** when the transactionis disposed.

The typical pattern is to wrap the entire `await using` block:

```csharp
try
{
    await using SxmTransaction ctx = new SxmTransaction();

    await DoWorkAsync(ctx);
    // No manual commit needed — dispose commits on success, rolls back on failure.
}
catch (Exception ex)
{
    logger.LogError(ex, "Transactional work failed and was rolled back.");
}
```

If you want to recover a faulted transactionand reuse it — instead of letting it dispose — call `RollbackTransactionAsync()`. That discards the failed transaction, clears the faulted flag, and lets subsequent statements start a fresh transaction on the same transaction.

### Exceptions to know about

| Exception | When it is thrown |
|---|---|
| `InvalidOperationException` | Calling `CommitTransactionAsync()` on a faulted `SxmTransaction` (call `RollbackTransactionAsync()` first); missing generated INSERT/UPDATE/DELETE statement for an entity type; database-name mismatch when a transactiontries to join an ambient transaction that is bound to a different database. |
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
- **Prefer `await using` for transactionblocks.** Synchronous `using` still works but will not asynchronously commit or roll back on disposal.
- **Do not commit manually unless you have a reason.** Auto-commit on dispose is the intended pattern. Manual `CommitTransactionAsync` / `RollbackTransactionAsync` are for advanced scenarios (batch boundaries, business-rule aborts, and similar).
- **Wrap the block, not each statement.** Inside a transaction, individual failures already short-circuit the remaining writes. A single `try` / `catch` around the whole `await using` block is usually enough.
- **Do not mix databases in one transaction.** An `SxmTransaction` is bound to one database. If you need to touch a second database, open a separate transaction for it.
- **Keep transactions short-lived.** A transaction holds a database connection and, once any write has run, an open transaction. Open it, do the work, dispose it.

---

## See Also

- [Entity DML](SQLiteXM-Entity-DML.md)
- [Embedded SQL Query Support](SQLiteXM-SQL-Support.md)
- [Named SQL Statements](SQLiteXM-Named-Statements.md)
- [LINQ Support](SQLiteXM-LINQ-Support.md)
- [Multi-Database Support](multi_database.md)
- [Defining Entities](defining_entities.md)
