using Microsoft.Data.Sqlite;
using System.Collections;
using System.Data.Common;

namespace SQLiteXM
{
    /// <summary>
    /// Common SQLite error codes returned by the underlying provider.
    /// Matches SQLite's native result codes for conversion into the library's API.
    /// </summary>
    internal enum SQLiteErrorCode
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
        /// The owner token associated with this lease.
        /// </summary>
        Guid OwnerId { get; }
    }


    /// <summary>
    /// Lightweight connection wrapper around <c>Microsoft.Data.Sqlite.SqliteConnection</c>.
    /// Provides convenience APIs for shared/non-shared connections, parameter handling,
    /// transaction management and simple reader helpers used throughout SQLiteXM.
    /// </summary>
    internal class SxmConnection
    {
        // true => connection is shared / reused across callers
        // false => connection is non-shared / private to the creator
        private bool _shared;
        /// <summary>
        /// Indicates whether the underlying connection is shared (true) or private (false).
        /// Shared connections may be reused across callers and support reentrant locking via owner tokens.
        /// </summary>
        internal bool Shared => _shared;

        private string? _databaseName;
        /// <summary>
        /// The resolved database name for this connection instance.
        /// Can be null for an implicit single-descriptor scenario.
        /// </summary>
        internal string? DatabaseName
        {
            get { return _databaseName; }
        }

        private DbCommand? _connCommand;
        private DbDataReader? _connDataReader;
        private Microsoft.Data.Sqlite.SqliteConnection? _sqliteConnection;
        private Microsoft.Data.Sqlite.SqliteTransaction? _dbConnTransaction;
        private bool _hasCurrentRow;

        /// <summary>
        /// The underlying provider connection. Internal so other library layers (e.g. the LINQ
        /// context) can share the same physical connection without exposing it publicly.
        /// </summary>
        internal Microsoft.Data.Sqlite.SqliteConnection? UnderlyingConnection => _sqliteConnection;

        /// <summary>
        /// The currently open SQLite transaction on this connection, or null when none is open.
        /// Internal so other library layers can enlist commands in the same transaction.
        /// </summary>
        internal Microsoft.Data.Sqlite.SqliteTransaction? CurrentTransaction => _dbConnTransaction;

        private static readonly object _synchLock = new object();

        // Semaphore used to guard concurrent access. Use ownership + reentrancy to avoid accidentally
        // releasing someone else's lock and to allow a logical owner to re-enter.
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private readonly object _ownerSync = new object();
        private readonly SemaphoreSlim _connectionGate = new SemaphoreSlim(1, 1);
        private Guid? _lockOwner;
        private int _lockReentrancy = 0;

        private static readonly Dictionary<string, string> _dbConnectionString = new Dictionary<string, string>();

        // Lifecycle gate: when true, new connection creation is blocked
        private static volatile bool _lifecycleGateClosed = false;
        internal static bool IsLifecycleGateClosed => _lifecycleGateClosed;

        internal static void BlockNewOperations()
        {
            _lifecycleGateClosed = true;
        }

        internal static void AllowNewOperations()
        {
            _lifecycleGateClosed = false;
        }

#if DEBUG
        /// <summary>
        /// Clears the connection string cache for testing purposes.
        /// Must be called when tests change database folder paths between test runs.
        /// </summary>
        internal static void ResetForTesting()
        {
            lock (_synchLock)
            {
                _dbConnectionString.Clear();
            }
        }
#endif

        private enum DbParametersDataType { List, TupleList, TwoDArray, OneDArray, HashTable, Dictionary }

        /// <summary>
        /// Create a new SxmConnection for the specified databaseName.
        /// If <paramref name="shared"/> is true the connection may be reused across callers.
        /// Internal: connections are managed by the library; consumers use <see cref="SxmDbContext"/>.
        /// Throws <see cref="SxmException"/> on initialization failures.
        /// </summary>
        /// <param name="databaseName">Name of the database file (or null to use implicit name).</param>
        /// <param name="shared">Whether the connection is shared/reused (default false).</param>
        internal SxmConnection(string? databaseName, bool shared = false)
        {
            // Check lifecycle gate first
            if (_lifecycleGateClosed)
            {
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded, databaseName));
            }

            try
            {
                this._databaseName = databaseName;
                this._shared = shared;

                CreateNewConnection(ref this._databaseName, ref _sqliteConnection);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                DestroyConnectionCore();

                SxmLogging.Log(ex, $"Connection failure. Database: '{this._databaseName}'. Shared: '{this._shared}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                DestroyConnectionCore();

                string errStr = $"Connection failure. Database: '{this._databaseName}'. Shared: '{this._shared}'.";
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
            private int _disposed;

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
                // Thread-safe disposal using Interlocked to prevent double-release in concurrent scenarios.
                // While dispose should typically be single-threaded per pattern guidelines, this ensures
                // correctness if callers accidentally dispose the same lease concurrently.
                if (System.Threading.Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    _connection.ReleaseLock(OwnerId);
                }

                return ValueTask.CompletedTask;
            }
        }

        internal static void CreateNewConnection(ref string? databaseName, ref Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection)
        {
            try
            {
                string? connectionString = SxmConnection.GetConnectionString(ref databaseName);
                sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
                sqliteConnection.Open();

                SxmDatabaseOptions.ConnectionOpened(sqliteConnection, databaseName);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"CreateNewConnection failure. Database: '{databaseName ?? "null"}'. {ex.Message}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"CreateNewConnection failure. Database: '{databaseName ?? "null"}'. {ex.Message}";
                SxmLogging.Log(ex);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        internal static void CloseConnection(Microsoft.Data.Sqlite.SqliteConnection? sqliteConnection, string? databaseName)
        {
            if (sqliteConnection == null)
                return;

            SxmDatabaseOptions.ConnectionClosing(sqliteConnection, databaseName);
            sqliteConnection?.Dispose();  // Closes the connection.
            SxmDatabaseOptions.ConnectionClosed(databaseName);
        }

        /// <summary>
        /// Build or retrieve a cached connection string for the given database name.
        /// This method resolves implicit names and verifies a matching DatabaseDescriptor.
        /// </summary>
        /// <param name="databaseName">Reference to database name; may be modified if implicit resolution occurs.</param>
        /// <returns>Connection string that can be used to open a Sqlite connection.</returns>
        /// <exception cref="SxmException">Thrown when no DatabaseDescriptor exists for the requested name.</exception>
        internal static string? GetConnectionString(ref string? databaseName)
        {
            string? connectionString = default(string);

            lock (_synchLock)
            {
                databaseName = SxmConnection.ResolveDatabaseName(databaseName);
                if (!_dbConnectionString.TryGetValue(databaseName, out connectionString))
                {
                    string? databaseFolderPath = SxmDatabaseDescriptor.DatabaseFolder;
                    if (databaseFolderPath == null)
                        throw new InvalidOperationException("Database folder path is not configured.");

                    SqliteConnectionStringBuilder csb = new SqliteConnectionStringBuilder()
                    {
                        DataSource = Path.Combine(databaseFolderPath, databaseName),
                        Mode = SqliteOpenMode.ReadWriteCreate,
                        DefaultTimeout = SxmDatabaseOptions.GetDefaultTimeout() ?? new SqliteConnection().DefaultTimeout,
                        Pooling = SxmDatabaseOptions.IsConnectionPoolingEnabled()
                    };
                    connectionString = csb.ToString();

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

                if (_sqliteConnection == null) return false;

                // Wait for the semaphore with timeout/cancellation.
                TimeSpan timeout = millisecondsTimeout == -1 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(millisecondsTimeout);
                if (await _asyncLock.WaitAsync(timeout, cancellationToken).ConfigureFalse())
                {
                    lock (_ownerSync)
                    {
                        // Set owner (use provided ownerId if given; otherwise create a token for best-effort ownership).
                        _lockOwner = ownerId ?? Guid.NewGuid();
                        _lockReentrancy = 1;
                    }

                    // If underlying connection was in a bad state, attempt to repair it.
                    if (_sqliteConnection.State == System.Data.ConnectionState.Broken)
                    {
                        try
                        {
                            DestroyConnectionCore();
                            CreateNewConnection(ref this._databaseName, ref _sqliteConnection);
                        }
                        catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                        {
                            try { DestroyConnectionCore(); } catch (System.Exception) { }
                            try { ReleaseLock(_lockOwner); } catch (System.Exception) { }

                            SxmLogging.Log(ex, $"LockAsync failure. Database: '{_databaseName}'.");
                            // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                            throw;
                        }
                        catch (System.Exception ex)
                        {
                            try { DestroyConnectionCore(); } catch (System.Exception) { }
                            try { ReleaseLock(_lockOwner); } catch (System.Exception) { }

                            string errStr = $"LockAsync failure. Database: '{_databaseName}'.";
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

                    // If caller provided ownerId and it doesn't match, log and throw.
                    if (ownerId.HasValue && _lockOwner.Value != ownerId.Value)
                    {
                        SxmLogging.Log(new InvalidOperationException($"Attempt to release lock by non-owner. Database: '{_databaseName}'."));
                        throw new InvalidOperationException($"Attempt to release lock by non-owner. Database: '{_databaseName}'.");
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
                            SxmLogging.Log(ex, $"ReleaseLock failure. Database: '{_databaseName}'.");
                            // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                            throw;
                        }
                        catch (System.Exception ex)
                        {
                            string errStr = $"ReleaseLock failure. Database: '{_databaseName}'.";
                            SxmLogging.Log(ex, errStr);
                            throw ExceptionHelper.Wrap(ex, errStr);
                        }
                    }

                    return;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"ReleaseLock failure. Database: '{_databaseName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ReleaseLock failure. Database: '{_databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        private static string ResolveDatabaseName(string? databaseName)
        {
            if (databaseName == null)
            {
                if (SxmDatabaseDescriptor.DefaultDatabase == null)
                    throw new InvalidDataException(
                        "Error: No default database is defined in any of your SqlStatements configuration files. " +
                        "Fix: Set 'isDefault' to 'true' in one of your SQL statements configuration files.");

                databaseName = SxmDatabaseDescriptor.DefaultDatabase;
            }
            else
            {
                // Check if database name is in the list of databases.
                if (!SxmDatabaseDescriptor.IsDatabaseDefined(databaseName))
                    throw new InvalidDataException($"The database '{databaseName}' has not been configured. Check the spelling matches the database name in your SQL statements file.");
            }

            return databaseName!;
        }

        /// <summary>
        /// Release the connection resources asynchronously. If <paramref name="destroy"/> is true or the connection is not shared,
        /// the underlying connection is closed and disposed.
        /// </summary>
        /// <param name="destroy">Force destruction of the underlying connection (default false).</param>
        /// <param name="ct">Cancellation token used to abort waiting for the connection gate.</param>
        /// <returns>A task that completes when the connection is released.</returns>
        public async Task ReleaseConnectionAsync(bool destroy = false, CancellationToken ct = default)
        {
            await _connectionGate.WaitAsync(ct).ConfigureFalse();

            try
            {
                if (_sqliteConnection != null)
                {
                    bool destroyTheConnection = !_shared || destroy ? true : false;

                    try
                    {
                        if (_dbConnTransaction != null)
                            // This appears to always rollback when the connection is released. But a successful transaction will have been committed before reaching
                            // this method, so, if the transaction was a success, the transaction will have already been committed and this rollback will have no affect.
                            // If the transaction was a failure, the commit will not have been executed and this rollback will, in fact, rollback the transaction.
                            await DoCommitAsync(SQLiteXM.SxmDefines.RollbackTransaction).ConfigureFalse();
                    }
                    catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                    {
                        SxmLogging.Log(ex, $"ReleaseConnection failure. Database: '{this._databaseName}'.");
                        // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                        throw;
                    }
                    catch (System.Exception ex)
                    {
                        string errStr = $"ReleaseConnection failure. Database: '{this._databaseName}'.";
                        SxmLogging.Log(ex, errStr);
                        throw ExceptionHelper.Wrap(ex, errStr);
                    }
                    finally
                    {
                        try
                        {
                            if (destroyTheConnection)
                                DestroyConnectionCore();
                            else
                                ReleaseConnectionResources();
                        }
                        catch (System.Exception ex)
                        {
                            string errStr = $"ReleaseConnection failure destroying or releasing connection. Database: '{this._databaseName}'. Shared: '{_shared}'. Destroy: '{destroy}'.";
                            SxmLogging.Log(ex, errStr);
                            throw ExceptionHelper.Wrap(ex, errStr);
                        }
                    }
                }
            }
            finally
            {
                _connectionGate.Release();
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

            if (_sqliteConnection != null && _dbConnTransaction != null)
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
                    if (_connCommand != null)
                        _connCommand.Transaction = default(Microsoft.Data.Sqlite.SqliteTransaction);
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex)
                {
                    string errStr = $"DoCommitAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}Commit flag: '{commitFlag}'.";
                    SxmLogging.Log(ex, errStr);

                    if (commitFlag == SQLiteXM.SxmDefines.CommitTransaction)
                        sqLiteErrorCode = (SQLiteErrorCode)ex.ErrorCode;
                    else
                        throw ExceptionHelper.Wrap(ex, errStr);
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    SxmLogging.Log(ex, $"DoCommitAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}Commit flag: '{commitFlag}'.");
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    string errStr = $"DoCommitAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}Commit flag: '{commitFlag}'.";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }

            return sqLiteErrorCode;
        }

        /// <summary>
        /// Immediately closes and disposes the underlying connection and related resources.
        /// After this call the instance will no longer hold an open SqliteConnection.
        /// </summary>
        /// <param name="ct">Cancellation token used to abort waiting for the connection gate.</param>
        /// <returns>A task that completes when the connection is destroyed.</returns>
        public async Task DestroyConnectionAsync(CancellationToken ct = default)
        {
            await _connectionGate.WaitAsync(ct).ConfigureFalse();
            try
            {
                DestroyConnectionCore();
            }
            finally
            {
                _connectionGate.Release();
            }
        }

        private void DestroyConnectionCore()
        {
            try
            {
                if (_sqliteConnection != null)
                {
                    ReleaseConnectionResources();
                    CloseConnection(_sqliteConnection, _databaseName);
                }
            }
            finally
            {
                _dbConnTransaction = default(Microsoft.Data.Sqlite.SqliteTransaction);
                _sqliteConnection = default(Microsoft.Data.Sqlite.SqliteConnection);
            }
        }

        /// <summary>
        /// Synchronous, best-effort cleanup for a private (non-shared) connection whose owning
        /// transaction/context failed during construction. Rolls back any open SQLite transaction
        /// and destroys the connection. Intended only for factory failure paths where no async
        /// context is available; SQLite operations are synchronous under the hood so this does
        /// not block on genuinely asynchronous work.
        /// </summary>
        internal void CleanupFailedCreate()
        {
            _connectionGate.Wait();
            try
            {
                if (_dbConnTransaction != null)
                {
                    try { _dbConnTransaction.Rollback(); }
                    catch (System.Exception ex) { SxmLogging.Log(ex, $"CleanupFailedCreate rollback failure. Database: '{this._databaseName}'."); }
                }

                DestroyConnectionCore();
            }
            finally
            {
                _connectionGate.Release();
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

            _hasCurrentRow = false;
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
                {
                    if (_sqliteConnection == null)
                        throw new InvalidOperationException("SQLite connection is not available.");
                    _connCommand = _sqliteConnection.CreateCommand();
                }
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

                _hasCurrentRow = false;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"ExecuteQueryAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {command}");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteQueryAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {command}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
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
                {
                    if (_sqliteConnection == null)
                        throw new InvalidOperationException("SQLite connection is not available.");
                    _connCommand = _sqliteConnection.CreateCommand();
                }

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
                SxmLogging.Log(ex, $"ExecuteNonQueryAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {command}");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ExecuteNonQueryAsync failure. Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {command}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        private void AddCommandParameters(List<object>? parameterValues)
        {
            if (_connCommand == null)
                throw new InvalidOperationException("Database command is not initialized.");

            _connCommand.Parameters.Clear();

            if (parameterValues != null && parameterValues.Count > 0)
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
                            dbParameter.Value = kvp.Value ?? DBNull.Value;

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

                        dbParameter.Value = parameterValue ?? DBNull.Value;
                        dbParameter.ParameterName = "@p" + counter.ToString();

                        _connCommand.Parameters.Add(dbParameter);

                        ++counter;
                    }
                }
            }
        }

        /// <summary>
        /// Determines the parameter container type used by the first element of <paramref name="parameterValues"/>.
        /// Currently supports <see cref="DbParametersDataType.List"/> and <see cref="DbParametersDataType.Dictionary"/>.
        /// </summary>
        /// <param name="parameterValues">The parameter container list to inspect.</param>
        /// <returns>The detected <see cref="DbParametersDataType"/>.</returns>
        private DbParametersDataType GetDbParameterType(ref List<object> parameterValues)
        {
            // TODO: Extend parameter binding to support tuple/array/hashtable parameter containers.
            // Type? parameterValueType = parameterValues[0]?.GetType();
            // 
            // if (parameterValueType == typeof(Tuple<string, object>))
            //     return DbParametersDataType.TupleList;
            //
            // if (parameterValueType == typeof(object[]))
            //     return DbParametersDataType.OneDArray;
            //
            // if (parameterValueType == typeof(object[,]))
            //     return DbParametersDataType.TwoDArray;
            //
            // if (parameterValueType == typeof(Hashtable))
            //     return DbParametersDataType.HashTable;

            if (parameterValues[0] is System.Collections.Generic.IDictionary<string, object>)
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
                    if (_sqliteConnection == null)
                        throw new InvalidOperationException("SQLite connection is not available.");

                    _dbConnTransaction = _sqliteConnection.BeginTransaction();
                    if (_connCommand == null)
                        _connCommand = _sqliteConnection.CreateCommand();

                    _connCommand.Transaction = _dbConnTransaction;
                }

            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"BeginTransaction failure. Database: '{this._databaseName}'.{Environment.NewLine}Error code: '{(ex is Microsoft.Data.Sqlite.SqliteException s ? s.ErrorCode : 0)}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"BeginTransaction failure. Database: '{this._databaseName}'.{Environment.NewLine}Error code: '{(ex is Microsoft.Data.Sqlite.SqliteException s ? s.ErrorCode : 0)}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Indicates whether the last executed query has row results available.
        /// </summary>
        /// <returns>True if a data reader is present and has rows; otherwise false.</returns>
        internal bool HasRows()
        {
            if (_connDataReader != null)
                return _connDataReader!.HasRows;

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
                if (HasRows() && _hasCurrentRow)
                {
                    if (_connDataReader == null)
                        throw new InvalidOperationException("Data reader is not available.");

                    int ordinal = _connDataReader.GetOrdinal(fieldName);
                    return _connDataReader.GetValue(ordinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetValue failure. Database: '{this._databaseName}'. Field name: '{fieldName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetValue failure. Database: '{this._databaseName}'. Field name: '{fieldName}'.";
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
                if (HasRows() && _hasCurrentRow)
                {
                    if (_connDataReader == null)
                        throw new InvalidOperationException("Data reader is not available.");

                    return _connDataReader.GetValue(fieldOrdinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetValue failure. Database: '{this._databaseName}'. Field ordinal: '{fieldOrdinal}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetValue failure. Database: '{this._databaseName}'. Field ordinal: '{fieldOrdinal}'.";
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
                if (HasRows())
                {
                    if (_connDataReader == null)
                        throw new InvalidOperationException("Data reader is not available.");

                    return _connDataReader.GetName(fieldOrdinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetFieldName failure. Database: '{this._databaseName}'. Field ordinal: '{fieldOrdinal}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetFieldName failure. Database: '{this._databaseName}'. Field ordinal: '{fieldOrdinal}'.";
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

            if (HasRows())
            {
                if (_connDataReader == null)
                    throw new InvalidOperationException("Data reader is not available.");

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
                if (_connDataReader == null)
                    throw new InvalidOperationException("Data reader is not available.");

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
            if (HasRows())
            {
                if (_connDataReader == null)
                    throw new InvalidOperationException("Data reader is not available.");

                return _connDataReader.FieldCount;
            }

            return 0;
        }

        /// <summary>
        /// Advance the reader to the next row. If no more rows are available the data reader is released.
        /// </summary>
        /// <returns>True if another row is available; otherwise false.</returns>
        internal bool NextRow()
        {
            if (HasRows())
            {
                if (_connDataReader == null)
                    throw new InvalidOperationException("Data reader is not available.");

                if (!(_hasCurrentRow = _connDataReader.Read()))
                    ReleaseDataReader();
            }

            return _hasCurrentRow;
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
                if (HasRows() && _hasCurrentRow)
                {
                    if (_connDataReader == null)
                        throw new InvalidOperationException("Data reader is not available.");

                    int ordinal = _connDataReader.GetOrdinal(fieldName);
                    return _connDataReader.GetFieldType(ordinal);
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"GetType failure. Database: '{this._databaseName}'. Field name: '{fieldName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetType failure. Database: '{this._databaseName}'. Field name: '{fieldName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return default;
        }
    }
}