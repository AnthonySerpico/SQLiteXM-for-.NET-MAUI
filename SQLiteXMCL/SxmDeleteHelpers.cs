using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmDeleteHelpers
    {
        public static async Task performDelete<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            await performDelete(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task performDelete(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default)
        {
            await performDelete(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

        public static async Task performDelete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    sxmTransaction.executeDelete(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask;
        }

        public static async Task performDeleteTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmTransaction sxmTransaction)
        {
            try
            {
                sxmTransaction.executeDelete(sqlStatementName, sqlStatementParameters);
                sxmTransaction.commitTransaction();
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask;
        }
    }
}
