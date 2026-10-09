namespace SQLiteXM
{
    /// <summary>
    /// Local friendly enum — names mirror <c>LinqToDB.DataType</c> so we can map by name.
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// Default data type.
        /// </summary>
        Default,

        /// <summary>
        /// Text data type.
        /// </summary>
        Text,

        /// <summary>
        /// NVarchar data type.
        /// </summary>
        NVarChar,

        /// <summary>
        /// Varchar data type.
        /// </summary>
        VarChar,

        /// <summary>
        /// Char data type.
        /// </summary>
        Char,

        /// <summary>
        /// NChar data type.
        /// </summary>
        NChar,

        /// <summary>
        /// 16-bit signed integer data type.
        /// </summary>
        Int16,

        /// <summary>
        /// 32-bit signed integer data type.
        /// </summary>
        Int32,

        /// <summary>
        /// 64-bit signed integer data type.
        /// </summary>
        Int64,

        /// <summary>
        /// 16-bit unsigned integer data type.
        /// </summary>
        UInt16,

        /// <summary>
        /// 32-bit unsigned integer data type.
        /// </summary>
        UInt32,

        /// <summary>
        /// 64-bit unsigned integer data type.
        /// </summary>
        UInt64,

        /// <summary>
        /// Boolean data type.
        /// </summary>
        Boolean,

        /// <summary>
        /// Guid data type.
        /// </summary>
        Guid,

        /// <summary>
        /// Single-precision floating point data type.
        /// </summary>
        Single,

        /// <summary>
        /// Double-precision floating point data type.
        /// </summary>
        Double,

        /// <summary>
        /// Decimal data type.
        /// </summary>
        Decimal,

        /// <summary>
        /// DateTime data type.
        /// </summary>
        DateTime,

        /// <summary>
        /// Date-only data type.
        /// </summary>
        Date,

        /// <summary>
        /// Time-only data type.
        /// </summary>
        Time,

        /// <summary>
        /// Binary data type.
        /// </summary>
        Binary,

        /// <summary>
        /// Blob data type.
        /// </summary>
        Blob,

        /// <summary>
        /// VarBinary data type.
        /// </summary>
        VarBinary,

        /// <summary>
        /// Long data type.
        /// </summary>
        Long
    }

    /// <summary>
    /// Specifies the SQLite journal mode used to control transaction durability
    /// and concurrency behavior.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Journal modes correspond to SQLite PRAGMA <c>journal_mode</c> settings.
    /// These options influence how changes are written to disk and how concurrent
    /// access is handled.
    /// </para>
    /// <para>
    /// Most applications should leave <c>JournalModeOption</c> unset (<c>null</c>) and allow
    /// SQLiteXM to select an appropriate mode automatically.
    /// </para>
    /// </remarks>
    public enum SxmJournalMode
    {
        /// <summary>
        /// Uses the DELETE journal mode, where the rollback journal is deleted
        /// after each transaction completes.
        /// </summary>
        Delete,

        /// <summary>
        /// Uses the TRUNCATE journal mode, where the rollback journal is truncated
        /// instead of deleted after transactions.
        /// </summary>
        Truncate,

        /// <summary>
        /// Uses the PERSIST journal mode, which retains the journal file but resets
        /// its header for reuse.
        /// </summary>
        Persist,

        /// <summary>
        /// Uses the MEMORY journal mode, storing the rollback journal in memory.
        /// This improves performance but reduces durability.
        /// </summary>
        Memory,

        /// <summary>
        /// Uses Write-Ahead Logging (WAL) mode, enabling higher concurrency and
        /// improved write performance in many scenarios.
        /// </summary>
        Wal,

        /// <summary>
        /// Disables journaling. This provides maximum performance but significantly
        /// reduces data safety and should be used with caution.
        /// </summary>
        Off
    }

    /// <summary>
    /// Controls whether SQLiteXM performs a WAL (Write-Ahead Logging) checkpoint when a
    /// connection is closing, and which checkpoint strategy it uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A checkpoint transfers data from the write-ahead log back into the main database file.
    /// Without checkpointing, the WAL file grows indefinitely.
    /// </para>
    /// <para>
    /// This option only takes effect when <c>JournalModeOption</c> is
    /// <see cref="SxmJournalMode.Wal"/>. In any other journal mode the setting is ignored,
    /// and <c>SxmDatabaseOptionsValidator</c> raises a warning if it is configured anyway.
    /// </para>
    /// <para>
    /// Leaving <c>CheckPointConnection</c> unset (<c>null</c>) allows SQLite's own
    /// automatic checkpointing to apply. See also <c>WalAutoCheckpoint</c>.
    /// </para>
    /// </remarks>
    public enum CheckPointConnection
    {
        /// <summary>
        /// Performs no checkpoint when a connection closes. SQLite's automatic
        /// checkpointing still applies unless it has been separately disabled.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Performs a PASSIVE checkpoint each time a connection closes. A passive
        /// checkpoint transfers as much of the WAL as it can without blocking other
        /// readers or writers, and does not shrink the WAL file.
        /// </summary>
        OnConnectionClose,

        /// <summary>
        /// Performs a TRUNCATE checkpoint when a connection closes, but only if the WAL
        /// file has grown beyond <c>CheckPointWalMaxSize</c> (specified in KB). A truncate
        /// checkpoint resets the WAL file to zero length, reclaiming disk space.
        /// Requires <c>CheckPointWalMaxSize</c> to be set; otherwise no checkpoint occurs.
        /// </summary>
        MaxSize
    }

    /// <summary>
    /// Controls the SQLite <c>PRAGMA synchronous</c> setting, which determines how
    /// aggressively the database engine flushes written data to physical storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the central tradeoff between write speed and durability — that is, whether a
    /// transaction reported as committed is guaranteed to survive a power loss or operating
    /// system crash. Faster settings return from a commit before the data is known to be
    /// safely on disk.
    /// </para>
    /// <para>
    /// Enum values map directly to SQLite's documented <c>PRAGMA synchronous</c> values
    /// (0 through 3) and are applied during connection initialization.
    /// </para>
    /// <para>
    /// Most applications should leave <c>SynchronousModeOption</c> unset (<c>null</c>) and
    /// accept the SQLite default, which is <see cref="Full"/> for most journal modes and
    /// <see cref="Normal"/> under WAL.
    /// </para>
    /// </remarks>
    public enum SxmSynchronousMode
    {
        /// <summary>
        /// Writes are not synchronized to disk at all; the database hands data to the
        /// operating system and continues without waiting.
        /// This is the fastest setting and the least safe: a system crash or power loss can
        /// corrupt or lose committed data. Use only for data you can afford to rebuild, or
        /// where durability is guaranteed by some other mechanism.
        /// </summary>
        Off = 0,

        /// <summary>
        /// Synchronizes at the most critical moments only. Under WAL this is generally safe
        /// against application crashes and is the usual recommendation for WAL databases,
        /// though a power loss may still lose the most recent transactions.
        /// </summary>
        Normal,

        /// <summary>
        /// Synchronizes before each transaction is reported as committed, so a committed
        /// transaction survives a power loss. This is SQLite's default for most journal modes
        /// and the recommended balance of safety and performance.
        /// </summary>
        Full,

        /// <summary>
        /// As <see cref="Full"/>, but additionally synchronizes the journal's containing
        /// directory. This provides maximum durability at a significant cost to write
        /// performance. Prefer <see cref="Full"/> unless absolute durability is required.
        /// </summary>
        Extra
    }


    /// <summary>
    /// Controls the SQLite <c>PRAGMA temp_store</c> setting, which determines where
    /// temporary tables and indices created during query execution are held.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQLite creates temporary storage for operations such as sorting, grouping, and some
    /// subqueries. Holding these in memory is faster but increases the process's memory
    /// footprint, which matters on mobile devices where available memory is constrained.
    /// </para>
    /// <para>
    /// Enum values map directly to SQLite's documented <c>PRAGMA temp_store</c> values
    /// (0 through 2) and are applied during connection initialization.
    /// </para>
    /// </remarks>
    public enum SxmTempStore
    {
        /// <summary>
        /// Defers to the default chosen when the SQLite library was compiled.
        /// This is the behavior you get when <c>TempStore</c> is not explicitly configured.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Stores temporary tables and indices in a file on disk. This keeps memory usage
        /// low at the cost of slower temporary operations.
        /// </summary>
        File,

        /// <summary>
        /// Stores temporary tables and indices in memory. This speeds up sorting and
        /// grouping but increases memory consumption, which can matter for large result
        /// sets on memory-constrained devices.
        /// </summary>
        Memory
    }

    /// <summary>
    /// Internal entity state used for change tracking.
    /// </summary>
    internal enum SxmEntityState
    {
        /// <summary>
        /// No state.
        /// </summary>
        None,

        /// <summary>
        /// Entity is marked for insert.
        /// </summary>
        Insert,

        /// <summary>
        /// Entity is marked for update.
        /// </summary>
        Update,

        /// <summary>
        /// Entity is marked for delete.
        /// </summary>
        Delete
    }


    /// <summary>
    /// Project-wide constant and helper definitions.
    /// </summary>
    public static class SxmDefines
    {
        /// <summary>
        /// Delimiter used to open a statement in SQL statements properties files.
        /// </summary>
        internal static readonly char OpenStatementDelimeter = '[';

        /// <summary>
        /// Delimiter used to close a statement in SQL statements properties files.
        /// </summary>
        internal static readonly char CloseStatementDelimeter = ']';

        /// <summary>
        /// Transaction commit flag.
        /// </summary>
        internal static readonly bool CommitTransaction = true;

        /// <summary>
        /// Transaction rollback flag.
        /// </summary>
        internal static readonly bool RollbackTransaction = false;

        /// <summary>
        /// Cloud synchronization flag indicating no cloud synchronization.
        /// </summary>
        public static readonly int NoCloudSync = 0;

        /// <summary>
        /// Cloud synchronization flag indicating cloud synchronization is enabled.
        /// </summary>
        public static readonly int CloudSync = 1;

        /// <summary>
        /// Cloud synchronization flag indicating a cloud move operation.
        /// </summary>
        public static readonly int CloudMove = 2;

        /// <summary>
        /// Types of synchronization errors.
        /// </summary>
        internal enum SynchErrorTypes
        {
            /// <summary>
            /// Synchronization succeeded.
            /// </summary>
            Success,

            /// <summary>
            /// An exception occurred during synchronization.
            /// </summary>
            Exception,

            /// <summary>
            /// A processing error occurred during synchronization.
            /// </summary>
            Processing
        };

        /// <summary>
        /// Index types for database indexes.
        /// </summary>
        internal enum IndexType
        {
            /// <summary>
            /// Standard (non-unique) index.
            /// </summary>
            Standard,

            /// <summary>
            /// Unique index.
            /// </summary>
            Unique
        }

        /// <summary>
        /// Database operation types.
        /// </summary>
        internal enum SqlStatementType
        {
            /// <summary>
            /// Insert statement.
            /// </summary>
            Insert,

            /// <summary>
            /// Delete statement.
            /// </summary>
            Delete,

            /// <summary>
            /// Update statement.
            /// </summary>
            Update,

            /// <summary>
            /// Select statement.
            /// </summary>
            Select,

            /// <summary>
            /// Direct insert statement.
            /// </summary>
            InsertDirect,

            /// <summary>
            /// Direct select statement.
            /// </summary>
            SelectDirect,

            /// <summary>
            /// Direct delete statement.
            /// </summary>
            DeleteDirect,

            /// <summary>
            /// Direct update statement.
            /// </summary>
            UpdateDirect,

            /// <summary>
            /// Unknown statement type.
            /// </summary>
            Unknown
        };

        /// <summary>
        /// Supported SQL statements file formats.
        /// </summary>
        internal enum SqlStatementsFileType
        {
            /// <summary>
            /// Plain text file.
            /// </summary>
            Unknown,

            /// <summary>
            /// JSON file.
            /// </summary>
            Json,

            /// <summary>
            /// XML file.
            /// </summary>
            Xml
        }

        /// <summary>
        /// Error codes used across the library.
        /// </summary>
        public enum SxmErrorCode
        {
            /// <summary>
            /// Missing SQL.
            /// </summary>
            MissingSQL,

            /// <summary>
            /// Database is locked.
            /// </summary>
            LockDb,

            /// <summary>
            /// No database exists.
            /// </summary>
            NoDatabaseExists,

            /// <summary>
            /// Unknown synchronization command.
            /// </summary>
            UnknownSynchCommand,

            /// <summary>
            /// Unknown SQL statement.
            /// </summary>
            UnknownSqlStatement,

            /// <summary>
            /// Invalid database name.
            /// </summary>
            InvalidDBName,

            /// <summary>
            /// Database version format error.
            /// </summary>
            DbVersionFormatError,

            /// <summary>
            /// Cannot acquire lease on shared connection.
            /// </summary>
            AcquireLease,

            /// <summary>
            /// Connection creation blocked: application is backgrounded.
            /// </summary>
            ConnectionBlockedBackgrounded,

            /// <summary>
            /// The table cannot be dropped because other tables reference it with foreign keys.
            /// </summary>
            TableHasDependents,

            /// <summary>
            /// A connection could not be created, released, or committed. Inspect the inner exception for the root cause.
            /// </summary>
            ConnectionFailure,

            /// <summary>
            /// An unexpected failure occurred while acquiring or releasing a connection lock.
            /// Inspect the inner exception for the root cause.
            /// </summary>
            LockFailure,

            /// <summary>
            /// A query or statement failed to execute. Inspect the inner exception for the root cause.
            /// </summary>
            QueryFailure,

            /// <summary>
            /// A schema operation failed, such as building the schema, creating a table, or reading the database version.
            /// Inspect the inner exception for the root cause.
            /// </summary>
            SchemaFailure,

            /// <summary>
            /// A database value could not be read or converted to the expected type. This usually indicates a
            /// mismatch between the entity model and the stored schema. Inspect the inner exception for the root cause.
            /// </summary>
            DataConversionFailure,

            /// <summary>
            /// An entity, column, or association could not be mapped. Inspect the inner exception for the root cause.
            /// </summary>
            MappingFailure,

            /// <summary>
            /// A transaction could not be created or finalized. Inspect the inner exception for the root cause.
            /// </summary>
            TransactionFailure
        };
    }
}