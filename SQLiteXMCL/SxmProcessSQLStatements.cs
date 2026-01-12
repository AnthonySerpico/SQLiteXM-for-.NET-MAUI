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
        /// Parse SQL statements from a stream that contains either a JSON or XML SQL-statements file and populate
        /// the internal <see cref="SxmSqlStatements"/> registry.
        /// </summary>
        /// <param name="sqlStatementAssets">Stream that contains the JSON or XML document. Must not be null.</param>
        /// <param name="sqlStatementsFileType">
        /// The expected file type of the stream. When set to <see cref="SxmDefines.SqlStatementsFileType.unknown"/>
        /// the method attempts format detection and will try JSON and XML parsing heuristically.
        /// </param>
        /// <returns>True when parsing completes successfully (throws on error).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sqlStatementAssets"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the content cannot be parsed as the explicitly requested format, or when autodetection
        /// fails to identify a valid JSON or XML document.
        /// </exception>
        /// <exception cref="AggregateException">
        /// When autodetection is used and both JSON and XML parsing fail, an <see cref="AggregateException"/>
        /// is thrown containing both parser exceptions as inner exceptions for diagnostics.
        /// </exception>
        /// <remarks>
        /// Implementation notes:
        /// - The method reads the stream into a string once so both parsers can be attempted safely (avoids stream-position/EOF issues).
        /// - JSON deserialization is performed with case-insensitive property name matching to be more tolerant of input.
        /// - XML deserialization uses an <see cref="System.Xml.XmlReader"/> with DTD processing prohibited to reduce attack surface (XXE).
        /// - If the caller supplies an explicit format (json or xml) only that parser is attempted and its exception is propagated.
        /// - When <see cref="SxmDefines.SqlStatementsFileType.unknown"/> a small heuristic (first non-whitespace character)
        ///   is used to prefer XML ('&lt;') or JSON ('{' or '[') before falling back to the other parser.
        /// - This method mutates static fields (for example <c>databaseName</c> and <c>versionNumber</c>) and is not thread-safe.
        ///   Callers should synchronize if concurrent parses are possible.
        /// </remarks>
        public static bool Parse(Stream sqlStatementAssets, SxmDefines.SqlStatementsFileType sqlStatementsFileType)
        {
            if (sqlStatementAssets == null)
                throw new ArgumentNullException(nameof(sqlStatementAssets));

            if (sqlStatementAssets.CanSeek)
                sqlStatementAssets.Seek(0, System.IO.SeekOrigin.Begin);

            string content;
            using (var reader = new System.IO.StreamReader(sqlStatementAssets, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            {
                content = reader.ReadToEnd();
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            Exception? jsonEx = null;
            Exception? xmlEx = null;

            bool TryParseJson()
            {
                try
                {
                    RootJson? rootJson = JsonSerializer.Deserialize<RootJson>(content, jsonOptions);
                    processJson(rootJson);
                    return true;
                }
                catch (Exception ex)
                {
                    jsonEx = ex;
                    return false;
                }
            }

            bool TryParseXml()
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(RootXml));
                    var settings = new System.Xml.XmlReaderSettings
                    {
                        DtdProcessing = System.Xml.DtdProcessing.Prohibit
                    };

                    using (var sr = new System.IO.StringReader(content))
                    using (var xr = System.Xml.XmlReader.Create(sr, settings))
                    {
                        RootXml? rootXml = (RootXml?)serializer.Deserialize(xr);
                        processXml(rootXml);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    xmlEx = ex;
                    return false;
                }
            }

            if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.json)
            {
                if (!TryParseJson())
                    throw jsonEx ?? new ArgumentException("Invalid JSON SQL statements file.");
                return true;
            }

            if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.xml)
            {
                if (!TryParseXml())
                    throw xmlEx ?? new ArgumentException("Invalid XML SQL statements file.");
                return true;
            }

            // unknown: prefer a heuristic, then fall back
            char firstNonWs = '\0';
            for (int i = 0; i < content.Length; i++)
            {
                if (!char.IsWhiteSpace(content[i]))
                {
                    firstNonWs = content[i];
                    break;
                }
            }

            if (firstNonWs == '<')
            {
                if (TryParseXml()) return true;
                if (TryParseJson()) return true;
            }
            else if (firstNonWs == '{' || firstNonWs == '[')
            {
                if (TryParseJson()) return true;
                if (TryParseXml()) return true;
            }
            else
            {
                if (TryParseJson()) return true;
                if (TryParseXml()) return true;
            }

            throw new ArgumentException(
                "Invalid SQL statements file. The SQL statements file must be valid JSON or XML.",
                new AggregateException(
                    jsonEx ?? new Exception("JSON parse failed."),
                    xmlEx ?? new Exception("XML parse failed.")
                )
            );
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
        /// Validate the parsed database name for invalid filesystem characters or emptiness.
        /// </summary>
        private static void checkValidDatabaseName()
        {
            char[] pattern = Path.GetInvalidFileNameChars();
            if (databaseName.Any(pattern.Contains) || string.IsNullOrEmpty(databaseName))
                throw new SxmException(new ErrorMessage("invalidDBName", databaseName));
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