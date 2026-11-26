using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Verifies how SxmTransaction commits or rolls back on dispose.
/// </summary>
[Collection("Sequential")]
public class SxmTransactionBehaviorTests : TestBase
{
    private static string NewPrefix() => Guid.NewGuid().ToString("N").Substring(0, 8);

    private static async Task<List<SimpleEntity>> FindAsync(string prefix)
    {
        await using SxmTransaction verify = new SxmTransaction(TestDatabaseName);
        return verify.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith(prefix))
            .ToList();
    }

    [Fact]
    public async Task UserException_AfterSaveAsync_CommitsOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"UserThrow_{NewPrefix()}";

        Func<Task> act = async () =>
        {
            await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

            SimpleEntity entity = new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 };
            await entity.SaveAsync();

            ctx.Faulted.Should().BeFalse();

            throw new InvalidOperationException("Exception thrown by user code.");
        };

        await act.Should().ThrowAsync<InvalidOperationException>();

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().ContainSingle(
            "an exception thrown by user code is not visible to SxmTransaction, so dispose commits");
    }

    [Fact]
    public async Task CaughtFailedLinqRead_AfterSaveAsync_RollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CaughtLinqFail_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SimpleEntity entity = new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 };
            await entity.SaveAsync();

            ctx.Faulted.Should().BeFalse();

            try
            {
                _ = ctx.GetTable<MissingTableEntity>().ToList();
            }
            catch (Exception)
            {
            }

            ctx.Faulted.Should().BeTrue();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().BeEmpty(
            "a failed LINQ read faults the transaction, so dispose rolls back even when the exception is caught");
    }

    [Fact]
    public async Task UserException_RollbackInCatch_ThenSaveAsync_CommitsOnlySecondEntity()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"RollbackThenSave_{NewPrefix()}";
        string firstName = $"{prefix}_First";
        string secondName = $"{prefix}_Second";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SimpleEntity first = new SimpleEntity { Name = firstName, Age = 1 };
            await first.SaveAsync();

            try
            {
                throw new InvalidOperationException("Exception thrown by user code.");
            }
            catch (InvalidOperationException)
            {
                await ctx.RollbackTransactionAsync();
            }

            ctx.Faulted.Should().BeFalse();

            SimpleEntity second = new SimpleEntity { Name = secondName, Age = 2 };
            await second.SaveAsync();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().ContainSingle("only the entity saved after the rollback should be persisted");
        found[0].Name.Should().Be(secondName);
        found.Should().NotContain(e => e.Name == firstName, "the first save was rolled back");
    }

    [Fact]
    public async Task CommitFirst_ThenUserException_RollbackInCatch_DiscardsOnlySecondEntity()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CommitThenRollback_{NewPrefix()}";
        string firstName = $"{prefix}_First";
        string secondName = $"{prefix}_Second";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SimpleEntity first = new SimpleEntity { Name = firstName, Age = 1 };
            await first.SaveAsync();

            await ctx.CommitTransactionAsync();

            SimpleEntity second = new SimpleEntity { Name = secondName, Age = 2 };
            await second.SaveAsync();

            try
            {
                throw new InvalidOperationException("Exception thrown by user code.");
            }
            catch (InvalidOperationException)
            {
                await ctx.RollbackTransactionAsync();
            }

            ctx.Faulted.Should().BeFalse();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().ContainSingle("only the entity committed before the rollback should be persisted");
        found[0].Name.Should().Be(firstName);
        found.Should().NotContain(e => e.Name == secondName, "the second save was rolled back");
    }

    // 1
    [Fact]
    public async Task UserException_InJoinedInnerTransaction_OuterStillCommits()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"JoinedUserThrow_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Outer", Age = 1 }.SaveAsync();

            try
            {
                await using SxmTransaction inner = new SxmTransaction();
                await new SimpleEntity { Name = $"{prefix}_Inner", Age = 2 }.SaveAsync();
                throw new InvalidOperationException("Exception thrown by user code.");
            }
            catch (InvalidOperationException)
            {
            }

            outer.Faulted.Should().BeFalse();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().HaveCount(2, "a user exception is not visible to SxmTransaction, so the outer transaction commits both saves");
    }

    // 2a
    [Fact]
    public async Task JoinedInnerTransaction_Rollback_DiscardsOuterWorkToo()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"JoinedInnerRollback_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_BeforeInner", Age = 1 }.SaveAsync();

            await using (SxmTransaction inner = new SxmTransaction())
            {
                await new SimpleEntity { Name = $"{prefix}_Inner", Age = 2 }.SaveAsync();
                await inner.RollbackTransactionAsync();
            }

            await new SimpleEntity { Name = $"{prefix}_AfterInner", Age = 3 }.SaveAsync();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(
            new[] { $"{prefix}_AfterInner" },
            "the inner rollback ends the shared database transaction, discarding the outer's earlier work");
    }

    // 2b
    [Fact]
    public async Task JoinedInnerTransaction_Commit_CommitsOuterWorkEarly()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"JoinedInnerCommit_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_BeforeInner", Age = 1 }.SaveAsync();

            await using (SxmTransaction inner = new SxmTransaction())
            {
                await new SimpleEntity { Name = $"{prefix}_Inner", Age = 2 }.SaveAsync();
                await inner.CommitTransactionAsync();
            }

            await new SimpleEntity { Name = $"{prefix}_AfterInner", Age = 3 }.SaveAsync();
            await outer.RollbackTransactionAsync();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(
            new[] { $"{prefix}_BeforeInner", $"{prefix}_Inner" },
            "the inner commit ends the shared database transaction, so the outer rollback only discards later work");
    }

    // 3
    [Fact]
    public async Task RepeatedCommitAndRollback_WithNoPendingWork_AreNoOps()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"RepeatNoOp_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.CommitTransactionAsync();
            await ctx.CommitTransactionAsync();
            await ctx.RollbackTransactionAsync();
            await ctx.RollbackTransactionAsync();

            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await ctx.CommitTransactionAsync();
            await ctx.CommitTransactionAsync();
            await ctx.RollbackTransactionAsync();

            ctx.Faulted.Should().BeFalse();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().ContainSingle("the committed save is not affected by repeated commit or rollback calls");
    }

    // 4
    [Fact]
    public async Task DisposedTransaction_CommitRollbackAndQuery_Throw()
    {
        await InitializeSqliteXMAsync();

        SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await ctx.DisposeAsync();

        Func<Task> commit = () => ctx.CommitTransactionAsync();
        Func<Task> rollback = () => ctx.RollbackTransactionAsync();
        Action query = () => ctx.GetTable<SimpleEntity>().ToList();

        await commit.Should().ThrowAsync<ObjectDisposedException>();
        await rollback.Should().ThrowAsync<ObjectDisposedException>();
        query.Should().Throw<Exception>();
    }

    // 4b
    [Fact]
    public async Task SaveAsync_AfterTransactionDisposed_PersistsOnItsOwn()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"SaveAfterDispose_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.RollbackTransactionAsync();
        }

        await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().ContainSingle("SaveAsync outside a transaction saves immediately");
    }

    // 5
    [Fact]
    public async Task CaughtFailedRawSql_AfterSaveAsync_RollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CaughtRawSqlFail_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            try
            {
                await ctx.RunStatementAsync("INVALID SQL STATEMENT");
            }
            catch (Exception)
            {
            }

            ctx.Faulted.Should().BeTrue();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().BeEmpty("a failed raw SQL statement faults the transaction, so dispose rolls back");
    }

    // 6
    [Fact]
    public async Task RepeatedCommitSaveRollbackCycles_KeepOnlyCommittedEntities()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"Cycles_{NewPrefix()}";
        const int cycles = 5;

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            for (int i = 0; i < cycles; i++)
            {
                await new SimpleEntity { Name = $"{prefix}_Kept{i}", Age = i }.SaveAsync();
                await ctx.CommitTransactionAsync();

                await new SimpleEntity { Name = $"{prefix}_Discarded{i}", Age = i }.SaveAsync();
                await ctx.RollbackTransactionAsync();

                ctx.Faulted.Should().BeFalse();
            }
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, cycles).Select(i => $"{prefix}_Kept{i}"));
    }

    // 7
    [Fact]
    public async Task UncommittedSave_IsVisibleInside_AndGoneAfterRollback()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"Isolation_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Pending", Age = 1 }.SaveAsync();

            ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList()
                .Should().ContainSingle("the transaction sees its own uncommitted save");

            await ctx.RollbackTransactionAsync();

            ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList()
                .Should().BeEmpty("the rolled-back save is no longer visible inside the transaction");
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 8
    [Fact]
    public async Task CaughtFailedSaveAsync_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CaughtSaveFail_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            try
            {
                await new ChildEntity { ChildName = $"{prefix}_Orphan", ParentId = long.MaxValue }.SaveAsync();
            }
            catch (Exception)
            {
            }

            ctx.Faulted.Should().BeTrue();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().BeEmpty("a failed SaveAsync faults the transaction, so dispose rolls back");
    }

    // 9
    [Fact]
    public async Task CancelledLinqQuery_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CancelledRead_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = async () => await ctx.GetTable<SimpleEntity>().ToListAsync(cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();

            ctx.Faulted.Should().BeTrue("a cancelled operation is an interrupted operation, so the transaction is faulted");
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().BeEmpty("the transaction was faulted by the cancellation, so dispose rolls back");
    }

    // 10
    [Fact]
    public async Task NestedTransaction_ForDifferentDatabase_Throws()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction outer = new SxmTransaction(TestDatabaseName);

        Action act = () => new SxmTransaction("some_other_database").Dispose();

        act.Should().Throw<Exception>()
            .Where(e => e.Message.Contains("ambient transaction") || (e.InnerException != null && e.InnerException.Message.Contains("ambient transaction")));
        outer.Faulted.Should().BeFalse();
    }
}
