using LinqToDB.SqlQuery;
using SQLiteXM.Internal.Threading;
using System.Data;
using System.Reflection;
using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;
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
        public static async Task<TResult> InsertAsync<T, TResult>(string sqlStatementName, T userObjectParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<T, TResult>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
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
        public static async Task<Dictionary<string, object?>> InsertAsync<T>(string sqlStatementName, T userObjectParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync<T>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
        }

        /// <summary>
        /// Executes an INSERT statement using a dictionary of parameter values and maps the returned record to <typeparamref name="TResult"/>.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="TResult">Type to map the insert result record to.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>The first result record mapped to <typeparamref name="TResult"/>.</returns>
        public static async Task<TResult> InsertAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
        }

        /// <summary>
        /// Executes an INSERT statement using a dictionary of parameter values and returns the inserted record as a dictionary.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>A dictionary representing the inserted record (column name -> value).</returns>
        public static async Task<Dictionary<string, object?>> InsertAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
        }

        /// <summary>
        /// Executes an INSERT statement using a list of parameter values and maps the returned record to <typeparamref name="TResult"/>.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <typeparam name="TResult">Type to map the insert result record to.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>The first result record mapped to <typeparamref name="TResult"/>.</returns>
        public static async Task<TResult> InsertAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<TResult> select = await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
        }

        /// <summary>
        /// Executes an INSERT statement using a list of parameter values and returns the inserted record as a dictionary.
        /// The SQL statement referenced by <paramref name="sqlStatementName"/> must be of type 'insert'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>A dictionary representing the inserted record (column name -> value).</returns>
        public static async Task<Dictionary<string, object?>> InsertAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Insert)
                throw new ArgumentException(string.Format("You cannot perform an insert using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
            return SxmHelpers.GetFirstOrThrow(select, sqlStatementName);
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
        public static async Task UpdateAsync<T>(string sqlStatementName, T userObjectParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Update && statementType != SqlStatementType.UpdateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync<T>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes an UPDATE statement using a dictionary of parameter values.
        /// The referenced statement must be of type 'update' or 'updateDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task UpdateAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Update && statementType != SqlStatementType.UpdateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes an UPDATE statement using a list of parameter values.
        /// The referenced statement must be of type 'update' or 'updateDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task UpdateAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Update && statementType != SqlStatementType.UpdateDirect)
                throw new ArgumentException(string.Format("You cannot perform an update using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
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
        public static async Task<List<TResult>> SelectAsync<T, TResult>(string sqlStatementName, T userObjectParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<T, TResult>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
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
        public static async Task<List<Dictionary<string, object?>>> SelectAsync<T>(string sqlStatementName, T userObjectParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<T>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a SELECT statement using a parameter dictionary and returns results as dictionaries.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of dictionaries where each dictionary represents a result row (column name -> value).</returns>
        public static async Task<List<Dictionary<string, object?>>> SelectAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a SELECT statement using a parameter dictionary and maps results to <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        public static async Task<List<TResult>> SelectAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a SELECT statement using an ordered parameter list and maps results to <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        public static async Task<List<TResult>> SelectAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync<TResult>(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a SELECT statement using an ordered parameter list and returns results as dictionaries.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        /// <returns>List of dictionaries where each dictionary represents a result row (column name -> value).</returns>
        public static async Task<List<Dictionary<string, object?>>> SelectAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Select && statementType != SqlStatementType.SelectDirect)
                throw new ArgumentException(string.Format("You cannot perform a select using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            return await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
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
        public static async Task DeleteAsync<T>(string sqlStatementName, T userObjectParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Delete && statementType != SqlStatementType.DeleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync<T>(sqlStatementName, userObjectParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a DELETE statement using a dictionary of parameter values.
        /// The referenced statement must be of type 'delete' or 'deleteDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task DeleteAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Delete && statementType != SqlStatementType.DeleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }

        /// <summary>
        /// Executes a DELETE statement using an ordered list of parameter values.
        /// The referenced statement must be of type 'delete' or 'deleteDirect'.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="dbName">Optional database name override.</param>
        public static async Task DeleteAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            if (statementType != SqlStatementType.Delete && statementType != SqlStatementType.DeleteDirect)
                throw new ArgumentException(string.Format("You cannot perform a delete using a {0} statement.", SxmHelpers.GetDatabaseStatementTypeName(statementType)));
            await RunStatementAsync(sqlStatementName, sqlStatementParameters, dbName).ConfigureFalse();
        }


        /************************************************************************* DDL ********************************************************************/
        /// <summary>
        /// Asynchronously drops the specified table if it exists within a transaction.
        /// If <paramref name="force"/> is true, foreign key enforcement is deferred to prevent 
        /// the drop from being blocked by active constraints.
        /// </summary>
        /// <param name="tableName">Name of the table to drop.</param>
        /// <param name="dbName">Optional database name override; uses the default database if null.</param>
        /// <param name="force">If true, executes 'PRAGMA defer_foreign_keys = ON' to allow dropping constrained tables within the transaction.</param>
        public static async Task DropTableAsync(string tableName, string? dbName = default, bool force = false)
        {
            // QuoteIdentifier performs validation and correct quoting per project guidelines.
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
            {
                dbName = sxmTransaction.Connection?.DatabaseName;

                if (force)
                {
                    // Within a transaction, defer_foreign_keys allows for schema changes that would 
                    // otherwise be blocked by foreign key constraints.
                    string fkDdl = $"PRAGMA defer_foreign_keys = ON";
                    await SxmDdlHelpers.PerformTableStatementAsync(fkDdl, dbName, sxmTransaction).ConfigureFalse();
                }
                
                string dtDdl = $"DROP TABLE IF EXISTS {quotedTable}";
                await SxmDdlHelpers.PerformTableStatementAsync(dtDdl, dbName, sxmTransaction).ConfigureFalse();

                // If force was used, SQLite validates all deferred foreign key constraints at this point.
                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
            }
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
        private static async Task<List<TResult>> RunStatementAsync<T, TResult>(string sqlStatementName, T userObjectParameters, string? databaseName = default) where TResult : class, new()
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(databaseName, sqlStatementName, statementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlStatementName, selectParameterValues, databaseName).ConfigureFalse();
            List<TResult> userRecordList = SxmHelpers.PopulateUserRecord<TResult>(select);

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
        private async static Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();

            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
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
        private static async Task<List<Dictionary<string, object?>>> RunStatementAsync<T>(string sqlStatementName, T userObjectParameters, string? databaseName = default)
        {
            SqlStatementType statementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            //if (statementType == SqlStatementType.insertDirect || statementType == SqlStatementType.selectDirect || statementType == SqlStatementType.updateDirect || statementType == SqlStatementType.deleteDirect)
                //throw new ArgumentException("Parameter values for a direct sql statement must be provided using a dictionary or a list. A user object is not supported.");

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(databaseName, sqlStatementName, statementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);

            return await RunStatementAsync(sqlStatementName, selectParameterValues, databaseName).ConfigureFalse();
        }

        /// <summary>
        /// Internal runner that accepts an ordered parameter list and maps results to <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        private async static Task<List<TResult>> RunStatementAsync<TResult>(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();

            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Internal runner that accepts a single dictionary of parameters and delegates to the list-based runner.
        /// </summary>
        /// <param name="sqlStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows.</returns>
        private static async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string))
        {
            return await RunStatementAsync(sqlStatementName, new List<object>(1) { sqlStatementParameters }, databaseName).ConfigureFalse();
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
        private static async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlStatementName, List<object> sqlStatementParameters, string? databaseName = default(string))
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            SqlStatementType sqlStatementType = SxmHelpers.GetDatabaseStatementType(sqlStatementName);
            try
            {
                //await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(databaseName).ConfigureFalse())
                {
                    switch (sqlStatementType)
                    {
                        case SqlStatementType.Select:
                            recordData = await SxmSelectHelpers.PerformSelectAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.Update:
                            await SxmUpdateHelpers.PerformUpdateAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.Delete:
                            await SxmDeleteHelpers.PerformDeleteAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.Insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.PerformInsertAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse());
                            break;

                        // Direct SQL statement queries are processed here. These are statements where the SQL is embedded in the code, not inside the SqlStatemenst file.
                        case SqlStatementType.SelectDirect:
                            recordData = await SxmSelectHelpers.PerformSelectDirectAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.UpdateDirect:
                            await SxmUpdateHelpers.PerformUpdateDirectAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.DeleteDirect:
                            await SxmDeleteHelpers.PerformDeleteDirectAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
                            break;

                        case SqlStatementType.InsertDirect:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await SxmInsertHelpers.PerformInsertDirectAsync(sqlStatementName, sqlStatementParameters, databaseName).ConfigureFalse());
                            break;
                        default: break;
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"RunStatementAsync failure for statement: '{sqlStatementName}{Environment.NewLine}Statement type: '{sqlStatementType.ToString()}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"RunStatementAsync failure for statement: '{sqlStatementName}'{Environment.NewLine}Statement type: '{sqlStatementType.ToString()}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            recordData ??= new List<Dictionary<string, object?>>();
            return recordData;
        }
    }
}