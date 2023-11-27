using System.Collections;
using System.Collections.Specialized;

namespace SQLiteXM
{
    public class SqlStatements
	{
		internal static Hashtable? alterStatements = default(Hashtable);
		internal static Hashtable? indexStatements = default(Hashtable);
		internal static Dictionary<string, InsertDefinition> insertStatements = new Dictionary<string, InsertDefinition>();
		internal static Hashtable tableCreateStatements = new Hashtable();
		internal static Dictionary<string, SelectDefinition> selectStatements = new Dictionary<string, SelectDefinition>();
		internal static Dictionary<string, UpdateDefinition> updateStatements = new Dictionary<string, UpdateDefinition>();
		internal static Dictionary<string, DeleteDefinition> deleteStatements = new Dictionary<string, DeleteDefinition>();

		internal static void addInsertDefinition (string insertName, string tableName, string insertSQL)
		{
			if (!insertStatements.ContainsKey (insertName))
				insertStatements.Add ( insertName, new InsertDefinition (tableName, insertSQL));
		}

		internal static void addSelectDefinition (string selectName, string? tableName, string selectSQL)
		{
			if (!selectStatements.ContainsKey(selectName))
				selectStatements.Add (selectName, new SelectDefinition(tableName, selectSQL));
		}

		internal static void addUpdateDefinition (string updateName, string updateSQL)
		{
			if (!updateStatements.ContainsKey(updateName))
				updateStatements.Add (updateName, new UpdateDefinition(updateSQL));
		}

		internal static void addDeleteDefinition(string deleteName, string deleteSQL)
		{
			if (!deleteStatements.ContainsKey(deleteName))
				deleteStatements.Add (deleteName, new DeleteDefinition(deleteSQL));
		}

		internal static void addIndexDefinition (string dbAndTableName, string indexName, string sqlStatement)
		{
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

		internal static void addAlterDefinition (string dbAndTableName, string columnName, string sqlStatement)
		{
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
            addTableDefinition(dbAndTableName, tableSQL, Defines.NO_CLOUD_SYNCH);
        }

        internal static void addTableDefinition (string dbAndTableName, string tableSQL, int cloudPush)
		{
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
		}

		internal SqlStatements () {}
	}
}

