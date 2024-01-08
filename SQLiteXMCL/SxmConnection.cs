using System.Data.Common;
using System.Collections;

namespace SQLiteXM
{
    public enum SQLiteErrorCode
    {
        Ok = 0,
        Error = 1,
        Internal = 2,
        Perm = 3,
        Abort = 4,
        Busy = 5,
        Locked = 6,
        NoMem = 7,
        ReadOnly = 8,
        Interrupt = 9,
        IOErr = 10,
        Corrupt = 11,
        NotFound = 12,
        Full = 13,
        CantOpen = 14,
        Protocol = 0xF,
        Empty = 0x10,
        Schema = 17,
        TooBig = 18,
        Constraint = 19,
        Mismatch = 20,
        Misuse = 21,
        NOLFS = 22,
        Auth = 23,
        Format = 24,
        Range = 25,
        NotADatabase = 26,
        Row = 100,
        Done = 101
    }

    public class SxmConnection
    {
        private bool transient;
        public bool Transient
        {
            get { return transient; }
        }
        private string? databaseName;
        public string? DatabaseName
        {
            get { return databaseName; }
        }
        private static readonly object synchLock = new object();
        private DbCommand? connCommand;
        private System.Data.SQLite.SQLiteConnection? dbConn;
        private DbDataReader? connDataReader;
        private System.Data.SQLite.SQLiteTransaction? dbConnTransaction;

        private static Dictionary<string, string> dbConnectionString = new Dictionary<string, string>();
        private static readonly string SQLiteConnString = "Data Source={0}; DateTimeFormat = Ticks; Read Only=False;";

        static private string? implicitDatabaseName;
        static internal string? ImplicitDatabaseName
        {
            get => implicitDatabaseName;
        }
        private enum DbParametersDataType { list, tupleList, twoDArray, oneDArray, hashTable, dictionary }

        public SxmConnection(string? databaseName, bool transient = false)
        {
            try
            {
                this.databaseName = databaseName;
                this.transient = transient;

                createNewConnection();
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }
        }

        internal void log(System.Exception ex, string? method)
        {
            if (this.databaseName != default(string))
                Logging.log(this.databaseName, ex, method);
        }

        private void createNewConnection()
        {
            try
            {
                string? connectionString = SxmConnection.getConnectionString(ref this.databaseName);
                dbConn = new System.Data.SQLite.SQLiteConnection(connectionString);
                dbConn.Open();

                this.executeQuery("PRAGMA foreign_keys = ON", default(List<object>));
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                destroyConnection();
                log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                throw new SxmException(ex);
            }
        }

        internal static string getConnectionString(ref string? databaseName)
        {
            string? connectionString = default(string);

            lock (synchLock)
            {
                databaseName = SxmConnection.resolveDatabaseName(databaseName);
                if (!dbConnectionString.TryGetValue(databaseName, out connectionString))
                {
                    DatabaseDescriptor? databaseDescriptor = DatabaseDescriptor.getDescriptor(databaseName!);
                    if (databaseDescriptor == null)
                        throw new SxmException(new ErrorMessage("noDBDescriptorExists", databaseName!));

                    string databaseFolderPath = Environment.GetFolderPath(databaseDescriptor.DatabaseFolder);
                    string pathToDatabase = Path.Combine(databaseFolderPath, databaseName);
                    connectionString = String.Format(SQLiteConnString, pathToDatabase);

                    dbConnectionString.Add(databaseName, connectionString);
                }
            }

            return connectionString;
        }

        private static string resolveDatabaseName(string? databaseName)
        {
            if (databaseName == null)
            {
                if (SxmConnection.implicitDatabaseName == null)
                {
                    ArrayList dbNames = DatabaseDescriptor.getDatabaseNames();
                    if (dbNames.Count != 1) // There must be only one descriptor in order to use implicit database naming.
                        throw new SxmException(ErrorMessages.error["noImplicitDBDescriptorExists"]);
                    else
                        SxmConnection.implicitDatabaseName = dbNames[0] as string;
                }

                databaseName = SxmConnection.implicitDatabaseName;
            }

            return databaseName!;
        }

        public bool lockConnection(int wait = 100)
        {
            if (dbConn != null)
                if (Monitor.TryEnter(dbConn, wait) == true)
                {
                    if (dbConn.State == System.Data.ConnectionState.Broken)
                    {
                        dbConn.Close();
                        dbConn.Open();
                    }

                    return true;
                }

            return false;
        }

        // Returns error code for SqliteException, otherwise throw the exception.
        public SQLiteErrorCode finishTransaction(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (dbConn != null)
                if (dbConnTransaction != null)
                    sqLiteErrorCode = doCommit(commitFlag);

            return sqLiteErrorCode;
        }

        // No-throw guarantee. Makes every effort to perform clean-up.
        public void releaseConnection(bool destroy = false)
        {
            if (dbConn != null)
            {
                try
                {
                    if (dbConnTransaction != null)
                        doCommit(SQLiteXM.Defines.rollbackTransaction);
                }
#pragma warning disable 0168
                catch (System.Exception notUsed) { } // Within a handled exception a finally is guaranteed to run. 
#pragma warning restore 0168        // https://msdn.microsoft.com/en-us/library/zwc8s4fz.aspx   
                finally
                {
                    try
                    {
                        if (Monitor.IsEntered(dbConn) == true)
                            Monitor.Exit(dbConn);

                        if (transient == true || destroy == true)
                            destroyConnection();
                        else
                            releaseConnectionResources();
                    }
#pragma warning disable 0168
                    catch (System.Exception notUsed) { }
#pragma warning restore 0168
                }
            }
        }

        // Returns error code for SqliteException, otherwise throw the exception.
        private SQLiteErrorCode doCommit(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (dbConnTransaction != null)
            {
                try
                {
                    if (commitFlag == SQLiteXM.Defines.commitTransaction)
                        dbConnTransaction.Commit();
                    else
                        dbConnTransaction.Rollback();

                    dbConnTransaction = default(System.Data.SQLite.SQLiteTransaction);
                }
                catch (System.Data.SQLite.SQLiteException ex)
                {
                    log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                    //if (ex.ErrorCode == SQLiteErrorCode.Busy) {/* May do something here.*/}

                    if (commitFlag == SQLiteXM.Defines.commitTransaction)
                        sqLiteErrorCode = (SQLiteErrorCode)ex.ErrorCode;
                    else
                        throw new SxmException(ex);
                }
                catch (System.Exception ex)
                {
                    log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                    throw new SxmException(ex);
                }
            }

            return sqLiteErrorCode;
        }

        public void destroyConnection()
        {
            if (dbConn != null)
            {
                releaseConnectionResources();

                dbConn.Close();
                dbConn.Dispose();
                dbConn = default(System.Data.SQLite.SQLiteConnection);
            }
        }

        private void releaseConnectionResources()
        {
            if (connCommand != null)
            {
                releaseDataReader();
                connCommand.Dispose();
                connCommand = default(DbCommand);
            }
        }

        private void releaseDataReader()
        {
            if (connDataReader != null && connDataReader.IsClosed == false)
            {
                connDataReader.Close();
                connDataReader = default(DbDataReader);
            }
        }
        public void executeQuery(string command, List<object>? parameterValues)
        {
            if (string.IsNullOrEmpty(command))
                throw new SxmException(ErrorMessages.error["missingSQL"]);

            try
            {
                if (connCommand == null)
                    connCommand = dbConn.CreateCommand();
                else
                    releaseDataReader();

                connCommand.CommandText = command;
                connCommand.CommandType = System.Data.CommandType.Text;
                addCommandParameters(parameterValues);
                connDataReader = connCommand.ExecuteReader();
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                throw new SxmException(ex);
            }
        }

        public void executeNonQuery(string command, List<object>? parameterValues)
        {
            if (string.IsNullOrEmpty(command))
                throw new SxmException(ErrorMessages.error["missingSQL"]);

            try
            {
                if (connCommand == null)
                    connCommand = dbConn.CreateCommand();
                else
                    if (command.StartsWith("DELETE FROM companyReg WHERE companyRegPK") == false)
                    releaseDataReader();

                connCommand.CommandText = command;
                connCommand.CommandType = System.Data.CommandType.Text;
                addCommandParameters(parameterValues);
                connCommand.ExecuteNonQuery();
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                throw new SxmException(ex);
            }
        }

        private void addCommandParameters(List<object>? parameterValues)
        {
            connCommand.Parameters.Clear();

            if (parameterValues != null)
            {
                DbParametersDataType dbParametersDataType = getDbParameterType(ref parameterValues);

                if (dbParametersDataType == DbParametersDataType.dictionary)
                {
                    Dictionary<string, object>? dict = (Dictionary<string, object>?)parameterValues[0];
                    if (dict != default)
                    {
                        foreach (KeyValuePair<string, object> kvp in dict)
                        {
                            DbParameter dbParameter = connCommand.CreateParameter();

                            dbParameter.ParameterName = kvp.Key;
                            dbParameter.Value = kvp.Value;

                            connCommand.Parameters.Add(dbParameter);
                        }
                    }

                    return;
                }

                if (dbParametersDataType == DbParametersDataType.list)
                {
                    int cntr = 0;

                    foreach (Object parameterValue in parameterValues)
                    {
                        DbParameter dbParameter = connCommand.CreateParameter();

                        dbParameter.Value = parameterValue;
                        dbParameter.ParameterName = "@p" + cntr.ToString();

                        connCommand.Parameters.Add(dbParameter);

                        ++cntr;
                    }
                }
            }
        }

        private DbParametersDataType getDbParameterType(ref List<object> parameterValues)
        {
            Type? pvt = parameterValues[0]?.GetType();

            if (pvt == typeof(Tuple<string, object>))
                return DbParametersDataType.tupleList;

            if (pvt == typeof(object[]))
                return DbParametersDataType.oneDArray;

            if (pvt == typeof(object[,]))
                return DbParametersDataType.twoDArray;

            if (pvt == typeof(Hashtable))
                return DbParametersDataType.hashTable;

            if (pvt == typeof(Dictionary<string, object>))
                return DbParametersDataType.dictionary;

            return DbParametersDataType.list;
        }

        public void beginTransaction()
        {
            try
            {
                if (dbConnTransaction == null)
                {
                    dbConnTransaction = dbConn.BeginTransaction();
                    if (connCommand == null)
                        connCommand = dbConn.CreateCommand();
                    connCommand.Transaction = dbConnTransaction;
                }

            }
            catch (System.Data.SQLite.SQLiteException ex)
            {
                if (ex.ErrorCode == (int)SQLiteErrorCode.Busy)
                {
                    log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                }
                throw new SxmException(ex);
            }
        }

        public bool hasRows()
        {
            if (connDataReader != null)
                return connDataReader.HasRows;

            return false;
        }

        public object? getValue(string fieldName)
        {
            try
            {
                if (hasRows() == true)
                {
                    int ordinal = connDataReader.GetOrdinal(fieldName);
                    if (ordinal != -1)
                        return connDataReader.GetValue(ordinal);
                }
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }

            return default;
        }

        public object? getValue(int fieldOrdinal)
        {
            try
            {
                if (hasRows() == true)
                    return connDataReader.GetValue(fieldOrdinal);
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }

            return default;
        }

        public string? getFieldName(int fieldOrdinal)
        {
            try
            {
                if (hasRows() == true)
                    return connDataReader.GetName(fieldOrdinal);
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }

            return default;
        }

        public string[] getFieldNames()
        {
            string[] fieldNames;

            if (hasRows() == true)
            {
                fieldNames = new string[connDataReader.FieldCount];
                for (int i = 0; i < connDataReader.FieldCount; i++)
                    fieldNames[i] = connDataReader.GetName(i);
            }
            else
                fieldNames = new string[0];

            return fieldNames;
        }
        public T? getNextRow<T>() where T : IDictionary<string, object?>, new()
        {
            T? row = default(T);

            if (nextRow() == true)
            {
                row = new T();
                int numColumns = getColumnCount();
                for (int i = 0; i < numColumns; i++)
                {
                    object columnValue = connDataReader.GetValue(i);
                    //Type type = columnValue.GetType();
                    row.Add(connDataReader.GetName(i), columnValue == DBNull.Value ? default : columnValue);
                }
            }

            return row;
        }

        public int getColumnCount()
        {
            if (hasRows() == true)
                return connDataReader.FieldCount;

            return 0;
        }

        public bool nextRow()
        {
            bool anotherRow = false;

            if (hasRows() == true)
            {
                anotherRow = connDataReader.Read();
                if (anotherRow == false)
                    releaseDataReader();
            }

            return anotherRow;
        }

        public Type? getType(string fieldName)
        {
            try
            {
                if (hasRows() == true)
                {
                    int ordinal = connDataReader.GetOrdinal(fieldName);
                    return connDataReader.GetFieldType(ordinal);
                }
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }

            return default;
        }
    }
}

