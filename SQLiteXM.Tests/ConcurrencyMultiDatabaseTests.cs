using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Verifies that separate transactions to different SQLite databases can run concurrently,
/// as documented in Docs/concurrency.md.
/// </summary>
[Collection(ConcurrencyLastCollectionOrderer.ConcurrencyCollectionName)]
public class ConcurrencyMultiDatabaseTests : IDisposable
{
    private static readonly string TestFolder = Path.Combine(TestBase.TestRootFolder, "ConcurrencyMultiDatabase");

    private readonly string _testId = Guid.NewGuid().ToString("N");
    private readonly string _testStatementsPath;
    private readonly string _testDbFolder;
    private readonly IDisposable _concurrencyScope;
    private bool _disposed = false;

    public ConcurrencyMultiDatabaseTests()
    {
        Directory.CreateDirectory(TestFolder);
        _testStatementsPath = Path.Combine(TestFolder, $"statements_{_testId}.json");
        _testDbFolder = Path.Combine(TestFolder, _testId);
        Directory.CreateDirectory(_testDbFolder);
        _concurrencyScope = TestBase.EnterConcurrencyScope();
    }

    public void Dispose()
    {
        if (_disposed) return;

        _concurrencyScope.Dispose();

        try
        {
            if (File.Exists(_testStatementsPath))
                File.Delete(_testStatementsPath);

            if (Directory.Exists(_testDbFolder))
                Directory.Delete(_testDbFolder, true);
        }
        catch { }

        // Restore the standard harness configuration so subsequent tests work correctly
        try
        {
            SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();
            SxmDatabaseOptions initOptions = TestBase.CreateTestOptions(Path.Combine(TestBase.TestRootFolder, "test_database"));
            string testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
            using FileStream stream = File.OpenRead(testStatementsPath);
            SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();

            SxmDatabase.RegisterEntitiesAsync(
                typeof(SimpleEntity),
                typeof(AllTypesEntity),
                typeof(TimeTypeTextEntity),
                typeof(ExplicitColumnEntity),
                typeof(IndexedEntity),
                typeof(ParentEntity),
                typeof(ChildEntity),
                typeof(TriggerEntity),
                typeof(RequiredFieldEntity)
            ).GetAwaiter().GetResult();
        }
        catch { }

        _disposed = true;
    }

    private void CreateStatementsFile()
    {
        object config = new Dictionary<string, object>
        {
            ["version"] = 1L,
            ["databases"] = new[]
            {
                new { database = "databaseA", isDefault = true },
                new { database = "databaseB", isDefault = false }
            }
        };
        File.WriteAllText(_testStatementsPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task SeparateTransactions_ConcurrentToDifferentDatabases_ReturnCorrectRows()
    {
        // Arrange
        CreateStatementsFile();
        SxmDatabaseOptions options = TestBase.CreateTestOptions(_testDbFolder);
        await SxmDatabase.ResetForTestingAsync();
        using (FileStream stream = File.OpenRead(_testStatementsPath))
        {
            await SxmDatabase.InitializeAsync(stream, options);
        }
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(MultiDatabaseTests.DatabaseAEntity),
            typeof(MultiDatabaseTests.DatabaseBEntity));

        for (int i = 0; i < 5; i++)
        {
            MultiDatabaseTests.DatabaseAEntity entityA = new MultiDatabaseTests.DatabaseAEntity { Name = $"A{i}", Count = i };
            await entityA.SaveAsync();
        }
        for (int i = 0; i < 7; i++)
        {
            MultiDatabaseTests.DatabaseBEntity entityB = new MultiDatabaseTests.DatabaseBEntity { Title = $"B{i}", CreatedDate = DateTime.Now };
            await entityB.SaveAsync();
        }

        // Act - Both transactions start together on different database files
        using Barrier ready = new Barrier(2);
        Task<List<MultiDatabaseTests.DatabaseAEntity>> taskA = Task.Run(() =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            using SxmTransaction ctxA = new SxmTransaction("databaseA");
            return ctxA.GetTable<MultiDatabaseTests.DatabaseAEntity>().ToList();
        });
        Task<List<MultiDatabaseTests.DatabaseBEntity>> taskB = Task.Run(() =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Readers did not reach the start barrier.");
            using SxmTransaction ctxB = new SxmTransaction("databaseB");
            return ctxB.GetTable<MultiDatabaseTests.DatabaseBEntity>().ToList();
        });

        List<MultiDatabaseTests.DatabaseAEntity> resultsA = await taskA;
        List<MultiDatabaseTests.DatabaseBEntity> resultsB = await taskB;

        // Assert
        resultsA.Should().HaveCount(5);
        resultsA.Select(e => e.Name).Should().BeEquivalentTo(Enumerable.Range(0, 5).Select(i => $"A{i}"));
        resultsB.Should().HaveCount(7);
        resultsB.Select(e => e.Title).Should().BeEquivalentTo(Enumerable.Range(0, 7).Select(i => $"B{i}"));
    }

    [Fact]
    public async Task SeparateTransactions_ConcurrentWrites_ToDifferentDatabases_PersistExactRows()
    {
        // Arrange
        CreateStatementsFile();
        SxmDatabaseOptions options = TestBase.CreateTestOptions(_testDbFolder);
        await SxmDatabase.ResetForTestingAsync();
        using (FileStream stream = File.OpenRead(_testStatementsPath))
        {
            await SxmDatabase.InitializeAsync(stream, options);
        }
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(MultiDatabaseTests.DatabaseAEntity),
            typeof(MultiDatabaseTests.DatabaseBEntity));

        // Act - Each writer holds its own transaction on its own database; both start together at the barrier
        using Barrier ready = new Barrier(2);
        using Barrier bothWritten = new Barrier(2);
        Task writerA = Task.Run(async () =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not all reach the start barrier.");
            await using SxmTransaction txA = new SxmTransaction("databaseA");
            for (int i = 0; i < 6; i++)
            {
                await new MultiDatabaseTests.DatabaseAEntity { Name = $"WA{i}", Count = i }.SaveAsync();
            }
            if (!bothWritten.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not both hold open transactions.");
            await txA.CommitTransactionAsync();
        });
        Task writerB = Task.Run(async () =>
        {
            if (!ready.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not all reach the start barrier.");
            await using SxmTransaction txB = new SxmTransaction("databaseB");
            for (int i = 0; i < 4; i++)
            {
                await new MultiDatabaseTests.DatabaseBEntity { Title = $"WB{i}", CreatedDate = DateTime.Now }.SaveAsync();
            }
            if (!bothWritten.SignalAndWait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Writers did not both hold open transactions.");
            await txB.CommitTransactionAsync();
        });

        await Task.WhenAll(writerA, writerB);

        // Assert
        await using (SxmTransaction verifyA = new SxmTransaction("databaseA"))
        {
            List<MultiDatabaseTests.DatabaseAEntity> rowsA = verifyA.GetTable<MultiDatabaseTests.DatabaseAEntity>().ToList();
            rowsA.Should().HaveCount(6);
            rowsA.Select(e => e.Name).Should().BeEquivalentTo(Enumerable.Range(0, 6).Select(i => $"WA{i}"));
        }
        await using (SxmTransaction verifyB = new SxmTransaction("databaseB"))
        {
            List<MultiDatabaseTests.DatabaseBEntity> rowsB = verifyB.GetTable<MultiDatabaseTests.DatabaseBEntity>().ToList();
            rowsB.Should().HaveCount(4);
            rowsB.Select(e => e.Title).Should().BeEquivalentTo(Enumerable.Range(0, 4).Select(i => $"WB{i}"));
        }
    }
}
