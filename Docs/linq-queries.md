# LINQ Queries in SQLiteXM

SQLiteXM provides comprehensive LINQ support for querying and manipulating data. LINQ queries execute directly against the database, translating your C# expressions into efficient SQL. This guide explains how to use LINQ with SQLiteXM, from simple queries to complex operations.

---

## Contents

**Getting oriented**
- [Understanding LINQ in SQLiteXM](#understanding-linq-in-sqlitexm)
- [Prerequisites](#prerequisites)
- [Creating a Transaction for LINQ](#creating-a-transaction-for-linq)

**Reading data**
- [Basic LINQ Queries](#basic-linq-queries)
- [Async Materialization Methods](#async-materialization-methods)
- [Advanced LINQ Operations](#advanced-linq-operations)

**Modifying data**
- [Bulk Insert Operations](#bulk-insert-operations)
- [Bulk Update Operations](#bulk-update-operations)
- [Bulk Delete Operations](#bulk-delete-operations)

**Putting it together**
- [Combining LINQ with Entity DML and SQL](#combining-linq-with-entity-dml-and-sql)
- [Query Execution and Performance](#query-execution-and-performance)
- [Working with Multiple SQLite Databases](#working-with-multiple-sqlite-databases)
- [Error Handling](#error-handling)
- [Complete Examples](#complete-examples)

**Reference**
- [Best Practices](#best-practices)
- [LINQ Method Reference](#linq-method-reference)
- [Related Documentation](#related-documentation)
- [Summary](#summary)

---

## Understanding LINQ in SQLiteXM

LINQ (Language-Integrated Query) allows you to write strongly-typed database queries using C# syntax. In SQLiteXM, LINQ queries:

- Execute directly against SQLite (not in-memory)
- Are translated to efficient SQL statements
- Return typed entity instances
- Support filtering, ordering, joining, grouping, and aggregation
- Must run inside an `SxmTransaction` block
- Participate in the same transaction as entity DML and SQL statements

> 💡 **LINQ Execution Mode:** Unlike entity DML (`entity.SaveAsync()`) and SQL statements (`SxmSql.RunStatementAsync`), LINQ does **not** have a standalone execution mode. LINQ queries must always run inside an `SxmTransaction` block.

---

## Prerequisites

LINQ examples in this guide assume:

- The database has been initialized. For details, see: ➡️ [Getting Started](./getting-started.md).
- You are executing the LINQ examples inside an `SxmTransaction` block. For details, see: ➡️ [Reading and Writing Data](./working-with-data.md).
- The following two entities are registered:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	public string? Name  { get; set; }
	public string? Email { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	public long     CustomerId { get; set; }
	public string?  Product    { get; set; }
	public decimal  Amount     { get; set; }
	public string?  Status     { get; set; }
	public DateTime OrderDate  { get; set; }
}
```

Both inherit from `SxmEntity`, which supplies the auto-managed `id` primary-key column.

> 💡 The string properties above are declared as nullable (`string?`). Some examples in this guide call a method on one, such as `c.Name!.StartsWith("A")`. The `!` is the null-forgiving operator: it tells the C# compiler you know the value is not null, suppressing a nullable warning. It has no effect on the generated SQL. If your own entities declare non-nullable strings, you can drop the `!`.

---

## Creating a Transaction for LINQ

Create a transaction using the `SxmTransaction` class. LINQ queries obtain their table reference from the transaction context:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	// Get a queryable table reference from the transaction
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Now you can query the table using LINQ
	List<Customer> results = await customers.Where(c => c.Name == "Ada Lovelace").ToListAsync();
}
```

The `ctx.GetTable<T>()` method returns an `SxmTable<T>` instance that represents the table and implements `IQueryable<T>`. This allows you to use standard LINQ query operators.

> 💡 The transaction automatically commits when disposed if all operations succeed, or rolls back if an exception occurs.

---

## Basic LINQ Queries

### Retrieving All Rows

The simplest query retrieves all rows from a table:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Asynchronously materialize all rows to a list
	List<Customer> allCustomers = await customers.ToListAsync();
}
```

### Filtering with Where

Use `Where` to filter rows based on a condition:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Find customers with a specific email
	List<Customer> matches = await customers
		.Where(c => c.Email == "ada@example.com")
		.ToListAsync();
}
```

Multiple conditions:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	DateTime cutoffDate = DateTime.UtcNow.AddDays(-30);

	// Find recent orders for a specific customer
	List<Order> recentOrders = await orders
		.Where(o => o.CustomerId == 42 && o.OrderDate >= cutoffDate)
		.ToListAsync();
}
```

### Ordering Results

Use `OrderBy` and `OrderByDescending` to sort results:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Sort customers by name
	List<Customer> sorted = await customers
		.OrderBy(c => c.Name)
		.ToListAsync();
}
```

Multiple sort criteria with `ThenBy`:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Sort by customer, then by order date descending
	List<Order> sorted = await orders
		.OrderBy(o => o.CustomerId)
		.ThenByDescending(o => o.OrderDate)
		.ToListAsync();
}
```

### Retrieving a Single Result

Retrieve just the first row or a single row:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Get the first customer (throws if none exist)
	Customer first = await customers
		.OrderBy(c => c.Name)
		.FirstAsync();

	// Get the first customer or null if none exist
	Customer? maybeFirst = await customers
		.OrderBy(c => c.Name)
		.FirstOrDefaultAsync();

	// Get a single customer (throws if zero or multiple exist)
	Customer single = await customers
		.Where(c => c.Email == "ada@example.com")
		.SingleAsync();

	// Get a single customer or null (throws if multiple exist)
	Customer? maybeSingle = await customers
		.Where(c => c.Email == "ada@example.com")
		.SingleOrDefaultAsync();
}
```

### Projection with Select

Use `Select` to project results into a different shape:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Project to anonymous type
	var names = await customers
		.Select(c => new { c.Name, c.Email })
		.ToListAsync();

	// Project to single property
	List<string> emailList = await customers
		.Select(c => c.Email)
		.ToListAsync();
}
```

### Taking and Skipping Rows

Implement paging with `Take` and `Skip`:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	int pageSize = 20;
	int pageNumber = 2; // zero-based

	// Get page 2 (rows 21-40)
	List<Customer> page = await customers
		.OrderBy(c => c.Name)
		.Skip(pageNumber * pageSize)
		.Take(pageSize)
		.ToListAsync();
}
```

---

## Async Materialization Methods

SQLiteXM provides async extension methods for materializing query results. All of these methods execute the query against the database and return the results asynchronously.

### Available Methods

| Method | Return Type | Description |
|--------|-------------|-------------|
| `ToListAsync()` | `Task<List<T>>` | Materializes all matching rows to a list |
| `ToArrayAsync()` | `Task<T[]>` | Materializes all matching rows to an array |
| `FirstAsync()` | `Task<T>` | Returns the first row (throws if none exist) |
| `FirstOrDefaultAsync()` | `Task<T?>` | Returns the first row or null |
| `SingleAsync()` | `Task<T>` | Returns the only row (throws if zero or multiple) |
| `SingleOrDefaultAsync()` | `Task<T?>` | Returns the only row or null (throws if multiple) |
| `CountAsync()` | `Task<int>` | Returns the count of matching rows |
| `LongCountAsync()` | `Task<long>` | Returns the count as a long |
| `AnyAsync()` | `Task<bool>` | Returns true if any rows match |
| `AllAsync()` | `Task<bool>` | Returns true if all rows match the predicate |
| `MinAsync()` | `Task<TResult?>` | Returns the minimum value. ⚠️ Not reliable on a `decimal` column |
| `MaxAsync()` | `Task<TResult?>` | Returns the maximum value. ⚠️ Not reliable on a `decimal` column |
| `SumAsync()` | `Task<TResult>` | Returns the sum of values |
| `AverageAsync()` | `Task<TResult>` | Returns the average of values |
| `ContainsAsync()` | `Task<bool>` | Returns true if the given item exists |
| `ToDictionaryAsync()` | `Task<Dictionary<TKey, T>>` | Materializes to a dictionary. ⚠️ Throws if the key column holds duplicate values |

### Examples

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Count orders
	int orderCount = await orders.CountAsync();

	// Check if this customer has any orders at all
	bool hasOrders = await orders.AnyAsync(o => o.CustomerId == 42);

	// Get total order amount (SUM is numeric, so this is safe on a decimal column)
	decimal totalRevenue = await orders.SumAsync(o => o.Amount);

	// Get average order value (also safe)
	decimal avgOrder = await orders.AverageAsync(o => o.Amount);

	// MinAsync/MaxAsync on a decimal column would compare text and return the
	// wrong row, so find the extremes in memory instead.
	List<decimal> amounts = await orders.Select(o => o.Amount).ToListAsync();
	decimal minOrder = amounts.Min();
	decimal maxOrder = amounts.Max();

	// Check that every order is assigned to a customer
	bool allAssigned = await orders.AllAsync(o => o.CustomerId != 0);

	// Build a lookup keyed by the order id
	Dictionary<long, Order> ordersById = await orders.ToDictionaryAsync(o => o.id);
}
```

> 💡 `ToDictionaryAsync` throws if the column you key on holds duplicate values, so key on something unique such as `id`.

---

## Advanced LINQ Operations

### Grouping and Aggregation

Group rows and perform aggregations:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Group orders by customer and count them
	var orderCountByCustomer = await orders
		.GroupBy(o => o.CustomerId)
		.Select(g => new
		{
			CustomerId = g.Key,
			OrderCount = g.Count(),
			TotalAmount = g.Sum(o => o.Amount)
		})
		.ToListAsync();
}
```

### Joins

Join multiple tables to combine data:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Inner join: customers with their orders
	var customersWithOrders = await (
		from c in customers
		join o in orders on c.id equals o.CustomerId
		select new
		{
			CustomerName = c.Name,
			OrderProduct = o.Product,
			OrderAmount = o.Amount
		}
	).ToListAsync();
}
```

Method syntax for joins:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	var customersWithOrders = await customers
		.Join(
			orders,
			c => c.id,
			o => o.CustomerId,
			(c, o) => new
			{
				CustomerName = c.Name,
				OrderProduct = o.Product,
				OrderAmount = o.Amount
			}
		)
		.ToListAsync();
}
```

Left outer join:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Customers with their orders (including customers with no orders)
	var customersWithOrders = await (
		from c in customers
		join o in orders on c.id equals o.CustomerId into customerOrders
		from co in customerOrders.DefaultIfEmpty()
		select new
		{
			CustomerName = c.Name,
			OrderProduct = co != null ? co.Product : null,
			OrderAmount = co != null ? co.Amount : 0m
		}
	).ToListAsync();
}
```

> ✏️ **Note:** When you simply want a related entity attached to each row, `LoadWith` is usually
> easier than writing a join. See [Loading Related Data](./loading-related-data.md).

### Distinct Results

Remove duplicates from results:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Get distinct customer IDs who have placed orders
	List<long> customerIds = await orders
		.Select(o => o.CustomerId)
		.Distinct()
		.ToListAsync();
}
```

### Conditional Logic

Use conditional expressions in queries:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Categorize orders by amount.
	// The comparison is a decimal range test, so it must be done in memory.
	List<Order> allOrders = await orders.ToListAsync();

	var categorized = allOrders
		.Select(o => new
		{
			o.Product,
			o.Amount,
			Category = o.Amount < 50m ? "Small" :
					   o.Amount < 200m ? "Medium" : "Large"
		})
		.ToList();
}
```

---

## Bulk Insert Operations

SQLiteXM supports bulk inserts using LINQ. Instead of calling `SaveAsync()` once per entity, `BulkInsertAsync` writes many new entities using multi-row INSERT statements, which is far more efficient for large batches.

### Basic Bulk Insert

Build a list of new entities and insert them in one call:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Build the entities to insert (id must not be set)
	List<Customer> newCustomers = new List<Customer>();
	for (int i = 0; i < 1000; i++)
	{
		newCustomers.Add(new Customer
		{
			Name = $"Customer {i}",
			Email = $"customer{i}@example.com"
		});
	}

	// Insert all entities inside the transaction
	int rowsInserted = await customers.BulkInsertAsync(newCustomers);

	Console.WriteLine($"Inserted {rowsInserted} customers");
}
```

After the call every entity has its `id` populated from the database and its `synchId` assigned, exactly as if each had been saved with `SaveAsync()`:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	List<Order> newOrders = new List<Order>
	{
		new Order { CustomerId = 42, Product = "Widget", Amount = 19.99m },
		new Order { CustomerId = 42, Product = "Gadget", Amount = 49.50m },
		new Order { CustomerId = 42, Product = "Gizmo",  Amount = 7.25m }
	};

	await orders.BulkInsertAsync(newOrders);

	// Ids are available immediately
	foreach (Order order in newOrders)
		Console.WriteLine($"Order {order.id}: {order.Product}");
}
```

### Controlling Batch Size

Rows are grouped into multi-row statements. The default of 20 rows per statement is a good balance for most workloads; you can override it with the `batchRows` parameter:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Use 50 rows per INSERT statement
	int rowsInserted = await customers.BulkInsertAsync(newCustomers, batchRows: 50);
}
```

SQLiteXM automatically caps the batch so that no single statement binds more than 1000 parameters, regardless of the value you pass.

### Bulk Insert Alongside Other Operations

Because `BulkInsertAsync` runs inside the transaction, it participates in the same commit or rollback as LINQ queries, entity DML, and SQL statements:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Entity DML
	Customer customer = new Customer { Name = "Ada Lovelace", Email = "ada@example.com" };
	await customer.SaveAsync();

	// Bulk insert using the id assigned above
	List<Order> initialOrders = new List<Order>
	{
		new Order { CustomerId = customer.id, Product = "Starter Kit", Amount = 99m },
		new Order { CustomerId = customer.id, Product = "Manual",      Amount = 15m }
	};
	await orders.BulkInsertAsync(initialOrders);

	// LINQ query sees the inserted rows within the same transaction
	int orderCount = await orders
		.Where(o => o.CustomerId == customer.id)
		.CountAsync();

	// If anything above throws, the customer and all orders are rolled back together
}
```

> ⚠️ **New entities only:** `BulkInsertAsync` accepts only entities whose `id` has not been set. If any entity already has an `id`, an `InvalidOperationException` is thrown before any row is written. Use `SaveAsync()` to update existing rows.

> ⚠️ **No self-inserting triggers:** `BulkInsertAsync` is not supported on tables with INSERT triggers that add rows back into the same table. Use `SaveAsync()` for those entities instead.

> 💡 **Standalone alternative:** When you do not need to combine bulk insert with other operations, `SxmSql.BulkInsertAsync` provides the same functionality outside an `SxmTransaction` block, managing its own transaction.

---

## Bulk Update Operations

SQLiteXM supports bulk updates using LINQ. These operations update multiple rows in a single database statement, which is much more efficient than loading entities, modifying them, and saving them one by one.

### Basic Bulk Update

Update a specific property on matching rows:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Apply a 10% discount to a customer's orders.
	// The filter uses CustomerId (an integer), so it is evaluated numerically.
	// The Set expression is arithmetic, which is also safe on a decimal column.
	int rowsUpdated = await orders
		.Where(o => o.CustomerId == 42)
		.Set(o => o.Amount, o => o.Amount * 0.9m)
		.UpdateAsync();

	Console.WriteLine($"Applied discount to {rowsUpdated} orders");
}
```

> ⚠️ Do **not** filter a bulk update by a decimal range, such as `.Where(o => o.Amount > 500m)`. That comparison is evaluated as text and will update the wrong rows. If you need a decimal threshold, load the matching ids in memory first and filter the update by those ids:
>
> ```csharp
> List<Order> loaded = await orders.ToListAsync();
> List<long> targetIds = loaded
> 	.Where(o => o.Amount > 500m)
> 	.Select(o => o.id)
> 	.ToList();
>
> int rowsUpdated = await orders
> 	.Where(o => targetIds.Contains(o.id))
> 	.Set(o => o.Amount, o => o.Amount * 0.9m)
> 	.UpdateAsync();
> ```

### Updating Multiple Properties

Chain multiple `Set` calls to update several properties:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Update multiple properties
	int rowsUpdated = await customers
		.Where(c => c.Email!.Contains("@oldcompany.com"))
		.Set(c => c.Email, c => c.Email!.Replace("@oldcompany.com", "@newcompany.com"))
		.Set(c => c.Name, c => c.Name + " (Migrated)")
		.UpdateAsync();
}
```

### Updating with Constant Values

Set properties to constant values:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Mark all orders from customer 42 as processed
	int rowsUpdated = await orders
		.Where(o => o.CustomerId == 42)
		.Set(o => o.Product, "PROCESSED")
		.UpdateAsync();
}
```

### Incremental Updates

Increment or modify values based on their current state:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Increase all order amounts by $5
	int rowsUpdated = await orders
		.Set(o => o.Amount, o => o.Amount + 5m)
		.UpdateAsync();
}
```

> 💡 **Performance:** Bulk updates execute as a single SQL UPDATE statement, making them far more efficient than iterating through entities. For large datasets, always prefer bulk updates over entity-by-entity modification.

---

## Bulk Delete Operations

Delete multiple rows that match a condition:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Delete all orders with zero amount
	int rowsDeleted = await orders
		.Where(o => o.Amount == 0m)
		.DeleteAsync();

	Console.WriteLine($"Deleted {rowsDeleted} zero-amount orders");
}
```

Delete with complex conditions:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Delete a specific customer's cancelled orders
	int rowsDeleted = await orders
		.Where(o => o.CustomerId == 42 && o.Status == "Cancelled")
		.DeleteAsync();
}
```

---

## Combining LINQ with Entity DML and SQL

One of SQLiteXM's strengths is that LINQ, entity DML, and SQL statements all participate in the same transaction. You can freely mix all three approaches in a single transaction block:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	// 1. LINQ query: Find customers with high-value orders
	SxmTable<Order> orders = ctx.GetTable<Order>();
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// Amount is a decimal column, so the threshold must be applied in memory.
	List<Order> allOrders = await orders.ToListAsync();

	List<long> highValueCustomerIds = allOrders
		.Where(o => o.Amount > 1000m)
		.Select(o => o.CustomerId)
		.Distinct()
		.ToList();

	// 2. Entity DML: Create a reward for each customer
	foreach (long customerId in highValueCustomerIds)
	{
		Customer customer = await customers
			.FirstOrDefaultAsync(c => c.id == customerId);

		if (customer != null)
		{
			// Modify and save the entity
			customer.Email = $"vip_{customer.Email}";
			await customer.SaveAsync();
		}
	}

	// 3. SQL: Log the promotion activity
	string sql = "INSERT INTO ActivityLog (EventType, EventDate, Details) " +
				 "VALUES (@Type, @Date, @Details)";

	Dictionary<string, object?> parameters = new Dictionary<string, object?>
	{
		["Type"] = "VIP_PROMOTION",
		["Date"] = DateTime.UtcNow,
		["Details"] = $"Promoted {highValueCustomerIds.Count} customers"
	};

	await ctx.RunStatementAsync(sql, parameters);

	// All three operations commit together
}
```

This unified transactional model ensures data consistency: either all operations succeed together, or all are rolled back together.

---

## Query Execution and Performance

### Understanding Query Execution

LINQ queries in SQLiteXM are **deferred** until you call a materialization method:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();

	// This does NOT execute a query yet
	IQueryable<Customer> query = customers.Where(c => c.Name!.StartsWith("A"));

	// Query executes here when materialized
	List<Customer> results = await query.ToListAsync();
}
```

This allows you to build queries incrementally:

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Start with base query
	IQueryable<Order> query = orders.Where(o => o.Status != "Cancelled");

	// Conditionally add filters
	if (customerId.HasValue)
	{
		query = query.Where(o => o.CustomerId == customerId.Value);
	}

	if (fromDate.HasValue)
	{
		query = query.Where(o => o.OrderDate >= fromDate.Value);
	}

	// Execute the composed query
	List<Order> results = await query.ToListAsync();

	// A decimal threshold cannot be composed into the SQL query, so apply it last
	if (minAmount.HasValue)
	{
		results = results.Where(o => o.Amount >= minAmount.Value).ToList();
	}
}
```

### SQL Translation

SQLiteXM translates LINQ expressions into efficient SQL. For example:

```csharp
// This LINQ query:
IQueryable<Customer> query = customers
	.Where(c => c.Name!.StartsWith("A") && c.Email!.Contains("@example.com"))
	.OrderBy(c => c.Name)
	.Take(10);

// Translates to SQL similar to:
// SELECT * FROM Customer
// WHERE Name LIKE 'A%' AND Email LIKE '%@example.com%'
// ORDER BY Name
// LIMIT 10
```

### Performance Tips

1. **Use projections** when you only need specific columns:
   ```csharp
   // Good: retrieves only needed columns
   var names = await customers
	   .Select(c => new { c.id, c.Name })
	   .ToListAsync();

   // Less efficient: retrieves all columns
   List<Customer> allCustomers = await customers.ToListAsync();
   ```

2. **Filter before joining** to reduce the dataset size:
   ```csharp
   // Good: filter first
   var results = await customers
	   .Where(c => c.Name!.StartsWith("A"))
	   .Join(orders, c => c.id, o => o.CustomerId, (c, o) => new { c, o })
	   .ToListAsync();
   ```

3. **Use bulk operations** for modifying multiple rows:
   ```csharp
   // Good: single UPDATE statement
   await orders.Where(o => o.Status == "Cancelled").Set(o => o.Amount, 0m).UpdateAsync();

   // Slower: many individual updates
   List<Order> orderList = await orders.Where(o => o.Status == "Cancelled").ToListAsync();
   foreach (Order order in orderList)
   {
	   order.Amount = 0m;
	   await order.SaveAsync();
   }
   ```

4. **Use indexes** on columns frequently used in `Where` clauses. See ➡️ [Defining Entities](./defining-entities.md) for index configuration.

---

## Working with Multiple SQLite Databases

When your application uses multiple SQLite databases, specify which database when creating the transaction:

```csharp
// Query the default SQLite database
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	List<Customer> all = await customers.ToListAsync();
}

// Query a specific named SQLite database
await using (SxmTransaction ctx = new SxmTransaction("Archive"))
{
	SxmTable<Customer> archivedCustomers = ctx.GetTable<Customer>();
	List<Customer> archived = await archivedCustomers.ToListAsync();
}
```

> 💡 Once an `SxmTransaction` is created for a specific database, all LINQ queries, entity DML, and SQL statements in that block operate on that database. You cannot mix databases within a single transaction.

For more details, see ➡️ [Multiple Databases](./multiple-databases.md).

---

## Error Handling

LINQ queries can fail for several reasons: SQL syntax errors, constraint violations, or database connection issues. Always handle exceptions appropriately.

Not every failure arrives as the same exception type, so a single `catch` is rarely enough:

| Exception | When it happens |
|---|---|
| `InvalidOperationException` | `SingleAsync` / `FirstAsync` found zero or multiple rows |
| `NotSupportedException` | The expression could not be translated into SQL |
| `SxmException` | SQLiteXM could not complete the operation. Branch on `ErrorCode` |
| `SqliteException` | A constraint violation, such as a foreign-key or unique conflict |

Order the `catch` arms from most specific to least specific:

```csharp
try
{
	await using (SxmTransaction ctx = new SxmTransaction())
	{
		SxmTable<Customer> customers = ctx.GetTable<Customer>();

		// This might throw if no matching customer exists
		Customer customer = await customers
			.Where(c => c.Email == searchEmail)
			.SingleAsync();

		customer.Name = "Updated Name";
		await customer.SaveAsync();
	}
}
catch (InvalidOperationException ex)
{
	// SingleAsync throws if zero or multiple rows match
	Console.WriteLine($"Query failed: {ex.Message}");
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.QueryFailure)
{
	// The query itself failed to execute
	Console.WriteLine($"Query failed: {ex.Message}");
}
catch (SxmException ex) when (ex.ErrorCode == SxmDefines.SxmErrorCode.DataConversionFailure)
{
	// A stored value did not match the entity property type,
	// which usually means the entity and the stored schema have diverged
	Console.WriteLine($"Conversion failed: {ex.Message}");
}
catch (SxmException ex)
{
	// Any other SQLiteXM failure. ex.Context carries diagnostic detail.
	Console.WriteLine($"SQLiteXM error: {ex.Message}");
}
catch (SqliteException ex)
{
	// Constraint violations surface as SqliteException, not SxmException
	Console.WriteLine($"Constraint violation: {ex.Message}");
}
```

> 💡 `MappingFailure` is also possible when an entity, column, or association cannot be mapped — most often with `LoadWith`. For the complete list of error codes and which APIs raise them, see: ➡️ [Error Handling](./error-handling.md).

When an exception occurs inside an `SxmTransaction`:

1. The transaction is marked as **faulted**
2. Subsequent write operations are silently skipped
3. The transaction automatically **rolls back** when disposed

For more details on error handling, see the "Error Handling and Rollback" section in ➡️ [Working with Data](./working-with-data.md).

---

## Complete Examples

### Example 1: Customer Loyalty Program

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Find customers who spent over $1000 total.
	// This comparison is against the result of Sum, which is already a number,
	// so it is evaluated numerically even though Amount is a decimal column.
	List<long> loyalCustomers = await (
		from o in orders
		group o by o.CustomerId into g
		where g.Sum(o => o.Amount) > 1000m
		select g.Key
	).ToListAsync();

	// Update their status using a bulk operation
	int updated = await customers
		.Where(c => loyalCustomers.Contains(c.id))
		.Set(c => c.Name, c => "[VIP] " + c.Name)
		.UpdateAsync();

	Console.WriteLine($"Upgraded {updated} customers to VIP status");
}
```

### Example 2: Report Generation

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Generate monthly sales report
	var monthlySales = await orders
		.GroupBy(o => new { Month = o.OrderDate.Month, Year = o.OrderDate.Year })
		.Select(g => new
		{
			g.Key.Year,
			g.Key.Month,
			TotalSales = g.Sum(o => o.Amount),
			OrderCount = g.Count(),
			AverageOrder = g.Average(o => o.Amount)
		})
		.OrderBy(s => s.Year)
		.ThenBy(s => s.Month)
		.ToListAsync();

	foreach (var month in monthlySales)
	{
		Console.WriteLine($"{month.Year}-{month.Month:D2}: " +
						 $"{month.OrderCount} orders, " +
						 $"${month.TotalSales:N2} total, " +
						 $"${month.AverageOrder:N2} average");
	}
}
```

### Example 3: Data Cleanup

```csharp
await using (SxmTransaction ctx = new SxmTransaction())
{
	SxmTable<Customer> customers = ctx.GetTable<Customer>();
	SxmTable<Order> orders = ctx.GetTable<Order>();

	// Find customers with no orders
	List<long> customerIdsWithOrders = await orders
		.Select(o => o.CustomerId)
		.Distinct()
		.ToListAsync();

	// Delete inactive customers (no orders)
	int deletedCount = await customers
		.Where(c => !customerIdsWithOrders.Contains(c.id))
		.DeleteAsync();

	Console.WriteLine($"Deleted {deletedCount} inactive customers");
}
```

---

## Best Practices

1. **Always use async methods:** SQLiteXM is built for async I/O. Always await `ToListAsync()`, `FirstAsync()`, etc., rather than using synchronous LINQ methods.

2. **Keep transactions scoped:** Create transactions with `await using` to ensure proper disposal and commit/rollback behavior.

3. **Prefer bulk operations:** When modifying multiple rows, use bulk `Set().UpdateAsync()` or `DeleteAsync()` instead of loading entities and saving them individually.

4. **Project early:** Use `Select` to retrieve only the columns you need, especially for reporting or display scenarios.

5. **Compose queries conditionally:** Build queries incrementally based on search criteria rather than writing separate query methods for each combination of filters.

6. **Use indexes wisely:** Add indexes to columns frequently used in `Where` clauses and joins. See ➡️ [Defining Entities](./defining-entities.md).

7. **Handle exceptions:** Always wrap transaction blocks in try/catch. Catch `SxmException` and branch on `ErrorCode` rather than catching bare `Exception`, and remember that constraint violations arrive as `SqliteException`. See ➡️ [Error Handling](./error-handling.md).

8. **Test query performance:** For complex queries or large datasets, verify that your LINQ expressions translate to efficient SQL.

Materialize first, then compare or sort in memory. See [Supported Data Types — decimal](./supported-data-types.md#decimal).

---

## LINQ Method Reference

### Query Operators

| Operator | Purpose | Example |
|----------|---------|---------|
| `Where` | Filter rows | `.Where(c => c.Name!.StartsWith("A"))` |
| `Select` | Project/transform results | `.Select(c => new { c.Name, c.Email })` |
| `OrderBy` | Sort ascending | `.OrderBy(c => c.Name)` |
| `OrderByDescending` | Sort descending | `.OrderByDescending(o => o.OrderDate)` |
| `ThenBy` | Additional sort ascending | `.OrderBy(c => c.Name).ThenBy(c => c.Email)` |
| `ThenByDescending` | Additional sort descending | `.OrderBy(c => c.Name).ThenByDescending(c => c.Email)` |
| `Take` | Limit results | `.Take(10)` |
| `Skip` | Skip rows | `.Skip(20)` |
| `Distinct` | Remove duplicates | `.Select(o => o.CustomerId).Distinct()` |
| `GroupBy` | Group rows | `.GroupBy(o => o.CustomerId)` |
| `Join` | Inner join | `.Join(orders, c => c.id, o => o.CustomerId, ...)` |

### Materialization Methods

All materialization methods are async and must be awaited:

| Method | Return Type | Description |
|--------|-------------|-------------|
| `ToListAsync()` | `Task<List<T>>` | Convert results to a list |
| `ToArrayAsync()` | `Task<T[]>` | Convert results to an array |
| `ToDictionaryAsync()` | `Task<Dictionary<TKey,T>>` | Convert results to a dictionary. ⚠️ Throws if the key column holds duplicate values |
| `FirstAsync()` | `Task<T>` | First row (throws if empty) |
| `FirstOrDefaultAsync()` | `Task<T?>` | First row or null |
| `SingleAsync()` | `Task<T>` | Only row (throws if 0 or >1) |
| `SingleOrDefaultAsync()` | `Task<T?>` | Only row or null (throws if >1) |

### Aggregation Methods

| Method | Return Type | Description |
|--------|-------------|-------------|
| `CountAsync()` | `Task<int>` | Count rows |
| `LongCountAsync()` | `Task<long>` | Count rows (long) |
| `AnyAsync()` | `Task<bool>` | Check if any rows exist |
| `AllAsync()` | `Task<bool>` | Check if all rows match predicate |
| `SumAsync()` | `Task<TResult>` | Sum a numeric column |
| `AverageAsync()` | `Task<TResult>` | Average a numeric column |
| `MinAsync()` | `Task<TResult?>` | Minimum value. ⚠️ Not reliable on a `decimal` column |
| `MaxAsync()` | `Task<TResult?>` | Maximum value. ⚠️ Not reliable on a `decimal` column |
| `ContainsAsync()` | `Task<bool>` | Check whether a given item exists |

### Modification Methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `BulkInsertAsync()` | `(entities, batchRows?, CancellationToken?)` | Insert many new entities using multi-row INSERT statements |
| `Set()` | `(property, value)` | Set property to constant value |
| `Set()` | `(property, expression)` | Set property using expression |
| `UpdateAsync()` | `CancellationToken?` | Execute the bulk update |
| `DeleteAsync()` | `CancellationToken?` | Execute the bulk delete |

---

## Related Documentation

- ➡️ [Getting Started](./getting-started.md) - Initialize SQLiteXM and register entities
- ➡️ [Working with Data](./working-with-data.md) - Entity DML, SQL, and transactions
- ➡️ [Defining Entities](./defining-entities.md) - Configure entities and indexes
- ➡️ [Loading Related Data](./loading-related-data.md) - Load related entities with `LoadWith`
- ➡️ [Multiple SQLite Databases](./multiple-databases.md) - Work with multiple SQLite databases

---

## Summary

LINQ in SQLiteXM provides a powerful, type-safe way to query and manipulate data. Key points to remember:

- LINQ queries must run inside an `SxmTransaction` block
- Use `ctx.GetTable<T>()` to obtain a queryable table reference
- Queries are translated to SQL and execute directly against the database
- Always use async materialization methods (`ToListAsync`, `FirstAsync`, etc.)
- Bulk operations (`Set().UpdateAsync()`, `DeleteAsync()`) are more efficient than entity-by-entity updates
- LINQ queries participate in the same transaction as entity DML and SQL statements

With LINQ support, SQLiteXM combines the productivity of strongly-typed queries with the power of direct SQL and entity persistence, all unified in a consistent transactional model.
