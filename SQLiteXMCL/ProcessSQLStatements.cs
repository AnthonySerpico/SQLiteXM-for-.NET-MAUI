using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Reflection.PortableExecutable;

namespace SQLiteXM
{
	public class ProcessSQLStatements
	{
		private ProcessSQLStatements() { }
		private static double versionNumber = 0;
		public static double getSqlStatementsVersionNumber { get => versionNumber; }
		internal static double setSqlStatementsVersionNumber { set { versionNumber = value; } get => versionNumber; }

		public static string retreiveDatabaseName { get => databaseName; }
		private static string databaseName = string.Empty;

        public static bool Parse (StreamReader sqlStatementAssets)
		{
			string sqlStatements = sqlStatementAssets.ReadToEnd ();
			return Parse (sqlStatements);
		}

		public static bool Parse (string sqlStatements)
		{
			int searchOffset = 0;

			while ( (searchOffset = getHeader (searchOffset, sqlStatements)) != -1 ) {}
			return true;
		}

		private static int getHeader (int searchOffset, string sqlStatements)
		{
			int index = sqlStatements.IndexOf (Defines.openStatementDelimeter, searchOffset);
			if (index != -1) 
			{
				int sIndex = index+1;
				index = sqlStatements.IndexOf (Defines.closeStatementDelimeter, sIndex);
				if (index != -1) 
				{
					if (sIndex == index)
						throw new SxmException (ErrorMessages.error["missingSQLStatementHeader"]);

					string header = sqlStatements.Substring (sIndex, index-sIndex).Trim();
					index = parseHeader (header, index+1, sqlStatements);
				}
				else
					throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);
			}

			return index;
		}

		private static int parseHeader(string header, int index, string sqlStatements)
		{
            header = header.ToLower ();

			switch (header) 
			{
                case "database":
                    index = getDatabaseName(index, sqlStatements,  ref databaseName);
                    break;

                case "version":
					checkDatabaseName();
                    index = getVersionNumber(index, sqlStatements);
                    break;

                case "table":
                    checkDatabaseName();
                    index = processTableStatements (index, sqlStatements);
					break;

				case "insert":
                    checkDatabaseName();
                    index = processInsertStatements (index, sqlStatements);
					break;

				case "alter":
                    checkDatabaseName();
                    index = processAlterStatements (index, sqlStatements);
					break;

				case "index":
                    checkDatabaseName();
                    index = processIndexStatements (index, sqlStatements);
					break;

				case "select":
				case "update":
				case "delete":
                    checkDatabaseName();
                    index = processStatement (index, header, sqlStatements);
					break;

				default:
					throw new SxmException (new ErrorMessage("unknownSQLStatementHeader", header));
			}

			return index;
        }

		private static void checkDatabaseName()
		{
			if(string.IsNullOrEmpty(databaseName))
                throw new SxmException(new ErrorMessage("missingDatabaseName", databaseName));
        }

        private static int getDatabaseName(int index, string name, ref string databaseName)
		{
            CommandReturn? commandReturn = default(CommandReturn);

            do
            {
                commandReturn = getCommand(index, name);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the version statement.
                    break;

                databaseName = commandReturn.command;
            } while (true);
			{
                char[] pattern = Path.GetInvalidFileNameChars();
                if(databaseName.Any(pattern.Contains) || string.IsNullOrEmpty(databaseName))
                    throw new SxmException(new ErrorMessage("invalidDBName", databaseName));
            }

            return index;
        }
        private static int getVersionNumber(int index, string versionStatement)
        {
            CommandReturn commandReturn = null;
			string version = string.Empty;

            do
            {
                commandReturn = getCommand(index, versionStatement);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the version statement.
                    break;

                version = commandReturn.command;
            } while (true);

			try
			{
				if (!string.IsNullOrEmpty(versionStatement))
				{
					versionNumber = Convert.ToDouble(version);
					if(versionNumber < 0)
                        throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));
                }
            }
			catch(System.FormatException)
			{

				if (version != null)
					version = "(is blank)";

                throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));
            }
            return index;
        }

        private static int processTableStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
            string tableName;
            string sqlStatement;
			int synch;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the table statements.
					break;
				tableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The table create statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
				}
				sqlStatement = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The synch statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
				}
				synch = parseSynchCommand (commandReturn.command.ToLower ());

				SqlStatements.addTableDefinition (databaseName + "." + tableName, sqlStatement, synch);
			} while (true);

			return index;
		}

		private static int processInsertStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string sqlStatement;
			string tableName;
			string dbName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the insert statements.
					break;
				dbName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				tableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "INSERT"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addInsertDefinition (dbName, tableName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processAlterStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string tableName;
			string sqlStatement;
			string columnName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the alter statements.
					break;
				tableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				columnName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "ALTER"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addAlterDefinition (databaseName + "." + tableName, columnName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processIndexStatements(int index, string sqlStatements)
		{
			CommandReturn commandReturn = null;
			string tableName;
			string sqlStatement;
			string indexName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the index statements.
					break;
                indexName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
                tableName = commandReturn.command;

				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", "INDEX"));
				}
				sqlStatement = commandReturn.command;

				SqlStatements.addIndexDefinition (databaseName + "." + tableName, indexName, sqlStatement);
			} while (true);

			return index;
		}

		private static int processStatement(int index, string header, string sqlStatements)
		{
			CommandReturn commandReturn = null;
            string tableName = default(string);
            string sqlStatement;
            string sqlName;

			do {
				commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // Were finished processing the select statements.
					break;
				sqlName = commandReturn.command;

				commandReturn = getCommand(index, sqlStatements);
				index = commandReturn.index;
				tableName = commandReturn.command;

                commandReturn = getCommand (index, sqlStatements);
				index = commandReturn.index;
				if (commandReturn.command.Length == 0) // The SQL select statement cannot be empty.
				{
					throw new SxmException (new ErrorMessage("invalidSQLStatementDefinition", header));
				}
				sqlStatement = commandReturn.command;

				if( header.Equals ("select") == true)
					SqlStatements.addSelectDefinition (sqlName, tableName, sqlStatement);
				if( header.Equals ("delete") == true)
					SqlStatements.addDeleteDefinition (sqlName, tableName, sqlStatement);
				if( header.Equals ("update") == true)
					SqlStatements.addUpdateDefinition (sqlName, tableName, sqlStatement);
			} while (true);

			return index;
		}

		private static CommandReturn getCommand (int index, string sqlStatements)
		{
			CommandReturn commandReturn = new CommandReturn ();

			index = sqlStatements.IndexOf (Defines.openStatementDelimeter, index);
			if (index != -1) 
			{
				int sIndex = index+1;
				index = sqlStatements.IndexOf (Defines.closeStatementDelimeter, sIndex);
				if (index != -1) 
				{
					if (sIndex != index)
						commandReturn.command = sqlStatements.Substring (sIndex, index - sIndex).Trim();
					else
						commandReturn.command = string.Empty;

					commandReturn.index = index+1;
				}
				else
					throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);
			}
			else
				throw new SxmException (ErrorMessages.error["invalidSQLStatementFile"]);

			return commandReturn;
		}

		private static int parseSynchCommand (string synchCommand)
		{
			if (synchCommand.Equals ("synch") == true)
				return Defines.CLOUD_SYNCH;
			if (synchCommand.Equals ("no_synch") == true)
				return Defines.NO_CLOUD_SYNCH;
			if (synchCommand.Equals ("move") == true)
				return Defines.CLOUD_MOVE;

			throw new SxmException (new ErrorMessage("unknownSynchCommand", synchCommand));
		}

        class CommandReturn
		{
			public int index; 
			public string command;

			public CommandReturn()
			{
			}
		}
	}

}

