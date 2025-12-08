using System.Collections;
using System.Data.Common;
using System.Data.Common;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

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
        // true => connection is shared / reused across callers
        // false => connection is non-shared / private to the creator
        private bool shared;
        /// <summary>
        /// Preferred property name: use Shared (true == shared/reused).
        /// </summary>
        public bool Shared => shared;

        private string? databaseName;
        public string? DatabaseName
        {
            get { return databaseName; }
        }
        private DbCommand? connCommand;
        private DbDataReader? connDataReader;
        private Microsoft.Data.Sqlite.SqliteConnection? dbConn;
        private Microsoft.Data.Sqlite.SqliteTransaction? dbConnTransaction;

        private static readonly object synchLock = new object();
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

        private static Dictionary<string, string> dbConnectionString = new Dictionary<string, string>();
        private static readonly string SQLiteConnString = "Data Source={0}; Mode=ReadWriteCreate;";

        static private string? implicitDatabaseName;
        static internal string? ImplicitDatabaseName
        {
            get => implicitDatabaseName;
        }
        private enum DbParametersDataType { list, tupleList, twoDArray, oneDArray, hashTable, dictionary }

        public SxmConnection(string? databaseName, bool shared = true)
        {
            try
            {
                this.databaseName = databaseName;
                this.shared = shared;

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
                dbConn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                dbConn.Open();

                // execute PRAGMA using async ADO but block here because we're in ctor
                // This is initialization; prefer to run the async call and block synchronously once.
                this.executeQueryAsync("PRAGMA foreign_keys = ON", default).GetAwaiter().GetResult();
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

        public async Task<bool> LockAsync(int millisecondsTimeout = 100, CancellationToken ct = default)
        {
            try
            {
                if (dbConn == null) return false;
                if (await _asyncLock.WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), ct).ConfigureAwait(false))
                {
                    if (dbConn.State == System.Data.ConnectionState.Broken)
                    {
                        dbConn.Close();
                        dbConn.Open();
                    }
                    return true;
                }
            }
            catch (OperationCanceledException) { }
            return false;
        }

        public void ReleaseLock()
        {
            try { _asyncLock.Release(); } catch { }
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

        // Returns error code for SqliteException, otherwise throw the exception.
        public SQLiteErrorCode finishTransaction(bool commitFlag)
        {
            // synchronous wrapper for convenience / compatibility: call async implementation and block.
            return finishTransactionAsync(commitFlag).GetAwaiter().GetResult();
        }

        // No-throw guarantee. Makes every effort to perform clean-up.
        public void releaseConnection(bool destroy = false)
        {
            if (dbConn != null)
            {
                try
                {
                    if (dbConnTransaction != null)
                        // ensure rollback is completed; block here to preserve previous behavior
                        doCommitAsync(SQLiteXM.Defines.rollbackTransaction).GetAwaiter().GetResult();
                }
#pragma warning disable 0168
                catch (System.Exception notUsed) { } // Within a handled exception a finally is guaranteed to run. 
#pragma warning restore 0168        // https://msdn.microsoft.com/en-us/library/zwc8s4fz.aspx   
                finally
                {
                    try
                    {
                        if (!shared || destroy == true)
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

        // Async implementation of commit/rollback
        public async Task<SQLiteErrorCode> finishTransactionAsync(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (dbConn != null && dbConnTransaction != null)
                sqLiteErrorCode = await doCommitAsync(commitFlag).ConfigureAwait(false);

            return sqLiteErrorCode;
        }

        // Async doCommit using async ADO APIs
        private async Task<SQLiteErrorCode> doCommitAsync(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (dbConnTransaction != null)
            {
                try
                {
                    if (commitFlag == SQLiteXM.Defines.commitTransaction)
                        await dbConnTransaction.CommitAsync().ConfigureAwait(false);
                    else
                        await dbConnTransaction.RollbackAsync().ConfigureAwait(false);

                    dbConnTransaction = default(Microsoft.Data.Sqlite.SqliteTransaction);
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
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

        // Keep old synchronous doCommit for compatibility (rarely used directly)
        private SQLiteErrorCode doCommit(bool commitFlag)
        {
            return doCommitAsync(commitFlag).GetAwaiter().GetResult();
        }

        public void destroyConnection()
        {
            if (dbConn != null)
            {
                releaseConnectionResources();

                dbConn.Close();
                dbConn.Dispose();
                dbConn = default(Microsoft.Data.Sqlite.SqliteConnection);
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

        // Async ExecuteReader
        public async Task executeQueryAsync(string command, List<object>? parameterValues, CancellationToken cancellationToken = default)
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

                if (connCommand is DbCommand dbCmd)
                {
                    connDataReader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback to sync if something unexpected: keep behavior but log
                    connDataReader = connCommand.ExecuteReader();
                }
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

        // Synchronous wrapper for compatibility
        public void executeQuery(string command, List<object>? parameterValues)
        {
            executeQueryAsync(command, parameterValues).GetAwaiter().GetResult();
        }

        // Async ExecuteNonQuery
        public async Task executeNonQueryAsync(string command, List<object>? parameterValues, CancellationToken cancellationToken = default)
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

                if (connCommand is DbCommand dbCmd)
                {
                    await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Fallback synchronous
                    connCommand.ExecuteNonQuery();
                }
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

        // Synchronous wrapper for compatibility
        public void executeNonQuery(string command, List<object>? parameterValues)
        {
            executeNonQueryAsync(command, parameterValues).GetAwaiter().GetResult();
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

                            dbParameter.ParameterName = "@" + kvp.Key;
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

        // Begin transaction (kept synchronous; underlying provider does not provide strong async benefit here)
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
            catch (Microsoft.Data.Sqlite.SqliteException ex)
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