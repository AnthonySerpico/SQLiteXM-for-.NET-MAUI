using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmUpdateHelpers
    {
        public static async Task performUpdate<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            await performUpdate(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task performUpdate(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

        public static async Task performUpdate(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
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

        public static async Task performUpdateTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmTransaction sxmTransaction)
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
    }
}
