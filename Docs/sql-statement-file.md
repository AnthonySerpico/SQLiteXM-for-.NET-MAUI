# SQL Statements File Guide

## Overview

The SQL Statements file, typically named `SqlStatements.json`, is SQLiteXM's declarative database-definition and named-SQL statement
repository. It defines the databases used by the application and where reusable named SQL statements and database triggers are defined.

This file is required by SQLiteXM and must be placed in your application's `Resources/Raw` folder. 
The `Build Action` must be set to `MauiAsset`. The SqlStatements.json file is processed during database initialization.

**Note:** Database configurations (folder paths, PRAGMA settings, connection settings, etc.) are specified separately via `SxmDatabaseOptions` during initialization, not in this file.

---

## File Structure

The SQL Statements file is a JSON document containing arrays for database definitions and each supported SQL statement type.

### Basic Structure

```json
{
  "databases": [ /* Database definitions */ ],

  "insert":  [ /* INSERT statements  */ ],
  "select":  [ /* SELECT statements  */ ],
  "update":  [ /* UPDATE statements  */ ],
  "delete":  [ /* DELETE statements  */ ],
  "trigger": [ /* TRIGGER statements */ ]
}
```

Below is a minimum valid `SqlStatements.json` file.

```json
{
  "databases": [ 
    {
      "database": "MainDb",
      "isDefault": true
    }
  ]
}
```

This example defines:

* One database
* The database name is `MainDb`
* It is marked as the default database
* There are no SQL statements defined yet

This instructs SQLiteXM to create a SQLite database file named `MainDb` in the application's data folder.

### Database definition rules
- There must be at least one database defined in your `SqlStatements.json` file
- Exactly one database must be marked as the default database
- You can define as many non-default databases as needed

Most SQLiteXM applications use a single database. However, SQLiteXM also supports applications that need to organize data across 
multiple databases.

For details, see  ➡️ [Multi-Database Configuration](./multiple-databases.md)

### Multi-Database Example

For multiple databases, add more entries to the `databases` array:

```json
{
  "databases": 
  [
	{
	  "database": "MainDb",
	  "isDefault": true
	},
	{
	  "database": "AnalyticsDb",
	  "isDefault": false
	},
	{
	  "database": "CacheDb",
	  "isDefault": false
	}
  ]
}
```

---

### **📌 Default Database Purpose** 
You might ask , "Why is it necessary to mark one database as the default?" The default database is used when a SQLiteXM API provides 
an optional database name and no database name is supplied. In that case, SQLiteXM executes the operation against the default database. 
This provides a convenient API for applications that use a single database—the most common scenario—without requiring the database name 
to be specified on every call.

---

### Registering SQL Statements

The SQL Statements file contains five arrays where you define the different SQL statement types:

- **`insert`** - INSERT statements
- **`select`** - SELECT statements
- **`update`** - UPDATE statements
- **`delete`** - DELETE statements
- **`trigger`** - TRIGGER definitions


Below is an example definition for an INSERT statement, which would be placed inside the `insert` array.

```json
{
  "insert": [
	{
	  "Statement Name": "insertUser",
	  "Table Name": "Users",
	  "Statement": "INSERT INTO Users (username, email, created_at) VALUES (@username, @email, @created_at)"
	}
  ]
}
```

INSERT, SELECT, UPDATE, and DELETE statements all use the same three fields for their definitions:
- **Statement Name** - Unique identifier for the statement

- **Table Name** - The target table

- **Statement** - The actual SQL code

Named SQL statements are not associated with a specific database in `SqlStatements.json`. A named statement can be executed against 
any configured database. When calling a SQL execution API such as `RunStatementAsync`, the application specifies the database 
against which the statement should execute.

### Parameterized Statements

SQLiteXM supports two parameter styles for named SQL statements:

- **Named parameters** — parameters such as `@email` and `@user_id`.
- **Positional parameters** — parameters such as `@p0`, `@p1`, and so on.

SQL statements that include parameters can use either named parameters, as in the example above 
(e.g., `@username`, `@email`, `@created_at`), or positional parameters (e.g., `@p0`, `@p1`, etc.).
SQLiteXM supports both parameter styles, but you cannot mix named and positional parameters within the same statement.

---
### Statement Definition Reference

| Statement Type | JSON Array | Required Fields | Optional Fields | Purpose |
|---|---|---|---|---|
| INSERT | `insert` | `Statement Name`, `Table Name`, `Statement` | — | Registers an INSERT statement |
| SELECT | `select` | `Statement Name`, `Table Name`, `Statement` | — | Registers a SELECT statement |
| UPDATE | `update` | `Statement Name`, `Table Name`, `Statement` | — | Registers an UPDATE statement |
| DELETE | `delete` | `Statement Name`, `Table Name`, `Statement` | — | Registers a DELETE statement |
| TRIGGER | `trigger` | `Table Name`, `Statement` | `Database` | Registers a SQLite trigger definition |

`Database` is required for trigger definitions when more than one database is defined. When only one database exists, it may be omitted.

### INSERT Statements Example

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

---

### SELECT Statements Example

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

---

### UPDATE Statements Example

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

---

### DELETE Statements Example

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

---

### TRIGGER Statements

Below is an example definition for a TRIGGER, which would be placed inside the `trigger` array.

```json
{
   "trigger": [
     {
       "Database": "MainDb",
       "Table Name": "user",
       "Statement": "CREATE TRIGGER updateCustomer AFTER INSERT ON user BEGIN INSERT INTO customer (name, address) VALUES (new.name, new.address); END;"
     }
   ]
}
```
Trigger definitions follow a different identification and configuration pattern from INSERT, SELECT, UPDATE, and DELETE statements. 
Unlike named SQL statements, triggers do not have a Statement Name.

- **Database** - The name of the database where the trigger should run. This is required when more than one database is defined. 
When only one database is defined, you can simply remove or omit the `"Database": "xxx"` field entirely.

- **Table Name** - This is the table name immediately after the `ON` keyword in the trigger statement. In SQL, this is known as the trigger table.

- **Statement** - The actual trigger statement

When using ONLY one database, triggers **do not require** a `Database` field:

```json
{
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

When using multiple databases, each trigger definition **must include** a `Database` field:

```json
{
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
---

## Complete Example

### Single Database Example

Here's a complete SQL Statements file for a simple e-commerce application using one database:

```json
{
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
---

## Multi-Database Examples

### Example 1: E-commerce with Separate Analytics

This example separates transactional data from analytics data for better organization:

```json
{
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

### 5. **Document Complex Queries**

Use clear statement names and table names to document intent:

```json
{
  "Statement Name": "getCustomersWithRecentOrders",
  "Table Name": "Customers",
  "Statement": "SELECT c.*, COUNT(o.id) as recent_order_count FROM Customers c LEFT JOIN Orders o ON c.id = o.customer_id AND o.order_date > @cutoff_date GROUP BY c.id HAVING recent_order_count > 0"
}
```

### 6. **Keep Statements File in Version Control**

The SQL Statements file is configuration, so:
- ✅ Commit it to Git
- ✅ Review changes in PRs
- ✅ Document breaking changes
- ✅ Version it alongside your code


---

### File Placement

Place the SQL statement file in your MAUI project's `Resources/Raw` folder:

```
MyMauiApp/
├── Resources/
│   └── Raw/
│       └── SqlStatements.json  ← Here
├── App.xaml.cs
└── MauiProgram.cs
└── etc...
```

Ensure the file is configured as a `MauiAsset`.

### Loading from Raw Assets

```csharp
using Stream stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
await SxmDatabase.InitializeAsync(stream, databaseOptions);
```

### Summary

The `SqlStatements.json` file is SQLiteXM's declarative database and SQL definition file. It:

- Defines one or more databases used by the application
- Identifies one database as the default database
- Registers reusable named INSERT, SELECT, UPDATE, and DELETE statements
- Defines SQLite triggers and associates them with their database and trigger table
- Supports both single-database and multi-database applications
- Keeps SQL definitions separate from application code
- Works with SQLiteXM's SQL APIs and database initialization process