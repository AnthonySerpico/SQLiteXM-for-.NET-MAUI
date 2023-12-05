using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmRunStatement
    {
        private SxmRunStatement() { }

        public static async Task<List<M>> RunStatement<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues, dbName);
            List<M> userRecordList = SxmHelpers.populateUserRecord<M>(select);

            return userRecordList;
        }
        public async static Task<List<T>> RunStatement<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default(string)) where T : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
            if (runSqlStatementResponse != default)
                return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);

            return new List<T>();
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            return await RunStatement(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default(string))
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public async static Task<List<T>> RunStatement<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default(string)) where T : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
            if (runSqlStatementResponse != default)
                return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);

            return new List<T>();
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default(string))
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
                    {
                        case SqlStatementType.select:
                            recordData = await SxmSelectHelpers.performSelect(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.performInsert(sqlStatementName, sqlStatementParameters, dbName));
                            break;

                        case SqlStatementType.update:
                            await SxmUpdateHelpers.performUpdate(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case SqlStatementType.delete:
                            await SxmDeleteHelpers.performDelete(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        default: break;
                    }
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            if (recordData == default(List<Dictionary<string, object?>>))
                recordData = new List<Dictionary<string, object?>>();

            return await Task.FromResult(recordData);
        }
    }
}
