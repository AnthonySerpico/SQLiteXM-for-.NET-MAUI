using SQLiteXM.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmUpdateHelpers
    {
        internal static async Task performUpdate(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
                {
                    sxmTransaction.executeUpdate(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        internal static async Task performUpdateTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                sxmTransaction.executeUpdate(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        internal static async Task performUpdateDirect(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
                {
                    sxmTransaction.executeUpdateDirect(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        internal static async Task performUpdateTransDirect(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                sxmTransaction.executeUpdateDirect(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }
    }
}
