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
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.executeDeleteAsync(sqlStatementName, sqlStatementParameters);
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
                await sxmTransaction.executeDeleteAsync(sqlStatementName, sqlStatementParameters);
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
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.executeDeleteDirectAsync(sqlStatement, sqlStatementParameters);
                    await sxmTransaction.commitTransactionAsync();
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
                await sxmTransaction.executeDeleteDirectAsync(sqlStatement, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

    }
}
