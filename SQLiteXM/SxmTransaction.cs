using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;

namespace SQLiteXM
{
    /// <summary>
    /// LINQ unit-of-work context with immediate execution and transactional semantics that mirror
    /// <see cref="SxmSqlTransaction"/>:
    /// <list type="bullet">
    /// <item><description>Write operations (bulk Update/Delete via LINQ) execute immediately inside a
    /// transaction that is started lazily on the first write ("least work" principle - read-only
    /// contexts never open a transaction).</description></item>
    /// <item><description>On <see cref="DisposeAsync"/> the transaction auto-commits when no operation
    /// has failed; if any operation threw, the transaction is rolled back.</description></item>
    /// <item><description><see cref="CommitTransactionAsync"/> may optionally be called to end the transaction early.
    /// Subsequent write operations start a new transaction.</description></item>
    /// <item><description><see cref="RollbackTransactionAsync"/> may optionally be called to discard the current
    /// transaction's work explicitly.</description></item>
    /// </list>
    /// Prefer <c>await using var ctx = new SxmTransaction();</c> so disposal can commit/rollback asynchronously.
    /// </summary>
    public class SxmTransaction : IDisposable, IAsyncDisposable
    {
        // Static registry to track DataConnection -> SxmTransaction mappings
        // This enables context recovery from IQueryable chains after LINQ operators like Where()
        private static readonly ConcurrentDictionary<DataConnection, WeakReference<SxmTransaction>> _contextRegistry
            = new ConcurrentDictionary<DataConnection, WeakReference<SxmTransaction>>();

        private bool _isDisposed = false;
        private readonly SxmConnection _sxmConnection;
        private readonly SxmSqlTransaction _sqlTransaction;
        private readonly bool _ownsTransaction;
        private LinqToDB.Data.DataConnection _linqToDbDataConnection;
        private Microsoft.Data.Sqlite.SqliteTransaction? _enlistedTransaction;
        private string? _databaseName;

        public SxmTransaction(string? databaseName = null)
        {
            SxmSqlTransaction? ownedTransaction = null;
            try
            {
                SxmDatabase.EnsureInitialized();

                SxmSqlTransaction? ambient = SxmAmbientTransaction.Current;
                if (ambient != null && ambient.Connection != null)
                {
                    // Join the existing ambient transaction so LINQ, entity writes and named SQL
                    // all execute on the same connection inside the same transaction.
                    if (databaseName != null && !string.Equals(databaseName, ambient.Connection.DatabaseName, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"An ambient transaction is active for database '{ambient.Connection.DatabaseName}'; " +
                            $"cannot create an SxmTransaction for database '{databaseName}'.");

                    _sqlTransaction = ambient;
                    _sxmConnection = ambient.Connection;
                    _ownsTransaction = false;
                }
                else
                {
                    // Create a private connection and register an ambient SxmSqlTransaction over it
                    // so SxmEntity.SaveAsync()/DeleteAsync() and named/embedded SQL enlist automatically.
                    ownedTransaction = SxmSqlTransaction.Create(databaseName);
                    _sqlTransaction = ownedTransaction;
                    _sxmConnection = ownedTransaction.Connection
                        ?? throw new InvalidOperationException("Failed to create SQLite connection.");
                    _ownsTransaction = true;
                }

                _databaseName = _sxmConnection.DatabaseName;

                // Begin a transaction lazily when none is open yet. This covers both owned
                // connections and freshly created ambient transactions (e.g. created via
                // SxmSqlTransaction.CreateAsync) that have not executed a statement yet.
                if (_sxmConnection.CurrentTransaction == null)
                    _sxmConnection.BeginTransaction();

                _linqToDbDataConnection = CreateLinqConnection();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                if (ownedTransaction != null) { try { ownedTransaction.Dispose(); } catch { /* best effort */ } }
                SxmLogging.Log(ex, $"SxmTransaction ctor failure. Database: '{databaseName}'.");
                // Cancellation/fatal - rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                if (ownedTransaction != null) { try { ownedTransaction.Dispose(); } catch { /* best effort */ } }
                string errStr = $"SxmTransaction ctor failure. Database: '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Creates a new <see cref="SxmTransaction"/> asynchronously by offloading the synchronous
        /// constructor to a background thread. Use this factory method in MAUI UI contexts where
        /// blocking the UI thread during transaction initialization (schema validation, callbacks, etc.)
        /// would cause responsiveness issues.
        /// </summary>
        /// <param name="databaseName">Optional database name. Uses the default database if null.</param>
        /// <param name="cancellationToken">Cancellation token to abort the initialization if needed. 
        /// The cancellation token can prevent initialization from starting if cancellation occurs before 
        /// the background operation begins. It cannot interrupt constructor work that has already started.</param>
        /// <returns>A task that completes with an initialized <see cref="SxmTransaction"/>.</returns>
        /// <remarks>
        /// For background/service code, prefer the synchronous constructor <c>new SxmTransaction(databaseName)</c>
        /// to avoid unnecessary thread pool scheduling overhead.
        /// </remarks>
        public static Task<SxmTransaction> CreateAsync(string? databaseName = null, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => new SxmTransaction(databaseName), cancellationToken);
        }

        /// <summary>
        /// Private ctor used by <see cref="CreateAsync"/>. Receives an already-resolved
        /// <see cref="SxmSqlTransaction"/> (connection lock acquired and ambient registered by the caller).
        /// </summary>
        private SxmTransaction(SxmSqlTransaction transaction, bool ownsTransaction)
        {
            _sqlTransaction = transaction;
            _sxmConnection = transaction.Connection
                ?? throw new InvalidOperationException("Failed to create SQLite connection.");
            _ownsTransaction = ownsTransaction;
            _databaseName = _sxmConnection.DatabaseName;

            // Begin a transaction lazily when none is open yet.
            if (_sxmConnection.CurrentTransaction == null)
                _sxmConnection.BeginTransaction();

            _linqToDbDataConnection = CreateLinqConnection();
        }

        /// <summary>
        /// Creates an <see cref="SxmTransaction"/> over a caller-supplied <see cref="SxmConnection"/>.
        /// For shared connections the connection lock is acquired asynchronously and held for the
        /// lifetime of the context.
        /// The context owns the transaction: it auto-commits on <see cref="DisposeAsync"/> when no
        /// operation failed, otherwise it rolls back.
        /// </summary>
        /// <param name="conn">An existing <see cref="SxmConnection"/> instance (e.g. supplied by
        /// <c>SxmConnectionManager.RunWorkersAsync</c>).</param>
        /// <param name="waitMilliseconds">Maximum time to wait for a shared connection lock (only used for shared connections).</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting for the lock.</param>
        /// <returns>An <see cref="SxmTransaction"/> that owns its transaction and, for shared connections, the connection lock.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="conn"/> is null.</exception>
        /// <exception cref="SxmException">Thrown when a shared connection lock cannot be acquired within the timeout.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an ambient transaction is already active;
        /// use <c>new SxmTransaction()</c> to join it instead.</exception>
        internal static Task<SxmTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            SxmDatabase.EnsureInitialized();

            // IMPORTANT: this method must NOT be 'async'. SxmSqlTransaction.CreateAsync registers
            // the ambient transaction (AsyncLocal) synchronously before its first await; that
            // registration only flows to the caller when no async state machine sits in between.
            Task<SxmSqlTransaction> transactionTask = SxmSqlTransaction.CreateAsync(conn, waitMilliseconds, cancellationToken);

            if (transactionTask.IsCompletedSuccessfully)
            {
                // Non-shared connections complete synchronously: build the context on the caller's
                // execution context so the ambient registration remains visible to the caller.
                SxmSqlTransaction sqlTransaction = transactionTask.Result;
                try
                {
                    return Task.FromResult(new SxmTransaction(sqlTransaction, ownsTransaction: true));
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    sqlTransaction.CleanupFailedCreate();
                    SxmLogging.Log(ex, $"SxmTransaction.CreateAsync failure. Database: '{conn.DatabaseName}'.");
                    // Cancellation/fatal - rethrow unchanged so callers/runtime can handle appropriately.
                    throw;
                }
                catch (System.Exception ex)
                {
                    // Ctor failed: release the ambient transaction so it does not leak.
                    sqlTransaction.CleanupFailedCreate();
                    string errStr = $"SxmTransaction.CreateAsync failure. Database: '{conn.DatabaseName}'.";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }

            return CreateCoreAsync(transactionTask, conn);
        }

        /// <summary>
        /// Async continuation for <see cref="CreateAsync"/> when the transaction (shared connection
        /// lease) completes asynchronously. The ambient transaction was already registered on the
        /// caller's execution context before this method was invoked.
        /// </summary>
        private static async Task<SxmTransaction> CreateCoreAsync(Task<SxmSqlTransaction> transactionTask, SxmConnection conn)
        {
            SxmSqlTransaction? sqlTransaction = null;
            try
            {
                sqlTransaction = await transactionTask.ConfigureFalse();
                return new SxmTransaction(sqlTransaction, ownsTransaction: true);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                if (sqlTransaction != null) { try { await sqlTransaction.DisposeAsync().ConfigureFalse(); } catch { /* best effort */ } }
                SxmLogging.Log(ex, $"SxmTransaction.CreateAsync failure. Database: '{conn.DatabaseName}'.");
                // Cancellation/fatal - rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                // Ctor failed after lock acquisition: release lease/ambient so it does not leak.
                if (sqlTransaction != null) { try { await sqlTransaction.DisposeAsync().ConfigureFalse(); } catch { /* best effort */ } }
                string errStr = $"SxmTransaction.CreateAsync failure. Database: '{conn.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Creates a LinqToDB <see cref="DataConnection"/> enlisted in the connection's current
        /// SQLite transaction and registers it for context recovery from query chains.
        /// </summary>
        private DataConnection CreateLinqConnection()
        {
            Microsoft.Data.Sqlite.SqliteTransaction tx = _sxmConnection.CurrentTransaction
                ?? throw new InvalidOperationException("No open SQLite transaction on the shared connection.");

            var dc = new DataConnection(new DataOptions()
                        .UseMappingSchema(SxmMapping.Schema)
                        .UseTransaction(SQLiteTools.GetDataProvider(SQLiteProvider.Microsoft), tx));

            _enlistedTransaction = tx;
            _contextRegistry[dc] = new WeakReference<SxmTransaction>(this);
            return dc;
        }

        /// <summary>
        /// Ensures a SQLite transaction is open on the shared connection and that the LinqToDB
        /// connection is enlisted in it. Rebuilds the LinqToDB connection after an explicit
        /// commit/rollback started a new transaction.
        /// </summary>
        private void EnsureLinqConnectionCurrent()
        {
            if (_sxmConnection.CurrentTransaction == null)
                _sxmConnection.BeginTransaction();

            if (!ReferenceEquals(_enlistedTransaction, _sxmConnection.CurrentTransaction))
            {
                _contextRegistry.TryRemove(_linqToDbDataConnection, out _);
                try { _linqToDbDataConnection.Dispose(); } catch { /* best effort */ }
                _linqToDbDataConnection = CreateLinqConnection();
            }
        }

        /// <summary>
        /// True when a write operation executed through this context has thrown.
        /// While faulted, subsequent write operations are skipped and disposal rolls back the transaction.
        /// Call <see cref="RollbackTransactionAsync"/> to discard the failed transaction and reset the context.
        /// </summary>
        internal bool Faulted => _sqlTransaction.EncounteredError;

        /// <summary>
        /// True when a transaction is currently open on the shared connection.
        /// </summary>
        internal bool HasActiveTransaction => !_isDisposed && _sxmConnection.CurrentTransaction != null;

        /// <summary>
        /// Attempts to recover the SxmTransaction from a LinqToDB query provider.
        /// This enables context preservation through LINQ chains (Where, Select, etc.).
        /// </summary>
        /// <param name="query">The IQueryable to extract context from.</param>
        /// <returns>The associated SxmTransaction if found; otherwise null.</returns>
        internal static SxmTransaction? TryGetContextFromQuery<T>(IQueryable<T> query) where T : class
        {
            if (query == null) return null;

            // Fast path: if it's already an SxmTable, return its context directly
            if (query is SxmTable<T> sxmTable)
                return sxmTable.DataContext;

            // Slower path: try to extract DataConnection from LinqToDB's query provider
            // LinqToDB's ITable<T> and query chains use IQueryProvider that holds the DataConnection
            try
            {
                var provider = query.Provider;
                if (provider == null) return null;

                // LinqToDB's ExpressionQueryImpl<T> has a DataContext property of type IDataContext
                // which is actually the DataConnection
                var dataContextProperty = provider.GetType().GetProperty("DataContext");
                if (dataContextProperty != null)
                {
                    var dataContext = dataContextProperty.GetValue(provider);
                    if (dataContext is DataConnection dc)
                    {
                        // Look up our context from the registry
                        if (_contextRegistry.TryGetValue(dc, out var weakRef) && weakRef.TryGetTarget(out var ctx))
                        {
                            return ctx;
                        }
                    }
                }
            }
            catch
            {
                // If reflection fails or LinqToDB internals change, fall back to null
                // This is a best-effort recovery mechanism
            }

            return null;
        }

        // LinqToDB table access
        public SxmTable<T> GetTable<T>() where T : class
        {
            ThrowIfDisposed();
            EnsureLinqConnectionCurrent();

            // Wrap the provider table so callers get an IQueryable-like wrapper that also
            // exposes LoadWith without referencing LinqToDB. Pass this context for transactional bulk operations.
            return new SxmTable<T>(_linqToDbDataConnection.GetTable<T>(), this);
        }

        // Make raw provider escape hatches internal to prevent consumers from calling LinqToDB APIs directly.
        // Keeps the safe public SxmTransaction surface (GetTable, bulk Update/Delete, Commit/Rollback).
        // Advanced users inside the library (or friend assemblies) can still use these helpers.

        // Opt-in: return the raw LinqToDB ITable<T> when a caller truly needs LinqToDB APIs.
        private ITable<T> GetRawTable<T>() where T : class
        {
            ThrowIfDisposed();
            EnsureLinqConnectionCurrent();
            return _linqToDbDataConnection.GetTable<T>();
        }

        // ---------- Entity operations (immediate execution) ----------

        /// <summary>
        /// Inserts the entity immediately inside the context transaction (started lazily).
        /// When the entity derives from <see cref="SxmEntity"/> its <c>id</c> property is populated
        /// with the database-generated identity value.
        /// The transaction auto-commits when the context is disposed without errors.
        /// </summary>
        /// <returns>The number of rows inserted (0 when the context is faulted and the operation was skipped).</returns>
        internal Task<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return ExecuteWriteAsync(async () =>
            {
                long identity = await _linqToDbDataConnection.InsertWithInt64IdentityAsync(entity, token: cancellationToken).ConfigureFalse();
                entity.id = identity;
                return 1;
            });
        }

        /// <summary>
        /// Updates the entity (by primary key) immediately inside the context transaction (started lazily).
        /// </summary>
        /// <returns>The number of rows updated (0 when the context is faulted and the operation was skipped).</returns>
        internal Task<int> UpdateAsync<T>(T entity, CancellationToken cancellationToken = default) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return ExecuteWriteAsync(() => _linqToDbDataConnection.UpdateAsync(entity, token: cancellationToken));
        }

        /// <summary>
        /// Deletes the entity (by primary key) immediately inside the context transaction (started lazily).
        /// </summary>
        /// <returns>The number of rows deleted (0 when the context is faulted and the operation was skipped).</returns>
        internal Task<int> DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return ExecuteWriteAsync(() => _linqToDbDataConnection.DeleteAsync(entity, token: cancellationToken));
        }

        /// <summary>
        /// Inserts or replaces the entity (by primary key) immediately inside the context transaction (started lazily).
        /// </summary>
        /// <returns>The number of rows affected (0 when the context is faulted and the operation was skipped).</returns>
        internal Task<int> InsertOrReplaceAsync<T>(T entity, CancellationToken cancellationToken = default) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return ExecuteWriteAsync(() => _linqToDbDataConnection.InsertOrReplaceAsync(entity, token: cancellationToken));
        }

        // ---------- Transaction management ------------------

        /// <summary>
        /// Executes a write operation immediately inside the shared context transaction.
        /// Mirrors <see cref="SxmSqlTransaction"/> semantics: once any operation has thrown (via LINQ,
        /// entity writes or named SQL on the shared transaction), subsequent operations are skipped
        /// (returning 0) and the transaction rolls back on dispose.
        /// The first failure is logged, marks the context as <see cref="Faulted"/>, and is rethrown.
        /// </summary>
        /// <param name="operation">The delegate performing the write and returning affected row count.</param>
        /// <returns>The number of rows affected, or 0 when the operation was skipped because the context is faulted.</returns>
        internal async Task<int> ExecuteWriteAsync(Func<Task<int>> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            ThrowIfDisposed();

            if (_sqlTransaction.EncounteredError)
            {
                // Consistent with SxmSqlTransaction: silently skip subsequent statements after a failure.
                return 0;
            }

            EnsureLinqConnectionCurrent();

            try
            {
                return await operation().ConfigureFalse();
            }
            catch (System.Exception ex)
            {
                _sqlTransaction.EncounteredError = true;
                SxmLogging.Log(ex, $"SxmTransaction write operation failure. Database: '{_databaseName}'.");
                throw;
            }
        }

        /// <summary>
        /// Optionally commits the current transaction early while continuing to hold the connection.
        /// Auto-commit on dispose is the recommended pattern; use this only when you need to end the
        /// SQL transaction before the context is disposed.
        /// Write operations performed after an explicit commit start a new transaction.
        /// No-op when no transaction is open.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the context is faulted; call <see cref="RollbackTransactionAsync"/> instead.</exception>
        public async Task CommitTransactionAsync()
        {
            ThrowIfDisposed();

            if (_sqlTransaction.EncounteredError)
                throw new InvalidOperationException(
                    "Cannot commit: a previous operation on this context failed. " +
                    "Call RollbackAsync() to discard the transaction and reset the context.");

            if (_sxmConnection.CurrentTransaction == null) return;

            SQLiteErrorCode errorCode = await _sxmConnection.FinishTransactionAsync(SxmDefines.CommitTransaction).ConfigureFalse();
            if (errorCode != SQLiteErrorCode.Ok)
            {
                throw new InvalidOperationException($"Commit failed with SQLite error code '{errorCode}'. Database: '{_databaseName}'.");
            }
        }

        /// <summary>
        /// Optionally rolls back the current transaction, discarding all uncommitted work, and resets
        /// the faulted state so the context can be used again (subsequent writes start a new transaction).
        /// No-op when no transaction is open (still clears the faulted state).
        /// </summary>
        public async Task RollbackTransactionAsync()
        {
            ThrowIfDisposed();

            try
            {
                if (_sxmConnection.CurrentTransaction != null)
                {
                    await _sxmConnection.FinishTransactionAsync(SxmDefines.RollbackTransaction).ConfigureFalse();
                }
            }
            finally
            {
                _sqlTransaction.EncounteredError = false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SxmTransaction));
        }

        // ---------- Raw SQL query ------------------

        /// <summary>
        /// Execute a SQL SELECT (or any query returning rows) and materialize the result as a
        /// list of dictionaries (column name -> value). Parameters are added as @p0, @p1, ...
        /// Participates in the context transaction when one is active.
        /// Example: QueryAsync("SELECT * FROM UserRecord WHERE id = @p0", 42)
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
        internal async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params object?[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
            ThrowIfDisposed();

            SqliteConnection sqliteConnection = _sxmConnection.UnderlyingConnection
                ?? throw new InvalidOperationException("SQLite connection is not available.");

            // Use the shared SqliteConnection directly (safe - still not exposing it).
            await using SqliteCommand cmd = sqliteConnection.CreateCommand();
            cmd.CommandText = sql;

            // Enlist in the active shared transaction (Microsoft.Data.Sqlite requires the command's
            // Transaction property to be set when the connection has a pending local transaction).
            if (_sxmConnection.CurrentTransaction is SqliteTransaction sqliteTx)
            {
                cmd.Transaction = sqliteTx;
            }

            // Add parameters named @p0, @p1, ... to keep the API simple.
            for (int i = 0; i < (parameters?.Length ?? 0); i++)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = $"@p{i}";
                param.Value = parameters![i] ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            var results = new List<Dictionary<string, object?>>();

            await using SqliteDataReader reader = await cmd.ExecuteReaderAsync().ConfigureFalse();
            while (await reader.ReadAsync().ConfigureFalse())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string name = reader.GetName(i);
                    object? val = await reader.IsDBNullAsync(i).ConfigureFalse() ? null : reader.GetValue(i);
                    row[name] = val;
                }
                results.Add(row);
            }

            return results;
        }

        // ---------- Dispose ------------------------------

        /// <summary>
        /// Asynchronously disposes the context. When a transaction is open:
        /// commits it when no operation failed; rolls it back when the context is <see cref="Faulted"/>.
        /// Commit failures are logged and rethrown after a best-effort rollback.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed) return;

            try
            {
                // Only the owning context finishes the transaction. A joined context leaves
                // commit/rollback to the outer ambient SxmSqlTransaction.
                if (_ownsTransaction && _sxmConnection.CurrentTransaction != null)
                {
                    if (!_sqlTransaction.EncounteredError)
                    {
                        SQLiteErrorCode errorCode = await _sxmConnection.FinishTransactionAsync(SxmDefines.CommitTransaction).ConfigureFalse();
                        if (errorCode != SQLiteErrorCode.Ok)
                        {
                            var commitEx = new InvalidOperationException($"Auto-commit failed with SQLite error code '{errorCode}'. Database: '{_databaseName}'.");
                            SxmLogging.Log(commitEx, $"SxmTransaction auto-commit failure on dispose. Database: '{_databaseName}'.");
                            try { await _sxmConnection.FinishTransactionAsync(SxmDefines.RollbackTransaction).ConfigureFalse(); } catch { /* best effort */ }
                            throw commitEx;
                        }
                    }
                    else
                    {
                        try
                        {
                            await _sxmConnection.FinishTransactionAsync(SxmDefines.RollbackTransaction).ConfigureFalse();
                        }
                        catch (System.Exception ex)
                        {
                            // Log and continue cleanup; do not throw during rollback of a faulted context.
                            SxmLogging.Log(ex, $"SxmTransaction rollback failure on dispose. Database: '{_databaseName}'.");
                        }
                    }
                }
            }
            finally
            {
                CleanupLinqConnection();

                if (_ownsTransaction)
                {
                    // Disposing the owned ambient transaction pops it from the ambient stack and
                    // releases/destroys the private connection. The SQLite transaction has already
                    // been finished above, so its auto-commit is a no-op.
                    try { await _sqlTransaction.DisposeAsync().ConfigureFalse(); }
                    catch (System.Exception ex) { SxmLogging.Log(ex, $"SxmTransaction transaction dispose failure. Database: '{_databaseName}'."); }
                }

                _isDisposed = true;
            }

            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        private void CleanupLinqConnection()
        {
            if (_linqToDbDataConnection != null)
            {
                _contextRegistry.TryRemove(_linqToDbDataConnection, out _);
                try { _linqToDbDataConnection.Dispose(); } catch { /* best effort */ }
            }
        }

        /************************************************ RunStatementAsync (public forwarders) ************************************************/


        public Task<List<TResult>> RunStatementAsync<TResult>(string sqlOrStatementName) where TResult : class, new()
            => _sqlTransaction.RunStatementAsync<TResult>(sqlOrStatementName, new Dictionary<string, object?>());

        /// <summary>
        /// Executes a named SQL statement mapping <paramref name="userObjectParameters"/> onto the statement's
        /// parameters and projecting results into a list of <typeparamref name="TResult"/> entities.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlOrStatementName, T userObjectParameters) where TResult : class, new()
            => _sqlTransaction.RunStatementAsync<T, TResult>(sqlOrStatementName, userObjectParameters);

        /// <summary>
        /// Executes a named SQL statement with a dictionary of named parameters and projects results
        /// into a list of <typeparamref name="TResult"/> entities.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<TResult>> RunStatementAsync<TResult>(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
            => _sqlTransaction.RunStatementAsync<TResult>(sqlOrStatementName, sqlStatementParameters);


        public Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName) 
            => _sqlTransaction.RunStatementAsync(sqlOrStatementName, new Dictionary<string, object?>());

        /// <summary>
        /// Executes a named SQL statement mapping <paramref name="userObjectParameters"/> onto the statement's
        /// parameters and returns raw rows as dictionaries.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlOrStatementName, T userObjectParameters)
            => _sqlTransaction.RunStatementAsync<T>(sqlOrStatementName, userObjectParameters);

        /// <summary>
        /// Executes a named SQL statement with a dictionary of named parameters and returns raw rows as dictionaries.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters)
            => _sqlTransaction.RunStatementAsync(sqlOrStatementName, sqlStatementParameters);

        /// <summary>
        /// Executes a named SQL statement with a list of positional parameter objects and projects results
        /// into a list of <typeparamref name="TResult"/> entities.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<TResult>> RunStatementAsync<TResult>(string sqlOrStatementName, List<object> sqlStatementParameters) where TResult : class, new()
            => _sqlTransaction.RunStatementAsync<TResult>(sqlOrStatementName, sqlStatementParameters);

        /// <summary>
        /// Executes a named SQL statement with a list of positional parameter objects and returns raw rows as dictionaries.
        /// </summary>
        /// <seealso cref="SxmSqlTransaction"/>
        public Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, List<object> sqlStatementParameters)
            => _sqlTransaction.RunStatementAsync(sqlOrStatementName, sqlStatementParameters);
    }
}
