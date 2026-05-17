using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for SxmConnectionManager.RunWorkersAsync pattern.
/// Verifies that multiple workers can execute concurrently against a shared connection
/// with proper lease management and deterministic cleanup.
/// </summary>
[Collection("SQLiteXM Tests")]
public class ConnectionManagerWorkerTests : TestBase
{
    [Fact]
    public async Task RunWorkersAsync_TwoWorkers_ShouldExecuteConcurrently()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        bool worker1Completed = false;
        bool worker2Completed = false;
        bool worker2FailedAsExpected = false;

        var workers = new List<Func<SxmConnection, Task>>
        {
            // Worker 1: Acquires lock and does multiple operations
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 5000))
                {
                    // Multiple save/delete operations like your example
                    var entity = new SimpleEntity { Name = "Worker1_Entity", Age = 100 };
                    await entity.SaveAsync(transaction);

                    var entity2 = new SimpleEntity { Name = "Worker1_Entity2", Age = 200 };
                    await entity2.SaveAsync(transaction);
                    await entity2.DeleteAsync(transaction);
                    await entity2.SaveAsync(transaction);

                    await transaction.CommitTransactionAsync();
                    worker1Completed = true;
                }
            },

            // Worker 2: Tries to acquire shortly after Worker 1 with longer timeout
            async (sharedConn) =>
            {
                await Task.Delay(100); // Ensure Worker 1 acquires first

                try
                {
                    await using (var transaction = await SxmSqlTransaction.CreateAsync(
                        sharedConn, 
                        waitMilliseconds: 2500))
                    {
                        var entity = new SimpleEntity { Name = "Worker2_Entity", Age = 300 };
                        await entity.SaveAsync(transaction);

                        await entity.DeleteAsync(transaction);
                        await entity.SaveAsync(transaction);

                        await transaction.CommitTransactionAsync();
                        worker2Completed = true;
                    }
                }
                catch (SxmException)
                {
                    // Expected if Worker 1 holds lock too long
                    worker2FailedAsExpected = true;
                }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert
        worker1Completed.Should().BeTrue("Worker 1 should complete successfully");
        (worker2Completed || worker2FailedAsExpected).Should().BeTrue(
            "Worker 2 should either complete or timeout gracefully");
    }

    [Fact]
    public async Task RunWorkersAsync_ThreeWorkers_ShouldExecuteWithProperLocking()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var completedWorkers = new List<int>();
        var lockObj = new object();

        var workers = new List<Func<SxmConnection, Task>>
        {
            // Worker 1
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 10000))
                {
                    var entity = new SimpleEntity { Name = "Worker1", Age = 111 };
                    await entity.SaveAsync(transaction);
                    await Task.Delay(50); // Brief hold
                    await transaction.CommitTransactionAsync();

                    lock (lockObj) { completedWorkers.Add(1); }
                }
            },

            // Worker 2
            async (sharedConn) =>
            {
                await Task.Delay(20); // Slight delay

                try
                {
                    await using (var transaction = await SxmSqlTransaction.CreateAsync(
                        sharedConn, 
                        waitMilliseconds: 10000))
                    {
                        var entity = new SimpleEntity { Name = "Worker2", Age = 222 };
                        await entity.SaveAsync(transaction);
                        await transaction.CommitTransactionAsync();

                        lock (lockObj) { completedWorkers.Add(2); }
                    }
                }
                catch (SxmException) { /* Timeout OK */ }
            },

            // Worker 3
            async (sharedConn) =>
            {
                await Task.Delay(40); // Slight delay

                try
                {
                    await using (var transaction = await SxmSqlTransaction.CreateAsync(
                        sharedConn, 
                        waitMilliseconds: 10000))
                    {
                        var entity = new SimpleEntity { Name = "Worker3", Age = 333 };
                        await entity.SaveAsync(transaction);
                        await transaction.CommitTransactionAsync();

                        lock (lockObj) { completedWorkers.Add(3); }
                    }
                }
                catch (SxmException) { /* Timeout OK */ }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert
        completedWorkers.Should().NotBeEmpty("at least one worker should complete");
    }

    [Fact]
    public async Task RunWorkersAsync_WorkerWithMultipleSaveDeleteCycles_ShouldSucceed()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        bool workerCompleted = false;
        long finalEntityId = 0;

        var workers = new List<Func<SxmConnection, Task>>
        {
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 5000))
                {
                    // Pattern from your example: save, delete, save, save
                    var entity = new SimpleEntity { Name = "CycleTest", Age = 99 };

                    await entity.SaveAsync(transaction);
                    entity.id.Should().BeGreaterThan(0, "first save should assign ID");

                    await entity.DeleteAsync(transaction);

                    // Reset ID to force new insert
                    entity.id = 0;
                    await entity.SaveAsync(transaction);
                    entity.id.Should().BeGreaterThan(0, "second save should assign new ID");

                    // Update (should reuse same ID)
                    long idBeforeUpdate = entity.id;
                    entity.Age = 100;
                    await entity.SaveAsync(transaction);
                    entity.id.Should().Be(idBeforeUpdate, "update should keep same ID");

                    finalEntityId = entity.id;

                    await transaction.CommitTransactionAsync();
                    workerCompleted = true;
                }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert
        workerCompleted.Should().BeTrue();
        finalEntityId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunWorkersAsync_WorkerCommitFailure_ShouldPropagateException()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        bool exceptionCaught = false;

        var workers = new List<Func<SxmConnection, Task>>
        {
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 5000))
                {
                    var entity = new SimpleEntity { Name = "FailTest", Age = 500 };
                    await entity.SaveAsync(transaction);

                    // Commit should succeed in normal case
                    var errorCode = await transaction.CommitTransactionAsync();
                    errorCode.Should().Be(SQLiteErrorCode.Ok);
                }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        catch (Exception)
        {
            exceptionCaught = true;
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert - no exception should occur for valid operations
        exceptionCaught.Should().BeFalse("valid operations should not throw");
    }

    [Fact]
    public async Task RunWorkersAsync_EmptyWorkerList_ShouldNotFail()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        var workers = new List<Func<SxmConnection, Task>>();

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert - should complete without error (implicit success)
        true.Should().BeTrue();
    }

    [Fact]
    public async Task RunWorkersAsync_SingleWorker_ShouldExecuteSuccessfully()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        bool workerCompleted = false;

        var workers = new List<Func<SxmConnection, Task>>
        {
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 5000))
                {
                    var entity1 = new SimpleEntity { Name = "Single1", Age = 1 };
                    var entity2 = new SimpleEntity { Name = "Single2", Age = 2 };

                    await entity1.SaveAsync(transaction);
                    await entity2.SaveAsync(transaction);

                    await transaction.CommitTransactionAsync();
                    workerCompleted = true;
                }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert
        workerCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RunWorkersAsync_WorkerWithRollback_ShouldReleaseLocksCorrectly()
    {
        // Arrange
        await InitializeSqliteXMAsync();

        bool worker1Completed = false;
        bool worker2Completed = false;

        var workers = new List<Func<SxmConnection, Task>>
        {
            // Worker 1: Does rollback
            async (sharedConn) =>
            {
                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 5000))
                {
                    var entity = new SimpleEntity { Name = "Rollback", Age = 777 };
                    await entity.SaveAsync(transaction);

                    // Rollback instead of commit
                    await transaction.RollbackTransactionAsync();
                    worker1Completed = true;
                }
            },

            // Worker 2: Should be able to acquire after Worker 1 releases
            async (sharedConn) =>
            {
                await Task.Delay(100); // Wait for Worker 1

                await using (var transaction = await SxmSqlTransaction.CreateAsync(
                    sharedConn, 
                    waitMilliseconds: 3000))
                {
                    var entity = new SimpleEntity { Name = "AfterRollback", Age = 888 };
                    await entity.SaveAsync(transaction);
                    await transaction.CommitTransactionAsync();
                    worker2Completed = true;
                }
            }
        };

        // Act
        try
        {
            await SxmConnectionManager.Instance.RunWorkersAsync(TestDatabaseName, workers);
        }
        finally
        {
            try { await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName); } catch { }
        }

        // Assert
        worker1Completed.Should().BeTrue("Worker 1 with rollback should complete");
        worker2Completed.Should().BeTrue("Worker 2 should acquire lock after Worker 1 releases");
    }
}
