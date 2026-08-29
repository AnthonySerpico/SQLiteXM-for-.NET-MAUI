# Concurrency in SQLiteXM

SQLiteXM fully supports concurrent database operations. You can run multiple LINQ statements, entity operations, and SQL statements at the same time — safely and efficiently. This guide shows you how to leverage concurrency in SQLiteXM and explains the patterns that work best.

---

## Concurrent Database Operations — How to Do It

SQLiteXM enables concurrency by allowing you to create **multiple transactions that run in parallel**. Each transaction gets its own connection to the database, and SQLite coordinates access between them. Here's how to use it.

---

## Safe Concurrent Patterns

### ✅ Separate transactions for concurrent LINQ queries

```csharp
// Each task gets its own transaction and connection
await Task.WhenAll(
	Task.Run(async () =>
	{
		await using var ctx1 = new SxmTransaction();
		return await ctx1.GetTable<Customer>().Where(c => c.Age > 30).ToListAsync();
	}),
	Task.Run(async () =>
	{
		await using var ctx2 = new SxmTransaction();
		return await ctx2.GetTable<Order>().Where(o => o.Total > 100).ToListAsync();
	})
);
```

**Why this works:** Each `SxmTransaction` has its own private `SqliteConnection`. SQLite handles coordination between the two connections at the database level.

---

### ✅ Concurrent entity operations (standalone mode)

When operations are **not** inside a transaction block, each operation creates its own connection:

```csharp
// Each SaveAsync creates its own connection internally
await Task.WhenAll(
	customer.SaveAsync(),
	order.SaveAsync()
);
```

**Why this works:** Outside of a transaction block, each `SaveAsync()` runs in standalone mode and opens its own connection.

---

### ✅ Concurrent SQL statements (standalone mode)

```csharp
// Each static call creates its own connection
await Task.WhenAll(
	SxmSql.RunStatementAsync<Customer>("GetActiveCustomers"),
	SxmSql.RunStatementAsync<Order>("GetRecentOrders")	
);
```

**Why this works:** The static `SxmSql.RunStatementAsync()` method runs in standalone mode and each call gets its own connection.

---

### ✅ Concurrent transactions to different databases

SQLiteXM supports multiple databases (see [Multi-Database Support](./multi-database.md)). You can safely run concurrent transactions to different databases:

```csharp
// Separate transactions to different databases
await Task.WhenAll(
	Task.Run(async () =>
	{
		await using var ctx1 = new SxmTransaction("MainDatabase");
		await ctx1.GetTable<Customer>().Where(c => c.Active).ToListAsync();
	}),
	Task.Run(async () =>
	{
		await using var ctx2 = new SxmTransaction("ArchiveDatabase");
		await ctx2.GetTable<Order>().Where(o => o.Year < 2020).ToListAsync();
	})
);
```

**Why this works:** Each transaction has its own connection, and the connections target different database files. There is no shared state.

---

### ✅ Concurrent transactions to the same database

You can also run concurrent transactions to the **same database** using separate `SxmTransaction` instances:

```csharp
// Separate transactions to the same database
await Task.WhenAll(
	Task.Run(async () =>
	{
		await using var ctx1 = new SxmTransaction();
		await ctx1.GetTable<Customer>().ToListAsync();
	}),
	Task.Run(async () =>
	{
		await using var ctx2 = new SxmTransaction();
		await ctx2.GetTable<Order>().ToListAsync();
	})
);
```

**Why this works:** Each `SxmTransaction` opens its own `SqliteConnection` to the same database file. SQLite coordinates access between the connections.

---

### ✅ Sequential operations on the same transaction

Of course, you can always use a single transaction for sequential operations:

```csharp
await using (var ctx = new SxmTransaction())
{
	// Operations execute one at a time on the same connection
	var customers = await ctx.GetTable<Customer>().Where(c => c.Age > 30).ToListAsync();
	var orders = await ctx.GetTable<Order>().Where(o => o.Total > 100).ToListAsync();
}
```

**Why this works:** Only one operation executes at a time on `ctx`'s connection. No concurrency, no problem.

---

## How Concurrency Works in SQLiteXM

Understanding the architecture helps explain why the patterns above work:

1. **Each `SxmTransaction` opens one connection:**  
   When you create `new SxmTransaction()`, SQLiteXM opens a private `SqliteConnection` to the database. That connection lives for the entire duration of the transaction block.

2. **All operations in a transaction share the same connection:**  
   LINQ queries (`ctx.GetTable<T>()`), entity operations (`entity.SaveAsync()`), and SQL statements (`ctx.RunStatementAsync(...)`) all execute on the same underlying connection inside the transaction.

3. **Standalone operations create their own connections:**  
   When you call `entity.SaveAsync()` or `SxmSql.RunStatementAsync(...)` outside of a transaction block, each operation opens and closes its own connection (standalone mode).

4. **SQLite handles multi-connection concurrency:**  
   SQLite **does** support multiple connections to the same database running concurrently. The SQLite engine coordinates access between separate connections.

5. **Separate transactions = separate connections = safe concurrency:**  
   To execute database operations concurrently, create **separate `SxmTransaction` instances** (one per concurrent task) or use standalone mode. Each transaction/standalone operation gets its own connection, and SQLite handles coordination at the database level.

---

## Important Constraint: Same Transaction, No Concurrency

While SQLiteXM fully supports concurrent operations through separate transactions, there is one important constraint:

**An `SxmTransaction` instance is not thread-safe and must not be used concurrently from multiple threads or tasks.**

This constraint comes from the underlying ADO.NET provider. According to [Microsoft's documentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors):

> **⚠️ WARNING**  
> Although SQLite supports concurrent access to the same database from multiple threads, the .NET API objects are not thread-safe. This means that `SqliteConnection`, `SqliteCommand`, and `SqliteDataReader` **cannot be shared and used concurrently from multiple threads**.

Each `SxmTransaction` wraps a single `SqliteConnection`. Attempting to execute multiple operations concurrently on the same `SxmTransaction` violates this thread-safety constraint and can lead to unpredictable behavior.

**The solution is simple:** Use separate `SxmTransaction` instances for concurrent operations, as shown in the examples above.

---

## Unsafe Patterns (What to Avoid)

Now that you've seen the safe patterns, here are the anti-patterns to avoid:

### ❌ Concurrent LINQ queries on the same transaction

```csharp
await using (var ctx = new SxmTransaction())
{
	// UNSAFE: Both queries use ctx's connection concurrently
	await Task.WhenAll(
		ctx.GetTable<Customer>().Where(c => c.Age > 30).ToListAsync(),
		ctx.GetTable<Order>().Where(o => o.Total > 100).ToListAsync()
	);
}
```

**Why this is unsafe:** Both `ToListAsync()` calls execute concurrently on the same `SqliteConnection`.

---

### ❌ Concurrent entity operations on the same transaction

```csharp
await using (var ctx = new SxmTransaction())
{
	// UNSAFE: Both SaveAsync calls use ctx's connection concurrently
	await Task.WhenAll(
		customer.SaveAsync(),
		order.SaveAsync()
	);
}
```

**Why this is unsafe:** When an ambient transaction is present, `SaveAsync()` enlists in it. Both operations share `ctx`'s connection.

---

### ❌ Concurrent SQL statements on the same transaction

```csharp
await using (var ctx = new SxmTransaction())
{
	// UNSAFE: Both RunStatementAsync calls use ctx's connection concurrently
	await Task.WhenAll(
		ctx.RunStatementAsync<Customer>("GetActiveCustomers"),
		ctx.RunStatementAsync<Order>("GetRecentOrders")
	);
}
```

**Why this is unsafe:** Both statements execute on `ctx`'s connection concurrently.

---

### ❌ Mixed operations on the same transaction

```csharp
await using (var ctx = new SxmTransaction())
{
	// UNSAFE: All three operations share ctx's connection
	await Task.WhenAll(
		ctx.GetTable<Customer>().ToListAsync(),
		customer.SaveAsync(),
		ctx.RunStatementAsync<Order>("GetRecentOrders")
	);
}
```

**Why this is unsafe:** LINQ, entity DML, and SQL all execute on the same connection concurrently.

---

**The simple fix:** When you need concurrent operations, use the patterns shown at the top of this guide — create separate `SxmTransaction` instances (one per concurrent task) or use standalone mode (operations outside of any transaction block).

---

## Quick Reference

| Pattern | Safe? | Reason |
|---------|-------|--------|
| Sequential operations on same `SxmTransaction` | ✅ Yes | Only one operation at a time on the connection |
| Concurrent operations on same `SxmTransaction` | ❌ No | Multiple threads/tasks using the same connection simultaneously |
| Concurrent operations with separate `SxmTransaction` instances | ✅ Yes | Each transaction has its own connection |
| Concurrent operations in standalone mode (no transaction block) | ✅ Yes | Each operation creates its own connection |
| Concurrent transactions to different databases | ✅ Yes | Separate connections to separate database files |
| Concurrent transactions to the same database | ✅ Yes | SQLite handles coordination between connections |

---

## Key Takeaways

1. **SQLiteXM supports concurrency** — use separate `SxmTransaction` instances or standalone mode for concurrent operations.
2. **Each transaction gets its own connection** — SQLite coordinates access between them.
3. **Never use the same `SxmTransaction` concurrently** — it wraps a non-thread-safe `SqliteConnection`.
4. **Operations inside a transaction block automatically use that transaction** — they share the same connection and must execute sequentially.

For more on transactions and execution modes, see [Working with Data](./working-with-data.md).
