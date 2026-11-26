using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Entity mapped to a table that is never created, used to force a LINQ read to fail.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(Name = "NoSuchTable_LinqReadFailure", IsColumnAttributeRequired = false)]
public class MissingTableEntity
{
    public long id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// Verifies that a failed LINQ read marks the SxmTransaction as faulted, so the transaction
/// rolls back on dispose, exactly like a failed write.
/// </summary>
[Collection("Sequential")]
public class LinqReadFailureTests : TestBase
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
    public async Task FailedLinqRead_SetsFaulted()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

        Action act = () => ctx.GetTable<MissingTableEntity>().ToList();

        act.Should().Throw<Exception>();
        ctx.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public async Task FailedLinqReadAsync_SetsFaulted()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

        Func<Task> act = async () => await ctx.GetTable<MissingTableEntity>().CountAsync();

        await act.Should().ThrowAsync<Exception>();
        ctx.IsFaulted.Should().BeTrue();
    }

    [Fact]
    public async Task SuccessfulLinqRead_DoesNotSetFaulted()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

        _ = ctx.GetTable<SimpleEntity>().Where(e => e.Name == NewPrefix()).ToList();

        ctx.IsFaulted.Should().BeFalse();
    }

    [Fact]
    public async Task FailedLinqRead_RollsBackEarlierWritesOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"ReadFail_{NewPrefix()}";

        Func<Task> act = async () =>
        {
            await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Linq", Age = 1 });
            await new SimpleEntity { Name = $"{prefix}_Save", Age = 2 }.SaveAsync();

            _ = ctx.GetTable<MissingTableEntity>().ToList();
        };

        await act.Should().ThrowAsync<Exception>();
        (await FindAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task FailedLinqRead_CaughtByUser_StillRollsBackOnDispose()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"ReadFailCaught_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Linq", Age = 1 });

            try
            {
                _ = ctx.GetTable<MissingTableEntity>().ToList();
            }
            catch (Exception)
            {
                // Swallowed by the caller; the transaction is still faulted.
            }

            ctx.IsFaulted.Should().BeTrue();
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task FailedLinqRead_SkipsSubsequentWrites()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"ReadFailSkip_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            Action act = () => ctx.GetTable<MissingTableEntity>().ToList();
            act.Should().Throw<Exception>();

            int inserted = await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Skipped", Age = 1 });
            inserted.Should().Be(0, "writes after a fault are skipped");
        }

        (await FindAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task FailedLinqRead_CommitThrows()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);

        Action read = () => ctx.GetTable<MissingTableEntity>().ToList();
        read.Should().Throw<Exception>();

        Func<Task> commit = () => ctx.CommitTransactionAsync();
        await commit.Should().ThrowAsync<InvalidOperationException>();

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task FailedLinqRead_ThenRollback_ResetsFaultAndLaterWritesCommit()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"ReadFailReset_{NewPrefix()}";

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Discarded", Age = 1 });

            Action act = () => ctx.GetTable<MissingTableEntity>().ToList();
            act.Should().Throw<Exception>();

            await ctx.RollbackTransactionAsync();
            ctx.IsFaulted.Should().BeFalse();

            int inserted = await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_Kept", Age = 2 });
            inserted.Should().Be(1);

            // The context's LINQ reads work again after the reset.
            ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList()
                .Should().ContainSingle();
        }

        List<SimpleEntity> results = await FindAsync(prefix);
        results.Should().ContainSingle().Which.Name.Should().Be($"{prefix}_Kept");

        await using SxmTransaction cleanup = new SxmTransaction(TestDatabaseName);
        await cleanup.DeleteAsync(results[0]);
    }

    [Fact]
    public async Task FailedLinqRead_InJoinedContext_RollsBackOuterTransaction()
    {
        await InitializeSqliteXMAsync();
        string prefix = $"ReadFailJoin_{NewPrefix()}";

        Func<Task> act = async () =>
        {
            await using SxmTransaction outer = new SxmTransaction(TestDatabaseName);
            await new SimpleEntity { Name = $"{prefix}_Outer", Age = 1 }.SaveAsync();

            await using (SxmTransaction inner = new SxmTransaction())
            {
                _ = inner.GetTable<MissingTableEntity>().ToList();
            }
        };

        await act.Should().ThrowAsync<Exception>();
        (await FindAsync(prefix)).Should().BeEmpty();
    }
}
