using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmInsertHelpers
    {
        public static async Task<M> performInsert<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                   where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            Dictionary<string, object?> select = await performInsert(sqlStatementName, selectParameterValues, dbName);

            M userRecordList = SxmHelpers.loadDbValues<M>(select);
            return userRecordList;
        }
        public static async Task<Dictionary<string, object?>> performInsert<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            return await performInsert(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<T> performInsert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = SxmHelpers.loadDbValues<T>(select);

            return userRecord;
        }
        public static async Task<T> performInsert<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = SxmHelpers.loadDbValues<T>(select);

            return userRecord;
        }
        public static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

        public static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    ir = sxmTransaction.executeInsert(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir);
        }

        public static async Task<Dictionary<string, object?>> performInsertTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;

            try
            {
                ir = sxmTransaction.executeInsert(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir);
        }
    }
}
