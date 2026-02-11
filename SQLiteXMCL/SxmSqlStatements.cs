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
        internal static Dictionary<string, TableDefinition> TableCreateStatements = new Dictionary<string, TableDefinition>();

        /// <summary>
        /// ALTER statements keyed by "database.table".
        /// </summary>
        internal static Dictionary<string, List<AlterDefinition>>? AlterStatements = default(Dictionary<string, List<AlterDefinition>>);

        /// <summary>
        /// Index definitions keyed by "database.table".
        /// </summary>
		internal static Dictionary<string, List<IndexDefinition>>? IndexStatements = default(Dictionary<string, List<IndexDefinition>>);

        /// <summary>
        /// Trigger definitions keyed by database name.
        /// </summary>
        internal static Dictionary<string, List<TriggerDefinition>>? TriggerStatements = default(Dictionary<string, List<TriggerDefinition>>);

        /// <summary>
        /// Insert statements keyed by statement name.
        /// </summary>
        internal static Dictionary<string, InsertDefinition> InsertStatements = new Dictionary<string, InsertDefinition>();

        /// <summary>
        /// Select statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, SelectDefinition> SelectStatements = new Dictionary<string, SelectDefinition>();

        /// <summary>
        /// Update statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, UpdateDefinition> UpdateStatements = new Dictionary<string, UpdateDefinition>();

        /// <summary>
        /// Delete statements keyed by statement name.
        /// </summary>
		internal static Dictionary<string, DeleteDefinition> DeleteStatements = new Dictionary<string, DeleteDefinition>();

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
                case SqlStatementType.Select:
                    return SelectStatements[sqlStatementName].SelectSQL;

                case SqlStatementType.Insert:
                    return InsertStatements[sqlStatementName].InsertSQL;

                case SqlStatementType.Update:
                    return UpdateStatements[sqlStatementName].UpdateSQL;

                case SqlStatementType.Delete:
                    return DeleteStatements[sqlStatementName].DeleteSQL;

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
        internal static void AddInsertDefinition(string insertName, string tableName, string insertSQL)
        {
            insertName = insertName.Trim();

            if (!InsertStatements.ContainsKey(insertName))
                InsertStatements.Add(insertName, new InsertDefinition(tableName.Trim(), insertSQL.Trim()));
        }

        /// <summary>
        /// Adds a named select definition.
        /// </summary>
        /// <param name="selectName">The name used to reference the select statement.</param>
        /// <param name="tableName">The table that the select targets.</param>
        /// <param name="selectSQL">The SQL text of the select statement.</param>
        internal static void AddSelectDefinition(string selectName, string tableName, string selectSQL)
        {
            selectName = selectName.Trim();

            if (!SelectStatements.ContainsKey(selectName))
                SelectStatements.Add(selectName, new SelectDefinition(tableName.Trim(), selectSQL.Trim()));
        }

        /// <summary>
        /// Adds a named update definition.
        /// </summary>
        /// <param name="updateName">The name used to reference the update statement.</param>
        /// <param name="tableName">The table that the update targets.</param>
        /// <param name="updateSQL">The SQL text of the update statement.</param>
		internal static void AddUpdateDefinition(string updateName, string tableName, string updateSQL)
        {
            updateName = updateName.Trim();

            if (!UpdateStatements.ContainsKey(updateName))
                UpdateStatements.Add(updateName, new UpdateDefinition(tableName.Trim(), updateSQL.Trim()));
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

            if (!DeleteStatements.ContainsKey(deleteName))
                DeleteStatements.Add(deleteName, new DeleteDefinition(tableName.Trim(), deleteSQL.Trim()));
        }

        /// <summary>
        /// Adds an index definition for a database table.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="indexName">The name of the index.</param>
        /// <param name="sqlStatement">The SQL text that creates the index.</param>
		internal static void AddIndexDefinition(string dbAndTableName, string indexName, string sqlStatement)
        {
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            indexName = indexName.Trim();

            if (IndexStatements == null)
                IndexStatements = new Dictionary<string, List<IndexDefinition>>();

            List<IndexDefinition>? indexStatementsList = IndexStatements[dbAndTableName] as List<IndexDefinition>;
            if (indexStatementsList == null)
            {
                indexStatementsList = new List<IndexDefinition>();
                IndexStatements.Add(dbAndTableName, indexStatementsList);
            }

            indexStatementsList.Add(new IndexDefinition(indexName, sqlStatement));
        }

        /// <summary>
        /// Removes all index definitions and resets the index store to uninitialized.
        /// </summary>
        internal static void RemoveIndexDefinitions()
        {
            if (IndexStatements != default(Dictionary<string, List<IndexDefinition>>))
            {
                IndexStatements.Clear();
                IndexStatements = default(Dictionary<string, List<IndexDefinition>>);
            }
        }

        /// <summary>
        /// Adds a trigger definition for the specified database.
        /// </summary>
        /// <param name="dbName">The database name the trigger belongs to.</param>
        /// <param name="triggerName">The trigger name.</param>
        /// <param name="sqlStatement">The SQL text of the trigger.</param>
        internal static void AddTriggerDefinition(string dbName, string triggerName, string sqlStatement)
        {
            sqlStatement = sqlStatement.Trim();
            triggerName = triggerName.Trim();
            dbName = dbName.Trim();

            if (TriggerStatements == null)
                TriggerStatements = new Dictionary<string, List<TriggerDefinition>>();

            List<TriggerDefinition>? triggerStatementsList = TriggerStatements[dbName] as List<TriggerDefinition>;
            if (triggerStatementsList == null)
            {
                triggerStatementsList = new List<TriggerDefinition>();
                TriggerStatements.Add(dbName, triggerStatementsList);
            }

            triggerStatementsList.Add(new TriggerDefinition(triggerName, sqlStatement));
        }

        /// <summary>
        /// Removes all trigger definitions and resets the trigger store to uninitialized.
        /// </summary>
        internal static void RemoveTriggerDefinitions()
        {
            if (TriggerStatements != default(Dictionary<string, List<TriggerDefinition>>))
            {
                TriggerStatements.Clear();
                TriggerStatements = default(Dictionary<string, List<TriggerDefinition>>);
            }
        }

        /// <summary>
        /// Adds an ALTER definition for a specific database.table entry.
        /// </summary>
        /// <param name="dbAndTableName">The combined database and table name used as the key.</param>
        /// <param name="columnName">The column being altered.</param>
        /// <param name="sqlStatement">The SQL text of the ALTER operation.</param>
        internal static void AddAlterDefinition(string dbAndTableName, string columnName, string sqlStatement)
        {
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            columnName = columnName.Trim();

            if (AlterStatements == null)
                AlterStatements = new Dictionary<string, List<AlterDefinition>>();

            List<AlterDefinition>? alterStatementsList = AlterStatements[dbAndTableName] as List<AlterDefinition>;
            if (alterStatementsList == null)
            {
                alterStatementsList = new List<AlterDefinition>();
                AlterStatements.Add(dbAndTableName, alterStatementsList);
            }

            alterStatementsList.Add(new AlterDefinition(columnName, sqlStatement));
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
                TableCreateStatements = new Dictionary<string, TableDefinition>();

            TableCreateStatements.Add(dbAndTableName, new TableDefinition(tableSQL, cloudPush));
        }

        /// <summary>
        /// Removes all table definitions and resets the table store to uninitialized.
        /// </summary>
        internal static void RemoveTableDefinitions()
        {
            if (TableCreateStatements != default(Dictionary<string, TableDefinition>))
            {
                TableCreateStatements.Clear();
                TableCreateStatements = default(Dictionary<string, TableDefinition>);
            }
        }

        /// <summary>
        /// Clears in-memory stores for alters, tables, indexes, and triggers.
        /// </summary>
        internal static void ClearStatementTables()
        {
            if (AlterStatements != default(Dictionary<string, List<AlterDefinition>>))
            {
                AlterStatements.Clear();
                AlterStatements = default(Dictionary<string, List<AlterDefinition>>);
            }

            if (TableCreateStatements != default(Dictionary<string, TableDefinition>))
            {
                TableCreateStatements?.Clear();
                TableCreateStatements = default(Dictionary<string, TableDefinition>)!;
            }

            if (IndexStatements != default(Dictionary<string, List<IndexDefinition>>))
            {
                IndexStatements.Clear();
                IndexStatements = default(Dictionary<string, List<IndexDefinition>>);
            }

            if (TriggerStatements != default(Dictionary<string, List<TriggerDefinition>>))
            {
                TriggerStatements.Clear();
                TriggerStatements = default(Dictionary<string, List<TriggerDefinition>>);
            }
        }

        /// <summary>
        /// Prevents external instantiation. Instances are not required because the class is used statically.
        /// </summary>
        internal SxmSqlStatements() { }
    }
}