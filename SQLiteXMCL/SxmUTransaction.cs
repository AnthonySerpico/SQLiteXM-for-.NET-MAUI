using SQLiteXM.Internal.Threading;
using SQLiteXM;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;

namespace SQLiteXM
{
    /// <summary>
    /// Lightweight transaction wrapper used by callers that need to run one or more
    /// SQL statements within a SQLite transaction and optionally hold a connection-level lock
    /// for the duration of the SxmUTransaction object's lifetime.
    /// </summary>
    public class SxmUTransaction : IDisposable, IAsyncDisposable
    {
        private bool _interruptSynchronize = false;
        private SxmConnection? _connection;
        private bool _disposed = false;
        private bool _ownsAsyncLock = false;
        private Guid? _lockOwnerId = null;

        /// <summary>
        /// Gets the underlying <see cref="SxmConnection"/> used by this transaction.
        /// May be null after the transaction is finalized/disposed.
        /// </summary>
        public SxmConnection? Connection { get => _connection; }

        // Private ctor used by the async factory. Connection lock already acquired.
        private protected SxmUTransaction(SxmConnection conn, bool ownsLock, Guid? ownerId = null)
        {
            if ((this._connection = conn) == default)
            {
                string errStr = $"SxmUTransaction ctor failure. SxmConnection 'conn' is null.";
                ArgumentNullException argumentNullException = new ArgumentNullException(errStr);
                SxmLogging.Log(argumentNullException, errStr);
                throw argumentNullException;
            }

            this._ownsAsyncLock = ownsLock;
            this._lockOwnerId = ownerId;
        }

        /// <summary>
        /// Factory: create a private (non-shared) connection (if <paramref name="databaseName"/> provided).
        /// Does not attempt to acquire the shared connection async lock.
        /// </summary>
        /// <param name="databaseName">Optional database file name to open; if null uses default connection.</param>
        /// <returns>A new <see cref="SxmUTransaction"/> wrapping a private connection.</returns>
        internal static SxmUTransaction Create(string? databaseName = null)
        {
            SxmConnection conn = new SxmConnection(databaseName, shared: false);
            return new SxmUTransaction(conn, ownsLock: false, ownerId: null);
        }

        /// <summary>
        /// Async factory overload used when the caller already has an <see cref="SxmConnection"/>.
        /// When the supplied connection is shared this method attempts to acquire the connection lock
        /// asynchronously and will throw an <see cref="SxmException"/> if the lock cannot be obtained.
        /// </summary>
        /// <param name="conn">Shared or private connection to use for the transaction.</param>
        /// <param name="waitMilliseconds">Timeout in milliseconds to wait for the connection lock when the connection is shared.</param>
        /// <param name="cancellationToken">Cancellation token to observe when waiting for the lock.</param>
        /// <returns>A new <see cref="SxmUTransaction"/> that may own the connection lock.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="conn"/> is null.</exception>
        /// <exception cref="SxmException">If the connection is shared and the lock could not be acquired.</exception>
        internal static async Task<SxmUTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            bool ownsLock = false;
            Guid? ownerId = null;

            // Only attempt lock when the supplied connection is shared.
            if (conn.Shared)
            {
                ownerId = Guid.NewGuid();
                bool locked = await conn.LockAsync(waitMilliseconds, cancellationToken, ownerId).ConfigureFalse();
                if (!locked)
                {
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.LockDb, conn.DatabaseName));
                }
                ownsLock = true;
            }

            return new SxmUTransaction(conn, ownsLock: ownsLock, ownerId: ownerId);
        }

        /// <summary>
        /// Finalize and clean up the transaction object.
        /// This method performs a best-effort release of the connection lock (if this transaction owns it)
        /// and then releases/returns the underlying connection.
        ///
        /// Important semantics:
        /// - Calling commitTransaction()/commitTransactionAsync() only ends the underlying SQLite transaction
        ///   (COMMIT/ROLLBACK). It does NOT release the SxmUTransaction's connection lock or null out this object.
        /// - The SxmUTransaction instance may be reused after a commit to start new database transactions on the same
        ///   connection; the connection lock remains held until this transaction is disposed/finalized.
        /// - finalizeTransaction is intentionally best-effort and non-throwing to avoid throwing from finalizers.
        /// </summary>
        protected void FinalizeTransaction()
        {
            // Centralized, idempotent lock release helper.
            EnsureLockReleased();

            // then cleanup the connection and transaction resources as before
            try
            {
                _connection?.ReleaseConnection();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"FinalizeTransaction failure for database '{_connection?.DatabaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"FinalizeTransaction failure for database '{_connection?.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
                _connection = null;
            }
        }

        /// <summary>
        /// Ensure the async lock is released if this transaction owns it.
        /// This method is idempotent and swallows/logs exceptions as a best-effort cleanup helper.
        /// Derived classes may call this to deterministically release a lock before final disposal.
        /// </summary>
        protected void EnsureLockReleased()
        {
            // Best-effort, no-throw lock release. Idempotent.
            try
            {
                if (_ownsAsyncLock && _connection != null)
                {
                    try
                    {
                        // Release only if we are the owner
                        _connection.ReleaseLock(_lockOwnerId);
                    }
                    catch
                    {
                        // Swallow any exception during lock release to avoid noisy logs from cleanup/finalizer paths.
                        // If you need diagnostics, enable a debug/verbose option and log there.
                    }
                    finally
                    {
                        _ownsAsyncLock = false;
                        _lockOwnerId = null;
                    }
                }
            }
            catch { /* best-effort release; don't let this block final cleanup */ }
        }

        /// <summary>
        /// Dispose pattern implementation. Releases connection lock and returns/releases the underlying connection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true); // Called from user code.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronous dispose. Overridable so derived types can perform async commit/cleanup.
        /// Default implementation runs the synchronous dispose logic.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes when disposal is finished.</returns>
        public virtual async ValueTask DisposeAsync()
        {
            Dispose(true);
            await Task.CompletedTask.ConfigureFalse();
        }

        /// <summary>
        /// Core dispose implementation. Called by both <see cref="Dispose"/> and the finalizer.
        /// </summary>
        /// <param name="disposing">True when called from user code; false when called from the runtime finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            // If called from user code we could release managed resources here.
            // Do not duplicate lock-release: finalizeTransaction() already calls EnsureLockReleased().
            if (disposing)
            {
                // Release managed resources (none specific here).
            }

            try
            {
                FinalizeTransaction();
            }
            catch (System.Exception ex)
            {
                // Best-effort: log and swallow to avoid throwing from Dispose.
                SxmLogging.Log(ex);
            }
            finally
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure best-effort cleanup if Dispose was not called.
        /// </summary>
        ~SxmUTransaction()
        {
            Dispose(false); // Called from runtime.
        }

        /********************* INSERT / UPDATE / DELETE wrappers (async implementations) ************************/

        internal async Task<Dictionary<string, object?>> ExecuteInsertDirectAsync(string insertSql, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            string commandName = new Guid().ToString();
            string? tableName = SxmHelpers.ExtractTableNameFromInsert(insertSql);

            InsertDefinition id = new InsertDefinition(commandName, insertSql);
            SxmSqlStatements.InsertStatements.Add(commandName, id);

            Dictionary<string, object?> insertResponse = await ExecuteInsertAsync(commandName, parameterValues, cancellationToken);
            SxmSqlStatements.InsertStatements.Remove(commandName);
            return insertResponse;
        }


        /// <summary>
        /// Execute an insert statement by command key and return the generated id and synchId.
        /// The method will run the insert inside a transaction and then update the record's synchId
        /// and the system cloud synchronization table as required by the library.
        /// </summary>
        /// <param name="command">Logical command key mapped to an insert statement.</param>
        /// <param name="parameterValues">Parameter values for the insert statement.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A dictionary containing keys "id" (long) and "synchId" (string).</returns>
        /// <exception cref="SxmException">If the command key is unknown or an underlying DB error occurs.</exception>
        internal async Task<Dictionary<string, object?>> ExecuteInsertAsync(string command, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            long recordID = -1;
            string? synchID = default(string);

            InsertDefinition? insertDefinition = SxmSqlStatements.InsertStatements[command] as InsertDefinition;
            if (insertDefinition == null)
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));

            await ExecuteNonQueryTransAsync(insertDefinition.InsertSQL, parameterValues, cancellationToken).ConfigureFalse();

            try
            {
                if (insertDefinition.TableName.Length != 0)
                {
                    if (_connection is null)
                    {
                        throw new ArgumentNullException($"ExecuteInsertAsync failure. SxmConnection '_connection' is null.");
                    }

                    await ExecuteQueryDirectAsync("select last_insert_rowid() as rowID", null, cancellationToken).ConfigureFalse();
                    Dictionary<string, object?>? nextRow = _connection.GetNextRow<Dictionary<string, object?>>();

                    if (nextRow != default && nextRow.Count > 0)
                        if (nextRow.ContainsKey("rowID") == true)
                        {
                            recordID = (long)nextRow["rowID"]!;
                            synchID = await GetSynchIdAsync(insertDefinition.TableName, recordID);
                        }

                    if (synchID == null || synchID.Length == 0)
                        synchID = Guid.NewGuid().ToString();

                    List<object> synchIdParams = new List<object>();
                    synchIdParams.Add(synchID);
                    synchIdParams.Add(recordID);
                    await ExecuteNonQueryAsync(String.Format("UPDATE {0} SET synchId = @p0 WHERE id = @p1", SxmHelpers.QuoteIdentifier(insertDefinition.TableName)), synchIdParams, cancellationToken).ConfigureFalse();
                    synchIdParams.RemoveAt(1);

                    await ExecuteNonQueryAsync(String.Format("UPDATE _systemCloudSynch SET action='insert' WHERE synchId = @p0 "), synchIdParams, cancellationToken).ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"ExecuteInsertAsync failure for database '{_connection?.DatabaseName}' table '{insertDefinition.TableName}' SQL statement '{insertDefinition.InsertSQL}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteInsertAsync failure for database '{_connection?.DatabaseName}' table '{insertDefinition.TableName}' SQL statement '{insertDefinition.InsertSQL}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            Dictionary<string, object?> ir = new Dictionary<string, object?>();
            ir.Add("id", recordID);
            ir.Add("synchId", synchID);
            return ir;
        }

        /// <summary>
        /// Attempt to read the synchId for a record in <paramref name="tableName"/> with <paramref name="recordID"/>.
        /// Returns null if the record has no synchId or the read fails.
        /// </summary>
        /// <param name="tableName">Table name to query.</param>
        /// <param name="recordID">Record id to look up.</param>
        /// <returns>The synchId string if present; otherwise null.</returns>
        private async Task<string?> GetSynchIdAsync(string tableName, long recordID)
        {
            string? synchId = default(string);

            if (_connection is null)
            {
                string errStr = $"GetSynchIdAsync failure. SxmConnection '_connection' is null.";
                ArgumentNullException argumentNullException = new ArgumentNullException(errStr);
                SxmLogging.Log(argumentNullException, errStr);
                throw argumentNullException;
            }

            try
            {
                List<object> parameterList = new List<object>();
                parameterList.Add(recordID);

                await _connection.ExecuteQueryAsync(String.Format("SELECT synchId FROM {0} WHERE id = @p0 LIMIT 1", SxmHelpers.QuoteIdentifier(tableName)), parameterList);
                Dictionary<string, object?>? row = _connection.GetNextRow<Dictionary<string, object?>>();

                if (row != null && row.Count > 0)
                    if (row.ContainsKey("synchId") == true)
                        synchId = (string?)row["synchId"];
            }
            catch (Exception) { /* If an error occurs reading the record, then do nothing. Assume synch ID does not exist. */ }

            return synchId;
        }

        /// <summary>
        /// Execute a named select statement from the library's statement dictionary.
        /// Results are available via <see cref="GetNextRow{T}"/> / <see cref="GetValue(string)"/>.
        /// </summary>
        /// <param name="command">Logical command key mapped to a select statement.</param>
        /// <param name="parameterValues">Parameter values for the query, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteQueryAsync(string command, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"ExecuteQueryAsync failure. SxmConnection '_connection' is null.");
            }
            await _connection.ExecuteQueryAsync(SxmSqlStatements.SelectStatements[command].SelectSQL, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a named update statement (non-query).
        /// </summary>
        /// <param name="command">Logical command key mapped to an update statement.</param>
        /// <param name="parameterValues">Parameter values for the update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteUpdateAsync(string command, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryAsync(SxmSqlStatements.UpdateStatements[command].UpdateSQL, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a named delete statement (non-query).
        /// </summary>
        /// <param name="command">Logical command key mapped to a delete statement.</param>
        /// <param name="parameterValues">Parameter values for the delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteDeleteAsync(string command, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryAsync(SxmSqlStatements.DeleteStatements[command].DeleteSQL, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute an ad-hoc select statement directly.
        /// </summary>
        /// <param name="sqlStatement">SQL select statement to execute.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteQueryDirectAsync(string sqlStatement, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"ExecuteQueryDirectAsync failure. SxmConnection '_connection' is null.");
            }
            await _connection.ExecuteQueryAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute an ad-hoc non-query (update) directly inside a transaction.
        /// This method marks the transaction as modified to interrupt synchronization if needed.
        /// </summary>
        /// <param name="sqlStatement">SQL statement to execute.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteUpdateDirectAsync(string sqlStatement, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute an ad-hoc delete directly inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL delete statement to execute.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteDeleteDirectAsync(string sqlStatement, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a system-level update directly inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL statement.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteSystemUpdateDirectAsync(string sqlStatement, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a table statement (e.g. CREATE TABLE) inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteTableStatementAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute an ALTER TABLE statement inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteAlterTableAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute an index creation statement inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteIndexAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a CREATE TRIGGER statement inside a transaction.
        /// </summary>
        /// <param name="sqlStatement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteCreateTriggerAsync(string sqlStatement, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, null, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a non-query inside a transaction and mark the transaction as modified
        /// so callers know synchronization may need to be interrupted.
        /// </summary>
        /// <param name="sqlStatement">SQL statement to execute.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ExecuteNonQueryAsync(string sqlStatement, List<object>? parameterValues = null, CancellationToken cancellationToken = default)
        {
            await ExecuteNonQueryTransAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
            _interruptSynchronize = true;
        }

        /// <summary>
        /// Internal helper to begin a transaction and execute a non-query on the connection.
        /// </summary>
        /// <param name="sqlStatement">SQL statement to execute.</param>
        /// <param name="parameterValues">Parameter values or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task ExecuteNonQueryTransAsync(string sqlStatement, List<object>? parameterValues = null, CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"ExecuteNonQueryTransAsync failure. SxmConnection '_connection' is null.");
            }
            _connection.BeginTransaction();
            await _connection.ExecuteNonQueryAsync(sqlStatement, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Attach all databases described by the DatabaseDescriptor collection to the current connection.
        /// </summary>
        public async Task AttachDatabaseAsync()
        {
            foreach (string databaseName in SxmDatabaseDescriptor.GetDatabaseNames())
                await AttachDatabaseAsync(databaseName);
        }

        /// <summary>
        /// Detach all attached databases. This is a best-effort, no-throw cleanup operation.
        /// </summary>
        public async Task DetachDatabaseAsync()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"DetachDatabaseAsync failure. SxmConnection '_connection' is null.");
            }
            await _connection.ExecuteQueryAsync("PRAGMA database_list", null as List<object>);

            while (NextRow() == true)
            {
                try
                {
                    string? dbName = (string?)GetValue("name");
                    if (dbName?.ToLower().Equals("main") == false && dbName.ToLower().Equals("temp") == false)
                        await DetachDatabaseAsync(dbName);
                }
                catch (System.Exception) // Keep trying to detach all databases.
                {
                }
            }
        }

        /// <summary>
        /// Attach a single database file to the current connection. Silent when attempting to attach the current connection.
        /// </summary>
        /// <param name="databaseName">Name of the database to attach.</param>
        /// <returns>A task that completes when the attach completes.</returns>
        /// <exception cref="SxmException">If the database descriptor is missing or the database file does not exist.</exception>
        public async Task AttachDatabaseAsync(string databaseName)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"AttachDatabaseAsync failure. SxmConnection '_connection' is null.");
            }
            if (_connection.DatabaseName is null)
            {
                throw new ArgumentNullException($"AttachDatabaseAsync failure. '_connection.DatabaseName' is null.");
            }

            if (_connection.DatabaseName?.Equals(databaseName) == false)
            {
                string databaseFolderPath = SxmDatabaseDescriptor.DatabaseFolder;
                string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);

                if (File.Exists(dbFullyQualifiedPath) == true)
                    await _connection.ExecuteNonQueryAsync(String.Format("ATTACH DATABASE '{0}' as {1}", dbFullyQualifiedPath, databaseName), null as List<object>);
                else
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.NoDatabaseExists, databaseName));
            }
        }

        /// <summary>
        /// Detach a single named database. Silent when attempting to detach the current connection.
        /// </summary>
        /// <param name="databaseName">Name of the database to detach.</param>
        /// <returns>A task that completes when the detach completes.</returns>
        /// <exception cref="SxmException">If the database descriptor is missing or the database file does not exist.</exception>
        public async Task DetachDatabaseAsync(string databaseName)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"DetachDatabaseAsync failure. SxmConnection '_connection' is null.");
            }
            if (_connection.DatabaseName is null)
            {
                throw new ArgumentNullException($"DetachDatabaseAsync failure. '_connection.DatabaseName' is null.");
            }

            if (_connection.DatabaseName?.Equals(databaseName) == false)
            {
                string databaseFolderPath = SxmDatabaseDescriptor.DatabaseFolder;
                string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);
                if (File.Exists(dbFullyQualifiedPath) == true)
                    await _connection.ExecuteNonQueryAsync(String.Format("DETACH DATABASE '{0}'", databaseName), null as List<object>);
                else
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.NoDatabaseExists, databaseName));
            }
        }

        /// <summary>
        /// Commit the current SQLite transaction.
        /// If the transaction has modified data this method may trigger synchronization interruption logic.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token (currently unused by implementation).</param>
        /// <returns>The SQLite error code returned from finishing the transaction.</returns>
        public async Task<SQLiteErrorCode> CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"CommitTransactionAsync failure. SxmConnection '_connection' is null.");
            }

            SQLiteErrorCode ec = await _connection.FinishTransactionAsync(SQLiteXM.SxmDefines.CommitTransaction).ConfigureFalse();
            if (_interruptSynchronize == true)
            {
                //SxmInit.interruptSynchronize (connection.DatabaseName);
                _interruptSynchronize = false;
            }
            return ec;
        }

        /// <summary>
        /// Roll back the current SQLite transaction.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token (currently unused by implementation).</param>
        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"RollbackTransactionAsync failure. SxmConnection '_connection' is null.");
            }

            await _connection.FinishTransactionAsync(SQLiteXM.SxmDefines.RollbackTransaction).ConfigureFalse();
            _interruptSynchronize = false;
        }

        /// <summary>
        /// Returns true when the last executed query has rows to read.
        /// </summary>
        public bool HasRows()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"HasRows failure. SxmConnection '_connection' is null.");
            }

            return _connection.HasRows();
        }

        /// <summary>
        /// Get the value of the named field from the current row.
        /// </summary>
        /// <param name="fieldName">Field name to read.</param>
        /// <returns>The field value or null.</returns>
        private object? GetValue(string fieldName)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetValue failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetValue(fieldName);
        }

        /// <summary>
        /// Get the value of the field at the given ordinal from the current row.
        /// </summary>
        /// <param name="fieldOrdinal">Zero-based column ordinal.</param>
        /// <returns>The field value or null.</returns>
        private object? GetValue(int fieldOrdinal)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetValue failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetValue(fieldOrdinal);
        }

        /// <summary>
        /// Get the name of the field at the given ordinal.
        /// </summary>
        /// <param name="fieldOrdinal">Zero-based column ordinal.</param>
        /// <returns>The field name or null.</returns>
        private string? GetFieldName(int fieldOrdinal)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetFieldName failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetFieldName(fieldOrdinal);
        }

        /// <summary>
        /// Get all field names for the current result set.
        /// </summary>
        /// <returns>Array of field names.</returns>
        private string[] GetFieldNames()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetFieldNames failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetFieldNames();
        }

        /// <summary>
        /// Read the next row from the current result set and return it as a dictionary-like object.
        /// </summary>
        /// <typeparam name="T">A dictionary type that maps column names to values.</typeparam>
        /// <returns>An instance of <typeparamref name="T"/> representing the row, or null when no more rows exist.</returns>
        private T? GetNextRow<T>() where T : IDictionary<string, object?>, new()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetNextRow failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetNextRow<T>();
        }

        /// <summary>
        /// Read all remaining rows from the current result set and return them as a list.
        /// </summary>
        /// <typeparam name="T">A dictionary type that maps column names to values.</typeparam>
        /// <returns>List of rows.</returns>
        internal List<T> GetAllRows<T>() where T : IDictionary<string, object?>, new()
        {
            List<T> allRows = new List<T>();
            T? row;

            while ((row = GetNextRow<T>()) != null)
                allRows.Add(row);

            return allRows;
        }

        /// <summary>
        /// Get the number of columns in the current result set.
        /// </summary>
        /// <returns>Column count.</returns>
        private int GetColumnCount()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetColumnCount failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetColumnCount();
        }

        /// <summary>
        /// Advance to the next row in the current result set.
        /// </summary>
        /// <returns>True if a row is available; otherwise false.</returns>
        private bool NextRow()
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"NextRow failure. SxmConnection '_connection' is null.");
            }

            return _connection.NextRow();
        }

        /// <summary>
        /// Get the CLR type for the named field in the current result set.
        /// </summary>
        /// <param name="fieldName">Field name to query.</param>
        /// <returns>CLR <see cref="System.Type"/> of the field or null.</returns>
        private Type? GetType(string fieldName)
        {
            if (_connection is null)
            {
                throw new ArgumentNullException($"GetType failure. SxmConnection '_connection' is null.");
            }

            return _connection.GetType(fieldName);
        }
    }
}