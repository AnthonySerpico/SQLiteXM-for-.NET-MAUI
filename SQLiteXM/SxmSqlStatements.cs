using SQLiteXM;
using System.Collections;
using System.Collections.Concurrent;
using System.Xml.Linq;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Provides a central store for SQL statement definitions used by the library.
    /// </summary>
    internal class SxmSqlStatements
    {
        internal static ConcurrentDictionary<string, TableDefinition>? TableCreateStatements = new ConcurrentDictionary<string, TableDefinition>();
        /// <summary>
        /// Trigger definitions keyed by database name.
        /// </summary>
        internal static ConcurrentDictionary<string, List<TriggerDefinition>> TriggerStatements = new ConcurrentDictionary<string, List<TriggerDefinition>>(StringComparer.Ordinal);
        /// <summary>
        /// Insert statements keyed by statement name.
        /// </summary>
		internal static ConcurrentDictionary<string, InsertDefinition> InsertStatements = new ConcurrentDictionary<string, InsertDefinition>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Select statements keyed by statement name.
		/// </summary>
		internal static ConcurrentDictionary<string, SelectDefinition> SelectStatements = new ConcurrentDictionary<string, SelectDefinition>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Update statements keyed by statement name.
		/// </summary>
		internal static ConcurrentDictionary<string, UpdateDefinition> UpdateStatements = new ConcurrentDictionary<string, UpdateDefinition>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Delete statements keyed by statement name.
		/// </summary>
		internal static ConcurrentDictionary<string, DeleteDefinition> DeleteStatements = new ConcurrentDictionary<string, DeleteDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the SQL text for a named statement.
        /// </summary>
        /// <param name="sqlOrStatementName">The name of the SQL statement to retrieve.</param>
        /// <returns>The SQL text for the requested statement.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sqlOrStatementName"/> is null or when the named statement cannot be found.</exception>
        public static string GetSqlStatement(string sqlOrStatementName)
        {
            if (sqlOrStatementName == null)
                throw new ArgumentException("A sql statement name cannot be null.");

            sqlOrStatementName = sqlOrStatementName.Trim();
            switch (SxmHelpers.GetDatabaseStatementTypeFromName(sqlOrStatementName))
            {
                case SqlStatementType.Select:
                    return SelectStatements[sqlOrStatementName].SelectSQL;

                case SqlStatementType.Insert:
                    return InsertStatements[sqlOrStatementName].InsertSQL;

                case SqlStatementType.Update:
                    return UpdateStatements[sqlOrStatementName].UpdateSQL;

                case SqlStatementType.Delete:
                    return DeleteStatements[sqlOrStatementName].DeleteSQL;

                default: break;
            }

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlOrStatementName));
        }

        /// <summary>
        /// Adds a named insert definition.
        /// </summary>
        /// <param name="statementName">The name used to reference the insert statement.</param>
        /// <param name="tableName">The target table name for the insert.</param>
        /// <param name="insertSQL">The SQL text of the insert statement.</param>
        internal static void AddInsertDefinition(string statementName, string tableName, string insertSQL)
        {
            statementName = statementName.Trim();
            InsertStatements.TryAdd(statementName, new InsertDefinition(tableName.Trim(), insertSQL.Trim()));
        }

        /// <summary>
        /// Adds a named select definition.
        /// </summary>
        /// <param name="statementName">The name used to reference the select statement.</param>
        /// <param name="tableName">The table that the select targets.</param>
        /// <param name="selectSQL">The SQL text of the select statement.</param>
        internal static void AddSelectDefinition(string statementName, string tableName, string selectSQL)
        {
            statementName = statementName.Trim();
            SelectStatements.TryAdd(statementName, new SelectDefinition(tableName.Trim(), selectSQL.Trim()));
        }

        /// <summary>
        /// Adds a named update definition.
        /// </summary>
        /// <param name="statementName">The name used to reference the update statement.</param>
        /// <param name="tableName">The table that the update targets.</param>
        /// <param name="updateSQL">The SQL text of the update statement.</param>
		internal static void AddUpdateDefinition(string statementName, string tableName, string updateSQL)
		{
			statementName = statementName.Trim();
			UpdateStatements.TryAdd(statementName, new UpdateDefinition(tableName.Trim(), updateSQL.Trim()));
		}

        /// <summary>
        /// Adds a named delete definition.
        /// </summary>
        /// <param name="deleteName">The name used to reference the delete statement.</param>
        /// <param name="tableName">The table that the delete targets.</param>
        /// <param name="deleteSQL">The SQL text of the delete statement.</param>
		internal static void AddDeleteDefinition(string deleteName, string tableName, string deleteSQL)
			{
				deleteName = deleteName.Trim();
				DeleteStatements.TryAdd(deleteName, new DeleteDefinition(tableName.Trim(), deleteSQL.Trim()));
			}

        /// <summary>
        /// Adds a trigger definition for the specified database.
        /// </summary>
        /// <param name="dbName">The database name the trigger belongs to.</param>
        /// <param name="tableName">The name of the source table for the trigger.</param>
        /// <param name="sqlStatement">The SQL text of the trigger.</param>
        internal static void AddTriggerDefinition(string dbName, string tableName, string sqlStatement)
        {
            sqlStatement = sqlStatement.Trim();
            dbName = dbName.Trim();

            // TryGetValue sets triggerStatementsList to the existing list or null if the key doesn't exist.
            TriggerStatements.TryGetValue(dbName, out List<TriggerDefinition>? triggerStatementsList);
            if (triggerStatementsList == null)
            {
                triggerStatementsList = new List<TriggerDefinition>();
                TriggerStatements[dbName] = triggerStatementsList;
            }

            triggerStatementsList.Add(new TriggerDefinition(tableName, sqlStatement));
        }

        internal static void CreateTriggerStatementsList(string dbName)
        {
            // TryGetValue sets triggerStatementsList to the existing list or null if the key doesn't exist.
            TriggerStatements.TryGetValue(dbName, out List<TriggerDefinition>? triggerStatementsList);
            if (triggerStatementsList == null)
            {
                triggerStatementsList = new List<TriggerDefinition>();
                TriggerStatements[dbName] = triggerStatementsList;
            }
        }

        /// <summary>
        /// Adds a table definition for the specified database.table using the default cloudPush flag.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="tableSQL">The SQL text that creates the table.</param>
        internal static void AddTableDefinition(string dbAndTableName, string tableSQL)
        {
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            AddTableDefinition(dbAndTableName, tableSQL, SxmDefines.NoCloudSync);
        }

        /// <summary>
        /// Adds a table definition for the specified database.table.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="tableSQL">The SQL text that creates the table.</param>
        /// <param name="cloudPush">Cloud push flag from <see cref="T:SQLiteXM.SxmDefines"/>.</param>
        internal static void AddTableDefinition(string dbAndTableName, string tableSQL, int cloudPush)
        {
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            if (TableCreateStatements == null)
                TableCreateStatements = new ConcurrentDictionary<string, TableDefinition>();

            TableCreateStatements.TryAdd(dbAndTableName, new TableDefinition(tableSQL, cloudPush));
        }

        /// <summary>
        /// Removes all table definitions and resets the table store to uninitialized.
        /// </summary>
        internal static void RemoveTableDefinitions()
        {
            if (TableCreateStatements != default(ConcurrentDictionary<string, TableDefinition>))
            {
                TableCreateStatements.Clear();
                TableCreateStatements = default(ConcurrentDictionary<string, TableDefinition>);
            }
        }

        /// <summary>
        /// Clears in-memory stores for alter, table create, and index statements. The TriggerStatements
        /// ConcurrentDictionary is not cleared because it is not know when the creation of a trigger 
        /// will succeed. If the source table of the trigger is created by an entity, and not by a create
        /// table statement in the SqlStatements file, then the trigger creation will fail during initialization.
        /// In this case, the trigger statement must remain in memory for the next attempt, which will be when
        /// each entity is created for the database associated with the trigger.
        /// </summary>
        internal static void ClearStatementTables()
        {
            if (TableCreateStatements != default(ConcurrentDictionary<string, TableDefinition>))
            {
                TableCreateStatements?.Clear();
                TableCreateStatements = default(ConcurrentDictionary<string, TableDefinition>)!;
            }
        }

#if DEBUG
        /// <summary>
        /// Resets all SQL statement caches for testing purposes.
        /// **WARNING:** Only call this in test scenarios.
        /// </summary>
        internal static void ResetForTesting()
        {
            TableCreateStatements?.Clear();
            TriggerStatements?.Clear();
            InsertStatements?.Clear();
            SelectStatements?.Clear();
            UpdateStatements?.Clear();
            DeleteStatements?.Clear();

            TableCreateStatements = new ConcurrentDictionary<string, TableDefinition>();
            TriggerStatements = new ConcurrentDictionary<string, List<TriggerDefinition>>(StringComparer.Ordinal);
            InsertStatements = new ConcurrentDictionary<string, InsertDefinition>(StringComparer.OrdinalIgnoreCase);
            SelectStatements = new ConcurrentDictionary<string, SelectDefinition>(StringComparer.OrdinalIgnoreCase);
            UpdateStatements = new ConcurrentDictionary<string, UpdateDefinition>(StringComparer.OrdinalIgnoreCase);
            DeleteStatements = new ConcurrentDictionary<string, DeleteDefinition>(StringComparer.OrdinalIgnoreCase);
        }
#endif

        /// <summary>
        /// Prevents external instantiation. Instances are not required because the class is used statically.
        /// </summary>
        internal SxmSqlStatements() { }
    }
}