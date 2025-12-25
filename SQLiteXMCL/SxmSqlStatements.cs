using SQLiteXM;
using System.Collections;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Provides a central store for SQL statement definitions used by the library.
    /// </summary>
    public class SxmSqlStatements
    {
        /// <summary>
        /// Table create definitions keyed by "database.table".
        /// </summary>
        internal static Dictionary<string, TableDefinition>? tableCreateStatements = new Dictionary<string, TableDefinition>();

        /// <summary>
        /// ALTER statements keyed by "database.table".
        /// </summary>
        internal static Dictionary<string, List<AlterDefinition>>? alterStatements = default(Dictionary<string, List<AlterDefinition>>);

        /// <summary>
        /// Index definitions keyed by "database.table".
        /// </summary>
		internal static Dictionary<string, List<IndexDefinition>>? indexStatements = default(Dictionary<string, List<IndexDefinition>>);

        /// <summary>
        /// Trigger definitions keyed by database name.
        /// </summary>
        internal static Dictionary<string, List<TriggerDefinition>>? triggerStatements = default(Dictionary<string, List<TriggerDefinition>>);

        /// <summary>
        /// Insert statements keyed by statement name.
        /// </summary>
        internal static Dictionary<string, InsertDefinition> insertStatements = new Dictionary<string, InsertDefinition>();

        /// <summary>
        /// Select statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, SelectDefinition> selectStatements = new Dictionary<string, SelectDefinition>();

        /// <summary>
        /// Update statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, UpdateDefinition> updateStatements = new Dictionary<string, UpdateDefinition>();

        /// <summary>
        /// Delete statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, DeleteDefinition> deleteStatements = new Dictionary<string, DeleteDefinition>();

        /// <summary>
        /// Returns the SQL text for a named statement.
        /// </summary>
        /// <param name="sqlStatementName">The name of the SQL statement to retrieve.</param>
        /// <returns>The SQL text for the requested statement.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sqlStatementName"/> is null or when the named statement cannot be found.</exception>
        public static string GetSqlStatement(string sqlStatementName)
        {
            if (sqlStatementName == null)
                throw new ArgumentException("A sql statement name cannot be null.");

            sqlStatementName = sqlStatementName.Trim();
            switch (SxmHelpers.GetDatabaseStatementType(sqlStatementName))
            {
                case SqlStatementType.select:
                    return selectStatements[sqlStatementName].SelectSQL;

                case SqlStatementType.insert:
                    return insertStatements[sqlStatementName].InsertSQL;

                case SqlStatementType.update:
                    return updateStatements[sqlStatementName].UpdateSQL;

                case SqlStatementType.delete:
                    return deleteStatements[sqlStatementName].DeleteSQL;

                default: break;
            }

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName));
        }

        /// <summary>
        /// Adds a named insert definition.
        /// </summary>
        /// <param name="insertName">The name used to reference the insert statement.</param>
        /// <param name="tableName">The target table name for the insert.</param>
        /// <param name="insertSQL">The SQL text of the insert statement.</param>
        internal static void addInsertDefinition(string insertName, string tableName, string insertSQL)
        {
            insertName = insertName.Trim();

            if (!insertStatements.ContainsKey(insertName))
                insertStatements.Add(insertName, new InsertDefinition(tableName.Trim(), insertSQL.Trim()));
        }

        /// <summary>
        /// Adds a named select definition.
        /// </summary>
        /// <param name="selectName">The name used to reference the select statement.</param>
        /// <param name="tableName">The table that the select targets.</param>
        /// <param name="selectSQL">The SQL text of the select statement.</param>
        internal static void addSelectDefinition(string selectName, string tableName, string selectSQL)
        {
            selectName = selectName.Trim();

            if (!selectStatements.ContainsKey(selectName))
                selectStatements.Add(selectName, new SelectDefinition(tableName.Trim(), selectSQL.Trim()));
        }

        /// <summary>
        /// Adds a named update definition.
        /// </summary>
        /// <param name="updateName">The name used to reference the update statement.</param>
        /// <param name="tableName">The table that the update targets.</param>
        /// <param name="updateSQL">The SQL text of the update statement.</param>
		internal static void addUpdateDefinition(string updateName, string tableName, string updateSQL)
        {
            updateName = updateName.Trim();

            if (!updateStatements.ContainsKey(updateName))
                updateStatements.Add(updateName, new UpdateDefinition(tableName.Trim(), updateSQL.Trim()));
        }

        /// <summary>
        /// Adds a named delete definition.
        /// </summary>
        /// <param name="deleteName">The name used to reference the delete statement.</param>
        /// <param name="tableName">The table that the delete targets.</param>
        /// <param name="deleteSQL">The SQL text of the delete statement.</param>
		internal static void addDeleteDefinition(string deleteName, string tableName, string deleteSQL)
        {
            deleteName = deleteName.Trim();

            if (!deleteStatements.ContainsKey(deleteName))
                deleteStatements.Add(deleteName, new DeleteDefinition(tableName.Trim(), deleteSQL.Trim()));
        }

        /// <summary>
        /// Adds an index definition for a database table.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="indexName">The name of the index.</param>
        /// <param name="sqlStatement">The SQL text that creates the index.</param>
		internal static void addIndexDefinition(string dbAndTableName, string indexName, string sqlStatement)
        {
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            indexName = indexName.Trim();

            if (indexStatements == null)
                indexStatements = new Dictionary<string, List<IndexDefinition>>();

            List<IndexDefinition>? indexStatementsList = indexStatements[dbAndTableName] as List<IndexDefinition>;
            if (indexStatementsList == null)
            {
                indexStatementsList = new List<IndexDefinition>();
                indexStatements.Add(dbAndTableName, indexStatementsList);
            }

            indexStatementsList.Add(new IndexDefinition(indexName, sqlStatement));
        }

        /// <summary>
        /// Removes all index definitions and resets the index store to uninitialized.
        /// </summary>
        internal static void removeIndexDefinitions()
        {
            if (indexStatements != default(Dictionary<string, List<IndexDefinition>>))
            {
                indexStatements.Clear();
                indexStatements = default(Dictionary<string, List<IndexDefinition>>);
            }
        }

        /// <summary>
        /// Adds a trigger definition for the specified database.
        /// </summary>
        /// <param name="dbName">The database name the trigger belongs to.</param>
        /// <param name="triggerName">The trigger name.</param>
        /// <param name="sqlStatement">The SQL text of the trigger.</param>
        internal static void addTriggerDefinition(string dbName, string triggerName, string sqlStatement)
        {
            sqlStatement = sqlStatement.Trim();
            triggerName = triggerName.Trim();
            dbName = dbName.Trim();

            if (triggerStatements == null)
                triggerStatements = new Dictionary<string, List<TriggerDefinition>>();

            List<TriggerDefinition>? triggerStatementsList = triggerStatements[dbName] as List<TriggerDefinition>;
            if (triggerStatementsList == null)
            {
                triggerStatementsList = new List<TriggerDefinition>();
                triggerStatements.Add(dbName, triggerStatementsList);
            }

            triggerStatementsList.Add(new TriggerDefinition(triggerName, sqlStatement));
        }

        /// <summary>
        /// Removes all trigger definitions and resets the trigger store to uninitialized.
        /// </summary>
        internal static void removeTriggerDefinitions()
        {
            if (triggerStatements != default(Dictionary<string, List<TriggerDefinition>>))
            {
                triggerStatements.Clear();
                triggerStatements = default(Dictionary<string, List<TriggerDefinition>>);
            }
        }

        /// <summary>
        /// Adds an ALTER definition for a specific database.table entry.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="columnName">The column being altered.</param>
        /// <param name="sqlStatement">The SQL text of the ALTER operation.</param>
        internal static void addAlterDefinition(string dbAndTableName, string columnName, string sqlStatement)
        {
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            columnName = columnName.Trim();

            if (alterStatements == null)
                alterStatements = new Dictionary<string, List<AlterDefinition>>();

            List<AlterDefinition>? alterStatementsList = alterStatements[dbAndTableName] as List<AlterDefinition>;
            if (alterStatementsList == null)
            {
                alterStatementsList = new List<AlterDefinition>();
                alterStatements.Add(dbAndTableName, alterStatementsList);
            }

            alterStatementsList.Add(new AlterDefinition(columnName, sqlStatement));
        }

        /// <summary>
        /// Adds a table definition for the specified database.table using the default cloudPush flag.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="tableSQL">The SQL text that creates the table.</param>
        internal static void addTableDefinition(string dbAndTableName, string tableSQL)
        {
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            addTableDefinition(dbAndTableName, tableSQL, SxmDefines.NO_CLOUD_SYNCH);
        }

        /// <summary>
        /// Adds a table definition for the specified database.table.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="tableSQL">The SQL text that creates the table.</param>
        /// <param name="cloudPush">Cloud push flag from <see cref="T:SQLiteXM.SxmDefines"/>.</param>
        internal static void addTableDefinition(string dbAndTableName, string tableSQL, int cloudPush)
        {
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            if (tableCreateStatements == null)
                tableCreateStatements = new Dictionary<string, TableDefinition>();

            tableCreateStatements.Add(dbAndTableName, new TableDefinition(tableSQL, cloudPush));
        }

        /// <summary>
        /// Removes all table definitions and resets the table store to uninitialized.
        /// </summary>
        internal static void removeTableDefinitions()
        {
            if (tableCreateStatements != default(Dictionary<string, TableDefinition>))
            {
                tableCreateStatements.Clear();
                tableCreateStatements = default(Dictionary<string, TableDefinition>);
            }
        }

        /// <summary>
        /// Clears in-memory stores for alters, tables, indexes, and triggers.
        /// </summary>
        internal static void clearStatementTables()
        {
            if (alterStatements != default(Dictionary<string, List<AlterDefinition>>))
            {
                alterStatements.Clear();
                alterStatements = default(Dictionary<string, List<AlterDefinition>>);
            }

            if (tableCreateStatements != default(Dictionary<string, TableDefinition>))
            {
                tableCreateStatements?.Clear();
                tableCreateStatements = default(Dictionary<string, TableDefinition>)!;
            }

            if (indexStatements != default(Dictionary<string, List<IndexDefinition>>))
            {
                indexStatements.Clear();
                indexStatements = default(Dictionary<string, List<IndexDefinition>>);
            }

            if (triggerStatements != default(Dictionary<string, List<TriggerDefinition>>))
            {
                triggerStatements.Clear();
                triggerStatements = default(Dictionary<string, List<TriggerDefinition>>);
            }
        }

        /// <summary>
        /// Prevents external instantiation. Instances are not required because the class is used statically.
        /// </summary>
        internal SxmSqlStatements() { }
    }
}