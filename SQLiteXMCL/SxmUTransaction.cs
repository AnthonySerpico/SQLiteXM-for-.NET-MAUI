using SQLiteXM;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    public class SxmUTransaction : IDisposable, IAsyncDisposable
    {
        private bool interruptSynchronize = false;
        private SxmConnection? connection;
        private bool disposed = false;
        private bool ownsAsyncLock = false;

        public SxmConnection? Connection { get => connection; }

        // Private ctor used by the async factory. Connection lock already acquired.
        protected SxmUTransaction(SxmConnection conn, bool ownsLock)
        {
            this.connection = conn;
            this.ownsAsyncLock = ownsLock;
        }

        // factory: create a private (non-shared) connection (if dbName provided) and acquire async lock without blocking the calling thread.
        public static SxmUTransaction Create(string? databaseName = null)
        {
            SxmConnection conn = new SxmConnection(databaseName, shared: false);
            return new SxmUTransaction(conn, ownsLock: false);
        }

        // Async factory overload when caller already has connection.
        public static async Task<SxmUTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            bool ownsLock = false;
            // Only attempt lock when the supplied connection is shared.
            if (conn.Shared)
            {
                bool locked = await conn.LockAsync(waitMilliseconds, cancellationToken).ConfigureAwait(false);
                if (!locked)
                {
                    throw new SxmException(new ErrorMessage("lockDB", conn.DatabaseName));
                }
                ownsLock = true;
            }

            return new SxmUTransaction(conn, ownsLock: ownsLock);
        }

        // No-throw guarantee.
        protected void finalizeTransaction()
        {
            // Release the async lock if we own it (best-effort).
            try
            {
                if (ownsAsyncLock && connection != null)
                {
                    try
                    {
                        connection.ReleaseLock();
                    }
                    catch (Exception ex)
                    {
                        try { connection.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                    }
                    finally
                    {
                        ownsAsyncLock = false;
                    }
                }
            }
            catch { /* best-effort release; don't let this block final cleanup */ }

            // then cleanup the connection and transaction resources as before
            try
            {
                connection?.releaseConnection();
            }
            catch (System.Exception ex) // I don't think there is any way to get here, but just in case.
            {
                try
                {
                    connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                }
                catch (Exception) { }
            }
            finally
            {
                connection = null;
            }
        }

        public void Dispose()
        {
            Dispose(true); // Called from user code.
            GC.SuppressFinalize(this);
        }

        // Make DisposeAsync overridable so derived types can override and perform async commit/cleanup.
        public virtual async ValueTask DisposeAsync()
        {
            Dispose(true);
            await Task.CompletedTask.ConfigureAwait(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed == true)
                return;

            if (disposing == true) {/* Called from user code. Release managed and unmanaged resources. */}

            finalizeTransaction();
            disposed = true;
        }

        ~SxmUTransaction()
        {
            Dispose(false); // Called from runtime.
        }

        /********************* INSERT / UPDATE / DELETE wrappers (async implementations) ************************/

        public async Task<Dictionary<string, object?>> executeInsertAsync(string command, List<object> ParameterValues, CancellationToken cancellationToken = default)
        {
            long recordID = -1;
            string? synchID = default(string);

            InsertDefinition? insertDefinition = SqlStatements.insertStatements[command] as InsertDefinition;
            if (insertDefinition == null)
                throw new SxmException(new ErrorMessage("unknownSQLStatement", command));

            await executeNonQueryTransAsync(insertDefinition.InsertSQL, ParameterValues, cancellationToken).ConfigureAwait(false);

            try
            {
                if (insertDefinition.TableName.Length != 0)
                {
                    await executeQueryDirectAsync("select last_insert_rowid() as rowID", null, cancellationToken).ConfigureAwait(false);
                    Dictionary<string, object?>? nextRow = connection.getNextRow<Dictionary<string, object?>>();

                    if (nextRow != default && nextRow.Count > 0)
                        if (nextRow.ContainsKey("rowID") == true)
                        {
                            recordID = (long)nextRow["rowID"]!;
                            synchID = await getSynchID(insertDefinition.TableName, recordID);
                        }

                    if (synchID == null || synchID.Length == 0)
                        synchID = Guid.NewGuid().ToString();

                    List<object> synchIDPV = new List<object>();
                    synchIDPV.Add(synchID);
                    synchIDPV.Add(recordID);
                    await executeNonQueryAsync(String.Format("UPDATE {0} SET synchId = @p0 WHERE id = @p1", insertDefinition.TableName), synchIDPV, cancellationToken).ConfigureAwait(false);
                    synchIDPV.RemoveAt(1);

                    await executeNonQueryAsync(String.Format("UPDATE _systemCloudSynch SET action='insert' WHERE synchId = @p0 "), synchIDPV, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SxmException)
            {
                throw;
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }

            Dictionary<string, object?> ir = new Dictionary<string, object?>();
            ir.Add("id", recordID);
            ir.Add("synchId", synchID);
            return ir;
        }

        private async Task<string?> getSynchID(string tableName, long recordID)
        {
            string? synchId = default(string);

            try
            {
                List<object> parameterList = new List<object>();
                parameterList.Add(recordID);

                await connection.executeQueryAsync(String.Format("SELECT synchId FROM {0} WHERE id = @p0 LIMIT 1", tableName), parameterList);
                Dictionary<string, object?>? row = connection.getNextRow<Dictionary<string, object?>>();

                if (row != null && row.Count > 0)
                    if (row.ContainsKey("synchId") == true)
                        synchId = (string?)row["synchId"];
            }
            catch (Exception) { /* If an error occurs reading the record, then do nothing. Assume synch ID does not exist. */ }

            return synchId;
        }

        public async Task executeQueryAsync(string command, List<object>? ParameterValues, CancellationToken cancellationToken = default)
        {
            await connection.executeQueryAsync(command, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeUpdateAsync(string command, List<object> ParameterValues, CancellationToken cancellationToken = default)
        {
            await executeNonQueryAsync(SqlStatements.updateStatements[command].UpdateSQL, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeDeleteAsync(string command, List<object> ParameterValues, CancellationToken cancellationToken = default)
        {
            await executeNonQueryAsync(SqlStatements.deleteStatements[command].DeleteSQL, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeQueryDirectAsync(string sqlStatement, List<object>? ParameterValues, CancellationToken cancellationToken = default)
        {
            await connection.executeQueryAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeUpdateDirectAsync(string sqlStatement, List<object>? ParameterValues, CancellationToken cancellationToken = default)
        {
            await executeNonQueryAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeDeleteDirectAsync(string sqlStatement, List<object>? ParameterValues, CancellationToken cancellationToken = default)
        {
            await executeNonQueryAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeSystemUpdateDirectAsync(string sqlStatement, List<object>? ParameterValues, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeTableStatementAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeAlterTableAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeIndexAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeCreateTriggerAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task executeNonQueryAsync(string sqlStatement, List<object>? ParameterValues = null, CancellationToken cancellationToken = default)
        {
            await executeNonQueryTransAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
            interruptSynchronize = true;
        }

        public async Task executeNonQueryTransAsync(string sqlStatement, List<object>? ParameterValues = null, CancellationToken cancellationToken = default)
        {
            connection.beginTransaction();
            await connection.executeNonQueryAsync(sqlStatement, ParameterValues, cancellationToken).ConfigureAwait(false);
        }

        public void attachDatabase()
        {
            ArrayList databaseNames = DatabaseDescriptor.getDatabaseNames();

            foreach (string databaseName in databaseNames)
                attachDatabase(databaseName);
        }

        // Silent when attempting to attach to the current connection.
        public async Task attachDatabase(string databaseName)
        {
            if (connection.DatabaseName.Equals(databaseName) == false)
            {
                DatabaseDescriptor? databaseDescriptor = DatabaseDescriptor.getDescriptor(databaseName);
                if (databaseDescriptor == null)
                    throw new SxmException(new ErrorMessage("noDBDescriptorExists", databaseName));

                try
                {
                    string databaseFolderPath = Environment.GetFolderPath(databaseDescriptor.DatabaseFolder);
                    string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);

                    if (File.Exists(dbFullyQualifiedPath) == true)
                        await connection.executeNonQueryAsync(String.Format("ATTACH DATABASE '{0}' as {1}", dbFullyQualifiedPath, databaseName), null as List<object>);
                    else
                        throw new SxmException(new ErrorMessage("noDatabaseExists", databaseName));
                }
                catch (SxmException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    throw new SxmException(ex);
                }
            }
        }

        // Detach all attached databases. Detaching all databases is normally associated with cleanup, no-throw.
        public async Task detachDatabase()
        {
            try
            {
                await connection.executeQueryAsync("PRAGMA database_list", null as List<object>);

                while (nextRow() == true)
                {
                    try
                    {
                        string? dbName = (string?)getValue("name");
                        if (dbName?.ToLower().Equals("main") == false && dbName.ToLower().Equals("temp") == false)
                            detachDatabase(dbName);
                    }
                    catch (System.Exception) // Keep trying to detach all databases.
                    {
                    }
                }
            }
            catch (System.Exception)
            {
            }
        }

        // Silent when attempting to detach to the current connection.
        public async Task detachDatabase(string databaseName)
        {
            if (connection.DatabaseName.Equals(databaseName) == false)
            {
                DatabaseDescriptor? databaseDescriptor = DatabaseDescriptor.getDescriptor(databaseName);
                if (databaseDescriptor == null)
                    throw new SxmException(new ErrorMessage("noDBDescriptorExists", databaseName));

                try
                {
                    string databaseFolderPath = Environment.GetFolderPath(databaseDescriptor.DatabaseFolder);
                    string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);
                    if (File.Exists(dbFullyQualifiedPath) == true)
                        await connection.executeNonQueryAsync(String.Format("DETACH DATABASE '{0}'", databaseName), null as List<object>);
                    else
                        throw new SxmException(new ErrorMessage("noDatabaseExists", databaseName));
                }
                catch (SxmException)
                {
                    throw;
                }
                catch (System.Exception ex)
                {
                    throw new SxmException(ex);
                }
            }
        }

        // Returns error code for SqliteException, otherwise throw the exception.
        public SQLiteErrorCode commitTransaction()
        {
            // synchronous wrapper for compatibility: block on the async commit
            return commitTransactionAsync().GetAwaiter().GetResult();
        }

        public async Task<SQLiteErrorCode> commitTransactionAsync(CancellationToken cancellationToken = default)
        {
            SQLiteErrorCode ec = await connection.finishTransactionAsync(SQLiteXM.Defines.commitTransaction).ConfigureAwait(false);
            if (interruptSynchronize == true)
            {
                //SxmInit.interruptSynchronize (connection.DatabaseName);
                interruptSynchronize = false;
            }
            return ec;
        }

        public void rollbackTransaction()
        {
            // synchronous wrapper for compatibility: block on async rollback
            rollbackTransactionAsync().GetAwaiter().GetResult();
        }

        public async Task rollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            await connection.finishTransactionAsync(SQLiteXM.Defines.rollbackTransaction).ConfigureAwait(false);
            interruptSynchronize = false;
        }

        public bool hasRows()
        {
            return connection.hasRows();
        }

        public object? getValue(string fieldName)
        {
            return connection.getValue(fieldName);
        }

        public object? getValue(int fieldOrdinal)
        {
            return connection.getValue(fieldOrdinal);
        }

        public string? getFieldName(int fieldOrdinal)
        {
            return connection.getFieldName(fieldOrdinal);
        }

        public string[] getFieldNames()
        {
            return connection.getFieldNames();
        }

        public T? getNextRow<T>() where T : IDictionary<string, object?>, new()
        {
            return connection.getNextRow<T>();
        }

        public List<T> getAllRows<T>() where T : IDictionary<string, object?>, new()
        {
            List<T> allRows = new List<T>();
            T? row;

            while ((row = getNextRow<T>()) != null)
                allRows.Add(row);

            return allRows;
        }

        public int getColumnCount()
        {
            return connection.getColumnCount();
        }

        public bool nextRow()
        {
            return connection.nextRow();
        }

        public Type? getType(string fieldName)
        {
            return connection.getType(fieldName);
        }
    }
}