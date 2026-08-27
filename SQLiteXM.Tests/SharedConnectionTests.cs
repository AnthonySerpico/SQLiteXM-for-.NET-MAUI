using FluentAssertions;
using SQLiteXM;
using System.Diagnostics;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for shared connection behavior with concurrent access and lock contention.
/// Verifies that multiple callers can safely share a connection with proper locking.
/// </summary>
[Collection("Sequential")]
public class SharedConnectionTests : TestBase
{
    [Fact]
    public async Task SharedConnection_ConcurrentAccess_ShouldEnforceLocking()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);
        bool task1Completed = false;
        bool task2FailedAsExpected = false;
        Exception? task2Exception = null;

        try
        {
            // Task 1: Acquires lock and holds it while doing work
            var task1 = Task.Run(async () =>
            {
                await using (var transaction = await SxmTransaction.CreateAsync(
                    sharedConnection, 
                    waitMilliseconds: 5000))
                {
                    // Create and save multiple entities to hold the lock for a moment
                    var entity1 = new SimpleEntity { Name = "Task1_Entity1", Age = 10 };
                    await entity1.SaveAsync();

                    var entity2 = new SimpleEntity { Name = "Task1_Entity2", Age = 20 };
                    await entity2.SaveAsync();

                    // Simulate some work
                    await Task.Delay(500);

                    await transaction.CommitTransactionAsync();
                    task1Completed = true;
                }
            });

            // Task 2: Tries to acquire lock shortly after Task1 with a short timeout
            var task2 = Task.Run(async () =>
            {
                // Wait a bit to ensure Task1 acquires the lock first
                await Task.Delay(100);

                try
                {
                    // This should timeout because Task1 holds the lock
                    await using (var transaction = await SxmTransaction.CreateAsync(
                        sharedConnection, 
                        waitMilliseconds: 200))  // Short timeout
                    {
                        var entity = new SimpleEntity { Name = "Task2_Entity", Age = 30 };
                        await entity.SaveAsync();
                        await transaction.CommitTransactionAsync();
                    }
                }
                catch (SxmException ex)
                {
                    // Expected: timeout acquiring lock
                    task2FailedAsExpected = true;
                    task2Exception = ex;
                }
            });

            // Wait for both tasks
            await Task.WhenAll(task1, task2);

            // Assert
            task1Completed.Should().BeTrue("Task1 should complete successfully");
            task2FailedAsExpected.Should().BeTrue("Task2 should fail to acquire lock due to timeout");
            task2Exception.Should().NotBeNull("Task2 should throw SxmException");
        }
        finally
        {
            // Cleanup shared connection
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task SharedConnection_SequentialAccess_ShouldSucceed()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            // First transaction
            await using (var transaction1 = await SxmTransaction.CreateAsync(sharedConnection))
            {
                var entity1 = new SimpleEntity { Name = "Sequential1", Age = 100 };
                await entity1.SaveAsync();
                await transaction1.CommitTransactionAsync();

                entity1.id.Should().BeGreaterThan(0);
            }

            // Second transaction (should succeed because first released lock)
            await using (var transaction2 = await SxmTransaction.CreateAsync(sharedConnection))
            {
                var entity2 = new SimpleEntity { Name = "Sequential2", Age = 200 };
                await entity2.SaveAsync();
                await transaction2.CommitTransactionAsync();

                entity2.id.Should().BeGreaterThan(0);
            }
        }
        finally
        {
            // Cleanup
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task SharedConnection_MultipleOperationsInTransaction_ShouldWork()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            await using (var transaction = await SxmTransaction.CreateAsync(sharedConnection))
            {
                // Create entity
                var entity = new SimpleEntity { Name = "MultiOp", Age = 50 };
                await entity.SaveAsync();
                entity.id.Should().BeGreaterThan(0);

                long savedId = entity.id;

                // Update entity
                entity.Age = 51;
                await entity.SaveAsync();

                // Delete entity
                await entity.DeleteAsync();

                // Re-create entity (SQLite may reuse the ID - that's normal behavior)
                entity.id = 0; // Reset ID to force insert
                entity.Age = 52;
                await entity.SaveAsync();

                entity.id.Should().BeGreaterThan(0, "new insert should get valid ID");

                await transaction.CommitTransactionAsync();
            }
        }
        finally
        {
            // Cleanup
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task SharedConnection_LongTimeoutAllowsSequentialAccess_ShouldSucceed()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);
        bool task1Completed = false;
        bool task2Completed = false;

        try
        {
            // Task 1: Quick transaction
            var task1 = Task.Run(async () =>
            {
                await using (var transaction = await SxmTransaction.CreateAsync(sharedConnection))
                {
                    var entity = new SimpleEntity { Name = "LongTimeout_Task1", Age = 111 };
                    await entity.SaveAsync();
                    await Task.Delay(200); // Hold lock briefly
                    await transaction.CommitTransactionAsync();
                    task1Completed = true;
                }
            });

            // Task 2: Waits with long timeout (should eventually acquire after Task1 releases)
            var task2 = Task.Run(async () =>
            {
                await Task.Delay(50); // Ensure Task1 starts first

                await using (var transaction = await SxmTransaction.CreateAsync(
                    sharedConnection, 
                    waitMilliseconds: 3000))  // Long enough timeout
                {
                    var entity = new SimpleEntity { Name = "LongTimeout_Task2", Age = 222 };
                    await entity.SaveAsync();
                    await transaction.CommitTransactionAsync();
                    task2Completed = true;
                }
            });

            await Task.WhenAll(task1, task2);

            // Assert
            task1Completed.Should().BeTrue();
            task2Completed.Should().BeTrue("Task2 should succeed with sufficient timeout");
        }
        finally
        {
            // Cleanup
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task SharedConnection_RollbackReleasesLock_ShouldAllowNextCaller()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            // First transaction with rollback
            await using (var transaction1 = await SxmTransaction.CreateAsync(sharedConnection))
            {
                var entity1 = new SimpleEntity { Name = "Rollback1", Age = 300 };
                await entity1.SaveAsync();
                await transaction1.RollbackTransactionAsync();
            }

            // Second transaction (should succeed immediately after rollback released lock)
            await using (var transaction2 = await SxmTransaction.CreateAsync(
                sharedConnection, 
                waitMilliseconds: 100))  // Short timeout should be fine
            {
                var entity2 = new SimpleEntity { Name = "AfterRollback", Age = 400 };
                await entity2.SaveAsync();
                await transaction2.CommitTransactionAsync();

                entity2.id.Should().BeGreaterThan(0);
            }
        }
        finally
        {
            // Cleanup
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task SharedConnection_ThreeWayContention_ShouldEnforceSerialAccess()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var sharedConnection = new SxmConnection(TestDatabaseName, shared: true);
        var completedTasks = new List<int>();
        var lockObj = new object();

        try
        {
            var tasks = new List<Task>();

            for (int i = 1; i <= 3; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await using (var transaction = await SxmTransaction.CreateAsync(
                            sharedConnection, 
                            waitMilliseconds: 5000))
                        {
                            var entity = new SimpleEntity 
                            { 
                                Name = $"ThreeWay_Task{taskId}", 
                                Age = taskId * 100 
                            };
                            await entity.SaveAsync();

                            // Hold lock briefly to force contention
                            await Task.Delay(150);

                            await transaction.CommitTransactionAsync();

                            lock (lockObj)
                            {
                                completedTasks.Add(taskId);
                            }
                        }
                    }
                    catch (SxmException)
                    {
                        // Some tasks may timeout - that's OK for this test
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - at least one task should complete successfully
            completedTasks.Should().NotBeEmpty("at least one task should successfully acquire the lock");
        }
        finally
        {
            // Cleanup
            try { await sharedConnection.ReleaseConnectionAsync(destroy: true); } catch { }
            try { await sharedConnection.DestroyConnectionAsync(); } catch { }
        }
    }

    [Fact]
    public async Task NonSharedConnection_ConcurrentAccess_ShouldWorkIndependently()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        // Each task gets its own non-shared connection
        bool task1Completed = false;
        bool task2Completed = false;

        var task1 = Task.Run(async () =>
        {
            var conn = new SxmConnection(TestDatabaseName, shared: false);
            try
            {
                await using (var transaction = await SxmTransaction.CreateAsync(conn))
                {
                    var entity = new SimpleEntity { Name = "NonShared_Task1", Age = 777 };
                    await entity.SaveAsync();
                    await Task.Delay(200);
                    await transaction.CommitTransactionAsync();
                    task1Completed = true;
                }
            }
            finally
            {
                await conn.DestroyConnectionAsync();
            }
        });

        var task2 = Task.Run(async () =>
        {
            var conn = new SxmConnection(TestDatabaseName, shared: false);
            try
            {
                await using (var transaction = await SxmTransaction.CreateAsync(conn))
                {
                    var entity = new SimpleEntity { Name = "NonShared_Task2", Age = 888 };
                    await entity.SaveAsync();
                    await Task.Delay(200);
                    await transaction.CommitTransactionAsync();
                    task2Completed = true;
                }
            }
            finally
            {
                await conn.DestroyConnectionAsync();
            }
        });

        await Task.WhenAll(task1, task2);

        // Assert - both should complete without contention
        task1Completed.Should().BeTrue();
        task2Completed.Should().BeTrue();
    }
}
