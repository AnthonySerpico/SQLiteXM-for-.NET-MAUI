using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Verifies the safe concurrency patterns documented in Docs/concurrency.md.
/// Each test uses a unique name prefix so rows from other tests never affect the assertions.
/// </summary>
[Collection(ConcurrencyLastCollectionOrderer.ConcurrencyCollectionName)]
public class ConcurrencyTests : TestBase
{
    private readonly IDisposable _concurrencyScope;

    public ConcurrencyTests()
    {
        _concurrencyScope = EnterConcurrencyScope();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _concurrencyScope.Dispose();
        base.Dispose(disposing);
    }

    private static string NewPrefix() => $"Conc_{Guid.NewGuid():N}_";

    private static async Task SeedAsync(string prefix, int count)
    {
        for (int i = 0; i < count; i++)
        {
            SimpleEntity entity = new SimpleEntity { Name = $"{prefix}{i}", Age = i, IsActive = i % 2 == 0 };
            await entity.SaveAsync();
        }
    }

    [Fact]
    public async Task SeparateTransactions_ConcurrentLinqQueries_SameDatabase_ReturnCorrectRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        await SeedAsync(prefix, 20);

        // Act - Both readers start together while a writer inserts rows into the same table
        string otherPrefix = NewPrefix();
        using Barrier ready = new Barrier(3);
        Task<List<SimpleEntity>> olderTask = Task.Run(() =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            using SxmTransaction ctx1 = new SxmTransaction();
            return ctx1.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix) && e.Age >= 10).ToList();
        });
        Task<List<SimpleEntity>> allTask = Task.Run(() =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            using SxmTransaction ctx2 = new SxmTransaction();
            return ctx2.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        });
        Task writerTask = Task.Run(async () =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writer did not reach the start barrier.");
            for (int i = 0; i < 10; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{otherPrefix}{i}", Age = i };
                await entity.SaveAsync();
            }
        });

        await writerTask;
        List<SimpleEntity>[] results = await Task.WhenAll(olderTask, allTask);

        List<SimpleEntity> written;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            written = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(otherPrefix)).ToList();
        }

        // Assert - Reader results are exact despite the concurrent writes
        results[0].Should().HaveCount(10);
        results[0].Should().OnlyContain(e => e.Age >= 10);
        results[1].Should().HaveCount(20);
        results[1].Select(e => e.Name).Should().BeEquivalentTo(Enumerable.Range(0, 20).Select(i => $"{prefix}{i}"));
        written.Should().HaveCount(10);
    }

    [Fact]
    public async Task SeparateTransactions_ConcurrentLinqQueries_ManyTasks_AllReturnCorrectRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        await SeedAsync(prefix, 10);

        // Act - All readers hold their connections open at the same time before querying
        const int readerCount = 8;
        using Barrier ready = new Barrier(readerCount);
        List<Task<List<SimpleEntity>>> tasks = new List<Task<List<SimpleEntity>>>();
        for (int i = 0; i < readerCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Readers did not all reach the start barrier.");
                using SxmTransaction ctx = new SxmTransaction();
                return ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
            }));
        }

        List<SimpleEntity>[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().HaveCount(10));
    }

    [Fact]
    public async Task StandaloneEntitySaves_Concurrent_PersistAllRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        // Act - Each SaveAsync runs in standalone mode and opens its own connection, released together from a start barrier
        using Barrier start = new Barrier(20);
        List<Task> saves = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            int index = i;
            saves.Add(Task.Run(async () =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Saves did not reach the start barrier.");
                SimpleEntity entity = new SimpleEntity { Name = $"{prefix}{index}", Age = index };
                await entity.SaveAsync();
            }));
        }

        await Task.WhenAll(saves);

        // Assert
        List<SimpleEntity> persisted;
        await using (SxmTransaction ctx = new SxmTransaction())
        {
            persisted = ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }
        persisted.Should().HaveCount(20);
        persisted.Select(e => e.Name).Should().BeEquivalentTo(Enumerable.Range(0, 20).Select(i => $"{prefix}{i}"));
    }

    [Fact]
    public async Task StandaloneSqlStatements_Concurrent_ReturnCorrectRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        await SeedAsync(prefix, 10);

        // Act - Two standalone reads run alongside five standalone inserts into the same table, all released from one start barrier
        string otherPrefix = NewPrefix();
        using Barrier start = new Barrier(7);
        List<Task> inserts = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            inserts.Add(Task.Run(async () =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Operations did not reach the start barrier.");
                await SxmSql.RunStatementAsync<SimpleEntity>(
                    $"INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES ('{otherPrefix}{index}', {index}, 1)");
            }));
        }
        Task<List<SimpleEntity>> lowTask = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Operations did not reach the start barrier.");
            return await SxmSql.RunStatementAsync<SimpleEntity>(
                $"SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE '{prefix}%' AND Age < 5");
        });
        Task<List<SimpleEntity>> highTask = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Operations did not reach the start barrier.");
            return await SxmSql.RunStatementAsync<SimpleEntity>(
                $"SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name LIKE '{prefix}%' AND Age >= 5");
        });

        await Task.WhenAll(inserts);
        List<SimpleEntity>[] results = await Task.WhenAll(lowTask, highTask);

        List<SimpleEntity> written;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            written = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(otherPrefix)).ToList();
        }

        // Assert
        results[0].Should().HaveCount(5);
        results[0].Should().OnlyContain(e => e.Age < 5);
        results[1].Should().HaveCount(5);
        results[1].Should().OnlyContain(e => e.Age >= 5);
        written.Should().HaveCount(5);
    }

    [Fact]
    public async Task SeparateTransactions_MixedReadsAndWrites_SameDatabase_ReturnExactRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        await SeedAsync(prefix, 10);
        string writePrefix = NewPrefix();

        // Act - Two writers and two readers start together on the same database
        using Barrier start = new Barrier(4);
        Task<List<SimpleEntity>> readerTask = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            return ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        });
        Task<List<SimpleEntity>> readerTask2 = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            return ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        });
        Task writerTask = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            for (int i = 0; i < 5; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{writePrefix}A_{i}", Age = i };
                await entity.SaveAsync();
            }
            await ctx.CommitTransactionAsync();
        });
        Task writerTask2 = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            for (int i = 0; i < 5; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{writePrefix}B_{i}", Age = i };
                await entity.SaveAsync();
            }
            await ctx.CommitTransactionAsync();
        });

        await Task.WhenAll(writerTask, writerTask2);
        List<SimpleEntity>[] reads = await Task.WhenAll(readerTask, readerTask2);

        List<SimpleEntity> written;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            written = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(writePrefix)).ToList();
        }

        // Assert - Readers saw exactly the seeded rows, and all 10 written rows persisted
        reads[0].Should().HaveCount(10);
        reads[1].Should().HaveCount(10);
        written.Select(e => e.Name).Should().BeEquivalentTo(
            from t in new[] { "A", "B" }
            from i in Enumerable.Range(0, 5)
            select $"{writePrefix}{t}_{i}");
    }

    [Fact]
    public async Task SeparateTransactions_RollbackWhileOtherCommits_OnlyCommittedRowsPersist()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        // Act - One transaction rolls back while another commits, released together
        using Barrier start = new Barrier(2);
        Task committer = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            for (int i = 0; i < 3; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{prefix}keep_{i}", Age = i };
                await entity.SaveAsync();
            }
            await ctx.CommitTransactionAsync();
        });
        Task rollbacker = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            for (int i = 0; i < 3; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{prefix}drop_{i}", Age = i };
                await entity.SaveAsync();
            }
            await ctx.RollbackTransactionAsync();
        });

        await Task.WhenAll(committer, rollbacker);

        // Assert - Only the committed rows persist
        List<SimpleEntity> persisted;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            persisted = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }
        persisted.Select(e => e.Name).Should().BeEquivalentTo(
            Enumerable.Range(0, 3).Select(i => $"{prefix}keep_{i}"));
    }

    [Fact]
    public async Task StandaloneSaves_ConflictingUpdatesToSameRow_LastWriterWins_NoLostRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        SimpleEntity seed = new SimpleEntity { Name = $"{prefix}target", Age = 0 };
        await seed.SaveAsync();
        long id = seed.id;

        // Act - Ten standalone updates to the same row start together
        using Barrier start = new Barrier(10);
        List<Task> updates = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            updates.Add(Task.Run(async () =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Updates did not reach the start barrier.");
                await SxmSql.RunStatementAsync<SimpleEntity>(
                    $"UPDATE SimpleEntity SET Age = {index + 1} WHERE id = {id}");
            }));
        }

        await Task.WhenAll(updates);

        // Assert - The row still exists exactly once and holds one of the written values
        List<SimpleEntity> persisted;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            persisted = verify.GetTable<SimpleEntity>().Where(e => e.id == id).ToList();
        }
        persisted.Should().HaveCount(1);
        persisted[0].Age.Should().BeInRange(1, 10);
    }

    [Fact]
    public async Task SingleTransaction_SequentialOperations_ReturnCorrectRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        await SeedAsync(prefix, 10);

        // Act - Operations run one at a time on the same connection
        List<SimpleEntity> active;
        List<SimpleEntity> all;
        await using (SxmTransaction ctx = new SxmTransaction())
        {
            active = ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix) && e.IsActive).ToList();
            all = ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }

        // Assert
        active.Should().HaveCount(5);
        all.Should().HaveCount(10);
    }

    [Fact]
    public async Task SeparateTransactions_HoldIndependentConnections()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        // Act - Two transactions each save rows through their own connection, released together from a start barrier
        using Barrier start = new Barrier(2);
        Task<(Microsoft.Data.Sqlite.SqliteConnection? Sqlite, string? DataSource)> writerA = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            SxmConnection connection = ctx.SxmConnectionForTesting;
            for (int i = 0; i < 3; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{prefix}A_{i}", Age = 0 };
                await entity.SaveAsync();
            }
            await ctx.CommitTransactionAsync();
            return (connection.UnderlyingConnection, connection.UnderlyingConnection?.DataSource);
        });
        Task<(Microsoft.Data.Sqlite.SqliteConnection? Sqlite, string? DataSource)> writerB = Task.Run(async () =>
        {
            if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not reach the start barrier.");
            await using SxmTransaction ctx = new SxmTransaction();
            SxmConnection connection = ctx.SxmConnectionForTesting;
            for (int i = 0; i < 3; i++)
            {
                SimpleEntity entity = new SimpleEntity { Name = $"{prefix}B_{i}", Age = 1 };
                await entity.SaveAsync();
            }
            await ctx.CommitTransactionAsync();
            return (connection.UnderlyingConnection, connection.UnderlyingConnection?.DataSource);
        });

        (Microsoft.Data.Sqlite.SqliteConnection? Sqlite, string? DataSource) a = await writerA;
        (Microsoft.Data.Sqlite.SqliteConnection? Sqlite, string? DataSource) b = await writerB;

        // Assert - Distinct connections to the same file, and all rows from both transactions persisted
        a.Sqlite.Should().NotBeNull();
        b.Sqlite.Should().NotBeNull();
        a.Sqlite.Should().NotBeSameAs(b.Sqlite);
        a.DataSource.Should().NotBeNullOrEmpty();
        b.DataSource.Should().Be(a.DataSource);

        List<SimpleEntity> persisted;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            persisted = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }
        persisted.Select(e => e.Name).Should().BeEquivalentTo(
            $"{prefix}A_0", $"{prefix}A_1", $"{prefix}A_2",
            $"{prefix}B_0", $"{prefix}B_1", $"{prefix}B_2");
    }

    [Fact]
    public async Task SeparateTransactions_ConcurrentWrites_SameDatabase_PersistAllRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        // Act - Each task opens its own transaction, waits at the start barrier, then writes and commits
        using Barrier start = new Barrier(4);
        List<Task> writers = new List<Task>();
        for (int t = 0; t < 4; t++)
        {
            int taskIndex = t;
            writers.Add(Task.Run(async () =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Writers did not reach the start barrier.");
                await using SxmTransaction ctx = new SxmTransaction();
                for (int i = 0; i < 5; i++)
                {
                    SimpleEntity entity = new SimpleEntity { Name = $"{prefix}{taskIndex}_{i}", Age = taskIndex };
                    await entity.SaveAsync();
                }
                await ctx.CommitTransactionAsync();
            }));
        }

        await Task.WhenAll(writers);

        // Assert
        List<SimpleEntity> persisted;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            persisted = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }
        persisted.Should().HaveCount(20);
        persisted.Select(e => e.Name).Should().BeEquivalentTo(
            from t in Enumerable.Range(0, 4)
            from i in Enumerable.Range(0, 5)
            select $"{prefix}{t}_{i}");
    }

    [Fact]
    public async Task StandaloneSqlStatements_ConcurrentWrites_PersistAllRows()
    {
        // Arrange
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        // Act - Each insert is released from a shared start barrier so they run concurrently
        using Barrier start = new Barrier(10);
        List<Task> inserts = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            inserts.Add(Task.Run(async () =>
            {
                if (!start.SignalAndWait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Inserts did not reach the start barrier.");
                await SxmSql.RunStatementAsync<SimpleEntity>(
                    $"INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES ('{prefix}{index}', {index}, 1)");
            }));
        }

        await Task.WhenAll(inserts);

        // Assert
        List<SimpleEntity> persisted;
        await using (SxmTransaction verify = new SxmTransaction())
        {
            persisted = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).ToList();
        }
        persisted.Should().HaveCount(10);
        persisted.Select(e => e.Name).Should().BeEquivalentTo(Enumerable.Range(0, 10).Select(i => $"{prefix}{i}"));
    }
}
