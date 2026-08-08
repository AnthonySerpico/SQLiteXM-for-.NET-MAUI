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
    /// Most applications should use <see cref="Default"/> and allow SQLiteXM to
    /// select an appropriate mode automatically.
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

    public enum CheckPointConnection
    {
        Off = 0,

        OnConnectionClose,

        MaxSize
    }

    public enum SxmSynchronousMode
    {
        Off = 0,

        Normal,

        Full,

        Extra
    }


    public enum SxmTempStore
    {
        Default = 0,

        File,

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
            /// SQLite exception occurred.
            /// </summary>
            SqliteException,

            /// <summary>
            /// Inner exception occurred.
            /// </summary>
            InnerException,

            /// <summary>
            /// Missing SQL.
            /// </summary>
            MissingSQL,

            /// <summary>
            /// Database is locked.
            /// </summary>
            LockDb,

            /// <summary>
            /// Database descriptor already exists.
            /// </summary>
            DbDescriptorExists,

            /// <summary>
            /// No database descriptor exists.
            /// </summary>
            NoDbDescriptorExists,

            /// <summary>
            /// Invalid table name.
            /// </summary>
            InvalidTableName,

            /// <summary>
            /// No database exists.
            /// </summary>
            NoDatabaseExists,

            /// <summary>
            /// Missing SQL statement header.
            /// </summary>
            MissingSQLStatementHeader,

            /// <summary>
            /// Unknown SQL statement header.
            /// </summary>
            UnknownSqlStatementHeader,

            /// <summary>
            /// Invalid SQL statement file.
            /// </summary>
            InvalidSqlStatementFile,

            /// <summary>
            /// Unknown synchronization command.
            /// </summary>
            UnknownSynchCommand,

            /// <summary>
            /// Invalid SQL statement definition.
            /// </summary>
            InvalidSqlStatementDefinition,

            /// <summary>
            /// No implicit database descriptor exists.
            /// </summary>
            NoImplicitDbDescriptorExists,

            /// <summary>
            /// Unknown error name.
            /// </summary>
            UnknownErrorName,

            /// <summary>
            /// Unknown SQL statement.
            /// </summary>
            UnknownSqlStatement,

            /// <summary>
            /// Invalid database name.
            /// </summary>
            InvalidDBName,

            /// <summary>
            /// User defined error.
            /// </summary>
            UserDefined,

            /// <summary>
            /// Thread lock error.
            /// </summary>
            ThreadLockError,

            /// <summary>
            /// Transaction timeout occurred.
            /// </summary>
            SxmSTransactionTimeout,

            /// <summary>
            /// Database version format error.
            /// </summary>
            DbVersionFormatError,

            /// <summary>
            /// Missing database name.
            /// </summary>
            MissingDatabaseName,

            /// <summary>
            /// Cannot acquire lease on shared connection.
            /// </summary>
            AcquireLease,

            /// <summary>
            /// Connection creation blocked: application is backgrounded.
            /// </summary>
            ConnectionBlockedBackgrounded
        };
    }
}