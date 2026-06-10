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
        /// Create a transaction wrapper for the supplied connection.
        /// If the supplied connection is non-shared (transient/private) and CreateAsync fails,
        /// the connection will be best-effort destroyed to avoid leaking the resource.
        /// Caller-supplied shared connections are never destroyed by this method.
        /// </summary>
        /// <param name="conn">Connection supplied by the caller.</param>
        /// <param name="waitMilliseconds">Timeout to wait for any required lock.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created <see cref="SxmUTransaction"/>.</returns>
        public static async Task<SxmUTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            try
            {
                Guid? ownerId = null;
                bool ownsLock = false;

                // If the connection is shared, we must acquire the lock before creating the transaction.
                if (conn.Shared)
                {
                    ownerId = Guid.NewGuid();
                    bool locked = await conn.LockAsync(waitMilliseconds, cancellationToken, ownerId).ConfigureFalse();
                    if (!locked)
                        throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.LockDb, conn.DatabaseName));

                    ownsLock = true;
                }

                // At this point either:
                // - conn.Shared == true and we hold the lock (ownsLock == true), or
                // - conn.Shared == false (transient) and we simply hand the connection to the transaction.
                return new SxmUTransaction(conn, ownsLock, ownerId);
            }
            catch
            {
                // Best-effort cleanup: if the caller passed a non-shared (transient) connection,
                // they likely handed ownership to the transaction factory. If CreateAsync failed,
                // destroy the transient connection so callers won't accidentally leak it.
                if (!conn.Shared)
                {
                    try { await conn.DestroyConnectionAsync().ConfigureFalse(); } catch { /* swallow cleanup errors */ }
                }

                throw;
            }
        }

        /// <summary>
        /// Finalize and clean up the transaction object asynchronously.
        /// </summary>
        /// <remarks>
        /// Note: This method may throw if resource cleanup fails. However, the 
        /// public DisposeAsync/Dispose methods are guaranteed to catch and log 
        /// these exceptions to ensure safe object disposal.
        /// </remarks>
        ///
        /// Important semantics:
        /// - Calling commitTransaction()/commitTransactionAsync() only ends the underlying SQLite transaction
        ///   (COMMIT/ROLLBACK). It does NOT release the SxmUTransaction's connection lock or null out this object.
        /// - The SxmUTransaction instance may be reused after a commit to start new database transactions on the same
        ///   connection; the connection lock remains held until this transaction is disposed/finalized.
        /// - FinalizeTransactionAsync is intentionally best-effort and non-throwing to avoid throwing from finalizers.
        /// </summary>
        protected async Task FinalizeTransactionAsync()
        {
            // Centralized, idempotent lock release helper.
            EnsureLockReleased();

            // then cleanup the connection and transaction resources as before.
            try
            {
                if (_connection != null)
                {
                    // Only destroy the underlying native connection automatically when the
                    // SxmConnection is non-shared. Shared connections remain caller-owned.
                    bool destroyAutomatically = !_connection.Shared;
                    await _connection.ReleaseConnectionAsync(destroy: destroyAutomatically).ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"FinalizeTransaction failure. Database: '{_connection?.DatabaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"FinalizeTransaction failure. Database: '{_connection?.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
                if (_connection != null)
                {
                    bool destroyedAutomatically = !_connection.Shared;
                    if (destroyedAutomatically)
                    {
                        // If we automatically destroyed the connection, then null out our reference to it to avoid future use.
                        _connection = null;
                    }
                }
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
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronous dispose. Overridable so derived types can perform async commit/cleanup.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> that completes when disposal is finished.</returns>
        public virtual async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            try
            {
                await FinalizeTransactionAsync().ConfigureFalse();
            }
            catch (System.Exception ex)
            {
                // Best-effort: log and swallow to avoid throwing from Dispose.
                SxmLogging.Log(ex, $"DisposeAsync failure. Database: '{_connection?.DatabaseName}'.");
            }
            finally
            {
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        /********************* INSERT / UPDATE / DELETE wrappers (async implementations) ************************/

        internal async Task<Dictionary<string, object?>> ExecuteInsertDirectAsync(string insertSql, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            string commandName = Guid.NewGuid().ToString();
            InsertDefinition id = new InsertDefinition(commandName, insertSql);

            Dictionary<string, object?> insertResponse;
            try
            {
                SxmSqlStatements.InsertStatements.TryAdd(commandName, id);
                insertResponse = await ExecuteInsertAsync(commandName, parameterValues, cancellationToken).ConfigureFalse();
            }
            finally
            {
                SxmSqlStatements.InsertStatements.TryRemove(commandName, out _);
            }

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

            if (!SxmSqlStatements.InsertStatements.TryGetValue(command, out InsertDefinition? insertDefinition) || insertDefinition == null)
            {
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));
            }

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
                            synchID = await GetSynchIdAsync(insertDefinition.TableName, recordID).ConfigureFalse();
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
                SxmLogging.Log(ex, $"ExecuteInsertAsync failure. Database: '{_connection?.DatabaseName}'. Table: '{insertDefinition.TableName}'. Command: {insertDefinition.InsertSQL}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteInsertAsync failure. Database: '{_connection?.DatabaseName}'. Table: '{insertDefinition.TableName}'. Command: {insertDefinition.InsertSQL}";
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

                await _connection.ExecuteQueryAsync(String.Format("SELECT synchId FROM {0} WHERE id = @p0 LIMIT 1", SxmHelpers.QuoteIdentifier(tableName)), parameterList).ConfigureFalse();
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

            if (!SxmSqlStatements.SelectStatements.TryGetValue(command, out SelectDefinition? selectDefinition) || selectDefinition == null)
            {
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));
            }

            await _connection.ExecuteQueryAsync(selectDefinition.SelectSQL, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a named update statement (non-query).
        /// </summary>
        /// <param name="command">Logical command key mapped to an update statement.</param>
        /// <param name="parameterValues">Parameter values for the update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteUpdateAsync(string command, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            if (!SxmSqlStatements.UpdateStatements.TryGetValue(command, out UpdateDefinition? updateDefinition) || updateDefinition == null)
            {
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));
            }

            await ExecuteNonQueryAsync(updateDefinition.UpdateSQL, parameterValues, cancellationToken).ConfigureFalse();
        }

        /// <summary>
        /// Execute a named delete statement (non-query).
        /// </summary>
        /// <param name="command">Logical command key mapped to a delete statement.</param>
        /// <param name="parameterValues">Parameter values for the delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task ExecuteDeleteAsync(string command, List<object> parameterValues, CancellationToken cancellationToken = default)
        {
            if (!SxmSqlStatements.DeleteStatements.TryGetValue(command, out DeleteDefinition? deleteDefinition) || deleteDefinition == null)
            {
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));
            }

            await ExecuteNonQueryAsync(deleteDefinition.DeleteSQL, parameterValues, cancellationToken).ConfigureFalse();
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
                await AttachDatabaseAsync(databaseName).ConfigureFalse();
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
            await _connection.ExecuteQueryAsync("PRAGMA database_list", null as List<object>).ConfigureFalse();

            while (NextRow() == true)
            {
                try
                {
                    string? dbName = (string?)GetValue("name");
                    if (!string.IsNullOrEmpty(dbName) &&
                        !string.Equals(dbName, "main", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(dbName, "temp", StringComparison.OrdinalIgnoreCase))
                    {
                        await DetachDatabaseAsync(dbName).ConfigureFalse();
                    }
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
                string? databaseFolderPath = SxmDatabaseDescriptor.DatabaseFolder;
                if (databaseFolderPath == null)
                    throw new InvalidOperationException("Database folder path is not configured.");

                string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);

                if (File.Exists(dbFullyQualifiedPath) == true)
                    await _connection.ExecuteNonQueryAsync(String.Format("ATTACH DATABASE '{0}' as {1}", dbFullyQualifiedPath, databaseName), null as List<object>).ConfigureFalse();
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
                string? databaseFolderPath = SxmDatabaseDescriptor.DatabaseFolder;
                if (databaseFolderPath == null)
                    throw new InvalidOperationException("Database folder path is not configured.");

                string dbFullyQualifiedPath = Path.Combine(databaseFolderPath, databaseName);
                if (File.Exists(dbFullyQualifiedPath) == true)
                    await _connection.ExecuteNonQueryAsync(String.Format("DETACH DATABASE '{0}'", databaseName), null as List<object>).ConfigureFalse();
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
                //SxmDatabase.interruptSynchronize (connection.DatabaseName);
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