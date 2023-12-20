using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmUpdateHelpers
    {
        private static async Task performUpdate<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            await performUpdate(sqlStatementName, selectParameterValues, dbName);
        }
        private static async Task performUpdate(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

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

            await Task.CompletedTask;
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

            await Task.CompletedTask;
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

            await Task.CompletedTask;
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

            await Task.CompletedTask;
        }
    }
}
