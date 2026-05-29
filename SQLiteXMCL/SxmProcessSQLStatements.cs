using System.Text.Json;
using System.Xml.Linq;
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


        // Backing field for all database names identified in the parsed file.
        private static List<string> _databases = new();

        /// <summary>
        /// Gets the list of all database names parsed from the last SQL statements file.
        /// </summary>
        internal static IReadOnlyList<string> Databases { get => _databases.AsReadOnly(); }


        // Backing field for the default database name identified in the parsed file.
        private static string _defaultDatabaseName = string.Empty;

        /// <summary>
        /// Gets the default database name parsed from the last SQL statements file.
        /// </summary>
        internal static string DefaultDatabaseName { get => _defaultDatabaseName; }


        /// <summary>
        /// Checks if the given database name is the default database.
        /// </summary>
        internal static bool IsDefaultDatabase(string t) => string.Equals(t, _defaultDatabaseName, StringComparison.OrdinalIgnoreCase);

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
                    if (rootJson == null)
                    {
                        jsonEx = new ArgumentException("JSON content for SQL statements file deserialized to null.");
                        return false;
                    }
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
                        if (rootXml == null)
                        {
                            xmlEx = new ArgumentException("XML content for SQL statements file deserialized to null.");
                            return false;
                        }
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
                SetVersionNumber(rootJson.version);

                // Parse databases array
                if (rootJson.databases == null || rootJson.databases.Count == 0)
                {
                    throw new ArgumentException("REQUIRED FIELD MISSING: SqlStatements file must contain a 'databases' array with at least one database definition.");
                }

                ProcessDatabases(rootJson.databases);

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
                {
                    foreach (Dictionary<string, string> triggerEntry in rootJson.Trigger)
                    {
                        // Database field is required for triggers
                        if (!triggerEntry.TryGetValue("Database", out string? triggerDatabase) || string.IsNullOrWhiteSpace(triggerDatabase))
                        {
                            throw new ArgumentException(
                                "REQUIRED FIELD MISSING: Each trigger entry must specify a 'Database' field.\n" +
                                $"Trigger for table '{triggerEntry.GetValueOrDefault("Table Name", "[unknown]")}' is missing the required 'Database' field.\n" +
                                "SOLUTION: Add a \"Database\": \"<database-name>\" field to each trigger entry in your SqlStatements file.\n" +
                                "EXAMPLE:\n" +
                                "  \"trigger\": [\n" +
                                "    {\n" +
                                "      \"Database\": \"sqlitexmtest\",\n" +
                                "      \"Table Name\": \"user\",\n" +
                                "      \"Statement\": \"CREATE TRIGGER ...\"\n" +
                                "    }\n" +
                                "  ]");
                        }

                        // Validate that trigger database references a defined database
                        if (!_databases.Contains(triggerDatabase, StringComparer.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException(
                                $"TRIGGER DATABASE MISMATCH: Trigger references database '{triggerDatabase}' which is not defined in the 'databases' array.\n" +
                                $"Defined databases: {string.Join(", ", _databases)}\n" +
                                $"Trigger table: {triggerEntry.GetValueOrDefault("Table Name", "[unknown]")}");
                        }

                        if (!triggerEntry.TryGetValue("Table Name", out string? tableName) || string.IsNullOrWhiteSpace(tableName))
                        {
                            throw new ArgumentException(
                                $"REQUIRED FIELD MISSING: Trigger for database '{triggerDatabase}' is missing the required 'Table Name' field.");
                        }

                        if (!triggerEntry.TryGetValue("Statement", out string? statement) || string.IsNullOrWhiteSpace(statement))
                        {
                            throw new ArgumentException(
                                $"REQUIRED FIELD MISSING: Trigger for database '{triggerDatabase}', table '{tableName}' is missing the required 'Statement' field.");
                        }

                        SxmSqlStatements.AddTriggerDefinition(triggerDatabase, tableName, statement);
                    }
                }
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
                SetVersionNumber(rootXml.Version);

                // Parse databases list
                if (rootXml.Databases == null || rootXml.Databases.Count == 0)
                {
                    throw new ArgumentException("REQUIRED FIELD MISSING: SqlStatements file must contain a 'Databases' list with at least one database definition.");
                }

                ProcessDatabasesXml(rootXml.Databases);

                if (rootXml?.Delete != default)
                    foreach (Delete deleteEntry in rootXml.Delete)
                        SxmSqlStatements.AddDeleteDefinition(deleteEntry.StatementName!, deleteEntry.TableName!, deleteEntry.Statement!);

                if (rootXml?.Update != default)
                    foreach (Update updateEntry in rootXml.Update)
                        SxmSqlStatements.AddUpdateDefinition(updateEntry.StatementName!, updateEntry.TableName!, updateEntry.Statement!);

                if (rootXml?.Select != default)
                    foreach (Select selectEntry in rootXml.Select)
                        SxmSqlStatements.AddSelectDefinition(selectEntry.StatementName!, selectEntry.TableName!, selectEntry.Statement!);

                if (rootXml?.Insert != default)
                    foreach (Insert insertEntry in rootXml.Insert)
                        SxmSqlStatements.AddInsertDefinition(insertEntry.StatementName!, insertEntry.TableName!, insertEntry.Statement!);

                if (rootXml?.Trigger != default)
                {
                    foreach (SxmSerialization.Trigger triggerEntry in rootXml.Trigger)
                    {
                        // Database field is required for triggers
                        if (string.IsNullOrWhiteSpace(triggerEntry.Database))
                        {
                            throw new ArgumentException(
                                "REQUIRED FIELD MISSING: Each trigger entry must specify a 'Database' field.\n" +
                                $"Trigger for table '{triggerEntry.TableName ?? "[unknown]"}' is missing the required 'Database' field.\n" +
                                "SOLUTION: Add a <Database>database-name</Database> element to each trigger entry in your SqlStatements file.\n" +
                                "EXAMPLE:\n" +
                                "  <trigger>\n" +
                                "    <Database>sqlitexmtest</Database>\n" +
                                "    <TableName>user</TableName>\n" +
                                "    <Statement>CREATE TRIGGER ...</Statement>\n" +
                                "  </trigger>");
                        }

                        // Validate that trigger database references a defined database
                        if (!_databases.Contains(triggerEntry.Database, StringComparer.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException(
                                $"TRIGGER DATABASE MISMATCH: Trigger references database '{triggerEntry.Database}' which is not defined in the 'Databases' list.\n" +
                                $"Defined databases: {string.Join(", ", _databases)}\n" +
                                $"Trigger table: {triggerEntry.TableName ?? "[unknown]"}");
                        }

                        if (string.IsNullOrWhiteSpace(triggerEntry.TableName))
                        {
                            throw new ArgumentException(
                                $"REQUIRED FIELD MISSING: Trigger for database '{triggerEntry.Database}' is missing the required 'TableName' field.");
                        }

                        if (string.IsNullOrWhiteSpace(triggerEntry.Statement))
                        {
                            throw new ArgumentException(
                                $"REQUIRED FIELD MISSING: Trigger for database '{triggerEntry.Database}', table '{triggerEntry.TableName}' is missing the required 'Statement' field.");
                        }

                        SxmSqlStatements.AddTriggerDefinition(triggerEntry.Database, triggerEntry.TableName, triggerEntry.Statement);
                    }
                }
            }
        }

        /// <summary>
        /// Process the databases array from JSON and populate _databases list and _databaseName (default).
        /// </summary>
        private static void ProcessDatabases(List<Dictionary<string, object>> databases)
        {
            _databases.Clear();
            _defaultDatabaseName = string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            bool foundDefault = false;

            foreach (var dbDict in databases)
            {
                if (!dbDict.TryGetValue("database", out var nameObj) || nameObj == null)
                {
                    throw new ArgumentException("Each database entry must have a 'database' field.");
                }

                string? dbName = nameObj.ToString()?.Trim();
                if (string.IsNullOrEmpty(dbName))
                {
                    throw new ArgumentException("Database name cannot be empty.");
                }

                // Validate database name
                if (dbName.Any(invalidChars.Contains)
                    || dbName.Equals("main", StringComparison.OrdinalIgnoreCase)
                    || dbName.Equals("temp", StringComparison.OrdinalIgnoreCase))
                {
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.InvalidDBName, dbName));
                }

                _databases.Add(dbName);

                // Check if this is the default database
                bool isDefault = false;
                if (dbDict.TryGetValue("isDefault", out var defaultObj) && defaultObj != null)
                {
                    // Handle JsonElement from System.Text.Json
                    if (defaultObj is System.Text.Json.JsonElement jsonElement)
                    {
                        isDefault = jsonElement.ValueKind == System.Text.Json.JsonValueKind.True;
                    }
                    else
                    {
                        isDefault = Convert.ToBoolean(defaultObj);
                    }
                }

                if (isDefault)
                {
                    if (foundDefault)
                    {
                        throw new ArgumentException($"Multiple databases marked as default. Only one database can have 'isDefault: true'. Duplicate found at: {dbName}");
                    }
                    _defaultDatabaseName = dbName;
                    foundDefault = true;
                }
            }

            // If no default was specified, throw error (user must explicitly mark one as default)
            if (!foundDefault)
            {
                throw new ArgumentException("No default database specified. At least one database must have 'isDefault: true'.");
            }
        }

        /// <summary>
        /// Process the databases list from XML and populate _databases list and _databaseName (default).
        /// </summary>
        private static void ProcessDatabasesXml(List<SxmSerialization.Database> databases)
        {
            _databases.Clear();
            _defaultDatabaseName = string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            bool foundDefault = false;

            foreach (var db in databases)
            {
                string? dbName = db.database?.Trim();
                if (string.IsNullOrEmpty(dbName))
                {
                    throw new ArgumentException("Database name cannot be empty.");
                }

                // Validate database name
                if (dbName.Any(invalidChars.Contains)
                    || dbName.Equals("main", StringComparison.OrdinalIgnoreCase)
                    || dbName.Equals("temp", StringComparison.OrdinalIgnoreCase))
                {
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.InvalidDBName, dbName));
                }

                _databases.Add(dbName);

                if (db.isDefault)
                {
                    if (foundDefault)
                    {
                        throw new ArgumentException($"Multiple databases marked as default. Only one database can have 'isDefault: true'. Duplicate found at: {dbName}");
                    }
                    _defaultDatabaseName = dbName;
                    foundDefault = true;
                }
            }

            // If no default was specified, throw error (user must explicitly mark one as default)
            if (!foundDefault)
            {
                throw new ArgumentException("No default database specified. At least one database must have 'isDefault: true'.");
            }
        }

        /// <summary>
        /// Validate and set the file version number. Throws on invalid versions.
        /// </summary>
        /// <param name="version">Numeric version parsed from the file.</param>
        /// <returns>The same version value when successfully set.</returns>
        private static void SetVersionNumber(long version)
        {
            if (version < 0)
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.DbVersionFormatError, version));

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
                return SxmDefines.CloudSync;
            if (synchCommand.Equals("no_synch") == true)
                return SxmDefines.NoCloudSync;
            if (synchCommand.Equals("move") == true)
                return SxmDefines.CloudMove;

            throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSynchCommand, synchCommand));
        }
    }
}