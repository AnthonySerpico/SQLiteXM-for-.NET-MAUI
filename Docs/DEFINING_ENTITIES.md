# SQLiteXM Defining Entities Guide

## Introduction

SQLiteXM entities are ordinary C# classes that inherit from `SxmEntity`.

These classes define the tables, columns, indexes, triggers, and relationships that SQLiteXM creates and manages for your application.

This guide explains how to design entity classes, which attributes SQLiteXM supports, and how those attributes affect schema generation and runtime behavior.

---

# Understanding SQLiteXM Entities

An entity class represents one table in a SQLite database.

At a minimum, an entity must:

* Inherit from `SxmEntity`
* Be registered with `SxmDatabase.RegisterEntitiesAsync(...)`
* Be included in a database initialized with `SxmDatabase.InitializeAsync(...)`

SQLiteXM uses reflection to read attribute metadata from your entity classes and then creates or updates the corresponding database schema.

---

# Basic Entity Pattern

The simplest entity definition looks like this:

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
	public string? Name { get; set; }
	public int Age { get; set; }
}
```

## What This Means

* `User` becomes a database table
* The class inherits persistence and notification behavior from `SxmEntity`
* Properties become database columns
* SQLiteXM can create or update the table during entity registration

Every entity automatically inherits an `id` property from `SxmEntity`. SQLiteXM uses this value as the table's primary key and automatically populates it when a new entity is saved.

---

# Required Entity Workflow

A typical setup follows this order:

1. Define entity classes
2. Initialize SQLiteXM
3. Register entities
4. Use the entities for saving, querying, updating, and deleting data

```csharp
using var stream = await FileSystem.OpenAppPackageFileAsync("SqlStatements.json");
await SxmDatabase.InitializeAsync(stream);
await SxmDatabase.RegisterEntitiesAsync(typeof(User));
```

---

# Entity Attributes

SQLiteXM provides attributes that control how a class or member maps to the database.

| Attribute | Targets | Purpose |
|---|---|---|
| `[Table]` | Class | Maps an entity to a table and can assign it to a database |
| `[Column]` | Property, Field | Maps a member to a column and controls data type mapping |
| `[NotColumn]` | Property, Field | Excludes a member from schema mapping |
| `[Rename]` | Property | Preserves data when a property name changes |
| `[Index]` | Class, Property, Field | Creates a non-unique index |
| `[UniqueIndex]` | Class, Property, Field | Creates a unique index |
| `[Trigger]` | Class | Adds trigger SQL during schema creation |
| `[RequiredNotNull]` | Property, Field | Requires a non-null value and supplies a default |
| `[ForeignKey]` | Property, Field | Creates a foreign key reference |

---

# 1. Table Attribute

The `[Table]` attribute marks a class as a SQLiteXM entity.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Product : SxmEntity
{
	public string? Name { get; set; }
}
```

## Table Attribute Options

#### Database

SQLiteXM supports the `Database` property for multi-database applications.

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

* If `Database` is not specified, the entity uses the default database
* Entities are registered with `SxmDatabase.RegisterEntitiesAsync(...)`


#### IsColumnAttributeRequired 

Use `IsColumnAttributeRequired` when you want to explicitly control which properties are mapped to database columns.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Invoice : SxmEntity
{
	public decimal Total { get; set; }
}
```

* When set to `false`, all public properties are automatically mapped to database columns unless marked with `[NotColumn]`
* When set to `true`, or when omitted, only public properties marked with `[Column]` are mapped

# 2. Column Attribute

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

#### Controlling Data Type Mapping

The `DataType` property allows you to override the default data type mapping for certain properties.

The default storage type for a `DateTime` is `INTEGER` and is stored as ticks. In the example below, 
we are overriding the storage type to `Text`, which will cause SQLiteXM to store this specific 
DateTime property as an ISO 8601 date:
```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateTime AddedOn { get; set; }
```

This is useful when you want to override the default storage type chosen by SQLiteXM.

For complete details on data type mapping and supported types, see the [SQLiteXM Data Type](./SUPPORTED_DATA_TYPES.md) guide.

---

# 3. NotColumnAttribute

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

## When to Use It

* Computed properties
* UI-only properties
* Runtime state that should not be persisted

---

# 4. Rename Attribute

Use `[Rename]` when a property name changes but you want SQLiteXM to preserve existing data during schema migration.

In the example below, for existing users the schema is updated by renaming the `FirstName` column to `GivenName` while 
preserving existing data. For new users, `FirstName` does not exist so no rename action is required and the column 
`GivenName` is simply created.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	[Rename("FirstName")]
	public string GivenName { get; set; } = string.Empty;
}
```

## Multi-Step Renames

If a column was renamed more than once, track the full rename history.

```csharp
[Rename("Title", "DisplayName")]
public string ProductName { get; set; } = string.Empty;
```

## Rules

* The old property must be removed from the entity class
* SQLiteXM searches rename history from newest to oldest
* If no old column exists, the new column is created normally

---

# 5. Index Attribute

Use `[Index]` to create a non-unique index.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	[Index]
	public DateTime OrderDate { get; set; }
}
```

## Class-Level Indexes

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

## When to Use It

* Frequently filtered columns
* Columns used in joins
* Composite query patterns

---

# 6. UniqueIndex Attribute

Use `[UniqueIndex]` to create a unique index.

```csharp
[Table(IsColumnAttributeRequired = false)]
[UniqueIndex("Email")]
public class User : SxmEntity
{
	public string Email { get; set; } = string.Empty;
}
```

## Composite Unique Index

```csharp
[UniqueIndex("PlaylistId", "TrackId")]
public class PlaylistTrack : SxmEntity
{
	public long PlaylistId { get; set; }
	public long TrackId { get; set; }
}
```

## When to Use It

* Prevent duplicate values in a column
* Enforce uniqueness across multiple columns
* Model natural keys

---

# 7. Trigger Attribute

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

## When to Use It

* Audit logging
* Automatic updates
* Custom database-side behavior

Triggers are created as part of schema initialization and registration.

---

# 8. RequiredNotNull Attribute

Use `[RequiredNotNull]` when a property must never be null and should have a default value.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Settings : SxmEntity
{
	[RequiredNotNull(DefaultValue = "Not Set")]
	public string Theme { get; set; } = string.Empty;
}
```

## Behavior

* The attribute stores a default value
* The default value cannot be null
* It is intended for values that must always be present

---

# 9. ForeignKey Attribute

Use `[ForeignKey]` to declare a relationship to another table.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Order : SxmEntity
{
	[ForeignKey(ForeignTable = nameof(Customer), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long CustomerId { get; set; }
}
```

## ForeignKeyDeleteAction Values

| Value | Meaning |
|---|---|
| `None` | No explicit delete action |
| `Cascade` | Delete child rows when the parent is deleted |
| `SetNull` | Set the foreign key column to `NULL` |
| `SetDefault` | Set the foreign key column to its default value |
| `Restrict` | Prevent deletion when related rows exist |
| `NoAction` | Defer the constraint check |

## When to Use It

* Parent-child relationships
* Referential integrity
* Modeling related entities in the same database

---

# Entity Registration

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

# Practical Entity Design Rules

## Keep Related Entities Together

Entities that are frequently queried together should usually live in the same database.

## Use Computed Members Carefully

Computed or UI-only members should be marked with `[NotColumn]`.

## Preserve Data During Renames

Use `[Rename]` whenever a property name changes in a later version of your app.

## Index Query Paths

Add indexes to columns that are frequently used for filtering, ordering, or joining.

## Use Foreign Keys for Real Relationships

Use `[ForeignKey]` when one entity depends on another entity's identifier.

---

# Complete Example

The following example shows a small related model with multiple attributes.

```csharp
using SQLiteXM;

[Table(IsColumnAttributeRequired = false)]
[UniqueIndex("Email")]
public class Customer : SxmEntity
{
	[Column]
	public string Email { get; set; } = string.Empty;

	[Column]
	public string? FirstName { get; set; }

	[Column]
	public string? LastName { get; set; }

	[NotColumn]
	public string DisplayName => $"{FirstName} {LastName}".Trim();
}

[Table(IsColumnAttributeRequired = false)]
[Index("CustomerId", "OrderDate")]
public class Order : SxmEntity
{
	[ForeignKey(ForeignTable = nameof(Customer), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long CustomerId { get; set; }

	[Column]
	public DateTime OrderDate { get; set; }

	[RequiredNotNull(0m)]
	public decimal Total { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
[UniqueIndex("OrderId", "LineNumber")]
public class OrderLine : SxmEntity
{
	[ForeignKey(ForeignTable = nameof(Order), OnDelete = ForeignKeyDeleteAction.Cascade)]
	public long OrderId { get; set; }

	[Column]
	public int LineNumber { get; set; }

	[Column(DataType = SQLiteXM.DataType.Text)]
	public DateTime AddedOn { get; set; }

	[Rename("ItemName")]
	public string ProductName { get; set; } = string.Empty;
}

[Table(Database = "Logging", IsColumnAttributeRequired = false)]
[Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_OrderLine_Insert
AFTER INSERT ON OrderLine
BEGIN
	INSERT INTO AuditLog(Message, CreatedOn)
	VALUES ('OrderLine inserted', CURRENT_TIMESTAMP);
END;")]
public class AuditLog : SxmEntity
{
	public string Message { get; set; } = string.Empty;
	public DateTime CreatedOn { get; set; }
}
```

## What This Example Shows

* `Customer` uses a unique index on `Email`
* `Order` references `Customer` through a foreign key
* `OrderLine` references `Order` and preserves data from a renamed column
* `AuditLog` is stored in a separate database
* A trigger adds custom database-side behavior
* `[NotColumn]` is used for a computed display property

---

# Summary

SQLiteXM entities are plain C# classes with attribute-based mapping.

Key points:

* Inherit from `SxmEntity`
* Use `[Table]` to define the entity and optionally choose the database
* Use `[Column]` to control mapped members
* Use `[NotColumn]` for values that should not be stored
* Use `[Rename]` to preserve data during refactoring
* Use `[Index]` and `[UniqueIndex]` to improve query performance and enforce uniqueness
* Use `[ForeignKey]` for relationships
* Use `[Trigger]` for custom database behavior
* Register entity types with `SxmDatabase.RegisterEntitiesAsync(...)`

For a basic workflow overview, see **GettingStarted.md**.
For multi-database setup, see **MULTI_DATABASES.md**.
