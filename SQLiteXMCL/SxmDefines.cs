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
    /// Column type enumeration used by the library.
    /// </summary>
    public enum ColumnType
    {
        /// <summary>
        /// No column type specified.
        /// </summary>
        None,

        /// <summary>
        /// Text column type.
        /// </summary>
        Text,

        /// <summary>
        /// NVarchar column type.
        /// </summary>
        NVarChar,

        /// <summary>
        /// Varchar column type.
        /// </summary>
        VarChar,

        /// <summary>
        /// Char column type.
        /// </summary>
        Char,

        /// <summary>
        /// NChar column type.
        /// </summary>
        NChar,

        /// <summary>
        /// 16-bit signed integer column type.
        /// </summary>
        Int16,

        /// <summary>
        /// 32-bit signed integer column type.
        /// </summary>
        Int32,

        /// <summary>
        /// 64-bit signed integer column type.
        /// </summary>
        Int64,

        /// <summary>
        /// 16-bit unsigned integer column type.
        /// </summary>
        UInt16,

        /// <summary>
        /// 32-bit unsigned integer column type.
        /// </summary>
        UInt32,

        /// <summary>
        /// 64-bit unsigned integer column type.
        /// </summary>
        UInt64,

        /// <summary>
        /// Boolean column type.
        /// </summary>
        Boolean,

        /// <summary>
        /// Single-precision floating point column type.
        /// </summary>
        Single,

        /// <summary>
        /// Double-precision floating point column type.
        /// </summary>
        Double,

        /// <summary>
        /// Decimal column type.
        /// </summary>
        Decimal,

        /// <summary>
        /// Guid column type.
        /// </summary>
        Guid,

        /// <summary>
        /// DateTime column type.
        /// </summary>
        DateTime,

        /// <summary>
        /// Date-only column type.
        /// </summary>
        Date,

        /// <summary>
        /// Time-only column type.
        /// </summary>
        Time,

        /// <summary>
        /// Binary column type.
        /// </summary>
        Binary,

        /// <summary>
        /// Blob column type.
        /// </summary>
        Blob,

        /// <summary>
        /// VarBinary column type.
        /// </summary>
        VarBinary
    };

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
    public class SxmDefines
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
            MissingDatabaseName
        };

        /// <summary>
        /// Prevents instantiation of the <see cref="SxmDefines"/> class.
        /// </summary>
        private SxmDefines() { }
    }
}