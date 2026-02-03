using SQLiteXM.Internal;
using System.Data;
using static SQLiteXM.SxmDefines;

/*
csharp
// Shared connection (correct): await factory + await using -> lock acquired, auto-commit on DisposeAsync
SxmConnection sharedConn = new SxmConnection("myDb", shared: true);
await using (var tx = await SxmTransaction.CreateAsync(sharedConn))
{
    await tx.PerformInsert("insertSomething", paramObj);
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
    /// - Synchronous <see cref="Dispose(bool)"/> does not auto-commit; call explicit commit methods before disposing if using sync pattern.
    /// </remarks>
    public class SxmTransaction : SxmUTransaction
    {
        /// <summary>
        /// Database name associated with the underlying connection. May be null for unnamed in-memory connections.
        /// </summary>
        private string? _databaseName = default;

        /// <summary>
        /// Tracks whether any child statement has thrown an exception. When set, subsequent statements are skipped until <see cref="ResetError"/> is called.
        /// </summary>
        private bool _encounteredError = false;

        /// <summary>
        /// Protected ctor used by the async factory. The connection lock (if required) is already acquired by the caller.
        /// </summary>
        /// <param name="conn">The underlying <see cref="SxmConnection"/> to execute statements on.</param>
        /// <param name="ownsLock">True when this transaction owns an acquired connection lock.</param>
        /// <param name="ownerId">Optional owner id for lock tracking when the connection is shared.</param>
        protected SxmTransaction(SxmConnection conn, bool ownsLock, Guid? ownerId = null) : base(conn, ownsLock, ownerId)
        {
            this._databaseName = conn.DatabaseName;
        }

        /// <summary>
        /// Factory that creates a private (non-shared) <see cref="SxmConnection"/> and registers this transaction as ambient.
        /// </summary>
        /// <param name="databaseName">Optional database name to open; null uses default.</param>
        /// <returns>A new <see cref="SxmTransaction"/> instance that is ambient and ready for use.</returns>
        /// <remarks>
        /// The synchronous factory does not attempt to acquire a shared connection lock because it creates a private connection.
        /// Prefer the async factory for shared connections.
        /// </remarks>
        public new static SxmTransaction Create(string? databaseName = null)
        {
            var conn = new SxmConnection(databaseName, shared: false);
            var tx = new SxmTransaction(conn, ownsLock: false, ownerId: null);
            SxmAmbientTransaction.Push(tx);
            return tx;
        }

        /// <summary>
        /// Async factory overload that uses an existing <see cref="SxmConnection"/>.
        /// If the connection is shared, this method will attempt to acquire the connection lock asynchronously.
        /// </summary>
        /// <param name="conn">An existing <see cref="SxmConnection"/> instance.</param>
        /// <param name="waitMilliseconds">Maximum time to wait for a shared connection lock when required.</param>
        /// <param name="cancellationToken">Cancellation token to abort waiting for the lock.</param>
        /// <returns>An ambient <see cref="SxmTransaction"/> with the connection lock acquired when appropriate.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="conn"/> is null.</exception>
        /// <exception cref="SxmException">Thrown when a shared connection lock cannot be acquired.</exception>
        public new static async Task<SxmTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            bool ownsLock = false;
            Guid? ownerId = null;

            // Only attempt lock when the supplied connection is shared.
            if (conn.Shared)
            {
                ownerId = Guid.NewGuid();
                bool locked = await conn.LockAsync(waitMilliseconds, cancellationToken, ownerId).ConfigureAwait(false);
                if (!locked)
                {
                    throw new SxmException(new ErrorMessage("lockDB", conn.DatabaseName));
                }
                ownsLock = true;
            }

            var tx = new SxmTransaction(conn, ownsLock: ownsLock, ownerId: ownerId);
            SxmAmbientTransaction.Push(tx);
            return tx;
        }

        /// <summary>
        /// Synchronous dispose that cleans up ambient transaction state and calls base dispose.
        /// Does NOT perform an automatic commit to avoid blocking on finalizers or in sync paths.
        /// </summary>
        /// <param name="disposing">True when called from user code; false when called from finalizer.</param>
        /// <remarks>
        /// - If <paramref name="disposing"/> is false (finalizer), managed state is not touched.
        /// - If this instance is the ambient/top transaction it will be popped from the ambient stack.
        /// - Errors during cleanup are logged but not rethrown to avoid throwing from Dispose.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            // Finalizer path: do not touch managed state (Ambient or Connection).
            if (!disposing)
            {
                try
                {
                    base.Dispose(disposing);
                }
                catch
                {
                    // Swallow to avoid throwing on finalizer thread.
                }
                return;
            }

            try
            {
                try
                {
                    // Only pop if we are the ambient/top transaction.
                    if (SxmAmbientTransaction.Current == this)
                    {
                        try
                        {
                            SxmAmbientTransaction.Pop(this);
                        }
                        catch (Exception ex)
                        {
                            // Log but do not rethrow from Dispose to avoid masking other cleanup.
                            try { SxmLogging.Log(ex); } catch { }
                            // Try a best-effort removal to recover the ambient stack.
                            try { SxmAmbientTransaction.TryRemove(this); } catch { }
                        }
                    }
                    else
                    {
                        // Log a warning — popping out-of-order is a programming error.
                        if(SxmAmbientTransaction.Current != null)
                            try { SxmLogging.Log(new InvalidOperationException("Dispose called when transaction is not the ambient/top transaction.")); } catch { }
                        // Do not attempt to pop or auto-commit.
                    }
                }
                catch (Exception ex)
                {
                    SxmLogging.Log(ex);
                    // Do not rethrow; keep disposing to allow base cleanup.
                }

                base.Dispose(disposing);
            }
            finally
            {
                // no synchronous commit to avoid blocking; use DisposeAsync or explicit commit instead
            }
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
                        await CommitTransactionAsync().ConfigureAwait(false);
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
                // Ensure lock is released deterministically before finishing async disposal.
                try
                {
                    // Protected helper on base class; idempotent and best-effort.
                    this.EnsureLockReleased();
                }
                catch { /* swallow to avoid throwing during final cleanup */ }
                
                // Call base async dispose to release resources.
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Clear the internal encountered-error flag so subsequent statements will run.
        /// </summary>
        /// <remarks>
        /// - If a previous statement set the error flag, <see cref="RunStatementAsync(string, List{object})"/> will skip subsequent statements
        ///   until <see cref="ResetError"/> is called.
        /// - Calling <c>commitTransaction()</c> / <c>commitTransactionAsync()</c> ends the underlying SQLite transaction but does NOT
        ///   release the SxmTransaction's connection lock or dispose the object. You may reuse the same SxmTransaction instance after a commit.
        /// - The connection lock is released only when the transaction is disposed (<see cref="DisposeAsync"/> / <see cref="Dispose(bool)"/>) or finalized.
        /// </remarks>
        public void ResetError() => _encounteredError = false;

        /************************************************************************* INSERT ********************************************************************/
        /// <summary>
        /// Perform an insert statement and map the returning row to <typeparamref name="TResult"/>. Supports entity mapping. Return the entity object.
        /// </summary>
        /// <typeparam name="T">Type of the input parameter object.</typeparam>
        /// <typeparam name="TResult">Type of the result record to return.</typeparam>
        /// <param name="sqlStatementName">Named SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object with parameter values.</param>
        /// <returns>The first result row mapped to <typeparamref name="TResult"/>.</returns>
        public async Task<TResult> InsertAsync<T, TResult>(string sqlStatementName, T userObjectParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<T, TResult>(sqlStatementName, userObjectParameters).CAF();
            return select[0];
        }

        /// <summary>
        /// Perform an insert and return the first result row as a dictionary. Supports entity mapping. Return dictionary of inserted columns.
        /// </summary>
        public async Task<Dictionary<string, object?>> InsertAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync<T>(sqlStatementName, userObjectParameters).CAF();
            return select[0];
        }

        /// <summary>
        /// Perform an insert using dictionary parameters and map the result to <typeparamref name="TResult"/>. Supports dictionary of named parameters. Return the entity object.
        /// </summary>
        public async Task<TResult> InsertAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }

        /// <summary>
        /// Perform an insert using dictionary parameters and return the first result row as dictionary. Supports dictionary of named parameters. Return dictionary of inserted columns.
        /// </summary>
        public async Task<Dictionary<string, object?>> InsertAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, new List<object>(1) { sqlStatementParameters }).CAF();
            return select[0];
        }

        /// <summary>
        /// Perform an insert using a list of parameter objects and map the first result to <typeparamref name="TResult"/>. Supports List of positional parameters. Return the entity object.
        /// </summary>
        public async Task<TResult> InsertAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }

        /// <summary>
        /// Perform an insert using a list of parameter objects and return the first result row as dictionary. Supports List of positional parameters. Return dictionary of inserted columns.
        /// </summary>
        public async Task<Dictionary<string, object?>> InsertAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }


        /************************************************************************* UPDATE ********************************************************************/

        /// <summary>
        /// Perform an update statement using a user object for parameters. Supports entity mapping.
        /// </summary>
        public async Task UpdateAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync<T>(sqlStatementName, userObjectParameters).CAF();
        }

        /// <summary>
        /// Perform an update using dictionary parameters. Supports dictionary of named parameters.
        /// </summary>
        public async Task UpdateAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }

        /// <summary>
        /// Perform an update using a list of parameter objects. Supports list of positional parameters.
        /// </summary>
        public async Task UpdateAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* SELECT ********************************************************************/
        /// <summary>
        /// Perform a select statement and return a list of dictionary rows. Supports entity mapping. Return list of dictionary rows.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> SelectAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<T>(sqlStatementName, userObjectParameters).CAF();
        }

        /// <summary>
        /// Perform a select statement and map results to <typeparamref name="TResult"/>. Supports entity mapping. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<T, TResult>(string sqlStatementName, T userObjectParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<T, TResult>(sqlStatementName, userObjectParameters).CAF();
        }

        /// <summary>
        /// Perform a select using dictionary parameters and return list of dictionary rows. Supports dictionary of named parameters. Return list of dictionary rows.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> SelectAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }

        /// <summary>
        /// Perform a select using dictionary parameters and map to <typeparamref name="TResult"/>. Supports dictionary of named parameters. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters).CAF();
        }

        /// <summary>
        /// Perform a select using a list of parameter objects and map to <typeparamref name="TResult"/>. Supports List of positional parameters. Return a List of entity objects.
        /// </summary>
        public async Task<List<TResult>> SelectAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters).CAF();
        }

        /// <summary>
        /// Perform a select using a list of parameter objects and return list of dictionary rows. Supports List of positional parameters. Return list of dictionary rows.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> SelectAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* DELETE ********************************************************************/

        /// <summary>
        /// Perform a delete statement using a user object for parameters. Supports entity mapping.
        /// </summary>
        public async Task DeleteAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync<T>(sqlStatementName, userObjectParameters).CAF();
        }

        /// <summary>
        /// Perform a delete using dictionary parameters. Supports dictionary of named parameters.
        /// </summary>
        public async Task DeleteAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }

        /// <summary>
        /// Perform a delete using a list of parameter objects. Supports List of positional parameters.
        /// </summary>
        public async Task DeleteAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* GENERIC ********************************************************************/

        /// <summary>
        /// Generic runner: map a user object into statement parameters, execute and map results to <typeparamref name="TResult"/>. Supports entity mapping. Return a List of entity objects.
        /// </summary>
        private async Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlStatementName, T userObjectParameters) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmInit.GetTableColumnNamesAsync(_databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, selectParameterValues).CAF();
            List<TResult> userRecordList = SxmHelpers.PopulateUserRecord<TResult>(select);

            return userRecordList;
        }

        /// <summary>
        /// Generic runner: execute with dictionary parameters and map results to <typeparamref name="TResult"/>. Supports dictionary of named parameters. Return a List of entity objects.
        /// </summary>
        private async Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();

            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Generic runner: map a user object into statement parameters, execute and return list of dictionary rows. Supports entity mapping. Return list of dictionary rows.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlStatementName, T userObjectParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmInit.GetTableColumnNamesAsync(_databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters);

            return await RunStatementAsync(sqlStatementName, selectParameterValues).CAF();
        }

        /// <summary>
        /// Generic runner: wrapper for dictionary-to-list overload. Execute with dictionary parameters and return list of dictionary rows. Supports dictionary of named parameters. Return list of dictionary rows.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatementAsync(sqlStatementName, new List<object>(1) { sqlStatementParameters }).CAF();
        }

        /// <summary>
        /// Generic runner: execute with a list of parameter objects and map results to <typeparamref name="TResult"/>. Supports List of positional parameters. Return a List of entity objects.
        /// </summary>
        private async Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters).CAF();

            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Core executor that dispatches to the appropriate helper based on the statement type. Supports a dictionary of named parameters that is put inside a List. Return list of dictionary rows.
        /// </summary>
        /// <param name="sqlStatementName">Named SQL statement.</param>
        /// <param name="sqlStatementParameters">Parameters supplied as a list of dictionaries or other objects as expected by the helper.</param>
        /// <returns>List of result rows as dictionaries. Empty list when no rows are returned.</returns>
        private async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, List<object> sqlStatementParameters)
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            if (!_encounteredError)
            {
                SqlStatementType sqlStatementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);

                try
                {
                    switch (sqlStatementType)
                    {
                        case SqlStatementType.select:
                            recordData = await SxmSelectHelpers.PerformSelectTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.update:
                            await SxmUpdateHelpers.PerformUpdateTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.delete:
                            await SxmDeleteHelpers.PerformDeleteTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.PerformInsertTransAsync(sqlStatementName, sqlStatementParameters, this).CAF());
                            break;

                        // Direct SQL statements.
                        case SqlStatementType.selectDirect:
                            recordData = await SxmSelectHelpers.PerformSelectDirectTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.updateDirect:
                            await SxmUpdateHelpers.PerformUpdateDirectTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.deleteDirect:
                            await SxmDeleteHelpers.PerformDeleteDirectTransAsync(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.insertDirect:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.PerformInsertDirectTransAsync(sqlStatementName, sqlStatementParameters, this).CAF()); 
                            break;

                        default: break;
                    }
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    // Record Error
                    _encounteredError = true;

                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    SxmLogging.Log(ex, $"RunStatementAsync failure for statement '{sqlStatementName}' statement type '{sqlStatementType.ToString()}'.");
                    throw;
                }
                catch (System.Exception ex)
                {
                    // Record Error
                    _encounteredError = true;

                    string errStr = $"RunStatementAsync failure for statement '{sqlStatementName}' dstatement type '{sqlStatementType.ToString()}'.";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }

            if (recordData == default(List<Dictionary<string, object?>>))
                recordData = new List<Dictionary<string, object?>>();

            return await Task.FromResult(recordData).CAF();
        }
    }
}