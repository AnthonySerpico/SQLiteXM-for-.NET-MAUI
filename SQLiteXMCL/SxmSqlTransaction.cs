using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmSqlTransaction : SxmTransaction
    {
        private string? dbName;

        public SxmSqlTransaction(string? dbName = default) : base(dbName)
        {
            this.dbName = dbName;
        }

        public async Task<List<M>> RunStatement<T, M>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                       where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues);
            List<M> userRecordList = SxmHelpers.populateUserRecord<M>(select);

            return userRecordList;
        }
        public async Task<List<T>> RunStatement<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where T : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            if (runSqlStatementResponse != default)
                return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);

            return new List<T>();
        }
        public async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            return await RunStatement(sqlStatementName, selectParameterValues);
        }
        public async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters });
        }
        public async Task<List<T>> RunStatement<T>(string sqlStatementName, List<object> sqlStatementParameters) where T : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);
        }
        public async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters)
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            try
            {
                switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
                {
                    case SqlStatementType.select:
                        recordData = await SxmSelectHelpers.performSelectTrans(sqlStatementName, sqlStatementParameters, this);
                        break;

                    case SqlStatementType.insert:
                        recordData = new List<Dictionary<string, object?>>(1);
                        recordData.Add(await SxmInsertHelpers.performInsertTrans(sqlStatementName, sqlStatementParameters, this));
                        break;

                    case SqlStatementType.update:
                        await SxmUpdateHelpers.performUpdateTrans(sqlStatementName, sqlStatementParameters, this);
                        break;

                    case SqlStatementType.delete:
                        await SxmDeleteHelpers.performDeleteTrans(sqlStatementName, sqlStatementParameters, this);
                        break;

                    default: break;
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
