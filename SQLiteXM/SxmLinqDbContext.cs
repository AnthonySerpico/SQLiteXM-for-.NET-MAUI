using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Concurrent;

namespace SQLiteXM
{
    public class SxmLinqDbContext : IDisposable
    {
        // Static registry to track DataConnection -> SxmLinqDbContext mappings
        // This enables context recovery from IQueryable chains after LINQ operators like Where()
        private static readonly ConcurrentDictionary<DataConnection, WeakReference<SxmLinqDbContext>> _contextRegistry 
            = new ConcurrentDictionary<DataConnection, WeakReference<SxmLinqDbContext>>();

        private bool _isDisposed = false;
        private readonly Microsoft.Data.Sqlite.SqliteConnection? _sqliteConnection;
        private readonly SxmChangeSet _changeSet = new SxmChangeSet();
        private readonly LinqToDB.Data.DataConnection _linqToDbDataConnection;
        private string? _databaseName;

        public SxmLinqDbContext(string? databaseName = null)
        {
            try
            {
                SxmDatabase.EnsureInitialized();
                SxmConnection.CreateNewConnection(ref databaseName, ref _sqliteConnection);
                _databaseName = databaseName;

                if (_sqliteConnection == null)
                    throw new InvalidOperationException("Failed to create SQLite connection.");

                _linqToDbDataConnection = new LinqToDB.Data.DataConnection(LinqToDB.DataProvider.SQLite.SQLiteTools.GetDataProvider("Microsoft.Data.Sqlite"), _sqliteConnection);
                _linqToDbDataConnection.AddMappingSchema(SxmMapping.Schema);

                // Register this context with its DataConnection for context recovery
                _contextRegistry[_linqToDbDataConnection] = new WeakReference<SxmLinqDbContext>(this);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"SxmLinqDbContext ctor failure. Database: '{databaseName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"SxmLinqDbContext ctor failure. Database: '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        public SxmChangeSet GetChangeSet() => _changeSet;

        /// <summary>
        /// Attempts to recover the SxmLinqDbContext from a LinqToDB query provider.
        /// This enables context preservation through LINQ chains (Where, Select, etc.).
        /// </summary>
        /// <param name="query">The IQueryable to extract context from.</param>
        /// <returns>The associated SxmLinqDbContext if found; otherwise null.</returns>
        internal static SxmLinqDbContext? TryGetContextFromQuery<T>(IQueryable<T> query) where T : class
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
            // Wrap the provider table so callers get an IQueryable-like wrapper that also
            // exposes LoadWith without referencing LinqToDB. Pass this context for deferred bulk operations.
            return new SxmTable<T>(_linqToDbDataConnection.GetTable<T>(), this);
        }

        // Make raw provider escape hatches internal to prevent consumers from calling LinqToDB APIs directly.
        // Keeps the safe public SxmLinqDbContext surface (GetTable, Insert/Update/Delete lifecycles, SubmitChanges).
        // Advanced users inside the library (or friend assemblies) can still use these helpers.

        // Opt-in: return the raw LinqToDB ITable<T> when a caller truly needs LinqToDB APIs.
        internal ITable<T> GetRawTable<T>() where T : class
        {
            return _linqToDbDataConnection.GetTable<T>();
        }


        // Added explicit high-level helpers for advanced operations (BulkCopy, raw SQL, query execution).
        // Kept low-level WithDataConnectionAsync internal so only library code (or friend assemblies) may access DataConnection.

        // Controlled async escape-hatch for advanced library code that needs direct DataConnection access.
        // Internal to prevent application code from bypassing SxmLinqDbContext semantics.
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
        ///using var ctx = new SxmLinqDbContext();

        // Prepare many entities
        ///var batch = Enumerable.Range(1, 1000)
        ///.Select(i => new UserRecord { name = $"User {i}", address = "Bulk St" })
        ///.ToList();

        // Perform efficient bulk insert, returns rows copied
        ///long rowsCopied = await ctx.BulkCopyAsync(batch).ConfigureFalse();
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
            var result = await WithDataConnectionAsync(dc => dc.BulkCopyAsync(opts, entities)).ConfigureFalse();
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
        /// using var ctx = new SxmLinqDbContext();
        /// var rows = await ctx.QueryAsync("SELECT id, name, address FROM UserRecord WHERE id > @p0", 100).ConfigureFalse();
        /// foreach (var row in rows)
        ///     Console.WriteLine($"{row["id"]}: {row["name"]} - {row["address"]}");
        ///
        /// int affected = await ctx.ExecuteRawSqlAsync("UPDATE UserRecord SET address = {0} WHERE name = {1}", "New Addr", "Alice").ConfigureFalse();
        /// Note: ExecuteRawSqlAsync uses LinqToDB ExecuteAsync so it accepts LinqToDB-style placeholders.
        /// 
        /// Execute a SQL SELECT (or any query returning rows) and materialize the result as a
        /// list of dictionaries (column name -> value). Parameters are added as @p0, @p1, ...
        /// Example: QueryAsync("SELECT * FROM UserRecord WHERE id = @p0", 42)
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params object?[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
            if (_sqliteConnection == null) throw new InvalidOperationException("SQLite connection is not available.");

            // Use the owned SqliteConnection directly (safe — still not exposing it).
            await using SqliteCommand cmd = _sqliteConnection.CreateCommand();
            cmd.CommandText = sql;

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

        /// <summary>
        /// Enqueues a bulk update operation to be executed during SubmitChangesAsync within the transaction.
        /// </summary>
        /// <param name="bulkOperation">The delegate that executes the bulk update.</param>
        internal void EnqueueBulkUpdate(Func<Task<int>> bulkOperation)
        {
            if (bulkOperation == null) throw new ArgumentNullException(nameof(bulkOperation));
            _changeSet.AddBulkOperation(ChangeType.BulkUpdate, bulkOperation);
        }

        /// <summary>
        /// Enqueues a bulk delete operation to be executed during SubmitChangesAsync within the transaction.
        /// </summary>
        /// <param name="bulkOperation">The delegate that executes the bulk delete.</param>
        internal void EnqueueBulkDelete(Func<Task<int>> bulkOperation)
        {
            if (bulkOperation == null) throw new ArgumentNullException(nameof(bulkOperation));
            _changeSet.AddBulkOperation(ChangeType.BulkDelete, bulkOperation);
        }

        // ---------- SubmitChanges ------------------------
        /// <summary>
        /// Submits all pending changes (inserts, updates, deletes) within a single transaction.
        /// Default behavior: stops on first failure and rolls back the entire transaction.
        /// </summary>
        /// <returns>A SubmitChangesResult containing succeeded and failed operations.</returns>
        public async Task<SubmitChangesResult> SubmitChangesAsync()
        {
            return await SubmitChangesAsync(ConflictMode.FailOnFirstError).ConfigureFalse();
        }

        /// <summary>
        /// Submits all pending changes (inserts, updates, deletes) within a single transaction.
        /// </summary>
        /// <param name="conflictMode">Controls behavior when operations fail:
        /// - FailOnFirstError (default): Stop on first failure and rollback the entire transaction.
        /// - ContinueOnError: Continue processing all operations and commit successes (partial commit).
        /// </param>
        /// <returns>A SubmitChangesResult containing succeeded and failed operations.</returns>
        public async Task<SubmitChangesResult> SubmitChangesAsync(ConflictMode conflictMode)
        {
            var report = new SubmitChangesResult();
            if (_changeSet.IsEmpty)
            {
                report.AllSucceeded = true;
                return report;
            }

            // One transaction for the whole unit of work
            await using (SxmSqlTransaction sxmTrans = SxmSqlTransaction.Create())
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
                                    await action.Entity!.SaveAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.Delete:
                                    await action.Entity!.DeleteAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.InsertOrReplace:
                                    await action.Entity!.InsertOrReplaceAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.InsertOrUpdate:
                                    await action.Entity!.InsertOrUpdateAsync(sxmTrans).ConfigureFalse();
                                    break;

                                case ChangeType.BulkUpdate:
                                case ChangeType.BulkDelete:
                                    // Execute bulk operation within the transaction
                                    int rowsAffected = await action.BulkOperation!().ConfigureFalse();
                                    action.Result = new ChangeResult
                                    {
                                        Success = true,
                                        Error = null,
                                        RowsAffected = rowsAffected
                                    };
                                    report.Succeeded.Add(action);
                                    continue; // Skip entity-specific result handling below
                            }

                            // Success (entity operations)
                            action.Result = new ChangeResult
                            {
                                Success = true,
                                Error = null,
                                IdAfterOperation = action.Entity!.id > 0 ? action.Entity.id : null,
                                SynchIdAfterOperation = action.Entity.synchId
                            };

                            report.Succeeded.Add(action);
                        }
                        catch (Exception ex)
                        {
                            // Record failure
                            action.Result = new ChangeResult
                            {
                                Success = false,
                                Error = ex,
                                // Only set entity-specific fields if this is an entity operation
                                IdAfterOperation = action.Entity?.id > 0 ? action.Entity.id : null,
                                SynchIdAfterOperation = action.Entity?.synchId,
                                RowsAffected = 0  // Failed operations affect 0 rows
                            };

                            report.Failed.Add(action);

                            // Stop immediately if FailOnFirstError
                            if (conflictMode == ConflictMode.FailOnFirstError)
                                break;

                            // Otherwise continue processing (ContinueOnError)
                        }
                    }

                    // Commit/rollback decision
                    if (conflictMode == ConflictMode.ContinueOnError)
                    {
                        // Always commit (partial success is acceptable)
                        await sxmTrans.CommitTransactionAsync().ConfigureFalse();
                    }
                    else // FailOnFirstError
                    {
                        // Rollback if any failure; otherwise commit
                        if (report.Failed.Count > 0)
                        {
                            await sxmTrans.RollbackTransactionAsync().ConfigureFalse();
                        }
                        else
                        {
                            await sxmTrans.CommitTransactionAsync().ConfigureFalse();
                        }
                    }
                }
                catch
                {
                    // Best-effort rollback if commit/processing failed.
                    try
                    {
                        await sxmTrans.RollbackTransactionAsync().ConfigureFalse();
                    }
                    catch
                    {
                        // Swallow rollback exceptions — keep original exception semantics.
                    }

                    throw;
                }
                finally
                {
                    // Always clear the change set after any submit attempt.
                    // This prevents accidental duplicate submissions and makes the behavior predictable.
                    // If retry is needed, the caller must explicitly re-queue operations.
                    _changeSet.Clear();
                }
            }

            report.AllSucceeded = report.Failed.Count == 0;
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
                // Unregister from the context registry
                if (_linqToDbDataConnection != null)
                {
                    _contextRegistry.TryRemove(_linqToDbDataConnection, out _);
                }

                SxmConnection.CloseConnection(_sqliteConnection, _databaseName);
                _linqToDbDataConnection?.Dispose();
            }

            _isDisposed = true;
        }
    }


    /// <summary>
    /// Controls SubmitChanges behavior when an individual operation throws.
    /// </summary>
    public enum ConflictMode
    {
        /// <summary>
        /// (DEFAULT) Stop on first failure and rollback the entire transaction.
        /// Returns a SubmitChangesResult with the failed action in report.Failed.
        /// </summary>
        FailOnFirstError,

        /// <summary>
        /// Continue processing all actions even when failures occur, then commit successes.
        /// Inspect report.Succeeded and report.Failed to see partial results.
        /// </summary>
        ContinueOnError
    }

    /// <summary>
    /// Aggregate result returned by SubmitChanges.
    /// </summary>
    public class SubmitChangesResult
    {
        public List<ChangeAction> Succeeded { get; } = new List<ChangeAction>();
        public List<ChangeAction> Failed { get; } = new List<ChangeAction>();
        public bool AllSucceeded { get; set; }
        public bool Partial { get; set; }

        /// <summary>
        /// Returns true if any operations failed.
        /// </summary>
        public bool AnyFailed => Failed.Count > 0;

        /// <summary>
        /// Returns the total number of operations (succeeded + failed).
        /// </summary>
        public int TotalOperations => Succeeded.Count + Failed.Count;

        /// <summary>
        /// Returns a human-readable summary of the result.
        /// </summary>
        public string GetErrorSummary()
        {
            if (AllSucceeded) return "All operations succeeded.";

            var first = Failed.FirstOrDefault();
            if (Failed.Count == 1)
            {
                var entityInfo = first?.Entity != null
                    ? $" (Entity: {first.Entity.GetType().Name}, Id: {first.Entity.id})"
                    : string.Empty;
                return $"1 operation failed{entityInfo}: {first?.Result?.Error?.Message ?? "Unknown error"}";
            }

            var firstError = first?.Result?.Error?.Message ?? "Unknown error";
            return $"{Failed.Count} of {TotalOperations} operations failed. First error: {firstError}";
        }
    }

    /// <summary>
    /// Exception thrown when SubmitChanges fails and EnsureSuccess() is called.
    /// Contains the full SubmitChangesResult for detailed error inspection.
    /// </summary>
    public class SubmitChangesException : InvalidOperationException
    {
        /// <summary>
        /// The SubmitChangesResult containing detailed failure information.
        /// </summary>
        public SubmitChangesResult Result { get; }

        public SubmitChangesException(string message, SubmitChangesResult result)
            : base(message, result.Failed.FirstOrDefault()?.Result?.Error)
        {
            Result = result;
        }
    }

    /// <summary>
    /// Extension methods for SubmitChangesResult.
    /// </summary>
    public static class SubmitChangesResultExtensions
    {
        /// <summary>
        /// Throws a SubmitChangesException if any operations failed, otherwise returns the result for chaining.
        /// Use this when you want fail-fast behavior with exceptions.
        /// </summary>
        /// <param name="result">The SubmitChangesResult to check.</param>
        /// <returns>The same result if all operations succeeded.</returns>
        /// <exception cref="SubmitChangesException">Thrown when any operations failed.</exception>
        public static SubmitChangesResult ThrowIfFailed(this SubmitChangesResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            if (!result.AllSucceeded)
            {
                throw new SubmitChangesException(result.GetErrorSummary(), result);
            }

            return result;
        }
    }
}

