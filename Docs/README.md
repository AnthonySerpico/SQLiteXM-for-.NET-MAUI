# SQLiteXM Documentation

Welcome to the SQLiteXM documentation.

The documentation is organized around the way you are likely to use SQLiteXM:
start with the fundamentals, then move into the areas you need as your application
develops.

---

## 🚀 Start Here

### [Getting Started](getting-started.md)

**Start here if you are new to SQLiteXM.**

A step-by-step introduction to adding SQLiteXM to a .NET MAUI application,
defining entities, configuring the database, initializing SQLiteXM, and performing
basic database operations.

**Recommended first read.**

---

## 🧱 Build Your Data Model

Once SQLiteXM is running, learn how to define and work with your application's
data model.

### [Defining Entities](defining-entities.md)

Learn how to define SQLiteXM entities and configure tables, columns, indexes,
primary keys, foreign keys, and other entity mapping features.

**Read this when:** You are designing or modifying your application's data model.

### [Supported Data Types](supported-data-types.md)

Reference for the .NET data types supported by SQLiteXM and how they are mapped
to SQLite data types.

**Read this when:** You are deciding which property types to use in your entities.

---

## 💾 Work With Your Data

### [Working with Data](working-with-data.md)

Learn how to query, insert, update, and delete data using SQLiteXM, including
LINQ, SQL, entity persistence, and transactions.

**Read this when:** You are ready to build your application's data access logic.

---

## ⚙️ Configure Your Database

These guides explain how SQLiteXM databases are configured and initialized.
You may not need all of them when first getting started.

### [Database Configuration and Initialization](database-configuration-and-initialization.md)

Learn how SQLiteXM configures, initializes, and manages SQLite databases,
including application startup and database initialization.

**Read this when:** You need to understand the database initialization process
or customize how SQLiteXM starts up.

### [SQL Statement File](sql-statement-file.md)

Complete reference for the `SqlStatements.json` configuration file, including
database definitions and SQLite initialization options.

**Read this when:** You need to create or modify `SqlStatements.json` or configure
SQLite database behavior.

---

## 🔄 As Your Application Evolves

### [Schema Evolution](schema-evolution.md)

Learn how SQLiteXM handles changes to your entity model and evolves the
underlying SQLite schema as your application changes.

**Read this when:** You add, remove, rename, or otherwise change properties,
tables, indexes, or relationships in an existing application.

### [Multiple Databases](multiple-databases.md)

Learn how to configure and work with multiple SQLite databases within the
same application.

**Read this when:** Your application needs to maintain separate SQLite databases
for different types of data.

---

## 🎨 .NET MAUI UI Integration

### [INotifyPropertyChanged](inotifypropertychanged.md)

Learn how SQLiteXM entities integrate with .NET MAUI data binding through
`INotifyPropertyChanged`.

**Read this when:** You want SQLiteXM entities to participate directly in
MAUI UI binding and automatically notify the UI when properties change.

---

## 📚 Recommended Reading Paths

### New to SQLiteXM

Follow this path:

1. **[Getting Started](getting-started.md)**
2. **[Defining Entities](defining-entities.md)**
3. **[Working with Data](working-with-data.md)**

This is enough to get SQLiteXM running and begin building an application.

### Building a Real Application

After the basics, explore the topics relevant to your application:

- **[Supported Data Types](supported-data-types.md)** — when defining properties
- **[Database Configuration and Initialization](database-configuration-and-initialization.md)** — when customizing database setup
- **[SQL Statement File](sql-statement-file.md)** — when configuring `SqlStatements.json`
- **[INotifyPropertyChanged](inotifypropertychanged.md)** — when binding entities to the MAUI UI

### Maintaining an Existing Application

When your application begins evolving:

- **[Schema Evolution](schema-evolution.md)** — when your data model changes
- **[Multiple Databases](multiple-databases.md)** — when your application needs separate databases

---

## 🗺️ Documentation at a Glance

| Document | Purpose | When to Read |
|----------|---------|--------------|
| [Getting Started](getting-started.md) | Complete first-use walkthrough | **Start here** |
| [Defining Entities](defining-entities.md) | Entity and schema definition | When designing your data model |
| [Working with Data](working-with-data.md) | Querying and modifying data | When building data access |
| [Supported Data Types](supported-data-types.md) | .NET → SQLite type reference | When choosing entity property types |
| [Database Configuration and Initialization](database-configuration-and-initialization.md) | Database startup and configuration | When configuring initialization |
| [SQL Statement File](sql-statement-file.md) | `SqlStatements.json` reference | When configuring the statement file |
| [Schema Evolution](schema-evolution.md) | Database schema changes | When your model evolves |
| [Multiple Databases](multiple-databases.md) | Multiple SQLite databases | When one database isn't enough |
| [INotifyPropertyChanged](inotifypropertychanged.md) | MAUI binding integration | When connecting entities to the UI |

---

## Need Help Finding Something?

If you are not sure where to look:

- **"How do I get started?"** → [Getting Started](getting-started.md)
- **"How do I define my entity?"** → [Defining Entities](defining-entities.md)
- **"What data types can I use?"** → [Supported Data Types](supported-data-types.md)
- **"How do I query or save data?"** → [Working with Data](working-with-data.md)
- **"How do I configure the database?"** → [Database Configuration and Initialization](database-configuration-and-initialization.md)
- **"What goes in `SqlStatements.json`?"** → [SQL Statement File](sql-statement-file.md)
- **"What happens when my schema changes?"** → [Schema Evolution](schema-evolution.md)
- **"How do I use multiple databases?"** → [Multiple Databases](multiple-databases.md)
- **"How do entities work with MAUI binding?"** → [INotifyPropertyChanged](inotifypropertychanged.md)