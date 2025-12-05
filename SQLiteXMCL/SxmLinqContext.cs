using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using SQLiteXM.Internal;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LinqToDB;

namespace SQLiteXM
{
    public class SxmLinqContext : IDisposable
    {
        private bool isDisposed = false;
        private readonly SqliteConnection? dConnection;
        private readonly DataConnection? _dataConnection;

        private readonly SxmChangeSet _changeSet = new SxmChangeSet();

        public SxmLinqContext(string? databaseName = null)
        {
            string connStr = SxmConnection.getConnectionString(ref databaseName);
            dConnection = new SqliteConnection(connStr);
            dConnection.Open();

            _dataConnection = new DataConnection(
                LinqToDB.DataProvider.SQLite.SQLiteTools.GetDataProvider("Microsoft.Data.Sqlite"),
                dConnection
            );

            DataConnection.AddMappingSchema(SxmMapping.Schema);
        }

        private DataConnection DataConnection => _dataConnection!;

        // LinqToDB table access
        public SxmTable<T> GetTable<T>() where T : class
        {
            // Wrap the provider table so callers get an IQueryable-like wrapper that also
            // exposes LoadWith without referencing LinqToDB.
            return new SxmTable<T>(DataConnection.GetTable<T>());
        }

        public SxmChangeSet GetChangeSet() => _changeSet;

        // ---------- Convenience async helpers to avoid exposing DataConnection externally ----------
        /// <summary>
        /// Insert the given entity using the underlying DataConnection.
        /// Use this instead of calling DataConnection.InsertAsync(...) from outside this assembly.
        /// </summary>
        public Task InsertAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _dataConnection!.InsertAsync(entity);
        }

        /// <summary>
        /// Update the given entity using the underlying DataConnection.
        /// Use this instead of calling DataConnection.UpdateAsync(...) from outside this assembly.
        /// </summary>
        public Task UpdateAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _dataConnection!.UpdateAsync(entity);
        }

        /// <summary>
        /// Delete the given entity using the underlying DataConnection.
        /// </summary>
        public Task DeleteAsync<T>(T entity) where T : class
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return _dataConnection!.DeleteAsync(entity);
        }
        // -------------------------------------------------------------------------------------------

        // ---------- Change tracking API ------------------

        public void InsertOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsInsert();

            if (!_changeSet.Inserts.Contains(entity))
                _changeSet.Inserts.Add(entity);

            _changeSet.Updates.Remove(entity);
            _changeSet.Deletes.Remove(entity);
        }

        public void UpdateOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsUpdate();

            if (!_changeSet.Updates.Contains(entity))
                _changeSet.Updates.Add(entity);

            _changeSet.Inserts.Remove(entity);
            _changeSet.Deletes.Remove(entity);
        }

        public void DeleteOnSubmit<T>(T entity) where T : SxmEntity
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            entity.MarkAsDelete();

            if (!_changeSet.Deletes.Contains(entity))
                _changeSet.Deletes.Add(entity);

            _changeSet.Inserts.Remove(entity);
            _changeSet.Updates.Remove(entity);
        }

        // Backwards-compatible name
        public void Delete<T>(T entity) where T : SxmEntity
        {
            DeleteOnSubmit(entity);
        }

        // ---------- SubmitChanges ------------------------

        public async Task SubmitChanges()
        {
            await SubmitChanges(ConflictMode.FailOnFirstConflict).CAF();
        }

        public async Task SubmitChanges(ConflictMode conflictMode)
        {
            if (_changeSet.IsEmpty)
                return;

            // One transaction for the whole unit of work
            using (var sxmTrans = new SxmTransaction())
            {
                try
                {
                    // INSERTS
                    foreach (var e in _changeSet.Inserts.ToList())
                    {
                        try
                        {
                            await e.Save(sxmTrans).CAF();
                        }
                        catch
                        {
                            if (conflictMode == ConflictMode.FailOnFirstConflict)
                                throw;
                            // ContinueOnConflict: skip this one, try to apply the rest
                        }
                    }

                    // UPDATES
                    foreach (var e in _changeSet.Updates.ToList())
                    {
                        try
                        {
                            await e.Save(sxmTrans).CAF();
                        }
                        catch
                        {
                            if (conflictMode == ConflictMode.FailOnFirstConflict)
                                throw;
                        }
                    }

                    // DELETES
                    foreach (var e in _changeSet.Deletes.ToList())
                    {
                        try
                        {
                            await e.Delete(sxmTrans).CAF();
                        }
                        catch
                        {
                            if (conflictMode == ConflictMode.FailOnFirstConflict)
                                throw;
                        }
                    }

                    // If we get here without an exception in FailOnFirstConflict mode,
                    // or we are in ContinueOnConflict mode and are okay with partial success,
                    // commit the transaction.
                    sxmTrans.commitTransaction();
                }
                catch
                {
                    // If SxmTransaction supports rollback, this is where you'd call it.
                    // sxmTrans.rollbackTransaction();
                    throw;
                }
                finally
                {
                    _changeSet.Clear();
                }
            }
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
                _dataConnection?.Dispose();
            }

            isDisposed = true;
        }
    }

    public enum ConflictMode
    {
        FailOnFirstConflict,
        ContinueOnConflict
    }

    /// <summary>
    /// Lightweight wrapper around IQueryable<T> that exposes an instance LoadWith(...) API
    /// so callers in the main app don't need to reference LinqToDB.
    /// </summary>
    public sealed class SxmTable<T> : IQueryable<T>
        where T : class
    {
        private readonly IQueryable<T> _inner;

        public SxmTable(IQueryable<T> inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        // Instance LoadWith that forwards to LinqToDB when possible.
        public SxmTable<T> LoadWith<TProperty>(Expression<Func<T, TProperty>> navigationProperty)
        {
            if (_inner is ITable<T> table)
            {
                // This resolves LinqToDB's LoadWith extension for ITable<T>
                var newQuery = table.LoadWith(navigationProperty);
                return new SxmTable<T>(newQuery);
            }

            // fallback: no-op (query stays unchanged)
            return this;
        }

        // Overload for multiple navigation properties
        public SxmTable<T> LoadWith(params Expression<Func<T, object>>[] navigationProperties)
        {
            if (_inner is ITable<T> table)
            {
                IQueryable<T> q = table;
                foreach (var prop in navigationProperties)
                {
                    q = ((ITable<T>)q).LoadWith(prop);
                }
                return new SxmTable<T>(q);
            }
            return this;
        }

        // IQueryable<T> implementation - delegate to the underlying query
        public Type ElementType => _inner.ElementType;
        public Expression Expression => _inner.Expression;
        public IQueryProvider Provider => _inner.Provider;

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();

        public override string? ToString() => _inner.ToString();
    }
}

