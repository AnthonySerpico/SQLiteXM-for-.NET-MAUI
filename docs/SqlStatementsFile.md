# SQL Statements File Guide

## Overview

The SQL Statements file is a centralized JSON (or XML) configuration file that serves as a repository for all named SQL statements used by your SQLiteXM application. This file is processed during database initialization and provides three critical functions:

1. **Database Declaration** - Declares one or more database names and which is the default
2. **SQL Statement Registry** - Registers named INSERT, SELECT, UPDATE, DELETE, and TRIGGER statements
3. **Multi-Database Support** - Enables working with multiple separate SQLite database files

**Note:** Database configurations (folder paths, connection settings, etc.) are specified separately via `SxmDatabaseOptions` during initialization, not in this file.

## Table of Contents

- [File Structure](#file-structure)
  - [Single Database Configuration](#single-database-configuration)
  - [Multi-Database Configuration](#multi-database-configuration)
- [Database Configuration](#database-configuration)
- [Registering SQL Statements](#registering-sql-statements)
- [Using Registered Statements](#using-registered-statements)
- [Multi-Database Examples](#multi-database-examples)
- [Best Practices](#best-practices)

---

## File Structure

The SQL Statements file uses a consistent structure with a `databases` array, whether you have one database or multiple databases.

### Basic Structure

```json
{
  "version": 1,
  "databases": [
	{
	  "database": "your_database_name",
	  "isDefault": true
	}
  ],

  "insert": [ /* INSERT statements */ ],
  "select": [ /* SELECT statements */ ],
  "update": [ /* UPDATE statements */ ],
  "delete": [ /* DELETE statements */ ],
  "trigger": [ /* TRIGGER statements */ ]
}
```

**Properties:**
- `version` (number, required): File format version (currently `1`)
- `databases` (array, required): Array of database definitions
  - Each database object has:
	- `database` (string, required): Database name (without `.db` extension)
	- `isDefault` (boolean, required): Whether this is the default database

**⚠️ Important:** Exactly **one database must be marked as default** (`isDefault: true`).

### Single Database Example

Even with one database, use the `databases` array:

```json
{
  "version": 1,
  "databases": [
	{
	  "database": "myapp_database",
	  "isDefault": true
	}
  ],

  "insert": [ /* INSERT statements */ ],
  "select": [ /* SELECT statements */ ]
}
```

### Multi-Database Example

For multiple databases, add more entries to the `databases` array:

```json
{
  "version": 1,
  "databases": [
	{
	  "database": "main_database",
	  "isDefault": true
	},
	{
	  "database": "analytics_database",
	  "isDefault": false
	},
	{
	  "database": "cache_database",
	  "isDefault": false
	}
  ],

  "insert": [ /* INSERT statements */ ],
  "select": [ /* SELECT statements */ ]
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

### Default Database

**Property:** `isDefault`  
**Type:** `boolean`  
**Required:** Yes

The `isDefault` property marks whether this database is the default database for your application.

**Default Database Rules:**

**✅ Required:**
- **Exactly one database must be marked as default**
- Every application must have a default database
- In single-database config: must be `true`
- In multi-database config: exactly one database in the array must be `true`

**📌 Purpose:**
The default database is used when you call SQLiteXM APIs without specifying a database name parameter.

**Example - APIs Using Default Database:**

```csharp
// ✅ These use the default database (no database name specified)

// Entity operations (automatically routes based on [Table] attribute)
[Table(IsColumnAttributeRequired = false)]  // No Database specified = uses default database
public class Product : SxmEntity
{
	public string Name { get; set; }
	public decimal Price { get; set; }
}

var product = new Product { Name = "Widget", Price = 9.99m };
await product.SaveAsync();  // Saves to default database (where Product table was created)

// LINQ queries
using var db = new SxmLinqDbContext();  // Uses default database
var activeProducts = db.GetTable<Product>()
	.Where(p => p.InStock)
	.ToList();

// Named statements
List<User> users = await SxmStatement.SelectAsync<User>(
	"getActiveUsers",
	new List<object>()
);  // Queries default database

// Transactions
using var transaction = SxmSqlTransaction.Create();  // Uses default database
await transaction.InsertAsync<Order>("insertOrder", newOrder);
```

**Example - Entities Routing to Non-Default Databases:**

```csharp
// ⚠️ Entity automatically routes to non-default database via [Table] attribute

// This entity's table is created in "analytics_database"
[Table(Database = "analytics_database", IsColumnAttributeRequired = false)]
public class AnalyticsEvent : SxmEntity
{
	public string EventName { get; set; }
	public DateTime Timestamp { get; set; }
}

var event = new AnalyticsEvent { EventName = "PageView" };
await event.SaveAsync();  // Automatically saves to analytics_database (where AnalyticsEvent table is)

// This entity's table is in the default database
[Table(IsColumnAttributeRequired = false)]  // No Database parameter
public class Product : SxmEntity
{
	public string Name { get; set; }
}

var product = new Product { Name = "Widget" };
await product.SaveAsync();  // Automatically saves to default database (where Product table is)
```

**How Entity Routing Works:**

- If `[Table]` attribute **has no `Database` parameter**: Entity table is created in the **default database**
- If `[Table]` attribute **has `Database = "name"`**: Entity table is created in the **named database**
- `SaveAsync()`, `DeleteAsync()`, etc. **automatically route** to wherever the entity's table was created
- You don't specify the database when calling `SaveAsync()` - it's determined by the `[Table]` attribute

**Example - Explicitly Specifying Database for LINQ/Statements:**

```csharp
// For LINQ queries and named statements, you can explicitly specify a database

// LINQ with specific database
using var db = new SxmLinqDbContext("analytics_database");
var events = db.GetTable<AnalyticsEvent>().ToList();

// Named statements with database parameter
List<Log> logs = await SxmStatement.SelectAsync<Log>(
	"getRecentLogs",
	new List<object> { DateTime.UtcNow.AddHours(-1) },
	"analytics_database"  // Explicitly specify database
);
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

**Format depends on the number of databases:**

#### Single Database

When using one database, triggers **do not require** a `Database` field:

```json
{
  "version": 1,
  "databases": [
	{
	  "database": "myapp_database",
	  "isDefault": true
	}
  ],

  "trigger": [
	{
	  "Table Name": "Users",
	  "Statement": "CREATE TRIGGER update_user_timestamp AFTER UPDATE ON Users BEGIN UPDATE Users SET updated_at = CURRENT_TIMESTAMP WHERE id = NEW.id; END;"
	},
	{
	  "Table Name": "Orders",
	  "Statement": "CREATE TRIGGER log_order_insert AFTER INSERT ON Orders BEGIN INSERT INTO OrderAuditLog (order_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
	}
  ]
}
```

#### Multiple Databases

When using multiple databases, each trigger **must specify** a `Database` field:

```json
{
  "version": 1,
  "databases": [
	{
	  "database": "myapp_database",
	  "isDefault": true
	},
	{
	  "database": "analytics_database",
	  "isDefault": false
	}
  ],

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

**Trigger Notes:**

- **One database**: `Database` field is optional (implicitly uses the single database)
- **Multiple databases**: `Database` field is **REQUIRED** for each trigger
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

### Single Database Example

Here's a complete SQL Statements file for a simple e-commerce application using one database:

```json
{
  "version": 1,
  "databases": [
    {
      "database": "ecommerce_db",
      "isDefault": true
    }
  ],

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

### Using the Single Database Example

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

## Multi-Database Examples

### Example 1: E-commerce with Separate Analytics

This example separates transactional data from analytics data for better performance and organization:

```json
{
  "version": 1,
  "databases": [
    {
      "database": "ecommerce",
      "isDefault": true
    },
    {
      "database": "analytics",
      "isDefault": false
    }
  ],

  "insert": [
    {
      "Statement Name": "insertProduct",
      "Table Name": "Products",
      "Statement": "INSERT INTO Products (name, price, stock) VALUES (@name, @price, @stock)"
    },
    {
      "Statement Name": "insertOrder",
      "Table Name": "Orders",
      "Statement": "INSERT INTO Orders (customer_id, total_amount) VALUES (@customer_id, @total_amount)"
    },
    {
      "Statement Name": "logPageView",
      "Table Name": "PageViews",
      "Statement": "INSERT INTO PageViews (page_url, user_id, timestamp) VALUES (@page_url, @user_id, @timestamp)"
    }
  ],

  "select": [
    {
      "Statement Name": "getProducts",
      "Table Name": "Products",
      "Statement": "SELECT * FROM Products WHERE stock > 0"
    },
    {
      "Statement Name": "getPageViewStats",
      "Table Name": "PageViews",
      "Statement": "SELECT page_url, COUNT(*) as view_count FROM PageViews WHERE timestamp > @since GROUP BY page_url ORDER BY view_count DESC"
    }
  ],

  "trigger": [
    {
      "Database": "ecommerce",
      "Table Name": "Orders",
      "Statement": "CREATE TRIGGER log_order_creation AFTER INSERT ON Orders BEGIN INSERT INTO OrderAudit (order_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
    },
    {
      "Database": "analytics",
      "Table Name": "PageViews",
      "Statement": "CREATE TRIGGER update_view_count AFTER INSERT ON PageViews BEGIN UPDATE PageStats SET total_views = total_views + 1 WHERE page_url = NEW.page_url; END;"
    }
  ]
}
```

**Entity Classes:**

```csharp
// Default database (ecommerce) - Database attribute not required
[Table(IsColumnAttributeRequired = false)]
public class Product : SxmEntity
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
}

// Analytics database - Must specify Database attribute
[Table(Database = "analytics", IsColumnAttributeRequired = false)]
public class PageView : SxmEntity
{
    public string PageUrl { get; set; }
    public long? UserId { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Usage:**

```csharp
// Initialize at startup (MAUI)
await using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
await SxmDatabase.InitializeAsync(stream);
await SxmDatabase.RegisterEntitiesAsync(
    typeof(Product),
    typeof(Order),
    typeof(PageView)
);

// Work with default database (ecommerce)
var product = new Product 
{ 
    Name = "Widget", 
    Price = 19.99m, 
    Stock = 100 
};
await product.SaveAsync();  // Automatically goes to ecommerce.db

// Work with analytics database
var pageView = new PageView
{
    PageUrl = "/products",
    UserId = 123,
    Timestamp = DateTime.UtcNow
};
await pageView.SaveAsync();  // Automatically goes to analytics.db (via Database attribute)

// Query default database
using (var db = new SxmLinqDbContext())
{
    var inStockProducts = db.GetTable<Product>()
        .Where(p => p.Stock > 0)
        .ToList();
}

// Query analytics database explicitly
using (var db = new SxmLinqDbContext("analytics"))
{
    var recentViews = db.GetTable<PageView>()
        .Where(pv => pv.Timestamp > DateTime.UtcNow.AddHours(-1))
        .ToList();
}
```

### Example 2: Multi-Tenant Application

This example uses separate databases for different tenants:

```json
{
  "version": 1,
  "databases": [
    {
      "database": "system",
      "isDefault": true
    },
    {
      "database": "tenant_acme",
      "isDefault": false
    },
    {
      "database": "tenant_contoso",
      "isDefault": false
    }
  ],

  "insert": [
    {
      "Statement Name": "insertTenant",
      "Table Name": "Tenants",
      "Statement": "INSERT INTO Tenants (name, database_name, created_at) VALUES (@name, @database_name, @created_at)"
    },
    {
      "Statement Name": "insertUser",
      "Table Name": "Users",
      "Statement": "INSERT INTO Users (username, email, tenant_id) VALUES (@username, @email, @tenant_id)"
    }
  ],

  "select": [
    {
      "Statement Name": "getTenantByName",
      "Table Name": "Tenants",
      "Statement": "SELECT * FROM Tenants WHERE name = @name LIMIT 1"
    },
    {
      "Statement Name": "getTenantUsers",
      "Table Name": "Users",
      "Statement": "SELECT * FROM Users WHERE tenant_id = @tenant_id"
    }
  ],

  "trigger": [
    {
      "Database": "system",
      "Table Name": "Tenants",
      "Statement": "CREATE TRIGGER audit_tenant_changes AFTER UPDATE ON Tenants BEGIN INSERT INTO TenantAudit (tenant_id, action, timestamp) VALUES (NEW.id, 'updated', CURRENT_TIMESTAMP); END;"
    },
    {
      "Database": "tenant_acme",
      "Table Name": "Users",
      "Statement": "CREATE TRIGGER log_user_activity AFTER INSERT ON Users BEGIN INSERT INTO UserActivityLog (user_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
    },
    {
      "Database": "tenant_contoso",
      "Table Name": "Users",
      "Statement": "CREATE TRIGGER log_user_activity AFTER INSERT ON Users BEGIN INSERT INTO UserActivityLog (user_id, action, timestamp) VALUES (NEW.id, 'created', CURRENT_TIMESTAMP); END;"
    }
  ]
}
```

**Entity Classes:**

```csharp
// System database (default)
[Table(IsColumnAttributeRequired = false)]
public class Tenant : SxmEntity
{
    public string Name { get; set; }
    public string DatabaseName { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Tenant-specific entities
[Table(Database = "tenant_acme", IsColumnAttributeRequired = false)]
public class AcmeUser : SxmEntity
{
    public string Username { get; set; }
    public string Email { get; set; }
    public long TenantId { get; set; }
}

[Table(Database = "tenant_contoso", IsColumnAttributeRequired = false)]
public class ContosoUser : SxmEntity
{
    public string Username { get; set; }
    public string Email { get; set; }
    public long TenantId { get; set; }
}
```

**Usage:**

```csharp
// System operations (default database)
var tenant = new Tenant
{
    Name = "Acme Corp",
    DatabaseName = "tenant_acme",
    CreatedAt = DateTime.UtcNow
};
await tenant.SaveAsync();  // Goes to system.db

// Tenant-specific operations
var acmeUser = new AcmeUser
{
    Username = "john.doe",
    Email = "john@acmecorp.com",
    TenantId = tenant.id
};
await acmeUser.SaveAsync();  // Goes to tenant_acme.db

var contosoUser = new ContosoUser
{
    Username = "jane.smith",
    Email = "jane@contoso.com",
    TenantId = 2
};
await contosoUser.SaveAsync();  // Goes to tenant_contoso.db

// Query across databases
using (var systemDb = new SxmLinqDbContext("system"))
{
    var allTenants = systemDb.GetTable<Tenant>().ToList();
}

using (var acmeDb = new SxmLinqDbContext("tenant_acme"))
{
    var acmeUsers = acmeDb.GetTable<AcmeUser>().ToList();
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

### 8. **Multi-Database Organization**

**Option 1: Single File (Recommended for most apps)**

Use one SqlStatements file with a `databases` array:

```json
{
  "version": 1,
  "databases": [
    { "database": "main", "isDefault": true },
    { "database": "analytics", "isDefault": false },
    { "database": "cache", "isDefault": false }
  ],
  "insert": [ /* all statements */ ],
  "select": [ /* all statements */ ]
}
```

**Benefits:**
- ✅ Single source of truth
- ✅ Easier to maintain
- ✅ All databases initialized together
- ✅ Simpler deployment

**Option 2: Separate Files (For complex scenarios)**

Use separate files if databases are truly independent:

```
/Resources/Raw/
  ├── MainDatabase.json      (isDefault: true)
  ├── AnalyticsDatabase.json (isDefault: false)
  └── CacheDatabase.json     (isDefault: false)
```

**When to use separate files:**
- Different lifecycle/versioning needs
- Different teams own different databases
- Database files loaded dynamically at runtime

### 9. **Always Specify `version: 1`**

Include the version property for forward compatibility:

```json
{
  "version": 1,
  "database": "myapp_db",
  "isDefault": true
}
```

### 10. **Use Database Attribute for Non-Default Tables**

For multi-database scenarios, use the `[Table(Database = "...")]` attribute on entities:

```csharp
// Default database - no attribute needed
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity { }

// Non-default database - must specify
[Table(Database = "analytics", IsColumnAttributeRequired = false)]
public class PageView : SxmEntity { }
```

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

**Problem:** Missing `database` property in a database entry  
**Solution:** Ensure each database in the `databases` array has a name:

```json
{
  "version": 1,
  "databases": [
    {
      "database": "myapp_database",
      "isDefault": true
    }
  ]
}
```

### "No default database configured"

**Problem:** No database has `isDefault: true`  
**Solution:** Mark exactly one database as default:

```json
{
  "version": 1,
  "databases": [
    {
      "database": "myapp_database",
      "isDefault": true
    }
  ]
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
- **Multi-Database Support:** Added in version 1.0
- **Last Updated:** January 2025
- **Compatibility:** .NET 8, .NET 9, .NET MAUI

---

## Summary

The SqlStatements file is a powerful configuration tool that:
- Defines one or more databases for your application
- Registers named SQL statements for reuse throughout your code
- Supports both single-database and multi-database scenarios
- Enables clean separation between SQL and application logic
- Works seamlessly with SQLiteXM's LINQ and entity APIs

For multi-database applications, use the `databases` array format and mark entities with the `[Table(Database = "...")]` attribute for non-default databases.
