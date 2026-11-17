# SQLiteXM Loading Related Data

## Understanding Related Data

Most applications store data that is connected. An order belongs to a customer. A book has a
publisher. In SQLiteXM you express that connection with a `[ForeignKey]` property, which stores
the `id` of the related row.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Book : SxmEntity
{
	public string? Title { get; set; }

	[ForeignKey("Publisher")]
	public long? PublisherId { get; set; }
}
```

This works, but it only gives you a number. When you load a `Book`, you get `PublisherId = 7`.
To show the publisher's name on screen, you still have to go and fetch publisher 7 yourself.

`LoadWith` solves that. It tells SQLiteXM to fetch the related rows at the same time as the main
rows, so the related data is already there when the query finishes.

---

## Defining a Navigation Property

Before you can use `LoadWith`, you need to define a property that holds the related entity.
This is called a **navigation property**. It is not stored in the database. It simply holds the
related entity in memory, giving your code somewhere to read it from.

A navigation property has two requirements:

* Its type is the related entity
* It is marked `[NotColumn]`

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Publisher : SxmEntity
{
	public string? PublisherName { get; set; }
	public string? Country { get; set; }
}

[Table(IsColumnAttributeRequired = false)]
public class Book : SxmEntity
{
	public string? Title { get; set; }

	[ForeignKey("Publisher")]
	public long? PublisherId { get; set; }

	[NotColumn]
	public Publisher? Publisher { get; set; } // The navigation property
}
```

The two properties do different jobs:

| Property | Marked | Purpose |
|---|---|---|
| `PublisherId` | `[ForeignKey]` | Stored in the database. Holds the `id` of a row in the `Publisher` table. |
| `Publisher` | `[NotColumn]` | Not stored in the database. Holds a `Publisher` object in memory. |

`[NotColumn]` is required here. Without it, SQLiteXM would try to create a database column to hold
a whole `Publisher` object, which is not something SQLite can store.

> ✏️ **Note:** Make the foreign key nullable (`long?`) when the relationship is optional. SQLite
> allows a `NULL` foreign key, but rejects one that points at a row that does not exist.

---

## Using LoadWith

Call `LoadWith` on the table and pass the navigation property you want filled in.

```csharp
await using (SxmTransaction ctx = new SxmTransaction("MyDatabase"))
{
	SxmTable<Book> table = ctx.GetTable<Book>();

	List<Book> books = await table
		.LoadWith(b => b.Publisher)
		.ToListAsync();

	foreach (Book book in books)
	{
		// Publisher is already loaded. No second query needed.
		Console.WriteLine($"{book.Title} - {book.Publisher?.PublisherName}");
	}
}
```

Without `LoadWith`, `book.Publisher` would be `null`, and you would need a separate database call
for every book to fill it in.

### Loading More Than One Relationship

Pass several navigation properties in a single call.

```csharp
List<Book> books = await table
	.LoadWith(b => b.Publisher, b => b.Author)
	.ToListAsync();
```

You can also chain the calls, which does the same thing.

```csharp
List<Book> books = await table
	.LoadWith(b => b.Publisher)
	.LoadWith(b => b.Author)
	.ToListAsync();
```

### Combining with Other LINQ Operations

LoadWith works alongside `Where`, `OrderBy`, and other LINQ operators. You can call it before or after these operators, as long as you apply it before executing the query.

```csharp
// LoadWith before filtering and sorting
List<Book> books = await table
    .LoadWith(b => b.Publisher)
    .Where(b => b.PublisherId != null)
    .OrderBy(b => b.Title)
    .ToListAsync();

// LoadWith after filtering and sorting
List<Book> books = await table
    .Where(b => b.PublisherId != null)
    .OrderBy(b => b.Title)
    .LoadWith(b => b.Publisher)
    .ToListAsync();
```

---

## What to Expect

### A Missing Related Row Leaves the Property Null

If the foreign key is `NULL`, or points at a row that no longer exists, the navigation property
stays `null`. `LoadWith` does not throw, and the main row is still returned.

Always check for `null` when reading a navigation property:

```csharp
string publisherName = book.Publisher?.PublisherName ?? "Unknown";
```

### Keeping Only Rows That Have a Match

Because unmatched rows are still returned, a query for books returns the ones with no publisher
too. When you only want rows that have a related row, filter the navigation property for `null`:

```csharp
List<Book> books = await table
	.LoadWith(b => b.Publisher)
	.Where(b => b.Publisher != null)
	.ToListAsync();
```

This runs as part of the database query, so unmatched rows are never fetched.

---

## One Foreign Key Per Table

`LoadWith` supports **at most one foreign key to a given table** per entity.

This entity is fine, because its two foreign keys point at two different tables:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Book : SxmEntity
{
	[ForeignKey("Publisher")]
	public long? PublisherId { get; set; }

	[NotColumn]
	public Publisher? Publisher { get; set; }

	[ForeignKey("Author")]          // different table, no problem
	public long? AuthorId { get; set; }

	[NotColumn]
	public Author? Author { get; set; }
}
```

This entity is **not** supported by `LoadWith`, because both foreign keys point at the same
`Person` table:

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Report : SxmEntity
{
	[ForeignKey("Person")]
	public long? AuthorId { get; set; }

	[ForeignKey("Person")]          // second foreign key to the same table
	public long? ReviewerId { get; set; }

	[NotColumn]
	public Person? Author { get; set; }

	[NotColumn]
	public Person? Reviewer { get; set; }
}
```

### Why

`LoadWith` works out which foreign key feeds a navigation property by looking at the property's
**type**. The `Author` and `Reviewer` properties are both of type `Person`, so there is no reliable
way to tell which column belongs to which property. `Reviewer` could end up loaded from `AuthorId`.

Rather than let that happen silently, SQLiteXM throws an `ArgumentException` that names the entity,
the property, and every foreign key involved:

```
LOADWITH ERROR on entity 'Report': 'Author' cannot be eagerly loaded because
'Report' declares 2 foreign keys referencing 'Person' (AuthorId, ReviewerId).
```

The error is raised immediately when you call `LoadWith`, not later during the query.

> ✏️ **Note:** This restriction applies only to `LoadWith`. Multiple foreign keys to the same table
> are fully supported for schema creation, referential integrity, storing and reading values, and
> explicit joins. It is only automatic eager loading that cannot be used.

### What to Do Instead

Load the two tables and match them in code:

```csharp
await using (SxmTransaction ctx = new SxmTransaction("MyDatabase"))
{
	List<Report> reports = await ctx.GetTable<Report>().ToListAsync();
	List<Person> people = await ctx.GetTable<Person>().ToListAsync();

	foreach (Report report in reports)
	{
		report.Author = people.FirstOrDefault(p => p.id == report.AuthorId);
		report.Reviewer = people.FirstOrDefault(p => p.id == report.ReviewerId);
	}
}
```

You stay in control of which column feeds which property, so there is no ambiguity.

---

## Error Messages

`LoadWith` validates your expression straight away, so mistakes show up at the call rather than
deep inside a query. Every message names the entity, explains the cause, and suggests a fix.

| Message contains | Cause | Fix |
|---|---|---|
| `is not a navigation property` | The property is a mapped column, or has no association | Pass a `[NotColumn]` property whose type is the related entity |
| `has no navigation properties` | The entity has no navigation property at all | Add a `[ForeignKey]` property and a matching `[NotColumn]` property |
| `declares N foreign keys referencing` | Two or more foreign keys point at the same table | Use an explicit join, as shown above |
| `is not a navigation property selector` | The expression is not a simple property access | Use the form `x => x.RelatedEntity` |

The message also lists the navigation properties that **are** available on the entity, which makes
a typo easy to spot.

---

## Checklist

Use this when a navigation property comes back `null` unexpectedly:

* The related entity inherits from `SxmEntity`
* A `[ForeignKey]` property holds the related row's `id`
* A `[NotColumn]` property of the related entity's type exists
* Both entity types are registered with `SxmDatabase.StartInitialization(...)`
* `LoadWith` was called on the query you actually awaited
* The foreign key value matches a row that exists

---

## Summary

`LoadWith` fetches related entities along with the main query, so you avoid a separate database
call for every row.

Key points:

* Define a `[ForeignKey]` property for storage and a `[NotColumn]` property for the related entity
* Call `LoadWith(x => x.RelatedEntity)` to populate it
* Pass several properties at once, or chain the calls
* A missing related row leaves the property `null` rather than throwing
* Filter with `Where(x => x.RelatedEntity != null)` to keep only rows that have a match
* An entity may have at most one foreign key per target table when using `LoadWith`

For entity and attribute details, see [Defining Entities](./defining-entities.md).

For general querying, see [LINQ Queries](./linq-queries.md).

For exception types and recovery, see [Error Handling](./error-handling.md).
