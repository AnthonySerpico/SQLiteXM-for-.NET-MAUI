using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using SQLiteXM.Internal;

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
}
