# SQLiteXM Cheat Sheet

Quick reference for common SQLiteXM operations.

---

## 🚀 Basic CRUD

```csharp
using var context = new SxmLinqContext();

// INSERT
context.InsertOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// UPDATE
context.UpdateOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// DELETE
context.DeleteOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// UPSERT
context.InsertOrUpdateOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## 🔍 Queries

```csharp
using var context = new SxmLinqContext();

// Get all
var all = await context.GetTable<User>().ToListAsync();

// Filter
var active = await context.GetTable<User>()
    .Where(u => u.IsActive)
    .ToListAsync();

// First or default
var user = await context.GetTable<User>()
    .FirstOrDefaultAsync(u => u.Email == "test@example.com");

// Count
var count = await context.GetTable<User>()
    .Where(u => u.IsActive)
    .CountAsync();

// Any
var exists = await context.GetTable<User>()
    .AnyAsync(u => u.Email == "test@example.com");

// Order by
var sorted = await context.GetTable<User>()
    .OrderBy(u => u.Name)
    .ToListAsync();

// Pagination
var page = await context.GetTable<User>()
    .Skip((pageNum - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// Eager loading
var orders = await context.GetTable<Order>()
    .LoadWith(o => o.Customer)
    .LoadWith(o => o.OrderItems)
    .ToListAsync();
```

---

## ⚡ Bulk Operations

```csharp
using var context = new SxmLinqContext();

// Bulk update
await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Bulk delete
await context.GetTable<Log>()
    .Where(l => l.Timestamp < DateTime.Now.AddDays(-30))
    .DeleteAsync();
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Multiple bulk operations in one transaction
await context.GetTable<User>()
    .Where(u => !u.IsActive)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();

await context.GetTable<Log>()
    .Where(l => l.UserId == userId)
    .DeleteAsync();

(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## 🛡️ Error Handling

```csharp
// Pattern 1: Simple fail-fast (80% of cases)
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Pattern 2: Catch and show friendly error
try
{
    (await context.SubmitChangesAsync()).ThrowIfFailed();
}
catch (SubmitChangesException ex)
{
    await DisplayAlert("Error", ex.Result.GetErrorSummary(), "OK");
}

// Pattern 3: Inspect without throwing
var result = await context.SubmitChangesAsync();
if (result.AnyFailed)
{
    Logger.LogError(result.GetErrorSummary());
    return;
}

// Pattern 4: Detailed logging
var result = await context.SubmitChangesAsync();
foreach (var failure in result.Failed)
{
    Logger.LogError($"{failure.Type} failed: {failure.Result?.Error?.Message}");
}
result.ThrowIfFailed();
```

---

## 📦 Batch Operations

```csharp
using var context = new SxmLinqContext();

// Batch insert (single transaction)
foreach (var item in items)
{
    context.InsertOnSubmit(item);
}
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Batch with partial success allowed
foreach (var item in items)
{
    context.InsertOnSubmit(item);
}
var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);
Console.WriteLine($"Imported {result.Succeeded.Count}/{result.TotalOperations}");
```

---

## 🔄 Transaction Modes

```csharp
// DEFAULT: FailOnFirstError
// Stops on first error, rolls back everything
var result = await context.SubmitChangesAsync();

// ContinueOnError
// Processes all operations, commits successes
var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);
```

---

## 📊 Result Inspection

```csharp
var result = await context.SubmitChangesAsync();

result.AllSucceeded      // bool: All operations succeeded
result.AnyFailed         // bool: At least one failed
result.Partial           // bool: Some succeeded, some failed
result.TotalOperations   // int: Total count
result.Succeeded         // List<ChangeAction>: Successes
result.Failed            // List<ChangeAction>: Failures
result.GetErrorSummary() // string: Human-readable summary
result.ThrowIfFailed()   // Throws if any failed
```

---

## 🎯 Common Patterns

### Save Form Data
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

### Delete with Confirmation
```csharp
private async void OnDeleteClicked(object sender, EventArgs e)
{
    var confirm = await DisplayAlert("Confirm", "Delete this item?", "Delete", "Cancel");
    if (!confirm) return;

    try
    {
        using var context = new SxmLinqContext();
        context.DeleteOnSubmit(_item);
        (await context.SubmitChangesAsync()).ThrowIfFailed();

        await Navigation.PopAsync();
    }
    catch (SubmitChangesException ex)
    {
        await DisplayAlert("Error", ex.Result.GetErrorSummary(), "OK");
    }
}
```

### Master-Detail Save
```csharp
// Save master first to get ID
using var context = new SxmLinqContext();
context.InsertOnSubmit(order);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Save details with master ID
using var context2 = new SxmLinqContext();
foreach (var item in orderItems)
{
    item.OrderId = order.id;
    context2.InsertOnSubmit(item);
}
(await context2.SubmitChangesAsync()).ThrowIfFailed();
```

### Import with Progress
```csharp
for (int i = 0; i < items.Count; i += batchSize)
{
    var batch = items.Skip(i).Take(batchSize);

    using var context = new SxmLinqContext();
    foreach (var item in batch)
        context.InsertOnSubmit(item);

    var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

    progress.Report(new Progress
    {
        Current = i + batch.Count(),
        Total = items.Count
    });
}
```

---

## ⚠️ Common Mistakes

### ❌ DON'T: Submit in a loop
```csharp
// SLOW - 1000 transactions
for (int i = 0; i < 1000; i++)
{
    using var context = new SxmLinqContext();
    context.InsertOnSubmit(new User { Name = $"User{i}" });
    await context.SubmitChangesAsync(); // ❌
}
```

### ✅ DO: Batch operations
```csharp
// FAST - 1 transaction
using var context = new SxmLinqContext();
for (int i = 0; i < 1000; i++)
{
    context.InsertOnSubmit(new User { Name = $"User{i}" });
}
await context.SubmitChangesAsync(); // ✅
```

---

### ❌ DON'T: Ignore errors
```csharp
var result = await context.SubmitChangesAsync();
// Forgot to check result or throw!
```

### ✅ DO: Handle errors
```csharp
(await context.SubmitChangesAsync()).ThrowIfFailed();
// or
var result = await context.SubmitChangesAsync();
if (result.AnyFailed) { /* handle */ }
```

---

### ❌ DON'T: Entity-based bulk updates
```csharp
// SLOW - loads all entities into memory
var users = await context.GetTable<User>().ToListAsync();
foreach (var user in users)
{
    user.Status = "Archived";
    context.UpdateOnSubmit(user);
}
```

### ✅ DO: SQL-based bulk updates
```csharp
// FAST - single SQL UPDATE statement
await context.GetTable<User>()
    .Set(u => u.Status, "Archived")
    .UpdateAsync();
```

---

## 🔗 See Also

- **Full Usage Guide**: `SQLITEXM_USAGE_PATTERNS.md`
- **API Reference**: `SUBMITCHANGES_API_SUMMARY.md`
- **Detailed Examples**: `SUBMITCHANGES_USAGE_EXAMPLES.md`
- **Test Suite**: `SQLiteXM.Tests` project

---

## 💡 Pro Tips

1. Always use `using var context = new SxmLinqContext()` to ensure disposal
2. Batch operations in a single `SubmitChangesAsync()` when possible
3. Use bulk operations (`Set().UpdateAsync()`, `DeleteAsync()`) for large datasets
4. Add `[Index]` attributes to frequently queried columns
5. Use `ContinueOnError` only for batch imports where partial success is OK
6. Always either call `.ThrowIfFailed()` or check the result
7. Log detailed errors with `result.GetErrorSummary()` and `result.Failed`
8. Test both success and failure paths in your unit tests

---

*Keep this file handy for quick reference!*
