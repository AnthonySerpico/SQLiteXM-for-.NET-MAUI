# SQL Statements File Guide

## Overview

The SQL Statements file is a centralized JSON (or XML) configuration file that serves as a repository for all named SQL statements used by your SQLiteXM application. This file is processed during database initialization and provides two critical functions:

1. **Database Definition** - Declares the database name and default status
2. **SQL Statement Registry** - Registers named INSERT, SELECT, UPDATE, DELETE, and TRIGGER statements

## Table of Contents

- [File Structure](#file-structure)
- [Database Configuration](#database-configuration)
- [Registering SQL Statements](#registering-sql-statements)
- [Using Registered Statements](#using-registered-statements)
- [Complete Example](#complete-example)
- [Best Practices](#best-practices)

---

## File Structure

The SQL Statements file must be a JSON or XML file with a specific structure. This guide uses JSON examples.

### Basic Structure

```json
{
  "database": "your_database_name",
  "isDefault": true,

  "insert": [ /* INSERT statements */ ],
  "select": [ /* SELECT statements */ ],
  "update": [ /* UPDATE statements */ ],
  "delete": [ /* DELETE statements */ ],
  "trigger": [ /* TRIGGER statements */ ]
}
```

---

## Database Configuration

### Database Name

**Property:** `database`  
**Type:** `string`  
**Required:** Yes

The `database` property specifies the name of the SQLite database file that will be created (without the `.db` extension).

```json
{
  "database": "myapp_database"
}
```

This will create a file named `myapp_database.db` in the configured database folder.

---

### Default Database

**Property:** `isDefault`  
**Type:** `boolean`  
**Required:** Yes

The `isDefault` property marks whether this database is the default database for your application.

```json
{
  "database": "myapp_database",
  "isDefault": true
}
```

#### Default Database Rules

**✅ Required:**
- **There MUST be exactly one database marked as default**
- Every application must have a default database

**📌 Purpose:**
The default database is used when you call SQLiteXM APIs that accept an optional database name parameter and you don't provide one.

**Example - APIs Using Default Database:**

```csharp
// ✅ These use the default database (no database name specified)

// SxmStatement API
List<User> users = await SxmStatement.SelectAsync<User>(
	"getUsers",
	new List<object> { 100 }
);

// SxmLinqDbContext API
using var db = new SxmLinqDbContext();  // Uses default database
var activeUsers = db.GetTable<User>()
	.Where(u => u.IsActive)
	.ToList();

// SxmSqlTransaction API
using var transaction = SxmSqlTransaction.Create();  // Uses default database
await transaction.InsertAsync<Order>("insertOrder", newOrder);
```

**Example - Specifying Different Database:**

```csharp
// ⚠️ These explicitly specify a different database name

// With database name parameter
List<User> users = await SxmStatement.SelectAsync<User>(
	"getUsers",
	new List<object> { 100 },
	"secondary_database"  // Override default
);

// SxmLinqDbContext with specific database
using var db = new SxmLinqDbContext("secondary_database");
```

---

## Registering SQL Statements

### Statement Arrays

The SQL Statements file contains five arrays for different statement types:

1. **`insert`** - INSERT statements
2. **`select`** - SELECT statements
3. **`update`** - UPDATE statements
4. **`delete`** - DELETE statements
5. **`trigger`** - TRIGGER definitions

#### Common Fields

**INSERT, SELECT, UPDATE, DELETE statements:**
- **Statement Name** - Unique identifier for the statement
- **Table Name** - The target table
- **Statement** - The actual SQL code

**TRIGGER statements:**
- **Database** - The target database (REQUIRED)
- **Table Name** - The target table
- **Statement** - The actual SQL code

---

### INSERT Statements

Register INSERT statements that will be used to add records.

```json
{
  "insert": [
	{
	  "Statement Name": "insertUser",
	  "Table Name": "Users",
	  "Statement": "INSERT INTO Users (username, email, created_at) VALUES (@username, @email, @created_at)"
	},
	{
	  "Statement Name": "insertOrder",
	  "Table Name": "Orders",
	  "Statement": "INSERT INTO Orders (user_id, total_amount, order_date) VALUES (@user_id, @total_amount, @order_date)"
	}
  ]
}
```

**Parameter Binding:**
- Use `@parameterName` for named parameters
- Use `@p0`, `@p1`, `@p2` for positional parameters

**Usage Example:**

```csharp
// Named parameters
var user = new User 
{ 
	username = "john_doe", 
	email = "john@example.com", 
	created_at = DateTime.UtcNow 
};

await SxmStatement.InsertAsync<User>("insertUser", user);

// Positional parameters
await SxmStatement.InsertAsync(
	"insertOrder",
	new List<object> { 123, 99.99, DateTime.UtcNow }
);
```

---

### SELECT Statements

Register SELECT statements for querying data.

```json
{
  "select": [
	{
	  "Statement Name": "getUserById",
	  "Table Name": "Users",
	  "Statement": "SELECT * FROM Users WHERE id = @p0"
	},
	{
	  "Statement Name": "getActiveUsers",
	  "Table Name": "Users",
	  "Statement": "SELECT * FROM Users WHERE is_active = 1 ORDER BY username LIMIT 100"
	},
	{
	  "Statement Name": "getOrdersByUser",
	  "Table Name": "Orders",
	  "Statement": "SELECT * FROM Orders WHERE user_id = @user_id ORDER BY order_date DESC"
	},
	{
	  "Statement Name": "getUsersWithOrders",
	  "Table Name": "Users",
	  "Statement": "SELECT u.*, COUNT(o.id) as order_count FROM Users u LEFT JOIN Orders o ON u.id = o.user_id GROUP BY u.id"
	}
  ]
}
```

**Usage Examples:**

```csharp
// Get single user by ID (positional parameter)
List<User> users = await SxmStatement.SelectAsync<User>(
	"getUserById",
	new List<object> { 42 }
);
User user = users.FirstOrDefault();

// Get all active users (no parameters)
List<User> activeUsers = await SxmStatement.SelectAsync<User>(
	"getActiveUsers",
	new List<object>()
);

// Get orders by user (named parameter)
var orderSearch = new { user_id = 123 };
List<Order> orders = await SxmStatement.SelectAsync<Order>(
	"getOrdersByUser",
	orderSearch
);

// Complex query returning dictionaries
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(
	"getUsersWithOrders",
	new List<object>()
);
```

---

### UPDATE Statements

Register UPDATE statements for modifying records.

```json
{
  "update": [
	{
	  "Statement Name": "updateUserEmail",
	  "Table Name": "Users",
	  "Statement": "UPDATE Users SET email = @email, updated_at = @updated_at WHERE id = @id"
	},
	{
	  "Statement Name": "activateUser",
	  "Table Name": "Users",
	  "Statement": "UPDATE Users SET is_active = 1 WHERE id = @p0"
	},
	{
	  "Statement Name": "updateOrderStatus",
	  "Table Name": "Orders",
	  "Statement": "UPDATE Orders SET status = @status, updated_at = @updated_at WHERE id = @id"
	}
  ]
}
```

**Usage Examples:**

```csharp
// Named parameters with object
var updateData = new 
{ 
	email = "newemail@example.com",
	updated_at = DateTime.UtcNow,
	id = 42
};
await SxmStatement.UpdateAsync("updateUserEmail", updateData);

// Positional parameter
await SxmStatement.UpdateAsync(
	"activateUser",
	new List<object> { 42 }
);

// Within transaction
await using var transaction = SxmSqlTransaction.Create();
await transaction.UpdateAsync(
	"updateOrderStatus",
	new { status = "shipped", updated_at = DateTime.UtcNow, id = 1001 }
);
await transaction.CommitTransactionAsync();
```

---

### DELETE Statements

Register DELETE statements for removing records.

```json
{
  "delete": [
	{
	  "Statement Name": "deleteUserById",
	  "Table Name": "Users",
	  "Statement": "DELETE FROM Users WHERE id = @p0"
	},
	{
	  "Statement Name": "deleteInactiveUsers",
	  "Table Name": "Users",
	  "Statement": "DELETE FROM Users WHERE is_active = 0 AND last_login < @p0"
	},
	{
	  "Statement Name": "deleteAllUserOrders",
	  "Table Name": "Orders",
	  "Statement": "DELETE FROM Orders WHERE user_id = @user_id"
	}
  ]
}
```

**Usage Examples:**

```csharp
// Delete single record
await SxmStatement.DeleteAsync(
	"deleteUserById",
	new List<object> { 42 }
);

// Delete with date filter
DateTime cutoffDate = DateTime.UtcNow.AddMonths(-6);
await SxmStatement.DeleteAsync(
	"deleteInactiveUsers",
	new List<object> { cutoffDate }
);

// Delete within transaction
await using var transaction = SxmSqlTransaction.Create();
await transaction.DeleteAsync(
	"deleteAllUserOrders",
	new { user_id = 123 }
);
await transaction.CommitTransactionAsync();
```

---

### TRIGGER Statements

Register database triggers that execute automatically on INSERT, UPDATE, or DELETE operations.

**⚠️ REQUIRED FIELD:** Each trigger must specify a `Database` field indicating which database the trigger belongs to.

```json
{
  "trigger": [
	{
	  "Database": "myapp_database",
	  "Table Name": "Users",
	  "Statement": "CREATE TRIGGER update_user_timestamp AFTER UPDATE ON Users BEGIN UPDATE Users SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id; END;"
	},
	{
	  "Database": "myapp_database",
	  "Table Name": "Orders",
	  "Statement": "CREATE TRIGGER log_order_insert AFTER INSERT ON Orders BEGIN INSERT INTO OrderAuditLog (order_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
	},
	{
	  "Database": "analytics_database",
	  "Table Name": "Events",
	  "Statement": "CREATE TRIGGER track_event_changes AFTER INSERT ON Events BEGIN INSERT INTO EventHistory (event_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
	}
  ]
}
```

**Multi-Database Support:**

Triggers can target different databases within the same SQL Statements file. Each trigger must explicitly specify its target database via the `Database` field.

```json
{
  "database": "myapp_database",
  "isDefault": true,

  "trigger": [
	{
	  "Database": "myapp_database",
	  "Table Name": "Users",
	  "Statement": "CREATE TRIGGER ..."
	},
	{
	  "Database": "analytics_database",
	  "Table Name": "Events",
	  "Statement": "CREATE TRIGGER ..."
	}
  ]
}
```

**Trigger Notes:**

- **`Database` field is REQUIRED** - Each trigger must specify which database it belongs to
- Triggers are created automatically during entity schema registration
- They apply to the specified table
- Use `NEW` to reference new row values (INSERT/UPDATE)
- Use `OLD` to reference old row values (UPDATE/DELETE)
- Triggers from the SQL Statements file are applied during `RegisterEntitiesAsync`

**⚠️ Warning:** If a trigger references a table that hasn't been registered, SQLiteXM will log a warning about unassigned triggers.

---

## Using Registered Statements

### In Application Code

Once registered in the SQL Statements file, you can use these statements throughout your application by name.

#### SxmStatement API

```csharp
using SQLiteXM;

// SELECT
List<User> users = await SxmStatement.SelectAsync<User>(
	"getActiveUsers",
	new List<object>()
);

// INSERT
await SxmStatement.InsertAsync<User>("insertUser", newUser);

// UPDATE
await SxmStatement.UpdateAsync("updateUserEmail", updateData);

// DELETE
await SxmStatement.DeleteAsync("deleteUserById", new List<object> { userId });
```

#### Within Transactions

```csharp
await using var transaction = SxmSqlTransaction.Create();

try
{
	// Multiple operations in one transaction
	await transaction.InsertAsync<Order>("insertOrder", newOrder);
	await transaction.UpdateAsync("updateUserEmail", emailUpdate);
	await transaction.DeleteAsync("deleteInactiveUsers", new List<object> { cutoff });

	await transaction.CommitTransactionAsync();
}
catch
{
	await transaction.RollbackTransactionAsync();
	throw;
}
```

---

## Complete Example

Here's a complete SQL Statements file for a simple e-commerce application:

```json
{
  "database": "ecommerce_db",
  "isDefault": true,

  "insert": [
	{
	  "Statement Name": "insertCustomer",
	  "Table Name": "Customers",
	  "Statement": "INSERT INTO Customers (name, email, phone, created_at) VALUES (@name, @email, @phone, @created_at)"
	},
	{
	  "Statement Name": "insertProduct",
	  "Table Name": "Products",
	  "Statement": "INSERT INTO Products (name, description, price, stock) VALUES (@name, @description, @price, @stock)"
	},
	{
	  "Statement Name": "insertOrder",
	  "Table Name": "Orders",
	  "Statement": "INSERT INTO Orders (customer_id, order_date, total_amount, status) VALUES (@customer_id, @order_date, @total_amount, @status)"
	},
	{
	  "Statement Name": "insertOrderItem",
	  "Table Name": "OrderItems",
	  "Statement": "INSERT INTO OrderItems (order_id, product_id, quantity, unit_price) VALUES (@order_id, @product_id, @quantity, @unit_price)"
	}
  ],

  "select": [
	{
	  "Statement Name": "getCustomerById",
	  "Table Name": "Customers",
	  "Statement": "SELECT * FROM Customers WHERE id = @p0"
	},
	{
	  "Statement Name": "getCustomerByEmail",
	  "Table Name": "Customers",
	  "Statement": "SELECT * FROM Customers WHERE email = @email LIMIT 1"
	},
	{
	  "Statement Name": "getAllProducts",
	  "Table Name": "Products",
	  "Statement": "SELECT * FROM Products WHERE stock > 0 ORDER BY name"
	},
	{
	  "Statement Name": "getProductById",
	  "Table Name": "Products",
	  "Statement": "SELECT * FROM Products WHERE id = @p0"
	},
	{
	  "Statement Name": "getOrdersByCustomer",
	  "Table Name": "Orders",
	  "Statement": "SELECT * FROM Orders WHERE customer_id = @customer_id ORDER BY order_date DESC"
	},
	{
	  "Statement Name": "getOrderItems",
	  "Table Name": "OrderItems",
	  "Statement": "SELECT oi.*, p.name as product_name FROM OrderItems oi JOIN Products p ON oi.product_id = p.id WHERE oi.order_id = @order_id"
	},
	{
	  "Statement Name": "getPendingOrders",
	  "Table Name": "Orders",
	  "Statement": "SELECT * FROM Orders WHERE status = 'pending' ORDER BY order_date"
	}
  ],

  "update": [
	{
	  "Statement Name": "updateCustomerInfo",
	  "Table Name": "Customers",
	  "Statement": "UPDATE Customers SET name = @name, phone = @phone WHERE id = @id"
	},
	{
	  "Statement Name": "updateProductStock",
	  "Table Name": "Products",
	  "Statement": "UPDATE Products SET stock = stock + @quantity WHERE id = @id"
	},
	{
	  "Statement Name": "updateOrderStatus",
	  "Table Name": "Orders",
	  "Statement": "UPDATE Orders SET status = @status, updated_at = @updated_at WHERE id = @id"
	},
	{
	  "Statement Name": "updateProductPrice",
	  "Table Name": "Products",
	  "Statement": "UPDATE Products SET price = @price WHERE id = @id"
	}
  ],

  "delete": [
	{
	  "Statement Name": "deleteCustomer",
	  "Table Name": "Customers",
	  "Statement": "DELETE FROM Customers WHERE id = @p0"
	},
	{
	  "Statement Name": "deleteProduct",
	  "Table Name": "Products",
	  "Statement": "DELETE FROM Products WHERE id = @p0"
	},
	{
	  "Statement Name": "deleteCancelledOrders",
	  "Table Name": "Orders",
	  "Statement": "DELETE FROM Orders WHERE status = 'cancelled' AND order_date < @p0"
	}
  ],

  "trigger": [
	{
	  "Table Name": "Orders",
	  "Statement": "CREATE TRIGGER update_order_timestamp AFTER UPDATE ON Orders BEGIN UPDATE Orders SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id; END;"
	},
	{
	  "Table Name": "OrderItems",
	  "Statement": "CREATE TRIGGER decrease_stock_on_order AFTER INSERT ON OrderItems BEGIN UPDATE Products SET stock = stock - NEW.quantity WHERE id = NEW.product_id; END;"
	},
	{
	  "Table Name": "OrderItems",
	  "Statement": "CREATE TRIGGER restore_stock_on_cancel AFTER DELETE ON OrderItems BEGIN UPDATE Products SET stock = stock + OLD.quantity WHERE id = OLD.product_id; END;"
	}
  ]
}
```

### Using the Complete Example

```csharp
public class EcommerceService
{
	// Create new customer
	public async Task<long> CreateCustomerAsync(string name, string email, string phone)
	{
		var customer = new Customer
		{
			name = name,
			email = email,
			phone = phone,
			created_at = DateTime.UtcNow
		};

		var result = await SxmStatement.InsertAsync<Customer>("insertCustomer", customer);
		return (long)result["id"];
	}

	// Get customer by email
	public async Task<Customer?> FindCustomerByEmailAsync(string email)
	{
		var customers = await SxmStatement.SelectAsync<Customer>(
			"getCustomerByEmail",
			new { email = email }
		);
		return customers.FirstOrDefault();
	}

	// Create order with items
	public async Task<long> CreateOrderAsync(long customerId, List<OrderItemData> items)
	{
		await using var transaction = SxmSqlTransaction.Create();

		try
		{
			// Calculate total
			decimal total = items.Sum(i => i.Quantity * i.UnitPrice);

			// Insert order
			var orderData = new
			{
				customer_id = customerId,
				order_date = DateTime.UtcNow,
				total_amount = total,
				status = "pending"
			};
			var orderResult = await transaction.InsertAsync("insertOrder", orderData);
			long orderId = (long)orderResult["id"];

			// Insert order items (triggers will decrease stock automatically)
			foreach (var item in items)
			{
				var itemData = new
				{
					order_id = orderId,
					product_id = item.ProductId,
					quantity = item.Quantity,
					unit_price = item.UnitPrice
				};
				await transaction.InsertAsync("insertOrderItem", itemData);
			}

			await transaction.CommitTransactionAsync();
			return orderId;
		}
		catch
		{
			await transaction.RollbackTransactionAsync();
			throw;
		}
	}

	// Get order with items
	public async Task<OrderWithItems> GetOrderDetailsAsync(long orderId)
	{
		// Get order
		var orders = await SxmStatement.SelectAsync<Order>(
			"getOrdersByCustomer",  // Could also create getOrderById
			new List<object> { orderId }
		);
		var order = orders.FirstOrDefault();

		// Get items
		var items = await SxmStatement.SelectAsync(
			"getOrderItems",
			new { order_id = orderId }
		);

		return new OrderWithItems
		{
			Order = order,
			Items = items
		};
	}

	// Update order status
	public async Task ShipOrderAsync(long orderId)
	{
		await SxmStatement.UpdateAsync(
			"updateOrderStatus",
			new { status = "shipped", updated_at = DateTime.UtcNow, id = orderId }
		);
	}
}
```

---

## Best Practices

### 1. **Use Descriptive Statement Names**

```json
// ✅ Good - Clear and descriptive
"Statement Name": "getUsersByRegistrationDate"
"Statement Name": "deleteExpiredSessions"
"Statement Name": "updateUserPassword"

// ❌ Bad - Vague or unclear
"Statement Name": "query1"
"Statement Name": "doSomething"
"Statement Name": "update"
```

### 2. **Consistent Naming Convention**

Choose a naming convention and stick to it:

```json
// Option 1: camelCase with verb prefix
"insertUser", "getActiveUsers", "updateUserEmail"

// Option 2: PascalCase with verb prefix
"InsertUser", "GetActiveUsers", "UpdateUserEmail"

// Option 3: snake_case with verb prefix
"insert_user", "get_active_users", "update_user_email"
```

### 3. **Organize by Feature**

Group related statements logically:

```json
{
  "select": [
	// User queries
	{ "Statement Name": "getUserById", ... },
	{ "Statement Name": "getUsersByRole", ... },

	// Order queries
	{ "Statement Name": "getOrderById", ... },
	{ "Statement Name": "getOrdersByStatus", ... },

	// Product queries
	{ "Statement Name": "getProductById", ... },
	{ "Statement Name": "getProductsByCategory", ... }
  ]
}
```

### 4. **Use Parameters for Security**

```json
// ✅ Good - Parameterized (prevents SQL injection)
"Statement": "SELECT * FROM Users WHERE email = @email"

// ❌ Bad - String concatenation (security risk)
// Never build SQL by concatenating user input
```

### 5. **Include Limits on SELECT Statements**

```json
// ✅ Good - Has LIMIT to prevent huge result sets
"Statement": "SELECT * FROM Users WHERE is_active = 1 LIMIT 1000"

// ⚠️ Caution - No limit (could return millions of rows)
"Statement": "SELECT * FROM Users WHERE is_active = 1"
```

### 6. **Document Complex Queries**

Use clear statement names and table names to document intent:

```json
{
  "Statement Name": "getCustomersWithRecentOrders",
  "Table Name": "Customers",
  "Statement": "SELECT c.*, COUNT(o.id) as recent_order_count FROM Customers c LEFT JOIN Orders o ON c.id = o.customer_id AND o.order_date > @cutoff_date GROUP BY c.id HAVING recent_order_count > 0"
}
```

### 7. **Keep Statements File in Version Control**

The SQL Statements file is configuration, so:
- ✅ Commit it to Git
- ✅ Review changes in PRs
- ✅ Document breaking changes
- ✅ Version it alongside your code

### 8. **One Database = One File**

If you have multiple databases:

```
/Resources/Raw/
  ├── MainDatabase.json      (isDefault: true)
  ├── AnalyticsDatabase.json (isDefault: false)
  └── CacheDatabase.json     (isDefault: false)
```

Each file declares its own database and has its own statements.

---

## File Placement

### .NET MAUI Projects

Place the file in your MAUI project's `Resources/Raw` folder:

```
MyMauiApp/
├── Resources/
│   └── Raw/
│       └── SqlStatements.json  ← Here
├── App.xaml.cs
└── MauiProgram.cs
```

Ensure the file is configured as a `MauiAsset`:

```xml
<MauiAsset Include="Resources\Raw\SqlStatements.json" />
```

### Loading from Raw Assets

```csharp
// In MauiProgram.cs or App.xaml.cs
using Stream stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
await SxmDatabase.InitializeAsync(stream, databaseOptions);
```

---

## Related Documentation

- [Database Initialization Guide](./DatabaseInitialization.md)
- [SxmDatabaseOptions Configuration](./SxmDatabaseOptions.md)
- [Entity Registration](./EntityRegistration.md)
- [Using Named Statements](./NamedStatements.md)

---

## Troubleshooting

### "Database name must be specified"

**Problem:** Missing `database` property  
**Solution:** Add the database property:

```json
{
  "database": "myapp_database",
  "isDefault": true
}
```

### "No default database configured"

**Problem:** No database has `isDefault: true`  
**Solution:** Mark one database as default:

```json
{
  "database": "myapp_database",
  "isDefault": true
}
```

### "Unassigned trigger(s) detected"

**Problem:** Trigger references a table that wasn't registered  
**Solution:** Ensure all trigger table names match registered entities:

```json
{
  "Table Name": "Users",  // Must match registered entity table name
  "Statement": "CREATE TRIGGER ..."
}
```

Then register the entity:

```csharp
await SxmDatabase.RegisterEntitiesAsync(typeof(User));
```

### "Statement not found"

**Problem:** Trying to use a statement name that doesn't exist  
**Solution:** Check spelling and ensure it's registered:

```csharp
// Make sure this name exists in the SQL Statements file
await SxmStatement.SelectAsync<User>("getUserById", new List<object> { 42 });
```

---

## Version Information

- **SQLiteXM Version:** 1.0+
- **Last Updated:** 2026
- **Compatibility:** .NET 8, .NET 9, .NET MAUI
