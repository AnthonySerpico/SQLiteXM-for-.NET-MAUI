# SQLiteXM Multi-Database Configuration

## Introduction

Most SQLiteXM applications use a single database. However, SQLiteXM also supports applications that need to organize data across multiple databases.

Common scenarios include:

* Separating application data from logging data
* Storing audit information independently
* Isolating large datasets
* Organizing data by functional area

This guide explains how to configure and use multiple databases in SQLiteXM.

---

# Understanding Multiple Databases

SQLiteXM databases are defined in the application's `SqlStatements.json` configuration file, typically named:

```text
SqlStatements.json
```

The configuration file determines which databases are available to the application. This file must be included in your application package in the `Resources/Raw` folder and is read during initialization.

---

# Single-Database Configuration

Most applications only require a single database.

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

In this configuration:

* One database is defined
* The database name is `MyApp`
* It is marked as the default database

---

# Multi-Database Configuration

To use multiple databases, simply define additional database entries.

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

In this example:

| Database | Purpose                          |
| -------- | -------------------------------- |
| MyApp    | Primary application database     |
| Logging  | Logging and diagnostics database |

The default database remains `MyApp`.

---

# Default Database Behavior

Entities automatically use the default database unless another database is explicitly specified using the `Database` property of the `[Table]` attribute.

For example:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class User : SxmEntity
{
    public string? Name { get; set; }
}
```

Because no database is specified, the `User` table is created in the default database.

---

# Assigning an Entity to a Specific Database

Use the `Database` property of the `[Table]` attribute to associate an entity with a non-default database.

```csharp
[Table(IsColumnAttributeRequired = false, Database = "Logging")]
public class ApplicationLog : SxmEntity
{
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}
```

In this example:

* The `ApplicationLog` table is created in the `Logging` database
* Other entities continue to use the default database unless configured differently

---

# Registering Entities

Entity registration works exactly the same regardless of how many databases are configured or to which database each entity belongs to.

```csharp
await SxmDatabase.RegisterEntitiesAsync(
    typeof(User),
    typeof(ApplicationLog)
);
```

SQLiteXM automatically creates or updates tables in the correct database based on each entity's configuration.

---

# Querying Multiple Databases

When using `SxmTransaction`, specify the database you want to query when creating the transaction.

```csharp
using var appTransaction = new SxmTransaction("MyApp");

using var logTransaction = new SxmTransaction("Logging");
```

Each transaction operates against the specified database.

```csharp
var users = appTransaction
    .GetTable<User>()
    .ToList();

var logs = logTransaction
    .GetTable<ApplicationLog>()
    .ToList();
```

---

# Database Configuration Rules

SQLiteXM enforces the following rules:

## At Least One Database Required

The `SqlStatements.json` file must define at least one database.

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

## Only One Default Database

Exactly one database must be marked as the default database.

```json
{
  "database": "MyApp",
  "isDefault": true
}
```

## Additional Databases

You may define as many non-default databases as needed.

```json
{
  "database": "Logging",
  "isDefault": false
}
```

---

SQLiteXM does not support:

- Cross-database joins
- Cross-database foreign keys
- Cross-database LINQ queries
- Transactions spanning multiple databases

Entities that participate in relationships or are frequently queried together should generally reside in the same database.

Multiple databases are best suited for separating independent data domains such as application data, logging, caching, or archived data.

---

# Common Usage Pattern

A common approach is to separate operational data from logging data.

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

```csharp
public class Customer : SxmEntity
{
    public string? Name { get; set; }
}

[Table(Database = "Logging")]
public class ApplicationLog : SxmEntity
{
    public string? Message { get; set; }
}
```

This allows application data and logging data to remain isolated while still being managed through SQLiteXM.

---

# Best Practices

### Use a Single Database Unless You Need More

Most applications work well with a single database.
Multiple databases should be introduced only when there is a clear organizational or operational benefit.

### Keep Related Entities Together

Entities that are frequently queried together generally belong in the same database.

### Design Database Boundaries Carefully

Consider the long-term maintenance implications before splitting data across databases.

### Use Meaningful Database Names

Choose names that clearly communicate the purpose of each database.

Examples:

```text
MyApp
Logging
Audit
Analytics
```

---

# Summary

SQLiteXM supports both single-database and multi-database applications.

Key concepts:

* Databases are defined in `SqlStatements.json`
* One database is designated as the default database
* Entities automatically use the default database
* The `[Table(Database = "...")]` attribute assigns an entity to a specific database
* `SxmTransaction` can target any configured database
* Multiple databases are useful for separating data by responsibility or function

For most applications, a single default database is sufficient. Multiple databases are available when additional separation or organization is required.
