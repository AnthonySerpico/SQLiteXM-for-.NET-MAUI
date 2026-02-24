using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using SQLiteXM.Internal.Threading;
using System;

namespace SQLiteXM
{
    public class SxmLinqContext : IDisposable
    {
        private bool _isDisposed = false;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _sqliteConnection;
        private readonly SxmChangeSet _changeSet = new SxmChangeSet();
        private readonly LinqToDB.Data.DataConnection _linqToDbDataConnection;

        public SxmLinqContext(string? databaseName = null)
        {
            SxmInit.EnsureInitialized();

            string connStr = SxmConnection.GetConnectionString(ref databaseName);
            _sqliteConnection = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
            _sqliteConnection.Open();

            SxmInitOptions.RunConnectionPragmas(_sqliteConnection);

            _linqToDbDataConnection = new LinqToDB.Data.DataConnection(LinqToDB.DataProvider.SQLite.SQLiteTools.GetDataProvider("Microsoft.Data.Sqlite"), _sqliteConnection);
            _linqToDbDataConnection.AddMappingSchema(SxmMapping.Schema);
        }

        public SxmChangeSet GetChangeSet() => _changeSet;

        // LinqToDB table access
        public SxmTable<T> GetTable<T>() where T : class
        {
            // Wrap the provider table so callers get an IQueryable-like wrapper that also
            // exposes LoadWith without referencing LinqToDB.
            return new SxmTable<T>(_linqToDbDataConnection.GetTable<T>());
        }

        // Make raw provider escape hatches internal to prevent consumers from calling LinqToDB APIs directly.
        // Keeps the safe public SxmLinqContext surface (GetTable, Insert/Update/Delete lifecycles, SubmitChanges).
        // Advanced users inside the library (or friend assemblies) can still use these helpers.

        // Opt-in: return the raw LinqToDB ITable<T> when a caller truly needs LinqToDB APIs.
        internal ITable<T> GetRawTable<T>() where T : class
        {
            return _linqToDbDataConnection.GetTable<T>();
        }


        // Added explicit high-level helpers for advanced operations (BulkCopy, raw SQL, query execution).
        // Kept low-level WithDataConnectionAsync internal so only library code (or friend assemblies) may access DataConnection.

        // Controlled async escape-hatch for advanced library code that needs direct DataConnection access.
        // Internal to prevent application code from bypassing SxmLinqContext semantics.
        // Do NOT dispose or retain the DataConnection instance — it's owned by this context.
        private async Task<T> WithDataConnectionAsync<T>(Func<LinqToDB.Data.DataConnection, Task<T>> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return await action(_linqToDbDataConnection).ConfigureFalse();
        }

        // -------------------------
        // High-level advanced helpers
        // -------------------------

        /// <summary>
        /// // C# example (caller in app code)
        ///using var ctx = new SxmLinqContext();

        // Prepare many entities
        ///var batch = Enumerable.Range(1, 1000)
        ///.Select(i => new UserRecord { name = $"User {i}", address = "Bulk St" })
        ///.ToList();

        // Perform efficient bulk insert, returns rows copied
        ///long rowsCopied = await ctx.BulkCopyAsync(batch);
        ///Console.WriteLine($"Rows copied: {rowsCopied}");
        ///
        /// 
        /// Perform a bulk copy of the provided entities using LinqToDB bulk API.
        /// Returns number of rows copied.
        /// This is a controlled helper that does not expose the DataConnection to callers.
        /// </summary>
        private async Task<long> BulkCopyAsync<T>(IEnumerable<T> entities, LinqToDB.Data.BulkCopyOptions? options = null) where T : class
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));

            var opts = options ?? new LinqToDB.Data.BulkCopyOptions();
            var result = await WithDataConnectionAsync(dc => dc.BulkCopyAsync(opts, entities));
            return result?.RowsCopied ?? 0L;
        }

        /// <summary>
        /// Execute a raw SQL statement (non-query) on the underlying connection.
        /// Returns the number of rows affected.
        /// This helper accepts SQL and parameters and runs it safely on the internal DataConnection.
        /// </summary>
        private Task<int> ExecuteRawSqlAsync(string sql, params object[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
            // Use internal escape hatch — still keeps DataConnection out of public API surface.
            return WithDataConnectionAsync(dc => dc.ExecuteAsync(sql, parameters));
        }

        /// <summary>
        /// Execute a LINQ query produced by the provided factory against the internal table and return a materialized list.
        /// The factory receives an SxmTable<T> so callers do not need to reference LinqToDB types.
        /// Use this when you need to run slightly more complex queries but want to remain within the safe API.
        /// </summary>
        private Task<List<T>> ExecuteQueryAsync<T>(Func<SxmTable<T>, IQueryable<T>> queryFactory) where T : class
        {
            if (queryFactory == null) throw new ArgumentNullException(nameof(queryFactory));

            // Execute synchronously (materialize) on the internal connection / table.
            var table = new SxmTable<T>(_linqToDbDataConnection.GetTable<T>());
            var q = queryFactory(table) ?? Enumerable.Empty<T>().AsQueryable();

            // Materialize synchronously and return as completed Task — caller can await.
            List<T> list = q.ToList();
            return Task.FromResult(list);
        }

        /// <summary>
        /// 
        /// using var ctx = new SxmLinqContext();
        /// var rows = await ctx.QueryAsync("SELECT id, name, address FROM UserRecord WHERE id > @p0", 100);
        /// foreach (var row in rows)
        ///     Console.WriteLine($"{row["id"]}: {row["name"]} - {row["address"]}");
        ///
        /// int affected = await ctx.ExecuteRawSqlAsync("UPDATE UserRecord SET address = {0} WHERE name = {1}", "New Addr", "Alice");
        /// Note: ExecuteRawSqlAsync uses LinqToDB ExecuteAsync so it accepts LinqToDB-style placeholders.
        /// 
        /// Execute a SQL SELECT (or any query returning rows) and materialize the result as a
        /// list of dictionaries (column name -> value). Parameters are added as @p0, @p1, ...
        /// Example: QueryAsync("SELECT * FROM UserRecord WHERE id = @p0", 42)
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params object?[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));

            // Use the owned SqliteConnection directly (safe — still not exposing it).
            await using var cmd = _sqliteConnection.CreateCommand();
            cmd.CommandText = sql;

            // Add parameters named @p0, @p1, ... to keep the API simple.
            for (int i = 0; i < (parameters?.Length ?? 0); i++)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = $"@p{i}";
                param.Value = parameters[i] ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            var results = new List<Dictionary<string, object?>>();

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureFalse();
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

        /// <summary>
        /// Insert or replace the given entity asynchronously.
        /// </summary>
        /// <typeparam name="T">Entity type.</typeparam>
        /// <param name="entity">Entity to insert or replace.</param>
        /// <returns>Number of affected rows (provider-specific).</returns>
        public async Task<int> InsertOrReplaceAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity is SxmEntity sxm)
            {
                // entity is SxmEntity or derived — use sxm
                await sxm.SaveAsync().ConfigureFalse();
                return 1;
            }
            else
            {
                throw new InvalidOperationException($"Entity {nameof(entity)} must be a SxmEntity.");
            }
        }

        /// <summary>
        /// Insert or update the given entity asynchronously.
        /// </summary>
        /// <remarks>
        /// Many providers expose either InsertOrUpdate or InsertOrReplace semantics.
        /// For the SQLite provider we alias InsertOrUpdate to InsertOrReplace which
        /// matches the SQLite behavior (INSERT OR REPLACE).
        /// </remarks>
        /// <typeparam name="T">Entity type.</typeparam>
        /// <param name="entity">Entity to insert or update.</param>
        /// <returns>Number of affected rows (provider-specific).</returns>
        public async Task<int> InsertOrUpdateAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            // Alias to InsertOrReplace for SQLite; keep surface for consumers.
            return await InsertOrReplaceAsync(entity);
        }

        // -------------------------
        // Insert APIs (entity-safe)
        // -------------------------

        /// <summary>
        /// Insert the given SxmEntity asynchronously; runs <c>Save()</c> lifecycle.
        /// </summary>
        public async Task InsertAsync(SxmEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            await entity.SaveAsync().ConfigureFalse();
        }

        // -------------------------
        // Update APIs
        // -------------------------

        /// <summary>
        /// Update the given SxmEntity using its lifecycle (runs <c>Save()</c> so any internal processing happens).
        /// Use this overload for SxmEntity instances.
        /// </summary>
        public Task UpdateAsync(SxmEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.SaveAsync();
        }

        /// <summary>
        /// Generic update convenience that calls LinqToDB.Data.DataConnection.UpdateAsync(entity).
        /// This bypasses SxmEntity lifecycle processing and should be used intentionally
        /// (useful for non-entity types or bulk scenarios).
        /// </summary>
        public Task UpdateAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _linqToDbDataConnection!.UpdateAsync(entity);
        }

        // -------------------------
        // Delete APIs
        // -------------------------

        /// <summary>
        /// Delete the given SxmEntity using its lifecycle (runs <c>Delete()</c> so any internal processing happens).
        /// Use this overload for SxmEntity instances.
        /// </summary>
        public Task DeleteAsync(SxmEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.DeleteAsync();
        }

        /// <summary>
        /// Generic delete convenience that calls LinqToDB.Data.DataConnection.DeleteAsync(entity).
        /// This bypasses SxmEntity lifecycle processing and should be used intentionally
        /// (useful for non-entity types or bulk scenarios).
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

            _changeSet.Add(entity, ChangeType.Insert);
        }

        public void UpdateOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _changeSet.Add(entity, ChangeType.Update);
        }

        public void DeleteOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _changeSet.Add(entity, ChangeType.Delete);
        }

        public void InsertOrReplaceOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _changeSet.Add(entity, ChangeType.InsertOrReplace);
        }

        public void InsertOrUpdateOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            _changeSet.Add(entity, ChangeType.InsertOrUpdate);
        }

        // ---------- SubmitChanges ------------------------
        // Default now uses RollbackOnAnyFailure (strict atomic behavior).
        public async Task<SubmitChangesResult> SubmitChangesAsync()
        {
            return await SubmitChangesAsync(ConflictMode.RollbackOnAnyFailure).ConfigureFalse();
        }

        public async Task<SubmitChangesResult> SubmitChangesAsync(ConflictMode conflictMode)
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
                    List<ChangeAction> actions = _changeSet.GetOrderedActions().ToList();

                    foreach (ChangeAction action in actions)
                    {
                        try
                        {
                            switch (action.Type)
                            {
                                case ChangeType.Insert:
                                case ChangeType.Update:
                                    // Save decides insert vs update based on existence; use transaction-aware overload.
                                    await action.Entity.SaveAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.Delete:
                                    await action.Entity.DeleteAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.InsertOrReplace:
                                    await action.Entity.InsertOrReplaceAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.InsertOrUpdate:
                                    await action.Entity.InsertOrUpdateAsync(sxmTrans).ConfigureFalse();
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
                        await sxmTrans.CommitTransactionAsync();
                        committed = true;
                    }
                    else if (conflictMode == ConflictMode.FailOnFirstConflict)
                    {
                        // If any failure happened we must rollback; otherwise commit.
                        if (anyFailure)
                        {
                            await sxmTrans.RollbackTransactionAsync();
                            committed = false;
                        }
                        else
                        {
                            await sxmTrans.CommitTransactionAsync();
                            committed = true;
                        }
                    }
                    else // RollbackOnAnyFailure (default)
                    {
                        if (anyFailure)
                        {
                            await sxmTrans.RollbackTransactionAsync();
                            committed = false;
                        }
                        else
                        {
                            await sxmTrans.CommitTransactionAsync();
                            committed = true;
                        }
                    }
                }
                catch
                {
                    // Best-effort rollback if commit/processing failed.
                    try
                    {
                        await sxmTrans.RollbackTransactionAsync();
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
            if (_isDisposed) return;

            if (disposing)
            {
                SxmInitOptions.RunConnectionClosing(_sqliteConnection);

                _sqliteConnection?.Dispose();  // Closes the connection.
                _linqToDbDataConnection?.Dispose();
            }

            _isDisposed = true;
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

