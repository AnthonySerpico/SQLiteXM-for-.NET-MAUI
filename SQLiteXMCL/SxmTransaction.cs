using SQLiteXM.Internal;
using System.Data;
using static SQLiteXM.Defines;

/*
csharp
// Shared connection (correct): await factory + await using -> lock acquired, auto-commit on DisposeAsync
SxmConnection sharedConn = new SxmConnection("myDb", shared: true);
await using (var tx = await SxmTransaction.CreateAsync(sharedConn))
{
    await tx.PerformInsert("insertSomething", paramObj);
} // DisposeAsync() commits if no error

// Transient factory is sync but still use await using to get auto-commit
await using (var tx = SxmTransaction.Create("myDb"))
{
    await tx.PerformInsert("insertSomething", paramObj);
} // DisposeAsync() commits

// Wrong if you expect auto-commit: using(...) without explicit commit -> rollback
using (var tx = SxmTransaction.Create("myDb"))
{
    await tx.PerformInsert("insertSomething", paramObj);
} // Dispose() does NOT commit -> rollback (unless you called tx.commitTransaction())*/


namespace SQLiteXM
{
    public class SxmTransaction : SxmUTransaction
    {
        private string? databaseName = default;
        private bool encounteredError = false;

        // Protected ctor used by the async factory. Connection lock already acquired.
        protected SxmTransaction(SxmConnection conn, bool ownsLock) : base(conn, ownsLock)
        {
            this.databaseName = conn.DatabaseName;
        }

        /// <summary>
        //•	Async pattern(preferred): await using (var tx = SxmTransaction.Create()) { ... }
        //•	Sync pattern(explicit): using (var tx = SxmTransaction.Create()) { /* call tx.commitTransaction() before leaving scope */ }
        /// </summary>
        // factory: create a private (non-shared) connection (if dbName provided) and acquire async lock without blocking the calling thread.
        public new static SxmTransaction Create(string? databaseName = null)
        {
            var conn = new SxmConnection(databaseName, shared: false);
            var tx = new SxmTransaction(conn, ownsLock: false);
            AmbientSxmTransaction.Push(tx);
            return tx;
        }

        // Async factory overload when caller already has connection.
        public new static async Task<SxmTransaction> CreateAsync(SxmConnection conn, int waitMilliseconds = 100, CancellationToken cancellationToken = default)
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

            var tx = new SxmTransaction(conn, ownsLock: ownsLock);
            AmbientSxmTransaction.Push(tx);
            return tx;
        }

        // Keep Dispose but DO NOT commit synchronously here.
        // Synchronous Dispose should only clean up ambient and call base Dispose.
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
                    if (AmbientSxmTransaction.Current == this )
                    {
                        try
                        {
                            AmbientSxmTransaction.Pop(this);
                        }
                        catch (Exception ex)
                        {
                            // Log but do not rethrow from Dispose to avoid masking other cleanup.
                            try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                            // Try a best-effort removal to recover the ambient stack.
                            try { AmbientSxmTransaction.TryRemove(this); } catch { }
                        }
                    }
                    else
                    {
                        // Log a warning — popping out-of-order is a programming error.
                        try { Connection?.log(new InvalidOperationException("Dispose called when transaction is not the ambient/top transaction."), System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                        // Do not attempt to pop or auto-commit.
                    }
                }
                catch (Exception ex)
                {
                    try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                    // Do not rethrow; keep disposing to allow base cleanup.
                }

                base.Dispose(disposing);
            }
            finally
            {
                // no synchronous commit to avoid blocking; use DisposeAsync or explicit commit instead
            }
        }

        // Provide async disposal that commits if ambient and no error.
        public override async ValueTask DisposeAsync()
        {
            try
            {
                // Commit only if this is the ambient/top transaction and there was no error.
                try
                {
                    if (AmbientSxmTransaction.Current == this && !encounteredError)
                    {
                        await commitTransactionAsync().ConfigureAwait(false);
                    }
                    else if (AmbientSxmTransaction.Current != this)
                    {
                        // Misordered dispose; log it. Do not try to implicitly commit/pop.
                        try { Connection?.log(new InvalidOperationException("DisposeAsync attempted to auto-commit when transaction is not top ambient."), System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                    throw;
                }
                finally
                {
                    try
                    {
                        if (AmbientSxmTransaction.Current == this)
                        {
                            try
                            {
                                AmbientSxmTransaction.Pop(this);
                            }
                            catch (Exception ex)
                            {
                                // Log and attempt best-effort removal; do not rethrow.
                                try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                                try { AmbientSxmTransaction.TryRemove(this); } catch { }
                            }
                        }
                        else
                        {
                            // Attempt best-effort removal if not top.
                            try
                            {
                                if (!AmbientSxmTransaction.TryRemove(this))
                                {
                                    // If removal failed, just log a warning. Operator can inspect and recover.
                                    try { Connection?.log(new InvalidOperationException("DisposeAsync could not remove non-top ambient transaction; manual recovery may be required."), System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        try { Connection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                        // Do not rethrow from final cleanup
                    }
                }
            }
            finally
            {
                // Call base async dispose to release resources.
                await base.DisposeAsync().ConfigureAwait(false);
            }
        }

        public void ResetError() => encounteredError = false;

        /************************************************************************* INSERT ********************************************************************/
        public async Task<TResult> PerformInsert<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                              where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatement<T, TResult>(sqlStatementName, userObjectParameters).CAF();
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement<T>(sqlStatementName, userObjectParameters).CAF();
            return select[0];
        }
        public async Task<T> PerformInsert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }).CAF();
            return select[0];
        }
        public async Task<T> PerformInsert<T>(string sqlStatementName, List<object> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
            return select[0];
        }


        /************************************************************************* SELECT ********************************************************************/
        public async Task<List<Dictionary<string, object?>>> PerformSelect<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, userObjectParameters).CAF();
        }
        public async Task<List<TResult>> PerformSelect<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                    where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T, TResult>(sqlStatementName, userObjectParameters).CAF();
        }
        public async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }
        public async Task<List<T>> PerformSelect<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters).CAF();
        }
        public async Task<List<T>> PerformSelect<T>(string sqlStatementName, List<object> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters).CAF();
        }
        public async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* DELETE ********************************************************************/

        public async Task PerformDelete<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters).CAF();
        }
        public async Task PerformDelete(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }
        public async Task PerformDelete(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* UPDATE ********************************************************************/

        public async Task PerformUpdate<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters).CAF();
        }
        public async Task PerformUpdate(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }
        public async Task PerformUpdate(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters).CAF();
        }


        /************************************************************************* GENERIC ********************************************************************/

        private async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                    where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = await SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues).CAF();
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }
        private async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters).CAF();

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = await SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues(columnNames, userObjectParameters);

            return await RunStatement(sqlStatementName, selectParameterValues).CAF();
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }).CAF();
        }
        private async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters).CAF();

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters)
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            if (!encounteredError)
            {
                try
                {
                    switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
                    {
                        case SqlStatementType.select:
                            recordData = await SxmSelectHelpers.performSelectTrans(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.performInsertTrans(sqlStatementName, sqlStatementParameters, this).CAF());
                            break;

                        case SqlStatementType.update:
                            await SxmUpdateHelpers.performUpdateTrans(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.delete:
                            await SxmDeleteHelpers.performDeleteTrans(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.selectDirect:
                            recordData = await SxmSelectHelpers.performSelectTransDirect(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.deleteDirect:
                            await SxmDeleteHelpers.performDeleteTransDirect(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        case SqlStatementType.updateDirect:
                            await SxmUpdateHelpers.performUpdateTransDirect(sqlStatementName, sqlStatementParameters, this).CAF();
                            break;

                        default: break;
                    }
                }
                catch (System.Exception)
                {
                    // Record Error
                    encounteredError = true;
                    throw;
                }
            }

            if (recordData == default(List<Dictionary<string, object?>>))
                recordData = new List<Dictionary<string, object?>>();

            return await Task.FromResult(recordData).CAF();
        }
    }
}
