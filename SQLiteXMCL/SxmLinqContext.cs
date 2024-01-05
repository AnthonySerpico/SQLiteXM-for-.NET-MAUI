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
        public SxmLinqContext(string? databaseName = default(string?)) : base(System.Data.SQLite.Linq.SQLiteProviderFactory.Instance.CreateDataSource(SxmConnection.getConnectionString(databaseName)).OpenConnection())
        {
            dConnection = this.Connection;
        }

        public void SubmitChanges()
        {
            try
            {
                ChangeSet cs = this.GetChangeSet();

                foreach (SxmEntity sxmEntity in cs.Inserts)
                {
                    sxmEntity.Save();
                }

                foreach (SxmEntity sxmEntity in cs.Updates)
                {
                    sxmEntity.Save();
                }

                foreach (SxmEntity sxmEntity in cs.Deletes)
                {
                    sxmEntity.Delete();
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
                // free managed resources
                dConnection.Dispose();
            }

            isDisposed = true;
        }
    }
}
