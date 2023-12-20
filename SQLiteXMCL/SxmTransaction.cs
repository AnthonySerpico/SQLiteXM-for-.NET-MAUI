using System.Data;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmTransaction : SxmUTransaction
    {
        private string? dbName;

        public SxmTransaction(string? dbName = default) : base(dbName)
        {
            this.dbName = dbName;
        }

        public async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                   where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues);
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }
        public async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        public async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            
            return await RunStatement(sqlStatementName, selectParameterValues);
        }
        public async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters });
        }
        public async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
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

                    case SqlStatementType.selectDirect:
                        recordData = await SxmSelectHelpers.performSelectTransDirect(sqlStatementName, sqlStatementParameters, this);
                        break;

                    case SqlStatementType.deleteDirect:
                        await SxmDeleteHelpers.performDeleteTransDirect(sqlStatementName, sqlStatementParameters, this);
                        break;

                    case SqlStatementType.updateDirect:
                        await SxmUpdateHelpers.performUpdateTransDirect(sqlStatementName, sqlStatementParameters, this);
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
