using SQLiteXM.Internal;
using System.Collections;
using System.Collections.Concurrent;
using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;
using static SQLiteXM.SxmDefines;
//using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;

namespace SQLiteXM
{
    /// <summary>
    /// Provides database initialization helpers used by the SQLiteXM library.
    /// </summary>
    /// <remarks>
    /// This class is responsible for parsing SQL statement definition files, creating and updating
    /// database schema objects (tables, indexes, triggers), and maintaining the stored SQL statements
    /// version number in the database (PRAGMA user_version).
    /// </remarks>
    public class SxmInit 
    {
        /// <summary>
        /// Cache mapping table name -> (column name -> column type) using thread-safe concurrent dictionaries.
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> columnNameTypes = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.Ordinal);

        private SxmInit() { }

        /// <summary>
        /// Initialize the database using SQL statements parsed from the specified file.
        /// </summary>
        /// <param name="SqlStatementsFileName">Path to the SQL statements file (absolute or relative).</param>
        /// <param name="fileType">Format of the SQL statements file.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        public static async Task initDB(string SqlStatementsFileName)
        {
            string fullPathToSqlStatementsFile = resolveSqlStatementsFile(SqlStatementsFileName);
            SqlStatementsFileType fileType = SxmHelpers.GetSqlStatementsFileType(fullPathToSqlStatementsFile);
            if (fileType == SqlStatementsFileType.unknown)
                throw new ArgumentException(
                    $"'{SqlStatementsFileName}' is an unknown SQL statements file type. The SQL statements file must be JSON or XML.");

            {
                using var stream = File.OpenRead(fullPathToSqlStatementsFile);
                await parseSqlStatementsFile(stream, fileType).CAF();
            }

            await SxmInit.initialize();
        }

        /// <summary>
        /// Initialize the database using SQL statements parsed from the provided stream.
        /// </summary>
        /// <param name="stream">Open, readable stream containing SQL statement definitions.</param>
        /// <param name="fileType">Format of the SQL statements contained in the stream.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        public static async Task initDB(Stream stream)
        {
            await parseSqlStatementsFile(stream, SqlStatementsFileType.unknown).CAF();
            await SxmInit.initialize();
        }

        /// <summary>
        /// Parse SQL statement definitions from a file on disk.
        /// </summary>
        /// <param name="fileName">
        /// Absolute or relative path to the SQL definition file. Relative paths are resolved against <see cref="AppContext.BaseDirectory"/>.
        /// </param>
        /// <param name="fileType">The format of the SQL definitions (json, xml, or txt).</param>
        /// <exception cref="ArgumentNullException">fileName is null or whitespace.</exception>
        /// <exception cref="FileNotFoundException">The resolved file cannot be found.</exception>
        private static string resolveSqlStatementsFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string fullPath = ResolveToBase(fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"The SQL statements file '{fileName}' could not be found. Full path: {fullPath}");

            return fullPath;
        }

        /// <summary>
        /// Parse SQL statement definitions from an open, readable stream.
        /// </summary>
        /// <param name="stream">An open, readable stream positioned at the beginning of the SQL definitions.</param>
        /// <param name="fileType">The format of the SQL definitions (json, xml, or txt).</param>
        /// <exception cref="ArgumentNullException">stream is null.</exception>
        /// <exception cref="ArgumentException">stream is not readable.</exception>
        private static Task parseSqlStatementsFile(Stream stream, SxmDefines.SqlStatementsFileType fileType)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

            switch (fileType)
            {
                case SxmDefines.SqlStatementsFileType.json:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.json);
                    break;

                case SxmDefines.SqlStatementsFileType.xml:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.xml);
                    break;

                case SxmDefines.SqlStatementsFileType.unknown:
                default:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.unknown);
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Resolves a relative path against the application's base directory.
        /// </summary>
        /// <param name="path">Input path to resolve.</param>
        /// <returns>Absolute path if <paramref name="path"/> is relative; otherwise returns the original path.</returns>
        private static string ResolveToBase(string path) => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

        /*private static async Task parseSqlStatementsFile(string SqlStatementsFileName)
        {
            if (await FileSystem.AppPackageFileExistsAsync(SqlStatementsFileName).CAF())
            {
                using (Stream stream = await FileSystem.OpenAppPackageFileAsync(SqlStatementsFileName).CAF())
                {
                    string sqlStatemenstFileExtenzion = Path.GetExtension(SqlStatementsFileName).ToLower();

                    if (sqlStatemenstFileExtenzion.Equals(".json"))
                    {
                        ProcessSQLStatements.Parse(stream, Defines.SqlStatementsFileType.json);
                    }
                    else
                    {
                        if (sqlStatemenstFileExtenzion.Equals(".txt"))
                        {
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                ProcessSQLStatements.Parse(reader);
                            }
                        }
                        else
                        {
                            if (sqlStatemenstFileExtenzion.Equals(".xml"))
                                ProcessSQLStatements.Parse(stream, Defines.SqlStatementsFileType.xml);
                        }
                    }
                }
            }
            else
                throw new FileNotFoundException(string.Format("The SQL statements file {0} could not be found.", SqlStatementsFileName));
        }*/

        /// <summary>
        /// Initialize the database schema and auxiliary structures using the currently parsed SQL statements.
        /// </summary>
        /// <returns>True on success.</returns>
        private static async Task<bool> initialize()
        {
            // At this point, the SQL statements file has been parsed, the database name, default status, and version have been loaded into SxmProcessSQLStatements.
            new SxmDatabaseDescriptor();
            string databaseName = SxmProcessSQLStatements.getDatabaseName;

            try
            {
                long sqlStatementsVersionNumber = SxmProcessSQLStatements.SqlStatementsVersionNumber;  // The value in the current SQL statements file.
                long currentDbVersionNumber = await getDbVersionNumber(databaseName);
                //setJournalMode(databaseName);

                if (sqlStatementsVersionNumber > currentDbVersionNumber || sqlStatementsVersionNumber == 0)
                {
                    await createSystemTables(databaseName);

                    if (SxmSqlStatements.tableCreateStatements != default(Dictionary<string, TableDefinition>))
                    {
                        Hashtable connectionMap = new();
                        Hashtable tableNamesMap = new();

                        foreach (string key in SxmSqlStatements.tableCreateStatements.Keys) // the 'key' string value is 'DatabaseName.TableName'
                        {
                            if (key.Split('.').Length != 2)
                                throw new SxmException(new ErrorMessage("invalidTableName", key));

                            if (!await doesTableExist(key, connectionMap, tableNamesMap))
                            {
                                TableDefinition tableDefinition = SxmSqlStatements.tableCreateStatements[key] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("CREATE ", true, null) == true)
                                    await applyCreateTableStatement(key, connectionMap, tableDefinition);
                            }
                            else
                            {
                                TableDefinition tableDefinition = SxmSqlStatements.tableCreateStatements[key] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("DROP ", true, null) == true)
                                    await applyDropTableStatement(key, connectionMap, tableDefinition);
                                else
                                {
                                    await applyAlterTableStatements(key, connectionMap);
                                    await applyIndexTableStatements(key, connectionMap);
                                }
                            }
                        }
                    }

                    await applyTriggerTableStatements(databaseName);
                    await dropUnusedEntities(databaseName);
                    await storeDbVersionNumber(sqlStatementsVersionNumber, databaseName);
                    await SxmAssociationMapper.InitializeAssociations();  // Calling this here might be an error when supporting multiple databases.
                                                                          // The call might need to be made after ALL databases are initialized.
                }
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }
            finally
            {
                SxmSqlStatements.clearStatementTables();
            }

            return true;
        }

        /// <summary>
        /// Drops tables for all types that implement <see cref="ISxmDropEntity"/>.
        /// </summary>
        /// <param name="databaseName">Optional database name to operate on. If null the implicit database is used.</param>
        /// <returns>A task that completes when all drop operations have finished.</returns>
        private static async Task dropUnusedEntities(string databaseName)
        {
            try
            {
                // Collect all non-abstract classes that implement ISxmDropEntity across loaded assemblies.
                var dropTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch { return Array.Empty<Type>(); } // skip assemblies we can't reflect over
                    }).Where(t => t.IsClass && !t.IsAbstract && typeof(ISxmDropEntity).IsAssignableFrom(t)).ToList();

                if (dropTypes.Count == 0)
                    return;

                SxmConnection sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    foreach (var type in dropTypes)
                    {
                        string tableName = type.Name;
                        string dropSql = $"DROP TABLE IF EXISTS {SxmHelpers.QuoteIdentifier(tableName)}";

                        try
                        {
                            // Use a shared connection/transaction for each drop to avoid interfering with shared state.
                            await sxmTransaction.executeTableStatementAsync(dropSql).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Best-effort logging - do not allow one failure to stop remaining drops.
                            try { SxmLogging.log(databaseName, ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
                        }
                    }

                    await sxmTransaction.commitTransactionAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                try { SxmLogging.log(databaseName, ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString()); } catch { }
            }
        }

        private static async Task createSystemTables(string databaseName)
        {
            SxmConnection sxmConnection = new SxmConnection(databaseName);
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
            {
                if (!await doesTableExist("_systemCloudSynchDescriptor", sxmConnection))
                    await CreateCloudSyncDescriptor(sxmTransaction);

                if (!await doesTableExist("_systemCloudSynch", sxmConnection))
                    await createCloudSynchTable(sxmTransaction);

                await sxmTransaction.commitTransactionAsync();
            }
        }

        /*        public static void interruptSynchronize (string databaseName)
                {
                    Synchronize synchronize = null;
                    if (synchronized.TryGetValue (databaseName, out synchronize) == true)
                        synchronize.interruptSynchThread ();
                }

                public static bool getSynchMonitor(string databaseName, int millisecondsTimeout)
                {
                    Synchronize synchronize = null;
                    if (synchronized.TryGetValue (databaseName, out synchronize) == true)
                        return synchronize.getSynchMonitor (millisecondsTimeout);
                    else
                        return true;
                }

                public static void releaseSynchMonitor(string databaseName)
                {
                    Synchronize synchronize = null;
                    if (synchronized.TryGetValue (databaseName, out synchronize) == true)
                        synchronize.releaseSynchMonitor ();
                }
        */

        /// <summary>
        /// Retrieves the numeric database schema version stored in PRAGMA user_version.
        /// </summary>
        /// <returns>The stored user_version value, or -1 on error.</returns>
        public static async Task<long> getDbVersionNumber(string databaseName)
        {
            long versionNumber = -1;

            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync("PRAGMA user_version", default(List<object>));

                    if (sxmConnection.nextRow() == true)
                    {
                        versionNumber = (long)sxmConnection.getValue("user_version");
                    }
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }

            return versionNumber;
        }

        /// <summary>
        /// Attempts to set recommended journal and synchronous PRAGMA settings for the database.
        /// </summary>
        /// <returns>A task that completes when the PRAGMA settings have been applied (errors are swallowed).</returns>
        public static async Task setJournalMode(string databaseName)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync("PRAGMA journal_mode=WAL", default(List<object>));
                    await sxmConnection.executeQueryAsync("PRAGMA synchronous=NORMAL", default(List<object>));
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        /// <summary>
        /// Reset the stored database version number (PRAGMA user_version = 0).
        /// </summary>
        /// <returns>A task that completes when the operation finishes.</returns>
        public static async Task deleteDbVersionNumber(string databaseName)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync("PRAGMA user_version = 0", default(List<object>));
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        /// <summary>
        /// Store the supplied version into PRAGMA user_version.
        /// </summary>
        /// <param name="versionNumber">Version number to store.</param>
        /// <returns>A task that completes when the PRAGMA has been set.</returns>
        private static async Task storeDbVersionNumber(long versionNumber, string databaseName)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync(String.Format("PRAGMA user_version = {0}", versionNumber), default(List<object>));
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        /// <summary>
        /// Execute a CREATE TABLE statement and ensure related synchronization descriptors and triggers exist.
        /// </summary>
        /// <param name="key">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection.</param>
        /// <param name="tableDefinition">Table definition containing DDL and flags.</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        private static async Task applyCreateTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition)
        {
            SxmConnection? sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                sxmConnection = connectionMap[databaseName] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    connectionMap.Add(databaseName, sxmConnection);
                }

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmTransaction.executeTableStatementAsync(tableDefinition.TableSQL);
                    await addSynchID(parts, sxmTransaction);

                    await insertIntoSystemCloudSyncDescriptor(key, databaseName, tableName, sxmTransaction);

                    await createCloudSynchTriggers(key, sxmTransaction);
                    await sxmTransaction.commitTransactionAsync();
                }

                await applyIndexTableStatements(key, connectionMap);
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                if (sxmConnection != null)
                    sxmConnection.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());

                throw new SxmException(ex);
            }
        }

        /// <summary>
        /// Create a single table by name in the given database using SQL statements parsed earlier.
        /// </summary>
        /// <param name="databaseName">Name of the database to operate on.</param>
        /// <param name="tableName">Name of the table to create.</param>
        internal static async Task createTable(string? databaseName, string tableName)
        {
            if (databaseName == null)
                return;

            string[] parts = { databaseName, tableName };
            string key = string.Format("{0}.{1}", databaseName, tableName);

            SxmConnection? sxmConnection = default(SxmConnection);

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                if (!await doesTableExist(tableName, sxmConnection))
                {
                    Hashtable tableNamesMap = new Hashtable();
                    TableDefinition? tableDefinition = SxmSqlStatements.tableCreateStatements![key] as TableDefinition;

                    await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                    {
                        await sxmTransaction.executeTableStatementAsync(tableDefinition.TableSQL);
                        await SxmInit.addSynchID(parts, sxmTransaction);

                        await SxmInit.insertIntoSystemCloudSyncDescriptor(key, databaseName, parts[1], sxmTransaction);

                        //await SxmInit.createCloudSynchTriggers(key, tableNamesMap, sxmTransaction);

                        await sxmTransaction.commitTransactionAsync();
                    }
                }
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                sxmConnection?.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                throw new SxmException(ex);
            }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        /// <summary>
        /// Determines whether the named table exists in the given connection.
        /// </summary>
        /// <param name="tableName">Table name to check.</param>
        /// <param name="sxmConnection">Optional existing connection. If null, a connection is created for the current database.</param>
        /// <returns>True if the table exists; otherwise false.</returns>
        internal static async Task<bool> doesTableExist(string tableName, SxmConnection sxmConnection)
        {
            try
            {
                string sqlSelect = "SELECT name FROM sqlite_master WHERE type='table' AND name=@p0";
                await sxmConnection.executeQueryAsync(sqlSelect, new List<object> { tableName });
                return sxmConnection.hasRows();
            }
            catch (Exception)
            {
            }
            finally
            {
            }

            return false;
        }

        /// <summary>
        /// Execute a DROP TABLE statement and remove related triggers.
        /// </summary>
        /// <param name="key">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection.</param>
        /// <param name="tableDefinition">Table definition containing drop SQL.</param>
        private static async Task applyDropTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition)
        {
            SxmConnection? sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                sxmConnection = connectionMap[databaseName] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    connectionMap.Add(databaseName, sxmConnection);
                }

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmTransaction.executeTableStatementAsync(tableDefinition.TableSQL);
                    await sxmTransaction.executeTableStatementAsync($"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier("update" + tableName)}");
                    await sxmTransaction.executeTableStatementAsync($"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier("delete" + tableName)}");

                    await sxmTransaction.commitTransactionAsync();
                }
            }
#pragma warning disable 0168
            catch (SxmException ex)
#pragma warning restore 0168
            {
                throw;
            }
            catch (System.Exception ex)
            {
                if (sxmConnection != null)
                    sxmConnection.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());

                throw new SxmException(ex);
            }
        }

        // Alter works with 'add', 'drop' and 'rename' column. Don't rename the table.
        /// <summary>
        /// Applies ALTER TABLE statements (add/drop/rename column) for the specified key if required.
        /// </summary>
        /// <param name="key">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection (updated when new connections are created).</param>
        private static async Task applyAlterTableStatements(string key, Hashtable connectionMap)
        {
            SxmConnection? sxmConnection = null;
            List<AlterDefinition>? alterStatementsList = null;

            if (SxmSqlStatements.alterStatements != null)
                alterStatementsList = SxmSqlStatements.alterStatements[key] as List<AlterDefinition>;

            if (alterStatementsList != null)
            {
                string[] parts = key.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                sxmConnection = connectionMap[databaseName] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    connectionMap.Add(databaseName, sxmConnection);
                }

                Hashtable? columnNames = null;
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync($"PRAGMA table_info({SxmHelpers.QuoteIdentifier(tableName)})", default(List<object>));

                    if (alterStatementsList.Count > 1)
                    {
                        columnNames = new Hashtable();
                        while (sxmConnection.nextRow() == true)
                            columnNames.Add((string)sxmConnection.getValue("name")!, new Object());
                    }

                    foreach (AlterDefinition alterDefinition in alterStatementsList)
                    {
                        bool columnFound = false;

                        if (columnNames != null)
                        {
                            if (columnNames[alterDefinition.ColumnName] != null)
                                columnFound = true;
                        }
                        else
                        {
                            while (sxmConnection.nextRow() == true)
                            {
                                string columnName = (string)sxmConnection.getValue("name")!;
                                if (columnName.Equals(alterDefinition.ColumnName) == true)
                                {
                                    columnFound = true;
                                    break;
                                }
                            }
                        }

                        bool runit = false;
                        if (!columnFound)
                        {
                            if (alterDefinition.AlterSQL.ToLower().IndexOf(" add ") != -1)
                                runit = true;
                        }
                        else
                        {
                            if (columnFound)
                            {
                                string lowerSqlStatement = alterDefinition.AlterSQL.ToLower();
                                if (lowerSqlStatement.IndexOf(" drop ") != -1 || lowerSqlStatement.IndexOf(" rename ") != -1)
                                    runit = true;
                            }
                        }

                        if (runit)
                        {
                            try
                            {
                                await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(sxmConnection))
                                {
                                    await sxmTransaction1.executeAlterTableAsync(alterDefinition.AlterSQL);
                                    await sxmTransaction1.commitTransactionAsync();
                                }
                            }
#pragma warning disable 0168
                            catch (SxmException ex)
#pragma warning restore 0168
                            {
                                throw;
                            }
                            catch (System.Exception ex)
                            {
                                sxmConnection.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                                throw new SxmException(ex);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns a mapping of column name to column type for the table referenced by a named SQL statement.
        /// </summary>
        /// <param name="dbName">Database name, or null to use default.</param>
        /// <param name="queryName">Name of the SQL statement defined in the SQL statements file.</param>
        /// <param name="sqlStatementType">Type of SQL statement (select/insert/update/delete).</param>
        /// <returns>Dictionary mapping column name to column type.</returns>
        internal static async Task<Dictionary<string, string>> getTableColumnNames(string? dbName, string queryName, SxmDefines.SqlStatementType sqlStatementType)
        {
            string? tableName = default(string);

            if (sqlStatementType == SxmDefines.SqlStatementType.select)
                tableName = SxmSqlStatements.selectStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.insert)
                tableName = SxmSqlStatements.insertStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.update)
                tableName = SxmSqlStatements.updateStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.delete)
                tableName = SxmSqlStatements.deleteStatements[queryName].TableName;

            if (sqlStatementType == SxmDefines.SqlStatementType.selectDirect)
                tableName = SxmHelpers.ExtractTableNameFromSelect(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.insertDirect)
                tableName = SxmHelpers.ExtractTableNameFromInsert(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.updateDirect)
                tableName = SxmHelpers.ExtractTableNameFromUpdate(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.deleteDirect)
                tableName = SxmHelpers.ExtractTableNameFromDelete(queryName);

            if (sqlStatementType == SxmDefines.SqlStatementType.unknown || string.IsNullOrEmpty(tableName))
                throw new SxmException(new ErrorMessage("unknownSQLStatement", queryName));

            return await getTableColumnNames(dbName, tableName);
        }

        /// <summary>
        /// Store a cached mapping of column name to column type for the specified table.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="columnName">Column name.</param>
        /// <param name="columnType">Column type as reported by PRAGMA table_info.</param>
        internal static void addColumnNameType(string tableName, string columnName, string columnType)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));

            if (columnNameTypes.TryGetValue(tableName, out var inner))
            {
                // Preserve original behavior: Dictionary.Add would throw on duplicate;
                // use TryAdd and throw if the key already exists.
                if (!inner.TryAdd(columnName, columnType))
                    throw new InvalidOperationException($"Column '{columnName}' already exists in cache for table '{tableName}'.");
            }
            // If table not present in cache, intentionally ignore (no creation) — same as original.
        }

        /// <summary>
        /// Remove a cached column name/type mapping for the specified table.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="columnName">Column to remove.</param>
        internal static void removeColumnNameType(string tableName, string columnName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));

            if (columnNameTypes.TryGetValue(tableName, out var inner))
            {
                inner.TryRemove(columnName, out _);
            }
        }

        /// <summary>
        /// Retrieve column name/type mapping for a table. Results are cached for subsequent calls.
        /// </summary>
        /// <param name="dbName">Database name, or null to use the default database.</param>
        /// <param name="tableName">Table name whose columns should be returned.</param>
        /// <returns>
        /// Dictionary mapping column name to SQL type.
        /// The returned Dictionary is a point-in-time snapshot of the internal cache and does not reflect subsequent concurrent changes.
        /// </returns>
        /// <remarks>
        /// The internal cache is thread-safe; callers receive a snapshot to preserve existing API semantics.
        /// Callers must not rely on the returned Dictionary reflecting later mutations.
        /// </remarks>
        internal static async Task<Dictionary<string, string>> getTableColumnNames(string? dbName, string? tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            // Fast path: if inner exists return a snapshot.
            if (columnNameTypes.TryGetValue(tableName, out var existingInner))
                return new Dictionary<string, string>(existingInner, StringComparer.Ordinal);

            // Load into a concurrent inner map.
            var columnNames = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

            SxmConnection sxmConnection = new SxmConnection(dbName);
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
            {
                await sxmConnection.executeQueryAsync($"PRAGMA table_info({SxmHelpers.QuoteIdentifier(tableName)})", default(List<object>));
                while (sxmConnection.nextRow() == true)
                {
                    string? columnName = (string?)sxmConnection.getValue("name");
                    string? columnType = (string?)sxmConnection.getValue("type");

                    if (columnName != null && columnType != null)
                        columnNames.TryAdd(columnName, columnType);
                }
            }

            // Install the loaded concurrent inner map as the live cached instance.
            var winner = columnNameTypes.GetOrAdd(tableName, columnNames);

            // Return a snapshot Dictionary<string,string> so callers continue to get the same concrete type.
            return new Dictionary<string, string>(winner, StringComparer.Ordinal);
        }

        /// <summary>
        /// Create or update triggers in each database according to the parsed SQL statements file.
        /// Existing triggers are dropped before new ones are created.
        /// </summary>
        /// <param name="connectionMap">Map of database name to active connection. If null, connections will be created as needed.</param>
        private static async Task applyTriggerTableStatements(string databaseName)
        {
            List<TriggerDefinition>? triggerStatementsList = default(List<TriggerDefinition>);

            if (SxmSqlStatements.triggerStatements != null)
            {
                ICollection triggerStatementKeys = SxmSqlStatements.triggerStatements.Keys;
                foreach (string dbName in triggerStatementKeys)
                {
                    // Delete all triggers in the database.
                    SxmConnection sxmConnection = new SxmConnection(databaseName);
                    await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                    {
                        List<string> ExistingTriggers = await SxmInit.getAllTriggers(sxmTransaction.Connection, string.Empty);
                        foreach (string existingTrigger in ExistingTriggers)
                        {
                            await sxmTransaction.executeCreateTriggerAsync($"DROP TRIGGER {SxmHelpers.QuoteIdentifier(existingTrigger)}");
                        }

                        await sxmTransaction.commitTransactionAsync();
                    }

                    // Get all triggers in the SQL Statements file and create them.
                    triggerStatementsList = SxmSqlStatements.triggerStatements[dbName] as List<TriggerDefinition>;
                    if (triggerStatementsList != null)
                    {
                        await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                        {
                            foreach (TriggerDefinition td in triggerStatementsList)
                            {
                                await sxmTransaction.executeCreateTriggerAsync(td.TriggerSQL);
                            }
                            await sxmTransaction.commitTransactionAsync();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply index create/drop statements for a given table key.
        /// </summary>
        /// <param name="key">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection (updated when new connections are created).</param>
        private static async Task applyIndexTableStatements(string key, Hashtable connectionMap)
        {
            List<IndexDefinition>? indexStatementsList = default(List<IndexDefinition>);

            if (SxmSqlStatements.indexStatements != null)
                indexStatementsList = SxmSqlStatements.indexStatements[key] as List<IndexDefinition>;

            if (indexStatementsList != null)
            {
                string[] parts = key.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                SxmConnection? sxmConnection = connectionMap[databaseName] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    connectionMap.Add(databaseName, sxmConnection);
                }

                Hashtable? indexNames = null;
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                {
                    await sxmConnection.executeQueryAsync($"PRAGMA index_list({SxmHelpers.QuoteIdentifier(tableName)})", null as List<object>);

                    if (indexStatementsList.Count > 1)
                    {
                        indexNames = new Hashtable();
                        while (sxmConnection.nextRow() == true)
                            indexNames.Add((string)sxmConnection.getValue("name")!, new Object());
                    }

                    foreach (IndexDefinition indexDefinition in indexStatementsList)
                    {
                        bool indexFound = false;
                        bool runit = false;

                        if (indexNames != null)
                        {
                            if (indexNames[indexDefinition.IndexName] != null)
                                indexFound = true;
                        }
                        else
                        {
                            while (sxmConnection.nextRow() == true)
                            {
                                string indexName = (string)sxmConnection.getValue("name")!;
                                if (indexName.Equals(indexDefinition.IndexName) == true)
                                {
                                    indexFound = true;
                                    break;
                                }
                            }
                        }

                        if (indexFound == false && indexDefinition.IndexSQL.StartsWith("CREATE ", true, null) == true)
                        {
                            if (dropExists(indexStatementsList, indexDefinition.IndexName) == false)
                                runit = true;
                        }
                        else
                            if (indexFound == true && indexDefinition.IndexSQL.StartsWith("DROP ", true, null) == true)
                            runit = true;

                        if (runit == true)
                        {
                            try
                            {
                                await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(sxmConnection))
                                {
                                    await sxmTransaction1.executeIndexAsync(indexDefinition.IndexSQL);
                                    await sxmTransaction1.commitTransactionAsync();
                                }
                            }
#pragma warning disable 0168
                            catch (SxmException ex)
#pragma warning restore 0168
                            {
                                throw;
                            }
                            catch (System.Exception ex)
                            {
                                sxmConnection.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                                throw new SxmException(ex);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if a DROP entry exists for the specified index name in the index statement list.
        /// </summary>
        /// <param name="indexStatementsList">List of index definitions to search.</param>
        /// <param name="indexName">Index name to find.</param>
        /// <returns>True if a DROP statement exists for the index; otherwise false.</returns>
        private static bool dropExists(List<IndexDefinition> indexStatementsList, string indexName)
        {
            foreach (IndexDefinition indexDefinition in indexStatementsList)
            {
                if (indexDefinition.IndexName.Equals(indexName) == true)
                    if (indexDefinition.IndexSQL.StartsWith("DROP ", true, null) == true)
                        return true;
            }

            return false;
        }

        /// <summary>
        /// Add a synchronization identifier column to a newly created table.
        /// </summary>
        /// <param name="parts">Array with two elements: [0] = database name, [1] = table name.</param>
        /// <param name="sxmTransaction">Active transaction used to execute the ALTER statement.</param>
        private static async Task addSynchID(string[] parts, SxmUTransaction sxmTransaction)
        {
            string alterSQL = $"ALTER TABLE {SxmHelpers.QuoteIdentifier(parts[1])} ADD COLUMN {SxmHelpers.QuoteIdentifier("synchId")} TEXT NOT NULL DEFAULT ''";
            await sxmTransaction.executeTableStatementAsync(alterSQL);
        }

        /// <summary>
        /// Create the _systemCloudSynchDescriptor table if required and insert a descriptor row for the created table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        /// <param name="sxmTransaction">Active transaction used to execute DDL and inserts.</param>
        private static async Task CreateCloudSyncDescriptor(SxmUTransaction sxmTransaction)
        {
            string databaseTable = "_systemCloudSynchDescriptor";

            string tableSQL = $"CREATE TABLE {SxmHelpers.QuoteIdentifier(databaseTable)} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, cloudSynchFlag INTEGER)";
            await sxmTransaction.executeTableStatementAsync(tableSQL);
        }

        /// <summary>
        /// Insert a row into the _systemCloudSynchDescriptor table for the specified table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="databaseName">Database name.</param>
        /// <param name="tableName">Table name.</param>
        /// <param name="sxmTransaction">Active transaction used to execute the insert.</param>
        private static async Task insertIntoSystemCloudSyncDescriptor(string key, string databaseName, string tableName, SxmUTransaction sxmTransaction)
        {
            TableDefinition tableDefinition = SxmSqlStatements.tableCreateStatements[key] as TableDefinition;
            List<object> parameterValues = new List<object>();
            parameterValues.Add(databaseName);
            parameterValues.Add(tableName);
            parameterValues.Add(tableDefinition.CloudSynch);
            await sxmTransaction.executeSystemUpdateDirectAsync("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues);
        }


        /// <summary>
        /// Create the _systemCloudSynch table used to queue cloud synchronization actions.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        /// <param name="sxmTransaction">Active transaction used to execute the DDL.</param>
        private static async Task createCloudSynchTable(SxmUTransaction sxmTransaction)
        {
            string databaseTable = "_systemCloudSynch";

            string tableSQL = $"CREATE TABLE {SxmHelpers.QuoteIdentifier(databaseTable)} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, action TEXT, synchId TEXT)";
            await sxmTransaction.executeTableStatementAsync(tableSQL);
        }

        /// <summary>
        /// Create triggers that populate the _systemCloudSynch queue for insert/update/delete operations on a table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database (unused in current implementation).</param>
        /// <param name="sxmTransaction">Active transaction used to create the triggers.</param>
        private static async Task createCloudSynchTriggers(string key, SxmUTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = parts[1];

            TableDefinition? tableDefinition = SxmSqlStatements.tableCreateStatements?[key] as TableDefinition;

            if (tableDefinition?.CloudSynch == SxmDefines.CLOUD_SYNCH || tableDefinition?.CloudSynch == SxmDefines.CLOUD_MOVE)
            {
                // Escape values for SQL string literals (used inside VALUES(...)).
                string safeDbName = databaseName.Replace("'", "''");
                string safeTableNameLiteral = databaseTable.Replace("'", "''");

                // Quote SQL identifiers (trigger name, table name, system table).
                string quotedTriggerUpdate = SxmHelpers.QuoteIdentifier("update" + databaseTable);
                string quotedSystemTable = SxmHelpers.QuoteIdentifier("_systemCloudSynch");
                string quotedTable = SxmHelpers.QuoteIdentifier(databaseTable);

                string triggerUpdateSql =
                    $"CREATE TRIGGER IF NOT EXISTS {quotedTriggerUpdate} UPDATE ON {quotedTable} " +
                    $"BEGIN INSERT INTO {quotedSystemTable} (dbName, tableName, action, synchId) " +
                    $"VALUES ('{safeDbName}', '{safeTableNameLiteral}', 'update', new.synchId); END;";
                await sxmTransaction.executeCreateTriggerAsync(triggerUpdateSql);

                if (tableDefinition?.CloudSynch == SxmDefines.CLOUD_SYNCH) // This is weird, needs fixing. How does it make sense for this to be inside this code block?
                {
                    string quotedTriggerDelete = SxmHelpers.QuoteIdentifier("delete" + databaseTable);
                    string triggerDeleteSql =
                        $"CREATE TRIGGER IF NOT EXISTS {quotedTriggerDelete} DELETE ON {quotedTable} " +
                        $"BEGIN INSERT INTO {quotedSystemTable} (dbName, tableName, action, synchId) " +
                        $"VALUES ('{safeDbName}', '{safeTableNameLiteral}', 'delete', old.synchId); END;";
                    await sxmTransaction.executeCreateTriggerAsync(triggerDeleteSql);
                }
            }
        }

        private static async Task<bool> doesTableExist(string key, Hashtable connectionMap, Hashtable tableNamesMap)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string tableName = parts[1];

            SxmConnection? sxmConnection = connectionMap[databaseName] as SxmConnection;
            if (sxmConnection == null)
            {
                sxmConnection = new SxmConnection(databaseName);
                connectionMap.Add(databaseName, sxmConnection);

                await sxmConnection.executeQueryAsync("SELECT name FROM sqlite_master WHERE type='table'", null as List<object>);

                ArrayList tableNames = new ArrayList();
                if (sxmConnection.hasRows() == true)
                {
                    string[] fieldNames = sxmConnection.getFieldNames();
                    while (sxmConnection.nextRow() == true)
                    {
                        foreach (string fieldName in fieldNames)
                            tableNames.Add(sxmConnection.getValue(fieldName));
                    }
                }

                tableNamesMap.Add(databaseName, tableNames);

                if (isTableInMap(databaseName, tableName, tableNamesMap))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true when the specified table name is present in the provided table names map for the database.
        /// </summary>
        /// <param name="databaseName">Database name used as the key into the map.</param>
        /// <param name="tableName">Table name to look for.</param>
        /// <param name="tableNamesMap">Map storing table lists per database.</param>
        /// <returns>True if the table is present in the map; otherwise false.</returns>
        private static bool isTableInMap(string databaseName, string tableName, Hashtable tableNamesMap)
        {
            var dbTableNames = tableNamesMap[databaseName] as ArrayList;
            return dbTableNames != null && System.Linq.Enumerable.Cast<string>(dbTableNames).Any(dbTableName => dbTableName.Equals(tableName));
        }

        /// <summary>
        /// Returns the names of all triggers in the provided connection optionally filtered by table name.
        /// </summary>
        /// <param name="sxmConnection">Connection to query for triggers.</param>
        /// <param name="tableName">Optional table name to filter triggers. Use empty string for no filter.</param>
        /// <returns>List of trigger names.</returns>
        internal static async Task<List<string>> getAllTriggers(SxmConnection? sxmConnection, string tableName)
        {
            List<string> triggerNames = new List<string>();

            if (sxmConnection != null)
            {
                // With parameterized call:
                string sql = "SELECT name FROM sqlite_master WHERE type='trigger'";

                List<object>? parms = null;
                if (!string.IsNullOrEmpty(tableName))
                {
                    sql += " AND tbl_name = @p0";
                    parms = new List<object> { tableName };
                }
                await sxmConnection.executeQueryAsync(sql, parms);

                if (sxmConnection.hasRows() == true)
                {
                    string[] fieldNames = sxmConnection.getFieldNames();
                    while (sxmConnection.nextRow() == true)
                    {
                        foreach (string fieldName in fieldNames)
                            triggerNames.Add(sxmConnection.getValue(fieldName).ToString());
                    }
                }
            }

            return triggerNames;
        }
    }
}