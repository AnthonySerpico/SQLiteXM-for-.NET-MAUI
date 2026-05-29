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
    /// Ensures all tests share the same database location and registers all test entity schemas.
    /// </summary>
    private static void InitializeSqliteXMSync()
    {
        // FIXED: SxmDatabase.InitializeAsync now applies DatabaseFolderOverride BEFORE creating
        // any SxmDatabaseDescriptor, so we no longer need the reflection hack.
        // The library correctly respects the override when passed via SxmDatabaseOptions.

        // Do NOT reset here - we want initialization to persist for all tests
        // (Reset is only needed when developing/debugging specific test scenarios)

        var initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = TestDatabaseFolder
        };

        // Run async initialization synchronously - safe in static constructor
        SxmDatabase.InitializeAsync(TestSqlStatementsPath, initOptions).GetAwaiter().GetResult();

        // CRITICAL: Register all test entity schemas at startup.
        // With the deterministic schema registration refactor, entity constructors
        // no longer create/migrate tables. All schema must be registered explicitly.
        RegisterAllTestEntitySchemasSync();
    }

    /// <summary>
    /// Registers all test entity schemas used across the test suite.
    /// This must be called after InitializeAsync and before any entity usage.
    /// </summary>
    private static void RegisterAllTestEntitySchemasSync()
    {
        SxmDatabase.RegisterEntitiesAsync(
            // Standard test entities (from TestEntities.cs)
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity),
            // Migration test entities (from EntityMigrationTests.cs)
            typeof(EntityMigrationTests.MigrationTestV1),
            typeof(EntityMigrationTests.MigrationTestV2),
            typeof(EntityMigrationTests.AddColumnEvolution),
            typeof(EntityMigrationTests.AddColumnRequired),
            typeof(EntityMigrationTests.AddColumnNullable),
            typeof(EntityMigrationTests.AddColumnDataTypeOverride),
            typeof(EntityMigrationTests.DropColumnEvolution),
            typeof(EntityMigrationTests.DropColumnNotColumnTest),
            typeof(EntityMigrationTests.SystemColumnsTest),
            typeof(EntityMigrationTests.IndexMigrationEntity),
            typeof(EntityMigrationTests.UniqueIndexMigrationEntity),
            typeof(EntityMigrationTests.TriggerMigrationEntity),
            typeof(EntityMigrationTests.AuditLogEntity),
            typeof(EntityMigrationTests.FKParentEntity),
            typeof(EntityMigrationTests.FKChildEntity),
            typeof(EntityMigrationTests.AttributeRequiredEntity),
            typeof(EntityMigrationTests.CompositeIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity2),
            // Drop table test entities (from DropTableTests.cs)
            typeof(DropTableTests.DropTestEntity1),
            typeof(DropTableTests.DropTestEntity2),
            typeof(DropTableTests.DropTestParentEntity3),
            typeof(DropTableTests.DropTestChildEntity3),
            typeof(DropTableTests.DropTestParentEntity4),
            typeof(DropTableTests.DropTestChildEntity4),
            typeof(DropTableTests.DropTestParentEntity5),
            typeof(DropTableTests.DropTestChildEntity5),
            typeof(DropTableTests.DropTestEntity8),
            typeof(DropTableTests.DropTestEntity9),
            typeof(DropTableTests.DropTestEntity10),
            typeof(DropTableTests.DropTestEntity11),
            typeof(DropTableTests.DropTestEntity12),
            typeof(DropTableTests.DropTestEntity13),
            typeof(DropTableTests.DropTestParentEntity14),
            typeof(DropTableTests.DropTestChildEntity14),
            typeof(DropTableTests.DropTestEntity15),
            typeof(DropTableTests.DropTestGrandParentEntity16),
            typeof(DropTableTests.DropTestParentEntity16),
            typeof(DropTableTests.DropTestChildEntity16),
            typeof(DropTableTests.DropTestEntity17),
            typeof(DropTableTests.DropTestEntity18),
            typeof(DropTableTests.DropTestEntity19),
            typeof(DropTableTests.DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling),
            typeof(DropTableTests.DropTestParentEntity20),
            typeof(DropTableTests.DropTestChildEntity20A),
            typeof(DropTableTests.DropTestChildEntity20B),
            typeof(DropTableTests.DropTestEntity21),
            typeof(DropTableTests.DropTestEntity22)
            // NOTE: Column rename test entities are NOT registered here - tests register them individually
            // after setting up test data to properly exercise the rename migration logic
        ).GetAwaiter().GetResult();
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
        // Match the new RootJson structure: version and databases array at root level
        var config = new
        {
            version = 1L,
            databases = new[]
            {
                new
                {
                    database = TestDatabaseName,
                    isDefault = true
                }
            }
            // No need for Table, Insert, etc. arrays - SQLiteXM entities create their own schema
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TestSqlStatementsPath, json);
    }

    /// <summary>
    /// Initialize SQLiteXM with the test configuration and register all test entity schemas.
    /// NOTE: Initialization normally happens in static constructor.
    /// This method can be called after CleanupTestDataAsync() to re-initialize.
    /// </summary>
    protected async Task InitializeSqliteXMAsync()
    {
        // Re-initialize if needed (e.g., after cleanup)
        // InitializeAsync is safe to call multiple times - it returns early if already initialized
        var initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = TestDatabaseFolder
        };

        await SxmDatabase.InitializeAsync(TestSqlStatementsPath, initOptions);

        // Register all test entity schemas (idempotent - safe to call multiple times)
        await SxmDatabase.RegisterEntitiesAsync(
            // Standard test entities (from TestEntities.cs)
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity),
            // Migration test entities (from EntityMigrationTests.cs)
            typeof(EntityMigrationTests.MigrationTestV1),
            typeof(EntityMigrationTests.MigrationTestV2),
            typeof(EntityMigrationTests.AddColumnEvolution),
            typeof(EntityMigrationTests.AddColumnRequired),
            typeof(EntityMigrationTests.AddColumnNullable),
            typeof(EntityMigrationTests.AddColumnDataTypeOverride),
            typeof(EntityMigrationTests.DropColumnEvolution),
            typeof(EntityMigrationTests.DropColumnNotColumnTest),
            typeof(EntityMigrationTests.SystemColumnsTest),
            typeof(EntityMigrationTests.IndexMigrationEntity),
            typeof(EntityMigrationTests.UniqueIndexMigrationEntity),
            typeof(EntityMigrationTests.TriggerMigrationEntity),
            typeof(EntityMigrationTests.AuditLogEntity),
            typeof(EntityMigrationTests.FKParentEntity),
            typeof(EntityMigrationTests.FKChildEntity),
            typeof(EntityMigrationTests.AttributeRequiredEntity),
            typeof(EntityMigrationTests.CompositeIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity2),
            // Drop table test entities (from DropTableTests.cs)
            typeof(DropTableTests.DropTestEntity1),
            typeof(DropTableTests.DropTestEntity2),
            typeof(DropTableTests.DropTestParentEntity3),
            typeof(DropTableTests.DropTestChildEntity3),
            typeof(DropTableTests.DropTestParentEntity4),
            typeof(DropTableTests.DropTestChildEntity4),
            typeof(DropTableTests.DropTestParentEntity5),
            typeof(DropTableTests.DropTestChildEntity5),
            typeof(DropTableTests.DropTestEntity8),
            typeof(DropTableTests.DropTestEntity9),
            typeof(DropTableTests.DropTestEntity10),
            typeof(DropTableTests.DropTestEntity11),
            typeof(DropTableTests.DropTestEntity12),
            typeof(DropTableTests.DropTestEntity13),
            typeof(DropTableTests.DropTestParentEntity14),
            typeof(DropTableTests.DropTestChildEntity14),
            typeof(DropTableTests.DropTestEntity15),
            typeof(DropTableTests.DropTestGrandParentEntity16),
            typeof(DropTableTests.DropTestParentEntity16),
            typeof(DropTableTests.DropTestChildEntity16),
            typeof(DropTableTests.DropTestEntity17),
            typeof(DropTableTests.DropTestEntity18),
            typeof(DropTableTests.DropTestEntity19),
            typeof(DropTableTests.DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling),
            typeof(DropTableTests.DropTestParentEntity20),
            typeof(DropTableTests.DropTestChildEntity20A),
            typeof(DropTableTests.DropTestChildEntity20B),
            typeof(DropTableTests.DropTestEntity21),
            typeof(DropTableTests.DropTestEntity22)
            // NOTE: Column rename test entities are NOT registered here - tests register them individually
            // after setting up test data to properly exercise the rename migration logic
        );
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
        await SxmDatabase.ResetForTestingAsync();

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
    /// Cleans all data from test tables without dropping them or resetting static state.
    /// Use this for test isolation when you need a clean slate but don't need full re-initialization.
    /// This is safer and faster than CleanupTestDataAsync and works in all build configurations.
    /// </summary>
    protected async Task CleanupTableDataAsync()
    {
        // Close all connections first
        await SxmConnectionManager.Instance.ShutdownAsync(TestDatabaseName);

        // Force GC to release handles
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Delete all data from all test entity tables
        var entityTypes = new[]
        {
            // Standard test entities
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity),
            // Migration test entities
            typeof(EntityMigrationTests.MigrationTestV1),
            typeof(EntityMigrationTests.MigrationTestV2),
            typeof(EntityMigrationTests.AddColumnEvolution),
            typeof(EntityMigrationTests.AddColumnRequired),
            typeof(EntityMigrationTests.AddColumnNullable),
            typeof(EntityMigrationTests.AddColumnDataTypeOverride),
            typeof(EntityMigrationTests.DropColumnEvolution),
            typeof(EntityMigrationTests.DropColumnNotColumnTest),
            typeof(EntityMigrationTests.SystemColumnsTest),
            typeof(EntityMigrationTests.IndexMigrationEntity),
            typeof(EntityMigrationTests.UniqueIndexMigrationEntity),
            typeof(EntityMigrationTests.TriggerMigrationEntity),
            typeof(EntityMigrationTests.AuditLogEntity),
            typeof(EntityMigrationTests.FKParentEntity),
            typeof(EntityMigrationTests.FKChildEntity),
            typeof(EntityMigrationTests.AttributeRequiredEntity),
            typeof(EntityMigrationTests.CompositeIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity),
            typeof(EntityMigrationTests.FreshTableWithIndexEntity2),
            // Drop table test entities
            typeof(DropTableTests.DropTestEntity1),
            typeof(DropTableTests.DropTestEntity2),
            typeof(DropTableTests.DropTestParentEntity3),
            typeof(DropTableTests.DropTestChildEntity3),
            typeof(DropTableTests.DropTestParentEntity4),
            typeof(DropTableTests.DropTestChildEntity4),
            typeof(DropTableTests.DropTestParentEntity5),
            typeof(DropTableTests.DropTestChildEntity5),
            typeof(DropTableTests.DropTestEntity8),
            typeof(DropTableTests.DropTestEntity9),
            typeof(DropTableTests.DropTestEntity10),
            typeof(DropTableTests.DropTestEntity11),
            typeof(DropTableTests.DropTestEntity12),
            typeof(DropTableTests.DropTestEntity13),
            typeof(DropTableTests.DropTestParentEntity14),
            typeof(DropTableTests.DropTestChildEntity14),
            typeof(DropTableTests.DropTestEntity15),
            typeof(DropTableTests.DropTestGrandParentEntity16),
            typeof(DropTableTests.DropTestParentEntity16),
            typeof(DropTableTests.DropTestChildEntity16),
            typeof(DropTableTests.DropTestEntity17),
            typeof(DropTableTests.DropTestEntity18),
            typeof(DropTableTests.DropTestEntity19),
            typeof(DropTableTests.DropTestEntityWithAVeryLongNameThatTestsTheLimitsOfTableNameHandling),
            typeof(DropTableTests.DropTestParentEntity20),
            typeof(DropTableTests.DropTestChildEntity20A),
            typeof(DropTableTests.DropTestChildEntity20B),
            typeof(DropTableTests.DropTestEntity21),
            typeof(DropTableTests.DropTestEntity22)
        };

        var dbPath = Path.Combine(TestDatabaseFolder, TestDatabaseName);
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        foreach (var entityType in entityTypes)
        {
            try
            {
                string tableName = entityType.Name;
                await using var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM \"{tableName.Replace("\"", "\"\"")}\"";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Ignore errors - table might not exist yet
            }
        }

        // Checkpoint WAL to ensure changes are visible
        try
        {
            await using var checkpointCmd = connection.CreateCommand();
            checkpointCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            await checkpointCmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore WAL checkpoint errors
        }
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
        using var context = new SxmLinqDbContext(TestDatabaseName);
        var entity = context.GetTable<T>().FirstOrDefault(e => e.id == id);
        return entity;
    }

    /// <summary>
    /// Verifies that an entity with the given ID does NOT exist in the database.
    /// </summary>
    protected async Task VerifyEntityNotInDbAsync<T>(long id) where T : SxmEntity
    {
        using var context = new SxmLinqDbContext(TestDatabaseName);
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
        using var context = new SxmLinqDbContext(TestDatabaseName);
        return context.GetTable<T>().ToList();
    }

    /// <summary>
    /// Gets the count of entities of a given type from the database.
    /// </summary>
    protected int GetEntityCountFromDb<T>() where T : SxmEntity
    {
        using var context = new SxmLinqDbContext(TestDatabaseName);
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
            using var context = new SxmLinqDbContext(TestDatabaseName);
            _ = context.GetTable<T>().Count();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes a raw SQL command against the test database.
    /// </summary>
    protected async Task ExecuteNonQueryAsync(string sql)
    {
        // Database file does NOT have .db extension (it's just the database name)
        var dbPath = Path.Combine(TestDatabaseFolder, TestDatabaseName);
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a raw SQL scalar query against the test database.
    /// </summary>
    protected async Task<T> ExecuteScalarAsync<T>(string sql)
    {
        // Database file does NOT have .db extension (it's just the database name)
        var dbPath = Path.Combine(TestDatabaseFolder, TestDatabaseName);
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return result == null || result is DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Checks if a column exists in a table.
    /// </summary>
    protected async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        var count = await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = '{columnName}'");
        return count > 0;
    }

    /// <summary>
    /// Gets the SQL type of a column.
    /// </summary>
    protected async Task<string> GetColumnTypeAsync(string tableName, string columnName)
    {
        var result = await ExecuteScalarAsync<string>(
            $"SELECT type FROM pragma_table_info('{tableName}') WHERE name = '{columnName}'");
        return result ?? string.Empty;
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
            // DO NOT delete the test database folder here!
            // The database is SHARED across all test classes (initialized once in static constructor).
            // Deleting it here would break subsequent test classes.
            // 
            // The temp folder cleanup happens automatically by the OS, or can be done manually
            // between full test suite runs if needed.
            //
            // Original problematic code that deleted shared resources:
            // try
            // {
            //     Thread.Sleep(100);
            //     if (Directory.Exists(TestDatabaseFolder))
            //     {
            //         Directory.Delete(TestDatabaseFolder, recursive: true);
            //     }
            // }
            // catch { }
        }

        _disposed = true;
    }
}
