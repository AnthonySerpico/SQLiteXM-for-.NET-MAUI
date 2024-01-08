using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    public class SxmLinqContext : System.Data.Linq.DataContext, IDisposable
    {
        private bool isDisposed;
        private System.Data.Common.DbConnection? dConnection = default(System.Data.Common.DbConnection);
        public SxmLinqContext(string? databaseName = default(string?)) : base(System.Data.SQLite.Linq.SQLiteProviderFactory.Instance.CreateDataSource(SxmConnection.getConnectionString(ref databaseName)).OpenConnection())
        {
            dConnection = this.Connection;
        }

        public async Task SubmitChanges(ConflictMode cm)
        {
            await SubmitChanges();
        }
        public async Task SubmitChanges()
        {
            try
            {
                ChangeSet cs = this.GetChangeSet();

                foreach (SxmEntity sxmEntity in cs.Inserts)
                {
                    await sxmEntity.Save();
                }

                foreach (SxmEntity sxmEntity in cs.Updates)
                {
                    await sxmEntity.Save();
                }

                foreach (SxmEntity sxmEntity in cs.Deletes)
                {
                    await sxmEntity.Delete();
                }
            }
            catch (Exception ex)
            {

            }
        }

        // Dispose() calls Dispose(true)
        new public void Dispose()
        {
            base.Dispose();

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code is implemented in Dispose(bool)
        new protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            if (disposing && dConnection != default(System.Data.Common.DbConnection))
            {
                // Free managed resources
                dConnection.Dispose();
            }

            isDisposed = true;
        }
    }
}
