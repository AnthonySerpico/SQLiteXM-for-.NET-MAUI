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
        /// One minute in milliseconds.
        /// </summary>
        public static readonly int ONE_MINUTE = 60000; // One Minutes in milliseconds.

        /// <summary>
        /// Delimiter used to open a statement in SQL statements properties files.
        /// </summary>
        internal static readonly char openStatementDelimeter = '[';

        /// <summary>
        /// Delimiter used to close a statement in SQL statements properties files.
        /// </summary>
        internal static readonly char closeStatementDelimeter = ']';

        /// <summary>
        /// Cloud synchronization flag indicating no cloud synchronization.
        /// </summary>
        public static readonly int NO_CLOUD_SYNCH = 0;

        /// <summary>
        /// Cloud synchronization flag indicating cloud synchronization is enabled.
        /// </summary>
        public static readonly int CLOUD_SYNCH = 1;

        /// <summary>
        /// Cloud synchronization flag indicating a cloud move operation.
        /// </summary>
        public static readonly int CLOUD_MOVE = 2;

        /// <summary>
        /// Transaction commit flag.
        /// </summary>
        public static readonly bool commitTransaction = true;

        /// <summary>
        /// Transaction rollback flag.
        /// </summary>
        public static readonly bool rollbackTransaction = false;

        /// <summary>
        /// Types of synchronization errors.
        /// </summary>
        public enum SynchErrorTypes
        {
            /// <summary>
            /// Synchronization succeeded.
            /// </summary>
            success,

            /// <summary>
            /// An exception occurred during synchronization.
            /// </summary>
            exception,

            /// <summary>
            /// A processing error occurred during synchronization.
            /// </summary>
            processing
        };

        /// <summary>
        /// Index types for database indexes.
        /// </summary>
        public enum IndexType
        {
            /// <summary>
            /// Standard (non-unique) index.
            /// </summary>
            standard,

            /// <summary>
            /// Unique index.
            /// </summary>
            unique
        }

        /// <summary>
        /// Database operation types.
        /// </summary>
        public enum SqlStatementType
        {
            /// <summary>
            /// Insert statement.
            /// </summary>
            insert,

            /// <summary>
            /// Delete statement.
            /// </summary>
            delete,

            /// <summary>
            /// Update statement.
            /// </summary>
            update,

            /// <summary>
            /// Select statement.
            /// </summary>
            select,

            /// <summary>
            /// Direct insert statement.
            /// </summary>
            insertDirect,

            /// <summary>
            /// Direct select statement.
            /// </summary>
            selectDirect,

            /// <summary>
            /// Direct delete statement.
            /// </summary>
            deleteDirect,

            /// <summary>
            /// Direct update statement.
            /// </summary>
            updateDirect,

            /// <summary>
            /// Unknown statement type.
            /// </summary>
            unknown
        };

        /// <summary>
        /// Supported SQL statements file formats.
        /// </summary>
        public enum SqlStatementsFileType
        {
            /// <summary>
            /// Plain text file.
            /// </summary>
            unknown,

            /// <summary>
            /// JSON file.
            /// </summary>
            json,

            /// <summary>
            /// XML file.
            /// </summary>
            xml
        }

        /// <summary>
        /// Error codes used across the library.
        /// </summary>
        public enum SxmErrorCode
        {
            /// <summary>
            /// SQLite exception occurred.
            /// </summary>
            sqliteException,

            /// <summary>
            /// Inner exception occurred.
            /// </summary>
            innerException,

            /// <summary>
            /// Missing SQL.
            /// </summary>
            missingSQL,

            /// <summary>
            /// Database is locked.
            /// </summary>
            lockDB,

            /// <summary>
            /// Database descriptor already exists.
            /// </summary>
            dbDescriptorExists,

            /// <summary>
            /// No database descriptor exists.
            /// </summary>
            noDBDescriptorExists,

            /// <summary>
            /// Invalid table name.
            /// </summary>
            invalidTableName,

            /// <summary>
            /// No database exists.
            /// </summary>
            noDatabaseExists,

            /// <summary>
            /// Missing SQL statement header.
            /// </summary>
            missingSQLStatementHeader,

            /// <summary>
            /// Unknown SQL statement header.
            /// </summary>
            unknownSQLStatementHeader,

            /// <summary>
            /// Invalid SQL statement file.
            /// </summary>
            invalidSQLStatementFile,

            /// <summary>
            /// Unknown synchronization command.
            /// </summary>
            unknownSynchCommand,

            /// <summary>
            /// Invalid SQL statement definition.
            /// </summary>
            invalidSQLStatementDefinition,

            /// <summary>
            /// No implicit database descriptor exists.
            /// </summary>
            noImplicitDBDescriptorExists,

            /// <summary>
            /// Unknown error name.
            /// </summary>
            unknownErrorName,

            /// <summary>
            /// Unknown SQL statement.
            /// </summary>
            unknownSQLStatement,

            /// <summary>
            /// Invalid database name.
            /// </summary>
            invalidDBName,

            /// <summary>
            /// User defined error.
            /// </summary>
            userDefined,

            /// <summary>
            /// Thread lock error.
            /// </summary>
            threadLockError,

            /// <summary>
            /// Transaction timeout occurred.
            /// </summary>
            sxmSTransactionTimeout,

            /// <summary>
            /// Database version format error.
            /// </summary>
            dbVersionFormatError,

            /// <summary>
            /// Missing database name.
            /// </summary>
            missingDatabaseName
        };

        /// <summary>
        /// Prevents instantiation of the <see cref="SxmDefines"/> class.
        /// </summary>
        private SxmDefines() { }
    }
}