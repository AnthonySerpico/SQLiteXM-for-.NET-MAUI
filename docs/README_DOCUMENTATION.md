# SQLiteXM Documentation

Welcome to SQLiteXM - a powerful, transaction-first ORM for .NET MAUI and SQLite.

---

## 📚 Documentation Structure

### **Getting Started**
- [Quick Start Guide](#quick-start-guide) *(below)*
- [Cheat Sheet](SQLITEXM_CHEATSHEET.md) - One-page quick reference

### **Core Concepts**
- [SubmitChanges API Summary](SUBMITCHANGES_API_SUMMARY.md) - API overview and design principles
- [SubmitChanges Usage Examples](SUBMITCHANGES_USAGE_EXAMPLES.md) - Detailed transaction patterns
- [Usage Patterns Guide](SQLITEXM_USAGE_PATTERNS.md) - Comprehensive real-world patterns

### **Reference**
- [Test Suite](SQLiteXM.Tests/) - Working examples and test cases
- API Documentation *(auto-generated from XML comments)*

---

## 🚀 Quick Start Guide

### Installation

1. Add SQLiteXM to your .NET MAUI project
2. Initialize in your `App.xaml.cs`:

```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Initialize SQLiteXM
        await InitializeDatabaseAsync();

        MainPage = new AppShell();
    }

    private async Task InitializeDatabaseAsync()
    {
        var options = new SxmInitOptions
        {
            DatabaseFolderOverride = FileSystem.AppDataDirectory
        };

        await SxmInit.InitDbAsync("statements.json", options);

        // Register your entity schemas
        await SxmInit.RegisterSchemaAsync(
            typeof(User),
            typeof(Order),
            typeof(Product)
        );
    }
}
```

### Define Entities

```csharp
[Table]
public class User : SxmEntity
{
    [Column]
    public string Name { get; set; }

    [Column]
    [Index] // Speeds up searches
    public string Email { get; set; }

    [Column]
    public int Age { get; set; }
}
```

### Basic Operations

```csharp
// INSERT
using var context = new SxmLinqContext();
var user = new User { Name = "Alice", Email = "alice@example.com" };
context.InsertOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// QUERY
using var context = new SxmLinqContext();
var users = await context.GetTable<User>()
    .Where(u => u.Age > 18)
    .ToListAsync();

// UPDATE
using var context = new SxmLinqContext();
var user = await context.GetTable<User>()
    .FirstAsync(u => u.Email == "alice@example.com");
user.Name = "Alice Smith";
context.UpdateOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// DELETE
using var context = new SxmLinqContext();
var user = await context.GetTable<User>()
    .FirstAsync(u => u.Email == "alice@example.com");
context.DeleteOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## 🎯 Core Concepts

### Transaction-First Design

SQLiteXM uses a **unit-of-work** pattern where you:
1. Queue operations (`InsertOnSubmit`, `UpdateOnSubmit`, `DeleteOnSubmit`)
2. Execute atomically (`SubmitChangesAsync`)
3. Handle results (throw or inspect)

```csharp
using var context = new SxmLinqContext();

// Queue multiple operations
context.InsertOnSubmit(user1);
context.UpdateOnSubmit(user2);
context.DeleteOnSubmit(user3);

// All succeed or all fail (atomic)
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### Two Error Handling Patterns

**Pattern 1: Fail-Fast** *(recommended for most cases)*
```csharp
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

**Pattern 2: Inspect Result**
```csharp
var result = await context.SubmitChangesAsync();
if (result.AnyFailed)
{
    Logger.LogError(result.GetErrorSummary());
    return;
}
```

### Conflict Modes

**`FailOnFirstError` (default)**: Stop on first error, rollback everything
```csharp
var result = await context.SubmitChangesAsync(); // default mode
```

**`ContinueOnError`**: Process all operations, commit successes
```csharp
var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);
```

---

## 📖 Documentation Overview

### [Cheat Sheet](SQLITEXM_CHEATSHEET.md)
**Quick reference for everyday use.**
- Common operations (CRUD, queries, bulk)
- Error handling patterns
- Common mistakes to avoid
- Pro tips

👉 *Start here for quick lookups!*

---

### [SubmitChanges API Summary](SUBMITCHANGES_API_SUMMARY.md)
**One-page API overview.**
- Calling patterns
- ConflictMode options
- Result properties
- Design principles

👉 *Read this to understand the API design!*

---

### [SubmitChanges Usage Examples](SUBMITCHANGES_USAGE_EXAMPLES.md)
**Detailed transaction patterns.**
- Basic CRUD operations
- Transaction patterns
- Error handling strategies
- Bulk operations
- Batch processing

👉 *Read this for detailed transaction handling!*

---

### [Usage Patterns Guide](SQLITEXM_USAGE_PATTERNS.md)
**Comprehensive real-world patterns.**

**Beginner Level:**
- Basic CRUD operations
- Simple queries
- Form save patterns

**Intermediate Level:**
- Transaction patterns
- Batch imports
- Master-detail saves
- Pagination

**Advanced Level:**
- Offline sync
- Background processing
- Conflict resolution
- Performance optimization

**Testing:**
- Unit test patterns
- Integration tests
- Mocking strategies

👉 *Read this for complete real-world scenarios!*

---

## 🔥 Common Use Cases

### MAUI Form Save
```csharp
private async void OnSaveClicked(object sender, EventArgs e)
{
    try
    {
        using var context = new SxmLinqContext();

        _user.Name = NameEntry.Text;
        _user.Email = EmailEntry.Text;

        if (_user.id == 0)
            context.InsertOnSubmit(_user);
        else
            context.UpdateOnSubmit(_user);

        (await context.SubmitChangesAsync()).ThrowIfFailed();

        await DisplayAlert("Success", "Saved!", "OK");
        await Navigation.PopAsync();
    }
    catch (SubmitChangesException ex)
    {
        await DisplayAlert("Error", ex.Result.GetErrorSummary(), "OK");
    }
}
```

### Bulk Operations
```csharp
using var context = new SxmLinqContext();

// Archive inactive users
await context.GetTable<User>()
    .Where(u => u.LastLogin < DateTime.Now.AddYears(-1))
    .Set(u => u.Status, "Archived")
    .UpdateAsync();

// Delete old logs
await context.GetTable<Log>()
    .Where(l => l.Timestamp < DateTime.Now.AddDays(-30))
    .DeleteAsync();

// Execute in one transaction
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### Batch Import
```csharp
const int batchSize = 100;

for (int i = 0; i < items.Count; i += batchSize)
{
    var batch = items.Skip(i).Take(batchSize);

    using var context = new SxmLinqContext();
    foreach (var item in batch)
        context.InsertOnSubmit(item);

    var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

    Console.WriteLine($"Imported {result.Succeeded.Count}/{batch.Count()}");
}
```

---

## 🎓 Learning Path

**Day 1: Basics**
1. Read [Quick Start Guide](#quick-start-guide)
2. Browse [Cheat Sheet](SQLITEXM_CHEATSHEET.md)
3. Try basic CRUD operations

**Day 2: Transactions**
1. Read [SubmitChanges API Summary](SUBMITCHANGES_API_SUMMARY.md)
2. Understand error handling patterns
3. Try different ConflictMode options

**Week 1: Real-World Patterns**
1. Read [Usage Patterns Guide](SQLITEXM_USAGE_PATTERNS.md)
2. Implement form save/delete
3. Try batch operations

**Ongoing: Advanced Topics**
1. Explore [SubmitChanges Usage Examples](SUBMITCHANGES_USAGE_EXAMPLES.md)
2. Study test suite examples
3. Optimize performance patterns

---

## 💡 Key Principles

### 1. **Explicit over Implicit**
- You explicitly queue operations
- You explicitly submit changes
- No hidden change tracking

### 2. **Transaction-First**
- All operations are atomic by default
- Clear transaction boundaries
- Rollback on failure (unless opted out)

### 3. **Flexible Error Handling**
- Choose exceptions or result inspection
- Detailed failure information
- Human-readable error summaries

### 4. **Performance by Design**
- Batch operations in single transactions
- Bulk SQL operations for large datasets
- Index support for fast queries

### 5. **MAUI-Optimized**
- Direct integration with MAUI lifecycle
- Platform-specific database paths
- Offline-first patterns

---

## 🆚 Comparison with Other ORMs

| Feature | SQLiteXM | EF Core | Dapper | LiteDB |
|---------|----------|---------|--------|--------|
| **LINQ Support** | ✅ Full | ✅ Full | ❌ None | ✅ Full |
| **Explicit Transactions** | ✅ Yes | ⚠️ Optional | ✅ Yes | ⚠️ Optional |
| **Bulk Operations** | ✅ Built-in | ⚠️ Extension | ✅ Manual | ✅ Built-in |
| **Error Inspection** | ✅ Detailed | ❌ Poor | ❌ None | ❌ Poor |
| **Column Rename Migration** | ✅ Automatic | ⚠️ Manual | ❌ None | N/A |
| **MAUI Integration** | ✅ Purpose-built | ⚠️ Manual | ⚠️ Manual | ✅ Good |

---

## 📞 Support & Resources

- **GitHub Issues**: Report bugs or request features
- **Test Suite**: `SQLiteXM.Tests` project for working examples
- **Sample App**: `MauiApp1` project for integration examples

---

## 🤝 Contributing

Contributions welcome! See the test suite for examples of how SQLiteXM should behave.

---

**Happy coding with SQLiteXM!** 🎉
