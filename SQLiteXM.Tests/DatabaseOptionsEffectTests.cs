using System.Text.Json;
using Microsoft.Data.Sqlite;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests verifying that the settings supplied through <see cref="SxmDatabaseOptions"/> are actually
/// applied to real SQLite connections, rather than merely passing validation.
/// </summary>
/// <remarks>
/// <para>
/// Most of the PRAGMAs configured by SQLiteXM (synchronous, foreign_keys, cache_size, temp_store,
/// busy_timeout, wal_autocheckpoint) are <em>per-connection</em> settings: they apply only to the
/// connection that issued them and are not stored in the database file. That means they cannot be
/// observed by opening a separate connection after the fact.
/// </para>
/// <para>
/// To observe them, these tests use the library's own extension point. <c>ConnectionOpened</c> applies
/// all PRAGMAs first and then invokes every registered connection-opened interceptor, handing each one
/// the live <see cref="SqliteConnection"/>. Registering an interceptor therefore gives the test a view
/// of exactly the connection SQLiteXM just configured. <c>journal_mode</c> is the exception - it is
/// persisted in the database file - so it is additionally verified from an independent connection.
/// </para>
/// <para>
/// These tests reconfigure process-wide state via <c>ResetForTestingAsync()</c>, so they run in their own
/// non-parallel collection and restore the standard TestBase configuration in <see cref="Dispose"/> so the
/// rest of the suite continues to work correctly.
/// </para>
/// </remarks>
[Collection("DatabaseOptionsEffect")]
public class DatabaseOptionsEffectTests : IDisposable
{
    private static readonly string OptionsEffectTestFolder;
    private readonly string _testId;
    private bool _disposed;

    static DatabaseOptionsEffectTests()
    {
        OptionsEffectTestFolder = Path.Combine(TestBase.TestRootFolder, "DatabaseOptionsEffect");
        Directory.CreateDirectory(OptionsEffectTestFolder);
    }

    public DatabaseOptionsEffectTests()
    {
        _testId = Guid.NewGuid().ToString("N");
    }

    #region Test infrastructure

    /// <summary>
    /// Reads a single PRAGMA value from an already-open connection.
    /// </summary>
    /// <remarks>
    /// Runs synchronously because it is called from inside a connection-opened interceptor, which the
    /// library invokes on the thread that opened the connection.
    /// </remarks>
    private static T? ReadPragma<T>(SqliteConnection connection, string pragmaName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragmaName}";
        object? result = command.ExecuteScalar();

        return result is null or DBNull ? default : (T)Convert.ChangeType(result, typeof(T));
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

        string path = Path.Combine(OptionsEffectTestFolder, $"statements_{_testId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    /// <summary>
    /// Initializes SQLiteXM from a clean slate using the supplied options and then forces a real
    /// connection to be opened so that the configured PRAGMAs are applied.
    /// </summary>
    /// <param name="configure">
    /// Applies the settings under test to a fresh options instance. The database folder override is
    /// supplied by this helper so every test writes into an isolated folder.
    /// </param>
    /// <returns>The options instance that was used, so interceptors can be registered against it.</returns>
    private async Task<SxmDatabaseOptions> InitializeWithOptionsAsync(Func<string, SxmDatabaseOptions> configure)
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"optdb_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);
        SxmDatabaseOptions options = configure(OptionsEffectTestFolder);

        using (Stream stream = File.OpenRead(statementsPath))
        {
            SxmDatabase.StartInitialization(stream, options, typeof(SimpleEntity));
            await SxmDatabase.EnsureReadyAsync();
        }

        // Saving an entity guarantees a physical connection is opened and configured.
        var entity = new SimpleEntity { Name = "OptionsEffect", Age = 1, IsActive = true };
        await entity.SaveAsync();

        return options;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // WAL mode leaves -wal and -shm sidecar files behind, so match on the test id prefix.
            foreach (string file in Directory.GetFiles(OptionsEffectTestFolder, $"*{_testId}*"))
            {
                try { File.Delete(file); } catch { /* best effort cleanup */ }
            }
        }
        catch { /* best effort cleanup */ }

        // CRITICAL: Reset SQLiteXM state and re-initialize with the standard TestBase configuration so
        // subsequent tests (which assume "test_database" is ready) continue to work correctly.
        SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();

        var initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = Path.Combine(TestBase.TestRootFolder, "test_database")
        };
        Directory.CreateDirectory(initOptions.DatabaseFolderOverride);

        string testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
        if (!File.Exists(testStatementsPath))
        {
            var config = new
            {
                version = 1L,
                databases = new[] { new { database = "test_database", isDefault = true } }
            };
            File.WriteAllText(testStatementsPath, JsonSerializer.Serialize(config));
        }

        using (Stream stream = File.OpenRead(testStatementsPath))
        {
            SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();
        }

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

        GC.SuppressFinalize(this);
    }

    #endregion

    #region foreign_keys

    [Theory]
    [InlineData(true, 1L)]
    [InlineData(false, 0L)]
    public async Task ForeignKeys_ShouldBeAppliedToTheOpenedConnection(bool foreignKeys, long expectedPragmaValue)
    {
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                ForeignKeys = foreignKeys
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "foreign_keys"));
            return options;
        });

        observed.Should().Be(expectedPragmaValue);
    }

    #endregion

    #region synchronous

    [Theory]
    [InlineData(SxmSynchronousMode.Normal, 1L)]
    [InlineData(SxmSynchronousMode.Full, 2L)]
    [InlineData(SxmSynchronousMode.Extra, 3L)]
    public async Task SynchronousMode_ShouldBeAppliedToTheOpenedConnection(SxmSynchronousMode mode, long expectedPragmaValue)
    {
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                SynchronousModeOption = mode
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "synchronous"));
            return options;
        });

        observed.Should().Be(expectedPragmaValue);
    }

    #endregion

    #region temp_store

    [Theory]
    [InlineData(SxmTempStore.File, 1L)]
    [InlineData(SxmTempStore.Memory, 2L)]
    public async Task TempStore_ShouldBeAppliedToTheOpenedConnection(SxmTempStore tempStore, long expectedPragmaValue)
    {
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                TempStore = tempStore
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "temp_store"));
            return options;
        });

        observed.Should().Be(expectedPragmaValue);
    }

    #endregion

    #region cache_size

    [Fact]
    public async Task CacheSize_ShouldBeAppliedAsANegativeKilobyteValue()
    {
        // SQLiteXM expresses CacheSize in KB. SQLite encodes a KB-based cache as a negative number
        // (a positive number would mean "pages"), so the library writes PRAGMA cache_size = -<KB>.
        const long cacheSizeKb = 4096;
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                CacheSize = cacheSizeKb
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "cache_size"));
            return options;
        });

        observed.Should().Be(-cacheSizeKb);
    }

    #endregion

    #region busy_timeout

    [Fact]
    public async Task BusyTimeout_ShouldBeAppliedToTheOpenedConnection()
    {
        const long busyTimeoutMs = 7500;
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                BusyTimeout = busyTimeoutMs
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "busy_timeout"));
            return options;
        });

        observed.Should().Be(busyTimeoutMs);
    }

    #endregion

    #region journal_mode

    [Theory]
    [InlineData(SxmJournalMode.Delete, "delete")]
    [InlineData(SxmJournalMode.Truncate, "truncate")]
    [InlineData(SxmJournalMode.Persist, "persist")]
    [InlineData(SxmJournalMode.Memory, "memory")]
    [InlineData(SxmJournalMode.Wal, "wal")]
    public async Task JournalMode_ShouldBeAppliedToTheOpenedConnection(SxmJournalMode journalMode, string expectedPragmaValue)
    {
        string? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                JournalModeOption = journalMode
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<string>(connection, "journal_mode"));
            return options;
        });

        observed.Should().NotBeNull();
        observed!.ToLowerInvariant().Should().Be(expectedPragmaValue);
    }

    [Fact]
    public async Task JournalModeWal_ShouldPersistInTheDatabaseFileForOtherConnections()
    {
        // Unlike the other PRAGMAs, WAL mode is recorded in the database file itself, so a completely
        // independent connection should also report it.
        await InitializeWithOptionsAsync(folder => new SxmDatabaseOptions
        {
            DatabaseFolderOverride = folder,
            JournalModeOption = SxmJournalMode.Wal
        });

        string databasePath = Path.Combine(OptionsEffectTestFolder, $"optdb_{_testId}");

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode";
        object? journalMode = await command.ExecuteScalarAsync();

        journalMode.Should().NotBeNull();
        journalMode!.ToString()!.ToLowerInvariant().Should().Be("wal");
    }

    #endregion

    #region wal_autocheckpoint

    [Fact]
    public async Task WalAutoCheckpoint_ShouldBeAppliedToTheOpenedConnection()
    {
        const long walAutoCheckpointPages = 512;
        long? observed = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                JournalModeOption = SxmJournalMode.Wal,
                WalAutoCheckpoint = walAutoCheckpointPages
            };

            options.OnConnectionOpened(connection => observed ??= ReadPragma<long>(connection, "wal_autocheckpoint"));
            return options;
        });

        observed.Should().Be(walAutoCheckpointPages);
    }

    #endregion

    #region Multiple options together

    [Fact]
    public async Task MultipleOptions_ShouldAllBeAppliedToTheSameConnection()
    {
        // Guards against one PRAGMA assignment clobbering another.
        long? foreignKeys = null;
        long? synchronous = null;
        long? cacheSize = null;
        long? tempStore = null;
        long? busyTimeout = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                ForeignKeys = true,
                SynchronousModeOption = SxmSynchronousMode.Full,
                CacheSize = 2048,
                TempStore = SxmTempStore.Memory,
                BusyTimeout = 3000
            };

            options.OnConnectionOpened(connection =>
            {
                foreignKeys ??= ReadPragma<long>(connection, "foreign_keys");
                synchronous ??= ReadPragma<long>(connection, "synchronous");
                cacheSize ??= ReadPragma<long>(connection, "cache_size");
                tempStore ??= ReadPragma<long>(connection, "temp_store");
                busyTimeout ??= ReadPragma<long>(connection, "busy_timeout");
            });

            return options;
        });

        foreignKeys.Should().Be(1L);
        synchronous.Should().Be(2L);
        cacheSize.Should().Be(-2048L);
        tempStore.Should().Be(2L);
        busyTimeout.Should().Be(3000L);
    }

    [Fact]
    public async Task NoOptionsConfigured_ShouldStillOpenAUsableConnection()
    {
        // With every PRAGMA left unset the library should skip them all and still work.
        bool interceptorRan = false;
        long? result = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder
            };

            options.OnConnectionOpened(connection =>
            {
                interceptorRan = true;
                result ??= ReadPragma<long>(connection, "foreign_keys");
            });

            return options;
        });

        interceptorRan.Should().BeTrue();
        result.Should().NotBeNull("the connection should be usable even with no options configured");
    }

    #endregion

    #region Interceptors

    [Fact]
    public async Task OnConnectionOpened_ShouldReceiveAnOpenConnection()
    {
        System.Data.ConnectionState? state = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder
            };

            options.OnConnectionOpened(connection => state ??= connection.State);
            return options;
        });

        state.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task OnConnectionOpened_WithMultipleInterceptors_ShouldInvokeThemInRegistrationOrder()
    {
        var invocationOrder = new List<string>();

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder
            };

            options.OnConnectionOpened(_ => invocationOrder.Add("first"));
            options.OnConnectionOpened(_ => invocationOrder.Add("second"));
            options.OnConnectionOpened(_ => invocationOrder.Add("third"));
            return options;
        });

        invocationOrder.Should().NotBeEmpty();
        invocationOrder.Take(3).Should().Equal("first", "second", "third");
    }

    [Fact]
    public async Task OnConnectionOpened_ShouldRunAfterPragmasHaveBeenApplied()
    {
        // The interceptor contract is that PRAGMAs are already in place when it runs, so an interceptor
        // can rely on the configured settings.
        long? synchronousSeenByInterceptor = null;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder,
                SynchronousModeOption = SxmSynchronousMode.Extra
            };

            options.OnConnectionOpened(connection =>
                synchronousSeenByInterceptor ??= ReadPragma<long>(connection, "synchronous"));

            return options;
        });

        synchronousSeenByInterceptor.Should().Be(3L, "PRAGMAs are applied before interceptors run");
    }

    [Fact]
    public async Task OnConnectionClosed_ShouldBeInvokedWhenConnectionsAreClosed()
    {
        int closedCount = 0;

        await InitializeWithOptionsAsync(folder =>
        {
            var options = new SxmDatabaseOptions
            {
                DatabaseFolderOverride = folder
            };

            options.OnConnectionClosed(() => Interlocked.Increment(ref closedCount));
            return options;
        });

        // Resetting tears down the pooled connections, which triggers the closed interceptors.
        await SxmDatabase.ResetForTestingAsync();

        closedCount.Should().BeGreaterThan(0);
    }

    #endregion
}
