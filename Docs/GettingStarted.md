# SQLiteXM Getting Started Guide

## Introduction

Welcome to SQLiteXM.

This guide walks through the complete SQLiteXM workflow, from initial setup to performing common database operations.

By the end of this guide you'll understand:

- How SQLiteXM is configured
- How entities are defined
- How databases are initialized
- How data is saved, queried, updated, and deleted
- When to use transactions
- Where to learn more about advanced features

This guide focuses on the most common workflow used by SQLiteXM applications. Advanced topics are covered in separate documentation.

---

# Understanding the SQLiteXM Lifecycle

Most SQLiteXM applications follow the same general lifecycle.

## Startup (One-Time Initialization)

These steps are typically performed once when your application starts.

```text
Startup
 ├─ Configure SQLiteXM
 ├─ Initialize database
 └─ Register entities
```

## Runtime (Normal Application Usage)

These operations occur throughout the lifetime of your application.

```text
Runtime
 ├─ Save
 ├─ Query
 ├─ Update
 ├─ Delete
 └─ Transactions
```

The remainder of this guide follows that same workflow.

---

# 1. Define Your Entities

Entities are ordinary C# classes that inherit from `SxmEntity`.

SQLiteXM uses these classes to generate database tables and map records to objects.

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    public string? Name { get; set; }
    public int Age { get; set; }

    [Index]
    public string? Email { get; set; }
}
```

## What Does SxmEntity Provide?

By inheriting from `SxmEntity`, your class automatically gains:

- Primary key support (`id`)
- `SaveAsync()` - Saves the current entity to the database by either inserting it if it is new, or updating it if it already exists.
- `DeleteAsync()`
- `INotifyPropertyChanged` support
- The database schema is created when entities are registered via `RegisterEntitiesAsync`


## Entity Attributes

SQLiteXM supports a variety of entity attributes that control schema creation and database behavior. 
Below is an entity with a number of applied attributes.

See  ➡️ **[Defining Entities](./DEFINING_ENTITIES.md)** for a complete attribute reference.

<!-- ```csharp

| Attribute | Targets | Supported options | Purpose |
|---|---|---|---|
| `[Table]` | Class | `Database`, `IsColumnAttributeRequired` | Maps an entity to a database table |
| `[Column]` | Property | `DataType` | Maps a property to a column and controls data type mapping |
| `[NotColumn]` | Property | None | Excludes a property from column mapping |
| `[Rename]` | Property | `OldName(s)` | Tracks previous column names for schema migration |
| `[Index]` | Class, Property | `IndexFields` (when target is class) | Creates a non-unique index |
| `[UniqueIndex]` | Class, Property | `IndexFields` (when target is class) | Creates a unique index |
| `[Trigger]` | Class | `TriggerSql` | Includes trigger creation SQL during initialization |
| `[RequiredNotNull]` | Property | `DefaultValue` | Requires a non-null value and supplies a default |
| `[ForeignKey]` | Property | `ForeignTable`, `OnDelete` | Creates a foreign key reference to another table |

### Supported `ForeignKeyDeleteAction` values

| Value | Meaning |
|---|---|
| `None` | No explicit action; SQLite uses the default behavior |
| `Cascade` | Delete child rows when the parent row is deleted |
| `SetNull` | Set the foreign key column to `NULL` |
| `SetDefault` | Set the foreign key column to its default value |
| `Restrict` | Prevent parent deletion when child rows exist |
| `NoAction` | Defer the constraint check without taking action |

### Example -->

```csharp
[Table(Database = "Chinook", IsColumnAttributeRequired = false)]
[UniqueIndex("PlaylistId", "TrackId")]
[Index("TrackId", "PlaylistId")]
public class PlaylistTrack : SxmEntity
{
    [Index]
    [ForeignKey(ForeignTable = nameof(Playlist), OnDelete = ForeignKeyDeleteAction.Cascade)]
    public long PlaylistId { get; set; }

    [Index]
    [ForeignKey(ForeignTable = nameof(Track), OnDelete = ForeignKeyDeleteAction.Cascade)]
    public long TrackId { get; set; }

    // Overrides the default data type for DateTime from long to ISO 8601 string
    [Column(DataType = SQLiteXM.DataType.Text)] 
    public DateTime Added { get; set; }
}
```

---

# 2. Configure Your Database

SQLiteXM uses a configuration file typically named:

```text
SqlStatements.json
```

This file defines the database(s) used by your application.

```json
{
  "databases": [
    {
      "database": "MyApp",
      "isDefault": true
    }
  ]
}
```

<!-- <details>
<summary>Multiple Databases</summary>

Most applications only need a single default database. However, you can define multiple databases in the configuration file.
```json
{
  "databases": [
    {
      "database": "MyApp",
      "isDefault": true
    },
    {
      "database": "Logging",
      "isDefault": false
    }
  ]
}
```

Entities automatically use the default database unless configured otherwise using the `[Table]` attribute. For example; 
```csharp
[Table(Database = "Logging")]
```
</details> -->

Most SQLiteXM applications use a single database. However, SQLiteXM also supports applications that need to organize data across multiple databases.

For details, see  ➡️ [Multi-Database Configuration](./MULTI_DATABASES.md)


## Database definition rules
- There must be at least one database defined
- There can only be one default database
- You can define as many non-default databases as needed



---

# 3. Initialize SQLiteXM

Initialization is performed once, typically during application startup.

```csharp
public static async Task InitializeDatabaseAsync()
{
    using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    await SxmDatabase.InitializeAsync(stream, databaseOptions: null);
    await SxmDatabase.RegisterEntitiesAsync(typeof(User));
}
```

## What's Happening?

Initialization and registration together perform several important tasks::

- Loads the database configuration from `SqlStatements.json`
- Creates the databases, if they don't already exist
- Applies SQLite settings and PRAGMA configuration
- Registers entity types
- Creates or updates database tables

## Database Options

The second parameter of `InitializeAsync()` is an optional `SxmDatabaseOptions` instance used to customize the operation of SQLiteXM and the SQLite database.

This is covered fully in the :

➡️ **[Database Configuration and Initialization](./DATABASE_CONFIGURATION_AND_INITIALIZATION.md)**

---

## After Initialization

Once initialization completes:

* Your databases are available
* Tables have been created or updated
* Entities are registered
* SQLiteXM is ready to perform CRUD operations

At this point, the startup phase is complete and your application can begin performing normal database operations such as saving, querying, updating, and deleting data.

---
# 4. Saving Data

Creating records is straightforward.

```csharp
var user = new User
{
    Name = "Alice",
    Email = "alice@example.com",
    Age = 30
};

await user.SaveAsync();
```

## What Does SaveAsync() Do?

SQLiteXM automatically determines whether:

- The entity is new (INSERT)
- The entity already exists (UPDATE)

This behavior is based on the entity's primary key value. You don't need separate insert and update methods.

## Generated IDs

After a successful save:

```csharp
await user.SaveAsync();

Console.WriteLine(user.id);
```

The generated primary key becomes available immediately.

## Error Handling

Basic error processing can simply allow exceptions to bubble up.

```csharp
try
{
    await user.SaveAsync();
}
catch (Exception ex)
{
    Logger.LogError(ex.Message);
}
```

More advanced error handling strategies are covered elsewhere.

See **ERROR_HANDLING.md** for additional patterns.

---

# 5. Querying Data

SQLiteXM provides LINQ-based querying through `SxmLinqDbContext`.

```csharp
// Create a DB context on the 'MyApp' database
using var context = new SxmLinqDbContext("MyApp");

var users = context.GetTable<User>()
               .Where(u => u.Age >= 18)
               .OrderBy(u => u.Name)
               .ToList();
```

## How Queries Work

LINQ queries are not executed until they are materialized using methods such as `ToList()`, `FirstOrDefault()`, `Count()`, etc. LINQ expressions are translated into SQLite queries. This means filtering occurs in the database, so only matching rows are returned.

### Good

```csharp
.Where(u => u.Age >= 18)
```

### Avoid

```csharp
.ToList()
.Where(u => u.Age >= 18)
```

The second example loads all records before filtering.

## Common Query Operations

### Find a Single Record

```csharp
var user = context.GetTable<User>()
               .FirstOrDefault(u => u.Email == email);
```

### Sorting

```csharp
.OrderBy(u => u.Name)
```

### Paging

```csharp
.Skip(20)
.Take(10)
```

### Counting

```csharp
.Count()
```

### Checking for Existence 

```csharp
.Any()
```

## Related Data

SQLiteXM supports eager loading through `LoadWith()`.

```csharp
.LoadWith(o => o.Customer)
```

This is useful when loading related entities.

See **QUERYING_DATA.md** for advanced query patterns.

---

# 6. Updating Data

Updating records follows the same workflow as saving.

```csharp
user.Age = 31;
await user.SaveAsync();
```

SQLiteXM detects that the entity already exists and performs an update.

## Why Doesn't UpdateAsync Exist?

SQLiteXM intentionally uses a single save workflow. This reduces API surface area and keeps persistence logic simple.

```csharp
await entity.SaveAsync();
```

works for both inserts and updates.

---

# 7. Deleting Data

Deleting records is equally straightforward.

```csharp
await user.DeleteAsync();
```

## What Happens?

SQLiteXM removes the corresponding row from the database.

After deletion, the entity object remains in memory but no longer exists in the database.

## Things to Consider

If foreign key constraints are enabled:

```csharp
ForeignKeys = true
```

SQLite may prevent deletion if related records still exist.

See **RELATIONSHIPS.md** for more information.

---

# 8. Transactions

SQLiteXM automatically wraps individual save and delete operations in transactions. For many applications, explicit transactions are unnecessary.

## When Should I Use a Transaction?

Use an explicit transaction when multiple database operations must succeed or fail as a single unit of work.

## Basic Example

```csharp
await using var transaction = SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();
await order.SaveAsync();
await orderItem.SaveAsync();
```

In this example, all `SaveAsync()` calls automatically participate in the transaction.

No special transaction parameter is required.

SQLiteXM detects the active transaction and automatically enlists database operations into it.

## Automatic Commit Behavior

Transactions automatically commit when they are disposed successfully.

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();
await order.SaveAsync();

// No explicit commit required
```

When execution reaches the end of the `await using` block without errors, SQLiteXM automatically commits the transaction.

## Automatic Rollback Behavior

If an exception occurs before the transaction completes, SQLiteXM automatically rolls back all changes.

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();

throw new Exception("Something went wrong");

// Transaction automatically rolls back
```

This helps ensure your database remains consistent even when failures occur.

## Manual Commit and Rollback

Although automatic transaction management is often sufficient, manual control is also available.

### Manual Commit

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();

await transaction.CommitTransactionAsync();
```

Calling `CommitTransactionAsync()` is optional.

It is perfectly valid to commit manually and then allow the transaction object to be disposed normally.

### Manual Rollback

```csharp
await using var transaction =
    SxmSqlTransaction.Create("MyApp");

await user.SaveAsync();

await transaction.RollbackTransactionAsync();
```

Manual rollback is useful when business rules determine that changes should not be persisted even though no exception occurred.

## How Transaction Participation Works

Whenever an SQLiteXM transaction exists, calls to:

```csharp
await entity.SaveAsync();

await entity.DeleteAsync();
```

automatically participate in that transaction.

The same API is used regardless of whether a transaction exists.

This means you do not need separate transaction-aware save or delete methods.

```csharp
await user.SaveAsync();
```

works correctly both inside and outside a transaction.

## Benefits

Transactions provide:

* Atomicity
* Consistency
* Automatic rollback on failure
* Simplified multi-entity persistence

SQLiteXM's transaction model allows you to write normal save and delete code while still gaining the safety and consistency benefits of transactions.

For advanced transaction scenarios and best practices, see:

➡️ **TRANSACTIONS.md**

---

# Common Beginner Questions

## Do I Need a DbContext to Save Data?

No.

```csharp
await user.SaveAsync();
```

is often sufficient.

Use a context primarily for LINQ queries and batch operations.

## Do I Need to Create Tables Manually?

No.

SQLiteXM creates tables when entities are registered.

```csharp
await SxmDatabase.RegisterEntitiesAsync(
    typeof(User));
```

## Do I Need Migrations?

SQLiteXM automatically manages schema creation and updates during entity registration.

## Should I Enable WAL Mode?

For most applications:

**Yes.**

For very small applications: The defaults are usually sufficient.

---

# Next Steps

At this point you understand the complete SQLiteXM workflow.

A good next progression is:

1. Entity Modeling
2. Database Options
3. Relationships & Foreign Keys
4. Error Handling
5. Bulk Operations
6. Usage Patterns

After that, the deep-dive documentation becomes much easier to understand.

See:

- ENTITY_MODELING.md
- DATABASE_CONFIGURATION_AND_INITIALIZATION.md
- RELATIONSHIPS.md
- ERROR_HANDLING.md
- BULK_OPERATIONS.md
- USAGE_PATTERNS.md