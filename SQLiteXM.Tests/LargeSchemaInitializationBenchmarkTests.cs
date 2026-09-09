using System.Diagnostics;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace SQLiteXM.Tests;

/// <summary>
/// Performance benchmark exercising the "database ready" initialization pattern documented in
/// Docs/advanced-initialization-patterns.md (<see cref="SxmDatabase.StartInitialization"/> combined with
/// <see cref="SxmDatabase.EnsureReadyAsync"/>) against a large schema: 75 entities, each with 50 columns
/// and a mix of indexes (single-column, composite, unique) and required-field defaults.
/// </summary>
/// <remarks>
/// This is intentionally not a correctness test - it exists to measure wall-clock time for
/// SQLiteXM to build out a schema that is large in table/column count (not row count).
/// The elapsed time is written to the test output; there is no pass/fail threshold, since the
/// intent is to observe the number, not to gate the build on it.
/// </remarks>
[Collection("InitializationPattern")]
public class LargeSchemaInitializationBenchmarkTests : IDisposable
{
    private static readonly string BenchmarkFolder;
    private readonly string _testId;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    static LargeSchemaInitializationBenchmarkTests()
    {
        BenchmarkFolder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "LargeSchemaBenchmark");
        Directory.CreateDirectory(BenchmarkFolder);
    }

    public LargeSchemaInitializationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _testId = Guid.NewGuid().ToString("N");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            var dbFiles = Directory.GetFiles(BenchmarkFolder, $"*{_testId}*");
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
            DatabaseFolderOverride = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", "test_database")
        };
        Directory.CreateDirectory(initOptions.DatabaseFolderOverride);
        var testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
        if (!File.Exists(testStatementsPath))
        {
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

    private string CreateStatementsFile(string databaseName)
    {
        var config = new
        {
            version = 1L,
            databases = new[]
            {
                new { database = databaseName, isDefault = true }
            }
        };

        var path = Path.Combine(BenchmarkFolder, $"statements_{_testId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    private SxmDatabaseOptions CreateOptions() => new()
    {
        DatabaseFolderOverride = BenchmarkFolder
    };

    private static readonly Type[] AllBenchEntityTypes =
    {
        typeof(BenchEntity01), typeof(BenchEntity02), typeof(BenchEntity03), typeof(BenchEntity04), typeof(BenchEntity05),
        typeof(BenchEntity06), typeof(BenchEntity07), typeof(BenchEntity08), typeof(BenchEntity09), typeof(BenchEntity10),
        typeof(BenchEntity11), typeof(BenchEntity12), typeof(BenchEntity13), typeof(BenchEntity14), typeof(BenchEntity15),
        typeof(BenchEntity16), typeof(BenchEntity17), typeof(BenchEntity18), typeof(BenchEntity19), typeof(BenchEntity20),
        typeof(BenchEntity21), typeof(BenchEntity22), typeof(BenchEntity23), typeof(BenchEntity24), typeof(BenchEntity25),
        typeof(BenchEntity26), typeof(BenchEntity27), typeof(BenchEntity28), typeof(BenchEntity29), typeof(BenchEntity30),
        typeof(BenchEntity31), typeof(BenchEntity32), typeof(BenchEntity33), typeof(BenchEntity34), typeof(BenchEntity35),
        typeof(BenchEntity36), typeof(BenchEntity37), typeof(BenchEntity38), typeof(BenchEntity39), typeof(BenchEntity40),
        typeof(BenchEntity41), typeof(BenchEntity42), typeof(BenchEntity43), typeof(BenchEntity44), typeof(BenchEntity45),
        typeof(BenchEntity46), typeof(BenchEntity47), typeof(BenchEntity48), typeof(BenchEntity49), typeof(BenchEntity50),
        typeof(BenchEntity51), typeof(BenchEntity52), typeof(BenchEntity53), typeof(BenchEntity54), typeof(BenchEntity55),
        typeof(BenchEntity56), typeof(BenchEntity57), typeof(BenchEntity58), typeof(BenchEntity59), typeof(BenchEntity60),
        typeof(BenchEntity61), typeof(BenchEntity62), typeof(BenchEntity63), typeof(BenchEntity64), typeof(BenchEntity65),
        typeof(BenchEntity66), typeof(BenchEntity67), typeof(BenchEntity68), typeof(BenchEntity69), typeof(BenchEntity70),
        typeof(BenchEntity71), typeof(BenchEntity72), typeof(BenchEntity73), typeof(BenchEntity74), typeof(BenchEntity75),
    };

    [Fact]
    public async Task StartInitialization_WithLargeSchema_75TablesBy50Columns_ReportsElapsedTime()
    {
        Assert.Equal(75, AllBenchEntityTypes.Length);

        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"benchdb_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = CreateOptions();

        Stream stream = File.OpenRead(statementsPath);

        // Measure the full "database ready" pipeline: StartInitialization kicks off schema creation for
        // all 75 tables in the background, and EnsureReadyAsync is the documented way to await completion
        // before first use. Together they represent the wall-clock cost of standing up this large schema.
        var stopwatch = Stopwatch.StartNew();
        SxmDatabase.StartInitialization(stream, options, AllBenchEntityTypes);
        await SxmDatabase.EnsureReadyAsync();
        stopwatch.Stop();

        // Clear only the StartInitialization/EnsureReadyAsync gating state (the "_initializeTask" flag
        // and the "DbReady" TaskCompletionSource) immediately after the first initialization completes,
        // so the second call to StartInitialization is treated as a fresh entry point rather than a
        // no-op. This does NOT delete or recreate the database that was just created above - it
        // simulates a second "migration" pass against the same on-disk database.
        await SxmDatabase.ResetForTestingAsync();

        Stream stream2 = File.OpenRead(statementsPath);

        var stopwatch2 = Stopwatch.StartNew();
        SxmDatabase.StartInitialization(stream2, options, AllBenchEntityTypes);
        await SxmDatabase.EnsureReadyAsync();
        stopwatch2.Stop();

        string message =
            $"First StartInitialization + EnsureReadyAsync for 75 tables x 50 columns completed in " +
            $"{stopwatch.Elapsed.TotalMilliseconds:F1}ms ({stopwatch.ElapsedTicks} ticks). " +
            $"Second (migration) StartInitialization + EnsureReadyAsync completed in " +
            $"{stopwatch2.Elapsed.TotalMilliseconds:F1}ms ({stopwatch2.ElapsedTicks} ticks).";
        _output.WriteLine(message);
        Console.WriteLine(message);

        // Sanity check: the database is actually usable after the benchmarked initialization completes.
        var entity = new BenchEntity01 { Field01 = "benchmark", Field02 = 1 };
        await entity.SaveAsync();

        await using var context = new SxmTransaction(databaseName);
        BenchEntity01? loaded = context.GetTable<BenchEntity01>().FirstOrDefault(e => e.id == entity.id);
        Assert.NotNull(loaded);
    }
}
