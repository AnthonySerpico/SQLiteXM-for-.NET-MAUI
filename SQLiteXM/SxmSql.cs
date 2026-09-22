using LinqToDB.SqlQuery;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static SQLiteXM.SxmDefines;
using static SxmQueryProcessor;

namespace SQLiteXM
{
    /// <summary>
    /// Central entry point for executing SQL statements defined for the library.
    /// Provides typed and untyped helper methods for performing INSERT, SELECT, UPDATE and DELETE
    /// statements as well as low-level internal routing to the appropriate helper classes.
    /// </summary>
    public class SxmSql
    {
        // Private constructor prevents instantiation - all members are static.
        private SxmSql() { }


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


        /************************************************************************* BULK INSERT ********************************************************************/

        /// <summary>
        /// Inserts many new entities of one type using multi-row <c>INSERT ... VALUES (...), (...) RETURNING id</c>
        /// statements inside a single, self-contained transaction that is committed on success and rolled back on failure.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the bulk counterpart of <see cref="SxmEntity.SaveAsync()"/> for inserts. After the call every entity
        /// has its <c>id</c> populated from the database and its <c>synchId</c> assigned, the same post-insert state
        /// <see cref="SxmEntity.SaveAsync()"/> leaves behind. Values changed by AFTER INSERT triggers are not read back.
        /// BulkInsertAsync is not supported on tables with triggers that insert additional rows back into the same table;
        /// such inserts may fail with <see cref="InvalidOperationException"/>. Use <see cref="SxmEntity.SaveAsync()"/> for those entities instead.
        /// </para>
        /// <para>
        /// The method always opens its own transaction;
        /// To bulk insert as part of a larger unit of work use <see cref="SxmLinqExtensions.BulkInsertAsync{T}"/> on
        /// <c>ctx.GetTable&lt;T&gt;()</c> instead.
        /// </para>
        /// <para>
        /// All entities must be new (<c>id == 0</c>) and of the same runtime type <typeparamref name="T"/>; the list
        /// is validated before any row is written. Rows are grouped <paramref name="statementCount"/> per INSERT
        /// statement, capped so no statement binds more than 1000 parameters (larger statements bind measurably
        /// slower in Microsoft.Data.Sqlite).
        /// </para>
        /// </remarks>
        /// <typeparam name="T">Entity type; must derive from <see cref="SxmEntity"/>.</typeparam>
        /// <param name="entities">New entities to insert. All must be exactly of type <typeparamref name="T"/>.</param>
        /// <param name="statementCount">Rows per INSERT statement. Defaults to 20.</param>
        /// <param name="databaseName">Optional database name override; when null the entity type's database is used.</param>
        /// <param name="cancellationToken">Cancellation token checked between statements.</param>
        /// <returns>The number of rows inserted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the list contains a null element or an entity of a different runtime type.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="statementCount"/> is less than 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an entity already has an id or the entity type's schema is not registered.</exception>
        public static async Task<int> BulkInsertAsync<T>(List<T> entities, int statementCount = SxmBulkInsertHelpers.DefaultBatchRows, string? databaseName = default, CancellationToken cancellationToken = default)
            where T : SxmEntity
        {
            if (statementCount < 1) throw new ArgumentOutOfRangeException(nameof(statementCount), "statementCount must be at least 1.");
            SxmBulkInsertHelpers.ValidateNewEntities(entities, nameof(entities));

            if (entities.Count == 0)
                return 0;

            Type entityType = typeof(T);
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].GetType() != entityType)
                    throw new ArgumentException(
                        $"BulkInsertAsync requires all entities to be of type '{entityType.Name}'. Entity at index {i} is '{entities[i].GetType().Name}'.",
                        nameof(entities));
            }

            databaseName ??= entities[0].DatabaseName;

            await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(databaseName))
            {
                int inserted;
                try
                {
                    inserted = await SxmBulkInsertHelpers.InsertAsync(
                        (sql, parameters) => sxmTransaction.ExecuteWriteReturningAsync(sql, new List<object>(parameters!), cancellationToken),
                        entities, statementCount, cancellationToken).ConfigureFalse();
                }
                catch
                {
                    await sxmTransaction.RollbackTransactionAsync(cancellationToken).ConfigureFalse();
                    throw;
                }

                await sxmTransaction.CommitTransactionAsync(cancellationToken).ConfigureFalse();
                return inserted;
            }
        }


        /************************************************************************* RETURN TResult ********************************************************************/

        /// <summary>
        /// Executes a SQL statement or named statement with no parameters and returns strongly-typed result records.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record. Must have a parameterless constructor.</typeparam>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement or direct SQL to execute.</param>
        /// <param name="databaseName">Optional database name override; uses the default database if null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of mapped result records of type <typeparamref name="TResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This is a convenience overload that invokes <see cref="RunStatementAsync(string, Dictionary{string, object?}, string?)"/>
        /// with an empty parameter dictionary, then maps the raw dictionaries to <typeparamref name="TResult"/> using
        /// <see cref="SxmHelpers.PopulateUserRecord{TResult}(List{Dictionary{string, object?}})"/>.
        /// </para>
        /// <para>
        /// Use this method when executing parameterless queries or when the statement definition does not
        /// require any input parameters.
        /// </para>
        /// </remarks>
        public static async Task<List<TResult>> RunStatementAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(string sqlOrStatementName, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlOrStatementName, new Dictionary<string, object?>(), databaseName).ConfigureFalse();
            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Generic internal runner that accepts a user-typed parameter object and returns mapped result records.
        /// This method validates that 'direct' statement variants are not used with user objects.
        /// </summary>
        /// <typeparam name="T">Type of the user-parameter object.</typeparam>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will provide parameter values.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped result records.</returns>
        /// <exception cref="ArgumentException">If the statement is a direct SQL variant that requires a dictionary or list of parameters.</exception>
        public static async Task<List<TResult>> RunStatementAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(string sqlOrStatementName, T userObjectParameters, string? databaseName = default) where TResult : class, new()
        {
            SqlStatementDetails statementDetails = new();

                statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlOrStatementName);
                if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
                {
                    statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlOrStatementName, databaseName);
                }

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(databaseName, sqlOrStatementName, statementDetails.SqlStatementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);
            List<Dictionary<string, object?>> select = await RunStatementAsync(sqlOrStatementName, selectParameterValues, databaseName).ConfigureFalse();

            List<TResult> userRecordList = SxmHelpers.PopulateUserRecord<TResult>(select);

            return userRecordList;
        }

        /// <summary>
        /// Internal runner that accepts a parameter dictionary and returns mapped results.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        public static async Task<List<TResult>> RunStatementAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlOrStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }

        /// <summary>
        /// Internal runner that accepts an ordered parameter list and maps results to <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type used to map each result record.</typeparam>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values (ordered) to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of mapped records of type <typeparamref name="TResult"/>.</returns>
        public static async Task<List<TResult>> RunStatementAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = default(string)) where TResult : class, new()
        {
            List<Dictionary<string, object?>> runSqlStatementResponse = await RunStatementAsync(sqlOrStatementName, sqlStatementParameters, databaseName).ConfigureFalse();
            return SxmHelpers.PopulateUserRecord<TResult>(runSqlStatementResponse);
        }


        /************************************************************************* RETURN Dictionary ********************************************************************/

        /// <summary>
        /// Executes a SQL statement or named statement with no parameters and returns raw result dictionaries.
        /// </summary>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement or direct SQL to execute.</param>
        /// <param name="databaseName">Optional database name override; uses the default database if null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of dictionaries, where each dictionary represents a result row with column names as keys and cell values as values.</returns>
        /// <remarks>
        /// <para>
        /// This is a convenience overload that invokes <see cref="RunStatementAsync(string, Dictionary{string, object?}, string?)"/>
        /// with an empty parameter dictionary. The result rows are returned as untyped dictionaries rather than
        /// being mapped to a specific type.
        /// </para>
        /// <para>
        /// Use this method when you need the raw query results without type mapping, or when the result
        /// schema is dynamic and does not correspond to a predefined class.
        /// </para>
        /// </remarks>
        public static async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, string? databaseName = default(string))
        {
            return await RunStatementAsync(sqlOrStatementName, new Dictionary<string, object?>(), databaseName).ConfigureFalse();
        }

        /// <summary>
        /// Internal runner that accepts a user-typed parameter object and returns raw result dictionaries.
        /// This method validates that 'direct' statement variants are not used with user objects.
        /// </summary>
        /// <typeparam name="T">Type of the user-parameter object.</typeparam>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="userObjectParameters">User object whose properties will provide parameter values.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows.</returns>
        /// <exception cref="ArgumentException">If the statement is a direct SQL variant that requires a dictionary or list of parameters.</exception>
        public static async Task<List<Dictionary<string, object?>>> RunStatementAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(string sqlOrStatementName, T userObjectParameters, string? databaseName = default)
        {
            SqlStatementDetails statementDetails = new();

            statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlOrStatementName);
            if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
            {
                statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlOrStatementName, databaseName);
            }

            Dictionary<string, string> columnNames = await SxmDatabase.GetTableColumnNamesAsync(databaseName, sqlOrStatementName, statementDetails.SqlStatementType).ConfigureFalse();
            Dictionary<string, object?> selectParameterValues = SxmHelpers.LoadParameterValues(columnNames, userObjectParameters!);

            return await RunStatementAsync(sqlOrStatementName, selectParameterValues, databaseName).ConfigureFalse();
        }

        /// <summary>
        /// Internal runner that accepts a single dictionary of parameters and delegates to the list-based runner.
        /// </summary>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Dictionary of parameter name -> value to use for the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows.</returns>
        public static async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, Dictionary<string, object?> sqlStatementParameters, string? databaseName = default(string))
        {
            return await RunStatementAsync(sqlOrStatementName, new List<object>(1) { sqlStatementParameters }, databaseName).ConfigureFalse();
        }

        /// <summary>
        /// Core routing method that dispatches the provided statement to the appropriate helper
        /// (select/update/delete/insert and their direct variants). Handles wrapper transaction-scope
        /// in the future (currently commented).
        /// </summary>
        /// <param name="sqlOrStatementName">Logical name of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">List of parameter values or dictionaries used by the statement.</param>
        /// <param name="databaseName">Optional database name override.</param>
        /// <returns>List of dictionaries representing result rows. Inserts return a single-record list containing the inserted row.</returns>
        public static async Task<List<Dictionary<string, object?>>> RunStatementAsync(string sqlOrStatementName, List<object> sqlStatementParameters, string? databaseName = default(string))
        {
            List<Dictionary<string, object?>> recordData = default(List<Dictionary<string, object?>>)!;

            SqlStatementDetails statementDetails = new();

            statementDetails.SqlStatementType = SxmHelpers.GetDatabaseStatementTypeFromName(sqlOrStatementName);
            if (statementDetails.SqlStatementType == SqlStatementType.Unknown)
            {
                statementDetails = SxmHelpers.GetDatabaseStatementTypeFromSql(sqlOrStatementName, databaseName);
            }

            try
            {
                switch (statementDetails.SqlStatementType)
                {
                    case SqlStatementType.Select:
                    case SqlStatementType.Update:
                    case SqlStatementType.Delete:
                    case SqlStatementType.Insert:
                        recordData = await SxmSelectHelpers.PerformSelectAsync(sqlOrStatementName, sqlStatementParameters, statementDetails,  databaseName).ConfigureFalse();
                        break;


                    // Direct SQL statement queries. These are statements where the SQL is embedded in the code, not inside the SqlStatemenst file.
                    case SqlStatementType.SelectDirect:
                    case SqlStatementType.UpdateDirect:
                    case SqlStatementType.DeleteDirect:
                    case SqlStatementType.InsertDirect:
                        recordData = await SxmSelectHelpers.PerformSelectDirectAsync(sqlOrStatementName, sqlStatementParameters, statementDetails, databaseName).ConfigureFalse();
                        break;

                    default: break;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                string? statement = SxmHelpers.SqlStatementFromStatementName(sqlOrStatementName, statementDetails.SqlStatementType);
                string statementName = string.Empty;
                if (statementDetails.SqlStatementType != SqlStatementType.SelectDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.UpdateDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.DeleteDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.InsertDirect)

                {
                    statementName = $"SQL statement: '{sqlOrStatementName}'.";
                }

                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"RunStatementAsync failure. {statementName} Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {statement}");
                throw;
            }
            catch (System.Exception ex)
            {

                string? statement = SxmHelpers.SqlStatementFromStatementName(sqlOrStatementName, statementDetails.SqlStatementType);
                string statementName = string.Empty;
                if (statementDetails.SqlStatementType != SqlStatementType.SelectDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.UpdateDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.DeleteDirect &&
                    statementDetails.SqlStatementType != SqlStatementType.InsertDirect)

                {
                    statementName = $"SQL statement: '{sqlOrStatementName}'.";
                }

                string errStr = $"RunStatementAsync failure. {statementName} Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {statement}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            recordData ??= new List<Dictionary<string, object?>>();
            return recordData;
        }
    }
}