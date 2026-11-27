using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Direct tests for <see cref="SxmConnection"/>: construction failures, lock ownership and
/// re-entrancy, reader helpers, and operations against a destroyed connection.
/// </summary>
[Collection("Sequential")]
public class SxmConnectionTests : TestBase
{
    private const string TwoRowSql = "SELECT 1 AS id, 'a' AS name UNION ALL SELECT 2, 'b'";

    // ---------- Construction ----------

    [Fact]
    public void Constructor_UnknownDatabase_ThrowsSxmException()
    {
        Action act = () => new SxmConnection("no_such_database_xyz.db3");

        act.Should().Throw<SxmException>();
    }

    [Fact]
    public void CloseConnection_Null_DoesNothing()
    {
        Action act = () => SxmConnection.CloseConnection(null, TestDatabaseName);

        act.Should().NotThrow();
    }

    [Fact]
    public void GetConnectionString_ReturnsCachedValue()
    {
        string? name1 = TestDatabaseName;
        string? name2 = TestDatabaseName;

        string? first = SxmConnection.GetConnectionString(ref name1);
        string? second = SxmConnection.GetConnectionString(ref name2);

        first.Should().NotBeNullOrEmpty();
        second.Should().Be(first);
    }

    // ---------- Locking ----------

    [Fact]
    public async Task Lock_SameOwner_IsReentrant_AndReleasedOnlyAfterMatchingReleases()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            Guid owner = Guid.NewGuid();
            (await conn.LockAsync(100, default, owner)).Should().BeTrue();
            (await conn.LockAsync(100, default, owner)).Should().BeTrue("the same owner may re-enter");

            conn.ReleaseLock(owner);
            (await conn.LockAsync(50, default, Guid.NewGuid())).Should().BeFalse("one re-entrant hold remains");

            conn.ReleaseLock(owner);
            Guid other = Guid.NewGuid();
            (await conn.LockAsync(50, default, other)).Should().BeTrue("the lock is free after the final release");
            conn.ReleaseLock(other);
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task ReleaseLock_ByNonOwner_Throws_AndKeepsLock()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            Guid owner = Guid.NewGuid();
            (await conn.LockAsync(100, default, owner)).Should().BeTrue();

            Action act = () => conn.ReleaseLock(Guid.NewGuid());
            act.Should().Throw<Exception>();

            (await conn.LockAsync(50, default, Guid.NewGuid())).Should().BeFalse("the real owner still holds the lock");
            conn.ReleaseLock(owner);
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task ReleaseLock_WhenNotHeld_DoesNothing()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            Action act = () => conn.ReleaseLock(Guid.NewGuid());
            act.Should().NotThrow();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task ReleaseLock_WithoutOwnerId_ReleasesCurrentHolder()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            (await conn.LockAsync(100)).Should().BeTrue();
            conn.ReleaseLock();
            (await conn.LockAsync(50)).Should().BeTrue();
            conn.ReleaseLock();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Lock_AfterDestroy_ReturnsFalse()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        await conn.DestroyConnectionAsync();

        (await conn.LockAsync(50)).Should().BeFalse();
    }

    [Fact]
    public async Task AcquireLease_WhenHeld_ThrowsSxmException()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            await using ISxmConnectionLease lease = await conn.AcquireLeaseAsync(100);

            Func<Task> act = () => conn.AcquireLeaseAsync(50);
            await act.Should().ThrowAsync<SxmException>();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task AcquireLease_SameOwner_IsReentrant_AndDoubleDisposeIsSafe()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            ISxmConnectionLease outer = await conn.AcquireLeaseAsync(100);
            ISxmConnectionLease inner = await conn.AcquireLeaseAsync(100, default, outer.OwnerId);
            inner.OwnerId.Should().Be(outer.OwnerId);

            await inner.DisposeAsync();
            await inner.DisposeAsync();
            (await conn.LockAsync(50, default, Guid.NewGuid())).Should().BeFalse("the outer lease still holds the lock");

            await outer.DisposeAsync();
            Guid other = Guid.NewGuid();
            (await conn.LockAsync(50, default, other)).Should().BeTrue();
            conn.ReleaseLock(other);
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    // ---------- Reader helpers ----------

    [Fact]
    public async Task Reader_Helpers_ReadAllRowsAndMetadata()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            await conn.ExecuteQueryAsync(TwoRowSql, null);

            conn.HasRows().Should().BeTrue();
            conn.GetColumnCount().Should().Be(2);
            conn.GetFieldNames().Should().Equal("id", "name");
            conn.GetValue("id").Should().BeNull("no row has been read yet");

            conn.NextRow().Should().BeTrue();
            conn.GetValue("id").Should().Be(1L);
            conn.GetValue(1).Should().Be("a");
            conn.GetFieldName(1).Should().Be("name");
            conn.GetType("id").Should().Be(typeof(long));

            Dictionary<string, object?>? row = conn.GetNextRow<Dictionary<string, object?>>();
            row.Should().NotBeNull();
            row!["id"].Should().Be(2L);
            row["name"].Should().Be("b");

            conn.GetNextRow<Dictionary<string, object?>>().Should().BeNull("there are no more rows");
            conn.HasRows().Should().BeFalse("the reader is released after the last row");
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Reader_Helpers_UnknownFieldOrOrdinal_Throw()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            await conn.ExecuteQueryAsync(TwoRowSql, null);
            conn.NextRow().Should().BeTrue();

            // Argument misuse is passed through unchanged by design (ExceptionHelper.IsNonWrappable).
            ((Action)(() => conn.GetValue("missing"))).Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => conn.GetType("missing"))).Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => conn.GetValue(99))).Should().Throw<Exception>();
            ((Action)(() => conn.GetFieldName(99))).Should().Throw<Exception>();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Reader_Helpers_EmptyResult_ReturnDefaults()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            await conn.ExecuteQueryAsync("SELECT 1 AS id WHERE 0", null);

            conn.HasRows().Should().BeFalse();
            conn.GetColumnCount().Should().Be(0);
            conn.GetFieldNames().Should().BeEmpty();
            conn.NextRow().Should().BeFalse();
            conn.GetValue("id").Should().BeNull();
            conn.GetValue(0).Should().BeNull();
            conn.GetFieldName(0).Should().BeNull();
            conn.GetType("id").Should().BeNull();
            conn.GetNextRow<Dictionary<string, object?>>().Should().BeNull();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Reader_Helpers_BeforeAnyQuery_ReturnDefaults()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            conn.HasRows().Should().BeFalse();
            conn.GetColumnCount().Should().Be(0);
            conn.GetFieldNames().Should().BeEmpty();
            conn.NextRow().Should().BeFalse();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Reader_NullColumnValue_MapsToNull()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            await conn.ExecuteQueryAsync("SELECT NULL AS v", null);

            Dictionary<string, object?>? row = conn.GetNextRow<Dictionary<string, object?>>();
            row.Should().NotBeNull();
            row!["v"].Should().BeNull();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    // ---------- Command execution ----------

    [Fact]
    public async Task Execute_EmptyCommand_ThrowsSxmException()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            Func<Task> query = () => conn.ExecuteQueryAsync(string.Empty, null);
            Func<Task> nonQuery = () => conn.ExecuteNonQueryAsync(string.Empty, null);

            await query.Should().ThrowAsync<SxmException>();
            await nonQuery.Should().ThrowAsync<SxmException>();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Execute_SecondQueryOnSameCommand_ReplacesReader()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        try
        {
            await conn.ExecuteQueryAsync(TwoRowSql, null);
            conn.NextRow().Should().BeTrue();

            await conn.ExecuteQueryAsync("SELECT 42 AS answer", null);
            conn.NextRow().Should().BeTrue();
            conn.GetValue("answer").Should().Be(42L);
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task Operations_AfterDestroy_Throw()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);
        await conn.DestroyConnectionAsync();

        Func<Task> query = () => conn.ExecuteQueryAsync("SELECT 1", null);
        Func<Task> nonQuery = () => conn.ExecuteNonQueryAsync("SELECT 1", null);
        Action begin = () => conn.BeginTransaction();

        await query.Should().ThrowAsync<Exception>();
        await nonQuery.Should().ThrowAsync<Exception>();
        begin.Should().Throw<Exception>();
    }

    [Fact]
    public async Task BeginTransaction_Twice_IsIdempotent_AndRollbackOnRelease()
    {
        string table = $"conn_{Guid.NewGuid():N}";
        SxmConnection setup = new SxmConnection(TestDatabaseName);
        try
        {
            await setup.ExecuteNonQueryAsync($"CREATE TABLE \"{table}\" (v INTEGER)", null);
        }
        finally
        {
            await setup.DestroyConnectionAsync();
        }

        try
        {
            SxmConnection conn = new SxmConnection(TestDatabaseName);
            conn.BeginTransaction();
            conn.BeginTransaction();
            await conn.ExecuteNonQueryAsync($"INSERT INTO \"{table}\" (v) VALUES (1)", null);
            await conn.ReleaseConnectionAsync();

            SxmConnection check = new SxmConnection(TestDatabaseName);
            try
            {
                await check.ExecuteQueryAsync($"SELECT COUNT(*) AS c FROM \"{table}\"", null);
                check.NextRow().Should().BeTrue();
                check.GetValue("c").Should().Be(0L, "releasing a connection with an open transaction rolls it back");
            }
            finally
            {
                await check.DestroyConnectionAsync();
            }
        }
        finally
        {
            SxmConnection drop = new SxmConnection(TestDatabaseName);
            await drop.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS \"{table}\"", null);
            await drop.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task ReleaseConnection_Shared_KeepsConnectionOpen_DestroyCloses()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName, shared: true);
        try
        {
            await conn.ReleaseConnectionAsync();
            await conn.ExecuteQueryAsync("SELECT 1 AS v", null);
            conn.NextRow().Should().BeTrue("a released shared connection stays usable");

            await conn.ReleaseConnectionAsync(destroy: true);
            Func<Task> after = () => conn.ExecuteQueryAsync("SELECT 1", null);
            await after.Should().ThrowAsync<Exception>();
        }
        finally
        {
            await conn.DestroyConnectionAsync();
        }
    }

    [Fact]
    public async Task ReleaseConnection_Twice_IsSafe()
    {
        SxmConnection conn = new SxmConnection(TestDatabaseName);

        await conn.ReleaseConnectionAsync();
        Func<Task> again = () => conn.ReleaseConnectionAsync();

        await again.Should().NotThrowAsync();
    }
}
