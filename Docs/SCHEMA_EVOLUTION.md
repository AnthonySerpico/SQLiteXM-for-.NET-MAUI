# Schema Evolution

SQLiteXM creates a corresponding table for an entity the first time an entity is registered via `RegisterEntitiesAsync`. As part of this process, it creates columns, indexes, triggers, foreign keys, and other schema objects to create a schema that reflects the entity and its applied attributes.

One of the most common ORM concerns is what happens when an entity changes over time?

SQLiteXM follows a conservative, schema-first approach:

* It adds new columns when new mapped properties are added to an existing entity
* It renames columns when a property has a matching `[Rename]` history
* It drops columns when a previously mapped property is removed from an existing entity as long as the column is not used in indexes or triggers
* It adds, removes, and updates indexes and triggers as these attributes are added, removed, or changed on an existing entity

New tables are created only when you register an entity for the 
first time. SQLiteXM does not rebuild existing tables when entities change. For existing entities, SQLiteXM compares the updated entity 
model against the existing database schema and applies the changes. SQLiteXM only supports and applies changes that it considers safe.

## What Changes Are Safe?

Safe changes are those that are expressible via SQLite's supported Data Definition Language (DDL) without table rebuilds.
Changes that would require a manual DDL workaround such as creating a new table, copying data, dropping the old table, and renaming 
the new table are not supported by SQLiteXM as these are not considered safe.

Changes that can be represented by direct `ALTER TABLE` statements or can be expressed using SQLite-supported schema DDL without rebuilding a 
table are considered safe. These are the changes listed in the bullet points above.

Here, "safe" means that SQLiteXM can perform the schema change without rebuilding the table; it does not 
mean that the change is non-destructive, for example, dropping a column is a safe operation but it is destructive because it results in data loss.

## Why SQLiteXM Does Not Rebuild Tables?

SQLiteXM avoids table rebuilds because they are inherently complex, potentially destructive, and often require data transformation logic that 
cannot be reliably inferred from the schema alone. When existing data must be converted from one representation to another, there is frequently 
no universally correct transformation. The appropriate conversion may depend on application-specific business rules, data quality, or assumptions 
that SQLiteXM cannot safely ascertain.

Additionally, whether a rebuild succeeds may depend on the contents of the existing data. Existing values may violate newly introduced constraints, 
contain duplicate data that prevents the creation of unique indexes, or require conversions that cannot be performed safely or automatically.

## How Does SQLiteXM Detect Differences?

During registration, SQLiteXM reads the live table schema from SQLite and compares it to the current entity.

The key inputs are:

* The entity's mapped members and attributes. This includes indexes, triggers, foreign keys, and all column mapping attributes.
* The current database table columns reported by SQLite
* Any `[Rename]` history on properties

SQLiteXM uses that comparison to determine how the schema should be modified and applies the required DDL statements to evolve 
the schema to match the new entity model.

## Can SQLiteXM Add Columns?

Yes.

When a new mapped property appears in the entity and no matching column exists in the database, SQLiteXM adds the column with `ALTER TABLE ... ADD COLUMN`.

This is the normal path for expanding a table over time.

## Can SQLiteXM Drop Columns?

Yes.

If a column exists in the database but is no longer included in the entity model, SQLiteXM will drop the column during registration.

Note: Columns that are still referenced by indexes or triggers or are still required by other schema objects or otherwise violate SQLite's requirements for `DROP COLUMN` cannot be dropped.

That means removing a mapped property from your entity is treated as a real schema change, not just a code-only cleanup.

## Can SQLiteXM Add New Indexes?

Yes.

If an index is added to an existing entity, SQLiteXM will create the new index during registration. All index types are supported:

* Single column indexes
* Multi-column indexes
* Unique single column indexes
* Unique multi-column indexes

## Can SQLiteXM Remove Indexes?

Yes.

If an index is removed from an existing entity, SQLiteXM will drop the index during registration. All index types can be removed:

* Single column indexes
* Multi-column indexes
* Unique single column indexes
* Unique multi-column indexes


## Can SQLiteXM Modify Indexes?

Yes.

In a single column index, if the uniqueness of the index is changed, SQLiteXM will drop the existing index and create the new index during registration. For example, 
if a single column index is changed from unique to non-unique, SQLiteXM will drop the existing unique index and create the new non-unique index.


If a multi-column index is changed in any way (columns added or removed, uniqueness changed), SQLiteXM will drop the existing 
index and create the new index during registration.

### Caution!
You must be incredibly careful when changing a regular index to a unique index. This is considered a high-risk operation because it introduces a strict, retroactive 
data constraint. If the existing data contains duplicate values in the indexed column(s), the new unique index cannot be created and the registration will fail. 
You must ensure that all existing data in the indexed column(s) is unique before making this change. This change may appear to work on your development database if 
the data happens to be unique, but it may fail in production if duplicate data exists. Unless you are absolutely sure of the uniqueness of the column(s) involved in the 
index, don't attempt this type of migration.


## Can SQLiteXM Add Triggers?

Yes.

If a trigger is added to an existing entity, SQLiteXM will create the new trigger during registration.

## Can SQLiteXM Remove Triggers?

Yes.

If a trigger is removed from an existing entity, SQLiteXM will drop the trigger during registration.

## Can SQLiteXM Modify Triggers?

Yes.

If a trigger is changed in any way, SQLiteXM will drop the existing trigger and create the new trigger during registration.

## Can SQLiteXM Rename Columns?

Yes, when you tell it how.

Use the `[Rename]` attribute to rename a column and preserve all data. SQLiteXM searches the rename history in the `[Rename]` attribute from newest to oldest and renames the 
first matching existing column it finds. 

In the example below, SQLiteXM will rename the existing 'FirstName' column to 'GivenName' while preserving existing data.

```csharp
public class Customer : SxmEntity
{
    [Rename("FirstName")]
    public string GivenName { get; set; }
}
```

This is the recommended way to evolve a property name without losing data.

## Does SQLiteXM Support Dropping Tables?

Yes, but this is considered a special case and is handled differently from other schema changes.

Simply removing an entity from your code will not drop the corresponding table. Instead, you must explicitly call the `DropTableAsync` API method to drop a table.

```csharp
    // tableName: Name of the table to drop.
    // dbName: Optional database name override; uses the default database if null.
    // force: If true, executes `PRAGMA defer_foreign_keys = ON`  to prevent the drop from being blocked by active constraints.
    public static async Task DropTableAsync(string tableName, string? dbName = default, bool force = false)

    //Example usage:
    await SxmStatements.DropTableAsync("OrderHistory", force: true);
```

Asynchronously drops the specified table if it exists. The operation is performed within a transaction and may be blocked by active foreign key constraints unless `force` is specified.

## What About Type Changes?

Changing a property's CLR type or SQLite storage type is not supported.

For example, changing a `DateTime` from the default `INTEGER` to `TEXT` fundamentally changes how the value is stored and read back. That kind of change is not supported.

These types of changes would require reading existing data, converting it, and rewriting the table, which SQLiteXM does not consider safe.

## What About Foreign Key Changes?

Changes to existing foreign key definitions are not supported.

SQLite does not provide direct ALTER TABLE support for modifying foreign key constraints. Because SQLiteXM does not rebuild existing tables, foreign key definitions are considered immutable once created.


## Practical Guidance

SQLiteXM is designed to keep the database aligned with the entity model while preserving data. 

However, removing a mapped property from your entity will result in the corresponding column being dropped from the database during the next registration cycle.
This is a schema change, not just code cleanup and represents a destructive change that will result in data loss.

Of course, dropping a table using the `DropTableAsync` API method is also a destructive operation and should be used with caution.


## Supported (Safe) Schema Changes

| Schema Change | Supported by SQLiteXM | SQLite Requirement | Notes |
|---------------|------------------------|--------------------|-------|
| Add column | Yes | Always supported | Uses `ALTER TABLE ADD COLUMN` |
| Drop column | Yes | SQLite 3.35+ | Column must not be referenced by indexes or triggers |
| Rename column | Yes | SQLite 3.25+ | Uses `ALTER TABLE RENAME COLUMN` |
| Add index | Yes | Always supported | All index types supported |
| Remove index | Yes | Always supported | Drops index via `DROP INDEX` |
| Modify index | Yes | Always supported | SQLiteXM drops + recreates index |
| Add trigger | Yes | Always supported | Creates trigger via `CREATE TRIGGER` |
| Remove trigger | Yes | Always supported | Drops trigger via `DROP TRIGGER` |
| Modify trigger | Yes | Always supported | SQLiteXM drops + recreates trigger |
| Rename column via `[Rename]` | Yes | SQLite 3.25+ | Preserves data during property renames |


## Unsupported (Unsafe) Schema Changes 

| Schema Change | Supported by SQLiteXM | Reason |
|---------------|------------------------|--------|
| Change CLR property type | No | Requires rewriting and converting existing data |
| Change SQLite storage type | No | Requires table rebuild and data transformation |
| Add foreign key to existing table | No | SQLite cannot alter FK constraints |
| Remove foreign key from existing table | No | SQLite cannot drop FK constraints |
| Modify foreign key (e.g., ON DELETE) | No | Requires table rebuild |
| Change column nullability (add/remove NOT NULL) | No | SQLite cannot alter NOT NULL constraints |
| Change column default value | No | Requires table rebuild |
| Change PRIMARY KEY definition | No | Requires table rebuild |

