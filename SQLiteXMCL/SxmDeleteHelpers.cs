using SQLiteXM.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmDeleteHelpers
    {
        internal static async Task performDelete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
                {
                    sxmTransaction.executeDelete(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        internal static async Task performDeleteTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                sxmTransaction.executeDelete(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }
        internal static async Task performDeleteDirect(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
                {
                    sxmTransaction.executeDeleteDirect(sqlStatement, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }
        internal static async Task performDeleteTransDirect(string sqlStatement, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                sxmTransaction.executeDeleteDirect(sqlStatement, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

    }
}
