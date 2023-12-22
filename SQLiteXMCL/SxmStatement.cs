using System.Data;
using System.Reflection;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SxmData
    {
        private string? databaseName = default;

        public SxmData(string? dbName) { this.databaseName = dbName; }
        public SxmData() { }

        public virtual async Task PerformInsert(string sqlStatementName)
        {
            Dictionary<string, object?> result = await SxmStatement.PerformInsert<SxmData>(sqlStatementName, this, databaseName);
            loadDbValues(result);
        }

        public virtual async Task PerformUpdate(string sqlStatementName)
        {
            await SxmStatement.PerformUpdate<SxmData>(sqlStatementName, this, databaseName);
        }

        public virtual async Task PerformDelete(string sqlStatementName)
        {
            await SxmStatement.PerformDelete<SxmData>(sqlStatementName, this, databaseName);
        }


        private void loadDbValues(Dictionary<string, object?> databaseRecord)
        {
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = this.GetType().GetProperty(kvp.Key);
                    if (pi != null)
                    {
                        if (kvp.Value != DBNull.Value && kvp.Value != null)
                        {
                            string piType = pi.PropertyType.Name;

                            if (piType == typeof(int).Name)
                                pi.SetValue(this, (int)(long)kvp.Value);
                            else if (piType == typeof(long).Name)
                                pi.SetValue(this, (long)kvp.Value);
                            else if (piType == typeof(float).Name)
                                pi.SetValue(this, (float)(double)kvp.Value);
                            else if (piType == typeof(double).Name)
                                pi.SetValue(this, (double)kvp.Value);
                            else if (piType == typeof(decimal).Name)
                                pi.SetValue(this, (decimal)(double)kvp.Value);
                            else if (piType == typeof(string).Name)
                                pi.SetValue(this, kvp.Value.ToString());
                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(this, true);
                                else
                                    pi.SetValue(this, false);
                            }
                            else if (piType == typeof(DateTime).Name)
                            {
                                if (kvp.Value.GetType().Name == typeof(string).Name)
                                    pi.SetValue(this, DateTime.Parse(kvp.Value.ToString()!));
                                if (kvp.Value.GetType().Name == typeof(double).Name)
                                    pi.SetValue(this, new DateTime((long)(double)kvp.Value));
                            }
                            else
                                pi.SetValue(this, kvp.Value);

                        }
                        else
                            pi.SetValue(this, default);
                    }
                }
                catch (System.ArgumentException)
                {
                    string? userPropertyType = this.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString();
                    string? databasePropertyType = kvp.Value?.GetType().ToString();
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", kvp.Key, databasePropertyType, this.GetType().ToString() + "." + kvp.Key, userPropertyType));
                }
            }
        }
    }

    public class SxmStatement
    {
        private SxmStatement() { }


        /************************************************************************* INSERT ********************************************************************/
        public static async Task<TResult> PerformInsert<T, TResult>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                               where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if(statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatement<T, TResult>(sqlStatementName, userObjectParameters, dbName);
            return select[0];
        }
        public static async Task<Dictionary<string, object?>> PerformInsert<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement<T>(sqlStatementName, userObjectParameters, dbName);
            return select[0];
        }
        public static async Task<T> PerformInsert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName);
            return select[0];
        }
        public static async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
            return select[0];
        }
        public static async Task<T> PerformInsert<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName);
            return select[0];
        }
        public static async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
            return select[0];
        }


        /************************************************************************* SELECT ********************************************************************/
        public static async Task<List<Dictionary<string, object?>>> PerformSelect<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, userObjectParameters, dbName);
        }
        public static async Task<List<M>> PerformSelect<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T, M>(sqlStatementName, userObjectParameters, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
        }
        public static async Task<List<T>> PerformSelect<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName);
        }
        public static async Task<List<T>> PerformSelect<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> PerformSelect(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
        }


        /************************************************************************* DELETE ********************************************************************/

        public static async Task PerformDelete<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters, dbName);
        }
        public static async Task PerformDelete(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
        }
        public static async Task PerformDelete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName);


        }


        /************************************************************************* UPDATE ********************************************************************/

        public static async Task PerformUpdate<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters, dbName);
        }
        public static async Task PerformUpdate(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
        }
        public static async Task PerformUpdate(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName);
        }



        /************************************************************************* GENERIC ********************************************************************/
        private static async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
                                                                                                                                                          where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues, databaseName);
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }
        private async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName);

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private static async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not allowed.");

            Dictionary<string, string> columnNames = SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);

            return await RunStatement(sqlStatementName, selectParameterValues, databaseName);
        }
        private async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName);

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }
        private static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string))
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, databaseName);
        }
        private static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string))
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
