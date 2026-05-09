using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SQLiteXM.Tests;

/// <summary>
/// Base class for all SQLiteXM tests providing common test infrastructure.
/// </summary>
public abstract class TestBase : IDisposable
{
    // Shared database name for ALL tests (SQLiteXM initializes only once per process)
    protected static readonly string TestDatabaseName = "test_database";
    protected static readonly string TestDatabaseFolder;
    protected static readonly string TestSqlStatementsPath;

    private bool _disposed = false;
    private static int _initCounter = 0;

    static TestBase()
    {
        // Initialize shared paths once for all tests
        TestDatabaseFolder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Tests", TestDatabaseName);
        Directory.CreateDirectory(TestDatabaseFolder);

        TestSqlStatementsPath = Path.Combine(TestDatabaseFolder, "statements.json");

        // Create minimal SQL statements file
        CreateTestSqlStatementsFile();

        // CRITICAL: Initialize SQLiteXM in static constructor to guarantee DatabaseFolder
        // is set before ANY test runs. This prevents race conditions where parallel tests
        // try to set different database folders (only the first setter wins).
        InitializeSqliteXMSync();
    }

    /// <summary>
    /// Synchronous initialization for static constructor.
    /// Ensures all tests share the same database location.
    /// </summary>
    private static void InitializeSqliteXMSync()
    {
        // FIXED: SxmInit.InitDbAsync now applies DatabaseFolderOverride BEFORE creating
        // any SxmDatabaseDescriptor, so we no longer need the reflection hack.
        // The library correctly respects the override when passed via SxmInitOptions.

        // Do NOT reset here - we want initialization to persist for all tests
        // (Reset is only needed when developing/debugging specific test scenarios)

        var initOptions = new SxmInitOptions
        {
            DatabaseFolderOverride = TestDatabaseFolder
        };

        // Run async initialization synchronously - safe in static constructor
        SxmInit.InitDbAsync(TestSqlStatementsPath, initOptions).GetAwaiter().GetResult();
    }

    protected TestBase()
    {
        // Instance constructor - can be used for per-test setup if needed
    }

    /// <summary>
    /// Creates a minimal SQL statements configuration file for testing.
    /// This is required by SQLiteXM for initialization.
    /// The JSON structure must match the RootJson class in SxmSerialization.cs
    /// </summary>
    private static void CreateTestSqlStatementsFile()
    {
        // Match the RootJson structure: database, isDefault, version at root level
        var config = new
        {
            database = TestDatabaseName,
            isDefault = true,
            version = 1L
            // No need for Table, Insert, etc. arrays - SQLiteXM entities create their own schema
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TestSqlStatementsPath, json);
    }

    /// <summary>
    /// Initialize SQLiteXM with the test configuration.
    /// NOTE: Initialization normally happens in static constructor.
    /// This method can be called after CleanupTestDataAsync() to re-initialize.
    /// </summary>
    protected async Task InitializeSqliteXMAsync()
    {
        // Re-initialize if needed (e.g., after cleanup)
        // InitDbAsync is safe to call multiple times - it returns early if already initialized
        var initOptions = new SxmInitOptions
        {
            DatabaseFolderOverride = TestDatabaseFolder
        };

        await SxmInit.InitDbAsync(TestSqlStatementsPath, initOptions);
    }

    /// <summary>
    /// Cleans up test data by deleting the database file and resetting all SQLiteXM state.
    /// Call this in tests that need isolated data.
    /// **WARNING:** This resets ALL static state - use sparingly and only in DEBUG builds.
    /// </summary>
    protected async Task CleanupTestDataAsync()
    {
#if DEBUG
        // Shutdown connection manager for this database to close all connections
        await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName);

        // Reset all SQLiteXM static state
        await SxmInit.ResetForTestingAsync();

        // Force GC to release any file handles
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Delete the database file and related files
        try
        {
            // Database file does NOT have .db extension (it's just the database name)
            var dbPath = Path.Combine(TestDatabaseFolder, TestDatabaseName);
            var walPath = $"{dbPath}-wal";
            var shmPath = $"{dbPath}-shm";

            // Try multiple times with delays (file might be locked)
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (File.Exists(dbPath)) File.Delete(dbPath);
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);
                    break; // Success
                }
                catch (IOException) when (i < 2)
                {
                    await Task.Delay(100); // Wait and retry
                }
            }
        }
        catch
        {
            // Ignore file deletion errors - tests will still work with stale data
        }

        // Recreate SQL statements file (it may have been deleted with the database folder)
        CreateTestSqlStatementsFile();

        // Re-initialize for next test
        Interlocked.Exchange(ref _initCounter, 0);
        await InitializeSqliteXMAsync();
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("CleanupTestDataAsync is only available in DEBUG builds.");
#endif
    }

    /// <summary>
    /// Simple cleanup that just deletes the database file without resetting static state.
    /// Use this when you want to clear data but don't need full re-initialization.
    /// </summary>
    protected void CleanupTestData()
    {
        try
        {
            var dbPath = Path.Combine(TestDatabaseFolder, $"{TestDatabaseName}.db");
            if (File.Exists(dbPath))
            {
                // Close any open connections first by forcing GC
                GC.Collect();
                GC.WaitForPendingFinalizers();

                File.Delete(dbPath);
            }
        }
        catch
        {
            // Ignore file deletion errors
        }
    }

    /// <summary>
    /// Verifies that an entity with the given ID exists in the database.
    /// Returns the entity retrieved from the database.
    /// </summary>
    protected async Task<T?> VerifyEntityExistsInDbAsync<T>(long id) where T : SxmEntity
    {
        using var context = new SxmLinqContext(TestDatabaseName);
        var entity = context.GetTable<T>().FirstOrDefault(e => e.id == id);
        return entity;
    }

    /// <summary>
    /// Verifies that an entity with the given ID does NOT exist in the database.
    /// </summary>
    protected async Task VerifyEntityNotInDbAsync<T>(long id) where T : SxmEntity
    {
        using var context = new SxmLinqContext(TestDatabaseName);
        var entity = context.GetTable<T>().FirstOrDefault(e => e.id == id);
        if (entity != null)
        {
            throw new InvalidOperationException($"Entity with id {id} was found in database but should not exist.");
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets all entities of a given type from the database.
    /// </summary>
    protected List<T> GetAllEntitiesFromDb<T>() where T : SxmEntity
    {
        using var context = new SxmLinqContext(TestDatabaseName);
        return context.GetTable<T>().ToList();
    }

    /// <summary>
    /// Gets the count of entities of a given type from the database.
    /// </summary>
    protected int GetEntityCountFromDb<T>() where T : SxmEntity
    {
        using var context = new SxmLinqContext(TestDatabaseName);
        return context.GetTable<T>().Count();
    }

    /// <summary>
    /// Verifies that a table exists in the database by attempting to query it.
    /// Returns true if the table exists, false otherwise.
    /// </summary>
    protected bool VerifyTableExists<T>() where T : SxmEntity
    {
        try
        {
            using var context = new SxmLinqContext(TestDatabaseName);
            _ = context.GetTable<T>().Count();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cleans up test databases and files.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                // Give a small delay for any pending file operations
                Thread.Sleep(100);

                // Delete entire test directory
                if (Directory.Exists(TestDatabaseFolder))
                {
                    Directory.Delete(TestDatabaseFolder, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        _disposed = true;
    }
}
