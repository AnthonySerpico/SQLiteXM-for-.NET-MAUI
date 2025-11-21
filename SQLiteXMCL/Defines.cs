namespace SQLiteXM
{
    public enum ColumnType
    {
        Text, NVarChar, VarChar, Char, NChar, Int16, Int32, Int64, UInt16, UInt32,
        UInt64, Boolean, Single, Double, Decimal, Guid, DateTime, Date, Time, Binary, Blob, VarBinary
    };

    internal enum SxmEntityState
    {
        None, Insert, Update, Delete
    }


    public class Defines
	{
        public static readonly int ONE_MINUTE = 60000; // One Minutes in milliseconds.

		// Delimeters used for enclosing commands in the SqlStatemets properties file.
		internal static readonly char openStatementDelimeter = '[';
		internal static readonly char closeStatementDelimeter = ']';

		// Cloud synch status flags for tables.
		public static readonly int NO_CLOUD_SYNCH = 0;
		public static readonly int CLOUD_SYNCH = 1;
		public static readonly int CLOUD_MOVE = 2;

		// Transaction commit / rollback flags. 
		public static readonly bool commitTransaction = true;
		public static readonly bool rollbackTransaction = false;

		// Synchronization error types. 
		public enum SynchErrorTypes{
			success,
			exception,
			processing
		};

        public enum IndexType
        {
			standard,
            unique
        }

        // Database operation types. 
        public enum SqlStatementType{
            insert,
            delete,
            update,
			select,
			selectDirect,
            deleteDirect,
            updateDirect,
            unknown
        };

		public enum SqlStatementsFileType
		{
			txt,
			json,
			xml
		}

        // Error message defines.
        public enum SxmErrorCode{
		sqliteException,
		innerException,
		missingSQL,
		lockDB,
		dbDescriptorExists,
		noDBDescriptorExists,
		invalidTableName,
		noDatabaseExists,
		missingSQLStatementHeader,
		unknownSQLStatementHeader,
		invalidSQLStatementFile,
		unknownSynchCommand,
		invalidSQLStatementDefinition,
		noImplicitDBDescriptorExists,
		unknownErrorName,
		unknownSQLStatement,
		invalidDBName,
		userDefined,
		threadLockError,
		sxmSTransactionTimeout,
        dbVersionFormatError,
        missingDatabaseName
        };

		private Defines () {}
	}
}

