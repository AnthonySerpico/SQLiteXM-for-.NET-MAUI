# SubmitChangesAsync API Summary

## Quick Reference

### Two Simple Calling Patterns

```csharp
// Pattern 1: Fail-fast with exceptions
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Pattern 2: Graceful error handling
var result = await context.SubmitChangesAsync();
if (!result.AllSucceeded) { /* handle */ }
```

---

## ConflictMode (2 Options)

| Mode | Behavior | Default? |
|------|----------|----------|
| **FailOnFirstError** | Stop on first error, rollback all | ✅ Yes |
| **ContinueOnError** | Process all operations, commit successes | ❌ No |

---

## SubmitChangesResult Properties

```csharp
result.AllSucceeded      // bool: All operations succeeded
result.AnyFailed         // bool: At least one failed
result.Partial           // bool: Some succeeded, some failed
result.TotalOperations   // int: Total count
result.Succeeded         // List<ChangeAction>: Successful operations
result.Failed            // List<ChangeAction>: Failed operations
result.GetErrorSummary() // string: Human-readable summary
result.ThrowIfFailed()   // Throws SubmitChangesException if any failed
```

---

## SubmitChangesException

Thrown by `ThrowIfFailed()` when operations fail.

```csharp
catch (SubmitChangesException ex)
{
    ex.Message              // Error summary message
    ex.Result               // Full SubmitChangesResult
    ex.Result.Failed        // List of failed operations
    ex.InnerException       // First failure's exception
}
```

---

## Common Patterns

### 1. Simple CRUD (Fail-Fast)
```csharp
using var context = new SxmLinqContext();
context.InsertOnSubmit(newEntity);
context.UpdateOnSubmit(existingEntity);
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### 2. Batch Import (Partial Success OK)
```csharp
using var context = new SxmLinqContext();
foreach (var item in items)
    context.InsertOnSubmit(item);

var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);
Console.WriteLine($"Imported {result.Succeeded.Count}/{result.TotalOperations}");
```

### 3. Bulk Operations
```csharp
using var context = new SxmLinqContext();

await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();

(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### 4. User-Friendly Error Handling
```csharp
try
{
    (await context.SubmitChangesAsync()).ThrowIfFailed();
    ShowNotification("Changes saved!");
}
catch (SubmitChangesException ex)
{
    ShowError(ex.Result.GetErrorSummary());
}
```

---

## Transaction Guarantees

| Scenario | Result |
|----------|--------|
| All operations succeed | ✅ Committed |
| Any operation fails (FailOnFirstError) | ❌ Rolled back |
| Some operations fail (ContinueOnError) | ⚠️ Partial commit |

---

## Design Principles

1. **Explicit over implicit**: No hidden magic, clear transaction boundaries
2. **Fail-fast by default**: Safe atomic behavior unless you opt into partial commits
3. **Flexible error handling**: Choose exceptions or result inspection
4. **Informative errors**: Rich diagnostics in `SubmitChangesResult` and exceptions

---

For detailed examples, see `SUBMITCHANGES_USAGE_EXAMPLES.md`.
