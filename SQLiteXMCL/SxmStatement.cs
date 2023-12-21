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

        public virtual async Task<List<TResult>> RunSelectStatement<TResult>(string sqlStatementName) where TResult : class, new()

        {
            return await SxmStatement.RunStatement<SxmData, TResult>(sqlStatementName, this, databaseName);
        }

        public virtual async Task<List<Dictionary<string, object?>>> RunSelectStatement(string sqlStatementName)

        {
            return await SxmStatement.RunStatement<SxmData>(sqlStatementName, this, databaseName);
        }
        public virtual async Task RunInsertStatement(string sqlStatementName)
        {
            List<Dictionary<string, object?>> result = await SxmStatement.RunStatement<SxmData>(sqlStatementName, this, databaseName);
            if (result.Count > 0)
                loadDbValues(result[0]);
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

        public static async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
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
