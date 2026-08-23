# SQLiteXM Supported Data Types



SQLiteXM supports a select set of C# data types and maps them to SQLite storage types during 
schema creation, parameter binding, and result materialization.

This guide explains:

* Which C# types SQLiteXM supports
* The default SQLite storage type used for each type
* Which types can be overridden with a different storage type
* Short examples for each supported type

For a broader overview of entity design, see the **[Defining Entities](./defining-entities.md)** guide.

---

## How Type Mapping Works

SQLiteXM determines the SQLite storage type from the C# property type when entities are registered.

At save time, SQLiteXM converts entity property values into values that SQLite can store.
```text
C# Property type
      ↓
SQLiteXM type mapping
      ↓
SQLite storage type
      ↓
SQLite database
```

At read time, SQLiteXM converts stored values back into C# types.
```text
SQLite database
      ↓
SQLite storage type
      ↓
SQLiteXM type mapping
      ↓
C# Property type
```
In practice, this means:

* Most numeric types map to `INTEGER` or `REAL`
* `string` maps to `TEXT`
* `byte[]` maps to `BLOB`
* `Guid` defaults to `BLOB` but can be stored as `TEXT`
* Specialized types such as `DateTime`, `DateOnly`, `TimeOnly`, `TimeSpan`, `DateTimeOffset`, use specific storage strategies

SQLite `INTEGER` is a 64-bit signed value and maps to C# `long`. Smaller integral types such as `int`, `short`, and `byte` are also stored using `INTEGER`.

SQLiteXM does not automatically map enums or complex types. These types must be handled using a 
converter or some other explicit mapping strategy.

---

## Supported Types Overview

| C# Data Type | Default SQLite Storage Type | Override Storage Type |
|---|---|---|
| `string` | `TEXT` | None |
| `decimal` | `TEXT` | None |
| `ulong` | `TEXT` | None |
| `Guid` | `BLOB` | `TEXT` |
| `DateTime` | `INTEGER` | `TEXT` |
| `DateTimeOffset` | `INTEGER` | `TEXT` |
| `DateOnly` | `INTEGER` | `TEXT` |
| `TimeOnly` | `INTEGER` | `TEXT` |
| `TimeSpan` | `INTEGER` | `TEXT` |
| `bool` | `INTEGER` | None |
| `byte` | `INTEGER` | None |
| `sbyte` | `INTEGER` | None |
| `short` | `INTEGER` | None |
| `ushort` | `INTEGER` | None |
| `int` | `INTEGER` | None |
| `uint` | `INTEGER` | None |
| `long` | `INTEGER` | None |
| `float` | `REAL` | None |
| `double` | `REAL` | None |
| `byte[]` | `BLOB` | None |

## Nullable Types

SQLiteXM also supports nullable forms of the same C# types, such as:

* `int?`
* `DateTime?`
* `Guid?`
* `decimal?`

Nullable values are stored as `NULL` when the property value is `null`.

---

## Type-by-Type Examples

### string

`string` values are stored as `TEXT`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Person : SxmEntity
{
	public string Name { get; set; } = string.Empty;
}
```

### decimal

`decimal` values are stored as `TEXT` to preserve precision. SQLite REAL cannot safely represent many decimal values.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Product : SxmEntity
{
	public decimal Price { get; set; }
}
```

### ulong

`ulong` values are stored as fixed-width, zero-padded `TEXT` because SQLite `INTEGER` 
cannot represent the entire `ulong` range without loss of precision.

Zero padding is what preserves lexical sort order for numeric strings.
With fixed width and zero padding, string order matches numeric order.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class SequenceItem : SxmEntity
{
	public ulong SequenceNumber { get; set; }
}
```

### Guid

`Guid` values are stored as `BLOB` by default. Guid values stored as BLOB use the 16‑byte binary representation.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Customer : SxmEntity
{
	public Guid PublicId { get; set; }
}
```

You can override `Guid` to `TEXT`.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public Guid PublicId { get; set; }
```

When SQLiteXM stores a `Guid` as `TEXT`, it uses the standard .NET canonical GUID string format:

- `8-4-4-4-12` hexadecimal groups
- hyphen-separated
- same as `Guid.ToString()`

Example:

- `3f2504e0-4f89-11d3-9a0c-0305e82c3301`

### DateTime

`DateTime` values are stored as `INTEGER` by default. SQLiteXM stores them as .NET ticks.

When read back, the value is reconstructed from those ticks.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class AuditEntry : SxmEntity
{
	public DateTime CreatedOn { get; set; }
}
```

You can override `DateTime` to `TEXT` to store an ISO 8601 string.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateTime CreatedOn { get; set; }
```

### DateTimeOffset

`DateTimeOffset` values are stored as `INTEGER` by default using UTC .NET ticks. This preserves the instant represented by the value, 
but does not preserve the original offset.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Event : SxmEntity
{
	public DateTimeOffset StartTime { get; set; }
}
```

You can override the `DateTimeOffset` storage type and store it as TEXT using the ISO 8601 round-trip format. 
This preserves both the instant and the original offset.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateTimeOffset StartTime { get; set; }
```

### DateOnly

`DateOnly` values are stored as `INTEGER` by default. SQLiteXM stores them as the number of days since the Unix epoch (`1970-01-01`).

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Holiday : SxmEntity
{
	public DateOnly Date { get; set; }
}
```

You can override `DateOnly` to `TEXT`.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateOnly Date { get; set; }
```

### TimeOnly

`TimeOnly` values are stored as `INTEGER` by default. SQLiteXM stores them as the number of .NET ticks since midnight, 
preserving the full 100-nanosecond precision of TimeOnly.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class ScheduleItem : SxmEntity
{
	public TimeOnly StartsAt { get; set; }
}
```

You can override `TimeOnly` to `TEXT`.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public TimeOnly StartsAt { get; set; }
```

### TimeSpan

`TimeSpan` values are stored as `INTEGER` by default. SQLiteXM stores them as the total number of ticks.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class TaskRun : SxmEntity
{
	public TimeSpan Duration { get; set; }
}
```

You can override `TimeSpan` to `TEXT`.

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public TimeSpan Duration { get; set; }
```

### bool

`bool` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class FeatureFlag : SxmEntity
{
	public bool IsEnabled { get; set; }
}
```

### byte

`byte` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class PacketHeader : SxmEntity
{
	public byte Version { get; set; }
}
```

### sbyte

`sbyte` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class SignedCounter : SxmEntity
{
	public sbyte Offset { get; set; }
}
```

### short

`short` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Sample : SxmEntity
{
	public short Priority { get; set; }
}
```

### ushort

`ushort` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class PortInfo : SxmEntity
{
	public ushort PortNumber { get; set; }
}
```

### int

`int` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class UserProfile : SxmEntity
{
	public int Age { get; set; }
}
```

### uint

`uint` values are stored as `INTEGER`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Counter : SxmEntity
{
	public uint Value { get; set; }
}
```

### long

`long` values are stored as `INTEGER`.

SQLite `INTEGER` is a 64-bit signed value and maps to C# `long`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class LogRecord : SxmEntity
{
	public long RecordNumber { get; set; }
}
```

### float

`float` values are stored as `REAL`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Reading : SxmEntity
{
	public float Temperature { get; set; }
}
```

### double

`double` values are stored as `REAL`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class Measurement : SxmEntity
{
	public double Value { get; set; }
}
```

### byte[]

`byte[]` values are stored as `BLOB`.

```csharp
[Table(IsColumnAttributeRequired = false)]
public class FileChunk : SxmEntity
{
	public byte[] Data { get; set; } = Array.Empty<byte>();
}
```

---

## Storage Overrides That Are Supported

Only a few C# types support an alternate SQLite storage type.

### Supported overrides

| C# Data Type | Default Storage | Supported Override |
|---|---|---|
| `Guid` | `BLOB` | `TEXT` |
| `DateTime` | `INTEGER` | `TEXT` |
| `DateTimeOffset` | `INTEGER` | `TEXT` |
| `DateOnly` | `INTEGER` | `TEXT` |
| `TimeOnly` | `INTEGER` | `TEXT` |
| `TimeSpan` | `INTEGER` | `TEXT` |

## Example override

```csharp
[Column(DataType = SQLiteXM.DataType.Text)]
public DateTime CreatedOn { get; set; }
```

This stores the `DateTime` as an ISO 8601 string instead of ticks.

## Types without overrides

These types do not currently support a different SQLite storage type through the `DataType` override:

* `string`
* `decimal`
* `ulong`
* `bool`
* `byte`
* `sbyte`
* `short`
* `ushort`
* `int`
* `uint`
* `long`
* `float`
* `double`
* `byte[]`

---

## Practical Recommendations

### Use the default mapping unless you need a specific format

The default mapping is usually the best choice.

### Override only when needed

Use `DataType = SQLiteXM.DataType.Text` only when you want a human-readable or interoperable representation for a supported type.

### Keep related data consistent

If multiple entities use the same C# type for the same kind of data, consider keeping the same storage representation across the application.

---

## Summary

SQLiteXM supports common numeric, text, binary, and date/time C# types.

Key points:

* Most primitive numeric types map to `INTEGER` or `REAL`
* SQLite `INTEGER` is a 64-bit signed value and maps to C# `long`
* `string` maps to `TEXT`
* `byte[]` maps to `BLOB`
* `Guid` defaults to `BLOB` but can be stored as `TEXT`
* `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and `TimeSpan` default to `INTEGER` but can be stored as `TEXT`
* Nullable versions of supported types are also supported

For entity modeling examples, see [DEFINING_ENTITIES.md](./DEFINING_ENTITIES.md)
