using System.Data;
using static SQLiteXM.SxmDefines;
using static SxmQueryProcessor;

/*
csharp
// Shared connection (correct): await factory + await using -> lock acquired, auto-commit on DisposeAsync
SxmConnection sharedConn = new SxmConnection("myDb", shared: true);
await using (var tx = await SxmSqlTransaction.CreateAsync(sharedConn).ConfigureFalse())
{
    await tx.PerformInsert("insertSomething", paramObj).ConfigureFalse();
} // DisposeAsync awaited here -> lock released
...
*/

namespace SQLiteXM
{
    /// <summary>
    /// Represents a unit-of-work style transaction that is ambient-aware and designed to be used
    /// with both synchronous and asynchronous patterns.
    /// </summary>
    /// <remarks>
    /// - Use <see cref="Create(string?)"/> for synchronous creation of a private (non-shared) connection.
    /// - Use <see cref="CreateAsync(SxmConnection, int, CancellationToken)"/> when you already have an
    ///   <see cref="SxmConnection"/> (shared connections may require acquiring an async lock).
    /// - Prefer the async pattern with <c>await using</c> so the transaction can auto-commit on <see cref="DisposeAsync"/>.
    /// - The synchronous <see cref="Dispose"/> path delegates to <see cref="DisposeAsync"/> and may block.
    /// </remarks>
    public class SxmSqlTransaction : SxmUTransaction
    {
        /// <summary>
        /// Database name associated with the underlying connection. May be null for unnamed in-memory connections.
        /// </summary>
        private string? _databaseName = default;

        /// <summary>
        /// Tracks whether any child statement has thrown an exception. When set, subsequent statements are skipped and auto-commit is prevented.
        /// </summary>
        private bool _encounteredError = false;

        /// <summary>
        /// Internal fault-state accessor so other library layers sharing this transaction
        /// (e.g. <see cref="SxmDbContext"/>) can observe and propagate failure state,
        /// ensuring a failure in any layer prevents auto-commit for all layers.
        /// </summary>
        internal bool EncounteredError
        {
            get => _encounteredError;
            set => _encounteredError = value;
        }

        // Lease (if created for a shared connection). Dispose async on transaction disposal.
        private ISxmConnectionLease? _connectionLease;

        /// <summary>
        /// Protected ctor used by the async factory. The connection lock (if required) is already acquired by the caller.
        /// </summary>
        /// <param name="conn">The underlying <see cref="SxmConnection"/> to execute statements on.</param>
        /// <param name="ownsLock">True when this transaction owns an acquired connection lock.</param>
        /// <param name="ownerId">Optional owner id for lock tracking when the connection is shared.</param>
        private SxmSqlTransaction(SxmConnection conn, bool ownsLock, Guid? ownerId = null) : base(conn, ownsLock, ownerId)
        {
            this._databaseName = conn.DatabaseName;
        }

        /// <summary>
        /// Factory that creates a private (non-shared) <see cref="SxmConnection"/> and registers this transaction as ambient.
        /// </summary>
        /// <param name="databaseName">Optional database name to open; null uses default.</param>
        /// <returns>A new <see cref="SxmSqlTransaction"/> instance that is ambient and ready for use.</returns>
        /// <remarks>
        /// The synchronous factory does not attempt to acquire a shared connection lock because it creates a private connection.
        /// Prefer the async factory for shared connections.
        /// </remarks>
        internal new static SxmSqlTransaction Create(string? databaseName = null)
        {
            SxmConnection conn = new SxmConnection(databaseName, shared: false);
            SxmSqlTransaction sxmTransaction = new SxmSqlTransaction(conn, ownsLock: false, ownerId: null);
            SxmAmbientTransaction.Push(sxmTransaction);
            return sxmTransaction;
        }

        /// <summary>
        /// Factory method that creates an ambient <see cref="SxmSqlTransaction"/> using an existing <see cref="SxmConnection"/>.
        /// If the connection is shared, this method acquires the connection lock asynchronously.
        /// If the connection is non-shared, the transaction is created synchronously without async state machine overhead.
        /// </summary>
        /// <param name="conn">An existing <see cref="SxmConnection"/> instance.</param>
        /// <param name="waitMilliseconds">Maximum time to wait for a shared connection lock when required (only used for shared connections).</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting for the lock (only used for shared connections).</param>
        /// <returns>
        /// A <see cref="Task{T}"/> that represents the asynchronous operation.
        /// The task result contains an ambient <see cref="SxmSqlTransaction"/> with the connection lock acquired when appropriate.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="conn"/> is null.</exception>
        /// <exception cref="SxmException">Thrown when a shared connection lock cannot be acquired.</exception>
        /// <exception cref="InvalidOperationException">Thrown when attempting to create a nested ambient transaction.</exception>
        /// <remarks>
        /// <para>
        /// This method optimizes execution based on connection sharing:
        /// </para>
        /// <list type="bullet">
        /// <item><description><b>Non-shared connections</b>: Completes synchronously using <see cref="Task.FromResult{TResult}"/>, 
        /// avoiding async state machine overhead while preserving <see cref="System.Threading.AsyncLocal{T}"/> ambient transaction context.</description></item>
        /// <item><description><b>Shared connections</b>: Executes asynchronously to acquire the connection lock without blocking.</description></item>
        /// </list>
        /// <para>
        /// The created transaction is automatically registered as the ambient transaction via <see cref="SxmAmbientTransaction"/>.
        /// Nested ambient transactions are not allowed and will throw <see cref="InvalidOperationException"/>.
        /// </para>
        /// </remarks>
        internal new static Task<SxmSqlTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            // Non-shared connections: execute synchronously without async machinery
            if (!conn.Shared)
            {
                var tx = new SxmSqlTransaction(conn, ownsLock: false, ownerId: null);
                tx._connectionLease = null;
                SxmAmbientTransaction.Push(tx);
                return Task.FromResult(tx);
            }

            // Shared connections: use actual async execution
            return CreateAsyncSharedCore(conn, waitMilliseconds, cancellationToken);
        }

        /// <summary>
        /// Core async implementation for creating transactions with shared connections.
        /// This method handles the asynchronous lock acquisition for shared connections.
        /// </summary>
        /// <param name="conn">The shared <see cref="SxmConnection"/> instance.</param>
        /// <param name="waitMilliseconds">Maximum time to wait for the shared connection lock.</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting for the lock.</param>
        /// <returns>
        /// A <see cref="Task{T}"/> representing the asynchronous operation.
        /// The task result contains an ambient <see cref="SxmSqlTransaction"/> with the connection lock acquired.
        /// </returns>
        /// <exception cref="SxmException">Thrown when the shared connection lock cannot be acquired within the timeout period.</exception>
        /// <exception cref="InvalidOperationException">Thrown when attempting to create a nested ambient transaction.</exception>
        /// <remarks>
        /// This method is called by <see cref="CreateAsync"/> when the connection is shared and requires asynchronous lock acquisition.
        /// The lease is acquired before creating the transaction, and the transaction owns the lock until disposed.
        /// </remarks>
        private static async Task<SxmSqlTransaction> CreateAsyncSharedCore(SxmConnection conn, int waitMilliseconds, CancellationToken cancellationToken)
        {
            // Acquire lease (this internally calls LockAsync and will throw on timeout)
            var lease = await conn.AcquireLeaseAsync(waitMilliseconds, cancellationToken).ConfigureFalse();

            var tx = new SxmSqlTransaction(conn, ownsLock: true, ownerId: lease.OwnerId);
            tx._connectionLease = lease;

            SxmAmbientTransaction.Push(tx);
            return tx;
        }

        /// <summary>
        /// Asynchronous dispose which will attempt to commit the transaction if this instance is the ambient/top transaction and no error was encountered.
        /// </summary>
        /// <returns>A task that completes after the transaction has been cleaned up and resources released.</returns>
        /// <remarks>
        /// - If this transaction is not the ambient/top transaction no implicit commit will be attempted and a warning is logged.
        /// - Any exceptions during commit are logged and rethrown so caller can observe failures.
        /// - Final cleanup always attempts to remove the transaction from the ambient stack (best-effort).
        /// </remarks>
        public override async ValueTask DisposeAsync()
        {
            try
            {
                // Commit only if this is the ambient/top transaction and there was no error.
                try
                {
                    if (SxmAmbientTransaction.Current == this && !_encounteredError)
                    {
                        await CommitTransactionAsync().ConfigureFalse();
                    }
                    else if (SxmAmbientTransaction.Current != null && SxmAmbientTransaction.Current != this)
                    {
                        // Misordered dispose; log it. Do not try to implicitly commit/pop.
                        try { SxmLogging.Log(new InvalidOperationException("DisposeAsync attempted to auto-commit when transaction is not top ambient.")); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    SxmLogging.Log(ex);
                    throw;
                }
                finally
                {
                    try
                    {
                        if (SxmAmbientTransaction.Current == this)
                        {
                            try
                            {
                                SxmAmbientTransaction.Pop(this);
                            }
                            catch (Exception ex)
                            {
                                // Log and attempt best-effort removal; do not rethrow.
                                SxmLogging.Log(ex);
                                try { SxmAmbientTransaction.TryRemove(this); } catch { }
                            }
                        }
                        else
                        {
                            // Attempt best-effort removal if not top.
                            try
                            {
                                if (SxmAmbientTransaction.Current != null && !SxmAmbientTransaction.TryRemove(this))
                                {
                                    // If removal failed, just log a warning. Operator can inspect and recover.
                                    try { SxmLogging.Log(new InvalidOperationException("DisposeAsync could not remove non-top ambient transaction; manual recovery may be required.")); } catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                SxmLogging.Log(ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SxmLogging.Log(ex);
                        // Do not rethrow from final cleanup
                    }
                }
            }
            finally
            {
                // If we acquired a lease for a shared connection, dispose it (releases lock).
                try
                {
                    if (_connectionLease != null)
                    {
                        await _connectionLease.DisposeAsync().ConfigureFalse();
                        _connectionLease = null;
                    }
                }
                catch
                {
                    // Swallow to avoid throwing during cleanup.
                }

                // Call base async dispose to release other resources.
                await base.DisposeAsync().ConfigureFalse();
            }
        }

        /************************************************************************* GENERIC ********************************************************************/

        /// <summary>
        /// Generic runner: map a user object into statement parameters, execute and map results to <typeparamref name="TResult"/>. Supports entity mapping. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlStatementName, T userObjectParameters) where TResult : class, new()
        {
            SqlStatementDetails statementDetails = new();

            statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlStatementName);
            if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
            {
                statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlStatementName, this._databaseName);
            }

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(_databaseName, sqlStatementName, statementDetails.SqlStatementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, selectParameterValues).ConfigureFalse();

            return SxmHelpers.PopulateUserRecord<TResult>(select);
        }

        /// <summary>
        /// Generic runner: execute with dictionary parameters and map results to <typeparamref name="TResult"/>. Supports dictionary of named parameters. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Generic runner: map a user object into statement parameters, execute and return list of dictionary rows. Supports entity mapping. Return list of dictionary rows.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementDetails statementDetails = new();

            statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlStatementName);
            if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
            {
                statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlStatementName, this._databaseName);
            }

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(_databaseName, sqlStatementName, statementDetails.SqlStatementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);

            return await RunStatementAsync(sqlStatementName, selectParameterValues).ConfigureFalse();
        }

        /// <summary>
        /// Generic runner: wrapper for dictionary-to-list overload. Execute with dictionary parameters and return list of dictionary rows. Supports dictionary of named parameters. Return list of dictionary rows.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatementAsync(sqlStatementName, new List<object>(1) { sqlStatementParameters }).ConfigureFalse();
        }

        /// <summary>
        /// Generic runner: execute with a list of parameter objects and map results to <typeparamref name="TResult"/>. Supports List of positional parameters. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Core executor that dispatches to the appropriate helper based on the statement type. Supports a dictionary of named parameters that is put inside a List. Return list of dictionary rows.
        /// </summary>
        /// <param name="sqlStatementName">Named SQL statement.</param>
        /// <param name="sqlStatementParameters">Parameters supplied as a list of dictionaries or other objects as expected by the helper.</param>
        /// <returns>List of result rows as dictionaries. Empty list when no rows are returned.</returns>
        public async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            if (!_encounteredError)
            {
                SqlStatementDetails statementDetails = new();

                statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlStatementName);
                if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
                {
                    statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlStatementName, this._databaseName);
                }

                try
                {
                    switch (statementDetails.SqlStatementType)
                    {
                        case SqlStatementType.Select:
                        case SqlStatementType.Update:
                        case SqlStatementType.Delete:
                        case SqlStatementType.Insert:
                            recordData = await SxmSelectHelpers.PerformSelectTransAsync(sqlStatementName, sqlStatementParameters, statementDetails, this).ConfigureFalse();
                            break;


                        // Direct SQL statement queries. These are statements where the SQL is embedded in the code, not inside the SqlStatemenst file.
                        case SqlStatementType.SelectDirect:
                        case SqlStatementType.UpdateDirect:
                        case SqlStatementType.DeleteDirect:
                        case SqlStatementType.InsertDirect:
                            recordData = await SxmSelectHelpers.PerformSelectDirectTransAsync(sqlStatementName, sqlStatementParameters, statementDetails, this).ConfigureFalse();
                            break;

                        default: break;
                    }
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    // Record Error
                    _encounteredError = true;

                    string? statement = SxmHelpers.SqlStatementFromStatementName(sqlStatementName, statementDetails.SqlStatementType);
                    string statementName = string.Empty;
                    if (statementDetails.SqlStatementType != SqlStatementType.SelectDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.UpdateDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.DeleteDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.InsertDirect)

                    {
                        statementName = $"SQL statement: '{sqlStatementName}'.";
                    }

                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    SxmLogging.Log(ex, $"RunStatementAsync failure. {statementName} Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {statement}");
                    throw;
                }
                catch (System.Exception ex)
                {
                    // Record Error
                    _encounteredError = true;

                    string? statement = SxmHelpers.SqlStatementFromStatementName(sqlStatementName, statementDetails.SqlStatementType);
                    string statementName = string.Empty;
                    if (statementDetails.SqlStatementType != SqlStatementType.SelectDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.UpdateDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.DeleteDirect &&
                        statementDetails.SqlStatementType != SqlStatementType.InsertDirect)

                    {
                        statementName = $"SQL statement: '{sqlStatementName}'.";
                    }

                    string errStr = $"RunStatementAsync failure. {statementName} Database: '{this._databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {statement}";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }

            recordData ??= new List<Dictionary<string, object?>>();
            return await Task.FromResult(recordData).ConfigureFalse();
        }
    }
}