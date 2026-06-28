# Direct SQL Query Support in SQLiteXM

SQLiteXM provides comprehensive support for executing raw SQL queries directly in your code. This guide explains how to use direct SQL queries, from simple examples to complex transaction scenarios.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Basic SELECT Queries](#basic-select-queries)
- [INSERT Statements](#insert-statements)
- [UPDATE Statements](#update-statements)
- [DELETE Statements](#delete-statements)
- [Working with Parameters](#working-with-parameters)
- [Transactions](#transactions)
- [Best Practices](#best-practices)

---

## Quick Start

SQLiteXM allows you to execute SQL queries in two ways:

1. **Named Statements** - SQL queries defined in `SqlStatements.json` and referenced by name
2. **Direct SQL** - SQL queries written directly in your code

This guide focuses on **Direct SQL** queries.


### Note:

> 💡SQLiteXM executes the SQL statement exactly as provided and supports the full SQL capabilities of the underlying SQLite engine. The examples in this guide demonstrate common parameter and result-mapping patterns, but any valid SQLite SQL statement may be used. This includes joins, subqueries, common table expressions (CTEs), aggregate functions, window functions, views, UNION queries, and other SQLite-supported SQL features.


---

## Basic SELECT Queries

> **💡 About DTOs:** Throughout this guide, we use the term **DTO (Data Transfer Object)** to refer to simple classes that hold data. These are plain C# classes with properties, used to pass parameters to queries or receive query results. You can also use your entity classes (classes that inherit from `SxmEntity`) in the same way.

> **💡 Database Selection:** All `SelectAsync` methods accept an optional third parameter `dbName` (type `string?`). If provided, the query executes on the specified database; if omitted, it runs on the default database defined in your `SqlStatements.json` configuration.


### SELECT with Dictionary of Named Parameters Returning a List of DTOs

For ad-hoc or dynamic queries, you can use a dictionary for the select parameters while still getting strongly-typed results. This is very common when building search filters dynamically:

DTO result types do not need to inherit from SxmEntity. Any public class with writable properties that match the selected column names can be used.

```csharp
public class UserDto
{
    public int id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Country { get; set; }
}

string sql = "SELECT id, Name, Age, Country FROM Users WHERE Age > @minAge AND Country = @country";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 },
    { "country", "USA" }
};

// Get strongly-typed results (recommended - best of both worlds)
List<UserDto> users = await SxmStatement.SelectAsync<UserDto>(sql, parameters);

foreach (UserDto user in users)
{
    Console.WriteLine($"{user.Name} is {user.Age} years old");
}

// To query a specific database (not the default), pass the database name:
List<UserDto> usersFromArchive = await SxmStatement.SelectAsync<UserDto>(sql, parameters, "ArchiveDB");
```
Result columns are matched to DTO properties by name.
Columns that do not have a matching property are ignored.
Properties without a matching column retain their default value.

**This pattern is ideal when:**
- Building search filters dynamically based on user input
- Parameters come from a configuration file or external source
- You want flexibility in parameters but type safety in results
- Working with variable/optional query conditions

**Alternative 1 - Select with Dictionary of Named Parameters Returning a list of Dictionary results:**
```csharp
// If you need dictionary results instead
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, parameters);

foreach (Dictionary<string, object?> row in results)
{
    string name = (string)row["Name"]!;
    int age = (int)(long)row["Age"]!;

    Console.WriteLine($"{name} is {age} years old");
}
```


**Alternative 2 - SELECT with DTO Parameter returning a List of DTOs**

The most natural way to query in SQLiteXM is using DTOs for both parameters and results. This provides full type safety:

The DTO input type does not need to inherit from SxmEntity. Any public class with readable properties that match the parameter names can be used.

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

string sql = "SELECT id, Name, Age, Country FROM Users WHERE Age > @minAge AND Country = @country";
UserSearchParams parameters = new UserSearchParams 
{ 
    minAge = 18, 
    country = "USA" 
};

List<UserDto> users = await SxmStatement.SelectAsync<UserSearchParams, UserDto>(sql, parameters);

foreach (UserDto user in users)
{
    Console.WriteLine($"{user.Name} (Age {user.Age}) from {user.Country}");
}
```

**Key Points:**
- Parameter class properties map to SQL parameter names (without `@`)
- Result class properties map to SQL column names
- Property names are case-sensitive
- Fully type-safe with IntelliSense support
- Natural pattern for entity-based ORMs like SQLiteXM


**Alternative 3 - SELECT with DTO Parameter returning a list of Dictionary results**

You can also combine a strongly-typed DTO parameter with dictionary results. This is useful when you have a known parameter structure but need flexible result handling:

```csharp
public class UserSearchParams
{
    public int minAge { get; set; }
    public string? country { get; set; }
}

string sql = "SELECT id, Name, Age, Country FROM Users WHERE Age > @minAge AND Country = @country";
UserSearchParams parameters = new UserSearchParams 
{ 
    minAge = 18, 
    country = "USA" 
};

List<Dictionary<string, object?>> users = await SxmStatement.SelectAsync<UserSearchParams>(sql, parameters);

foreach (Dictionary<string, object?> user in users)
{
    string name = (string)user["Name"]!;
    int age = (int)user["Age"]!;
    Console.WriteLine($"{name} is {age} years old");
}
```

**This pattern is ideal when:**
- You have a well-defined parameter structure (DTO)
- Result columns vary or are determined at runtime
- You need to dynamically access different columns
- Working with generic data processing logic

### SELECT with Positional Parameters

For simple queries with few parameters, you can use positional parameters:

```csharp
string sql = "SELECT id, Name, Age FROM Users WHERE Age > @p0";
List<object> parameters = new List<object> { 18 };

// Get typed results (recommended)
List<UserDto> users = await SxmStatement.SelectAsync<UserDto>(sql, parameters);

foreach (UserDto user in users)
{
    Console.WriteLine($"{user.Name} is {user.Age} years old");
}

// Or get dictionary results
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, parameters);

foreach (Dictionary<string, object?> row in results)
{
    int id = (int)(long)row["id"];
    string name = (string)row["Name"]!;
    int age = (int)(long)row["Age"];

    Console.WriteLine($"{name} is {age} years old");
}
```

**Important Notes:**
- When using dictionary results, SQLite integer values are typically returned as long. Cast accordingly when reading values from the dictionary.
- Use `@p0`, `@p1`, `@p2`, etc. for positional parameters
- Easy to mix up parameter order with many parameters

### Query with No Parameters

If your query doesn't need parameters, pass an empty list:

```csharp
string sql = "SELECT id, Name, Age FROM Users";

// Get typed results
List<UserDto> users = await SxmStatement.SelectAsync<UserDto>(sql, new List<object>());

// Or get dictionary results
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, new List<object>());
```

---

## INSERT Statements

> **💡 Database Selection:** All `InsertAsync` methods accept an optional third parameter `dbName` (type `string?`). If provided, the insert executes on the specified database; if omitted, it runs on the default database defined in your `SqlStatements.json` configuration.

### INSERT with DTO Parameter Returning a Typed Result

The most natural way to insert records in SQLiteXM is using a DTO for both parameters and results. This provides full type safety and makes it easy to work with the inserted record:

DTO parameter types do not need to inherit from SxmEntity. Any public class with readable properties that match the parameter names can be used.

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
}

public class User : SxmEntity
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? Email { get; set; }
}

string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@name, @age, @email) RETURNING *";
NewUserParams parameters = new NewUserParams 
{ 
    name = "Alice Johnson", 
    age = 28, 
    email = "alice@example.com" 
};

User newUser = await SxmStatement.InsertAsync<NewUserParams, User>(sql, parameters);
Console.WriteLine($"Created user with ID: {newUser.id}, Name: {newUser.Name}");

// To insert into a specific database (not the default), pass the database name:
User archivedUser = await SxmStatement.InsertAsync<NewUserParams, User>(sql, parameters, "ArchiveDB");
```

The inserted record is automatically mapped to your result class.
Use `RETURNING *` to get the complete inserted record including auto-generated columns like `id`.

**This pattern is ideal when:**
- You want type safety for both input parameters and returned results
- You need to work with the inserted record immediately after insertion
- You're building reusable data access methods
- You prefer IntelliSense and compile-time checking

**Key Points:**
- Parameter class properties map to SQL parameter names (without `@`)
- Result class properties map to returned column names
- Property names are case-sensitive
- Fully type-safe with IntelliSense support
- Natural pattern for entity-based ORMs like SQLiteXM

**Alternative 1 - INSERT with DTO Parameter Returning a Dictionary**

If you need flexible result handling but still want typed parameters:

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
}

string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@name, @age, @email) RETURNING *";
NewUserParams parameters = new NewUserParams 
{ 
    name = "Bob Wilson", 
    age = 35, 
    email = "bob@example.com" 
};

Dictionary<string, object?> insertedRow = await SxmStatement.InsertAsync<NewUserParams>(sql, parameters);
int newId = (int)(long)insertedRow["id"];
string name = (string)insertedRow["Name"]!;
Console.WriteLine($"Inserted user {name} with ID: {newId}");
```

**This pattern is ideal when:**
- You have a well-defined parameter structure (DTO)
- Result columns vary or are determined at runtime
- You need to dynamically access different columns
- Working with generic data processing logic


### INSERT with Dictionary Parameters

For ad-hoc or dynamic insertion scenarios, you can use a dictionary for the parameters:

```csharp
string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@name, @age, @email) RETURNING *";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name", "Charlie Brown" },
    { "age", 42 },
    { "email", "charlie@example.com" }
};

// Get strongly-typed result (recommended - best of both worlds)
User newUser = await SxmStatement.InsertAsync<User>(sql, parameters);
Console.WriteLine($"Inserted: {newUser.Name}, ID: {newUser.id}");
```

**This pattern is ideal when:**
- Building dynamic insert operations based on user input
- Parameters come from a configuration file or external source
- You want flexibility in parameters but type safety in results
- Working with variable/optional insertion fields

**Alternative - Dictionary parameters with dictionary result:**
```csharp
// If you need dictionary results instead
Dictionary<string, object?> insertedRow = await SxmStatement.InsertAsync(sql, parameters);
int newId = (int)(long)insertedRow["id"];
string email = (string)insertedRow["Email"]!;
```


### INSERT with Positional Parameters

For simple inserts with few parameters, you can use positional parameters:

```csharp
string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@p0, @p1, @p2) RETURNING *";
List<object> parameters = new List<object> { "John Doe", 30, "john@example.com" };

// Get typed result (recommended)
User newUser = await SxmStatement.InsertAsync<User>(sql, parameters);
Console.WriteLine($"Inserted: {newUser.Name}, ID: {newUser.id}");

// Or get dictionary result
Dictionary<string, object?> insertedRow = await SxmStatement.InsertAsync(sql, parameters);
int newId = (int)(long)insertedRow["id"];
string name = (string)insertedRow["Name"]!;
```

**Important Notes:**
- When using dictionary results, SQLite integer values are typically returned as long. Cast accordingly when reading values from the dictionary.
- Use `@p0`, `@p1`, `@p2`, etc. for positional parameters
- Easy to mix up parameter order with many parameters

**When to use positional parameters:**
- Very simple inserts with 1-3 parameters
- Quick prototyping or testing
- When parameter names don't add clarity

**Disadvantages:**
- Easy to mix up parameter order
- Less readable with many parameters
- No compile-time checking

### INSERT with No RETURNING Clause

If your SQL doesn't include `RETURNING *`, you can still insert but won't get the generated ID or inserted values back. This is rarely recommended:

```csharp
string sql = "INSERT INTO Users (Name, Age) VALUES (@p0, @p1)";
Dictionary<string, object?> result = await SxmStatement.InsertAsync(sql, new List<object> { "Jane", 25 });
// result will be empty - you won't know the generated ID
```

**Best Practice:** Always use `RETURNING *` to get the inserted record with its auto-generated ID.

> **💡 Inserting Multiple Records:** If you need to insert multiple records efficiently and safely, see the [Transactions](#transactions) section for examples of bulk inserts with proper atomicity guarantees.

---

## UPDATE Statements

> **💡 Database Selection:** All `UpdateAsync` methods accept an optional third parameter `dbName` (type `string?`). If provided, the update executes on the specified database; if omitted, it runs on the default database defined in your `SqlStatements.json` configuration.

> **💡 Return Value:** `UpdateAsync` returns `Task` (no return value). Unlike `SelectAsync` or `InsertAsync`, UPDATE operations don't return data. Use a separate SELECT query if you need to retrieve the updated records.

### UPDATE with DTO Parameters

The most natural way to update records in SQLiteXM is using a DTO. Your class properties map directly to SQL parameters, providing type safety and maintainability:

DTO parameter types do not need to inherit from SxmEntity. Any public class with readable properties that match the parameter names can be used.

```csharp
public class UpdateUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
    public int id { get; set; }
}

string sql = "UPDATE Users SET Name = @name, Age = @age, Email = @email WHERE id = @id";
UpdateUserParams parameters = new UpdateUserParams 
{ 
    name = "Alice Updated", 
    age = 29, 
    email = "alice.new@example.com",
    id = 1
};

await SxmStatement.UpdateAsync<UpdateUserParams>(sql, parameters);
Console.WriteLine("User updated successfully");

// To update in a specific database (not the default), pass the database name:
await SxmStatement.UpdateAsync<UpdateUserParams>(sql, parameters, "ArchiveDB");
```

**This pattern is ideal when:**
- You want type safety for input parameters
- You're building reusable data access methods
- You have multiple updates that share the same parameter structure
- You prefer IntelliSense and compile-time checking
- Working with well-defined update operations

**Key Points:**
- Parameter class properties map to SQL parameter names (without `@`)
- Property names are case-sensitive
- Fully type-safe with IntelliSense support
- Natural pattern for entity-based ORMs like SQLiteXM
- Returns `Task` - no return value


### UPDATE with Dictionary Parameters

For ad-hoc or dynamic update scenarios, you can use a dictionary for the parameters:

```csharp
string sql = "UPDATE Users SET Name = @name, Age = @age, Email = @email WHERE id = @id";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name", "Bob Updated" },
    { "age", 36 },
    { "email", "bob.new@example.com" },
    { "id", 2 }
};

await SxmStatement.UpdateAsync(sql, parameters);
Console.WriteLine("User updated successfully");
```

**This pattern is ideal when:**
- Building dynamic update operations based on user input
- Parameters come from a configuration file or external source
- You need flexibility in which fields to update
- Working with variable/optional update fields
- Update structure isn't known at compile time

**Example - Conditional Updates:**
```csharp
string sql = "UPDATE Users SET Name = @name, Age = @age WHERE id = @id";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name", updatedName },
    { "age", updatedAge },
    { "id", userId }
};

await SxmStatement.UpdateAsync(sql, parameters);
```


### UPDATE with Positional Parameters

For simple updates with few parameters, you can use positional parameters:

```csharp
string sql = "UPDATE Users SET Age = @p0, Email = @p1 WHERE id = @p2";
List<object> parameters = new List<object> { 31, "john.updated@example.com", 1 };

await SxmStatement.UpdateAsync(sql, parameters);
Console.WriteLine("User updated successfully");
```

**Important Notes:**
- Use `@p0`, `@p1`, `@p2`, etc. for positional parameters
- Easy to mix up parameter order with many parameters
- Returns `Task` (void) - no confirmation of rows affected

**When to use positional parameters:**
- Very simple updates with 1-3 parameters
- Quick prototyping or testing
- When parameter names don't add clarity

**Disadvantages:**
- Easy to mix up parameter order
- Less readable with many parameters
- No compile-time checking

### UPDATE Multiple Rows

Update operations can affect multiple rows at once. The same parameter patterns apply:

**With DTO:**
```csharp
public class BulkUpdateParams
{
    public bool isActive { get; set; }
    public DateTime lastUpdated { get; set; }
    public int minAge { get; set; }
}

string sql = "UPDATE Users SET IsActive = @isActive, LastUpdated = @lastUpdated WHERE Age < @minAge";
BulkUpdateParams parameters = new BulkUpdateParams
{
    isActive = false,
    lastUpdated = DateTime.UtcNow,
    minAge = 18
};

await SxmStatement.UpdateAsync<BulkUpdateParams>(sql, parameters);
// All users with Age < 18 are now updated
```

**With Dictionary:**
```csharp
string sql = "UPDATE Users SET IsActive = @active WHERE LastLoginAt < @cutoffDate";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "active", false },
    { "cutoffDate", DateTime.UtcNow.AddDays(-30) }
};

await SxmStatement.UpdateAsync(sql, parameters);
```

**With Positional Parameters:**
```csharp
string sql = "UPDATE Products SET Stock = Stock + @p0 WHERE Category = @p1";
await SxmStatement.UpdateAsync(sql, new List<object> { 100, "Electronics" });
```

### UPDATE with No Parameters

If your update doesn't need parameters (rare), pass an empty list:

```csharp
string sql = "UPDATE Settings SET LastResetDate = datetime('now')";
await SxmStatement.UpdateAsync(sql, new List<object>());
```

---

## DELETE Statements

> **💡 Database Selection:** All `DeleteAsync` methods accept an optional third parameter `dbName` (type `string?`). If provided, the delete executes on the specified database; if omitted, it runs on the default database defined in your `SqlStatements.json` configuration.

> **💡 Return Value:** `DeleteAsync` returns `Task` (no return value). Unlike `SelectAsync` or `InsertAsync`, DELETE operations don't return data. Use a separate SELECT query before deleting if you need to retrieve the records first.

### DELETE with DTO Parameters

The most natural way to delete records in SQLiteXM is using a DTO. Your class properties map directly to SQL parameters, providing type safety and clarity:

DTO parameter types do not need to inherit from SxmEntity. Any public class with readable properties that match the parameter names can be used.

```csharp
public class DeleteUserParams
{
    public int id { get; set; }
    public bool confirmDelete { get; set; }
}

string sql = "DELETE FROM Users WHERE id = @id AND @confirmDelete = 1";
DeleteUserParams parameters = new DeleteUserParams 
{ 
    id = 5,
    confirmDelete = true
};

await SxmStatement.DeleteAsync<DeleteUserParams>(sql, parameters);
Console.WriteLine("User deleted successfully");

// To delete from a specific database (not the default), pass the database name:
await SxmStatement.DeleteAsync<DeleteUserParams>(sql, parameters, "ArchiveDB");
```

**This pattern is ideal when:**
- You want type safety for input parameters
- You're building reusable data access methods
- You have multiple deletes that share the same parameter structure
- You prefer IntelliSense and compile-time checking
- Working with well-defined delete operations

**Key Points:**
- Parameter class properties map to SQL parameter names (without `@`)
- Property names are case-sensitive
- Fully type-safe with IntelliSense support
- Natural pattern for entity-based ORMs like SQLiteXM
- Returns `Task` - no return value


### DELETE with Dictionary Parameters

For ad-hoc or dynamic delete scenarios, you can use a dictionary for the parameters:

```csharp
string sql = "DELETE FROM Users WHERE Age < @minAge AND IsActive = @active";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 },
    { "active", false }
};

await SxmStatement.DeleteAsync(sql, parameters);
Console.WriteLine("Inactive underage users deleted");
```

**This pattern is ideal when:**
- Building dynamic delete operations based on user input
- Parameters come from a configuration file or external source
- You need flexibility in delete criteria
- Working with variable/optional conditions
- Delete criteria isn't known at compile time

**Example - Conditional Deletes:**
```csharp
string sql = "DELETE FROM Orders WHERE UserId = @userId AND Status = @status";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "userId", targetUserId },
    { "status", "cancelled" }
};

await SxmStatement.DeleteAsync(sql, parameters);
```


### DELETE with Positional Parameters

For simple deletes with few parameters, you can use positional parameters:

```csharp
string sql = "DELETE FROM Users WHERE id = @p0";
List<object> parameters = new List<object> { 5 };

await SxmStatement.DeleteAsync(sql, parameters);
Console.WriteLine("User deleted successfully");
```

**Important Notes:**
- Use `@p0`, `@p1`, `@p2`, etc. for positional parameters
- Easy to mix up parameter order with many parameters
- Returns `Task` (void) - no confirmation of rows affected
- Always use parameters to prevent SQL injection

**When to use positional parameters:**
- Very simple deletes with 1-2 parameters
- Quick prototyping or testing
- When parameter names don't add clarity

**Disadvantages:**
- Easy to mix up parameter order
- Less readable with many parameters
- No compile-time checking

### DELETE Multiple Rows

Delete operations can affect multiple rows at once. The same parameter patterns apply:

**With DTO:**
```csharp
public class BulkDeleteParams
{
    public DateTime cutoffDate { get; set; }
    public string status { get; set; }
}

string sql = "DELETE FROM Logs WHERE CreatedAt < @cutoffDate AND Status = @status";
BulkDeleteParams parameters = new BulkDeleteParams
{
    cutoffDate = DateTime.UtcNow.AddDays(-90),
    status = "processed"
};

await SxmStatement.DeleteAsync<BulkDeleteParams>(sql, parameters);
// All processed logs older than 90 days are now deleted
```

**With Dictionary:**
```csharp
string sql = "DELETE FROM TempData WHERE ExpiresAt < @now";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "now", DateTime.UtcNow }
};

await SxmStatement.DeleteAsync(sql, parameters);
```

**With Positional Parameters:**
```csharp
string sql = "DELETE FROM Sessions WHERE LastAccessedAt < @p0";
await SxmStatement.DeleteAsync(sql, new List<object> { DateTime.UtcNow.AddHours(-24) });
```

### DELETE All Rows (Use with Caution)

To delete all rows from a table, pass an empty parameter list. **Use with extreme caution:**

```csharp
string sql = "DELETE FROM TempCache";
await SxmStatement.DeleteAsync(sql, new List<object>());
Console.WriteLine("All temp cache cleared");
```

**Warning:** This operation cannot be undone unless you're in a transaction. Always double-check your SQL and consider using a WHERE clause.

**Safer Alternative with Confirmation:**
```csharp
public class ClearTableParams
{
    public bool confirmClear { get; set; }
}

string sql = "DELETE FROM TempCache WHERE @confirmClear = 1 OR 1=0";
await SxmStatement.DeleteAsync<ClearTableParams>(sql, new ClearTableParams { confirmClear = true });
```

---

## Working with Parameters

### Positional Parameters

Use `@p0`, `@p1`, `@p2`, etc. with a `List<object>`:

```csharp
string sql = "SELECT * FROM Users WHERE Age > @p0 AND Country = @p1";
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(
    sql, 
    new List<object> { 18, "USA" }
);
```

**Advantages:**
- Simple for queries with few parameters
- Parameters are matched by position in the list

**Disadvantages:**
- Less readable with many parameters
- Easy to mix up parameter order

### Named Parameters

Use descriptive names with a `Dictionary<string, object?>`:

```csharp
string sql = "SELECT * FROM Users WHERE Age > @minAge AND Country = @country";
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "minAge", 18 },
    { "country", "USA" }
};

List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, parameters);
```

**Advantages:**
- More readable
- Self-documenting
- Harder to make mistakes

**Disadvantages:**
- Slightly more verbose

### Object/DTO Parameters

Instead of dictionaries, you can use a class or DTO to provide parameters. This is especially useful when you have multiple queries that share the same parameter structure:

```csharp
// Define a parameter class
public class UserSearchParams
{
    public int minAge { get; set; }
    public string? country { get; set; }
}

// Use it in your query
string sql = "SELECT * FROM Users WHERE Age > @minAge AND Country = @country";
UserSearchParams parameters = new UserSearchParams 
{ 
    minAge = 18, 
    country = "USA" 
};

// Option 1: Get results as dictionaries
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, parameters);

// Option 2: Get typed results directly
List<User> users = await SxmStatement.SelectAsync<UserSearchParams, User>(sql, parameters);
```

**Advantages:**
- Type-safe parameter definitions
- Reusable across multiple queries
- Better IntelliSense support
- Easier to refactor
- Self-documenting code

**Important Notes:**
- Property names must match SQL parameter names (without the `@` prefix)
- Property names are **case-sensitive**
- Works with SELECT, INSERT, UPDATE, and DELETE
- Also works inside transactions with `SxmSqlTransaction`

**Example with INSERT:**

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
}

string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@name, @age, @email) RETURNING *";
NewUserParams parameters = new NewUserParams 
{ 
    name = "Alice Johnson", 
    age = 28, 
    email = "alice@example.com" 
};

// Insert and get the new user back
User newUser = await SxmStatement.InsertAsync<NewUserParams, User>(sql, parameters);
Console.WriteLine($"Created user with ID: {newUser.id}");
```

**Example with Transactions:**

```csharp
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

string sql = "UPDATE Users SET Age = @age, Email = @email WHERE id = @id";
NewUserParams updateParams = new NewUserParams 
{ 
    age = 29, 
    email = "alice.updated@example.com",
    id = 1 
};

await transaction.UpdateAsync(sql, updateParams);
// Transaction commits automatically
```

### Parameter Types

SQLiteXM automatically handles type conversion for common types:

```csharp
List<object> parameters = new List<object>
{
    "string value",          // TEXT
    42,                      // INTEGER
    3.14,                    // REAL
    true,                    // INTEGER (1)
    DateTime.UtcNow,         // TEXT (ISO8601)
    new byte[] { 1, 2, 3 }   // BLOB
};
```

### Null Parameters

Pass `null` or `DBNull.Value` for NULL values:

```csharp
string sql = "INSERT INTO Users (Name, Email, Phone) VALUES (@p0, @p1, @p2) RETURNING *";
await SxmStatement.InsertAsync(sql, new List<object> 
{ 
    "John", 
    "john@example.com", 
    DBNull.Value  // Phone is NULL
});
```

---

## Transactions

For operations that must succeed or fail together, use transactions.

### Basic Transaction

```csharp
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

// Perform multiple operations
string insertSql = "INSERT INTO Users (Name, Age) VALUES (@p0, @p1) RETURNING *";
Dictionary<string, object?> user = await transaction.InsertAsync(insertSql, new List<object> { "Alice", 28 });

string updateSql = "UPDATE Settings SET LastUserId = @p0 WHERE id = @p1";
await transaction.UpdateAsync(updateSql, new List<object> { user["id"], 1 });

// Transaction commits automatically when disposed (if no errors occurred)
```

**Key Points:**
- Use `SxmSqlTransaction.Create()` to create a transaction
- The transaction commits automatically on `DisposeAsync()` if no errors occurred
- If an exception is thrown, the transaction rolls back automatically
- Always use `await using` to ensure proper disposal

### Explicit Commit

You can also commit explicitly:

```csharp
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

string sql1 = "INSERT INTO Orders (UserId, Total) VALUES (@p0, @p1) RETURNING *";
Dictionary<string, object?> order = await transaction.InsertAsync(sql1, new List<object> { 1, 99.99 });

string sql2 = "UPDATE Users SET OrderCount = OrderCount + 1 WHERE id = @p0";
await transaction.UpdateAsync(sql2, new List<object> { 1 });

// Explicitly commit (optional - happens automatically on dispose)
await transaction.CommitTransactionAsync();
```

### Transaction with Error Handling

```csharp
try
{
    await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

    // Operation 1
    string sql1 = "INSERT INTO Accounts (Name, Balance) VALUES (@p0, @p1) RETURNING *";
    await transaction.InsertAsync(sql1, new List<object> { "Savings", 1000 });

    // Operation 2
    string sql2 = "UPDATE Accounts SET Balance = Balance - @p0 WHERE Name = @p1";
    await transaction.UpdateAsync(sql2, new List<object> { 100, "Checking" });

    // Auto-commits on successful disposal
}
catch (Exception ex)
{
    // Transaction automatically rolls back on exception
    Console.WriteLine($"Transaction failed: {ex.Message}");
}
```

### Inserting Multiple Records with Transaction

When you need to insert multiple records, use a transaction for atomicity and performance. This example uses DTOs for clean, type-safe parameter handling:

```csharp
public class NewUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
}

// Prepare multiple records to insert
List<NewUserParams> users = new List<NewUserParams> 
{ 
    new NewUserParams { name = "John Doe", age = 30, email = "john@example.com" },
    new NewUserParams { name = "Jane Smith", age = 25, email = "jane@example.com" },
    new NewUserParams { name = "Bob Wilson", age = 35, email = "bob@example.com" }
};

string sql = "INSERT INTO Users (Name, Age, Email) VALUES (@name, @age, @email) RETURNING *";

await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

List<User> insertedUsers = new List<User>();

foreach (NewUserParams userParams in users)
{
    User newUser = await transaction.InsertAsync<NewUserParams, User>(sql, userParams);
    insertedUsers.Add(newUser);
}

// All inserts committed together - either all succeed or all fail
Console.WriteLine($"Successfully inserted {insertedUsers.Count} users");
```

**Benefits:**
- **Atomicity**: All inserts succeed or fail together
- **Performance**: Much faster than separate non-transactional inserts
- **Type Safety**: DTOs provide compile-time checking
- **Get IDs Back**: Collect all inserted records with their generated IDs

**Alternative with Dictionary Parameters:**

```csharp
List<Dictionary<string, object?>> userDicts = new List<Dictionary<string, object?>>
{
    new Dictionary<string, object?> { { "name", "John" }, { "age", 30 }, { "email", "john@example.com" } },
    new Dictionary<string, object?> { { "name", "Jane" }, { "age", 25 }, { "email", "jane@example.com" } }
};

await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

foreach (Dictionary<string, object?> userDict in userDicts)
{
    await transaction.InsertAsync(sql, userDict);
}
```

### Complex Transaction with Typed Results

```csharp
public class Order : SxmEntity
{
    public int UserId { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

// Insert order
string orderSql = "INSERT INTO Orders (UserId, Total, CreatedAt) VALUES (@p0, @p1, @p2) RETURNING *";
Order order = await transaction.InsertAsync<Order>(
    orderSql, 
    new List<object> { 1, 149.99, DateTime.UtcNow }
);

// Insert order items
string itemSql = "INSERT INTO OrderItems (OrderId, ProductId, Quantity) VALUES (@p0, @p1, @p2)";
await transaction.InsertAsync(itemSql, new List<object> { order.id, 101, 2 });
await transaction.InsertAsync(itemSql, new List<object> { order.id, 102, 1 });

// Update inventory
string inventorySql = "UPDATE Products SET Stock = Stock - @p0 WHERE id = @p1";
await transaction.UpdateAsync(inventorySql, new List<object> { 2, 101 });
await transaction.UpdateAsync(inventorySql, new List<object> { 1, 102 });

// Transaction commits automatically
```

### Transaction with SELECT

You can also query within transactions:

```csharp
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");

// Check current balance
string checkSql = "SELECT Balance FROM Accounts WHERE id = @p0";
List<Dictionary<string, object?>> results = await transaction.SelectAsync(checkSql, new List<object> { 1 });
decimal currentBalance = (decimal)results[0]["Balance"];

if (currentBalance >= 100)
{
    // Perform withdrawal
    string updateSql = "UPDATE Accounts SET Balance = Balance - @p0 WHERE id = @p1";
    await transaction.UpdateAsync(updateSql, new List<object> { 100, 1 });
}
else
{
    throw new InvalidOperationException("Insufficient funds");
}

// Commits automatically if no exception
```

---

## Best Practices

### 1. Always Use Parameters

**❌ Don't do this (SQL injection risk):**
```csharp
string name = userInput;
string sql = $"SELECT * FROM Users WHERE Name = '{name}'";  // DANGEROUS!
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, new List<object>());
```

**✅ Do this instead:**
```csharp
string sql = "SELECT * FROM Users WHERE Name = @p0";
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, new List<object> { userInput });
```

### 2. Use Typed Results When Possible

Typed results are safer and more maintainable:

```csharp
// Good: Type-safe
List<User> users = await SxmStatement.SelectAsync<User>(sql, parameters);
foreach (User user in users)
{
    Console.WriteLine(user.Name);  // Compile-time safety
}

// Less ideal: Runtime casting required
List<Dictionary<string, object?>> results = await SxmStatement.SelectAsync(sql, parameters);
foreach (Dictionary<string, object?> row in results)
{
    Console.WriteLine((string)row["Name"]!);  // Runtime error if column missing
}
```

### 3. Use Transactions for Related Operations

If multiple operations must succeed together, use a transaction:

```csharp
// Good: Transactional
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");
await transaction.InsertAsync(sql1, params1);
await transaction.UpdateAsync(sql2, params2);
// Auto-commits

// Not ideal: No atomicity guarantee
await SxmStatement.InsertAsync(sql1, params1, "AppData");  
await SxmStatement.UpdateAsync(sql2, params2, "AppData");  // If this fails, first INSERT remains
```

### 4. Use Named Parameters or DTOs for Complex Queries

For queries with many parameters, named parameters or DTOs improve readability:

**With Dictionary (good):**
```csharp
string sql = @"
    UPDATE Users 
    SET Name = @name, 
        Age = @age, 
        Email = @email, 
        Phone = @phone 
    WHERE id = @id";

Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
    { "name", "John" },
    { "age", 30 },
    { "email", "john@example.com" },
    { "phone", "555-1234" },
    { "id", 1 }
};

await SxmStatement.UpdateAsync(sql, parameters);
```

**With DTO (better for reusability):**
```csharp
public class UpdateUserParams
{
    public string? name { get; set; }
    public int age { get; set; }
    public string? email { get; set; }
    public string? phone { get; set; }
    public int id { get; set; }
}

string sql = @"
    UPDATE Users 
    SET Name = @name, 
        Age = @age, 
        Email = @email, 
        Phone = @phone 
    WHERE id = @id";

UpdateUserParams parameters = new UpdateUserParams
{
    name = "John",
    age = 30,
    email = "john@example.com",
    phone = "555-1234",
    id = 1
};

await SxmStatement.UpdateAsync(sql, parameters);
```

### 5. Handle SQLite Type Conversions

Remember that SQLite has limited types. Handle conversions carefully:

```csharp
// SQLite stores integers as long
Dictionary<string, object?> row = results[0];
int id = (int)(long)row["id"];  // ✅ Correct

// Booleans are stored as 0/1
bool isActive = ((long)row["IsActive"]) != 0;  // ✅ Correct

// DateTimes are stored as TEXT
DateTime created = DateTime.Parse((string)row["CreatedAt"]!);  // ✅ Works
```

### 6. Use RETURNING for INSERTs

Always use `RETURNING *` to get inserted data, especially the auto-generated ID:

```csharp
// Good: Get the generated ID back
string sql = "INSERT INTO Users (Name) VALUES (@p0) RETURNING *";
Dictionary<string, object?> newUser = await SxmStatement.InsertAsync(sql, new List<object> { "John" });
int newId = (int)(long)newUser["id"];

// Not ideal: You don't know the generated ID
string sql = "INSERT INTO Users (Name) VALUES (@p0)";
await SxmStatement.InsertAsync(sql, new List<object> { "John" });
// Now what? How do you get the ID?
```

### 7. Specify Database Name When Using Multiple Databases

If you have multiple databases, specify which one:

```csharp
// Query the default database
List<Dictionary<string, object?>> results1 = await SxmStatement.SelectAsync(sql, parameters);

// Query a specific database
List<Dictionary<string, object?>> results2 = await SxmStatement.SelectAsync(sql, parameters, "SecondaryDB");
```

### 8. Dispose Transactions Properly

Always use `await using` with transactions:

```csharp
// ✅ Correct: Auto-disposal
await using SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");
// ...operations...

// ❌ Incorrect: Manual disposal (error-prone)
SxmSqlTransaction transaction = SxmSqlTransaction.Create("AppData");
try 
{
    // ...operations...
    await transaction.CommitTransactionAsync();
}
finally 
{
    await transaction.DisposeAsync();  // Easy to forget!
}
```

### 9. Use LINQ for Complex Queries

For complex queries, consider using SQLiteXM's LINQ support instead:

```csharp
// Direct SQL (fine for simple queries)
string sql = "SELECT * FROM Users WHERE Age > @p0 ORDER BY Name";
List<User> results = await SxmStatement.SelectAsync<User>(sql, new List<object> { 18 });

// LINQ (better for complex queries)
using SxmLinqDbContext context = new SxmLinqDbContext("AppData");
List<User> results = context.GetTable<User>()
    .Where(u => u.Age > 18)
    .OrderBy(u => u.Name)
    .ToList();
```

---

## Summary

SQLiteXM's direct SQL support provides:

- ✅ **Flexible querying** - Write any valid SQLite SQL
- ✅ **Type safety** - Map results to typed objects
- ✅ **Parameter safety** - Protected against SQL injection
- ✅ **Transaction support** - ACID guarantees for related operations
- ✅ **Simple API** - Consistent methods for all operations

Start with simple queries using `SxmStatement`, then use `SxmSqlTransaction` when you need atomicity.

For more information, see:
- [Entity Framework](GettingStarted.md) - Using SQLiteXM entities
- [LINQ Support](QUERYING_DATA.md) - Query using LINQ
- [Relationships](RELATIONSHIPS.md) - Working with related data
