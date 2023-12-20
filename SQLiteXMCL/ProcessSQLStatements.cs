using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace SQLiteXM
{
    // using System.Xml.Serialization;
    // XmlSerializer serializer = new XmlSerializer(typeof(Root));
    // using (StringReader reader = new StringReader(xml))
    // {
    //    var test = (Root)serializer.Deserialize(reader);
    // }

    [XmlRoot(ElementName = "table")]
    public class Table
    {

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "alter")]
    public class Alter
    {

        [XmlElement(ElementName = "ColumnName")]
        public string ColumnName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "index")]
    public class Index
    {

        [XmlElement(ElementName = "IndexName")]
        public string IndexName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "insert")]
    public class Insert
    {

        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "select")]
    public class Select
    {

        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "update")]
    public class Update
    {

        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "delete")]
    public class Delete
    {

        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "trigger")]
    public class Trigger
    {

        [XmlElement(ElementName = "TriggerName")]
        public string TriggerName { get; set; }

        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    [XmlRoot(ElementName = "rootxml")]
    public class RootXml
    {

        [XmlElement(ElementName = "database")]
        public string Database { get; set; }

        [XmlElement(ElementName = "version")]
        public double Version { get; set; }

        [XmlElement(ElementName = "table")]
        public List<Table> Table { get; set; }

        [XmlElement(ElementName = "alter")]
        public List<Alter> Alter { get; set; }

        [XmlElement(ElementName = "index")]
        public List<Index> Index { get; set; }

        [XmlElement(ElementName = "insert")]
        public List<Insert> Insert { get; set; }

        [XmlElement(ElementName = "select")]
        public List<Select> Select { get; set; }

        [XmlElement(ElementName = "update")]
        public List<Update> Update { get; set; }

        [XmlElement(ElementName = "delete")]
        public List<Delete> Delete { get; set; }

        [XmlElement(ElementName = "trigger")]
        public List<Trigger> Trigger { get; set; }
    }

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class RootJson
    {
        public string database { get; set; }
        public double version { get; set; }
        public List<Dictionary<string, string>> Table { get; set; }
        public List<Dictionary<string, string>> Alter { get; set; }
        public List<Dictionary<string, string>> Index { get; set; }
        public List<Dictionary<string, string>> Insert { get; set; }
        public List<Dictionary<string, string>> Select { get; set; }
        public List<Dictionary<string, string>> Update { get; set; }
        public List<Dictionary<string, string>> Delete { get; set; }
        public List<Dictionary<string, string>> Trigger { get; set; }
    }

    public class ProcessSQLStatements
    {
        private ProcessSQLStatements() { }
        private static double versionNumber = 0;
        public static double getSqlStatementsVersionNumber { get => versionNumber; }
        internal static double setSqlStatementsVersionNumber { set { versionNumber = value; } get => versionNumber; }

        public static string retreiveDatabaseName { get => databaseName; }
        private static string databaseName = string.Empty;

        public static bool Parse(StreamReader sqlStatementAssets)
        {
            string sqlStatements = sqlStatementAssets.ReadToEnd();
            return Parse(sqlStatements);
        }

        public static bool Parse(Stream sqlStatementAssets, Defines.SqlStatementsFileType sqlStatementsFileType)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // Set camelCase
            };

            if (sqlStatementsFileType == Defines.SqlStatementsFileType.json)
            {
                RootJson? rootJson = JsonSerializer.Deserialize<RootJson>(sqlStatementAssets, options);
                processJson(rootJson);
            }
            else
            {
                if (sqlStatementsFileType == Defines.SqlStatementsFileType.xml)
                {
                    RootXml? rootXml = (RootXml?)(new XmlSerializer(typeof(RootXml))).Deserialize(sqlStatementAssets);
                    processXml(rootXml);
                }
            }

            return true;
        }
        private static void processXml(RootXml? rootXml)
        {
            if (rootXml != default)
            {
                databaseName = rootXml.Database.Trim();
                checkValidDatabaseName();

                setVersionNumber(rootXml.Version);

                if (rootXml?.Table != default)
                    foreach (Table tableEntry in rootXml.Table)
                        SqlStatements.addTableDefinition(databaseName + "." + tableEntry.TableName, tableEntry.Statement);

                if (rootXml?.Index != default)
                    foreach (Index indexEntry in rootXml.Index)
                        SqlStatements.addIndexDefinition(databaseName + "." + indexEntry.TableName, indexEntry.IndexName, indexEntry.Statement);

                if (rootXml?.Alter != default)
                    foreach (Alter alterEntry in rootXml.Alter)
                        SqlStatements.addAlterDefinition(databaseName + "." + alterEntry.TableName, alterEntry.ColumnName, alterEntry.Statement);

                if (rootXml?.Delete != default)
                    foreach (Delete deleteEntry in rootXml.Delete)
                        SqlStatements.addDeleteDefinition(deleteEntry.StatementName, deleteEntry.TableName, deleteEntry.Statement);

                if (rootXml?.Update != default)
                    foreach (Update updateEntry in rootXml.Update)
                        SqlStatements.addUpdateDefinition(updateEntry.StatementName, updateEntry.TableName, updateEntry.Statement);

                if (rootXml?.Select != default)
                    foreach (Select selectEntry in rootXml.Select)
                        SqlStatements.addSelectDefinition(selectEntry.StatementName, selectEntry.TableName, selectEntry.Statement);

                if (rootXml?.Insert != default)
                    foreach (Insert insertEntry in rootXml.Insert)
                        SqlStatements.addInsertDefinition(insertEntry.StatementName, insertEntry.TableName, insertEntry.Statement);

                if (rootXml?.Trigger != default)
                    foreach (Trigger triggerEntry in rootXml.Trigger)
                        SqlStatements.addTriggerDefinition(databaseName, triggerEntry.TriggerName, triggerEntry.Statement);
            }
        }

            private static void processJson(RootJson? rootJson)
        {
            if (rootJson != default)
            {
                databaseName = rootJson.database.Trim();
                checkValidDatabaseName();

                setVersionNumber(rootJson.version);

                if (rootJson?.Table != default)
                    foreach (Dictionary<string, string> tableEntry in rootJson.Table)
                        SqlStatements.addTableDefinition(databaseName + "." + tableEntry["Table Name"], tableEntry["Statement"]);

                if (rootJson?.Index != default)
                    foreach (Dictionary<string, string> indexEntry in rootJson.Index)
                        SqlStatements.addIndexDefinition(databaseName + "." + indexEntry["Table Name"], indexEntry["Index Name"], indexEntry["Statement"]);

                if (rootJson?.Alter != default)
                    foreach (Dictionary<string, string> alterEntry in rootJson.Alter)
                        SqlStatements.addAlterDefinition(databaseName + "." + alterEntry["Table Name"], alterEntry["Column Name"], alterEntry["Statement"]);

                if (rootJson?.Delete != default)
                    foreach (Dictionary<string, string> deleteEntry in rootJson.Delete)
                        SqlStatements.addDeleteDefinition(deleteEntry["Statement Name"], deleteEntry["Table Name"], deleteEntry["Statement"]);

                if (rootJson?.Update != default)
                    foreach (Dictionary<string, string> updateEntry in rootJson.Update)
                        SqlStatements.addUpdateDefinition(updateEntry["Statement Name"], updateEntry["Table Name"], updateEntry["Statement"]);

                if (rootJson?.Select != default)
                    foreach (Dictionary<string, string> selectEntry in rootJson.Select)
                        SqlStatements.addSelectDefinition(selectEntry["Statement Name"], selectEntry["Table Name"], selectEntry["Statement"]);

                if (rootJson?.Insert != default)
                    foreach (Dictionary<string, string> insertEntry in rootJson.Insert)
                        SqlStatements.addInsertDefinition(insertEntry["Statement Name"], insertEntry["Table Name"], insertEntry["Statement"]);

                if (rootJson?.Trigger != default)
                    foreach (Dictionary<string, string> triggerEntry in rootJson.Trigger)
                        SqlStatements.addTriggerDefinition(databaseName, triggerEntry["Trigger Name"], triggerEntry["Statement"]);
            }
        }

        private static double setVersionNumber(double version)
        {
            if (version < 0)
                throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));

            ProcessSQLStatements.setSqlStatementsVersionNumber = version;
            return version;
        }

        public static bool Parse(string sqlStatements)
        {
            int searchOffset = 0;

            while ((searchOffset = getHeader(searchOffset, sqlStatements)) != -1) { }
            return true;
        }

        private static int getHeader(int searchOffset, string sqlStatements)
        {
            int index = sqlStatements.IndexOf(Defines.openStatementDelimeter, searchOffset);
            if (index != -1)
            {
                int sIndex = index + 1;
                index = sqlStatements.IndexOf(Defines.closeStatementDelimeter, sIndex);
                if (index != -1)
                {
                    if (sIndex == index)
                        throw new SxmException(ErrorMessages.error["missingSQLStatementHeader"]);

                    string header = sqlStatements.Substring(sIndex, index - sIndex).Trim();
                    index = parseHeader(header, index + 1, sqlStatements);
                }
                else
                    throw new SxmException(ErrorMessages.error["invalidSQLStatementFile"]);
            }

            return index;
        }

        private static int parseHeader(string header, int index, string sqlStatements)
        {
            header = header.ToLower();

            switch (header)
            {
                case "database":
                    index = getDatabaseName(index, sqlStatements, ref databaseName);
                    break;

                case "version":
                    checkDatabaseName();
                    index = getVersionNumber(index, sqlStatements);
                    break;

                case "table":
                    checkDatabaseName();
                    index = processTableStatements(index, sqlStatements);
                    break;

                case "trigger":
                    checkDatabaseName();
                    index = processTriggerStatements(index, sqlStatements);
                    break;

                case "insert":
                    checkDatabaseName();
                    index = processInsertStatements(index, sqlStatements);
                    break;

                case "alter":
                    checkDatabaseName();
                    index = processAlterStatements(index, sqlStatements);
                    break;

                case "index":
                    checkDatabaseName();
                    index = processIndexStatements(index, sqlStatements);
                    break;

                case "select":
                case "update":
                case "delete":
                    checkDatabaseName();
                    index = processStatement(index, header, sqlStatements);
                    break;

                default:
                    throw new SxmException(new ErrorMessage("unknownSQLStatementHeader", header));
            }

            return index;
        }

        private static void checkDatabaseName()
        {
            if (string.IsNullOrEmpty(databaseName))
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

                databaseName = commandReturn.command.Trim();
            } while (true);

            checkValidDatabaseName();
            return index;
        }
        private static void checkValidDatabaseName()
        {
            char[] pattern = Path.GetInvalidFileNameChars();
            if (databaseName.Any(pattern.Contains) || string.IsNullOrEmpty(databaseName))
                throw new SxmException(new ErrorMessage("invalidDBName", databaseName));
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
                    if (versionNumber < 0)
                        throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));
                }
            }
            catch (System.FormatException)
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

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the table statements.
                    break;
                tableName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The table create statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
                }
                sqlStatement = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The synch statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "TABLE"));
                }
                synch = parseSynchCommand(commandReturn.command.ToLower());

                SqlStatements.addTableDefinition(databaseName + "." + tableName, sqlStatement, synch);
            } while (true);

            return index;
        }

        private static int processInsertStatements(int index, string sqlStatements)
        {
            CommandReturn commandReturn = null;
            string sqlStatement;
            string tableName;
            string dbName;

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the insert statements.
                    break;
                dbName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                tableName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "INSERT"));
                }
                sqlStatement = commandReturn.command;

                SqlStatements.addInsertDefinition(dbName, tableName, sqlStatement);
            } while (true);

            return index;
        }

        private static int processAlterStatements(int index, string sqlStatements)
        {
            CommandReturn commandReturn = null;
            string tableName;
            string sqlStatement;
            string columnName;

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the alter statements.
                    break;
                columnName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                tableName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "ALTER"));
                }
                sqlStatement = commandReturn.command;

                SqlStatements.addAlterDefinition(databaseName + "." + tableName, columnName, sqlStatement);
            } while (true);

            return index;
        }

        private static int processTriggerStatements(int index, string sqlStatements)
        {
            CommandReturn commandReturn = null;
            string sqlStatement;
            string triggerName;

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the trigger statements.
                    break;
                triggerName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The SQL trigger statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "TRIGGER"));
                }
                sqlStatement = commandReturn.command;

                SqlStatements.addTriggerDefinition(databaseName, triggerName, sqlStatement);
            } while (true);

            return index;
        }

        private static int processIndexStatements(int index, string sqlStatements)
        {
            CommandReturn commandReturn = null;
            string tableName;
            string sqlStatement;
            string indexName;

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the index statements.
                    break;
                indexName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                tableName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The SQL insert statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", "INDEX"));
                }
                sqlStatement = commandReturn.command;

                SqlStatements.addIndexDefinition(databaseName + "." + tableName, indexName, sqlStatement);
            } while (true);

            return index;
        }

        private static int processStatement(int index, string header, string sqlStatements)
        {
            CommandReturn commandReturn = null;
            string tableName = default(string);
            string sqlStatement;
            string sqlName;

            do
            {
                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // Were finished processing the select statements.
                    break;
                sqlName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                tableName = commandReturn.command;

                commandReturn = getCommand(index, sqlStatements);
                index = commandReturn.index;
                if (commandReturn.command.Length == 0) // The SQL select statement cannot be empty.
                {
                    throw new SxmException(new ErrorMessage("invalidSQLStatementDefinition", header));
                }
                sqlStatement = commandReturn.command;

                if (header.Equals("select") == true)
                    SqlStatements.addSelectDefinition(sqlName, tableName, sqlStatement);
                if (header.Equals("delete") == true)
                    SqlStatements.addDeleteDefinition(sqlName, tableName, sqlStatement);
                if (header.Equals("update") == true)
                    SqlStatements.addUpdateDefinition(sqlName, tableName, sqlStatement);
            } while (true);

            return index;
        }

        private static CommandReturn getCommand(int index, string sqlStatements)
        {
            CommandReturn commandReturn = new CommandReturn();

            index = sqlStatements.IndexOf(Defines.openStatementDelimeter, index);
            if (index != -1)
            {
                int sIndex = index + 1;
                index = sqlStatements.IndexOf(Defines.closeStatementDelimeter, sIndex);
                if (index != -1)
                {
                    if (sIndex != index)
                        commandReturn.command = sqlStatements.Substring(sIndex, index - sIndex).Trim();
                    else
                        commandReturn.command = string.Empty;

                    commandReturn.index = index + 1;
                }
                else
                    throw new SxmException(ErrorMessages.error["invalidSQLStatementFile"]);
            }
            else
                throw new SxmException(ErrorMessages.error["invalidSQLStatementFile"]);

            return commandReturn;
        }

        private static int parseSynchCommand(string synchCommand)
        {
            if (synchCommand.Equals("synch") == true)
                return Defines.CLOUD_SYNCH;
            if (synchCommand.Equals("no_synch") == true)
                return Defines.NO_CLOUD_SYNCH;
            if (synchCommand.Equals("move") == true)
                return Defines.CLOUD_MOVE;

            throw new SxmException(new ErrorMessage("unknownSynchCommand", synchCommand));
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

