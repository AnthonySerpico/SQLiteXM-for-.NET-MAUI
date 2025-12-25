using System.Text.Json;
using System.Xml.Serialization;

namespace SQLiteXM
{
    // using System.Xml.Serialization;
    // XmlSerializer serializer = new XmlSerializer(typeof(Root));
    // using (StringReader reader = new StringReader(xml))
    // {
    //    var test = (Root)serializer.Deserialize(reader);
    // }

    /// <summary>
    /// Represents a table entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "table")]
    public class Table
    {

        /// <summary>
        /// The name of the table (as found in the source SQL definition).
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL statement text used to create or define the table.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents an alter (column) entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "alter")]
    public class Alter
    {

        /// <summary>
        /// The column name affected by the alter command.
        /// </summary>
        [XmlElement(ElementName = "ColumnName")]
        public string ColumnName { get; set; }

        /// <summary>
        /// The table name associated with the alter command.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL alter statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents an index definition entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "index")]
    public class Index
    {

        /// <summary>
        /// The index name.
        /// </summary>
        [XmlElement(ElementName = "IndexName")]
        public string IndexName { get; set; }

        /// <summary>
        /// The table name the index belongs to.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL index statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents an insert statement entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "insert")]
    public class Insert
    {

        /// <summary>
        /// Optional name for the insert statement.
        /// </summary>
        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        /// <summary>
        /// The table targeted by the insert.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL insert statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents a select statement entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "select")]
    public class Select
    {

        /// <summary>
        /// Optional name for the select statement.
        /// </summary>
        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        /// <summary>
        /// The table targeted by the select.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL select statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents an update statement entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "update")]
    public class Update
    {

        /// <summary>
        /// Optional name for the update statement.
        /// </summary>
        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        /// <summary>
        /// The table targeted by the update.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL update statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents a delete statement entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "delete")]
    public class Delete
    {

        /// <summary>
        /// Optional name for the delete statement.
        /// </summary>
        [XmlElement(ElementName = "StatementName")]
        public string StatementName { get; set; }

        /// <summary>
        /// The table targeted by the delete.
        /// </summary>
        [XmlElement(ElementName = "TableName")]
        public string TableName { get; set; }

        /// <summary>
        /// The SQL delete statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Represents a trigger entry in an XML SQL statements file.
    /// </summary>
    [XmlRoot(ElementName = "trigger")]
    public class Trigger
    {

        /// <summary>
        /// The trigger name.
        /// </summary>
        [XmlElement(ElementName = "TriggerName")]
        public string TriggerName { get; set; }

        /// <summary>
        /// The SQL trigger statement text.
        /// </summary>
        [XmlElement(ElementName = "Statement")]
        public string Statement { get; set; }
    }

    /// <summary>
    /// Root model for XML formatted SQL statements files.
    /// Maps top-level XML elements to strongly typed collections.
    /// </summary>
    [XmlRoot(ElementName = "rootxml")]
    public class RootXml
    {

        /// <summary>
        /// Database identifier/name included in the SQL statements file.
        /// </summary>
        [XmlElement(ElementName = "database")]
        public string Database { get; set; }

        /// <summary>
        /// Version number of the SQL statements file format/content.
        /// </summary>
        [XmlElement(ElementName = "version")]
        public long Version { get; set; }

        /// <summary>
        /// Collection of table definitions.
        /// </summary>
        [XmlElement(ElementName = "table")]
        public List<Table> Table { get; set; }

        /// <summary>
        /// Collection of alter definitions.
        /// </summary>
        [XmlElement(ElementName = "alter")]
        public List<Alter> Alter { get; set; }

        /// <summary>
        /// Collection of index definitions.
        /// </summary>
        [XmlElement(ElementName = "index")]
        public List<Index> Index { get; set; }

        /// <summary>
        /// Collection of insert statements.
        /// </summary>
        [XmlElement(ElementName = "insert")]
        public List<Insert> Insert { get; set; }

        /// <summary>
        /// Collection of select statements.
        /// </summary>
        [XmlElement(ElementName = "select")]
        public List<Select> Select { get; set; }

        /// <summary>
        /// Collection of update statements.
        /// </summary>
        [XmlElement(ElementName = "update")]
        public List<Update> Update { get; set; }

        /// <summary>
        /// Collection of delete statements.
        /// </summary>
        [XmlElement(ElementName = "delete")]
        public List<Delete> Delete { get; set; }

        /// <summary>
        /// Collection of trigger definitions.
        /// </summary>
        [XmlElement(ElementName = "trigger")]
        public List<Trigger> Trigger { get; set; }
    }

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    /// <summary>
    /// Root model for JSON formatted SQL statements files.
    /// Uses dictionaries for flexible JSON key names and to match expected payload structure.
    /// </summary>
    public class RootJson
    {
        /// <summary>
        /// Database identifier/name included in the SQL statements file.
        /// </summary>
        public string database { get; set; }

        /// <summary>
        /// Version number of the SQL statements file format/content.
        /// </summary>
        public long version { get; set; }

        /// <summary>
        /// Collections of statement entries represented as dictionaries keyed by column names.
        /// Expected keys differ slightly from XML variant (e.g. "Table Name" vs "TableName").
        /// </summary>
        public List<Dictionary<string, string>> Table { get; set; }
        public List<Dictionary<string, string>> Alter { get; set; }
        public List<Dictionary<string, string>> Index { get; set; }
        public List<Dictionary<string, string>> Insert { get; set; }
        public List<Dictionary<string, string>> Select { get; set; }
        public List<Dictionary<string, string>> Update { get; set; }
        public List<Dictionary<string, string>> Delete { get; set; }
        public List<Dictionary<string, string>> Trigger { get; set; }
    }

    /// <summary>
    /// Parser for SQL statements files. Supports XML and JSON formats and populates the SqlStatements registry.
    /// This class exposes static Parse methods and maintains a version and database name read from the file.
    /// </summary>
    public class SxmProcessSQLStatements
    {
        private SxmProcessSQLStatements() { }

        // Backing field for the currently parsed SQL statements version.
        private static long versionNumber = 0;

        /// <summary>
        /// Gets the version number found in the last parsed SQL statements file.
        /// </summary>
        public static long getSqlStatementsVersionNumber { get => versionNumber; }

        /// <summary>
        /// Internal property used to set/read the stored version number.
        /// </summary>
        internal static long setSqlStatementsVersionNumber { set { versionNumber = value; } get => versionNumber; }

        /// <summary>
        /// Gets the database name parsed from the last SQL statements file.
        /// </summary>
        public static string retreiveDatabaseName { get => databaseName; }

        // Backing field for the database name identified in the parsed file.
        private static string databaseName = string.Empty;

        /// <summary>
        /// Parse SQL statements from a StreamReader. Convenience wrapper that reads the entire stream then parses the string.
        /// </summary>
        /// <param name="sqlStatementAssets">StreamReader containing the SQL statements file contents.</param>
        /// <returns>True when parsing completes successfully (throws on error).</returns>
        public static bool Parse(StreamReader sqlStatementAssets)
        {
            string sqlStatements = sqlStatementAssets.ReadToEnd();
            return Parse(sqlStatements);
        }

        /// <summary>
        /// Parse SQL statements from a Stream with an explicitly provided file type (json or xml).
        /// Populates internal SqlStatements registry according to parsed entries.
        /// </summary>
        /// <param name="sqlStatementAssets">Stream that contains the JSON or XML document.</param>
        /// <param name="sqlStatementsFileType">Indicates whether the stream is JSON or XML.</param>
        /// <returns>True when parsing completes successfully (throws on error).</returns>
        public static bool Parse(Stream sqlStatementAssets, SxmDefines.SqlStatementsFileType sqlStatementsFileType)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // Set camelCase
            };

            if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.json)
            {
                RootJson? rootJson = JsonSerializer.Deserialize<RootJson>(sqlStatementAssets, options);
                processJson(rootJson);
            }
            else
            {
                if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.xml)
                {
                    RootXml? rootXml = (RootXml?)(new XmlSerializer(typeof(RootXml))).Deserialize(sqlStatementAssets);
                    processXml(rootXml);
                }
            }

            return true;
        }

        /// <summary>
        /// Process a deserialized XML root object and register each found SQL definition into SqlStatements.
        /// </summary>
        /// <param name="rootXml">Deserialized XML root object (may be null).</param>
        private static void processXml(RootXml? rootXml)
        {
            if (rootXml != default)
            {
                databaseName = rootXml.Database.Trim();
                checkValidDatabaseName();

                setVersionNumber(rootXml.Version);

                if (rootXml?.Table != default)
                    foreach (Table tableEntry in rootXml.Table)
                        SxmSqlStatements.addTableDefinition(databaseName + "." + tableEntry.TableName, tableEntry.Statement);

                if (rootXml?.Index != default)
                    foreach (Index indexEntry in rootXml.Index)
                        SxmSqlStatements.addIndexDefinition(databaseName + "." + indexEntry.TableName, indexEntry.IndexName, indexEntry.Statement);

                if (rootXml?.Alter != default)
                    foreach (Alter alterEntry in rootXml.Alter)
                        SxmSqlStatements.addAlterDefinition(databaseName + "." + alterEntry.TableName, alterEntry.ColumnName, alterEntry.Statement);

                if (rootXml?.Delete != default)
                    foreach (Delete deleteEntry in rootXml.Delete)
                        SxmSqlStatements.addDeleteDefinition(deleteEntry.StatementName, deleteEntry.TableName, deleteEntry.Statement);

                if (rootXml?.Update != default)
                    foreach (Update updateEntry in rootXml.Update)
                        SxmSqlStatements.addUpdateDefinition(updateEntry.StatementName, updateEntry.TableName, updateEntry.Statement);

                if (rootXml?.Select != default)
                    foreach (Select selectEntry in rootXml.Select)
                        SxmSqlStatements.addSelectDefinition(selectEntry.StatementName, selectEntry.TableName, selectEntry.Statement);

                if (rootXml?.Insert != default)
                    foreach (Insert insertEntry in rootXml.Insert)
                        SxmSqlStatements.addInsertDefinition(insertEntry.StatementName, insertEntry.TableName, insertEntry.Statement);

                if (rootXml?.Trigger != default)
                    foreach (Trigger triggerEntry in rootXml.Trigger)
                        SxmSqlStatements.addTriggerDefinition(databaseName, triggerEntry.TriggerName, triggerEntry.Statement);
            }
        }

        /// <summary>
        /// Process a deserialized JSON root object and register each found SQL definition into SqlStatements.
        /// JSON uses dictionary entries so keys must match expected textual keys (e.g. "Table Name").
        /// </summary>
        /// <param name="rootJson">Deserialized JSON root object (may be null).</param>
        private static void processJson(RootJson? rootJson)
        {
            if (rootJson != default)
            {
                databaseName = rootJson.database.Trim();
                checkValidDatabaseName();

                setVersionNumber(rootJson.version);

                if (rootJson?.Table != default)
                    foreach (Dictionary<string, string> tableEntry in rootJson.Table)
                        SxmSqlStatements.addTableDefinition(databaseName + "." + tableEntry["Table Name"], tableEntry["Statement"]);

                if (rootJson?.Index != default)
                    foreach (Dictionary<string, string> indexEntry in rootJson.Index)
                        SxmSqlStatements.addIndexDefinition(databaseName + "." + indexEntry["Table Name"], indexEntry["Index Name"], indexEntry["Statement"]);

                if (rootJson?.Alter != default)
                    foreach (Dictionary<string, string> alterEntry in rootJson.Alter)
                        SxmSqlStatements.addAlterDefinition(databaseName + "." + alterEntry["Table Name"], alterEntry["Column Name"], alterEntry["Statement"]);

                if (rootJson?.Delete != default)
                    foreach (Dictionary<string, string> deleteEntry in rootJson.Delete)
                        SxmSqlStatements.addDeleteDefinition(deleteEntry["Statement Name"], deleteEntry["Table Name"], deleteEntry["Statement"]);

                if (rootJson?.Update != default)
                    foreach (Dictionary<string, string> updateEntry in rootJson.Update)
                        SxmSqlStatements.addUpdateDefinition(updateEntry["Statement Name"], updateEntry["Table Name"], updateEntry["Statement"]);

                if (rootJson?.Select != default)
                    foreach (Dictionary<string, string> selectEntry in rootJson.Select)
                        SxmSqlStatements.addSelectDefinition(selectEntry["Statement Name"], selectEntry["Table Name"], selectEntry["Statement"]);

                if (rootJson?.Insert != default)
                    foreach (Dictionary<string, string> insertEntry in rootJson.Insert)
                        SxmSqlStatements.addInsertDefinition(insertEntry["Statement Name"], insertEntry["Table Name"], insertEntry["Statement"]);

                if (rootJson?.Trigger != default)
                    foreach (Dictionary<string, string> triggerEntry in rootJson.Trigger)
                        SxmSqlStatements.addTriggerDefinition(databaseName, triggerEntry["Trigger Name"], triggerEntry["Statement"]);
            }
        }

        /// <summary>
        /// Validate and set the file version number. Throws on invalid versions.
        /// </summary>
        /// <param name="version">Numeric version parsed from the file.</param>
        /// <returns>The same version value when successfully set.</returns>
        private static long setVersionNumber(long version)
        {
            if (version < 0)
                throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));

            SxmProcessSQLStatements.setSqlStatementsVersionNumber = version;
            return version;
        }

        /// <summary>
        /// Parse SQL statements from a single combined string using header delimiters.
        /// This method looks for header blocks (database, version, table, etc.) and dispatches to handlers.
        /// </summary>
        /// <param name="sqlStatements">The combined SQL statements file content.</param>
        /// <returns>True when parsing completes successfully (throws on error).</returns>
        public static bool Parse(string sqlStatements)
        {
            int searchOffset = 0;

            while ((searchOffset = getHeader(searchOffset, sqlStatements)) != -1) { }
            return true;
        }

        /// <summary>
        /// Finds the next header delimited by the configured open/close delimiters and parses it.
        /// </summary>
        /// <param name="searchOffset">Position in the string to start searching.</param>
        /// <param name="sqlStatements">The combined SQL statements content.</param>
        /// <returns>The index immediately after the parsed header block, or -1 when none remain.</returns>
        private static int getHeader(int searchOffset, string sqlStatements)
        {
            int index = sqlStatements.IndexOf(SxmDefines.openStatementDelimeter, searchOffset);
            if (index != -1)
            {
                int sIndex = index + 1;
                index = sqlStatements.IndexOf(SxmDefines.closeStatementDelimeter, sIndex);
                if (index != -1)
                {
                    if (sIndex == index)
                        throw new SxmException(SxmErrorMessages.error["missingSQLStatementHeader"]);

                    string header = sqlStatements.Substring(sIndex, index - sIndex).Trim();
                    index = parseHeader(header, index + 1, sqlStatements);
                }
                else
                    throw new SxmException(SxmErrorMessages.error["invalidSQLStatementFile"]);
            }

            return index;
        }

        /// <summary>
        /// Dispatches parsing based on the header string (e.g. "database", "table", "select").
        /// </summary>
        /// <param name="header">Lower-case header token.</param>
        /// <param name="index">Current index in the source string (position after header).</param>
        /// <param name="sqlStatements">The combined SQL statements content.</param>
        /// <returns>Index after the processed header block.</returns>
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

        /// <summary>
        /// Ensure a database name has been parsed before parsing other content.
        /// </summary>
        private static void checkDatabaseName()
        {
            if (string.IsNullOrEmpty(databaseName))
                throw new SxmException(new ErrorMessage("missingDatabaseName", databaseName));
        }

        /// <summary>
        /// Reads the database name from the current position using getCommand and validates it.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="name">Source string containing commands.</param>
        /// <param name="databaseName">Reference to the databaseName field to populate.</param>
        /// <returns>Index position after reading the database name block.</returns>
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

        /// <summary>
        /// Validate the parsed database name for invalid filesystem characters or emptiness.
        /// </summary>
        private static void checkValidDatabaseName()
        {
            char[] pattern = Path.GetInvalidFileNameChars();
            if (databaseName.Any(pattern.Contains) || string.IsNullOrEmpty(databaseName))
                throw new SxmException(new ErrorMessage("invalidDBName", databaseName));
        }

        /// <summary>
        /// Read and parse the version number block from the source. Validates numeric format and non-negative value.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="versionStatement">Source content string.</param>
        /// <returns>Index position after processing the version block.</returns>
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
                    versionNumber = Convert.ToInt32(version);
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

        /// <summary>
        /// Parse consecutive table definition triplets: table name, create statement, synch option.
        /// Adds each table to the SqlStatements registry.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the table block.</returns>
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

                SxmSqlStatements.addTableDefinition(databaseName + "." + tableName, sqlStatement, synch);
            } while (true);

            return index;
        }

        /// <summary>
        /// Parse insert statement blocks of the form: dbName, tableName, sqlStatement.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the insert block.</returns>
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

                SxmSqlStatements.addInsertDefinition(dbName, tableName, sqlStatement);
            } while (true);

            return index;
        }

        /// <summary>
        /// Parse alter statement blocks of the form: columnName, tableName, sqlStatement.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the alter block.</returns>
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

                SxmSqlStatements.addAlterDefinition(databaseName + "." + tableName, columnName, sqlStatement);
            } while (true);

            return index;
        }

        /// <summary>
        /// Parse trigger statement blocks of the form: triggerName, sqlStatement.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the trigger block.</returns>
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

                SxmSqlStatements.addTriggerDefinition(databaseName, triggerName, sqlStatement);
            } while (true);

            return index;
        }

        /// <summary>
        /// Parse index statement blocks of the form: indexName, tableName, sqlStatement.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the index block.</returns>
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

                SxmSqlStatements.addIndexDefinition(databaseName + "." + tableName, indexName, sqlStatement);
            } while (true);

            return index;
        }

        /// <summary>
        /// Generic parser used for select/update/delete statements composed of: statementName, tableName, sqlStatement.
        /// </summary>
        /// <param name="index">Current index in the source string.</param>
        /// <param name="header">Header type ("select", "update", "delete").</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>Index position after the statement block.</returns>
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
                    SxmSqlStatements.addSelectDefinition(sqlName, tableName, sqlStatement);
                if (header.Equals("delete") == true)
                    SxmSqlStatements.addDeleteDefinition(sqlName, tableName, sqlStatement);
                if (header.Equals("update") == true)
                    SxmSqlStatements.addUpdateDefinition(sqlName, tableName, sqlStatement);
            } while (true);

            return index;
        }

        /// <summary>
        /// Retrieve the next delimited command token from the supplied string starting at the specified index.
        /// This function expects tokens to be surrounded by Defines.openStatementDelimeter and Defines.closeStatementDelimeter.
        /// </summary>
        /// <param name="index">Start index for searching the open delimiter.</param>
        /// <param name="sqlStatements">Source content string.</param>
        /// <returns>A CommandReturn containing the parsed token and the index after the close delimiter.</returns>
        private static CommandReturn getCommand(int index, string sqlStatements)
        {
            CommandReturn commandReturn = new CommandReturn();

            index = sqlStatements.IndexOf(SxmDefines.openStatementDelimeter, index);
            if (index != -1)
            {
                int sIndex = index + 1;
                index = sqlStatements.IndexOf(SxmDefines.closeStatementDelimeter, sIndex);
                if (index != -1)
                {
                    if (sIndex != index)
                        commandReturn.command = sqlStatements.Substring(sIndex, index - sIndex).Trim();
                    else
                        commandReturn.command = string.Empty;

                    commandReturn.index = index + 1;
                }
                else
                    throw new SxmException(SxmErrorMessages.error["invalidSQLStatementFile"]);
            }
            else
                throw new SxmException(SxmErrorMessages.error["invalidSQLStatementFile"]);

            return commandReturn;
        }

        /// <summary>
        /// Parse a synch command token (like "synch", "no_synch", "move") and return the corresponding Defines constant.
        /// Throws when the token is unknown.
        /// </summary>
        /// <param name="synchCommand">Lower-cased synch token.</param>
        /// <returns>Integer code representing the synch behavior.</returns>
        private static int parseSynchCommand(string synchCommand)
        {
            if (synchCommand.Equals("synch") == true)
                return SxmDefines.CLOUD_SYNCH;
            if (synchCommand.Equals("no_synch") == true)
                return SxmDefines.NO_CLOUD_SYNCH;
            if (synchCommand.Equals("move") == true)
                return SxmDefines.CLOUD_MOVE;

            throw new SxmException(new ErrorMessage("unknownSynchCommand", synchCommand));
        }

        /// <summary>
        /// Internal helper used to return both a parsed command string and the next index to resume parsing.
        /// </summary>
        class CommandReturn
        {
            /// <summary>
            /// Next index in the source string after the parsed token.
            /// </summary>
            public int index;
            /// <summary>
            /// The parsed token string (empty when token represented termination).
            /// </summary>
            public string command;

            public CommandReturn()
            {
            }
        }
    }

}