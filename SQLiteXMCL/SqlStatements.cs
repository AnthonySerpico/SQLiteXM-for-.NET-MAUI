using System.Collections;
using System.Collections.Specialized;

namespace SQLiteXM
{
    public class SqlStatements
	{
		internal static Hashtable? alterStatements = default(Hashtable);
		internal static Hashtable? indexStatements = default(Hashtable);
		internal static Hashtable insertStatements = new Hashtable();
		internal static Hashtable tableCreateStatements = new Hashtable();
		internal static Dictionary<string, SelectDefinition> selectStatements = new Dictionary<string, SelectDefinition>( );
		internal static NameValueCollection updateStatements = new NameValueCollection ();
		internal static NameValueCollection deleteStatements = new NameValueCollection ();

		internal static void addInsertDefinition (string insertName, string tableName, string insertSQL)
		{
			if (insertStatements.ContainsKey (insertName) == false)
				insertStatements.Add ( insertName, new InsertDefinition (tableName, insertSQL));
		}

		internal static void addSelectDefinition (string selectName, string? tableName, string selectSQL)
		{
			if (!selectStatements.ContainsKey(selectName))
				selectStatements.Add (selectName, new SelectDefinition(tableName, selectSQL));
		}

		internal static void addUpdateDefinition (string updateName, string updateSQL)
		{
			if (updateStatements [updateName] == null)
				updateStatements.Add (updateName, updateSQL);
		}

		internal static void addDeleteDefinition(string deleteName, string deleteSQL)
		{
			if (deleteStatements [deleteName] == null)
				deleteStatements.Add (deleteName, deleteSQL);
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

