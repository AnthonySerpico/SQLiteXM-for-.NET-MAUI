# Advanced Transaction Processing

> 📌 **Note:** This guide builds on the [Transactions](./working-with-data.md#transactions) and [Error Handling and Rollback](./working-with-data.md#error-handling-and-rollback) sections of [Working with Data](./working-with-data.md). Read those first. Most applications only need the simple transaction patterns described there. Read this guide when you need to know exactly when a transaction commits or rolls back, or when you nest transactions, commit part of your work early, or cancel database operations.

---

## Contents

- [How a Transaction Decides to Commit or Roll Back](#how-a-transaction-decides-to-commit-or-roll-back)
  - [Committing or rolling back yourself](#committing-or-rolling-back-yourself)
- [Quick Reference](#quick-reference)
- [What Marks a Transaction for Rollback](#what-marks-a-transaction-for-rollback)
- [Exceptions From Your Own Code](#exceptions-from-your-own-code)
- [Life of a Faulted Transaction](#life-of-a-faulted-transaction)
- [Cancellation](#cancellation)
- [Saving Part of the Work](#saving-part-of-the-work)
- [Nested Transactions](#nested-transactions)
- [Seeing Your Own Uncommitted Work](#seeing-your-own-uncommitted-work)
- [Edge Cases](#edge-cases)
- [Recommended Patterns](#recommended-patterns)
- [See Also](#see-also)

---

## How a Transaction Decides to Commit or Roll Back

You normally create a transaction with `await using`. When the block ends, the transaction is disposed, 
and that is when the transaction is committed or rolled back:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await customer.SaveAsync();
	await order.SaveAsync();
}   // <- the commit or rollback decision is made here
```

The decision depends on a single flag that you can read at any time through the transaction's `IsFaulted` property:

| `ctx.IsFaulted` | What dispose does |
|---|---|
| `false` when the block ends | Commits all the work |
| `true` when the block ends | Rolls back all the work |

At the start of a transaction, `IsFaulted` is `false`. SQLiteXM sets it to `true` when one of its own operations fails or is cancelled. Nothing else sets it.

When `IsFaulted` is `true`, the transaction is said to be **faulted**, which simply means it is **marked for rollback**. This guide uses both terms for the same thing.

You cannot set `IsFaulted` yourself. It is read-only, so a faulted transaction can never be forced to commit. 
And you cannot manually commit a faulted transaction by calling `CommitTransactionAsync()`; this throws an 
`InvalidOperationException`. The only way to set `IsFaulted` back to `false` is to call `RollbackTransactionAsync()` 
(see [Resetting a faulted transaction](#resetting-a-faulted-transaction)).

### Committing or rolling back yourself

The methods below can be used to manually commit or roll back a transaction. Transaction disposal automatically handles 
this for you, however, they are available when you need finer control.

| Method | What it does | Can the transaction be used afterwards? |
|---|---|---|
| `CommitTransactionAsync()` | Saves all work done so far. Throws if the transaction is faulted. | Yes. The next operation starts a new database transaction. |
| `RollbackTransactionAsync()` | Discards all work done so far and resets `IsFaulted` to `false`. | Yes. The next operation starts a new database transaction. |

Both are safe to call when there is nothing to commit or roll back. They simply do nothing. Calling either one several times in a row is also safe.

---

## Quick Reference

| What happens inside the transaction | Marked for rollback (`IsFaulted`)? | Result when the transaction is disposed |
|---|---|---|
| Every SQLiteXM operation succeeds | No | **Commit** |
| A SQLiteXM write fails (`SaveAsync`, `DeleteAsync`, insert, update, delete, bulk insert) | Yes | **Rollback** |
| A raw or named SQL statement fails (`RunStatementAsync`) | Yes | **Rollback** |
| A LINQ query fails | Yes | **Rollback** |
| A SQLiteXM operation is cancelled with a `CancellationToken` | Yes | **Rollback** |
| Any of the failures above, **and your code catches the exception** | Yes | **Rollback** |
| Your own code throws an exception | **No** | **Commit**, unless you call `RollbackTransactionAsync()` |
| You call `RollbackTransactionAsync()` | Failure cleared | Only work done *after* the rollback is committed |

---

## What Marks a Transaction for Rollback

Any SQLiteXM operation that fails inside a transaction marks the transaction for rollback (`IsFaulted` becomes `true`).

- **Writes:** `SaveAsync()`, `DeleteAsync()`, LINQ inserts, updates and deletes, and `BulkInsertAsync()`.
- **SQL statements:** `RunStatementAsync(...)`, for both embedded SQL and named statements.
- **LINQ queries:** for example `ToList()`, `ToListAsync()`, `CountAsync()` or `FirstOrDefaultAsync()`.
- **Cancellation:** any of the above stopped by a `CancellationToken`. See [Cancellation](#cancellation).

### Why a failed *read* rolls back the transaction

It may seem odd that a failed query, which changes nothing, can cause your saved data to be discarded. The reason is that a transaction protects the **work**, not just the individual writes.

Consider this example:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await payment.SaveAsync();

	// Read the balance so we can update it
	Account account = await ctx.GetTable<Account>()
		.FirstAsync(a => a.id == payment.AccountId);   // suppose this fails

	account.Balance -= payment.Amount;
	await account.SaveAsync();
}
```

If the query fails and the transaction committed anyway, the payment would be saved but the balance would never be updated. The database would accept that, but your data would be wrong. Because the query was part of the work, a failed query means the work did not finish, so nothing is saved.

### Catching the exception does not change the outcome

The exception from a failed SQLiteXM operation is always passed to your code. Catching it does **not** clear the failure. The transaction still rolls back:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	try
	{
			await customer.SaveAsync();
			await order.SaveAsync();   // throws
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Order could not be saved.");
		// The exception was handled, but ctx.IsFaulted is still true.
	}
}   // <- rollback: neither the customer nor the order is saved
```

If you really want to continue after a failure, see [Starting over inside the same transaction](#starting-over-inside-the-same-transaction).

---

## Exceptions From Your Own Code

**An exception thrown by your code does not mark a transaction for rollback.** SQLiteXM only marks a transaction for rollback as a result of failures in its own operations. 

In a transaction, an unhandled exception originating from your code has no effect on the `IsFaulted` flag.

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await customer.SaveAsync();

	if (order.Quantity <= 0)
		throw new InvalidOperationException("Quantity must be positive.");

	await order.SaveAsync();
} // ⚠️ IsFaulted is still false, the transaction commits even though the method throws
```

In this example the customer is committed and the order is never saved, which is probably not what you intended.

### How to roll back on your own exceptions

Catch the exception, roll back, then rethrow it if callers need to know:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	try
	{
		await customer.SaveAsync();

		if (order.Quantity <= 0)
			throw new InvalidOperationException("Quantity must be positive.");

		await order.SaveAsync();
	}
	catch
	{
		await ctx.RollbackTransactionAsync();
		throw;
	}
}
```

> 💡 **Why the difference?** It is not that SQLiteXM treats your exceptions as less important. A transaction simply has no way to observe them. SQLiteXM sees a failure only when it happens inside one of its own calls.

---

## Life of a Faulted Transaction

Once a transaction is marked for rollback, it stays that way until it is disposed or you call `RollbackTransactionAsync()`. 

While a transaction is faulted:

| You try to… | What happens |
|---|---|
| Write (`SaveAsync`, insert, update, delete, bulk insert) | The write is **skipped**. No exception is thrown. LINQ writes return `0`; `SaveAsync()` and `DeleteAsync()` simply do nothing. |
| Call `CommitTransactionAsync()` | Throws `InvalidOperationException`. A faulted transaction cannot be committed. |
| Call `RollbackTransactionAsync()` | Discards the work, resets `IsFaulted` to `false`, and the transaction can continue to be used again. |
| Let the block end | Rolls back automatically. |

Writes inside a faulted transaction are skipped because they are going to be rolled back anyway.

### Committing explicitly after a failure

If you commit explicitly, check `IsFaulted` first:

```csharp
if (ctx.IsFaulted)
{
	await ctx.RollbackTransactionAsync();
}
else
{
	await ctx.CommitTransactionAsync();
}
```

Or simply let disposal decide for you, which already follows this logic.

### Resetting a faulted transaction

Calling `RollbackTransactionAsync()` on a faulted transaction discards the failed work **and resets `IsFaulted` to `false`**. From that point on, the transaction behaves like a new transaction: writes run again, and it commits normally when disposed.

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await customer.SaveAsync();

	try
	{
		await order.SaveAsync();   // SQLiteXM operation fails
	}
	catch (Exception ex)
	{
		logger.LogError(ex, "Order could not be saved.");

		bool before = ctx.IsFaulted;   // true
		await ctx.RollbackTransactionAsync();   // customer and order are discarded
		bool after = ctx.IsFaulted;    // false
	}

	await invoice.SaveAsync();     // runs normally
}   // <- commit: only the invoice is saved
```

> ⚠️ If your exception handler did not call `RollbackTransactionAsync()`, the transaction would remain faulted, and the invoice write would be skipped and the entire transaction rolled back when the block ends.

### Starting over inside the same transaction

`RollbackTransactionAsync()` discards everything since the transaction started (or since the last commit or rollback), clears the failure, and lets you keep using the same `SxmTransaction`. Work you do afterwards is committed normally.

---

## Cancellation

Many SQLiteXM operations accept a `CancellationToken`. In a .NET MAUI app, a token is often cancelled when the user leaves a page, closes the app, or a timeout expires.

**A cancelled SQLiteXM operation marks the transaction for rollback, just like any other failure.** The operation throws an `OperationCanceledException` to your code, and the transaction rolls back when it is disposed:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await customer.SaveAsync();

	try
	{
		List<Order> orders = await ctx.GetTable<Order>()
			.Where(o => o.CustomerId == customer.id)
			.ToListAsync(cancellationToken);   // cancelled
	}
	catch (OperationCanceledException)
	{
		// ctx.IsFaulted is now true
	}
}   // <- rollback: the customer is not saved
```

A cancellation is an interruption. The work was not finished, so nothing is saved. This is the same for reads and writes.

> ⚠️ **There is no way to keep a transaction after a cancellation.** If you need the earlier work saved even when a later step might be cancelled, commit it first (see [Saving Part of the Work](#saving-part-of-the-work)).

---

## Saving Part of the Work

Because the transaction stays usable after `CommitTransactionAsync()`, you can save work in stages. Each commit saves everything so far. Later work starts a new database transaction, so a later rollback only discards what came after the most recent commit.

Each batch is saved as soon as it is complete, so a problem in one batch does not throw away the batches that were already imported or halt the import:

```csharp
List<List<Customer>> batches = LoadCustomerBatches();   // for example, 3 batches of 100 customers

await using (SxmTransaction ctx = new SxmTransaction())
{
	foreach (List<Customer> batch in batches)
	{
		try
		{
			foreach (Customer customer in batch)
			{
				ValidateCustomer(customer);   // your own code; may throw
				await customer.SaveAsync();
			}

			await ctx.CommitTransactionAsync();       // this batch is now permanently saved
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Batch could not be imported.");
			await ctx.RollbackTransactionAsync(); // discards
		}
	}
}   // <- nothing is left to commit here; every good batch was already saved
```

Suppose batch 2 contains an invalid customer:

| Step | What happens | Saved in the database |
|---|---|---|
| Batch 1 saved, then committed | Batch 1 is permanently saved | Batch 1 |
| Batch 2: some customers saved, then one fails | The exception is caught | Batch 1 (batch 2 is still uncommitted) |
| `RollbackTransactionAsync()` | The partial batch 2 is discarded and `IsFaulted` is reset | Batch 1 |
| Batch 3 saved, then committed | Batch 3 is permanently saved | Batches 1 and 3 |
| The block ends | Nothing is pending | Batches 1 and 3 |

The rollback in the `catch` block matters for two reasons:

- If batch 2 failed because of **your own code** (`ValidateCustomer` threw), the transaction is not marked for rollback. Without the rollback, the customers saved before the failure would be committed together with batch 3.
- If batch 2 failed because of a **SQLiteXM operation**, the transaction is marked for rollback. Without the rollback, every later write would be skipped and the next `CommitTransactionAsync()` would throw. The rollback resets `IsFaulted` so batch 3 can be saved.

> ⚠️ Committing in stages means the work is no longer all-or-nothing. Only use it when each stage makes sense on its own.

---

## Nested Transactions

### Calling transactional methods from inside a transaction

Nested transactions are useful when a method that uses a transaction calls another method 
that also uses a transaction. Each method can be written and used as a stand-alone,
independent unit of work. They can also work together safely as part of a larger transaction.

```csharp
public async Task PlaceOrderAsync(Order order, Invoice invoice)
{
	await using SxmTransaction ctx = new SxmTransaction();
	await order.SaveAsync();
	await invoice.SaveAsync();
}

public async Task CheckoutAsync(Customer customer, Order order, Invoice invoice, Payment payment)
{
	await using SxmTransaction ctx = new SxmTransaction();
	await customer.SaveAsync();
	await PlaceOrderAsync(order, invoice);   // joins the checkout transaction
	await payment.SaveAsync();
}   // <- commit: customer, order, invoice and payment are saved together
```

- Called on its own, `PlaceOrderAsync` starts a transaction and commits the order and invoice when its transaction block ends.
- When `PlaceOrderAsync` is called from `CheckoutAsync`, it **joins** the checkout transaction. Nothing is committed when the `PlaceOrderAsync` transaction block ends. Everything is committed or rolled back together when `CheckoutAsync` finishes.

The author of `PlaceOrderAsync` does not need to know who calls it.

The inner transaction, in this example `PlaceOrderAsync`, does not start a new transaction. It joins the one that is already open. Both objects share the same connection and the same database transaction. You can think of the inner `SxmTransaction` as a second handle to the same transaction. The outer transaction is the controlling transaction, so only the outer transaction's disposal commits or rolls back.

### How the inner transaction affects the shared work

Because there is really only one transaction, here is how each action on the inner transaction affects the shared work:

| On the inner transaction you… | Effect |
|---|---|
| Let the block end | **Nothing.** Only the outer transaction commits or rolls back. |
| Hit a failed SQLiteXM operation | The **shared** transaction is marked for rollback. The outer transaction rolls back when disposed. |
| Throw an exception from your own code | The transaction is not marked for rollback. If the outer block catches the exception, the outer transaction still commits **all** the work, including the inner work. |
| Call `RollbackTransactionAsync()` | Discards **all** work so far, **including work done by the outer transaction** before the inner one started. |
| Call `CommitTransactionAsync()` | Saves **all** work so far, **including the outer transaction's earlier work**, immediately. A later rollback by the outer transaction can no longer undo it. |

The last two rows are the ones to watch. By default, the outer transaction decides when the work is committed or rolled back. But an explicit `CommitTransactionAsync()` or `RollbackTransactionAsync()` always does what it says, whether or not the transaction is nested. The catch is that it acts on the **whole** shared transaction, not "just the inner part":

```csharp
public async Task PlaceOrderAsync(Order order, Invoice invoice)
{
	await using SxmTransaction ctx = new SxmTransaction();
	try
	{
		await order.SaveAsync();
		await invoice.SaveAsync();
	}
	catch
	{
		await ctx.RollbackTransactionAsync();   // ⚠️ also discards the caller's customer
		throw;
	}
}

public async Task CheckoutAsync(Customer customer, Order order, Invoice invoice, Payment payment)
{
	await using SxmTransaction ctx = new SxmTransaction();
	await customer.SaveAsync();
	await PlaceOrderAsync(order, invoice);   // joins the checkout transaction
	await payment.SaveAsync();
}
```

If saving the order fails, the rollback inside `PlaceOrderAsync` also discards the customer saved by `CheckoutAsync`.

### Nested transactions must use the same database

Because an inner transaction joins the outer one, both must target the same database. Creating an inner transaction for a **different** database from the outer transaction throws an exception:

```csharp
public async Task PlaceOrderAsync(Order order, Invoice invoice)
{
	await using SxmTransaction ctx = new SxmTransaction("Orders");
	await order.SaveAsync();
	await invoice.SaveAsync();
}

public async Task CheckoutAsync(Customer customer, Order order, Invoice invoice, Payment payment)
{
	await using SxmTransaction ctx = new SxmTransaction();
	await customer.SaveAsync();

	try
	{
	    // ❌ Throws: a transaction is already open for the default database
	    await PlaceOrderAsync(order, invoice);  
	}
	catch
	{
		await ctx.RollbackTransactionAsync(); // discards the customer
		throw;
	}	

	await payment.SaveAsync(); // skipped because of PlaceOrderAsync threw

    // Nothing to commit or rollback; the transaction was
}   // already rolled back and no further work was done
```

To work with two databases, you need to use two separate, non-nested transactions. See [Multiple Databases](./multiple-databases.md).

### Nesting Transaction Blocks

> 📌 **Note:** Nesting a transaction block directly inside another transaction block in the same method provides no extra functionality and is not recommended.

```csharp
await using (SxmTransaction main = new SxmTransaction())
{
    // ❌ No additional functionality: the inner transaction joins the outer one
    await using (SxmTransaction inner = new SxmTransaction())
    {
    }
}
```

---

## Seeing Your Own Uncommitted Work

Inside a transaction, queries see the work you have done so far, even before it is committed:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	Customer customer = new Customer { Name = "Ada" };
	await customer.SaveAsync();

	// Finds "Ada", even though nothing has been committed yet
	int count = await ctx.GetTable<Customer>().CountAsync(c => c.Name == "Ada");

	await ctx.RollbackTransactionAsync();

	// "Ada" is gone
	count = await ctx.GetTable<Customer>().CountAsync(c => c.Name == "Ada");   // 0
}
```

Other transactions never see your uncommitted work; they only see data once it has been committed. (A nested `SxmTransaction` is not "another transaction". It shares the connection, so it sees everything the outer transaction has done.)

---

## Edge Cases

### Using a transaction after it is disposed

Once a transaction has been disposed, it cannot be used again:

| You call… | What happens |
|---|---|
| `CommitTransactionAsync()` or `RollbackTransactionAsync()` | Throws `ObjectDisposedException` |
| A LINQ query through the disposed transaction | Throws |
| `entity.SaveAsync()` | Runs **on its own**, outside any transaction, and is saved immediately |

The last row is worth remembering. `SaveAsync()` does not take a transaction argument; it joins whatever transaction is currently open. After the transaction is disposed, there is none, so the save happens on its own:

```csharp
SxmTransaction ctx = new SxmTransaction();
await ctx.DisposeAsync();

await customer.SaveAsync();   // saved immediately, not part of any transaction
```

### Commit and rollback with nothing to do

Calling `CommitTransactionAsync()` or `RollbackTransactionAsync()` when nothing has changed since the last commit or rollback does nothing. Calling them repeatedly is safe.

### Do not move a transaction between threads

Create and use a transaction in the same flow of code. Creating it inside `Task.Run` and using it outside can break the way inner operations find the open transaction. See [Transaction performance and threading](./working-with-data.md#transaction-performance-and-threading).

---

## Recommended Patterns

### 1. Let disposal decide

For most work, write the operations and let the block end. Disposal commits on success and rolls back on any SQLiteXM failure:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	await customer.SaveAsync();
	await order.SaveAsync();
}
```

### 2. Validate before you write

Check your own rules before the first write, so an exception from your code never leaves half the work committed.

### 3. Roll back on your own exceptions

If your code can throw in the middle of a transaction, catch the exception, roll back, then rethrow:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	try
	{
		await DoWorkAsync();
	}
	catch
	{
		await ctx.RollbackTransactionAsync();
		throw;
	}
}
```

### 4. Check `IsFaulted` before an explicit commit

```csharp
if (ctx.IsFaulted)
	await ctx.RollbackTransactionAsync();
else
	await ctx.CommitTransactionAsync();
```

### 5. Let the outermost transaction decide

In code that might run inside another transaction, do not call `CommitTransactionAsync()` or `RollbackTransactionAsync()`. Throw instead, and let the caller decide.

### 6. Commit in stages only when each stage stands on its own

Partial commits give up all-or-nothing behavior. Use them for independent batches, not for steps that depend on each other.

---

## See Also

- [Working with Data](./working-with-data.md) — transactions, entity DML and SQL basics
- [LINQ Queries](./linq-queries.md) — querying inside a transaction
- [Error Handling](./error-handling.md) — exception types and recovery
- [Concurrency](./concurrency.md) — running transactions at the same time
- [Multiple Databases](./multiple-databases.md) — working with more than one database
- [API Reference](./api-reference.md) — `SxmTransaction` members
