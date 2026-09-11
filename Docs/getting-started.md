# SQLiteXM Getting Started Guide

This guide walks through the basic SQLiteXM workflow: configuring your database, defining entities, initializing SQLiteXM, registering entities, and performing your first database operation.

---

## Understanding the SQLiteXM Lifecycle

Most SQLiteXM applications follow the same general lifecycle.

### Startup (One-Time Initialization)

These steps are typically performed once as part of application startup.

```text
Startup
 ├─ Initialize database
```

### Runtime (Normal Application Usage)

These operations occur throughout the lifetime of your application.

```text
Runtime
 ├─ Save
 ├─ Query
 ├─ Update
 ├─ Delete
 └─ Transactions
```

The remainder of this guide explains this workflow.

---

Startup initialization requires two things:
- Define the database(s) used by your application.
- Define the entity classes used by your application. At least one is required.


## 1. Define Your Database

SQLiteXM uses a configuration file typically named:

```text
SqlStatements.json
```
This is where you define the database(s) used by your application. This file must be included in your 
application package in the `Resources/Raw` folder and the `Build Action` must be set to `MauiAsset`.

Below is an example of a minimal valid `SqlStatements.json` file that defines a single default database named `Chinook`.
```json
{
  "databases": [
    {
      "database": "Chinook",
      "isDefault": true
    }
  ]
}
```

<!-- <details>
<summary>Multiple Databases</summary>
</details> -->

Most SQLiteXM applications use a single database. However, SQLiteXM also supports applications that need 
to organize data across multiple SQLite databases.

For details, see  ➡️ [Multi-Database Configuration](./multiple-databases.md)


### Database definition rules
- There must be at least one database defined in your `SqlStatements.json` file
- Exactly one database must be marked as the default database
- You can define as many non-default databases as needed

---

## 2. Create Your Entities

Entities are C# classes that represent data your application stores in the database. They inherit 
from SxmEntity. SQLiteXM uses these classes to create and manage database tables, 
columns, indexes, triggers, and relationships.

If you are unfamiliar with entities, read the first two sections of  ➡️ **[Defining Entities](./defining-entities.md)**.

The example below shows a simple entity class named `User` with three properties: `Name`, `Age`, and `Email`. The `Email` property is indexed to improve query performance.
During initialization, SQLiteXM will create a table named `User` with three columns: `Name`, `Age`, and `Email`. An index will be created on the `Email` column.

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

### What Does SxmEntity Provide?

By inheriting from `SxmEntity`, your class automatically gains:

- Auto-managed primary key support (`id`) - SQLiteXM automatically assigns the primary key when a new entity is inserted and populates it when an entity is read from the database.
- Entities can save, update, and delete themselves with a single method call:
    1. `SaveAsync()` - Saves the current entity to the database by either inserting it if it is new, or updating it if it already exists.
    2. `DeleteAsync()` - Deletes the current entity from the database if it exists.
- `INotifyPropertyChanged` support
- During initialization, SQLiteXM compares the entity metadata with the existing database schema and creates or updates the required tables, columns, indexes, triggers, and constraints.


### Entity Attributes

SQLiteXM supports a variety of entity attributes that control schema creation and database behavior. 
Below is an entity with a number of applied attributes.

See  ➡️ **[Defining Entities](./defining-entities.md)** for a complete attribute reference.

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

    // Overrides the default data type for DateTime from long (ticks) to ISO 8601 string
    [Column(DataType = SQLiteXM.DataType.Text)] 
    public DateTime Added { get; set; }
}
```

---

## 3. Database Initialization

Once you've defined a database in `SqlStatements.json` and created at least one entity class, 
you're ready to initialize the database.

Initialization is performed once, typically during application startup.

```csharp
    // Create an array containing all the entities used by your application
    Type[] applicationEntities = new Type[]
    {
        typeof(User), 
        typeof(Order), 
        typeof(Product)
    };

    // Open the SqlStatements.json file from the application package
    Stream sqlStatementsStream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");

    // Start database initialization in the background
    // SQLiteXM takes ownership of 'sqlStatementsStream' and ensures proper disposal.
    SxmDatabase.StartInitialization(sqlStatementsStream, databaseOptions: null, applicationEntities);

    // Later, when database access is required:
    await SxmDatabase.EnsureReadyAsync();
```

Call `SxmDatabase.StartInitialization(...)` once, normally early in the application startup cycle. A good place is 
in MauiProgram.cs right after calling `MauiApp.CreateBuilder()`. `StartInitialization` returns immediately 
without blocking - initialization runs in the background.

You can call `StartInitialization` from anywhere (including headless startup paths like Android `BroadcastReceiver`s or iOS
background handlers). You do not need to synchronize calls to `StartInitialization` either - SQLiteXM handles this internally. 
And it is safe to call multiple times and concurrently; only the first call actually performs initialization. 
Subsequent calls return immediately.

See the **[Basic Initialization](./basic-initialization.md)** guide for details.

## What's Happening?

Initialization performs several important tasks.

- Reads the database configuration from `SqlStatements.json`
- Opens or creates the databases
- Applies database options and PRAGMA settings
- Prepares SQLiteXM
- Registers which entity types to use
- Inspects entity metadata
- Creates or updates the database schema

## Database Options

The second parameter of `StartInitialization` is an optional `SxmDatabaseOptions` instance used to customize 
the operation of SQLiteXM and the SQLite database.

This is covered fully in: ➡️ **[Applying Database Options](./database-configuration-options.md)**

---

## 4. Verifying Database Initialization Has Completed

Before the *first* use of the database, anywhere in your app, be sure to call:

```csharp
await SxmDatabase.EnsureReadyAsync();
```

`EnsureReadyAsync` only needs to be called once. It waits for the database initialization task started by `StartInitialization` 
to complete, guaranteeing that the database is fully initialized and ready for use.
It can be called multiple times and concurrently if needed. Once initialization has completed, subsequent 
calls return immediately.

`EnsureReadyAsync` is safe to call from UI-thread async code, view models, or background services alike. You
decide *where* in your app it makes the most sense to await initialization.

---

## 5. Start Working With Your Data

Once initialization is complete, SQLiteXM is ready for normal application use.
You can create and save entities, query and modify data using LINQ or SQL, and begin using transactions.

This is covered fully in: ➡️ **[Working With Data](./working-with-data.md)**

For example:

```csharp
var user = new User
{
    Name = "Alice",
    Age = 30,
    Email = "alice@example.com"
};

// Insert the new entity.
await user.SaveAsync();

await using (var ctx = new SxmTransaction())
{
    // Query using LINQ.
    var existingUser =
        ctx.GetTable<User>()
           .FirstOrDefault(u => u.Name == "Alice");

    // Execute SQL within the same transaction.
    await ctx.RunStatementAsync(
        "UPDATE User SET LastLogin = CURRENT_TIMESTAMP WHERE Name = 'Alice'");

    // Modify and persist the entity.
    existingUser.Age = 31;
    await existingUser.SaveAsync();

} // <-- Automatically commits transaction on dispose if no errors occurred
```

SxmTransaction uses a commit-on-success model. If the transaction scope exits normally, 
the transaction is committed. If a database operation throws an exception, the transaction 
is rolled back. Manual commit and rollback are also supported. See ➡️ **[Working With Data](./working-with-data.md)** for details.

<br>&nbsp;</b>

