# SQLiteXM Defining Entities

## Understanding SQLiteXM Entities

If you are new to SQLiteXM or ORMs, the word entity may be unfamiliar. An entity is simply a C# class
that represents data your application wants to store in the database.

In SQLiteXM, an entity is a C# class that inherits from `SxmEntity` and is registered with
`SxmDatabase.RegisterEntitiesAsync(...)`.

For example, an application might need to store customers. A customer has information such as a name and email address. 
In SQLiteXM, you define a Customer entity as a C# class:

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	public string? Name { get; set; }
	public string? Email { get; set; }
}
```

In this example, SQLiteXM uses this class as the C# representation of a `Customer` record stored in the database.
Behind the scenes, SQLiteXM maps the entity to a SQLite table:

| C# | | SQLite Database |
|---|---|---|
|`Customer` Object     | ─────►  | `Customer` table
|`Name` Property     | ─────►  | `Name` column
|`Email` Property    | ─────►  | `Email` column

An entity serves two related purposes:

- In your C# code, it represents data your application works with.
- In SQLiteXM, it defines the structure of a table that is used to store that data.

In this example:

- `Customer` represents data that the application wants to store.
- SQLiteXM maps `Customer` to a database table also named `Customer`.
- `[Table(IsColumnAttributeRequired = false)]` tells SQLiteXM to automatically map all public properties to database columns.
- `Name` and `Email` become database columns in the `Customer` table.
- A `Customer` object represents one row of that table.

SQLiteXM creates or updates the corresponding table when the entity is registered with `SxmDatabase.RegisterEntitiesAsync(...)`.

This means you work with ordinary C# objects in your application, while SQLiteXM handles creating and managing the 
corresponding database tables and columns for you.

For example, when you create and save a Customer object, SQLiteXM stores its values in the corresponding Customer 
database table. When you query the database, SQLiteXM can create Customer objects from the stored data.

An entity can also define additional database behavior. You add that information using SQLiteXM attributes, 
which are explained below.

---

## Entity Attributes

Once you understand the basic relationship between a C# entity and a database table, you can use attributes to control how SQLiteXM maps the entity to the database.

SQLiteXM provides attributes that control things such as:

| Attribute | Targets | Purpose |
|---|---|---|
| `[Table]` | Class | Configures table-level behavior and can assign the entity to a database |
| `[Column]` | Property | Maps a public property to a column |
| `[NotColumn]` | Property | Excludes a public property from schema mapping |
| `[Rename]` | Property | Preserves data when a public property name changes |
| `[Index]` | Class, Property | Creates a non-unique index |
| `[UniqueIndex]` | Class, Property | Creates a unique index |
| `[Trigger]` | Class | Adds trigger SQL during schema creation |
| `[RequiredNotNull]` | Property | Requires a non-null value and supplies a default |
| `[ForeignKey]` | Property | Creates a foreign key reference |

The important thing to understand is that the entity defines the data, while the attributes provide configuration
for how SQLiteXM maps that data to the database.

> ✏️ **Note:** A class only needs to inherit from `SxmEntity` to be recognized as an entity. 
> However, a useful entity will require additional configuration. While attributes are optional,
> they provide configuration to control which entity properties are mapped to columns and can 
> define additional schema features such as indexes, foreign keys, triggers, etc.

---

## Table Attribute

The `[Table]` attribute is used to control which database the entity is mapped to and which properties 
are mapped to table columns.

## Table Attribute Options

Use the `IsColumnAttributeRequired` property of the `Table` attribute when you want to explicitly control 
which properties are mapped to database columns.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Invoice : SxmEntity
{
	public decimal Total { get; set; }
}
```

* When set to `false`, all public properties are automatically mapped to database columns unless marked with `[NotColumn]`
* When set to `true`, or when omitted, only public properties marked with `[Column]` will be mapped

The `Database` property tells SQLiteXM the database where the entity's corresponding table should be created.

```csharp
[Table(Database = "Logging")]
public class ApplicationLog : SxmEntity
{
	[Column]
	public string? Message { get; set; }
	[Column]
	public DateTime Timestamp { get; set; }
}
```

* If the `Database` property is omitted, the entity's corresponding table is created in the default database.
* When present, the entity's corresponding table is created in the named database.

Of course, these two properties can be combined:
```csharp
[Table(Database = "Logging", IsColumnAttributeRequired = true)]
public class ApplicationLog : SxmEntity
{
	[Column]
	public string? Message { get; set; }
	[Column]
	public DateTime Timestamp { get; set; }
}
```

## Column Attribute

Use `[Column]` to explicitly control the mapping for a property.

```csharp
[Table(IsColumnAttributeRequired = true)]
public class Invoice : SxmEntity
{
	[Column]
	public decimal Total { get; set; }
}
```

* If `[Column]` is specified, the property is automatically mapped to a database column
* If `[Column]` is omitted, property mapping is controlled by the setting of `IsColumnAttributeRequired` on the class

## Column Attribute Options

The `DataType` property allows you to override the default data type mapping for certain properties.

The default storage type for a `DateTime` is `INTEGER`, stored as .NET ticks. In the example below, 
we are overriding the storage type to `Text`, which will cause SQLiteXM to store this specific 
DateTime property as an ISO 8601 date:
```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateTime AddedOn { get; set; }
```

This is useful when you want to override the default storage type chosen by SQLiteXM.

For complete details on data type mapping and supported types, see the [SQLiteXM Data Type](./SUPPORTED_DATA_TYPES.md) guide.

---

## NotColumn Attribute

Use `[NotColumn]` for properties that should not be stored in the database.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Person : SxmEntity
{
	public string? FirstName { get; set; }
	public string? LastName { get; set; }

	[NotColumn]
	public string FullName => $"{FirstName} {LastName}";
}
```

When present, `[NotColumn]` prevents the property from being mapped to a database column, regardless of the `IsColumnAttributeRequired` setting on the class.

### When to Use It

* Computed properties
* UI-only properties
* Runtime state that should not be persisted

---

## Rename Attribute

Use `[Rename]` when a property name changes but you want SQLiteXM to preserve existing data during schema migration.

In the example below, for an existing `Customer` table, the schema is updated by renaming the `FirstName` column to `GivenName` while 
preserving existing data. For new `Customer` registrations where `FirstName` does not exist, no rename action is required and the column 
`GivenName` is simply created.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	[Rename("FirstName")]
	public string GivenName { get; set; } = string.Empty;
}
```

### Multi-Step Renames

If a column was renamed more than once, track the full rename history.

```csharp
[Rename("Title", "DisplayName")]
public string ProductName { get; set; } = string.Empty;
```

### Rules

* The old property must be removed from the entity class
* SQLiteXM searches rename history from newest to oldest
* If no old column exists, the new column is created normally


---

## Index Attribute

Use `[Index]` to create a non-unique index.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	[Index]
	public DateTime OrderDate { get; set; }
}
```

### Composite Indexes

You can apply `[Index]` at the class level to define a composite index.

```csharp
[Table(IsColumnAttributeRequired = false)]
[Index("CustomerId", "OrderDate")]
public class Order : SxmEntity
{
	public long CustomerId { get; set; }
	public DateTime OrderDate { get; set; }
}
```

### When to Use It

* Frequently filtered columns
* Columns used in joins
* Composite query patterns

---

## UniqueIndex Attribute

Use `[UniqueIndex]` to create a unique index.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
	[UniqueIndex]
	public string Email { get; set; } = string.Empty;
}
```

### Composite Unique Index

You can apply `[UniqueIndex]` at the class level to define a composite index.

```csharp
[UniqueIndex("PlaylistId", "TrackId")]
public class PlaylistTrack : SxmEntity
{
	public long PlaylistId { get; set; }
	public long TrackId { get; set; }
}
```

### When to Use It

* Prevent duplicate values in a column
* Enforce uniqueness across multiple columns
* Model natural keys

---

## Trigger Attribute

Use `[Trigger]` to attach trigger SQL to an entity class.

```csharp
[Table(IsColumnAttributeRequired = false)]
[Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_User_Insert
AFTER INSERT ON User
BEGIN
	UPDATE User SET Name = upper(new.Name) WHERE id = new.id;
END;")]
public class User : SxmEntity
{
	public string? Name { get; set; }
}
```

### When to Use It

* Audit logging
* Automatic updates
* Custom database-side behavior

Triggers are created as part of schema initialization and registration.

### Trigger Lifecycle Management

SQLiteXM manages trigger definitions as part of schema synchronization.

During entity registration:

* New triggers defined with `[Trigger]` are created automatically
* Existing triggers are updated when the trigger SQL changes
* Triggers that are no longer defined on the entity are removed

Trigger definitions evolve alongside your entity classes without requiring manual migration scripts.

SQLiteXM keeps the database synchronized with the trigger configuration in your code.


---

## RequiredNotNull Attribute

Use `[RequiredNotNull]` when a property must not be null and should have a non-null default value.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Settings : SxmEntity
{
	[RequiredNotNull(DefaultValue = "Not Set")]
	public string Theme { get; set; } = string.Empty;
}
```

### Behavior

* The attribute stores a default value
* The default value cannot be null
* It is intended for values that must always be present

---

## ForeignKey Attribute

Use `[ForeignKey]` to declare a relationship to another table.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	[ForeignKey(ForeignTable = nameof(Customer), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long CustomerId { get; set; }
}
```

### Understanding ForeignKeyDeleteAction

ForeignKeyDeleteAction determines what happens when a parent row is deleted while related child rows still exist.

For example, consider an Order table that references a Customer table through a foreign key. If a customer is deleted, SQLite must decide what happens to the related orders. The selected ForeignKeyDeleteAction controls that behavior.

### ForeignKeyDeleteAction Values

| Value | Meaning |
|---|---|
| `None` | No explicit delete action |
| `Cascade` | Delete child rows when the parent is deleted |
| `SetNull` | Set the foreign key column to `NULL` |
| `SetDefault` | Set the foreign key column to its default value |
| `Restrict` | Prevent deletion when related rows exist |
| `NoAction` | Take no automatic action; the delete succeeds only if referential integrity is preserved. The foreign-key constraint is still enforced. |

### When to Use It

* Parent-child relationships
* Referential integrity
* Modeling related entities in the same database

---

### Entity Registration

Defining entities is only the first step. SQLiteXM must also register them before use.

```csharp
await SxmDatabase.RegisterEntitiesAsync(
	typeof(Customer),
	typeof(Order),
	typeof(OrderLine)
);
```

During registration, SQLiteXM creates or updates the schema for each entity and applies indexes, triggers, and foreign keys where appropriate.

---

## Practical Entity Design Rules

### Keep Related Entities Together

Entities that are frequently queried together should usually live in the same database.

### Use Computed Members Carefully

Computed or UI-only members should be marked with `[NotColumn]`.

### Preserve Data During Renames

Use `[Rename]` whenever a property name changes in a later version of your app.

###	Index Query Paths

Add indexes to columns that are frequently used for filtering, ordering, or joining.

### Use Foreign Keys for Real Relationships

Use `[ForeignKey]` when one entity depends on another entity's identifier.

---

## Complete Example

The following example shows a small related model with multiple attributes.

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	[UniqueIndex]
	public string Email { get; set; } = string.Empty;

	public string? FirstName { get; set; }

	public string? LastName { get; set; }

	[NotColumn]
	public string DisplayName => $"{FirstName} {LastName}".Trim();
}

[Table(IsColumnAttributeRequired = true)]
[Index("CustomerId", "OrderDate")]
public class Order : SxmEntity
{
	[Column]
	[ForeignKey(ForeignTable = nameof(Customer), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long CustomerId { get; set; }

	[Column]
	public DateTime OrderDate { get; set; }

	[Column]
	[RequiredNotNull(DefaultValue = 0m)]
	public decimal Total { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
[UniqueIndex("OrderId", "LineNumber")]
[Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_OrderLine_Insert
AFTER INSERT ON OrderLine
BEGIN
	INSERT INTO AuditLog(Message, CreatedOn)
	VALUES ('OrderLine inserted', CURRENT_TIMESTAMP);
END;")]
public class OrderLine : SxmEntity
{
	[ForeignKey(ForeignTable = nameof(Order), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long OrderId { get; set; }

	public int LineNumber { get; set; }

	[Column(DataType = SQLiteXM.DataType.Text)]
	public DateTime AddedOn { get; set; }

	[Rename("ItemName")]
	public string ProductName { get; set; } = string.Empty;
}

[Table(IsColumnAttributeRequired = false)]
public class AuditLog : SxmEntity
{
	public string Message { get; set; } = string.Empty;
	public DateTime CreatedOn { get; set; }
}
```

### What This Example Shows

* `Customer` uses a unique index on `Email`
* `Order` references `Customer` through a foreign key
* `OrderLine` references `Order` and preserves data from a renamed column
* A trigger adds custom database-side behavior
* `[NotColumn]` is used for a computed display property

---

## Advanced Details

### Inherited Properties

SQLiteXM maps public instance properties inherited from base classes as well as
properties declared directly on the entity.

This allows common properties to be defined once in a base entity class and
automatically included in derived entities.

For example:

```csharp
public abstract class DomainEntity : SxmEntity
{
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Customer : DomainEntity
{
	public string? Name { get; set; }
}
```

Because `Customer` inherits from `DomainEntity`, SQLiteXM maps `CreatedAt` and
`ModifiedAt` in addition to `Name`.

The resulting `Customer` table contains:

| C#  Property| | SQLite Column |
|---|---|---|
|`CreatedAt`     | ─────►  | `CreatedAt` column
|`ModifiedAt`     | ─────►  | `ModifiedAt` column
|`Name` Property     | ─────►  | `Name` column

Inherited properties follow the same mapping rules as properties declared
directly on the entity. `[Column]`, `[NotColumn]`, and
`[Table(IsColumnAttributeRequired = ...)]` apply to inherited properties as
well.

---

## Summary

SQLiteXM entities are plain C# classes with attribute-based mapping.

Key points:

* Inherit from `SxmEntity`
* Use `[Table]` to configure column mapping and optionally choose the database
* Use `[Column]` to control mapped public properties
* Use `[NotColumn]` for values that should not be stored
* Use `[Rename]` to preserve data during refactoring
* Use `[Index]` and `[UniqueIndex]` to improve query performance and enforce uniqueness
* Use `[ForeignKey]` for relationships
* Use `[Trigger]` for custom database behavior
* Register entity types with `SxmDatabase.RegisterEntitiesAsync(...)`

For a basic workflow overview, see **GettingStarted.md**.
For multi-database setup, see **MULTI_DATABASES.md**.
