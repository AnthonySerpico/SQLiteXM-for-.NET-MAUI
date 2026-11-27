using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for <see cref="SxmSqlTransaction"/> statement running and fault handling, and for
/// out-of-order disposal handling in <see cref="SxmAmbientTransaction"/>.
/// </summary>
[Collection("Sequential")]
public class SqlTransactionAmbientTests : TestBase
{
    public class ValueRow
    {
        public long v { get; set; }
    }

    private static async Task<SxmSqlTransaction> CreateInOtherContextAsync()
    {
        // Task.Run captures a copy of the execution context, so the transaction becomes ambient
        // only inside that task; the caller's ambient stack is unaffected.
        return await Task.Run(() => SxmSqlTransaction.Create(TestDatabaseName));
    }

    [Fact]
    public async Task Create_PushesAmbient_DisposePopsIt()
    {
        SxmAmbientTransaction.Current.Should().BeNull();

        SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);
        SxmAmbientTransaction.Current.Should().BeSameAs(tx);

        await tx.DisposeAsync();
        SxmAmbientTransaction.Current.Should().BeNull();
    }

    [Fact]
    public async Task RunStatement_DirectSelect_AllOverloads_ReturnRows()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);
        const string sql = "SELECT 1 AS v UNION ALL SELECT 2";

        List<Dictionary<string, object?>> fromList = await tx.RunStatementAsync(sql, new List<object>());
        List<Dictionary<string, object?>> fromDict = await tx.RunStatementAsync(sql, new Dictionary<string, object?>());
        List<ValueRow> typedList = await tx.RunStatementAsync<ValueRow>(sql, new List<object>());
        List<ValueRow> typedDict = await tx.RunStatementAsync<ValueRow>(sql, new Dictionary<string, object?>());

        fromList.Should().HaveCount(2);
        fromDict.Should().HaveCount(2);
        typedList.Select(r => r.v).Should().Equal(1L, 2L);
        typedDict.Select(r => r.v).Should().Equal(1L, 2L);
        tx.EncounteredError.Should().BeFalse();
    }

    [Fact]
    public async Task RunStatement_InvalidSql_Throws_AndFaults()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);

        Func<Task> act = () => tx.RunStatementAsync("SELECT * FROM no_such_table_xyz", new List<object>());

        await act.Should().ThrowAsync<Exception>();
        tx.EncounteredError.Should().BeTrue();
    }

    [Fact]
    public async Task RunStatement_UnrecognisedText_Throws_AndFaults()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);

        Func<Task> act = () => tx.RunStatementAsync("this is not sql", new List<object>());

        await act.Should().ThrowAsync<Exception>();
        tx.EncounteredError.Should().BeTrue();
    }

    [Fact]
    public async Task RunStatement_AfterFault_IsSkippedAndReturnsEmpty()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);
        tx.EncounteredError = true;

        List<Dictionary<string, object?>> rows = await tx.RunStatementAsync("SELECT 1 AS v", new List<object>());

        rows.Should().BeEmpty("statements after a failure are skipped to preserve all-or-nothing behaviour");
    }

    [Fact]
    public async Task RunStatement_EntityOverload_UnrecognisedText_Faults()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);

        Func<Task> untyped = () => tx.RunStatementAsync("not sql at all", new ValueRow());
        await untyped.Should().ThrowAsync<Exception>();
        tx.EncounteredError.Should().BeTrue();
    }

    [Fact]
    public async Task RunStatement_TypedEntityOverload_UnrecognisedText_Faults()
    {
        await using SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName);

        Func<Task> typed = () => tx.RunStatementAsync<ValueRow, ValueRow>("not sql at all", new ValueRow());
        await typed.Should().ThrowAsync<Exception>();
        tx.EncounteredError.Should().BeTrue();
    }

    // ---------- Ambient stack: out-of-order handling ----------

    [Fact]
    public async Task TryRemoveAndPop_ForTransactionNotOnThisStack_LeaveStackIntact()
    {
        SxmSqlTransaction foreign = await CreateInOtherContextAsync();
        try
        {
            await using SxmSqlTransaction mine = SxmSqlTransaction.Create(TestDatabaseName);

            SxmAmbientTransaction.TryRemove(foreign).Should().BeFalse();
            SxmAmbientTransaction.Current.Should().BeSameAs(mine, "the rebuild path must preserve other entries");

            Action pop = () => SxmAmbientTransaction.Pop(foreign);
            pop.Should().Throw<InvalidOperationException>();
            SxmAmbientTransaction.Current.Should().BeSameAs(mine);
        }
        finally
        {
            await Task.Run(async () => await foreign.DisposeAsync());
        }

        SxmAmbientTransaction.Current.Should().BeNull();
    }

    [Fact]
    public async Task Dispose_OfNonTopTransaction_DoesNotDisturbCurrentAmbient()
    {
        SxmSqlTransaction foreign = await CreateInOtherContextAsync();

        await using (SxmSqlTransaction mine = SxmSqlTransaction.Create(TestDatabaseName))
        {
            await foreign.DisposeAsync();

            SxmAmbientTransaction.Current.Should().BeSameAs(mine);
            foreign.Connection.Should().BeNull("the foreign transaction's private connection is still released");
        }

        SxmAmbientTransaction.Current.Should().BeNull();
    }

    [Fact]
    public async Task Dispose_WithNoAmbient_StillReleasesConnection()
    {
        SxmSqlTransaction foreign = await CreateInOtherContextAsync();
        SxmAmbientTransaction.Current.Should().BeNull();

        await foreign.DisposeAsync();

        foreign.Connection.Should().BeNull();
        SxmAmbientTransaction.Current.Should().BeNull();
    }

    [Fact]
    public async Task Dispose_Faulted_RollsBack_Healthy_AutoCommits()
    {
        string table = $"sqltx_{Guid.NewGuid():N}";
        await using (SxmUTransaction setup = SxmUTransaction.Create(TestDatabaseName))
        {
            await setup.ExecuteTableStatementAsync($"CREATE TABLE \"{table}\" (v INTEGER)");
            await setup.CommitTransactionAsync();
        }

        try
        {
            await using (SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName))
            {
                await tx.RunStatementAsync($"INSERT INTO \"{table}\" (v) VALUES (1)", new List<object>());
                Func<Task> bad = () => tx.RunStatementAsync("SELECT * FROM no_such_table_xyz", new List<object>());
                await bad.Should().ThrowAsync<Exception>();
            }

            (await CountAsync(table)).Should().Be(0L, "a faulted transaction rolls back on dispose");

            await using (SxmSqlTransaction tx = SxmSqlTransaction.Create(TestDatabaseName))
            {
                await tx.RunStatementAsync($"INSERT INTO \"{table}\" (v) VALUES (2)", new List<object>());
            }

            (await CountAsync(table)).Should().Be(1L, "a healthy ambient transaction auto-commits on dispose");
        }
        finally
        {
            await using SxmUTransaction drop = SxmUTransaction.Create(TestDatabaseName);
            await drop.ExecuteTableStatementAsync($"DROP TABLE IF EXISTS \"{table}\"");
            await drop.CommitTransactionAsync();
        }
    }

    private static async Task<long> CountAsync(string table)
    {
        await using SxmUTransaction check = SxmUTransaction.Create(TestDatabaseName);
        List<Dictionary<string, object?>> rows = await check.ExecuteReadReturningAsync($"SELECT COUNT(*) AS c FROM \"{table}\"", null);
        await check.CommitTransactionAsync();
        return (long)rows[0]["c"]!;
    }
}
