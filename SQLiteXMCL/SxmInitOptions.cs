
using Microsoft.Data.Sqlite;
using SQLiteXM;

public delegate void ConnectionOpenedInteceptor(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection);
public delegate void ConnectionClosedInteceptor();

/// <summary>
/// Represents configuration options used when initializing a SQLiteXM database.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SxmInitOptions"/> allows callers to control database initialization
/// behavior without requiring direct interaction with low-level SQLite PRAGMA commands.
/// These settings are applied during <see cref="SxmInit.InitDbAsync(string, SxmInitOptions?, System.Threading.CancellationToken)"/>
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
public sealed class SxmInitOptions
{
    private bool _checkpointOnConnectionClose = false;
    public bool CheckpointOnConnectionClose { get => _checkpointOnConnectionClose; set { _checkpointOnConnectionClose = value; } }

    private long? _busyTimeout = null;
    public long? BusyTimeout { get => _busyTimeout; set { _busyTimeout = value; } }

    private long? _cacheSize = null;
    public long? CacheSize { get => _cacheSize; set { _cacheSize = value; } }

    private long? _walAutoCheckpoint = null;
    public long? WalAutoCheckpoint { get => _walAutoCheckpoint; set { _walAutoCheckpoint = value; } }

    private bool? _foreignKeys = null;
    public bool? ForeignKeys { get => _foreignKeys; set { _foreignKeys = value; } }

    private SxmTempStore? _tempStore = null;
    public SxmTempStore? TempStore { get => _tempStore; set { _tempStore = value; } }

    private List<ConnectionOpenedInteceptor>? ConnectionOpenedInteceptors { get; set; }
    private List<ConnectionClosedInteceptor>? ConnectionClosedInteceptors { get; set; }

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
    public SxmSynchronousMode? SynchronousModeOption { get => _synchronousMode; set { _synchronousMode = value; } }

    public void AddConnectionOpenedInteceptor(ConnectionOpenedInteceptor connectionOpenedInteceptor)
    {
        if (ConnectionOpenedInteceptors == default)
            ConnectionOpenedInteceptors = new();

        ConnectionOpenedInteceptors.Add(connectionOpenedInteceptor);
    }

    public void AddConnectionClosedInteceptor(ConnectionClosedInteceptor connectionClosedInteceptor)
    {
        if (ConnectionClosedInteceptors == default)
            ConnectionClosedInteceptors = new();

        ConnectionClosedInteceptors.Add(connectionClosedInteceptor);
    }

    /// <summary>
    /// Gets the SQLite PRAGMA string for journal_mode corresponding to <see cref="JournalModeOption"/>.
    /// </summary>
    public string JournalMode => JournalModeOption switch
    {
        SxmJournalMode.Wal => "WAL",
        SxmJournalMode.Delete => "DELETE",
        SxmJournalMode.Truncate => "TRUNCATE",
        SxmJournalMode.Persist => "PERSIST",
        SxmJournalMode.Memory => "MEMORY",
        SxmJournalMode.Off => "OFF",
        _ => throw new ArgumentOutOfRangeException(nameof(JournalModeOption), JournalModeOption, "Unsupported journal mode.")
    };

    internal static void ConnectionOpened(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection)
    {
        if (sqliteConnection == null || SxmInit.InitOptions == null)
            return;

        RunConnectionPragmas(sqliteConnection);

        if (SxmInit.InitOptions.ConnectionOpenedInteceptors != null)
        {
            foreach (ConnectionOpenedInteceptor connectionOpenedInteceptor in SxmInit.InitOptions.ConnectionOpenedInteceptors)
                connectionOpenedInteceptor(sqliteConnection);
        }
    }

    internal static void ConnectionClosed()
    {
        if (SxmInit.InitOptions == null)
            return;

        if (SxmInit.InitOptions.ConnectionClosedInteceptors != null)
        {
            foreach (ConnectionClosedInteceptor connectionClosedInteceptor in SxmInit.InitOptions.ConnectionClosedInteceptors)
                connectionClosedInteceptor();
        }
    }

    internal static void ConnectionClosing(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection)
    {
        if (sqliteConnection == null || SxmInit.InitOptions == null || SxmInit.InitOptions.CheckpointOnConnectionClose == false)
            return;

        using (var cmd = sqliteConnection.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA wal_checkpoint(PASSIVE)";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int busy = reader.GetInt32(0);
                int log = reader.GetInt32(1);
                int checkpointed = reader.GetInt32(2);
            }
        }
    }

    private static void RunConnectionPragmas(Microsoft.Data.Sqlite.SqliteConnection sqliteConnection)
    {
        SxmInitOptions initOptions = SxmInit.InitOptions!;

        // Execute PRAGMA synchronously (very quick) to avoid sync-over-async in ctor.
        using (var cmd = sqliteConnection.CreateCommand())
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
}
