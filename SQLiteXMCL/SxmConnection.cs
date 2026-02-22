using SQLiteXM.Internal.Threading;
using System.Collections;
using System.Data.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SQLiteXM
{
    /// <summary>
    /// Common SQLite error codes returned by the underlying provider.
    /// Matches SQLite's native result codes for conversion into the library's API.
    /// </summary>
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

    /// <summary>
    /// Lease handle returned when a caller acquires exclusive access to a shared connection.
    /// Disposing the lease releases the connection lock.
    /// </summary>
    internal interface ISxmConnectionLease : IAsyncDisposable
    {
        /// <summary>
        /// The connection instance the lease protects.
        /// </summary>
        SxmConnection Connection { get; }

        /// <summary>
        /// The owner token associated with this lease.
        /// </summary>
        Guid OwnerId { get; }
    }


    /// <summary>
    /// Lightweight connection wrapper around <c>Microsoft.Data.Sqlite.SqliteConnection</c>.
    /// Provides convenience APIs for shared/non-shared connections, parameter handling,
    /// transaction management and simple reader helpers used throughout SQLiteXM.
    /// </summary>
    public class SxmConnection
    {
        // true => connection is shared / reused across callers
        // false => connection is non-shared / private to the creator
        private bool _shared;
        /// <summary>
        /// Indicates whether the underlying connection is shared (true) or private (false).
        /// Shared connections may be reused across callers and support reentrant locking via owner tokens.
        /// </summary>
        public bool Shared => _shared;

        private string? _databaseName;
        /// <summary>
        /// The resolved database name for this connection instance.
        /// Can be null for an implicit single-descriptor scenario.
        /// </summary>
        public string? DatabaseName
        {
            get { return _databaseName; }
        }
        private DbCommand? _connCommand;
        private DbDataReader? _connDataReader;
        private Microsoft.Data.Sqlite.SqliteConnection? _dbConn;
        private Microsoft.Data.Sqlite.SqliteTransaction? _dbConnTransaction;

        private static readonly object _synchLock = new object();

        // Semaphore used to guard concurrent access. Use ownership + reentrancy to avoid accidentally
        // releasing someone else's lock and to allow a logical owner to re-enter.
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private readonly object _ownerSync = new object();
        private Guid? _lockOwner;
        private int _lockReentrancy = 0;

        private static Dictionary<string, string> _dbConnectionString = new Dictionary<string, string>();
        private static readonly string _sqliteConnString = "Data Source={0}; Mode=ReadWriteCreate;";

        private enum DbParametersDataType { List, TupleList, TwoDArray, OneDArray, HashTable, Dictionary }

        /// <summary>
        /// Create a new SxmConnection for the specified databaseName.
        /// If <paramref name="shared"/> is true the connection may be reused across callers.
        /// Throws <see cref="SxmException"/> on initialization failures.
        /// </summary>
        /// <param name="databaseName">Name of the database file (or null to use implicit name).</param>
        /// <param name="shared">Whether the connection is shared/reused (default true).</param>
        public SxmConnection(string? databaseName, bool shared = true)
        {
            try
            {
                this._databaseName = databaseName;
                this._shared = shared;

                CreateNewConnection();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"Connection failure for database '{this._databaseName}' shared '{this._shared}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"Connection failure for database '{this._databaseName}' shared '{this._shared}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Acquire a lease that owns the connection lock. The returned lease must be disposed (await using) to release the lock.
        /// Throws <see cref="SxmException"/> when the lock cannot be acquired in the requested time.
        /// </summary>
        /// <param name="millisecondsTimeout">Wait time in milliseconds (use -1 for infinite).</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting.</param>
        /// <param name="requestedOwnerId">Optional owner id to use for reentrancy; if null a new Guid is created.</param>
        /// <returns>An <see cref="ISxmConnectionLease"/> that will release the lock when disposed.</returns>
        internal async Task<ISxmConnectionLease> AcquireLeaseAsync(int millisecondsTimeout = 100, CancellationToken cancellationToken = default, Guid? requestedOwnerId = null)
        {
            Guid ownerId = requestedOwnerId ?? Guid.NewGuid();

            bool locked = await LockAsync(millisecondsTimeout, cancellationToken, ownerId).ConfigureFalse();
            if (!locked)
            {
                // Preserve existing behavior: surface a lock failure as a library exception.
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.LockDb, this._databaseName));
            }

            return new ConnectionLease(this, ownerId);
        }

        /// <summary>
        /// Small lease implementation that releases the connection lock when disposed.
        /// </summary>
        private sealed class ConnectionLease : ISxmConnectionLease
        {
            private readonly SxmConnection _connection;
            private bool _disposed;

            /// <inheritdoc/>
            public SxmConnection Connection => _connection;

            /// <inheritdoc/>
            public Guid OwnerId { get; }

            internal ConnectionLease(SxmConnection connection, Guid ownerId)
            {
                _connection = connection ?? throw new ArgumentNullException(nameof(connection));
                OwnerId = ownerId;
            }

            /// <inheritdoc/>
            public ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    try
                    {
                        // best-effort release; swallow to avoid throwing during cleanup
                        _connection.ReleaseLock(OwnerId);
                    }
                    catch
                    {
                        // intentionally ignore - release should never crash callers during cleanup
                    }
                }
                return ValueTask.CompletedTask;
            }
        }

        private void CreateNewConnection()
        {
            try
            {
                string? connectionString = SxmConnection.GetConnectionString(ref this._databaseName);
                _dbConn = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                _dbConn.Open();

                // Execute PRAGMA synchronously (very quick) to avoid sync-over-async in ctor.
                using (var cmd = _dbConn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA foreign_keys = ON";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "PRAGMA journal_mode = WAL";
                    cmd.ExecuteNonQuery();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                DestroyConnection();
                SxmLogging.Log(ex, $"CreateNewConnection failure for database '{this._databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                DestroyConnection();
                string errStr = $"CreateNewConnection failure for database '{this._databaseName}'.";
                SxmLogging.Log(ex);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Build or retrieve a cached connection string for the given database name.
        /// This method resolves implicit names and verifies a matching DatabaseDescriptor.
        /// </summary>
        /// <param name="databaseName">Reference to database name; may be modified if implicit resolution occurs.</param>
        /// <returns>Connection string that can be used to open a Sqlite connection.</returns>
        /// <exception cref="SxmException">Thrown when no DatabaseDescriptor exists for the requested name.</exception>
        internal static string GetConnectionString(ref string? databaseName)
        {
            string? connectionString = default(string);

            lock (_synchLock)
            {
                databaseName = SxmConnection.ResolveDatabaseName(databaseName);
                if (!_dbConnectionString.TryGetValue(databaseName, out connectionString))
                {
                    string databaseFolderPath = Environment.GetFolderPath(SxmDatabaseDescriptor.DatabaseFolder);
                    string pathToDatabase = Path.Combine(databaseFolderPath, databaseName);
                    connectionString = string.Format(_sqliteConnString, pathToDatabase);

                    _dbConnectionString.Add(databaseName, connectionString);
                }
            }

            return connectionString;
        }

        /// <summary>
        /// Acquire the async lock. When using a shared connection callers SHOULD supply a stable
        /// <paramref name="ownerId"/> (Guid) so reentrancy and ownership checks work correctly.
        /// If <paramref name="ownerId"/> matches the current owner the reentrancy counter is incremented
        /// and the method returns true immediately.
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout in milliseconds to wait for the lock (default 100ms).</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting.</param>
        /// <param name="ownerId">Optional owner token to support reentrancy/ownership semantics.</param>
        /// <returns>True when the lock was acquired; false otherwise.</returns>
        internal async Task<bool> LockAsync(int millisecondsTimeout = 100, CancellationToken cancellationToken = default, Guid? ownerId = null)
        {
            try
            {
                // Fast path: if caller supplied an ownerId that already owns the lock, allow re-entrancy.
                if (ownerId.HasValue)
                {
                    lock (_ownerSync)
                    {
                        if (_lockOwner.HasValue && _lockOwner.Value == ownerId.Value)
                        {
                            // Re-entrant acquire
                            _lockReentrancy++;
                            return true;
                        }
                    }
                }

                if (_dbConn == null) return false;

                // Wait for the semaphore with timeout/cancellation.
                if (await _asyncLock.WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken).ConfigureFalse())
                {
                    lock (_ownerSync)
                    {
                        // Set owner (use provided ownerId if given; otherwise create a token for best-effort ownership).
                        _lockOwner = ownerId ?? Guid.NewGuid();
                        _lockReentrancy = 1;
                    }

                    // If underlying connection was in a bad state, attempt to repair it.
                    if (_dbConn.State == System.Data.ConnectionState.Broken)
                    {
                        try
                        {
                            _dbConn.Close();
                            _dbConn.Open();
                        }
                        catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                        {
                            ReleaseLock(_lockOwner);
                            SxmLogging.Log(ex, "LockAsync failure.");
                            // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                            throw;
                        }
                        catch (System.Exception ex)
                        {
                            ReleaseLock(_lockOwner);

                            string errStr = "LockAsync failure.";
                            SxmLogging.Log(ex, errStr);
                            throw ExceptionHelper.Wrap(ex, errStr);
                        }
                    }

                    return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            return false;
        }

        /// <summary>
        /// Release the async lock. If <paramref name="ownerId"/> is supplied, ownership is verified before releasing.
        /// Reentrancy count is decremented and the underlying semaphore is released only when the counter reaches zero.
        /// </summary>
        /// <param name="ownerId">Optional owner token used to verify ownership before releasing the lock.</param>
        internal void ReleaseLock(Guid? ownerId = null)
        {
            try
            {
                lock (_ownerSync)
                {
                    // Nothing to release
                    if (!_lockOwner.HasValue)
                        return;

                    // If caller provided ownerId and it doesn't match, log and ignore release attempt.
                    if (ownerId.HasValue && _lockOwner.Value != ownerId.Value)
                    {
                        SxmLogging.Log(new InvalidOperationException("Attempt to release lock by non-owner."));
                        throw new InvalidOperationException("Attempt to release lock by non-owner.");
                    }

                    // Decrement reentrancy and only release semaphore when 0.
                    _lockReentrancy--;
                    if (_lockReentrancy <= 0)
                    {
                        _lockReentrancy = 0;
                        _lockOwner = null;
                        try
                        {
                            _asyncLock.Release();
                        }
                        catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                        {
                            SxmLogging.Log(ex, "ReleaseLock failure.");
                            // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                            throw;
                        }
                        catch (System.Exception ex)
                        {
                            string errStr = "ReleaseLock failure.";
                            SxmLogging.Log(ex, errStr);
                            throw ExceptionHelper.Wrap(ex, errStr);
                        }
                    }

                    return;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, "ReleaseLock failure.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = "ReleaseLock failure.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        private static string ResolveDatabaseName(string? databaseName)
        {
            if (databaseName == null)
            {
                databaseName = SxmDatabaseDescriptor.DefaultDatabase;
            }
            else
            {
                // Check if database name is in the list of databases.
                if(!SxmDatabaseDescriptor.IsDatabaseDefined(databaseName))
                    throw new InvalidDataException($"The database '{databaseName}' has not been configured. Check the spelling matches the database name in your SQL statements file.");
            }

            return databaseName!;
        }

        /// <summary>
        /// Release the connection resources. If <paramref name="destroy"/> is true or the connection is not shared,
        /// the underlying connection is closed and disposed. This method attempts to rollback any pending transaction
        /// without throwing to preserve the no-throw guarantee for cleanup.
        /// </summary>
        /// <param name="destroy">Force destruction of the underlying connection (default false).</param>
        public void ReleaseConnection(bool destroy = false)
        {
            if (_dbConn != null)
            {
                try
                {
                    if (_dbConnTransaction != null)
                        // ensure rollback is completed; block here to preserve previous behavior
                        DoCommitAsync(SQLiteXM.SxmDefines.RollbackTransaction).GetAwaiter().GetResult();
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    SxmLogging.Log(ex, $"ReleaseConnection failure for database '{this._databaseName}'.");
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    string errStr = $"ReleaseConnection failure for database '{this._databaseName}'.";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
                finally
                {
                    try
                    {
                        if (!_shared || destroy == true)
                            DestroyConnection();
                        else
                            ReleaseConnectionResources();
                    }
                    catch (System.Exception ex) 
                    {
                        string errStr = $"ReleaseConnection failure destroying or releasing connection for database '{this._databaseName}' shared '{_shared}' destroy '{destroy}'.";
                        SxmLogging.Log(ex);
                        throw ExceptionHelper.Wrap(ex, errStr); ;
                    }
                }
            }
        }

        /// <summary>
        /// Async implementation of finishing a transaction. Returns a <see cref="SQLiteErrorCode"/>.
        /// </summary>
        /// <param name="commitFlag">True to commit; false to rollback.</param>
        /// <returns>SQLiteErrorCode representing the operation result.</returns>
        internal async Task<SQLiteErrorCode> FinishTransactionAsync(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (_dbConn != null && _dbConnTransaction != null)
                sqLiteErrorCode = await DoCommitAsync(commitFlag).ConfigureFalse();

            return sqLiteErrorCode;
        }

        // Async doCommit using async ADO APIs
        private async Task<SQLiteErrorCode> DoCommitAsync(bool commitFlag)
        {
            SQLiteErrorCode sqLiteErrorCode = SQLiteErrorCode.Ok;

            if (_dbConnTransaction != null)
            {
                try
                {
                    if (commitFlag == SQLiteXM.SxmDefines.CommitTransaction)
                        await _dbConnTransaction.CommitAsync().ConfigureFalse();
                    else
                        await _dbConnTransaction.RollbackAsync().ConfigureFalse();

                    _dbConnTransaction = default(Microsoft.Data.Sqlite.SqliteTransaction);
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    SxmLogging.Log(ex, $"DoCommitAsync failure for database '{this._databaseName}' commit flag '{commitFlag}'.");
                    //if (ex.ErrorCode == SQLiteErrorCode.Busy) {/* May do something here.*/}

                    if (commitFlag == SQLiteXM.SxmDefines.CommitTransaction)
                        sqLiteErrorCode = (SQLiteErrorCode)ex.ErrorCode;
                    else
                        throw new SxmException(ex);
                }
                catch (System.Exception ex)
                {
                    string errStr = $"DoCommitAsync failure for database '{this._databaseName}' commit flag '{commitFlag}'.";
                    SxmLogging.Log(ex, errStr);
                    throw new SxmException(ex);
                }
            }

            return sqLiteErrorCode;
        }

        // Keep old synchronous doCommit for compatibility (rarely used directly)
        private SQLiteErrorCode DoCommit(bool commitFlag)
        {
            return DoCommitAsync(commitFlag).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Immediately closes and disposes the underlying connection and related resources.
        /// After this call the instance will no longer hold an open SqliteConnection.
        /// </summary>
        public void DestroyConnection()
        {
            if (_dbConn != null)
            {
                ReleaseConnectionResources();

                _dbConn.Close();
                _dbConn.Dispose();
                _dbConn = default(Microsoft.Data.Sqlite.SqliteConnection);
            }
        }

        private void ReleaseConnectionResources()
        {
            if (_connCommand != null)
            {
                ReleaseDataReader();
                _connCommand.Dispose();
                _connCommand = default(DbCommand);
            }
        }

        private void ReleaseDataReader()
        {
            if (_connDataReader != null && _connDataReader.IsClosed == false)
            {
                _connDataReader.Close();
                _connDataReader = default(DbDataReader);
            }
        }

        /// <summary>
        /// Execute a query and prepare an open data reader for subsequent row access.
        /// Caller should use <see cref="NextRow"/> / <see cref="GetNextRow{T}"/> / <see cref="GetValue(string)"/> to read results.
        /// </summary>
        /// <param name="command">SQL text to execute.</param>
        /// <param name="parameterValues">Optional parameter values (see internal parameter handling).</param>
        /// <param name="cancellationToken">Token used to cancel the async execution.</param>
        /// <exception cref="SxmException">Thrown for invalid SQL or provider errors.</exception>
        internal async Task ExecuteQueryAsync(string command, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
                throw new SxmException(SxmErrorMessages.Errors[SxmDefines.SxmErrorCode.MissingSQL]);

            try
            {
                if (_connCommand == null)
                    _connCommand = _dbConn.CreateCommand();
                else
                    ReleaseDataReader();

                _connCommand.CommandText = command;
                _connCommand.CommandType = System.Data.CommandType.Text;
                AddCommandParameters(parameterValues);

                if (_connCommand is DbCommand dbCmd)
                {
                    _connDataReader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureFalse();
                }
                else
                {
                    // Fallback to sync if something unexpected: keep behavior but log
                    _connDataReader = _connCommand.ExecuteReader();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"ExecuteQueryAsync failure for database '{this._databaseName}' and command '{command}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteQueryAsync failure for database '{this._databaseName}' and command '{command}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Synchronous wrapper for <see cref="ExecuteQueryAsync(string, List{object}?, CancellationToken)"/>.
        /// </summary>
        /// <param name="command">SQL text to execute.</param>
        /// <param name="parameterValues">Optional parameter values.</param>
        internal void ExecuteQuery(string command, List<object>? parameterValues)
        {
            ExecuteQueryAsync(command, parameterValues).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Execute a command that does not return rows (INSERT/UPDATE/DELETE) asynchronously.
        /// </summary>
        /// <param name="command">SQL text to execute.</param>
        /// <param name="parameterValues">Optional parameter values.</param>
        /// <param name="cancellationToken">Token used to cancel the async execution.</param>
        /// <exception cref="SxmException">Thrown for invalid SQL or provider errors.</exception>
        internal async Task ExecuteNonQueryAsync(string command, List<object>? parameterValues, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
                throw new SxmException(SxmErrorMessages.Errors[SxmDefines.SxmErrorCode.MissingSQL]);

            try
            {
                if (_connCommand == null)
                    _connCommand = _dbConn.CreateCommand();
                else
                    if (command.StartsWith("DELETE FROM companyReg WHERE companyRegPK") == false)
                        ReleaseDataReader();

                _connCommand.CommandText = command;
                _connCommand.CommandType = System.Data.CommandType.Text;
                AddCommandParameters(parameterValues);

                if (_connCommand is DbCommand dbCmd)
                {
                    await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureFalse();
                }
                else
                {
                    // Fallback synchronous
                    _connCommand.ExecuteNonQuery();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"ExecuteNonQueryAsync failure for database '{this._databaseName}' and command '{command}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteNonQueryAsync failure for database '{this._databaseName}' and command '{command}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Synchronous wrapper for <see cref="ExecuteNonQueryAsync(string, List{object}?, CancellationToken)"/>.
        /// </summary>
        /// <param name="command">SQL text to execute.</param>
        /// <param name="parameterValues">Optional parameter values.</param>
        internal void ExecuteNonQuery(string command, List<object>? parameterValues)
        {
            ExecuteNonQueryAsync(command, parameterValues).GetAwaiter().GetResult();
        }

        private void AddCommandParameters(List<object>? parameterValues)
        {
            _connCommand.Parameters.Clear();

            if (parameterValues != null)
            {
                DbParametersDataType dbParametersDataType = GetDbParameterType(ref parameterValues);

                if (dbParametersDataType == DbParametersDataType.Dictionary)
                {
                    Dictionary<string, object>? dict = (Dictionary<string, object>?)parameterValues[0];
                    if (dict != default)
                    {
                        foreach (KeyValuePair<string, object> kvp in dict)
                        {
                            DbParameter dbParameter = _connCommand.CreateParameter();

                            dbParameter.ParameterName = "@" + kvp.Key;
                            dbParameter.Value = kvp.Value;

                            _connCommand.Parameters.Add(dbParameter);
                        }
                    }

                    return;
                }

                if (dbParametersDataType == DbParametersDataType.List)
                {
                    int counter = 0;

                    foreach (object parameterValue in parameterValues)
                    {
                        DbParameter dbParameter = _connCommand.CreateParameter();

                        dbParameter.Value = parameterValue;
                        dbParameter.ParameterName = "@p" + counter.ToString();

                        _connCommand.Parameters.Add(dbParameter);

                        ++counter;
                    }
                }
            }
        }

        private DbParametersDataType GetDbParameterType(ref List<object> parameterValues)
        {
            Type? parameterValueType = parameterValues[0]?.GetType();

            if (parameterValueType == typeof(Tuple<string, object>))
                return DbParametersDataType.TupleList;

            if (parameterValueType == typeof(object[]))
                return DbParametersDataType.OneDArray;

            if (parameterValueType == typeof(object[,]))
                return DbParametersDataType.TwoDArray;

            if (parameterValueType == typeof(Hashtable))
                return DbParametersDataType.HashTable;

            if (parameterValueType == typeof(Dictionary<string, object>))
                return DbParametersDataType.Dictionary;

            return DbParametersDataType.List;
        }

        /// <summary>
        /// Begin a database transaction. Transaction support is synchronous for compatibility.
        /// </summary>
        /// <exception cref="SxmException">Wraps provider exceptions thrown while beginning a transaction.</exception>
        internal void BeginTransaction()
        {
            try
            {
                if (_dbConnTransaction == null)
                {
                    _dbConnTransaction = _dbConn.BeginTransaction();
                    if (_connCommand == null)
                        _connCommand = _dbConn.CreateCommand();
                    _connCommand.Transaction = _dbConnTransaction;
                }

            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                if (ex.ErrorCode == (int)SQLiteErrorCode.Busy)
                {
                    SxmLogging.Log(ex);
                }
                throw new SxmException(ex);
            }
        }

        /// <summary>
        /// Indicates whether the last executed query has row results available.
        /// </summary>
        /// <returns>True if a data reader is present and has rows; otherwise false.</returns>
        public bool HasRows()
        {
            if (_connDataReader != null)
                return _connDataReader.HasRows;

            return false;
        }

        /// <summary>
        /// Get the value of the named field on the current row.
        /// Returns null (default) if the field is not present or no current row.
        /// </summary>
        /// <param name="fieldName">Name of the field/column to retrieve.</param>
        /// <returns>Field value or null if not available.</returns>
        internal object? GetValue(string fieldName)
        {
            try
            {
                if (HasRows() == true)
                {
                    int ordinal = _connDataReader.GetOrdinal(fieldName);
                    return _connDataReader.GetValue(ordinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetValue failure for database '{this._databaseName}' and field name '{fieldName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetValue failure for database '{this._databaseName}' and field name '{fieldName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return default;
        }

        /// <summary>
        /// Get the value of the field at the specified ordinal on the current row.
        /// </summary>
        /// <param name="fieldOrdinal">Zero-based column ordinal.</param>
        /// <returns>Field value or null if not available.</returns>
        internal object? GetValue(int fieldOrdinal)
        {
            try
            {
                if (HasRows() == true)
                    return _connDataReader.GetValue(fieldOrdinal);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetValue failure for database '{this._databaseName}' and field ordinal '{fieldOrdinal}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetValue failure for database '{this._databaseName}' and field ordinal '{fieldOrdinal}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return default;
        }

        /// <summary>
        /// Return the field name for the given ordinal in the current resultset.
        /// </summary>
        /// <param name="fieldOrdinal">Zero-based column ordinal.</param>
        /// <returns>Column name or null if not available.</returns>
        internal string? GetFieldName(int fieldOrdinal)
        {
            try
            {
                if (HasRows() == true)
                    return _connDataReader.GetName(fieldOrdinal);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetFieldName failure for database '{this._databaseName}' and field ordinal '{fieldOrdinal}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetFieldName failure for database '{this._databaseName}' and field ordinal '{fieldOrdinal}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return default;
        }

        /// <summary>
        /// Return all field names for the current resultset.
        /// </summary>
        /// <returns>Array of field names. Empty array if no rows are available.</returns>
        internal string[] GetFieldNames()
        {
            string[] fieldNames;

            if (HasRows() == true)
            {
                fieldNames = new string[_connDataReader.FieldCount];
                for (int i = 0; i < _connDataReader.FieldCount; i++)
                    fieldNames[i] = _connDataReader.GetName(i);
            }
            else
                fieldNames = new string[0];

            return fieldNames;
        }
        /// <summary>
        /// Read the next row and map it into a dictionary-like instance of <typeparamref name="T"/>.
        /// The returned dictionary keys are column names and values are the column values.
        /// </summary>
        /// <typeparam name="T">An IDictionary&lt;string, object?&gt; implementation with a public parameterless constructor.</typeparam>
        /// <returns>A populated instance of <typeparamref name="T"/> for the next row, or null if no more rows.</returns>
        internal T? GetNextRow<T>() where T : IDictionary<string, object?>, new()
        {
            T? row = default(T);

            if (NextRow() == true)
            {
                row = new T();
                int numColumns = GetColumnCount();
                for (int i = 0; i < numColumns; i++)
                {
                    object columnValue = _connDataReader.GetValue(i);
                    //Type type = columnValue.GetType();
                    row.Add(_connDataReader.GetName(i), columnValue == DBNull.Value ? default : columnValue);
                }
            }

            return row;
        }

        /// <summary>
        /// Return the number of columns in the current resultset.
        /// </summary>
        /// <returns>Number of columns or zero if no resultset is present.</returns>
        internal int GetColumnCount()
        {
            if (HasRows() == true)
                return _connDataReader.FieldCount;

            return 0;
        }

        /// <summary>
        /// Advance the reader to the next row. If no more rows are available the data reader is released.
        /// </summary>
        /// <returns>True if another row is available; otherwise false.</returns>
        internal bool NextRow()
        {
            bool anotherRow = false;

            if (HasRows() == true)
            {
                anotherRow = _connDataReader.Read();
                if (anotherRow == false)
                    ReleaseDataReader();
            }

            return anotherRow;
        }

        /// <summary>
        /// Return the CLR <see cref="Type"/> of the specified column by name in the current resultset.
        /// </summary>
        /// <param name="fieldName">Column name.</param>
        /// <returns>CLR type for the column, or null if not available.</returns>
        internal Type? GetType(string fieldName)
        {
            try
            {
                if (HasRows() == true)
                {
                    int ordinal = _connDataReader.GetOrdinal(fieldName);
                    return _connDataReader.GetFieldType(ordinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetType failure for database '{this._databaseName}' and field name '{fieldName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetType failure for database '{this._databaseName}' and field name '{fieldName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return default;
        }
    }
}