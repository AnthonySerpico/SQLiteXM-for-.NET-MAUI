using Microsoft.Data.Sqlite;
using SQLiteXM;
using System.Collections.Concurrent;

/// <summary>
/// Delegate invoked after a SQLite connection has been opened.
/// </summary>
/// <param name="sqliteConnection">The opened SQLite connection.</param>
public delegate void ConnectionOpenedInterceptor(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection);

/// <summary>
/// Delegate invoked after a SQLite connection has been closed.
/// </summary>
public delegate void ConnectionClosedInterceptor();

/// <summary>
/// Represents configuration options used when initializing a SQLiteXM database.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SxmDatabaseOptions"/> allows callers to control database initialization
/// behavior without requiring direct interaction with low-level SQLite PRAGMA commands.
/// These settings are applied during <see cref="SxmDatabase.InitializeAsync(string, SxmDatabaseOptions?, System.Threading.CancellationToken)"/>
/// and are intended to provide a safe, high-level configuration surface.
/// </para>
/// <para>
/// Instances of this class are immutable after creation. Callers may use object
/// initializer syntax to specify only the settings they wish to override.
/// </para>
/// <para>
/// Additional options may be added in future versions of SQLiteXM without breaking
/// existing callers.
/// </para>
/// </remarks>
public sealed class SxmDatabaseOptions
{
    private static ConcurrentDictionary<string, SxmDatabaseOptions>? _databaseNames;

#if DEBUG
    /// <summary>
    /// Resets the database name registry for testing purposes.
    /// **WARNING:** Only call this in test scenarios.
    /// </summary>
    internal static void ResetForTesting()
    {
        _databaseNames?.Clear();
        _databaseNames = null;
    }
#endif

    private CheckPointConnection? _checkPointConnection;

    /// <summary>
    /// Gets or sets the checkpoint behavior applied when a connection is closing.
    /// </summary>
    public CheckPointConnection? CheckPointConnection { get => _checkPointConnection; init { _checkPointConnection = value; } }

    private int? _checkPointWalMaxSize;

    /// <summary>
    /// Gets or sets the maximum WAL size in KB before a checkpoint is triggered.
    /// </summary>
    public int? CheckPointWalMaxSize { get => _checkPointWalMaxSize; init { _checkPointWalMaxSize = value; } }

    /// <summary>
    /// Gets or sets the SQLite busy timeout in milliseconds.
    /// </summary>
    public long? BusyTimeout { get => _busyTimeout; init { _busyTimeout = value; } }
    private long? _busyTimeout = null;

    /// <summary>
    /// Gets or sets the SQLite cache size in KB.
    /// </summary>
    public long? CacheSize { get => _cacheSize; init { _cacheSize = value; } }
    private long? _cacheSize = null;

    /// <summary>
    /// Gets or sets the WAL auto-checkpoint threshold in pages.
    /// </summary>
    public long? WalAutoCheckpoint { get => _walAutoCheckpoint; init { _walAutoCheckpoint = value; } }
    private long? _walAutoCheckpoint = null;

    /// <summary>
    /// Gets or sets whether connection pooling is enabled.
    /// </summary>
    public bool? EnableConnectionPooling { get => _enableConnectionPooling; init { _enableConnectionPooling = value; } }
    private static bool? _enableConnectionPooling = null;

    /// <summary>
    /// Gets or sets whether logging is enabled.
    /// </summary>
    public bool? EnableLogging { get => _enableLogging; init { _enableLogging = value; } }
    private static bool? _enableLogging = null;

    /// <summary>
    /// Gets or sets the default timeout in seconds.
    /// </summary>
    public int? DefaultTimeout { get => _defaultTimeout; init { _defaultTimeout = value; } }
    private static int? _defaultTimeout = null;

    /// <summary>
    /// Gets or sets whether foreign key enforcement is enabled.
    /// </summary>
    public bool? ForeignKeys { get => _foreignKeys; init { _foreignKeys = value; } }
    private bool? _foreignKeys = null;

    /// <summary>
    /// Gets or sets the SQLite temp_store setting.
    /// </summary>
    public SxmTempStore? TempStore { get => _tempStore; init { _tempStore = value; } }
    private SxmTempStore? _tempStore = null;

    /// <summary>
    /// Gets or sets the folder override used when creating the database file.
    /// </summary>
    public string? DatabaseFolderOverride { get; init; }

    private List<ConnectionOpenedInterceptor>? ConnectionOpenedInterceptors { get; set; }
    private List<ConnectionClosedInterceptor>? ConnectionClosedInterceptors { get; set; }

    /// <summary>
    /// Gets the SQLite journal mode enum that should be applied during initialization.
    /// Callers set this value via object initializer syntax.
    /// </summary>
    public SxmJournalMode? JournalModeOption { get; init; } = null;

    private SxmSynchronousMode? _synchronousMode = null;

    /// <summary>
    /// Gets the SQLite synchronous mode enum that should be applied during initialization.
    /// Callers set this value via object initializer syntax.
    /// </summary>
    public SxmSynchronousMode? SynchronousModeOption { get => _synchronousMode; init { _synchronousMode = value; } }

    private readonly object _interceptorLock = new();

    /// <summary>
    /// Adds a database name mapping for the provided initialization options.
    /// </summary>
    /// <param name="initOptions">The initialization options to associate.</param>
    /// <remarks>
    /// <para>
    /// This method registers the provided <paramref name="initOptions"/> for all databases
    /// currently known to <see cref="SxmProcessSQLStatements.Databases"/>. Each database name
    /// is mapped to the same options instance, allowing connection-level configuration to be
    /// retrieved later via <see cref="GetInitOptionsFromDatabaseName"/>.
    /// </para>
    /// <para>
    /// If <paramref name="initOptions"/> is null, the method returns immediately without
    /// making any changes to the registry.
    /// </para>
    /// <para>
    /// This method is thread-safe and uses a <see cref="ConcurrentDictionary{TKey, TValue}"/>
    /// to store the mappings. If a database name has already been registered, an
    /// <see cref="InvalidOperationException"/> is thrown to prevent accidental reconfiguration.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a database name is already registered with different initialization options.
    /// </exception>
    internal static void AddDatabaseNames(SxmDatabaseOptions? initOptions)
    {
        if (initOptions is null)
            return;

        _databaseNames ??= new();

        foreach(string databaseName in  SxmProcessSQLStatements.Databases)
        {
            if (!_databaseNames.TryAdd(databaseName, initOptions))
            {
                throw new InvalidOperationException($"Initialization failed. Database name '{databaseName}' was already registered.");
            }
        }
    }

    /// <summary>
    /// Retrieves initialization options previously associated with a database name.
    /// </summary>
    /// <param name="databaseName">The database name to look up.</param>
    /// <returns>The initialization options, or null if none are registered.</returns>
    /// <remarks>
    /// <para>
    /// This method performs a thread-safe lookup in the internal database name registry.
    /// It is used internally by connection lifecycle methods to apply the correct
    /// configuration settings and invoke the appropriate interceptors.
    /// </para>
    /// <para>
    /// If <paramref name="databaseName"/> is null, empty, or if the internal registry
    /// has not been initialized, the method returns null immediately.
    /// </para>
    /// </remarks>
    private static SxmDatabaseOptions? GetInitOptionsFromDatabaseName(string? databaseName)
    {
        if (string.IsNullOrEmpty(databaseName) || _databaseNames is null)
            return null;

        return _databaseNames.TryGetValue(databaseName, out SxmDatabaseOptions? initOptions) ? initOptions : null;
    }

    /// <summary>
    /// Adds a handler that is invoked after a connection is opened.
    /// </summary>
    /// <param name="connectionOpenedInterceptor">The handler to register.</param>
    /// <remarks>
    /// <para>
    /// This method allows callers to register custom logic that executes immediately after
    /// a SQLite connection is opened and after all PRAGMA settings have been applied.
    /// Multiple handlers can be registered, and they will be invoked in the order they
    /// were added.
    /// </para>
    /// <para>
    /// The registered handler receives the opened <see cref="SqliteConnection"/> instance,
    /// allowing for custom initialization, logging, or instrumentation.
    /// </para>
    /// <para>
    /// This method is thread-safe. The handler is stored in an internal collection that
    /// is protected by a lock.
    /// </para>
    /// </remarks>
    public void OnConnectionOpened(ConnectionOpenedInterceptor connectionOpenedInterceptor)
    {
        lock (_interceptorLock)
        {
            ConnectionOpenedInterceptors ??= new List<ConnectionOpenedInterceptor>();
            ConnectionOpenedInterceptors.Add(connectionOpenedInterceptor);
        }
    }

    /// <summary>
    /// Adds a handler that is invoked after a connection is closed.
    /// </summary>
    /// <param name="connectionClosedInterceptor">The handler to register.</param>
    /// <remarks>
    /// <para>
    /// This method allows callers to register custom logic that executes after a SQLite
    /// connection has been closed. Multiple handlers can be registered, and they will be
    /// invoked in the order they were added.
    /// </para>
    /// <para>
    /// Unlike <see cref="OnConnectionOpened"/>, the closed interceptor does not receive
    /// a connection parameter since the connection is already closed when the handler runs.
    /// This is useful for cleanup operations, logging, or resource management.
    /// </para>
    /// <para>
    /// This method is thread-safe. The handler is stored in an internal collection that
    /// is protected by a lock.
    /// </para>
    /// </remarks>
    public void OnConnectionClosed(ConnectionClosedInterceptor connectionClosedInterceptor)
    {
        lock (_interceptorLock)
        {
            ConnectionClosedInterceptors ??= new List<ConnectionClosedInterceptor>();
            ConnectionClosedInterceptors.Add(connectionClosedInterceptor);
        }
    }

    /// <summary>
    /// Gets the SQLite PRAGMA string for journal_mode corresponding to <see cref="JournalModeOption"/>.
    /// </summary>
    private string JournalMode => JournalModeOption switch
    {
        SxmJournalMode.Wal => "WAL",
        SxmJournalMode.Delete => "DELETE",
        SxmJournalMode.Truncate => "TRUNCATE",
        SxmJournalMode.Persist => "PERSIST",
        SxmJournalMode.Memory => "MEMORY",
        SxmJournalMode.Off => "OFF",
        _ => throw new ArgumentOutOfRangeException(nameof(JournalModeOption), JournalModeOption, "Unsupported journal mode.")
    };

    /// <summary>
    /// Applies initialization pragmas and invokes any connection-opened interceptors.
    /// </summary>
    /// <param name="sqliteConnection">The opened SQLite connection.</param>
    /// <param name="databaseName">The database name associated with the connection.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically by the SQLiteXM connection infrastructure immediately
    /// after a physical SQLite connection is opened. It performs two main operations:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>
    /// Applies all configured PRAGMA settings (journal mode, synchronous mode, foreign keys,
    /// cache size, etc.) by calling <see cref="RunConnectionPragmas"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Invokes all registered connection-opened interceptors in the order they were added.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// If either <paramref name="sqliteConnection"/> is null or no initialization options
    /// are found for <paramref name="databaseName"/>, the method returns immediately without
    /// performing any operations.
    /// </para>
    /// </remarks>
    internal static void ConnectionOpened(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection, string? databaseName)
    {
        SxmDatabaseOptions? initOptions = GetInitOptionsFromDatabaseName(databaseName);
        if (sqliteConnection == null || initOptions == null)
            return;

        RunConnectionPragmas(sqliteConnection, initOptions);

        lock (initOptions._interceptorLock)
        {
            if (initOptions.ConnectionOpenedInterceptors != null)
            {
                foreach (ConnectionOpenedInterceptor connectionOpenedInterceptor in initOptions.ConnectionOpenedInterceptors)
                    connectionOpenedInterceptor(sqliteConnection);
            }
        }
    }

    /// <summary>
    /// Determines whether connection pooling is enabled.
    /// </summary>
    /// <returns>True if connection pooling is enabled; otherwise, false. Defaults to true if not explicitly configured.</returns>
    /// <remarks>
    /// This method is used internally by the connection management infrastructure to decide
    /// whether to reuse existing connections or create new ones. Connection pooling is enabled
    /// by default to improve performance.
    /// </remarks>
    internal static bool IsConnectionPoolingEnabled()
    {
        return _enableConnectionPooling ?? true;
    }

    /// <summary>
    /// Retrieves the configured default timeout in seconds.
    /// </summary>
    /// <returns>The default timeout in seconds, or null if not configured.</returns>
    /// <remarks>
    /// This method returns the timeout value that should be applied to database commands.
    /// If no timeout has been explicitly configured, null is returned, allowing the caller
    /// to apply their own default.
    /// </remarks>
    internal static int? GetDefaultTimeout()
    {
        return _defaultTimeout;
    }
    /// <summary>
    /// Determines whether logging is enabled.
    /// </summary>
    /// <returns>True if logging is enabled; otherwise, false. Defaults to false if not explicitly configured.</returns>
    /// <remarks>
    /// This method is used by the SQLiteXM infrastructure to determine whether to emit
    /// diagnostic log messages. Logging is disabled by default to minimize performance overhead.
    /// </remarks>
    internal static bool IsLoggingEnabled()
    {
        return _enableLogging ?? false;
    }

    /// <summary>
    /// Invokes any connection-closed interceptors associated with a database name.
    /// </summary>
    /// <param name="databaseName">The database name whose interceptors should run.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically by the SQLiteXM connection infrastructure after
    /// a physical SQLite connection has been closed. It invokes all registered connection-closed
    /// interceptors in the order they were added.
    /// </para>
    /// <para>
    /// If no initialization options are found for <paramref name="databaseName"/>, or if
    /// no interceptors have been registered, the method returns immediately without performing
    /// any operations.
    /// </para>
    /// <para>
    /// The interceptors are invoked within a lock to ensure thread-safe access to the
    /// interceptor collection.
    /// </para>
    /// </remarks>
    internal static void ConnectionClosed(string? databaseName)
    {
        SxmDatabaseOptions? initOptions = GetInitOptionsFromDatabaseName(databaseName);
        if (initOptions == null)
            return;

        lock (initOptions._interceptorLock)
        {
            if (initOptions.ConnectionClosedInterceptors != null)
            {
                foreach (ConnectionClosedInterceptor connectionClosedInterceptor in initOptions.ConnectionClosedInterceptors)
                    connectionClosedInterceptor();
            }
        }
    }

    /// <summary>
    /// Performs optional WAL checkpointing when a connection is closing.
    /// </summary>
    /// <param name="sqliteConnection">The SQLite connection being closed.</param>
    /// <param name="databaseName">The associated database name.</param>
    /// <remarks>
    /// <para>
    /// This method is called automatically before a SQLite connection is closed. It handles
    /// WAL (Write-Ahead Logging) checkpoint operations based on the configured
    /// <see cref="CheckPointConnection"/> option.
    /// </para>
    /// <para>
    /// The method performs the following checkpoint strategies:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If <see cref="CheckPointConnection"/> is <c>OnConnectionClose</c>, a PASSIVE checkpoint
    /// is executed to attempt transferring WAL data to the main database file without blocking.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <see cref="CheckPointConnection"/> is <c>MaxSize</c> and the WAL file exceeds
    /// <see cref="CheckPointWalMaxSize"/> (in KB), a TRUNCATE checkpoint is executed to
    /// reduce the WAL file size.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Checkpointing is only performed when the journal mode is WAL. If the database is
    /// not using WAL mode, the method returns immediately.
    /// </para>
    /// </remarks>
    internal static void ConnectionClosing(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection, string? databaseName)
    {
        SxmDatabaseOptions? initOptions = GetInitOptionsFromDatabaseName(databaseName);
        if (sqliteConnection == null || initOptions == null || initOptions.CheckPointConnection == null || initOptions.CheckPointConnection == SQLiteXM.CheckPointConnection.Off)
            return;

        if (initOptions.JournalModeOption != SxmJournalMode.Wal)
        {   // Checkpointing is only relevant for WAL mode, so skip if not in WAL mode.
            return;
        }

        if (initOptions.CheckPointConnection == SQLiteXM.CheckPointConnection.OnConnectionClose)
        {
            CheckPointWal(sqliteConnection, "PASSIVE");
        }

        // Before computing this, you need to get the SxmDatabase.InitOptions asssociated with this database name, which requires it to be saved here in SxmDatabase.
        if (databaseName != null && _databaseNames != null)
        {
            if (initOptions != null)
            {
                if (initOptions.CheckPointWalMaxSize == null || initOptions.CheckPointConnection != SQLiteXM.CheckPointConnection.MaxSize)
                    return;

                long? walMaxSize = 1024 * 2048;
                if (initOptions.CheckPointWalMaxSize != null)
                    walMaxSize = initOptions.CheckPointWalMaxSize * 1024;

                long walFileSize = SxmDatabaseDescriptor.GetWalFileSize(databaseName);
                if (walFileSize > walMaxSize)
                    CheckPointWal(sqliteConnection, "TRUNCATE");
            }
        }
    }

    /// <summary>
    /// Executes a WAL checkpoint using the specified checkpoint mode.
    /// </summary>
    /// <param name="sqliteConnection">The SQLite connection to use.</param>
    /// <param name="checkPointType">The checkpoint mode to apply.</param>
    /// <remarks>
    /// <para>
    /// This method executes the SQLite PRAGMA wal_checkpoint command with the specified
    /// checkpoint type (e.g., "PASSIVE", "FULL", "RESTART", or "TRUNCATE").
    /// </para>
    /// <para>
    /// The checkpoint operation transfers data from the WAL file to the main database file.
    /// Different checkpoint modes have different behaviors:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// PASSIVE: Checkpoint as much as possible without blocking, up to the busy timeout.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// TRUNCATE: Like FULL, but also truncates the WAL file to zero bytes if successful.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// The method reads the result from the PRAGMA command, which returns three integers:
    /// the number of busy pages, the number of log pages, and the number of checkpointed pages.
    /// These values are currently read but not used by the caller.
    /// </para>
    /// </remarks>
    private static void CheckPointWal(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection, string checkPointType)
    {
        using (var cmd = sqliteConnection!.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA wal_checkpoint({checkPointType})";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int busy = reader.GetInt32(0);
                int log = reader.GetInt32(1);
                int checkpointed = reader.GetInt32(2);
            }
        }
    }

    /// <summary>
    /// Applies configured SQLite PRAGMA settings to the connection.
    /// </summary>
    /// <param name="sqliteConnection">The SQLite connection to configure.</param>
    /// <param name="initOptions">The initialization options to apply.</param>
    /// <remarks>
    /// <para>
    /// This method executes synchronously to apply all configured PRAGMA settings immediately
    /// after a connection is opened. It validates that each PRAGMA was successfully applied
    /// by reading back the value and comparing it to the requested setting.
    /// </para>
    /// <para>
    /// The following PRAGMA settings are applied if configured in <paramref name="initOptions"/>:
    /// </para>
    /// <list type="bullet">
    /// <item><description>foreign_keys - Enables or disables foreign key constraint enforcement</description></item>
    /// <item><description>synchronous - Controls how aggressively SQLite writes data to disk</description></item>
    /// <item><description>journal_mode - Sets the rollback journal mode (DELETE, WAL, etc.)</description></item>
    /// <item><description>busy_timeout - Sets the maximum time to wait when the database is locked</description></item>
    /// <item><description>cache_size - Sets the maximum number of database pages to cache in memory (in KB)</description></item>
    /// <item><description>wal_autocheckpoint - Sets the WAL auto-checkpoint threshold in pages</description></item>
    /// <item><description>temp_store - Controls where temporary tables and indices are stored</description></item>
    /// </list>
    /// <para>
    /// If any PRAGMA fails to apply correctly, an <see cref="InvalidOperationException"/> is thrown
    /// with details about the requested and actual values.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a PRAGMA setting cannot be applied or when the verification fails.
    /// </exception>
    private static void RunConnectionPragmas(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection, SxmDatabaseOptions initOptions)
    {
        // Execute PRAGMA synchronously (very quick) to avoid sync-over-async in ctor.
        using (Microsoft.Data.Sqlite.SqliteCommand cmd = sqliteConnection.CreateCommand())
        {
            if (initOptions.ForeignKeys != null)
            {
                cmd.CommandText = $"PRAGMA foreign_keys = {initOptions.ForeignKeys}";
                cmd.ExecuteNonQuery();

                cmd.CommandText = $"PRAGMA foreign_keys";
                long? foreignKeys = (long?)cmd.ExecuteScalar();
                if (foreignKeys == null || foreignKeys != (long)((bool)initOptions.ForeignKeys ? 1L : 0L))
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA foreign_keys to '{initOptions.ForeignKeys}'. Actual mode is '{foreignKeys}'.");
                }
            }

            if (initOptions.SynchronousModeOption != default)
            {
                cmd.CommandText = $"PRAGMA synchronous = {(long?)initOptions.SynchronousModeOption}";
                cmd.ExecuteNonQuery();

                cmd.CommandText = $"PRAGMA synchronous";
                long? synchronous = (long?)cmd.ExecuteScalar();
                if (synchronous == null || synchronous != (long?)initOptions.SynchronousModeOption)
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA synchronous to '{initOptions.SynchronousModeOption}'. Actual mode is '{synchronous}'.");
                }
            }

            if (initOptions.JournalModeOption != default)
            {
                cmd.CommandText = $"PRAGMA journal_mode = {initOptions.JournalMode}";
                string? journalMode = (string?)cmd.ExecuteScalar();
                if (journalMode == null || !journalMode.ToLower().Equals(initOptions.JournalMode.ToLower()))
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA journal_mode to '{initOptions.JournalMode}'. Actual mode is '{journalMode}'.");
                }
            }

            if (initOptions.BusyTimeout != default)
            {
                cmd.CommandText = $"PRAGMA busy_timeout = {initOptions.BusyTimeout}";
                long? busyTimeout = (long?)cmd.ExecuteScalar();
                if (busyTimeout == null || busyTimeout != initOptions.BusyTimeout)
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA busy_timeout to '{initOptions.BusyTimeout}'. Actual mode is '{busyTimeout}'.");
                }
            }

            if (initOptions.CacheSize != default)
            {
                cmd.CommandText = $"PRAGMA cache_size = -{initOptions.CacheSize}";
                cmd.ExecuteNonQuery();

                cmd.CommandText = $"PRAGMA cache_size";
                long? cacheSize = (long?)cmd.ExecuteScalar();
                if (cacheSize == null || cacheSize != -initOptions.CacheSize)
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA cache_size to '{initOptions.CacheSize}'. Actual mode is '{cacheSize}'.");
                }
            }

            if (initOptions.WalAutoCheckpoint != default)
            {
                cmd.CommandText = $"PRAGMA wal_autocheckpoint = {initOptions.WalAutoCheckpoint}";
                long? walAutoCheckpoint = (long?)cmd.ExecuteScalar();
                if (walAutoCheckpoint == null || walAutoCheckpoint != initOptions.WalAutoCheckpoint)
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA wal_autocheckpoint to '{initOptions.WalAutoCheckpoint}'. Actual mode is '{walAutoCheckpoint}'.");
                }
            }

            if (initOptions.TempStore != default)
            {
                cmd.CommandText = $"PRAGMA temp_store = {(long?)initOptions.TempStore}";
                cmd.ExecuteNonQuery();

                cmd.CommandText = $"PRAGMA temp_store";
                long? tempStore = (long?)cmd.ExecuteScalar();
                if (tempStore == null || tempStore != (long?)initOptions.TempStore)
                {
                    throw new InvalidOperationException($"SQLiteXM connection failed. Unable to set PRAGMA temp_store to '{initOptions.TempStore}'. Actual mode is '{tempStore}'.");
                }
            }
        }
    }
}