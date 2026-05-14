# SubmitChangesAsync Usage Guide

This guide demonstrates the two `ConflictMode` options and calling patterns for `SubmitChangesAsync()`.

---

## ConflictMode Options

### 1. `FailOnFirstError` (DEFAULT)
- **Behavior**: Stops on the first failure and rolls back the entire transaction
- **Use case**: Strict atomic operations where all-or-nothing is required
- **Transaction**: Always rolled back if any operation fails

### 2. `ContinueOnError`
- **Behavior**: Continues processing all operations even when failures occur, then commits successes
- **Use case**: Batch imports where partial success is acceptable
- **Transaction**: Always commits (partial success allowed)

---

## Usage Pattern 1: Fluent Fail-Fast (Recommended for Most Cases)

Throws `SubmitChangesException` if any operation fails.

```csharp
using var context = new SxmLinqContext();

context.InsertOnSubmit(new User { Name = "Alice" });
context.UpdateOnSubmit(existingUser);
context.DeleteOnSubmit(oldUser);

// Throws on failure, continues on success
await context.SubmitChangesAsync().ThrowIfFailed();

// Code here only runs if all operations succeeded
Console.WriteLine("All changes saved successfully!");
```

### With Exception Handling

```csharp
try
{
    (await context.SubmitChangesAsync()).ThrowIfFailed();
    ShowSuccessNotification("Changes saved!");
}
catch (SubmitChangesException ex)
{
    // Access detailed failure information
    Console.WriteLine($"Save failed: {ex.Message}");
    Console.WriteLine($"Failed: {ex.Result.Failed.Count}, Succeeded: {ex.Result.Succeeded.Count}");

    // Inspect individual failures
    foreach (var failure in ex.Result.Failed)
    {
        Console.WriteLine($"  - {failure.Type} failed: {failure.Result?.Error?.Message}");
    }

    ShowErrorDialog(ex.Result.GetErrorSummary());
}
```

---

## Usage Pattern 2: Manual Inspection (Graceful Handling)

Inspect the result without throwing exceptions.

```csharp
using var context = new SxmLinqContext();

context.InsertOnSubmit(user1);
context.InsertOnSubmit(user2);
context.InsertOnSubmit(user3);

var result = await context.SubmitChangesAsync();

if (!result.AllSucceeded)
{
    // Handle failures gracefully
    Console.WriteLine($"Some operations failed: {result.Failed.Count} of {result.TotalOperations}");

    foreach (var failure in result.Failed)
    {
        var entity = failure.Entity;
        var error = failure.Result?.Error;

        Logger.LogWarning($"Failed to {failure.Type} {entity?.GetType().Name} (Id: {entity?.id}): {error?.Message}");
    }

    return; // Early exit or continue with degraded state
}

// Success path
Console.WriteLine($"All {result.Succeeded.Count} operations completed successfully!");
```

---

## Usage Pattern 3: Hybrid (Inspect + Throw)

Log details before throwing.

```csharp
var result = await context.SubmitChangesAsync();

// Log all failures before throwing
if (result.AnyFailed)
{
    foreach (var failure in result.Failed)
    {
        Logger.LogError($"{failure.Type} failed: {failure.Result?.Error?.Message}");
    }
}

// Now throw if any failures occurred
result.ThrowIfFailed(); // Throws SubmitChangesException
```

---

## Usage Pattern 4: Partial Success with ContinueOnError

Accept partial success in batch operations.

```csharp
using var context = new SxmLinqContext();

// Queue 1000 inserts
for (int i = 0; i < 1000; i++)
{
    context.InsertOnSubmit(new User { Name = $"User{i}" });
}

// Use ContinueOnError to commit successful inserts even if some fail
var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

Console.WriteLine($"Batch import complete:");
Console.WriteLine($"  Succeeded: {result.Succeeded.Count}");
Console.WriteLine($"  Failed: {result.Failed.Count}");
Console.WriteLine($"  Success rate: {result.Succeeded.Count * 100.0 / result.TotalOperations:F1}%");

if (result.Partial)
{
    Logger.LogWarning($"Partial success: {result.Succeeded.Count}/{result.TotalOperations} operations completed.");

    // Optionally retry failed operations
    foreach (var failure in result.Failed)
    {
        RetryQueue.Add(failure.Entity);
    }
}
```

---

## Usage Pattern 5: Conditional Throwing with ContinueOnError

Only throw if ALL operations failed.

```csharp
var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

// Only throw if complete failure (zero succeeded)
if (result.Failed.Count == result.TotalOperations)
{
    result.ThrowIfFailed(); // Throws because nothing succeeded
}
else if (result.Partial)
{
    Logger.LogWarning($"Partial success: {result.Succeeded.Count}/{result.TotalOperations}");
}
else
{
    Logger.LogInfo($"All {result.TotalOperations} operations succeeded!");
}
```

---

## Helper Methods on SubmitChangesResult

### `AnyFailed`
Quick boolean check for failures.

```csharp
var result = await context.SubmitChangesAsync();
if (result.AnyFailed)
{
    Console.WriteLine("At least one operation failed!");
}
```

### `TotalOperations`
Get the total count of all operations.

```csharp
var result = await context.SubmitChangesAsync();
Console.WriteLine($"Processed {result.TotalOperations} operations");
```

### `GetErrorSummary()`
Human-readable error summary.

```csharp
var result = await context.SubmitChangesAsync();
if (!result.AllSucceeded)
{
    Console.WriteLine(result.GetErrorSummary());
    // Output examples:
    // "1 operation failed (Entity: User, Id: 42): Unique constraint violation"
    // "3 of 10 operations failed. First error: Foreign key constraint failed"
}
```

### `ThrowIfFailed()`
Throws `SubmitChangesException` if any failures.

```csharp
var result = await context.SubmitChangesAsync();
result.ThrowIfFailed(); // Throws if result.AnyFailed == true
```

---

## Bulk LINQ Operations

Bulk operations work seamlessly with the transaction model.

```csharp
using var context = new SxmLinqContext();

// Queue bulk update
await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();

// Queue bulk delete
await context.GetTable<Log>()
    .Where(l => l.Timestamp < DateTime.Now.AddDays(-30))
    .DeleteAsync();

// Submit all changes atomically
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## Transaction Behavior Summary

| Mode | First Failure? | Continue Processing? | Commit? | Rollback? |
|------|----------------|----------------------|---------|-----------|
| **FailOnFirstError** (default) | Stops immediately | ❌ No | Only if all succeed | On any failure |
| **ContinueOnError** | Continues | ✅ Yes | Always | Never |

---

## Best Practices

1. **Default to `FailOnFirstError`**: Use the parameterless `SubmitChangesAsync()` for strict atomic operations
2. **Use `ThrowIfFailed()` for fail-fast**: `(await context.SubmitChangesAsync()).ThrowIfFailed()`
3. **Inspect manually for graceful handling**: Capture the result and check `AllSucceeded` or `AnyFailed`
4. **Use `ContinueOnError` sparingly**: Only for batch operations where partial success is acceptable
5. **Always log failures**: Use `GetErrorSummary()` or iterate `result.Failed` for diagnostics

---

## Complete Real-World Example

```csharp
public async Task<bool> SaveUserChangesAsync(User user, List<Order> orders)
{
    using var context = new SxmLinqContext();

    try
    {
        // Queue entity changes
        context.UpdateOnSubmit(user);

        foreach (var order in orders)
        {
            if (order.id == 0)
                context.InsertOnSubmit(order);
            else
                context.UpdateOnSubmit(order);
        }

        // Queue bulk cleanup
        await context.GetTable<Log>()
            .Where(l => l.UserId == user.id && l.Timestamp < DateTime.Now.AddMonths(-1))
            .DeleteAsync();

        // Submit atomically with fail-fast
        (await context.SubmitChangesAsync()).ThrowIfFailed();

        Logger.LogInfo($"Successfully saved user {user.id} with {orders.Count} orders");
        return true;
    }
    catch (SubmitChangesException ex)
    {
        Logger.LogError($"Failed to save changes: {ex.Message}");
        Logger.LogError($"Failed operations: {ex.Result.Failed.Count}");

        // Show user-friendly error
        ShowErrorDialog($"Could not save changes: {ex.Result.GetErrorSummary()}");
        return false;
    }
    catch (Exception ex)
    {
        Logger.LogError($"Unexpected error: {ex.Message}");
        throw;
    }
}
```
