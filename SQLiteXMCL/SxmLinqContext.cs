using LinqToDB;
using Microsoft.Data.Sqlite;
using SQLiteXM.Internal;

namespace SQLiteXM
{
    public class SxmLinqContext : IDisposable
    {
        private bool isDisposed = false;
        private readonly SqliteConnection dConnection;
        private readonly SxmChangeSet _changeSet = new SxmChangeSet();
        private readonly LinqToDB.Data.DataConnection _linqToDbDataConnection;

        public SxmLinqContext(string? databaseName = null)
        {
            string connStr = SxmConnection.getConnectionString(ref databaseName);
            dConnection = new SqliteConnection(connStr);
            dConnection.Open();

            _linqToDbDataConnection = new LinqToDB.Data.DataConnection(LinqToDB.DataProvider.SQLite.SQLiteTools.GetDataProvider("Microsoft.Data.Sqlite"), dConnection);
            _linqToDbDataConnection.AddMappingSchema(SxmMapping.Schema);
        }

        // LinqToDB table access
        public SxmTable<T> GetTable<T>() where T : class
        {
            // Wrap the provider table so callers get an IQueryable-like wrapper that also
            // exposes LoadWith without referencing LinqToDB.
            return new SxmTable<T>(_linqToDbDataConnection.GetTable<T>());
        }

        // Opt-in: return the raw LinqToDB ITable<T> when a caller truly needs LinqToDB APIs.
        public ITable<T> GetRawTable<T>() where T : class
        {
            return _linqToDbDataConnection.GetTable<T>();
        }

        public SxmChangeSet GetChangeSet() => _changeSet;

        // Insert the entity and return the generated identity (as object).
        public object InsertWithIdentity<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection.InsertWithIdentity(entity);
        }

        // Async insert returning generated identity (as object).
        public Task<object> InsertWithIdentityAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection.InsertWithIdentityAsync(entity);
        }

        // ---------- Convenience async helpers to avoid exposing DataConnection externally ----------
        /// <summary>
        /// Insert the given entity using the underlying DataConnection.
        /// Use this instead of calling DataConnection.InsertAsync(...) from outside this assembly.
        /// </summary>
        public Task InsertAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection!.InsertAsync(entity);
        }

        /// <summary>
        /// Update the given entity using the underlying DataConnection.
        /// Use this instead of calling DataConnection.UpdateAsync(...) from outside this assembly.
        /// </summary>
        public Task UpdateAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection!.UpdateAsync(entity);
        }

        /// <summary>
        /// Delete the given entity using the underlying DataConnection.
        /// </summary>
        public Task DeleteAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection!.DeleteAsync(entity);
        }
        // -------------------------------------------------------------------------------------------

        // ---------- Change tracking API ------------------

        public void InsertOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsInsert();
            _changeSet.Add(entity, ChangeType.Insert);
        }

        public void UpdateOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsUpdate();
            _changeSet.Add(entity, ChangeType.Update);
        }

        public void DeleteOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsDelete();
            _changeSet.Add(entity, ChangeType.Delete);
        }

        // ---------- SubmitChanges ------------------------
        // Default now uses RollbackOnAnyFailure (strict atomic behavior).
        public async Task<SubmitChangesResult> SubmitChanges()
        {
            return await SubmitChanges(ConflictMode.RollbackOnAnyFailure).CAF();
        }

        public async Task<SubmitChangesResult> SubmitChanges(ConflictMode conflictMode)
        {
            var report = new SubmitChangesResult();
            if (_changeSet.IsEmpty)
            {
                report.AllSucceeded = true;
                return report;
            }

            bool committed = false;
            bool anyFailure = false;

            // One transaction for the whole unit of work
            await using (SxmTransaction sxmTrans = SxmTransaction.Create())
            {
                try
                {
                    var actions = _changeSet.GetOrderedActions().ToList();

                    foreach (var action in actions)
                    {
                        try
                        {
                            switch (action.Type)
                            {
                                case ChangeType.Insert:
                                case ChangeType.Update:
                                    // Save decides insert vs update based on existence; use transaction-aware overload.
                                    await action.Entity.Save(sxmTrans).CAF();
                                    break;

                                case ChangeType.Delete:
                                    await action.Entity.Delete(sxmTrans).CAF();
                                    break;
                            }

                            // Success
                            action.Result = new ChangeResult
                            {
                                Success = true,
                                Error = null,
                                IdAfterOperation = action.Entity.id > 0 ? action.Entity.id : null,
                                SynchIdAfterOperation = action.Entity.synchId
                            };

                            report.Succeeded.Add(action);
                        }
                        catch (Exception ex)
                        {
                            anyFailure = true;

                            action.Result = new ChangeResult
                            {
                                Success = false,
                                Error = ex,
                                IdAfterOperation = action.Entity.id > 0 ? action.Entity.id : null,
                                SynchIdAfterOperation = action.Entity.synchId
                            };

                            report.Failed.Add(action);

                            if (conflictMode == ConflictMode.FailOnFirstConflict)
                            {
                                // stop processing further actions
                                break;
                            }

                            // ContinueOnConflict or RollbackOnAnyFailure: continue processing to collect results
                        }
                    }

                    // Decide commit/rollback based on conflict mode and outcomes
                    if (conflictMode == ConflictMode.ContinueOnConflict)
                    {
                        // commit whatever succeeded (partial commit)
                        await sxmTrans.commitTransactionAsync();
                        committed = true;
                    }
                    else if (conflictMode == ConflictMode.FailOnFirstConflict)
                    {
                        // If any failure happened we must rollback; otherwise commit.
                        if (anyFailure)
                        {
                            await sxmTrans.rollbackTransactionAsync();
                            committed = false;
                        }
                        else
                        {
                            await sxmTrans.commitTransactionAsync();
                            committed = true;
                        }
                    }
                    else // RollbackOnAnyFailure (default)
                    {
                        if (anyFailure)
                        {
                            await sxmTrans.rollbackTransactionAsync();
                            committed = false;
                        }
                        else
                        {
                            await sxmTrans.commitTransactionAsync();
                            committed = true;
                        }
                    }
                }
                catch
                {
                    // Best-effort rollback if commit/processing failed.
                    try
                    {
                        await sxmTrans.rollbackTransactionAsync();
                    }
                    catch
                    {
                        // Swallow rollback exceptions — keep original exception semantics.
                    }

                    throw;
                }
                finally
                {
                    // Only clear the change set when commit succeeded.
                    if (committed)
                        _changeSet.Clear();
                }
            }

            report.AllSucceeded = !report.Failed.Any();
            report.Partial = report.Succeeded.Count > 0 && report.Failed.Count > 0;
            return report;
        }

        // ---------- Dispose ------------------------------
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                dConnection?.Dispose();
                _linqToDbDataConnection?.Dispose();
            }

            isDisposed = true;
        }
    }


    /// <summary>
    /// Controls SubmitChanges behavior when an individual operation throws.
    /// - FailOnFirstConflict: stop on first failure and rollback.
    /// - RollbackOnAnyFailure: default — if any action fails, rollback the whole unit.
    /// - ContinueOnConflict: continue processing and commit successful actions (partial commit).
    /// </summary>
    public enum ConflictMode
    {
        /// <summary>
        /// Stop on first failure and rollback.
        /// </summary>
        FailOnFirstConflict,

        /// <summary>
        /// If any action fails, rollback the whole unit. This is the new default.
        /// </summary>
        RollbackOnAnyFailure,

        /// <summary>
        /// Continue applying remaining actions when an action throws and commit successes.
        /// </summary>
        ContinueOnConflict,
    }

    // Aggregate result returned by SubmitChanges
    public class SubmitChangesResult
    {
        public List<ChangeAction> Succeeded { get; } = new List<ChangeAction>();
        public List<ChangeAction> Failed { get; } = new List<ChangeAction>();
        public bool AllSucceeded { get; set; }
        public bool Partial { get; set; }
    }
}

