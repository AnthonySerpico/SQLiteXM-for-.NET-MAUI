using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Direct tests for the internal <see cref="SxmUTransaction"/> wrapper: factories, named and direct
/// statement execution, commit/rollback, attach/detach and disposal.
/// Each test works in its own uniquely named table which is dropped afterwards.
/// </summary>
[Collection("Sequential")]
public class SxmUTransactionTests : TestBase
{
    private static string NewTable() => $"ut_{Guid.NewGuid():N}";

    private static async Task CreateTableAsync(string table, bool withSynchId = true)
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        string synch = withSynchId ? ", synchId BLOB" : string.Empty;
        await t.ExecuteTableStatementAsync($"CREATE TABLE \"{table}\" (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT{synch})");
        await t.CommitTransactionAsync();
    }

    private static async Task DropTableAsync(string table)
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.ExecuteTableStatementAsync($"DROP TABLE IF EXISTS \"{table}\"");
        await t.CommitTransactionAsync();
    }

    private static async Task<long> CountAsync(string table)
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        List<Dictionary<string, object?>> rows = await t.ExecuteReadReturningAsync($"SELECT COUNT(*) AS c FROM \"{table}\"", null);
        await t.CommitTransactionAsync();
        return (long)rows[0]["c"]!;
    }

    // ---------- Factories ----------

    [Fact]
    public async Task Create_UsesPrivateConnection_AndDisposeReleasesIt()
    {
        SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        t.Connection.Should().NotBeNull();
        t.Connection!.Shared.Should().BeFalse();

        await t.DisposeAsync();

        t.Connection.Should().BeNull("a private connection is destroyed and dropped on dispose");
    }

    [Fact]
    public async Task CreateAsync_NullConnection_Throws()
    {
        Func<Task> act = () => SxmUTransaction.CreateAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateAsync_PrivateConnection_DoesNotLock()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: false);
        SxmUTransaction t = await SxmUTransaction.CreateAsync(conn);

        t.Connection.Should().BeSameAs(conn);
        await t.DisposeAsync();
        t.Connection.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_SharedConnection_LocksAndReleasesOnDispose()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            SxmUTransaction first = await SxmUTransaction.CreateAsync(conn, waitMilliseconds: 100);

            Func<Task> second = () => SxmUTransaction.CreateAsync(conn, waitMilliseconds: 50);
            (await second.Should().ThrowAsync<SxmException>())
                .Which.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.LockDb);

            await first.DisposeAsync();
            first.Connection.Should().BeSameAs(conn, "shared connections are caller-owned and not dropped");

            await using SxmUTransaction third = await SxmUTransaction.CreateAsync(conn, waitMilliseconds: 100);
            third.Connection.Should().BeSameAs(conn, "the lock was released by the first dispose");
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task CreateAsync_CancelledWhileWaitingForSharedLock_Throws()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            await using SxmUTransaction holder = await SxmUTransaction.CreateAsync(conn);
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> act = () => SxmUTransaction.CreateAsync(conn, 5000, cts.Token);
            await act.Should().ThrowAsync<Exception>();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Dispose_Sync_And_DoubleDispose_AreSafe()
    {
        SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        t.Dispose();
        t.Dispose();
        await t.DisposeAsync();
        t.Connection.Should().BeNull();
    }

    // ---------- Commit / rollback ----------

    [Fact]
    public async Task UpdateDirect_Commit_Persists_Rollback_Discards()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using (SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName))
            {
                await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES (@p0)", new List<object> { "kept" });
                await t.CommitTransactionAsync();
            }

            await using (SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName))
            {
                await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES (@p0)", new List<object> { "discarded" });
                await t.RollbackTransactionAsync();
            }

            (await CountAsync(table)).Should().Be(1);
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task Transaction_IsReusableAfterCommit()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using (SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName))
            {
                await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES ('a')", null);
                await t.CommitTransactionAsync();
                await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES ('b')", null);
                await t.CommitTransactionAsync();
            }

            (await CountAsync(table)).Should().Be(2);
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task DeleteDirect_And_SystemUpdateDirect_Work()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using (SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName))
            {
                await t.ExecuteSystemUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES ('a'), ('b'), ('c')", null);
                await t.ExecuteDeleteDirectAsync($"DELETE FROM \"{table}\" WHERE name = @p0", new List<object> { "b" });
                await t.CommitTransactionAsync();
            }

            (await CountAsync(table)).Should().Be(2);
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task AlterTable_Index_And_Trigger_Statements_Execute()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using (SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName))
            {
                await t.ExecuteAlterTableAsync($"ALTER TABLE \"{table}\" ADD COLUMN extra INTEGER DEFAULT 0");
                await t.ExecuteIndexAsync($"CREATE INDEX \"ix_{table}\" ON \"{table}\" (name)");
                await t.ExecuteCreateTriggerAsync(
                    $"CREATE TRIGGER \"tr_{table}\" AFTER INSERT ON \"{table}\" BEGIN UPDATE \"{table}\" SET extra = 7 WHERE id = NEW.id; END");
                await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES ('x')", null);

                List<Dictionary<string, object?>> rows = await t.ExecuteReadReturningAsync($"SELECT extra FROM \"{table}\"", null);
                rows.Should().ContainSingle().Which["extra"].Should().Be(7L, "the trigger fired");

                List<Dictionary<string, object?>> idx = await t.ExecuteReadReturningAsync(
                    "SELECT name FROM sqlite_master WHERE type = 'index' AND name = @p0", new List<object> { $"ix_{table}" });
                idx.Should().ContainSingle();
                await t.CommitTransactionAsync();
            }
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task WriteReturning_ReturnsInsertedRows()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            List<Dictionary<string, object?>> rows = await t.ExecuteWriteReturningAsync(
                $"INSERT INTO \"{table}\" (name) VALUES (@p0), (@p1) RETURNING id, name", new List<object> { "a", "b" });

            rows.Should().HaveCount(2);
            rows[1]["name"].Should().Be("b");
            await t.RollbackTransactionAsync();
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task QueryDirect_ThenGetAllRows_ReturnsAllRows()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.ExecuteQueryDirectAsync("SELECT 1 AS v UNION ALL SELECT 2 UNION ALL SELECT 3", null);

        List<Dictionary<string, object?>> rows = t.GetAllRows<Dictionary<string, object?>>();

        rows.Should().HaveCount(3);
        rows[2]["v"].Should().Be(3L);
        t.GetAllRows<Dictionary<string, object?>>().Should().BeEmpty("the reader is exhausted");
    }

    [Fact]
    public async Task InvalidSql_PropagatesSqliteException()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        Func<Task> act = () => t.ExecuteUpdateDirectAsync("UPDATE no_such_table_xyz SET a = 1", null);
        await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>();
    }

    [Fact]
    public async Task CancelledToken_AbortsStatement()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => t.ExecuteQueryDirectAsync("SELECT 1", null, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------- Named statements ----------

    [Fact]
    public async Task NamedStatements_UnknownNames_ThrowUnknownSqlStatement()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        string missing = $"missing_{Guid.NewGuid():N}";

        List<Func<Task>> calls = new List<Func<Task>>
        {
            () => t.ExecuteInsertAsync(missing, new List<object>()),
            () => t.ExecuteUpdateAsync(missing, new List<object>()),
            () => t.ExecuteDeleteAsync(missing, new List<object>()),
            () => t.ExecuteQueryAsync(missing, null, SxmDefines.SqlStatementType.Select),
            () => t.ExecuteQueryAsync(missing, null, SxmDefines.SqlStatementType.Insert),
            () => t.ExecuteQueryAsync(missing, null, SxmDefines.SqlStatementType.Update),
            () => t.ExecuteQueryAsync(missing, null, SxmDefines.SqlStatementType.Delete)
        };

        foreach (Func<Task> call in calls)
        {
            (await call.Should().ThrowAsync<SxmException>())
                .Which.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.UnknownSqlStatement);
        }
    }

    [Fact]
    public async Task NamedStatements_RegisteredDefinitions_Execute()
    {
        string table = NewTable();
        string key = $"k_{Guid.NewGuid():N}";
        await CreateTableAsync(table);

        SxmSqlStatements.InsertStatements[key] = new InsertDefinition(table, $"INSERT INTO \"{table}\" (name) VALUES (@p0)");
        SxmSqlStatements.UpdateStatements[key] = new UpdateDefinition(table, $"UPDATE \"{table}\" SET name = @p0 WHERE name = @p1");
        SxmSqlStatements.DeleteStatements[key] = new DeleteDefinition(table, $"DELETE FROM \"{table}\" WHERE name = @p0");
        SxmSqlStatements.SelectStatements[key] = new SelectDefinition(table, $"SELECT id, name, synchId FROM \"{table}\" ORDER BY id");
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);

            Dictionary<string, object?> first = await t.ExecuteInsertAsync(key, new List<object> { "a" });
            Dictionary<string, object?> second = await t.ExecuteInsertAsync(key, new List<object> { "b" });

            ((long)first["id"]!).Should().BeGreaterThan(0);
            ((long)second["id"]!).Should().Be((long)first["id"]! + 1);
            first["synchId"].Should().BeOfType<byte[]>().Which.Should().HaveCount(16);

            await t.ExecuteUpdateAsync(key, new List<object> { "B", "b" });
            await t.ExecuteDeleteAsync(key, new List<object> { "a" });
            await t.ExecuteQueryAsync(key, null, SxmDefines.SqlStatementType.Select);
            List<Dictionary<string, object?>> rows = t.GetAllRows<Dictionary<string, object?>>();

            rows.Should().ContainSingle();
            rows[0]["name"].Should().Be("B");
            rows[0]["synchId"].Should().BeEquivalentTo(second["synchId"], "the generated synchId is written back to the row");

            byte[]? synch = await t.GetSynchIdAsync(table, (long)second["id"]!);
            synch.Should().BeEquivalentTo((byte[])second["synchId"]!);
            await t.CommitTransactionAsync();
        }
        finally
        {
            SxmSqlStatements.InsertStatements.TryRemove(key, out _);
            SxmSqlStatements.UpdateStatements.TryRemove(key, out _);
            SxmSqlStatements.DeleteStatements.TryRemove(key, out _);
            SxmSqlStatements.SelectStatements.TryRemove(key, out _);
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task NamedInsert_EmptyTableName_SkipsSynchIdHandling()
    {
        string table = NewTable();
        string key = $"k_{Guid.NewGuid():N}";
        await CreateTableAsync(table, withSynchId: false);
        SxmSqlStatements.InsertStatements[key] = new InsertDefinition(string.Empty, $"INSERT INTO \"{table}\" (name) VALUES (@p0)");
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            Dictionary<string, object?> result = await t.ExecuteInsertAsync(key, new List<object> { "a" });

            result["id"].Should().Be(-1L);
            result["synchId"].Should().BeNull();
            await t.CommitTransactionAsync();
        }
        finally
        {
            SxmSqlStatements.InsertStatements.TryRemove(key, out _);
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task NamedInsert_TableWithoutSynchIdColumn_IsWrappedAsQueryFailure()
    {
        string table = NewTable();
        string key = $"k_{Guid.NewGuid():N}";
        await CreateTableAsync(table, withSynchId: false);
        SxmSqlStatements.InsertStatements[key] = new InsertDefinition(table, $"INSERT INTO \"{table}\" (name) VALUES (@p0)");
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            Func<Task> act = () => t.ExecuteInsertAsync(key, new List<object> { "a" });

            await act.Should().ThrowAsync<Exception>();
            await t.RollbackTransactionAsync();
        }
        finally
        {
            SxmSqlStatements.InsertStatements.TryRemove(key, out _);
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task GetSynchIdAsync_MissingRowOrTable_GeneratesNewGuidBytes()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);

        byte[]? a = await t.GetSynchIdAsync($"missing_{Guid.NewGuid():N}", 1);
        byte[]? b = await t.GetSynchIdAsync($"missing_{Guid.NewGuid():N}", 1);

        a.Should().HaveCount(16);
        b.Should().HaveCount(16);
        a.Should().NotEqual(b!);
    }

    [Fact]
    public async Task GetLastInsertRowIdAsync_ReturnsRowIdOfLastInsert()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            await t.ExecuteUpdateDirectAsync($"INSERT INTO \"{table}\" (name) VALUES ('a')", null);
            long id = await t.GetLastInsertRowIdAsync();
            id.Should().BeGreaterThan(0);
            await t.RollbackTransactionAsync();
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    // ---------- Members after dispose ----------

    [Fact]
    public async Task Operations_AfterDisposeOfPrivateConnection_ThrowArgumentNull()
    {
        SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.DisposeAsync();

        List<Func<Task>> calls = new List<Func<Task>>
        {
            () => t.ExecuteQueryDirectAsync("SELECT 1", null),
            () => t.ExecuteReadReturningAsync("SELECT 1", null),
            () => t.ExecuteWriteReturningAsync("SELECT 1", null),
            () => t.ExecuteUpdateDirectAsync("SELECT 1", null),
            () => t.GetLastInsertRowIdAsync(),
            () => t.GetSynchIdAsync("x", 1),
            () => t.CommitTransactionAsync(),
            () => t.RollbackTransactionAsync(),
            () => t.AttachDatabaseAsync("x"),
            () => t.DetachDatabaseAsync("x"),
            () => t.DetachDatabaseAsync()
        };

        foreach (Func<Task> call in calls)
            await call.Should().ThrowAsync<ArgumentNullException>();

        Action rows = () => t.GetAllRows<Dictionary<string, object?>>();
        rows.Should().Throw<ArgumentNullException>();
    }

    // ---------- Attach / detach ----------

    [Fact]
    public async Task AttachAndDetach_SameDatabase_AreSilentNoOps()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        string current = t.Connection!.DatabaseName!;

        await t.AttachDatabaseAsync(current);
        await t.DetachDatabaseAsync(current);
    }

    [Fact]
    public async Task AttachAndDetach_MissingDatabaseFile_ThrowNoDatabaseExists()
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        string missing = $"missing_{Guid.NewGuid():N}.db";

        (await ((Func<Task>)(() => t.AttachDatabaseAsync(missing))).Should().ThrowAsync<SxmException>())
            .Which.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.NoDatabaseExists);
        (await ((Func<Task>)(() => t.DetachDatabaseAsync(missing))).Should().ThrowAsync<SxmException>())
            .Which.ErrorCode.Should().Be(SxmDefines.SxmErrorCode.NoDatabaseExists);
    }

    [Fact]
    public async Task AttachExistingFile_ThenDetachAll_RemovesAttachment()
    {
        string folder = SxmDatabaseDescriptor.DatabaseFolder!;
        string otherName = $"attach_{Guid.NewGuid():N}";
        string otherPath = Path.Combine(folder, otherName);

        using (Microsoft.Data.Sqlite.SqliteConnection c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={otherPath};Pooling=False"))
        {
            c.Open();
            using Microsoft.Data.Sqlite.SqliteCommand cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE t (v INTEGER)";
            cmd.ExecuteNonQuery();
        }

        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            await t.AttachDatabaseAsync(otherName);

            List<Dictionary<string, object?>> before = await t.ExecuteReadReturningAsync("PRAGMA database_list", null);
            before.Should().Contain(r => (string?)r["name"] == otherName);
            await t.CommitTransactionAsync();

            await t.DetachDatabaseAsync();
            await t.CommitTransactionAsync();

            List<Dictionary<string, object?>> after = await t.ExecuteReadReturningAsync("PRAGMA database_list", null);
            after.Should().NotContain(r => (string?)r["name"] == otherName);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(otherPath); } catch { }
        }
    }
}
