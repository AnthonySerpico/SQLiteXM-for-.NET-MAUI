using SQLiteXM;
using System.Collections;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class SqlStatements
	{
        internal static Hashtable? tableCreateStatements = new Hashtable();
        internal static Hashtable? alterStatements = default(Hashtable);
		internal static Hashtable? indexStatements = default(Hashtable);
        internal static Hashtable? triggerStatements = default(Hashtable);
        internal static Dictionary<string, InsertDefinition> insertStatements = new Dictionary<string, InsertDefinition>();
		internal static Dictionary<string, SelectDefinition> selectStatements = new Dictionary<string, SelectDefinition>();
		internal static Dictionary<string, UpdateDefinition> updateStatements = new Dictionary<string, UpdateDefinition>();
		internal static Dictionary<string, DeleteDefinition> deleteStatements = new Dictionary<string, DeleteDefinition>();

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

        internal static void addInsertDefinition (string insertName, string tableName, string insertSQL)
		{
            insertName = insertName.Trim();

            if (!insertStatements.ContainsKey (insertName))
				insertStatements.Add ( insertName, new InsertDefinition (tableName.Trim(), insertSQL.Trim()));
		}
        internal static void removeInsertDefinition(string insertName)
        {
            insertName = insertName.Trim();

            if (!insertStatements.ContainsKey(insertName))
                insertStatements.Remove(insertName);
        }

        internal static void addSelectDefinition (string selectName, string tableName, string selectSQL)
		{
            selectName = selectName.Trim();

            if (!selectStatements.ContainsKey(selectName))
				selectStatements.Add (selectName, new SelectDefinition(tableName.Trim(), selectSQL.Trim()));
		}

		internal static void addUpdateDefinition (string updateName, string tableName, string updateSQL)
		{
            updateName = updateName.Trim();

            if (!updateStatements.ContainsKey(updateName))
				updateStatements.Add (updateName, new UpdateDefinition(tableName.Trim(), updateSQL.Trim()));
		}

		internal static void addDeleteDefinition(string deleteName, string tableName, string deleteSQL)
		{
            deleteName = deleteName.Trim();

            if (!deleteStatements.ContainsKey(deleteName))
				deleteStatements.Add (deleteName, new DeleteDefinition(tableName.Trim(), deleteSQL.Trim()));
		}

		internal static void addIndexDefinition (string dbAndTableName, string indexName, string sqlStatement)
		{
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            indexName = indexName.Trim();

            if (indexStatements == null)
				indexStatements = new Hashtable();

			ArrayList? indexStatementsList = indexStatements [dbAndTableName] as ArrayList;
			if (indexStatementsList == null) 
			{
				indexStatementsList = new ArrayList ();
				indexStatements.Add ( dbAndTableName, indexStatementsList);
			}

			indexStatementsList.Add ( new IndexDefinition (indexName, sqlStatement));
		}

        internal static void addTriggerDefinition(string dbName, string triggerName, string sqlStatement)
        {
            sqlStatement = sqlStatement.Trim();
            triggerName = triggerName.Trim();
            dbName = dbName.Trim();

            if (triggerStatements == null)
                triggerStatements = new Hashtable();

            ArrayList? triggerStatementsList = triggerStatements[dbName] as ArrayList;
            if (triggerStatementsList == null)
            {
                triggerStatementsList = new ArrayList();
                triggerStatements.Add(dbName, triggerStatementsList);
            }

            triggerStatementsList.Add(new TriggerDefinition(triggerName, sqlStatement));
        }

        internal static void addAlterDefinition (string dbAndTableName, string columnName, string sqlStatement)
		{
            dbAndTableName = dbAndTableName.Trim();
            sqlStatement = sqlStatement.Trim();
            columnName = columnName.Trim();

            if (alterStatements == null)
				alterStatements = new Hashtable();

			ArrayList? alterStatementsList = alterStatements [dbAndTableName] as ArrayList;
			if (alterStatementsList == null) 
			{
				alterStatementsList = new ArrayList ();
				alterStatements.Add ( dbAndTableName, alterStatementsList);
			}

			alterStatementsList.Add ( new AlterDefinition (columnName, sqlStatement));
		}

        internal static void addTableDefinition(string dbAndTableName, string tableSQL)
        {
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            addTableDefinition(dbAndTableName, tableSQL, Defines.NO_CLOUD_SYNCH);
        }

        internal static void addTableDefinition (string dbAndTableName, string tableSQL, int cloudPush)
		{
            dbAndTableName = dbAndTableName.Trim();
            tableSQL = tableSQL.Trim();

            if (tableCreateStatements == null)
				tableCreateStatements = new Hashtable();

			tableCreateStatements.Add(dbAndTableName, new TableDefinition (tableSQL, cloudPush));
		}

		internal static void clearStatementTables ()
		{
			if (alterStatements != default(Hashtable)) 
			{
				alterStatements.Clear ();
				alterStatements = default(Hashtable);
			}

			tableCreateStatements?.Clear ();
			tableCreateStatements = default(Hashtable)!;

			if (indexStatements != default(Hashtable)) 
			{
				indexStatements.Clear ();
				indexStatements = default(Hashtable);
			}

            if (triggerStatements != default(Hashtable))
            {
                triggerStatements.Clear();
                triggerStatements = default(Hashtable);
            }
        }

        internal SqlStatements () {}
	}
}

