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

    // Renames the SimpleEntity table inside the transaction so the existence check that
    // SaveAsync/DeleteAsync run before their own SQL fails. The rename is rolled back with the transaction.
    private static Task HideSimpleEntityTableAsync(SxmTransaction ctx) =>
        ctx.RunStatementAsync("ALTER TABLE SimpleEntity RENAME TO SimpleEntity_Hidden");

    // Early failure: SaveAsync's existence check fails before the INSERT runs.
    [Fact]
    public async Task EarlySaveAsyncFailure_Caught_FaultsAndRollsBack()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"EarlySaveCaught_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);

            Func<Task> act = () => new SimpleEntity { Name = $"{prefix}_Failed", Age = 2 }.SaveAsync();
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // Early failure that escapes the block uncaught must still roll back.
    [Fact]
    public async Task EarlySaveAsyncFailure_Uncaught_RollsBack()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"EarlySaveUncaught_{NewPrefix()}";

        Func<Task> act = async () =>
        {
            await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);
            await new SimpleEntity { Name = $"{prefix}_Failed", Age = 2 }.SaveAsync();
        };

        await act.Should().ThrowAsync<Exception>();
        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // Early failure: DeleteAsync's existence check fails before the DELETE runs.
    [Fact]
    public async Task EarlyDeleteAsyncFailure_Caught_FaultsAndRollsBack()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"EarlyDeleteCaught_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SimpleEntity saved = new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 };
            await saved.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);

            Func<Task> act = () => saved.DeleteAsync();
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // Early failure: the parameter-object overload fails during preparation, before the SQL runs.
    [Fact]
    public async Task EarlyRunStatementWithObjectFailure_Caught_FaultsAndRollsBack()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"EarlyRunStmt_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            Func<Task> act = () => ctx.RunStatementAsync<MissingTableEntity, MissingTableEntity>(
                "SELECT * FROM NoSuchTable_LinqReadFailure WHERE Name = @Name", new MissingTableEntity { Name = "x" });
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

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

            ctx.IsFaulted.Should().BeFalse();

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

            ctx.IsFaulted.Should().BeFalse();

            try
            {
                _ = ctx.GetTable<MissingTableEntity>().ToList();
            }
            catch (Exception)
            {
            }

            ctx.IsFaulted.Should().BeTrue();
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

            ctx.IsFaulted.Should().BeFalse();

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

            ctx.IsFaulted.Should().BeFalse();
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

            outer.IsFaulted.Should().BeFalse();
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

            ctx.IsFaulted.Should().BeFalse();
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

            ctx.IsFaulted.Should().BeTrue();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Should().BeEmpty("a failed raw SQL statement faults the transaction, so dispose rolls back");
    }

    // 6
    [Fact]
    public async Task IsFaulted_ResetByRollback_AndSharedByNestedTransaction()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"IsFaultedReset_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            outer.IsFaulted.Should().BeFalse();
            await new SimpleEntity { Name = $"{prefix}_Discarded", Age = 1 }.SaveAsync();

            await using (SxmTransaction inner = new SxmTransaction(TestDatabaseName))
            {
                try
                {
                    await inner.RunStatementAsync("INVALID SQL STATEMENT");
                }
                catch (Exception)
                {
                }

                inner.IsFaulted.Should().BeTrue();
            }

            outer.IsFaulted.Should().BeTrue("the inner transaction shares the outer transaction's state");

            await outer.RollbackTransactionAsync();
            outer.IsFaulted.Should().BeFalse("rollback resets the faulted state");

            await new SimpleEntity { Name = $"{prefix}_Kept", Age = 2 }.SaveAsync();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(new[] { $"{prefix}_Kept" });
    }

    // 7
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

                ctx.IsFaulted.Should().BeFalse();
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

            ctx.IsFaulted.Should().BeTrue();
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

            ctx.IsFaulted.Should().BeTrue("a cancelled operation is an interrupted operation, so the transaction is faulted");
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
        outer.IsFaulted.Should().BeFalse();
    }

    // 10
    [Fact]
    public async Task NestedTransaction_ForDifferentDatabase_CaughtWithoutRollback_OuterWorkCommits()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"DiffDbNoRb_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Order", Age = 1 }.SaveAsync();

            try
            {
                await using SxmTransaction inner = new SxmTransaction("some_other_database");
                await new SimpleEntity { Name = $"{prefix}_Payment", Age = 2 }.SaveAsync();
            }
            catch (Exception)
            {
            }

            outer.IsFaulted.Should().BeFalse("the constructor failure happens before any SQLiteXM operation runs");
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(new[] { $"{prefix}_Order" });
        await CleanupAsync(found);
    }

    // 10
    [Fact]
    public async Task NestedTransaction_ForDifferentDatabase_CaughtWithRollback_DiscardsOuterWork()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"DiffDbRb_{NewPrefix()}";

        await using (SxmTransaction outer = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Order", Age = 1 }.SaveAsync();

            try
            {
                await using SxmTransaction inner = new SxmTransaction("some_other_database");
                await new SimpleEntity { Name = $"{prefix}_Payment", Age = 2 }.SaveAsync();
            }
            catch (Exception)
            {
                await outer.RollbackTransactionAsync();
            }
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 11
    [Fact]
    public async Task CancelledLinqWrite_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"CancelledWrite_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, 99)
                .UpdateAsync(cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 12
    [Fact]
    public async Task FailedLinqUpdate_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"FailedUpdate_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);

            Func<Task> act = () => ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, 99)
                .UpdateAsync();
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 12
    [Fact]
    public async Task FailedLinqDelete_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"FailedDelete_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);

            Func<Task> act = () => ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .DeleteAsync();
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 12
    [Fact]
    public async Task FailedBulkInsert_FaultsTransaction_AndRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"FailedBulk_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();
            await HideSimpleEntityTableAsync(ctx);

            List<SimpleEntity> batch = new List<SimpleEntity>
            {
                new SimpleEntity { Name = $"{prefix}_Bulk1", Age = 2 },
                new SimpleEntity { Name = $"{prefix}_Bulk2", Age = 3 }
            };
            Func<Task> act = () => ctx.BulkInsertAsync(batch);
            await act.Should().ThrowAsync<Exception>();

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 13
    [Fact]
    public async Task FaultedTransaction_SkipsLinqWrites_AndReturnsZero()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"SkippedWrites_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Saved", Age = 1 }.SaveAsync();

            Func<Task> fail = () => new ChildEntity { ChildName = $"{prefix}_Orphan", ParentId = long.MaxValue }.SaveAsync();
            await fail.Should().ThrowAsync<Exception>();
            ctx.IsFaulted.Should().BeTrue();

            int updated = await ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, 99)
                .UpdateAsync();
            int deleted = await ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .DeleteAsync();
            int inserted = await ctx.BulkInsertAsync(new List<SimpleEntity>
            {
                new SimpleEntity { Name = $"{prefix}_Bulk", Age = 2 }
            });

            updated.Should().Be(0);
            deleted.Should().Be(0);
            inserted.Should().Be(0);

            ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList()
                .Should().ContainSingle(e => e.Age == 1, "skipped writes change nothing");
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    // 14
    [Fact]
    public async Task UncommittedSave_IsNotVisibleToSeparateTransaction()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"SeparateTx_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Pending", Age = 1 }.SaveAsync();

            Task<List<SimpleEntity>> otherTask;
            using (ExecutionContext.SuppressFlow())
            {
                otherTask = Task.Run(async () =>
                {
                    await using SxmTransaction other = new SxmTransaction(TestDatabaseName);
                    return other.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
                });
            }

            // Depending on journal mode the other reader either sees only committed data or is blocked.
            try
            {
                List<SimpleEntity> seenByOther = await otherTask;
                seenByOther.Should().BeEmpty("a separate transaction only sees committed data");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 5)
            {
            }

            await ctx.RollbackTransactionAsync();
        }
    }

    // 15
    [Fact]
    public async Task StagedBatches_FailedBatchRolledBack_LaterBatchesStillCommit()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"Staged_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await new SimpleEntity { Name = $"{prefix}_Batch1", Age = 1 }.SaveAsync();
            await ctx.CommitTransactionAsync();

            try
            {
                await new SimpleEntity { Name = $"{prefix}_Batch2", Age = 2 }.SaveAsync();
                await new ChildEntity { ChildName = $"{prefix}_Orphan", ParentId = long.MaxValue }.SaveAsync();
                await ctx.CommitTransactionAsync();
            }
            catch (Exception)
            {
                ctx.IsFaulted.Should().BeTrue();
                await ctx.RollbackTransactionAsync();
            }

            ctx.IsFaulted.Should().BeFalse();

            await new SimpleEntity { Name = $"{prefix}_Batch3", Age = 3 }.SaveAsync();
            await ctx.CommitTransactionAsync();
        }

        List<SimpleEntity> found = await FindAsync(prefix);
        found.Select(e => e.Name).Should().BeEquivalentTo(new[] { $"{prefix}_Batch1", $"{prefix}_Batch3" });
        await CleanupAsync(found);
    }

    private static async Task CleanupAsync(List<SimpleEntity> entities)
    {
        foreach (SimpleEntity entity in entities)
            await entity.DeleteAsync();
    }
}
