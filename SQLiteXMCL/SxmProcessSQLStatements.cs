using System.Text.Json;
using System.Xml.Serialization;
using static SQLiteXM.SxmSerialization;

namespace SQLiteXM
{
    /// <summary>
    /// Parser for SQL statements files. Supports XML and JSON formats and populates the SqlStatements registry.
    /// This class exposes static Parse methods and maintains a version and database name read from the file.
    /// </summary>
    public class SxmProcessSQLStatements
    {
        private SxmProcessSQLStatements() { }

        // Backing field for the currently parsed SQL statements version.
        private static long _versionNumber = 0;

        /// <summary>
        /// Gets the version number found in the last parsed SQL statements file.
        /// </summary>
        internal static long SqlStatementsVersionNumber { get => _versionNumber; }


        // Backing field for the database name identified in the parsed file.
        private static string _databaseName = string.Empty;

        /// <summary>
        /// Gets the database name parsed from the last SQL statements file.
        /// </summary>
        internal static string DatabaseName { get => _databaseName; }


        // Backing field for IsDefaultDatabase flag.
        private static bool _isDefaultDatabase = false;

        /// <summary>
        /// Gets the defaultdatabase flag.
        /// </summary>
        internal static bool IsDefaultDatabase { get => _isDefaultDatabase; }


        /// <summary>
        /// Parse SQL statements from a stream that contains either a JSON or XML SQL-statements file and populate
        /// the internal <see cref="SxmSqlStatements"/> registry.
        /// </summary>
        /// <param name="sqlStatementAssets">Stream that contains the JSON or XML document. Must not be null.</param>
        /// <param name="sqlStatementsFileType">
        /// The expected file type of the stream. When set to <see cref="SxmDefines.SqlStatementsFileType.Unknown"/>
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
        /// - When <see cref="SxmDefines.SqlStatementsFileType.Unknown"/> a small heuristic (first non-whitespace character)
        ///   is used to prefer XML ('&lt;') or JSON ('{' or '[') before falling back to the other parser.
        /// - This method mutates static fields (for example <c>databaseName</c> and <c>versionNumber</c>) and is not thread-safe.
        ///   Callers should synchronize if concurrent parses are possible.
        /// </remarks>
        internal static bool Parse(Stream sqlStatementAssets, SxmDefines.SqlStatementsFileType sqlStatementsFileType)
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
                    ProcessJson(rootJson);
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
                        ProcessXml(rootXml);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    xmlEx = ex;
                    return false;
                }
            }

            if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.Json)
            {
                if (!TryParseJson())
                    throw jsonEx ?? new ArgumentException("Invalid JSON SQL statements file.");
                return true;
            }

            if (sqlStatementsFileType == SxmDefines.SqlStatementsFileType.Xml)
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
        /// Process a deserialized JSON root object and register each found SQL definition into SqlStatements.
        /// JSON uses dictionary entries so keys must match expected textual keys (e.g. "Table Name").
        /// </summary>
        /// <param name="rootJson">Deserialized JSON root object (may be null).</param>
        private static void ProcessJson(RootJson? rootJson)
        {
            if (rootJson != default)
            {
                SetDatabaseName(rootJson.database.Trim());
                SetIsDefault(rootJson.isDefault);
                SetVersionNumber(rootJson.version);

                if (rootJson?.Table != default)
                    foreach (Dictionary<string, string> tableEntry in rootJson.Table)
                        SxmSqlStatements.AddTableDefinition(_databaseName + "." + tableEntry["Table Name"], tableEntry["Statement"]);

                if (rootJson?.Index != default)
                    foreach (Dictionary<string, string> indexEntry in rootJson.Index)
                        SxmSqlStatements.AddIndexDefinition(_databaseName + "." + indexEntry["Table Name"], indexEntry["Index Name"], indexEntry["Statement"]);

                if (rootJson?.Alter != default)
                    foreach (Dictionary<string, string> alterEntry in rootJson.Alter)
                        SxmSqlStatements.AddAlterDefinition(_databaseName + "." + alterEntry["Table Name"], alterEntry["Column Name"], alterEntry["Statement"]);

                if (rootJson?.Delete != default)
                    foreach (Dictionary<string, string> deleteEntry in rootJson.Delete)
                        SxmSqlStatements.AddDeleteDefinition(deleteEntry["Statement Name"], deleteEntry["Table Name"], deleteEntry["Statement"]);

                if (rootJson?.Update != default)
                    foreach (Dictionary<string, string> updateEntry in rootJson.Update)
                        SxmSqlStatements.AddUpdateDefinition(updateEntry["Statement Name"], updateEntry["Table Name"], updateEntry["Statement"]);

                if (rootJson?.Select != default)
                    foreach (Dictionary<string, string> selectEntry in rootJson.Select)
                        SxmSqlStatements.AddSelectDefinition(selectEntry["Statement Name"], selectEntry["Table Name"], selectEntry["Statement"]);

                if (rootJson?.Insert != default)
                    foreach (Dictionary<string, string> insertEntry in rootJson.Insert)
                        SxmSqlStatements.AddInsertDefinition(insertEntry["Statement Name"], insertEntry["Table Name"], insertEntry["Statement"]);

                if (rootJson?.Trigger != default)
                    foreach (Dictionary<string, string> triggerEntry in rootJson.Trigger)
                        SxmSqlStatements.AddTriggerDefinition(_databaseName, triggerEntry["Trigger Name"], triggerEntry["Statement"]);
            }
        }

        /// <summary>
        /// Process a deserialized XML root object and register each found SQL definition into SqlStatements.
        /// </summary>
        /// <param name="rootXml">Deserialized XML root object (may be null).</param>
        private static void ProcessXml(RootXml? rootXml)
        {
            if (rootXml != default)
            {
                SetDatabaseName(rootXml.Database.Trim());
                SetIsDefault(rootXml.IsDefault);
                SetVersionNumber(rootXml.Version);

                if (rootXml?.Table != default)
                    foreach (Table tableEntry in rootXml.Table)
                        SxmSqlStatements.AddTableDefinition(_databaseName + "." + tableEntry.TableName, tableEntry.Statement);

                if (rootXml?.Index != default)
                    foreach (SQLiteXM.SxmSerialization.Index indexEntry in rootXml.Index)
                        SxmSqlStatements.AddIndexDefinition(_databaseName + "." + indexEntry.TableName, indexEntry.IndexName, indexEntry.Statement);

                if (rootXml?.Alter != default)
                    foreach (Alter alterEntry in rootXml.Alter)
                        SxmSqlStatements.AddAlterDefinition(_databaseName + "." + alterEntry.TableName, alterEntry.ColumnName, alterEntry.Statement);

                if (rootXml?.Delete != default)
                    foreach (Delete deleteEntry in rootXml.Delete)
                        SxmSqlStatements.AddDeleteDefinition(deleteEntry.StatementName, deleteEntry.TableName, deleteEntry.Statement);

                if (rootXml?.Update != default)
                    foreach (Update updateEntry in rootXml.Update)
                        SxmSqlStatements.AddUpdateDefinition(updateEntry.StatementName, updateEntry.TableName, updateEntry.Statement);

                if (rootXml?.Select != default)
                    foreach (Select selectEntry in rootXml.Select)
                        SxmSqlStatements.AddSelectDefinition(selectEntry.StatementName, selectEntry.TableName, selectEntry.Statement);

                if (rootXml?.Insert != default)
                    foreach (Insert insertEntry in rootXml.Insert)
                        SxmSqlStatements.AddInsertDefinition(insertEntry.StatementName, insertEntry.TableName, insertEntry.Statement);

                if (rootXml?.Trigger != default)
                    foreach (Trigger triggerEntry in rootXml.Trigger)
                        SxmSqlStatements.AddTriggerDefinition(_databaseName, triggerEntry.TriggerName, triggerEntry.Statement);
            }
        }

        /// <summary>
        /// Validate the parsed database name for invalid filesystem characters or emptiness.
        /// </summary>
        private static void SetDatabaseName(string databaseName)
        {
            char[] pattern = Path.GetInvalidFileNameChars();

            if (string.IsNullOrEmpty(databaseName) || databaseName.Any(pattern.Contains) || databaseName.ToLower().Equals("main") || databaseName.ToLower().Equals("temp"))
                throw new SxmException(new ErrorMessage("Invalid datanase name. The databse name may not contain invalid characters or be named 'main' or 'temp'.", databaseName));

            SxmProcessSQLStatements._databaseName = databaseName;
        }

        private static void SetIsDefault(bool isDefault)
        {
            SxmProcessSQLStatements._isDefaultDatabase = isDefault;
        }

        /// <summary>
        /// Validate and set the file version number. Throws on invalid versions.
        /// </summary>
        /// <param name="version">Numeric version parsed from the file.</param>
        /// <returns>The same version value when successfully set.</returns>
        private static void SetVersionNumber(long version)
        {
            if (version < 0)
                throw new SxmException(new ErrorMessage("Improperly formatted database version number. The version number must be a non-negative whole number.", version));

            SxmProcessSQLStatements._versionNumber = version;
        }

        /// <summary>
        /// Parse a synch command token (like "synch", "no_synch", "move") and return the corresponding Defines constant.
        /// Throws when the token is unknown.
        /// </summary>
        /// <param name="synchCommand">Lower-cased synch token.</param>
        /// <returns>Integer code representing the synch behavior.</returns>
        private static int ParseSynchCommand(string synchCommand)
        {
            if (synchCommand.Equals("synch") == true)
                return SxmDefines.CLOUD_SYNCH;
            if (synchCommand.Equals("no_synch") == true)
                return SxmDefines.NO_CLOUD_SYNCH;
            if (synchCommand.Equals("move") == true)
                return SxmDefines.CLOUD_MOVE;

            throw new SxmException(new ErrorMessage("unknownSynchCommand", synchCommand));
        }
    }
}