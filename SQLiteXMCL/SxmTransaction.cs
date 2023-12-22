using System.Data;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmTransaction : SxmUTransaction
    {
        private string? databaseName = default;

        public SxmTransaction(string? databaseName) : base(databaseName)
        {
            this.databaseName = databaseName;
        }
        public SxmTransaction() : base()
        {
        }

        /************************************************************************* INSERT ********************************************************************/
        public async Task<TResult> PerformInsert<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                                                where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatement<T, TResult>(sqlStatementName, userObjectParameters);
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement<T>(sqlStatementName, userObjectParameters);
            return select[0];
        }
        public async Task<T> PerformInsert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters);
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters });
            return select[0];
        }
        public async Task<T> PerformInsert<T>(string sqlStatementName, List<object> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters);
            return select[0];
        }
        public async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, sqlStatementParameters);
            return select[0];
        }


        /************************************************************************* SELECT ********************************************************************/
        public async Task<List<Dictionary<string, object?>>> PerformSelect<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, userObjectParameters);
        }
        public async Task<List<M>> PerformSelect<T, M>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T, M>(sqlStatementName, userObjectParameters);
        }
        public async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters);
        }
        public async Task<List<T>> PerformSelect<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters);
        }
        public async Task<List<T>> PerformSelect<T>(string sqlStatementName, List<object> sqlStatementParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters);
        }
        public async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters);
        }


        /************************************************************************* DELETE ********************************************************************/

        public async Task PerformDelete<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters);
        }
        public async Task PerformDelete(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters);
        }
        public async Task PerformDelete(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters);
        }


        /************************************************************************* UPDATE ********************************************************************/

        public async Task PerformUpdate<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters);
        }
        public async Task PerformUpdate(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters);
        }
        public async Task PerformUpdate(string sqlStatementName, List<object> sqlStatementParameters)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters);
        }


        /************************************************************************* GENERIC ********************************************************************/

        private async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters) where T : class, new()
                                                                                                                   where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues);
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }
        private async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            
            return await RunStatement(sqlStatementName, selectParameterValues);
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters)
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters });
        }
        private async Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters);
            
            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters)
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
