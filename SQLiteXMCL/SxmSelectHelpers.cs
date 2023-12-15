using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmSelectHelpers
    {
        public static async Task<List<Dictionary<string, object?>>> performSelect<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            return await performSelect(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<List<M>> performSelect<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await performSelect(sqlStatementName, selectParameterValues, dbName);
            List<M> userRecordList = SxmHelpers.populateUserRecord<M>(select);
            return userRecordList;
        }
        public static async Task<List<T>> performSelect<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            List<Dictionary<string, object?>> select = await performSelect(sqlStatementName, sqlStatementParameters, dbName);
            List<T> userRecordList = SxmHelpers.populateUserRecord<T>(select);
            return userRecordList;
        }
        public static async Task<List<T>> performSelect<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            List<Dictionary<string, object?>> select = await performSelect(sqlStatementName, sqlStatementParameters, dbName);
            List<T> userRecordList = SxmHelpers.populateUserRecord<T>(select);
            return userRecordList;
        }
        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            return await performSelect(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    sxmTransaction.executeQuery(sqlStatementName, sqlStatementParameters);
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows);
        }
        public static async Task<List<Dictionary<string, object?>>> performSelectTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                sxmTransaction.executeQuery(sqlStatementName, sqlStatementParameters);
                selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows);
        }
    }
}
