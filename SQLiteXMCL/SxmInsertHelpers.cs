using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmInsertHelpers
    {
        private static async Task<M> performInsert<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                    where M : class, new()
        {
            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            Dictionary<string, object?> select = await performInsert(sqlStatementName, selectParameterValues, dbName);

            M userRecordList = new M(); 
            SxmHelpers.loadDbValues(select, ref userRecordList);
            return userRecordList;
        }
        private static async Task<Dictionary<string, object?>> performInsert<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            return await performInsert(sqlStatementName, selectParameterValues, dbName);
        }
        private static async Task<T> performInsert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = new T();
            SxmHelpers.loadDbValues(select, ref userRecord);

            return userRecord;
        }
        private static async Task<T> performInsert<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = new T();
            SxmHelpers.loadDbValues(select, ref userRecord);

            return userRecord;
        }
        private static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }

        internal static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
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

        internal static async Task<Dictionary<string, object?>> performInsertTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
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
