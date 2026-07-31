# Named SQL Statements via `SqlStatements.json`

SQLiteXM lets you keep your SQL out of your C# code by declaring statements in a JSON file — `SqlStatements.json` — and then referencing them from `RunStatementAsync` by name. This guide covers the JSON schema, how named statements are resolved at runtime, and how to call them from both standalone and transactional execution contexts.

> 💡 This document is the companion to [Direct SQL Query Support in SQLiteXM](SQLiteXM-SQL-Support.md). Both styles share the *same* `RunStatementAsync` API and the *same* overload matrix — only the first argument differs (a **statement name** here vs. a raw **SQL string** there). For the shared overload matrix, parameter conventions, result-type handling, and best practices, see the embedded-SQL guide.

---

## Table of Contents

- [Why Named Statements?](#why-named-statements)
- [The `SqlStatements.json` File](#the-sqlstatementsjson-file)
- [File Schema](#file-schema)
- [How the First Argument to `RunStatementAsync` Is Resolved](#how-the-first-argument-to-runstatementasync-is-resolved)
- [Multiple Databases](#multiple-databases)
- [Triggers](#triggers)
- [Standalone Execution — `SxmStatement.RunStatementAsync`](#standalone-execution--sxmstatementrunstatementasync)
- [Transactional Execution — `SxmSqlTransaction.RunStatementAsync`](#transactional-execution--sxmsqltransactionrunstatementasync)
- [Parameter Styles Supported in JSON](#parameter-styles-supported-in-json)
- [Named Statements vs. Embedded SQL](#named-statements-vs-embedded-sql)
- [Best Practices](#best-practices)

---

## Why Named Statements?

Declaring SQL in `SqlStatements.json` instead of inline in C# gives you:

- **Central location for all SQL.** One place to review, audit, or refactor the queries used by your app.
- **Separation of concerns.** C# code says *what* to run by name; the JSON file says *how* it runs.
- **Consistency across call sites.** The same logical query (e.g., `"getUser"`) always resolves to the same SQL.
- **Easier DBA/reviewer workflows.** Anyone can open one file and see every statement the app issues.
- **Cleaner source files.** Keeps large SQL statements out of C# methods.

The trade-off is a small amount of indirection: at a call site you see the name, not the SQL. Choose whichever style — named or embedded — best fits the query. Both styles can coexist in the same project and even in the same transaction.

---

## The `SqlStatements.json` File

Every SQLiteXM project supplies a `SqlStatements.json` file that describes:

1. The **databases** the app uses (with one marked as the default).
2. The **DML statements** (`insert`, `select`, `update`, `delete`) the app can execute by name.
3. Optionally, any **triggers** the schema requires.

A minimal file looks like this:

```json
{
  "databases": [
	{ "database": "appdb", "isDefault": true }
  ],
  "trigger": [],
  "insert": [],
  "select": [],
  "update": [],
  "delete": []
}
```

A more complete example is included with the library at [`SQLiteXM/SqlStatements/ExampleSqlStatements.json`](../SQLiteXM/SqlStatements/ExampleSqlStatements.json). Refer to it as a working template.

> ⚠️ **Do not put SQL comments inside a `Statement` value.** SQLiteXM passes the value to SQLite as-is, and inline comments (`-- ...` or `/* ... */`) inside the JSON strings can break parsing and statement classification. Comment your JSON at the JSON level (for example, a top-level `"_comment"` key) rather than inside SQL text.

---

## File Schema

### Top-level fields

| Field | Type | Required | Description |
|---|---|---|---|
| `databases` | array | Yes | One entry per database the app uses. See [Multiple Databases](#multiple-databases). |
| `trigger` | array | No | `CREATE TRIGGER` DDL statements to install. See [Triggers](#triggers). |
| `insert` | array | No | Named `INSERT` statements. |
| `select` | array | No | Named `SELECT` statements. |
| `update` | array | No | Named `UPDATE` statements. |
| `delete` | array | No | Named `DELETE` statements. |
| `_comment` | string | No | Free-form note; ignored by the loader. |

### Database entry

```json
{ "database": "appdb", "isDefault": true }
```

| Field | Type | Description |
|---|---|---|
| `database` | string | The name used to reference this database elsewhere (in `RunStatementAsync`'s `databaseName` argument, in `SxmSqlTransaction.Create`, and in `trigger` entries). |
| `isDefault` | bool | Exactly **one** entry must be `true`. Used when no `databaseName` is supplied at the call site. |

### DML statement entry (`insert`, `select`, `update`, `delete`)

```json
{
  "Statement Name": "getUser",
  "Table Name": "user",
  "Statement": "SELECT * FROM user WHERE doublee = @doublee LIMIT 50"
}
```

| Field | Type | Description |
|---|---|---|
| `Statement Name` | string | The lookup key passed as the first argument to `RunStatementAsync`.</br>Must be **unique** across all entries in SqlStatements.json. Statement names are matched **case-insensitively**. |
| `Table Name` | string | The table the statement targets. Used by SQLiteXM for column-name resolution and DTO mapping. |
| `Statement` | string | The SQL body. May use named parameters (`@name`) or positional parameters (`@p0`, `@p1`, …). No SQL comments. |

### Trigger entry

```json
{
  "Database": "appdb",
  "Table Name": "user",
  "Statement": "CREATE TRIGGER updateCustomer AFTER INSERT ON user BEGIN INSERT INTO customer (name, address) VALUES (new.name, new.address); END;"
}
```

| Field | Type | Description |
|---|---|---|
| `Database` | string | Which database (by name) the trigger belongs to. |
| `Table Name` | string | The table the trigger is attached to. |
| `Statement` | string | The full `CREATE TRIGGER ...` DDL. |

Triggers are DDL — they are installed by SQLiteXM at startup rather than invoked by name at a call site. See [Triggers](#triggers).

---

## How the First Argument to `RunStatementAsync` Is Resolved

The first argument to every `RunStatementAsync` overload is a `string`. SQLiteXM decides at runtime whether that string is a **statement name** or a **raw SQL statement**:

1. It first tries to look the string up as a `Statement Name` in the loaded `SqlStatements.json` registry.
2. If a match is found, the corresponding `Statement` from JSON is executed.
3. If no match is found, the string is treated as **embedded SQL** and executed as-is (see [Direct SQL Query Support](SQLiteXM-SQL-Support.md)).

The same overload set works for both cases — the parameter shape (DTO / `Dictionary` / positional `List<object>`) and result shape (typed / raw dictionaries) are chosen exactly the same way.

> 💡 Because resolution is case-sensitive, `"getUser"` and `"GetUser"` are *not* the same name. If neither matches a JSON entry, both fall through to embedded-SQL handling, which is unlikely to be what you want. Keep the casing consistent between the JSON file and the call site.

> 💡 Statement names are loaded once during initialization into an in-memory lookup table. Resolving a statement name does not reread SqlStatements.json.

---

## Multiple Databases

`databases` may contain more than one entry. Exactly one **must** be marked `isDefault: true`; additional entries are optional and marked `isDefault: false`.

```json
"databases": [
  { "database": "appdb",  "isDefault": true },
  { "database": "logdb",  "isDefault": false }
]
```

- **Default database.** When you call `SxmStatement.RunStatementAsync(...)` without a `databaseName`, or `SxmSqlTransaction.Create()` without an argument, the default database is used.
- **Named database.** Pass the `database` string to target a specific one:

  ```csharp
  await SxmStatement.RunStatementAsync<UserDto>("getUser", parameters, databaseName: "logdb");

  await using SxmSqlTransaction tx = SxmSqlTransaction.Create("logdb");
  ```

- **Trigger association.** Trigger entries pick their database via the `Database` field on the entry itself.

Statement names are **not** namespaced by database — a `Statement Name` is looked up in a single registry and the `databaseName` argument controls only *which* database it runs against. Keep names unique across your project.

---

## Triggers

Trigger entries are `CREATE TRIGGER` DDL statements that SQLiteXM installs on the associated database during startup. They are not invoked by name from your C# code — SQLite fires them automatically in response to the `INSERT`, `UPDATE`, or `DELETE` they are bound to.

```json
"trigger": [
  {
	"Database": "appdb",
	"Table Name": "user",
	"Statement": "CREATE TRIGGER updateCustomer AFTER INSERT ON user BEGIN INSERT INTO customer (name, address) VALUES (new.name, new.address); END;"
  }
]
```

Notes:

- Use `CREATE TRIGGER` (not `CREATE TRIGGER IF NOT EXISTS`) or the equivalent form your schema-management workflow expects.
- Because triggers run inside SQLite as part of the surrounding statement, they participate automatically in any active `SxmSqlTransaction`.
- Triggers are the only entries where the key is `Database` (capitalized) rather than `Statement Name` — they identify themselves by the DDL they contain and the database they belong to.

---

## Standalone Execution — `SxmStatement.RunStatementAsync`

Once a statement is declared in `SqlStatements.json`, calling it looks identical to calling embedded SQL — just pass the `Statement Name` instead of a SQL string.

Given this JSON entry:

```json
{
  "Statement Name": "getUser",
  "Table Name": "user",
  "Statement": "SELECT * FROM user WHERE doublee = @doublee LIMIT 50"
}
```

You can call it with any of the same overloads used for embedded SQL:

**Dictionary parameters → typed results**

```csharp
Dictionary<string, object?> parameters = new Dictionary<string, object?>
{
	{ "doublee", 3.14 }
};

List<UserDto> users =
	await SxmStatement.RunStatementAsync<UserDto>("getUser", parameters);
```

**DTO parameters → typed results**

```csharp
public class GetUserParams
{
	public double doublee { get; set; }
}

GetUserParams parameters = new GetUserParams { doublee = 3.14 };

List<UserDto> users =
	await SxmStatement.RunStatementAsync<GetUserParams, UserDto>("getUser", parameters);
```

**Positional parameters (when the JSON uses `@p0`, `@p1`, …)**

Given:

```json
{
  "Statement Name": "deleteUser",
  "Table Name": "user",
  "Statement": "DELETE FROM user WHERE doublee = @p0"
}
```

Call it with a `List<object>`:

```csharp
await SxmStatement.RunStatementAsync("deleteUser", new List<object> { 3.14 });
```

**Targeting a non-default database**

```csharp
List<UserDto> archived =
	await SxmStatement.RunStatementAsync<UserDto>("getUser", parameters, "logdb");
```

See the [Overload Matrix](SQLiteXM-SQL-Support.md#overload-matrix) in the embedded-SQL guide for the full set of six overload shapes — they apply identically here.

---

## Transactional Execution — `SxmSqlTransaction.RunStatementAsync`

Named statements can be executed inside an `SxmSqlTransaction` exactly like embedded SQL. The transaction is tied to a specific database at creation time; the statements you call inside it must exist in `SqlStatements.json` (or be embedded SQL) and must be compatible with that database.

```csharp
Dictionary<string, object?> insertParams = new Dictionary<string, object?>
{
	{ "intt",      1 },
	{ "longg",     100L },
	{ "floatt",    1.5f },
	{ "doublee",   3.14 },
	{ "decimall",  9.99m },
	{ "booll",     true },
	{ "address",   "123 Main St" },
	{ "name",      "Alice" },
	{ "datetimee", DateTime.UtcNow }
};

Dictionary<string, object?> selectParams = new Dictionary<string, object?>
{
	{ "doublee", 3.14 }
};

await using SxmSqlTransaction tx = SxmSqlTransaction.Create("appdb");

await tx.RunStatementAsync("insertUser", insertParams);

List<UserDto> users =
	await tx.RunStatementAsync<UserDto>("getUser", selectParams);

// Auto-commit on dispose (no exceptions were thrown).
```

You can freely mix named statements and embedded SQL inside the same transaction:

```csharp
await using SxmSqlTransaction tx = SxmSqlTransaction.Create("appdb");

// Named statement from SqlStatements.json.
await tx.RunStatementAsync("insertUser", insertParams);

// Embedded SQL — resolved as raw SQL because no Statement Name matches.
await tx.RunStatementAsync(
	"UPDATE user SET address = @addr WHERE name = @name",
	new Dictionary<string, object?> { { "addr", "456 Elm St" }, { "name", "Alice" } });
```

All of the transactional behavior described in the embedded-SQL guide applies — see [Transactional Execution](SQLiteXM-SQL-Support.md#transactional-execution--sxmsqltransactionrunstatementasync) for auto-commit / auto-rollback semantics, the "first exception short-circuits the rest" rule, and the difference between `Create` and `CreateAsync`.

---

## Parameter Styles Supported in JSON

The `Statement` value in a JSON entry may use either parameter style — pick whichever suits the query. The example file demonstrates both:

**Named parameters**

```json
{
  "Statement Name": "getUserNameValue",
  "Table Name": "user",
  "Statement": "SELECT * FROM user WHERE doublee = @doublee LIMIT 50"
}
```

Bind by property name (DTO) or dictionary key (`Dictionary<string, object?>`). Names are case-sensitive and must match the `@name` in the SQL without the leading `@`.

**Positional parameters**

```json
{
  "Statement Name": "getUser",
  "Table Name": "user",
  "Statement": "SELECT * FROM user WHERE doublee = @p0 LIMIT 50"
}
```

Bind by ordinal via a `List<object>` in the order `@p0`, `@p1`, `@p2`, …

Named parameters are usually preferable because they are self-documenting and order-independent. Positional parameters keep short queries concise. Both are fully supported and can coexist across different entries in the same file.

For the full list of supported CLR-to-SQLite type bindings and how to pass `NULL`, see [Working with Parameters](SQLiteXM-SQL-Support.md#working-with-parameters) in the embedded-SQL guide.

---

## Named Statements vs. Embedded SQL

Both styles use the exact same `RunStatementAsync` overloads. Use this table to decide which to reach for at a given call site — you don't have to pick one for the whole project.

| Situation | Prefer |
|---|---|
| Query is stable, reused from many call sites, or belongs with the schema. | **Named statement** |
| Query is dynamic (built at runtime from user input, feature flags, etc.). | **Embedded SQL** |
| You want a single audit surface for every query the app can issue. | **Named statement** |
| One-off diagnostic or migration query. | **Embedded SQL** |
| Query is short, tightly coupled to its call site, and unlikely to be reused. | Either — pick for readability. |

You can mix both in a single transaction; the resolution logic is per call, not per transaction.

---

## Best Practices

### 1. Keep `Statement Name` values unique and descriptive

Names live in a single flat namespace. Use verbs and clear nouns (`insertUser`, `getUserByEmail`, `deleteExpiredSessions`) rather than terse codes.

### 2. Match parameter names in JSON to DTO property names exactly

DTO property → SQL parameter mapping is case-sensitive and drops the leading `@`. Rename either side carefully; the compiler cannot catch mismatches for named-statement calls where the SQL lives in JSON.

### 3. Never place SQL comments inside a `Statement` value

`-- ...` and `/* ... */` inside the JSON string can confuse SQLite and SQLiteXM's statement classification. Put explanatory notes at the JSON level (a `_comment` key) instead.

### 4. One database entry marked `isDefault: true`

Exactly one — no more, no fewer. Additional databases are opt-in and use `isDefault: false`.

### 5. Prefer typed results

Just like with embedded SQL, `RunStatementAsync<TResult>(...)` returns a `List<TResult>` and eliminates manual casting. See [Result Type Handling](SQLiteXM-SQL-Support.md#result-type-handling).

### 6. Reach for a transaction whenever two or more named statements must succeed together

`SxmSqlTransaction` works with named statements exactly as it works with embedded SQL — see [Transactional Execution](SQLiteXM-SQL-Support.md#transactional-execution--sxmsqltransactionrunstatementasync).

### 7. When you have multiple databases, pass `databaseName` explicitly

Relying on the default is fine for the primary database; for every other database, name it at the call site (or at `SxmSqlTransaction.Create`) so cross-database mistakes are impossible.

---

## Summary

- Declare reusable SQL in `SqlStatements.json` under `insert` / `select` / `update` / `delete`, keyed by `Statement Name`.
- Pass that `Statement Name` as the first argument to `RunStatementAsync` — everything else (overloads, parameter styles, result mapping, transactions) behaves exactly as documented in [Direct SQL Query Support in SQLiteXM](SQLiteXM-SQL-Support.md).
- Configure one default database (and optionally others) via the `databases` array. Use `databaseName` at the call site to target a non-default database.
- Declare `CREATE TRIGGER` DDL in the `trigger` array; SQLiteXM installs triggers at startup, and SQLite fires them automatically.
- Named statements and embedded SQL are interchangeable at any call site — pick the style that best fits each query.

For entity-oriented data access and LINQ, see the companion guides:

- [Getting Started](GettingStarted.md)
- [LINQ Support](SQLiteXM-LINQ-Support.md)
- [Query Support](SQLiteXM-Query-Support.md)
