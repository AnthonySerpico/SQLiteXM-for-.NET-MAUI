using SQLiteXM.Internal;
using System.Data;
using System.Reflection;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Central entry point for executing SQL statements defined for the library.
    /// Provides typed and untyped helper methods for performing INSERT, SELECT, UPDATE and DELETE
    /// statements as well as low-level internal routing to the appropriate helper classes.
    /// </summary>
    public class SxmStatement
    {
        // Private constructor prevents instantiation - all members are static.
        private SxmStatement() { }


        /************************************************************************* INSERT ********************************************************************/

        /// <summary>
        /// Executes an INSERT statement and maps the returned record to <typeparamref name="TResult"/>.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <typeparam name="TResult">Type to map the insert result record to.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>The first result record mapped to <typeparamref name="TResult"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the referenced statement is not an insert.</exception>
        public static async Task<TResult> Insert<T, TResult>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                               where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatement<T, TResult>(sqlStatementName, userObjectParameters, dbName).CAF();
            return select[0];
        }

        /// <summary>
        /// Executes an INSERT statement and returns the inserted record as a dictionary.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>A dictionary representing the inserted record (column name -> value).</returns>
        public static async Task<Dictionary<string, object?>> Insert<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement<T>(sqlStatementName, userObjectParameters, dbName).CAF();
            return select[0];
        }

        /// <summary>
        /// Executes an INSERT statement using a dictionary of parameter values and maps the returned record to <typeparamref name="T"/>.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="T">Type to map the insert result record to.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>The first result record mapped to <typeparamref name="T"/>.</returns>
        public static async Task<T> Insert<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName).CAF();
            return select[0];
        }

        /// <summary>
        /// Executes an INSERT statement using a dictionary of parameter values and returns the inserted record as a dictionary.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>A dictionary representing the inserted record (column name -> value).</returns>
        public static async Task<Dictionary<string, object?>> Insert(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName).CAF();
            return select[0];
        }

        /// <summary>
        /// Executes an INSERT statement using a list of parameter values and maps the returned record to <typeparamref name="T"/>.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="T">Type to map the insert result record to.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>The first result record mapped to <typeparamref name="T"/>.</returns>
        public static async Task<T> Insert<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<T> select = await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName).CAF();
            return select[0];
        }

        /// <summary>
        /// Executes an INSERT statement using a list of parameter values and returns the inserted record as a dictionary.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>A dictionary representing the inserted record (column name -> value).</returns>
        public static async Task<Dictionary<string, object?>> Insert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
            return select[0];
        }



        /************************************************************************* UPDATE ********************************************************************/

        /// <summary>
        /// Executes an UPDATE statement using a user-typed parameter object.
        /// The referenced statement must be of type 'update' or 'updateDirect'.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Update<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes an UPDATE statement using a dictionary of parameter values.
        /// The referenced statement must be of type 'update' or 'updateDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Update(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes an UPDATE statement using a list of parameter values.
        /// The referenced statement must be of type 'update' or 'updateDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Update(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.update && statementType != SqlStatementType.updateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }


        /************************************************************************* SELECT ********************************************************************/

        /// <summary>
        /// Executes a SELECT statement and maps the returned records to a list of <typeparamref name="TResult"/>.
        /// Accepts a user-typed parameter object.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the referenced statement is not a select.</exception>
        public static async Task<List<TResult>> Select<T, TResult>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                                     where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T, TResult>(sqlStatementName, userObjectParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a SELECT statement and returns the results as a list of dictionaries.
        /// Accepts a user-typed parameter object.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of dictionaries where each dictionary represents a result row (column name -> value).</returns>
        public static async Task<List<Dictionary<string, object?>>> Select<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, userObjectParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a SELECT statement using a parameter dictionary and returns results as dictionaries.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of dictionaries where each dictionary represents a result row (column name -> value).</returns>
        public static async Task<List<Dictionary<string, object?>>> Select(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a SELECT statement using a parameter dictionary and maps results to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="T"/>.</returns>
        public static async Task<List<T>> Select<T>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a SELECT statement using an ordered parameter list and maps results to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="T"/>.</returns>
        public static async Task<List<T>> Select<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement<T>(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a SELECT statement using an ordered parameter list and returns results as dictionaries.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of dictionaries where each dictionary represents a result row (column name -> value).</returns>
        public static async Task<List<Dictionary<string, object?>>> Select(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.select && statementType != SqlStatementType.selectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }


        /************************************************************************* DELETE ********************************************************************/

        /// <summary>
        /// Executes a DELETE statement using a user-typed parameter object.
        /// The referenced statement must be of type 'delete' or 'deleteDirect'.
        /// </summary>
        /// <typeparam name="T">Type of the user-provided parameter object used to populate SQL parameters.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will be used as statement parameters.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Delete<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement<T>(sqlStatementName, userObjectParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a DELETE statement using a dictionary of parameter values.
        /// The referenced statement must be of type 'delete' or 'deleteDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Delete(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }

        /// <summary>
        /// Executes a DELETE statement using an ordered list of parameter values.
        /// The referenced statement must be of type 'delete' or 'deleteDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task Delete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.delete && statementType != SqlStatementType.deleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatement(sqlStatementName, sqlStatementParameters, dbName).CAF();
        }



        /************************************************************************* GENERIC ********************************************************************/

        /// <summary>
        /// Generic internal runner that accepts a user-typed parameter object and returns mapped result records.
        /// This method validates that 'direct' statement variants are not used with user objects.
        /// </summary>
        /// <typeparam name="T">Type of the user-parameter object.</typeparam>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will provide parameter values.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped result records.</returns>
        /// <exception cref="ArgumentException">If the statement is a direct SQL variant that requires a dictionary or list of parameters.</exception>
        private static async Task<List<TResult>> RunStatement<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
                                                                                                                                                           where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await RunStatement(sqlStatementName, selectParameterValues, databaseName).CAF();
            List<TResult> userRecordList = SxmHelpers.populateUserRecord<TResult>(select);

            return userRecordList;
        }

        /// <summary>
        /// Internal runner that accepts a parameter dictionary and returns mapped results.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        private async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName).CAF();

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Internal runner that accepts a user-typed parameter object and returns raw result dictionaries.
        /// This method validates that 'direct' statement variants are not used with user objects.
        /// </summary>
        /// <typeparam name="T">Type of the user-parameter object.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will provide parameter values.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows.</returns>
        /// <exception cref="ArgumentException">If the statement is a direct SQL variant that requires a dictionary or list of parameters.</exception>
        private static async Task<List<Dictionary<string, object?>>> RunStatement<T>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where T : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmInit.getTableColumnNames(databaseName, sqlStatementName, statementType);
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues(columnNames, userObjectParameters);

            return await RunStatement(sqlStatementName, selectParameterValues, databaseName).CAF();
        }

        /// <summary>
        /// Internal runner that accepts an ordered parameter list and maps results to <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        private async static Task<List<TResult>> RunStatement<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatement(sqlStatementName, sqlStatementParameters, databaseName).CAF();

            return SxmHelpers.populateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Internal runner that accepts a single dictionary of parameters and delegates to the list-based runner.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows.</returns>
        private static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string))
        {
            return await RunStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, databaseName).CAF();
        }

        /// <summary>
        /// Core routing method that dispatches the provided statement to the appropriate helper
        /// (select/update/delete/insert and their direct variants). Handles wrapper transaction-scope
        /// in the future (currently commented).
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values or dictionaries used by the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows. Inserts return a single-record list containing the inserted row.</returns>
        private static async Task<List<Dictionary<string, object?>>> RunStatement(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string))
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            try
            {
                //await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(databaseName))
                {
                    switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
                    {
                        case SqlStatementType.select:
                            recordData = await SxmSelectHelpers.performSelect(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.update:
                            await SxmUpdateHelpers.performUpdate(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.delete:
                            await SxmDeleteHelpers.performDelete(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.performInsert(sqlStatementName, sqlStatementParameters, databaseName).CAF());
                            break;

                        // Direct SQL statement queries are processed here.
                        case SqlStatementType.selectDirect:
                            recordData = await SxmSelectHelpers.performSelectDirect(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.updateDirect:
                            await SxmUpdateHelpers.performUpdateDirect(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.deleteDirect:
                            await SxmDeleteHelpers.performDeleteDirect(sqlStatementName, sqlStatementParameters, databaseName).CAF();
                            break;

                        case SqlStatementType.insertDirect:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.performInsertDirect(sqlStatementName, sqlStatementParameters, databaseName).CAF());
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

            return await Task.FromResult(recordData).CAF();
        }
    }
}