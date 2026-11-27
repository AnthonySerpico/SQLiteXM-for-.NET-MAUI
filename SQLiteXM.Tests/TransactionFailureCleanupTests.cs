using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Covers transaction failure and cleanup paths that only run when something goes wrong:
/// shared-connection lock timeouts and cancellation, cleanup after a failed transaction
/// creation, ambient-transaction bookkeeping, the app lifecycle gate (suspend/resume),
/// and InsertOrReplaceAsync inside a transaction.
/// In every case the key assertion is that no ambient transaction leaks and that the
/// next transaction works normally.
/// </summary>
[Collection("Sequential")]
public class TransactionFailureCleanupTests : TestBase
{
    private static string NewPrefix() => $"TxClean_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

    private static async Task<List<SimpleEntity>> FindAsync(string prefix)
    {
        await using SxmTransaction verify = new SxmTransaction(TestDatabaseName);
        return verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
    }

    private static async Task DestroyAsync(SxmConnection connection)
    {
        try { await connection.ReleaseConnectionAsync(destroy: true); } catch { }
        try { await connection.DestroyConnectionAsync(); } catch { }
    }

    private static async Task AssertNextTransactionWorksAsync(string prefix)
    {
        SxmAmbientTransaction.Current.Should().BeNull("a failed creation must not leak an ambient transaction");

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_After", Age = 1 }.SaveAsync();
        }

        (await FindAsync($"{prefix}_After")).Should().ContainSingle();
    }

    // ---------- Shared connection: lock timeout and cancellation ----------

    [Fact]
    public async Task CreateAsync_SharedConnection_LockTimeout_ThrowsAndDoesNotLeakAmbient()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection shared = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            await using (ISxmConnectionLease lease = await shared.AcquireLeaseAsync(5000))
            {
                Func<Task> act = async () =>
                {
                    await using SxmTransaction ctx = await SxmTransaction.CreateAsync(shared, waitMilliseconds: 50);
                };

                await act.Should().ThrowAsync<SxmException>();
                SxmAmbientTransaction.Current.Should().BeNull();
            }

            await AssertNextTransactionWorksAsync(prefix);
        }
        finally
        {
            await DestroyAsync(shared);
        }
    }

    [Fact]
    public async Task CreateAsync_SharedConnection_CancelledWhileWaiting_ThrowsAndDoesNotLeakAmbient()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection shared = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            await using (ISxmConnectionLease lease = await shared.AcquireLeaseAsync(5000))
            {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

                Func<Task> act = async () =>
                {
                    await using SxmTransaction ctx = await SxmTransaction.CreateAsync(shared, waitMilliseconds: 10000, cts.Token);
                };

                await act.Should().ThrowAsync<Exception>();
                SxmAmbientTransaction.Current.Should().BeNull();
            }

            await AssertNextTransactionWorksAsync(prefix);
        }
        finally
        {
            await DestroyAsync(shared);
        }
    }

    [Fact]
    public async Task CreateAsync_SharedConnection_SucceedsAfterLockReleased()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection shared = new SxmConnection(TestDatabaseName, shared: true);

        try
        {
            ISxmConnectionLease lease = await shared.AcquireLeaseAsync(5000);
            Func<Task> blocked = async () =>
            {
                await using SxmTransaction ctx = await SxmTransaction.CreateAsync(shared, waitMilliseconds: 50);
            };
            await blocked.Should().ThrowAsync<SxmException>();
            await lease.DisposeAsync();

            await using (SxmTransaction ctx = await SxmTransaction.CreateAsync(shared, waitMilliseconds: 5000))
            {
                await new SimpleEntity { Name = $"{prefix}_Shared", Age = 1 }.SaveAsync();
            }

            (await FindAsync(prefix)).Should().ContainSingle(e => e.Name == $"{prefix}_Shared");
        }
        finally
        {
            await DestroyAsync(shared);
        }
    }

    [Fact]
    public async Task CreateAsync_WhileAmbientActive_ThrowsAndLeavesOuterTransactionIntact()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection other = new SxmConnection(TestDatabaseName);

        try
        {
            await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
            {
                SxmSqlTransaction? ambientBefore = SxmAmbientTransaction.Current;
                await new SimpleEntity { Name = $"{prefix}_Outer", Age = 1 }.SaveAsync();

                Func<Task> act = () => SxmTransaction.CreateAsync(other);

                await act.Should().ThrowAsync<InvalidOperationException>();
                SxmAmbientTransaction.Current.Should().BeSameAs(ambientBefore);
                outer.IsFaulted.Should().BeFalse("a rejected nested creation is not a failed operation of the outer transaction");
            }

            (await FindAsync(prefix)).Should().ContainSingle(e => e.Name == $"{prefix}_Outer");
        }
        finally
        {
            await DestroyAsync(other);
        }
    }

    [Fact]
    public async Task CreateAsync_NullConnection_Throws()
    {
        await InitializeSqliteXMAsync();

        Func<Task> act = () => SxmTransaction.CreateAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
        SxmAmbientTransaction.Current.Should().BeNull();
    }

    // ---------- Cleanup after a failed creation ----------

    [Fact]
    public async Task CleanupFailedCreate_RollsBackRemovesAmbientAndDestroysConnection()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection connection = new SxmConnection(TestDatabaseName);

        SxmSqlTransaction sqlTransaction = await SxmSqlTransaction.CreateAsync(connection);
        SxmAmbientTransaction.Current.Should().BeSameAs(sqlTransaction);

        await using (SxmTransaction joined = new SxmTransaction(TestDatabaseName))
        {
            await joined.InsertAsync(new SimpleEntity { Name = $"{prefix}_Uncommitted", Age = 1 });
        }

        sqlTransaction.CleanupFailedCreate();

        SxmAmbientTransaction.Current.Should().BeNull();
        sqlTransaction.Connection.Should().BeNull("the private connection is destroyed");
        (await FindAsync(prefix)).Should().BeEmpty("work done before the failure is rolled back");

        Func<Task> disposeAgain = async () => await sqlTransaction.DisposeAsync();
        await disposeAgain.Should().NotThrowAsync("cleanup marks the transaction disposed");

        await AssertNextTransactionWorksAsync(prefix);
    }

    [Fact]
    public async Task CleanupFailedCreate_WithoutOpenTransaction_DoesNotThrow()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection connection = new SxmConnection(TestDatabaseName);

        SxmSqlTransaction sqlTransaction = await SxmSqlTransaction.CreateAsync(connection);

        Action act = () => sqlTransaction.CleanupFailedCreate();

        act.Should().NotThrow();
        SxmAmbientTransaction.Current.Should().BeNull();
        await AssertNextTransactionWorksAsync(prefix);
    }

    // ---------- Ambient stack bookkeeping ----------

    [Fact]
    public async Task AmbientTransaction_TryRemoveAndPop_HandleEmptyAndMismatchedStacks()
    {
        await InitializeSqliteXMAsync();
        SxmConnection connection = new SxmConnection(TestDatabaseName);

        try
        {
            SxmAmbientTransaction.Current.Should().BeNull();

            SxmSqlTransaction tx = await SxmSqlTransaction.CreateAsync(connection);
            SxmAmbientTransaction.TryRemove(tx).Should().BeTrue();
            SxmAmbientTransaction.TryRemove(tx).Should().BeFalse("the stack is now empty");

            Action popEmpty = () => SxmAmbientTransaction.Pop(tx);
            popEmpty.Should().Throw<InvalidOperationException>();

            Action removeNull = () => SxmAmbientTransaction.TryRemove(null!);
            removeNull.Should().Throw<ArgumentNullException>();

            Action pushNull = () => SxmAmbientTransaction.Push(null!);
            pushNull.Should().Throw<ArgumentNullException>();

            tx.CleanupFailedCreate();
        }
        finally
        {
            await DestroyAsync(connection);
        }
    }

    [Fact]
    public async Task AmbientTransaction_NestedCreation_IsRejectedAndStackKept()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SxmConnection otherConnection = new SxmConnection(TestDatabaseName);

        try
        {
            await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
            {
                SxmSqlTransaction top = SxmAmbientTransaction.Current!;

                Func<Task> nested = () => SxmSqlTransaction.CreateAsync(otherConnection);
                await nested.Should().ThrowAsync<InvalidOperationException>("nested ambient transactions are not allowed");
                SxmAmbientTransaction.Current.Should().BeSameAs(top);
                await new SimpleEntity { Name = $"{prefix}_Kept", Age = 1 }.SaveAsync();
            }

            (await FindAsync(prefix)).Should().ContainSingle();
        }
        finally
        {
            await DestroyAsync(otherConnection);
        }
    }

    // ---------- Lifecycle gate (suspend / resume) ----------

    [Fact]
    public async Task LifecycleGate_Closed_BlocksNewTransactions_AndReopens()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        try
        {
            SxmConnection.BlockNewOperations();
            SxmConnection.IsLifecycleGateClosed.Should().BeTrue();

            Action act = () => new SxmTransaction(TestDatabaseName);

            act.Should().Throw<SxmException>();
            SxmAmbientTransaction.Current.Should().BeNull("a blocked transaction must not register as ambient");
        }
        finally
        {
            SxmConnection.AllowNewOperations();
        }

        SxmConnection.IsLifecycleGateClosed.Should().BeFalse();
        await AssertNextTransactionWorksAsync(prefix);
    }

    [Fact]
    public async Task LifecycleGate_Closed_DoesNotInterruptTransactionAlreadyRunning()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        try
        {
            await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
            {
                await new SimpleEntity { Name = $"{prefix}_Before", Age = 1 }.SaveAsync();

                SxmConnection.BlockNewOperations();

                // The transaction already owns its connection, so it can finish its work.
                await new SimpleEntity { Name = $"{prefix}_During", Age = 2 }.SaveAsync();
                ctx.IsFaulted.Should().BeFalse();
            }
        }
        finally
        {
            SxmConnection.AllowNewOperations();
        }

        (await FindAsync(prefix)).Should().HaveCount(2, "in-flight work commits normally during the grace period");
    }

    [Fact]
    public async Task LifecycleManager_OnSleepAndOnResume_ToggleGate()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        TimeSpan originalGrace = SxmLifecycleManager.SuspendGracePeriod;

        try
        {
            SxmLifecycleManager.SuspendGracePeriod = TimeSpan.Zero;

            SxmLifecycleManager.OnSleep();
            SxmConnection.IsLifecycleGateClosed.Should().BeTrue();

            // A second OnSleep is a no-op.
            SxmLifecycleManager.OnSleep();
            SxmConnection.IsLifecycleGateClosed.Should().BeTrue();

            Action blocked = () => new SxmTransaction(TestDatabaseName);
            blocked.Should().Throw<SxmException>();

            SxmLifecycleManager.OnResume();
            SxmConnection.IsLifecycleGateClosed.Should().BeFalse();

            // A second OnResume is a no-op.
            SxmLifecycleManager.OnResume();
            SxmConnection.IsLifecycleGateClosed.Should().BeFalse();
        }
        finally
        {
            SxmLifecycleManager.OnResume();
            SxmConnection.AllowNewOperations();
            SxmLifecycleManager.SuspendGracePeriod = originalGrace;
        }

        await AssertNextTransactionWorksAsync(prefix);
    }

    [Fact]
    public void LifecycleManager_NegativeGracePeriod_Throws()
    {
        Action act = () => SxmLifecycleManager.SuspendGracePeriod = TimeSpan.FromMilliseconds(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------- InsertOrReplaceAsync ----------

    [Fact]
    public async Task InsertOrReplaceAsync_ReplacesExistingRow_AndCommits()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Original", Age = 1 });
        }

        long id = (await FindAsync(prefix)).Single().id;

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            int affected = await ctx.InsertOrReplaceAsync(new SimpleEntity { id = id, Name = $"{prefix}_Replaced", Age = 2 });
            affected.Should().BeGreaterThan(0);
        }

        List<SimpleEntity> rows = await FindAsync(prefix);
        rows.Should().ContainSingle();
        rows[0].id.Should().Be(id);
        rows[0].Name.Should().Be($"{prefix}_Replaced");
        rows[0].Age.Should().Be(2);
    }

    [Fact]
    public async Task InsertOrReplaceAsync_RolledBack_LeavesOriginalRow()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Original", Age = 1 });
        }

        long id = (await FindAsync(prefix)).Single().id;

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertOrReplaceAsync(new SimpleEntity { id = id, Name = $"{prefix}_Replaced", Age = 2 });
            await ctx.RollbackTransactionAsync();
        }

        (await FindAsync(prefix)).Should().ContainSingle(e => e.Name == $"{prefix}_Original");
    }

    [Fact]
    public async Task InsertOrReplaceAsync_AfterFault_IsSkipped()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            Action failedRead = () => ctx.GetTable<MissingTableEntity>().ToList();
            failedRead.Should().Throw<Exception>();

            int affected = await ctx.InsertOrReplaceAsync(new SimpleEntity { Name = $"{prefix}_Skipped", Age = 1 });
            affected.Should().Be(0, "writes after a fault are skipped");
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }
}
