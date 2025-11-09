using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;

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
        }

        public DataConnection DataConnection => _dataConnection!;

        // LinqToDB table access
        public ITable<T> GetTable<T>() where T : class
        {
            return DataConnection.GetTable<T>();
        }

        public SxmChangeSet GetChangeSet() => _changeSet;

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

            if (!_changeSet.Inserts.Contains(entity) &&
                !_changeSet.Updates.Contains(entity))
            {
                _changeSet.Updates.Add(entity);
            }

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
            await SubmitChanges(ConflictMode.FailOnFirstConflict);
        }

        public async Task SubmitChanges(ConflictMode conflictMode)
        {
            try
            {
                // Inserts + updates both call your existing Save()
                foreach (var e in _changeSet.Inserts.ToList())
                {
                    try
                    {
                        await e.Save();
                    }
                    catch
                    {
                        if (conflictMode == ConflictMode.FailOnFirstConflict)
                            throw;
                    }
                }

                foreach (var e in _changeSet.Updates.ToList())
                {
                    try
                    {
                        await e.Save();
                    }
                    catch
                    {
                        if (conflictMode == ConflictMode.FailOnFirstConflict)
                            throw;
                    }
                }

                foreach (var e in _changeSet.Deletes.ToList())
                {
                    try
                    {
                        await e.Delete();
                    }
                    catch
                    {
                        if (conflictMode == ConflictMode.FailOnFirstConflict)
                            throw;
                    }
                }
            }
            finally
            {
                _changeSet.Clear();
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
}
