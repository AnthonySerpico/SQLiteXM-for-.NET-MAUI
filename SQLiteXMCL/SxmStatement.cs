using System.Data;
using System.Reflection;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmData
    {
        private string? databaseName = default;

        public SxmData(string? dbName) { this.databaseName = dbName;  }
        public SxmData() { }

        public virtual async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName) where TResult : class, new()

        {
            return await SxmStatement.RunStatement<SxmData, TResult>(sqlStatementName, this, databaseName);
        }

        public virtual async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName)

        {
            List<Dictionary<string, object?>> result = await SxmStatement.RunStatement<SxmData>(sqlStatementName, this, databaseName);

            /*new(this.GetType()alues<SxmData>((Dictionary<string, object?>)result[0], ref this);

            PropertyInfo? userObjectPI = this.GetType().GetProperty("id");
            if (userObjectPI != default)
                userObjectPI.SetValue(this, ((Dictionary<string, object?>)result[0])["id"]);*/

            return result;
        }
    }

    public class SxmStatement
    {
        private SxmStatement() { }

        public static async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
                                                                                                                                                          where TResult : class, new()
        {
            SqlStatementType statementType =  SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues, databaseName);
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }
        public async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            
            return await RunStatement(sqlStatementName, selectParameterValues, databaseName);
        }
        public async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string))
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, databaseName);
        }
        public static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string))
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(databaseName))
                {
                    switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
                    {
                        case SqlStatementType.select:
                            recordData = await SxmSelectHelpers.performSelect(sqlStatementName, sqlStatementParameters, databaseName);
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.performInsert(sqlStatementName, sqlStatementParameters, databaseName));
                            break;

                        case SqlStatementType.update:
                            await SxmUpdateHelpers.performUpdate(sqlStatementName, sqlStatementParameters, databaseName);
                            break;

                        case SqlStatementType.delete:
                            await SxmDeleteHelpers.performDelete(sqlStatementName, sqlStatementParameters, databaseName);
                            break;

                        // Direct SQL statement queries are processed here.
                        case SqlStatementType.selectDirect:
                            recordData = await SxmSelectHelpers.performSelectDirect(sqlStatementName, sqlStatementParameters, databaseName);
                            break;

                        case SqlStatementType.deleteDirect:
                            await SxmDeleteHelpers.performDeleteDirect(sqlStatementName, sqlStatementParameters, databaseName);
                            break;

                        case SqlStatementType.updateDirect:
                            await SxmUpdateHelpers.performUpdateDirect(sqlStatementName, sqlStatementParameters, databaseName);
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
