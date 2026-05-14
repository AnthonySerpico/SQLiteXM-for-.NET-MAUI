# SQLiteXM Usage Patterns & Best Practices

This document provides real-world usage patterns for SQLiteXM, organized by complexity and use case.

---

## Table of Contents

1. [Basic CRUD Operations](#basic-crud-operations)
2. [Transaction Patterns](#transaction-patterns)
3. [Error Handling Strategies](#error-handling-strategies)
4. [Bulk Operations](#bulk-operations)
5. [Batch Processing & Imports](#batch-processing--imports)
6. [Background Sync & Offline Apps](#background-sync--offline-apps)
7. [User-Facing Applications](#user-facing-applications)
8. [Advanced Query Patterns](#advanced-query-patterns)
9. [Testing Patterns](#testing-patterns)
10. [Performance Optimization](#performance-optimization)

---

## Basic CRUD Operations

### Simple Insert

```csharp
using var context = new SxmLinqContext();

var user = new User { Name = "Alice", Email = "alice@example.com" };
context.InsertOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();

Console.WriteLine($"Inserted user with ID: {user.id}");
```

### Simple Update

```csharp
using var context = new SxmLinqContext();

var user = await context.GetTable<User>()
    .FirstAsync(u => u.Email == "alice@example.com");

user.Name = "Alice Smith";
context.UpdateOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### Simple Delete

```csharp
using var context = new SxmLinqContext();

var user = await context.GetTable<User>()
    .FirstAsync(u => u.Email == "alice@example.com");

context.DeleteOnSubmit(user);
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

### Multiple Operations (Atomic)

```csharp
using var context = new SxmLinqContext();

// Queue multiple operations
context.InsertOnSubmit(new User { Name = "Bob" });
context.UpdateOnSubmit(existingUser);
context.DeleteOnSubmit(oldUser);

// All succeed or all fail (atomic)
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## Transaction Patterns

### Explicit Transaction with Multiple Contexts

```csharp
using var transaction = SxmTransaction.Create();

try
{
    // Insert user
    var user = new User { Name = "Charlie" };
    await user.SaveAsync(transaction);

    // Insert related orders
    foreach (var item in cartItems)
    {
        var order = new Order { UserId = user.id, ProductId = item.ProductId };
        await order.SaveAsync(transaction);
    }

    // Commit all
    await transaction.CommitTransactionAsync();
}
catch
{
    await transaction.RollbackTransactionAsync();
    throw;
}
```

### Context-Based Transaction (Recommended)

```csharp
using var context = new SxmLinqContext();

// Create user
var user = new User { Name = "David", Email = "david@example.com" };
context.InsertOnSubmit(user);

// We don't know the ID yet, so submit first to get it
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Now create related orders in a new context
using var context2 = new SxmLinqContext();
foreach (var item in cartItems)
{
    context2.InsertOnSubmit(new Order 
    { 
        UserId = user.id,  // Now we have the ID
        ProductId = item.ProductId,
        Quantity = item.Quantity
    });
}
(await context2.SubmitChangesAsync()).ThrowIfFailed();
```

### Transaction with Bulk Operations

```csharp
using var context = new SxmLinqContext();

// Queue entity operations
context.InsertOnSubmit(newUser);
context.UpdateOnSubmit(existingUser);

// Queue bulk operations
await context.GetTable<Log>()
    .Where(l => l.Timestamp < DateTime.Now.AddDays(-30))
    .DeleteAsync();

await context.GetTable<User>()
    .Where(u => u.IsActive == false && u.LastLogin < DateTime.Now.AddYears(-1))
    .Set(u => u.Status, "Archived")
    .UpdateAsync();

// All operations execute in one transaction
(await context.SubmitChangesAsync()).ThrowIfFailed();
```

---

## Error Handling Strategies

### Pattern 1: Simple Fail-Fast (80% of cases)

```csharp
public async Task SaveUserAsync(User user)
{
    using var context = new SxmLinqContext();
    context.InsertOnSubmit(user);
    (await context.SubmitChangesAsync()).ThrowIfFailed();
}
```

### Pattern 2: User-Friendly Error Messages

```csharp
public async Task<bool> SaveUserAsync(User user)
{
    using var context = new SxmLinqContext();

    try
    {
        context.InsertOnSubmit(user);
        (await context.SubmitChangesAsync()).ThrowIfFailed();
        return true;
    }
    catch (SubmitChangesException ex)
    {
        // Show friendly message to user
        await DisplayAlert("Save Failed", ex.Result.GetErrorSummary(), "OK");
        return false;
    }
}
```

### Pattern 3: Detailed Logging

```csharp
public async Task SaveChangesWithLoggingAsync(SxmLinqContext context)
{
    var result = await context.SubmitChangesAsync();

    if (result.AnyFailed)
    {
        foreach (var failure in result.Failed)
        {
            Logger.LogError($"Failed to {failure.Type} entity {failure.Entity?.GetType().Name} " +
                          $"(ID: {failure.Entity?.id}): {failure.Result?.Error?.Message}");

            // Log stack trace for debugging
            if (failure.Result?.Error != null)
            {
                Logger.LogError(failure.Result.Error.StackTrace);
            }
        }

        result.ThrowIfFailed();
    }

    Logger.LogInfo($"Successfully saved {result.Succeeded.Count} operations");
}
```

### Pattern 4: Retry Logic

```csharp
public async Task<SubmitChangesResult> SaveWithRetryAsync(
    SxmLinqContext context, 
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        var result = await context.SubmitChangesAsync();

        if (result.AllSucceeded)
            return result;

        // Check if failures are retryable (e.g., SQLITE_BUSY)
        var retryable = result.Failed.All(f => 
            f.Result?.Error is SqliteException ex && 
            ex.SqliteErrorCode == SQLitePCL.raw.SQLITE_BUSY);

        if (!retryable || attempt == maxRetries)
        {
            result.ThrowIfFailed(); // Give up
        }

        // Exponential backoff
        await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)));
        Logger.LogWarning($"Retrying save operation (attempt {attempt + 1}/{maxRetries})");
    }

    throw new InvalidOperationException("Should not reach here");
}
```

### Pattern 5: Graceful Degradation

```csharp
public async Task<SyncResult> SyncDataAsync(List<Entity> entities)
{
    using var context = new SxmLinqContext();

    foreach (var entity in entities)
    {
        context.InsertOrUpdateOnSubmit(entity);
    }

    // Allow partial success for sync operations
    var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

    if (result.Partial)
    {
        Logger.LogWarning($"Partial sync: {result.Succeeded.Count}/{result.TotalOperations} succeeded");

        // Queue failed items for retry later
        foreach (var failure in result.Failed)
        {
            await QueueForRetryAsync(failure.Entity!);
        }
    }

    return new SyncResult
    {
        TotalItems = result.TotalOperations,
        SuccessCount = result.Succeeded.Count,
        FailureCount = result.Failed.Count,
        RequiresRetry = result.AnyFailed
    };
}
```

---

## Bulk Operations

### Bulk Delete with Threshold Check

```csharp
public async Task CleanupOldLogsAsync()
{
    using var context = new SxmLinqContext();

    var cutoffDate = DateTime.Now.AddDays(-90);

    // Check count first (safety check)
    var count = await context.GetTable<Log>()
        .Where(l => l.Timestamp < cutoffDate)
        .CountAsync();

    if (count > 10000)
    {
        throw new InvalidOperationException(
            $"Refusing to delete {count} logs at once. Use batched deletion.");
    }

    // Perform bulk delete
    await context.GetTable<Log>()
        .Where(l => l.Timestamp < cutoffDate)
        .DeleteAsync();

    (await context.SubmitChangesAsync()).ThrowIfFailed();

    Logger.LogInfo($"Deleted {count} old log entries");
}
```

### Bulk Update with Condition

```csharp
public async Task ArchiveInactiveUsersAsync()
{
    using var context = new SxmLinqContext();

    var inactiveSince = DateTime.Now.AddYears(-1);

    await context.GetTable<User>()
        .Where(u => u.IsActive && u.LastLogin < inactiveSince)
        .Set(u => u.IsActive, false)
        .Set(u => u.Status, "Archived")
        .Set(u => u.ArchivedDate, DateTime.Now)
        .UpdateAsync();

    var result = await context.SubmitChangesAsync();

    // Log the number of rows affected
    var bulkAction = result.Succeeded.FirstOrDefault(s => s.Type == ChangeType.BulkUpdate);
    if (bulkAction != null)
    {
        Logger.LogInfo($"Archived {bulkAction.Result?.RowsAffected ?? 0} inactive users");
    }
}
```

### Combining Entity and Bulk Operations

```csharp
public async Task ProcessOrderAsync(Order order)
{
    using var context = new SxmLinqContext();

    // Insert new order
    context.InsertOnSubmit(order);

    // Update product inventory (bulk)
    await context.GetTable<Product>()
        .Where(p => p.id == order.ProductId)
        .Set(p => p.StockQuantity, p => p.StockQuantity - order.Quantity)
        .UpdateAsync();

    // Update user's order count (bulk)
    await context.GetTable<User>()
        .Where(u => u.id == order.UserId)
        .Set(u => u.TotalOrders, u => u.TotalOrders + 1)
        .UpdateAsync();

    // All operations are atomic
    (await context.SubmitChangesAsync()).ThrowIfFailed();
}
```

---

## Batch Processing & Imports

### Batch Insert with Progress Reporting

```csharp
public async Task<ImportResult> ImportUsersAsync(
    List<User> users, 
    IProgress<ImportProgress> progress)
{
    const int batchSize = 100;
    int totalProcessed = 0;
    int totalFailed = 0;

    for (int i = 0; i < users.Count; i += batchSize)
    {
        var batch = users.Skip(i).Take(batchSize).ToList();

        using var context = new SxmLinqContext();
        foreach (var user in batch)
        {
            context.InsertOnSubmit(user);
        }

        // Use ContinueOnError for imports
        var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

        totalProcessed += result.Succeeded.Count;
        totalFailed += result.Failed.Count;

        // Report progress
        progress?.Report(new ImportProgress
        {
            TotalItems = users.Count,
            ProcessedItems = totalProcessed,
            FailedItems = totalFailed,
            PercentComplete = (totalProcessed + totalFailed) * 100 / users.Count
        });

        // Log failures
        foreach (var failure in result.Failed)
        {
            Logger.LogWarning($"Failed to import user: {failure.Result?.Error?.Message}");
        }
    }

    return new ImportResult
    {
        TotalImported = totalProcessed,
        TotalFailed = totalFailed
    };
}
```

### CSV Import with Validation

```csharp
public async Task<ImportResult> ImportFromCsvAsync(string csvPath)
{
    var lines = await File.ReadAllLinesAsync(csvPath);
    var users = new List<User>();
    var errors = new List<string>();

    // Parse and validate
    foreach (var line in lines.Skip(1)) // Skip header
    {
        try
        {
            var fields = line.Split(',');
            var user = new User
            {
                Name = fields[0],
                Email = fields[1],
                Age = int.Parse(fields[2])
            };

            // Validate
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                errors.Add($"Invalid email in line: {line}");
                continue;
            }

            users.Add(user);
        }
        catch (Exception ex)
        {
            errors.Add($"Parse error in line '{line}': {ex.Message}");
        }
    }

    // Import valid users
    using var context = new SxmLinqContext();
    foreach (var user in users)
    {
        context.InsertOnSubmit(user);
    }

    var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

    return new ImportResult
    {
        TotalLines = lines.Length - 1,
        TotalImported = result.Succeeded.Count,
        TotalFailed = result.Failed.Count + errors.Count,
        ValidationErrors = errors
    };
}
```

### Upsert Pattern (Insert or Update)

```csharp
public async Task UpsertUsersAsync(List<User> users)
{
    using var context = new SxmLinqContext();

    foreach (var user in users)
    {
        // SQLiteXM provides InsertOrUpdateOnSubmit
        context.InsertOrUpdateOnSubmit(user);
    }

    (await context.SubmitChangesAsync()).ThrowIfFailed();
}

// Alternative: Manual upsert logic
public async Task UpsertUserManualAsync(User user)
{
    using var context = new SxmLinqContext();

    var existing = await context.GetTable<User>()
        .FirstOrDefaultAsync(u => u.Email == user.Email);

    if (existing != null)
    {
        existing.Name = user.Name;
        existing.Age = user.Age;
        context.UpdateOnSubmit(existing);
    }
    else
    {
        context.InsertOnSubmit(user);
    }

    (await context.SubmitChangesAsync()).ThrowIfFailed();
}
```

---

## Background Sync & Offline Apps

### Offline Queue Pattern

```csharp
public class OfflineQueueService
{
    private readonly Queue<SyncOperation> _pendingOperations = new();

    public void QueueOperation(SyncOperation operation)
    {
        _pendingOperations.Enqueue(operation);
    }

    public async Task<SyncResult> SyncAsync()
    {
        if (!IsOnline())
        {
            return SyncResult.Offline();
        }

        using var context = new SxmLinqContext();

        while (_pendingOperations.TryDequeue(out var operation))
        {
            switch (operation.Type)
            {
                case OperationType.Insert:
                    context.InsertOnSubmit(operation.Entity);
                    break;
                case OperationType.Update:
                    context.UpdateOnSubmit(operation.Entity);
                    break;
                case OperationType.Delete:
                    context.DeleteOnSubmit(operation.Entity);
                    break;
            }
        }

        // Allow partial success - re-queue failures
        var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

        foreach (var failure in result.Failed)
        {
            _pendingOperations.Enqueue(new SyncOperation
            {
                Type = failure.Type switch
                {
                    ChangeType.Insert => OperationType.Insert,
                    ChangeType.Update => OperationType.Update,
                    ChangeType.Delete => OperationType.Delete,
                    _ => OperationType.Insert
                },
                Entity = failure.Entity!
            });
        }

        return new SyncResult
        {
            Synced = result.Succeeded.Count,
            Failed = result.Failed.Count,
            Pending = _pendingOperations.Count
        };
    }
}
```

### Conflict Resolution with Server

```csharp
public async Task SyncWithServerAsync()
{
    // Get local changes
    using var context = new SxmLinqContext();
    var localChanges = await context.GetTable<User>()
        .Where(u => u.IsDirty)
        .ToListAsync();

    foreach (var localUser in localChanges)
    {
        // Fetch server version
        var serverUser = await FetchFromServerAsync(localUser.id);

        if (serverUser == null)
        {
            // Deleted on server
            context.DeleteOnSubmit(localUser);
        }
        else if (serverUser.UpdatedAt > localUser.UpdatedAt)
        {
            // Server is newer - take server version
            localUser.Name = serverUser.Name;
            localUser.Email = serverUser.Email;
            localUser.UpdatedAt = serverUser.UpdatedAt;
            localUser.IsDirty = false;
            context.UpdateOnSubmit(localUser);
        }
        else
        {
            // Local is newer - push to server
            await PushToServerAsync(localUser);
            localUser.IsDirty = false;
            context.UpdateOnSubmit(localUser);
        }
    }

    (await context.SubmitChangesAsync()).ThrowIfFailed();
}
```

### Periodic Background Sync

```csharp
public class BackgroundSyncService
{
    private readonly PeriodicTimer _timer;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public BackgroundSyncService()
    {
        _timer = new PeriodicTimer(_interval);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        while (await _timer.WaitForNextTickAsync(cancellationToken))
        {
            await SyncAsync(cancellationToken);
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            using var context = new SxmLinqContext();

            // Get dirty records
            var dirtyUsers = await context.GetTable<User>()
                .Where(u => u.IsDirty)
                .ToListAsync();

            foreach (var user in dirtyUsers)
            {
                // Push to server
                await PushToServerAsync(user, cancellationToken);

                // Mark as clean
                user.IsDirty = false;
                context.UpdateOnSubmit(user);
            }

            var result = await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

            Logger.LogInfo($"Background sync: {result.Succeeded.Count} records synced, " +
                         $"{result.Failed.Count} failed");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Background sync failed: {ex.Message}");
        }
    }
}
```

---

## User-Facing Applications

### MAUI Form Save Pattern

```csharp
public partial class UserEditPage : ContentPage
{
    private User _user;

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!ValidateForm())
            return;

        IsBusy = true;
        SaveButton.IsEnabled = false;

        try
        {
            using var context = new SxmLinqContext();

            _user.Name = NameEntry.Text;
            _user.Email = EmailEntry.Text;
            _user.Age = int.Parse(AgeEntry.Text);

            if (_user.id == 0)
                context.InsertOnSubmit(_user);
            else
                context.UpdateOnSubmit(_user);

            (await context.SubmitChangesAsync()).ThrowIfFailed();

            await DisplayAlert("Success", "User saved successfully!", "OK");
            await Navigation.PopAsync();
        }
        catch (SubmitChangesException ex)
        {
            await DisplayAlert("Error", 
                $"Could not save user: {ex.Result.GetErrorSummary()}", 
                "OK");
        }
        finally
        {
            IsBusy = false;
            SaveButton.IsEnabled = true;
        }
    }
}
```

### Delete Confirmation Pattern

```csharp
private async void OnDeleteClicked(object sender, EventArgs e)
{
    var confirm = await DisplayAlert(
        "Confirm Delete",
        $"Are you sure you want to delete {_user.Name}?",
        "Delete",
        "Cancel");

    if (!confirm)
        return;

    IsBusy = true;

    try
    {
        using var context = new SxmLinqContext();
        context.DeleteOnSubmit(_user);
        (await context.SubmitChangesAsync()).ThrowIfFailed();

        await DisplayAlert("Deleted", "User deleted successfully", "OK");
        await Navigation.PopAsync();
    }
    catch (SubmitChangesException ex)
    {
        await DisplayAlert("Error", 
            $"Could not delete user: {ex.Result.GetErrorSummary()}", 
            "OK");
    }
    finally
    {
        IsBusy = false;
    }
}
```

### Master-Detail Save Pattern

```csharp
public async Task SaveOrderWithItemsAsync(Order order, List<OrderItem> items)
{
    using var context = new SxmLinqContext();

    try
    {
        // Save order first
        context.InsertOnSubmit(order);
        (await context.SubmitChangesAsync()).ThrowIfFailed();

        // Now save items with the order ID
        using var context2 = new SxmLinqContext();
        foreach (var item in items)
        {
            item.OrderId = order.id;
            context2.InsertOnSubmit(item);
        }
        (await context2.SubmitChangesAsync()).ThrowIfFailed();

        await DisplayAlert("Success", 
            $"Order #{order.id} saved with {items.Count} items", 
            "OK");
    }
    catch (SubmitChangesException ex)
    {
        await DisplayAlert("Error", 
            "Could not save order: " + ex.Result.GetErrorSummary(), 
            "OK");
        throw;
    }
}
```

---

## Advanced Query Patterns

### Pagination

```csharp
public async Task<PagedResult<User>> GetUsersPagedAsync(int pageNumber, int pageSize)
{
    using var context = new SxmLinqContext();

    var query = context.GetTable<User>()
        .Where(u => u.IsActive)
        .OrderBy(u => u.Name);

    var totalCount = await query.CountAsync();

    var users = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return new PagedResult<User>
    {
        Items = users,
        TotalCount = totalCount,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    };
}
```

### Search with Multiple Criteria

```csharp
public async Task<List<User>> SearchUsersAsync(UserSearchCriteria criteria)
{
    using var context = new SxmLinqContext();

    var query = context.GetTable<User>().AsQueryable();

    if (!string.IsNullOrWhiteSpace(criteria.Name))
    {
        query = query.Where(u => u.Name.Contains(criteria.Name));
    }

    if (!string.IsNullOrWhiteSpace(criteria.Email))
    {
        query = query.Where(u => u.Email.Contains(criteria.Email));
    }

    if (criteria.MinAge.HasValue)
    {
        query = query.Where(u => u.Age >= criteria.MinAge.Value);
    }

    if (criteria.MaxAge.HasValue)
    {
        query = query.Where(u => u.Age <= criteria.MaxAge.Value);
    }

    return await query
        .OrderBy(u => u.Name)
        .ToListAsync();
}
```

### Eager Loading (LoadWith)

```csharp
public async Task<List<Order>> GetOrdersWithDetailsAsync()
{
    using var context = new SxmLinqContext();

    var orders = await context.GetTable<Order>()
        .LoadWith(o => o.Customer)
        .LoadWith(o => o.OrderItems)
        .Where(o => o.OrderDate > DateTime.Now.AddDays(-30))
        .ToListAsync();

    return orders;
}
```

### Aggregations

```csharp
public async Task<OrderStatistics> GetOrderStatisticsAsync(int userId)
{
    using var context = new SxmLinqContext();

    var orders = context.GetTable<Order>()
        .Where(o => o.UserId == userId);

    return new OrderStatistics
    {
        TotalOrders = await orders.CountAsync(),
        TotalAmount = await orders.SumAsync(o => o.TotalAmount),
        AverageAmount = await orders.AverageAsync(o => o.TotalAmount),
        MaxAmount = await orders.MaxAsync(o => o.TotalAmount),
        MinAmount = await orders.MinAsync(o => o.TotalAmount)
    };
}
```

---

## Testing Patterns

### Unit Test with In-Memory Database

```csharp
[Fact]
public async Task SaveUser_WithValidData_ShouldSucceed()
{
    // Arrange
    using var context = new SxmLinqContext(); // Uses test database
    var user = new User { Name = "Test User", Email = "test@example.com" };

    // Act
    context.InsertOnSubmit(user);
    var result = await context.SubmitChangesAsync();

    // Assert
    Assert.True(result.AllSucceeded);
    Assert.Single(result.Succeeded);
    Assert.Empty(result.Failed);
    Assert.True(user.id > 0);

    // Verify in database
    using var verifyContext = new SxmLinqContext();
    var saved = await verifyContext.GetTable<User>()
        .FirstOrDefaultAsync(u => u.Email == "test@example.com");

    Assert.NotNull(saved);
    Assert.Equal("Test User", saved.Name);
}
```

### Test Failure Scenarios

```csharp
[Fact]
public async Task SaveUser_WithNullName_ShouldFail()
{
    // Arrange
    using var context = new SxmLinqContext();
    var user = new User { Name = null!, Email = "test@example.com" };
    context.InsertOnSubmit(user);

    // Act
    var result = await context.SubmitChangesAsync();

    // Assert
    Assert.False(result.AllSucceeded);
    Assert.True(result.AnyFailed);
    Assert.Single(result.Failed);
    Assert.Contains("NOT NULL", result.GetErrorSummary());
}
```

### Test Transaction Rollback

```csharp
[Fact]
public async Task SubmitChanges_WithFailOnFirstError_ShouldRollbackAll()
{
    // Arrange
    using var context = new SxmLinqContext();

    var valid1 = new User { Name = "Valid1", Email = "valid1@test.com" };
    var valid2 = new User { Name = "Valid2", Email = "valid2@test.com" };
    var invalid = new User { Name = null!, Email = "invalid@test.com" };

    context.InsertOnSubmit(valid1);
    context.InsertOnSubmit(valid2);
    context.InsertOnSubmit(invalid);

    // Act
    var result = await context.SubmitChangesAsync(ConflictMode.FailOnFirstError);

    // Assert
    Assert.False(result.AllSucceeded);

    // Verify rollback - nothing should be saved
    using var verifyContext = new SxmLinqContext();
    var count = await verifyContext.GetTable<User>()
        .Where(u => u.Email.Contains("@test.com"))
        .CountAsync();

    Assert.Equal(0, count);
}
```

---

## Performance Optimization

### Batch Inserts

```csharp
// ❌ SLOW: Individual commits
for (int i = 0; i < 1000; i++)
{
    using var context = new SxmLinqContext();
    context.InsertOnSubmit(new User { Name = $"User{i}" });
    await context.SubmitChangesAsync(); // 1000 transactions!
}

// ✅ FAST: Batch commit
using var context = new SxmLinqContext();
for (int i = 0; i < 1000; i++)
{
    context.InsertOnSubmit(new User { Name = $"User{i}" });
}
await context.SubmitChangesAsync(); // 1 transaction!
```

### Use Bulk Operations for Large Updates

```csharp
// ❌ SLOW: Entity-based updates
using var context = new SxmLinqContext();
var users = await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .ToListAsync();

foreach (var user in users)
{
    user.Status = "Archived";
    context.UpdateOnSubmit(user);
}
await context.SubmitChangesAsync();

// ✅ FAST: Bulk SQL update
using var context = new SxmLinqContext();
await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();
await context.SubmitChangesAsync();
```

### Query Optimization

```csharp
// ❌ SLOW: Loading all data
var allUsers = await context.GetTable<User>().ToListAsync();
var activeUsers = allUsers.Where(u => u.IsActive).ToList();

// ✅ FAST: Filter in database
var activeUsers = await context.GetTable<User>()
    .Where(u => u.IsActive)
    .ToListAsync();
```

### Index Usage

```csharp
// Add index to entity
[Table]
public class User : SxmEntity
{
    [Column]
    public string Name { get; set; }

    [Column]
    [Index] // Speeds up searches on Email
    public string Email { get; set; }

    [Column]
    [Index] // Speeds up filtered queries
    public bool IsActive { get; set; }
}

// Query will use index
var user = await context.GetTable<User>()
    .FirstOrDefaultAsync(u => u.Email == "alice@example.com");
```

---

## Best Practices Summary

### ✅ DO

- Use `ThrowIfFailed()` for simple fail-fast scenarios
- Batch operations in a single `SubmitChangesAsync()` call
- Use bulk operations for large updates/deletes
- Use `ContinueOnError` for batch imports where partial success is acceptable
- Log detailed errors using `result.GetErrorSummary()` and `result.Failed`
- Validate data before submitting to the database
- Use indexes on frequently queried columns
- Test both success and failure paths

### ❌ DON'T

- Don't call `SubmitChangesAsync()` in a loop for individual records
- Don't ignore `SubmitChangesResult` - always check or throw
- Don't use entity-based updates for large datasets (use bulk operations)
- Don't catch and swallow `SubmitChangesException` without logging
- Don't forget to dispose contexts (`using` statement)
- Don't query all data and filter in memory

---

## Quick Reference Card

```csharp
// ═══════════════════════════════════════════════════════════════
// BASIC OPERATIONS
// ═══════════════════════════════════════════════════════════════

// Insert
context.InsertOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Update
context.UpdateOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Delete
context.DeleteOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Upsert
context.InsertOrUpdateOnSubmit(entity);
(await context.SubmitChangesAsync()).ThrowIfFailed();

// ═══════════════════════════════════════════════════════════════
// BULK OPERATIONS
// ═══════════════════════════════════════════════════════════════

// Bulk update
await context.GetTable<User>()
    .Where(u => u.IsActive == false)
    .Set(u => u.Status, "Archived")
    .UpdateAsync();
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Bulk delete
await context.GetTable<Log>()
    .Where(l => l.Timestamp < cutoffDate)
    .DeleteAsync();
(await context.SubmitChangesAsync()).ThrowIfFailed();

// ═══════════════════════════════════════════════════════════════
// ERROR HANDLING
// ═══════════════════════════════════════════════════════════════

// Pattern 1: Fail-fast
(await context.SubmitChangesAsync()).ThrowIfFailed();

// Pattern 2: Inspect result
var result = await context.SubmitChangesAsync();
if (result.AnyFailed) { /* handle */ }

// Pattern 3: Detailed logging
var result = await context.SubmitChangesAsync();
foreach (var failure in result.Failed)
{
    Logger.LogError($"{failure.Type} failed: {failure.Result?.Error?.Message}");
}
result.ThrowIfFailed();

// ═══════════════════════════════════════════════════════════════
// CONFLICT MODES
// ═══════════════════════════════════════════════════════════════

// Stop on first error, rollback (default)
await context.SubmitChangesAsync(ConflictMode.FailOnFirstError);

// Continue processing, commit successes
await context.SubmitChangesAsync(ConflictMode.ContinueOnError);

// ═══════════════════════════════════════════════════════════════
// QUERIES
// ═══════════════════════════════════════════════════════════════

// Simple query
var users = await context.GetTable<User>()
    .Where(u => u.IsActive)
    .ToListAsync();

// With eager loading
var orders = await context.GetTable<Order>()
    .LoadWith(o => o.Customer)
    .ToListAsync();

// Pagination
var page = await context.GetTable<User>()
    .OrderBy(u => u.Name)
    .Skip((pageNum - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

---

## Additional Resources

- See `SUBMITCHANGES_USAGE_EXAMPLES.md` for detailed `SubmitChangesAsync()` patterns
- See `SUBMITCHANGES_API_SUMMARY.md` for quick API reference
- See the test project (`SQLiteXM.Tests`) for working examples

---

*This document will be updated as new patterns emerge. Contributions welcome!*
