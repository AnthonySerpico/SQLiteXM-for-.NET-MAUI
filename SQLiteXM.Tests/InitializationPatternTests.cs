using System.Text.Json;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests exercising the "database ready" initialization pattern documented in
/// Docs/advanced-initialization-patterns.md: <see cref="SxmDatabase.StartInitialization"/> combined with
/// <see cref="SxmDatabase.EnsureReadyAsync"/>.
/// </summary>
/// <remarks>
/// These tests reset all SQLiteXM static state (including the process-wide DbReady signal) so that
/// StartInitialization's idempotency and failure-propagation behavior can be observed from a clean slate.
/// Because that state is shared by the whole test process, these tests run in their own non-parallel
/// collection and restore the standard TestBase configuration in Dispose() so the rest of the suite
/// continues to work correctly.
/// </remarks>
[Collection("InitializationPattern")]
public class InitializationPatternTests : IDisposable
{
    private static readonly string InitPatternTestFolder;
    private readonly string _testId;
    private bool _disposed;

    static InitializationPatternTests()
    {
        InitPatternTestFolder = Path.Combine(TestBase.TestRootFolder, "InitializationPattern");
        Directory.CreateDirectory(InitPatternTestFolder);
    }

    public InitializationPatternTests()
    {
        _testId = Guid.NewGuid().ToString("N");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var dbFiles = Directory.GetFiles(InitPatternTestFolder, $"*{_testId}*");
            foreach (var file in dbFiles)
            {
                try { File.Delete(file); } catch { /* best effort cleanup */ }
            }
        }
        catch { /* best effort cleanup */ }

        // CRITICAL: Reset SQLiteXM state and re-initialize with the standard TestBase configuration so
        // subsequent tests (which assume "test_database" is ready) continue to work correctly.
#if DEBUG
        SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();
#endif
        var initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = Path.Combine(TestBase.TestRootFolder, "test_database")
        };
        Directory.CreateDirectory(initOptions.DatabaseFolderOverride);
        var testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
        if (!File.Exists(testStatementsPath))
        {
            // If InitializationPatternTests happens to run before any TestBase-derived test, the shared
            // "test_database" statements file won't exist yet. Recreate it so state is restored correctly.
            var config = new
            {
                version = 1L,
                databases = new[] { new { database = "test_database", isDefault = true } }
            };
            File.WriteAllText(testStatementsPath, JsonSerializer.Serialize(config));
        }
        using var stream = File.OpenRead(testStatementsPath);
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
            typeof(RequiredFieldEntity)).GetAwaiter().GetResult();
    }

    private string CreateStatementsFile(string databaseName, string? suffix = null)
    {
        var config = new
        {
            version = 1L,
            databases = new[]
            {
                new { database = databaseName, isDefault = true }
            }
        };

        var path = Path.Combine(InitPatternTestFolder, $"statements_{_testId}_{suffix ?? Guid.NewGuid().ToString("N")}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    private string CreateMalformedStatementsFile()
    {
        var path = Path.Combine(InitPatternTestFolder, $"malformed_{_testId}.json");
        File.WriteAllText(path, "{ this is not valid json ]");
        return path;
    }

    private SxmDatabaseOptions CreateOptions() => new()
    {
        DatabaseFolderOverride = InitPatternTestFolder
    };

    [Fact]
    public async Task StartInitialization_ThenEnsureReadyAsync_CompletesAndEntityIsUsable()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(stream, options, typeof(SimpleEntity));

        // Documented usage: await EnsureReadyAsync() before the first use of the database.
        await SxmDatabase.EnsureReadyAsync();

        var entity = new SimpleEntity { Name = "Alice", Age = 30, IsActive = true };
        await entity.SaveAsync();

        await using var context = new SxmTransaction(databaseName);
        SimpleEntity? loaded = context.GetTable<SimpleEntity>().FirstOrDefault(e => e.id == entity.id);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded!.Name);
        Assert.Equal(30, loaded.Age);
        Assert.True(loaded.IsActive);
    }

    [Fact]
    public async Task EnsureReadyAsync_CalledConcurrentlyManyTimes_AllCompleteSuccessfully()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(stream, options, typeof(SimpleEntity));

        // EnsureReadyAsync is documented as safe to call multiple times and concurrently.
        var waiters = Enumerable.Range(0, 20).Select(_ => SxmDatabase.EnsureReadyAsync());
        await Task.WhenAll(waiters);

        // Fast path: once ready, awaiting again completes immediately without throwing.
        await SxmDatabase.EnsureReadyAsync();
    }

    [Fact]
    public async Task StartInitialization_CalledMultipleTimesConcurrently_OnlyOneCallsEntitiesAreRegistered()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        // Two independent "entry points" race to call StartInitialization with different entity lists.
        // Per the documented contract, "only the first call actually performs initialization" - but for
        // two concurrent calls, "first" means whichever call wins the internal lock race, which is not
        // guaranteed to be the one that appears first in source order. Only one call's entities should
        // end up registered; the other call's entities must be silently discarded.
        Stream firstStream = File.OpenRead(statementsPath);
        Stream secondStream = File.OpenRead(statementsPath);

        SxmDatabase.StartInitialization(firstStream, options, typeof(SimpleEntity));
        SxmDatabase.StartInitialization(secondStream, options, typeof(AllTypesEntity));

        await SxmDatabase.EnsureReadyAsync();

        bool simpleEntityWorks;
        try
        {
            var simple = new SimpleEntity { Name = "Bob", Age = 40, IsActive = false };
            await simple.SaveAsync();
            simpleEntityWorks = true;
        }
        catch (InvalidOperationException)
        {
            simpleEntityWorks = false;
        }

        bool allTypesEntityWorks;
        try
        {
            var allTypes = new AllTypesEntity();
            await allTypes.SaveAsync();
            allTypesEntityWorks = true;
        }
        catch (InvalidOperationException)
        {
            allTypesEntityWorks = false;
        }

        // Exactly one of the two competing calls' entity lists should have been registered - never both,
        // and never neither.
        Assert.True(simpleEntityWorks ^ allTypesEntityWorks,
            $"Expected exactly one entity type to be registered, but SimpleEntity={simpleEntityWorks} and AllTypesEntity={allTypesEntityWorks}.");
    }

    [Fact]
    public async Task StartInitialization_WithMalformedSqlStatementsFile_PropagatesFailureToEnsureReadyAsync()
    {
        await SxmDatabase.ResetForTestingAsync();

        string statementsPath = CreateMalformedStatementsFile();
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(stream, options, typeof(SimpleEntity));

        // Per the documented contract, a failed initialization must propagate its exception to every
        // caller awaiting EnsureReadyAsync(), rather than allowing callers to proceed as if ready.
        await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());

        // Calling EnsureReadyAsync again must continue to observe the same failure (task stays faulted).
        await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());
    }

    [Fact]
    public async Task StartInitialization_WithManyEntityTypesIncludingRelationsIndexesAndTriggers_AllRegisteredAndUsable()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(
            stream,
            options,
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity));

        await SxmDatabase.EnsureReadyAsync();

        await using var context = new SxmTransaction(databaseName);

        // Foreign key relationship
        var parent = new ParentEntity { ParentName = "Parent1" };
        await parent.SaveAsync();
        var child = new ChildEntity { ChildName = "Child1", ParentId = parent.id };
        await child.SaveAsync();
        ChildEntity? loadedChild = context.GetTable<ChildEntity>().FirstOrDefault(e => e.id == child.id);
        Assert.NotNull(loadedChild);
        Assert.Equal(parent.id, loadedChild!.ParentId);

        // Indexed entity (composite + unique + single-field indexes)
        var indexed = new IndexedEntity { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", CreatedDate = DateTime.UtcNow };
        await indexed.SaveAsync();
        IndexedEntity? loadedIndexed = context.GetTable<IndexedEntity>().FirstOrDefault(e => e.Email == "jane@example.com");
        Assert.NotNull(loadedIndexed);

        // Trigger entity
        var trigger = new TriggerEntity { Name = "T1" };
        await trigger.SaveAsync();

        // Required field entity (fields must be supplied explicitly; RequiredNotNull only supplies a
        // DB-level default when a column is added via migration, not when omitted from an insert).
        var required = new RequiredFieldEntity { RequiredName = "Explicit Name", RequiredAge = 21 };
        await required.SaveAsync();
        RequiredFieldEntity? loadedRequired = context.GetTable<RequiredFieldEntity>().FirstOrDefault(e => e.id == required.id);
        Assert.NotNull(loadedRequired);
        Assert.Equal("Explicit Name", loadedRequired!.RequiredName);
        Assert.Equal(21, loadedRequired.RequiredAge);

        // All-types entity (broad column coverage)
        var allTypes = new AllTypesEntity { IntValue = 7, StringValue = "hello", GuidValue = Guid.NewGuid() };
        await allTypes.SaveAsync();
        AllTypesEntity? loadedAllTypes = context.GetTable<AllTypesEntity>().FirstOrDefault(e => e.id == allTypes.id);
        Assert.NotNull(loadedAllTypes);
        Assert.Equal("hello", loadedAllTypes!.StringValue);
    }

    [Fact]
    public async Task StartInitialization_HighConcurrency_ManySimultaneousCallsResultInExactlyOneWinner()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        const int callerCount = 50;
        var streams = new Stream[callerCount];
        for (int i = 0; i < callerCount; i++)
        {
            streams[i] = File.OpenRead(statementsPath);
        }

        // Simulate many independent entry points (UI launch, deep link, BroadcastReceiver, background
        // task, etc.) all racing to call StartInitialization at nearly the same instant, each with its
        // own distinct entity type so we can tell which one "won" the race.
        var entityTypesPerCaller = new Type[callerCount];
        entityTypesPerCaller[0] = typeof(SimpleEntity);
        for (int i = 1; i < callerCount; i++)
        {
            // Every other caller passes a type that would never be usable if it were the winner unless
            // it is genuinely the one whose call initialized the schema (AllTypesEntity as a stand-in
            // "different" entity list for all non-zero callers).
            entityTypesPerCaller[i] = typeof(AllTypesEntity);
        }

        var startBarrier = new ManualResetEventSlim(false);
        var launchTasks = new Task[callerCount];
        for (int i = 0; i < callerCount; i++)
        {
            int index = i;
            launchTasks[index] = Task.Run(() =>
            {
                startBarrier.Wait();
                SxmDatabase.StartInitialization(streams[index], options, entityTypesPerCaller[index]);
            });
        }

        startBarrier.Set();
        await Task.WhenAll(launchTasks);

        // Every caller must be able to observe successful completion.
        var readyWaiters = Enumerable.Range(0, callerCount).Select(_ => SxmDatabase.EnsureReadyAsync());
        await Task.WhenAll(readyWaiters);

        bool simpleEntityWorks;
        try
        {
            var simple = new SimpleEntity { Name = "Race", Age = 1, IsActive = true };
            await simple.SaveAsync();
            simpleEntityWorks = true;
        }
        catch (InvalidOperationException)
        {
            simpleEntityWorks = false;
        }

        bool allTypesEntityWorks;
        try
        {
            var allTypes = new AllTypesEntity();
            await allTypes.SaveAsync();
            allTypesEntityWorks = true;
        }
        catch (InvalidOperationException)
        {
            allTypesEntityWorks = false;
        }

        // Exactly one entity list should have won the race, regardless of how many callers competed.
        Assert.True(simpleEntityWorks ^ allTypesEntityWorks,
            $"Expected exactly one entity type to be registered out of {callerCount} concurrent callers, " +
            $"but SimpleEntity={simpleEntityWorks} and AllTypesEntity={allTypesEntityWorks}.");
    }

    [Fact]
    public async Task StartInitialization_WithAbstractEntityType_FailsDuringRegistrationAndPropagatesToEnsureReadyAsync()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        // FailFastTests.AbstractEntity cannot be registered (it's abstract). This fails during the
        // RegisterEntitiesAsync step, after InitializeAsync has already succeeded - a different failure
        // point than a malformed SQL statements file (which fails during InitializeAsync itself).
        Stream stream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(stream, options, typeof(FailFastTests.AbstractEntity));

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());
        Assert.Contains("abstract", ex.Message, StringComparison.OrdinalIgnoreCase);

        // The failure must remain observable on repeated calls.
        await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());
    }

    [Fact]
    public async Task StartInitialization_CalledAgainAfterAFailure_DoesNotRetryAndRemainsFaulted()
    {
        await SxmDatabase.ResetForTestingAsync();

        string malformedStatementsPath = CreateMalformedStatementsFile();
        SxmDatabaseOptions options = CreateOptions();

        Stream firstStream = File.OpenRead(malformedStatementsPath);
        SxmDatabase.StartInitialization(firstStream, options, typeof(SimpleEntity));
        await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());

        // A second, independent entry point calls StartInitialization again with a valid configuration
        // after the first failure. Per the documented "only once per process" guarantee, this must NOT
        // retry initialization - the process remains permanently faulted until the process restarts
        // (or, in tests, until ResetForTestingAsync is called).
        string databaseName = $"db_{_testId}";
        string validStatementsPath = CreateStatementsFile(databaseName, "retry");
        Stream secondStream = File.OpenRead(validStatementsPath);
        SxmDatabase.StartInitialization(secondStream, options, typeof(SimpleEntity));

        await Assert.ThrowsAnyAsync<Exception>(() => SxmDatabase.EnsureReadyAsync());
    }

    [Fact]
    public async Task StartInitialization_ReturnsImmediatelyWithoutBlockingCallingThread()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        SxmDatabase.StartInitialization(
            stream,
            options,
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity));
        stopwatch.Stop();

        // The call itself must return essentially instantly - all real work happens on a background task.
        // On a warm CLR this consistently measures well under 1ms (sub-millisecond); 25ms leaves headroom
        // for cold JIT / slow CI agents without hiding a real regression the way a 200ms bound would.
        double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        Assert.True(elapsedMs < 25,
            $"StartInitialization took {elapsedMs:F3}ms to return; it must not block the caller.");

        await SxmDatabase.EnsureReadyAsync();
    }

    [Fact]
    public async Task StartInitialization_DisposesTheStreamOfBothTheWinningAndTheLosingCall()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream winningStream = File.OpenRead(statementsPath);
        Stream losingStream = File.OpenRead(statementsPath);

        SxmDatabase.StartInitialization(winningStream, options, typeof(SimpleEntity));
        SxmDatabase.StartInitialization(losingStream, options, typeof(AllTypesEntity));

        await SxmDatabase.EnsureReadyAsync();

        // SQLiteXM takes ownership of the stream and disposes it once initialization completes,
        // regardless of whether that particular call ended up winning the initialization race.
        Assert.Throws<ObjectDisposedException>(() => winningStream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => losingStream.ReadByte());
    }

    [Fact]
    public async Task StartInitialization_CalledAgainAfterSuccess_IsNoOpAndStillDisposesTheUnusedStream()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"db_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream firstStream = File.OpenRead(statementsPath);
        SxmDatabase.StartInitialization(firstStream, options, typeof(SimpleEntity));
        await SxmDatabase.EnsureReadyAsync();

        // A later entry point calls StartInitialization again after initialization has already
        // completed successfully. This must be a cheap no-op that still disposes its stream.
        string secondStatementsPath = CreateStatementsFile(databaseName, "second");
        Stream secondStream = File.OpenRead(secondStatementsPath);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        SxmDatabase.StartInitialization(secondStream, options, typeof(AllTypesEntity));
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 200,
            $"StartInitialization took {stopwatch.ElapsedMilliseconds}ms to return on a no-op call.");

        // EnsureReadyAsync must still resolve immediately to the already-completed signal.
        await SxmDatabase.EnsureReadyAsync();

        // AllTypesEntity was never registered because the second call was a no-op.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            var allTypes = new AllTypesEntity();
            await allTypes.SaveAsync();
        });

        // Give the background Task.Run for the second call a brief chance to run its finally block.
        await Task.Delay(200);
        Assert.Throws<ObjectDisposedException>(() => secondStream.ReadByte());
    }
}
