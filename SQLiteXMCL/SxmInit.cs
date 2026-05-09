using LinqToDB.Common;
using SQLiteXM.Internal.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Xml.Linq;
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
    public static class SxmInit
    {
        /// <summary>
        /// Cache mapping table name -> (column name -> column type) using thread-safe concurrent dictionaries.
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _columnNameTypes = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.Ordinal);

        /// <summary>
        /// Async gate that serializes database initialization.
        /// </summary>
        /// <remarks>
        /// Only one InitDbAsync call may execute at a time. The initialization process
        /// modifies schema and PRAGMA settings and is therefore not concurrency-safe.
        /// This semaphore prevents race conditions without requiring callers to coordinate.
        /// </remarks>
        private static readonly SemaphoreSlim _initGate = new(1, 1);

        // Volatile read is not required because all writes occur under _initGate.
        // Reads outside the gate are safe because false positives only cause an extra wait.
        private static bool _initialized = false;

#if DEBUG
        /// <summary>
        /// Resets all static initialization state to allow re-initialization.
        /// **WARNING:** This is intended ONLY for testing scenarios and should NEVER be called in production code.
        /// Calling this while entities or connections are active will cause undefined behavior.
        /// </summary>
        /// <remarks>
        /// This method clears:
        /// - Initialization flag and cached metadata in SxmInit
        /// - Entity schema caches in SxmEntity (column maps, index bags, initialization tasks)
        /// - Database descriptors and SQL statement caches
        /// - Connection manager state
        /// 
        /// After calling this, you must call InitDbAsync again before using any entities.
        /// </remarks>
        public static async Task ResetForTestingAsync()
        {
            await _initGate.WaitAsync().ConfigureAwait(false);
            try
            {
                // Reset SxmInit state
                _initialized = false;

                // Reset SxmEntity static caches via reflection (they're private)
                var entityType = typeof(SxmEntity);

                ClearStaticField(entityType, "_initTasks");
                ClearStaticField(entityType, "_columnNameAndTypeDict");
                ClearStaticField(entityType, "_uniqueIndexDict");
                ClearStaticField(entityType, "_standardIndexDict");
                ClearStaticField(entityType, "_insertGuidDict");
                ClearStaticField(entityType, "_updateGuidDict");
                ClearStaticField(entityType, "_deleteGuidDict");
                ClearStaticField(entityType, "_tableAttributeNameCache");
                ClearStaticField(entityType, "_entityTypeMap");
                ClearStaticField(entityType, "_entityDatabaseMap");

                // Reset database descriptors
                SxmDatabaseDescriptor.ResetForTesting();

                // Reset SQL statements
                SxmSqlStatements.ResetForTesting();

                // Reset init options database registry
                SxmInitOptions.ResetForTesting();

                // Note: We don't reset SxmConnectionManager as it manages active connections
                // Tests should ensure all connections are properly disposed before calling ResetForTestingAsync
            }
            finally
            {
                _initGate.Release();
            }
        }

        private static void ClearStaticField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                var fieldValue = field.GetValue(null);
                if (fieldValue is System.Collections.IDictionary dict)
                {
                    dict.Clear();
                }
            }
        }
#endif

        /// <summary>
        /// Initialize the database using SQL statements parsed from the specified file.
        /// </summary>
        /// <param name="sqlStatementsFileName">Path to the SQL statements file (absolute or relative).
        /// <param name="fileType">Format of the SQL statements file.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        public static async Task InitDbAsync(string sqlStatementsFileName, SxmInitOptions? initOptions = null)
        {
            await _initGate.WaitAsync().ConfigureFalse();
            try
            {
                if (_initialized)
                    return;

                string fullPathToSqlStatementsFile = ResolveSqlStatementsFile(sqlStatementsFileName);
                SqlStatementsFileType fileType = SxmHelpers.GetSqlStatementsFileType(fullPathToSqlStatementsFile);
                if (fileType == SqlStatementsFileType.Unknown)
                    throw new ArgumentException($"'{sqlStatementsFileName}' is an unknown SQL statements file type. The SQL statements file must be JSON or XML.");

                // Only the first call to 'DatabaseFolder' property setter will actually set the 'DatabaseFolder'.
                // Follow on calls to set will be ignored even if the initial setter value is null.
                // CRITICAL: Must set DatabaseFolder BEFORE any SxmDatabaseDescriptor is created.
                SxmDatabaseDescriptor.DatabaseFolder = initOptions?.DatabaseFolderOverride;

                {
                    using FileStream stream = File.OpenRead(fullPathToSqlStatementsFile);
                    await ParseSqlStatementsFile(stream, fileType).ConfigureFalse();
                }

                SxmInitOptions.AddDatabaseName(initOptions, SxmProcessSQLStatements.DatabaseName);
                await SxmInit.InitializeAsync().ConfigureFalse();

                // Mark initialization complete only after the full pipeline succeeds.
                // If any step throws, _initialized remains false so a later call can retry.
                _initialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        /// <summary>
        /// Initialize the database using SQL statements parsed from the provided stream.
        /// </summary>
        /// <param name="stream">Open, readable stream containing SQL statement definitions.</param>
        /// <param name="fileType">Format of the SQL statements contained in the stream.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        public static async Task InitDbAsync(Stream stream, SxmInitOptions? initOptions = null)
        {
            await _initGate.WaitAsync().ConfigureFalse();
            try
            {
                if (_initialized)
                    return;

                // Only the first call to 'DatabaseFolder' property setter will actually set the 'DatabaseFolder'.
                // Follow on calls to set will be ignored even if the initial setter value is null.
                // CRITICAL: Must set DatabaseFolder BEFORE any SxmDatabaseDescriptor is created.
                SxmDatabaseDescriptor.DatabaseFolder = initOptions?.DatabaseFolderOverride;

                await ParseSqlStatementsFile(stream, SqlStatementsFileType.Unknown).ConfigureFalse();

                SxmInitOptions.AddDatabaseName(initOptions, SxmProcessSQLStatements.DatabaseName);
                await SxmInit.InitializeAsync().ConfigureFalse();

                // Mark initialization complete only after the full pipeline succeeds.
                // If any step throws, _initialized remains false so a later call can retry.
                _initialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        /// <summary>
        /// Ensures that the SQLiteXM initialization process has completed.
        /// </summary>
        /// <remarks>
        /// Entity classes and database operations require the ORM to be initialized
        /// via <see cref="SxmInit.InitDbAsync"/> before use. This method provides a
        /// centralized fail-fast guard that throws a clear exception if initialization
        /// has not yet occurred.
        /// 
        /// The initialization flag is written only after successful completion of the
        /// initialization pipeline. A simple read check is sufficient here because
        /// concurrent calls are serialized by the initialization gate.
        /// </remarks>
        internal static void EnsureInitialized()
        {
            // Fail fast if initialization has not completed.
            // This prevents entity usage before schema and PRAGMA configuration are applied.
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "SQLiteXM has not been initialized. Call SxmInit.InitDbAsync(...) before instantiating entity classes.");
            }
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
        private static string ResolveSqlStatementsFile(string fileName)
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
        private static Task ParseSqlStatementsFile(Stream stream, SxmDefines.SqlStatementsFileType fileType)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

            switch (fileType)
            {
                case SxmDefines.SqlStatementsFileType.Json:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.Json);
                    break;

                case SxmDefines.SqlStatementsFileType.Xml:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.Xml);
                    break;

                case SxmDefines.SqlStatementsFileType.Unknown:
                default:
                    SxmProcessSQLStatements.Parse(stream, SxmDefines.SqlStatementsFileType.Unknown);
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
            if (await FileSystem.AppPackageFileExistsAsync(SqlStatementsFileName).ConfigureFalse())
            {
                using (Stream stream = await FileSystem.OpenAppPackageFileAsync(SqlStatementsFileName).ConfigureFalse())
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
        private static async Task<bool> InitializeAsync()
        {
            // At this point, the SQL statements file has been parsed, the database name, default status, and version have been loaded into SxmProcessSQLStatements.
            new SxmDatabaseDescriptor();
            string databaseName = SxmProcessSQLStatements.DatabaseName;

            SxmConnection? sxmConnection = null;
            try
            {
                long sqlStatementsVersionNumber = SxmProcessSQLStatements.SqlStatementsVersionNumber;  // The value in the current SQL statements file.
                long currentDbVersionNumber = await GetDbVersionNumberAsync(databaseName).ConfigureFalse();

                if (sqlStatementsVersionNumber > currentDbVersionNumber || sqlStatementsVersionNumber == 0)
                {
                    await CreateSystemTablesAsync(databaseName).ConfigureFalse();
                    sxmConnection = new SxmConnection(databaseName, shared: true);

                    if (SxmSqlStatements.TableCreateStatements != default(Dictionary<string, TableDefinition>))
                    {
                        Hashtable tableNamesMap = new();

                        foreach (string DatabaseNameTableName in SxmSqlStatements.TableCreateStatements.Keys) // the 'key' string value is 'DatabaseName.TableName'
                        {
                            if (DatabaseNameTableName.Split('.').Length != 2)
                                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.InvalidTableName, DatabaseNameTableName));

                            if (!await DoesTableExistAsync(DatabaseNameTableName, sxmConnection, tableNamesMap).ConfigureFalse())
                            {
                                TableDefinition tableDefinition = SxmSqlStatements.TableCreateStatements[DatabaseNameTableName] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("CREATE ", true, null) == true)
                                    await ApplyCreateTableStatementAsync(DatabaseNameTableName, sxmConnection, tableDefinition).ConfigureFalse();
                            }
                            else
                            {
                                TableDefinition tableDefinition = SxmSqlStatements.TableCreateStatements[DatabaseNameTableName] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("DROP ", true, null) == true)
                                    await ApplyDropTableStatementAsync(DatabaseNameTableName, sxmConnection, tableDefinition).ConfigureFalse();
                                else
                                {
                                    await ApplyAlterTableStatementsAsync(DatabaseNameTableName, sxmConnection).ConfigureFalse();
                                    await ApplyIndexTableStatementsAsync(DatabaseNameTableName, sxmConnection).ConfigureFalse();
                                }
                            }
                        }
                    }

                    await ApplyTriggerTableStatementsAsync(sxmConnection, databaseName).ConfigureFalse();
                    await StoreDbVersionNumberAsync(sqlStatementsVersionNumber, databaseName, sxmConnection).ConfigureFalse();
                    await SxmAssociationMapper.InitializeAssociationsAsync(databaseName).ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"InitializeAsync failure for database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"InitializeAsync failure for database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
                if (sxmConnection != null)
                    await sxmConnection.DestroyConnectionAsync().ConfigureFalse();
                SxmSqlStatements.ClearStatementTables();
            }

            return true;
        }

        private static async Task CreateSystemTablesAsync(string databaseName)
        {
            SxmConnection sxmConnection = new SxmConnection(databaseName);
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
            {
                if (!await DoesTableExistAsync("_systemCloudSynchDescriptor", sxmConnection).ConfigureFalse())
                    await CreateCloudSyncDescriptorAsync(sxmTransaction).ConfigureFalse();

                if (!await DoesTableExistAsync("_systemCloudSynch", sxmConnection).ConfigureFalse())
                    await CreateCloudSynchTableAsync(sxmTransaction).ConfigureFalse();

                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
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
        public static async Task<long> GetDbVersionNumberAsync(string databaseName)
        {
            long versionNumber = -1;

            try
            {
                SxmConnection sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmConnection.ExecuteQueryAsync("PRAGMA user_version", default(List<object>)).ConfigureFalse();

                    if (sxmConnection.HasRows() && sxmConnection.NextRow())
                    {
                        versionNumber = Convert.ToInt64(sxmConnection.GetValue("user_version"));
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"GetDbVersionNumberAsync failure for database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetDbVersionNumberAsync failure for database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return versionNumber;
        }

        internal static void SetJournalMode()
        {

        }

        /// <summary>
        /// Reset the stored database version number (PRAGMA user_version = 0).
        /// </summary>
        /// <returns>A task that completes when the operation finishes.</returns>
        public static async Task DeleteDbVersionNumberAsync(string databaseName)
        {
            try
            {
                SxmConnection sxmConnection = new SxmConnection(databaseName);
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmConnection.ExecuteQueryAsync("PRAGMA user_version = 0", default(List<object>)).ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DeleteDbVersionNumberAsync failure for database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DeleteDbVersionNumberAsync failure for database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Store the supplied version into PRAGMA user_version.
        /// </summary>
        /// <param name="versionNumber">Version number to store.</param>
        /// <returns>A task that completes when the PRAGMA has been set.</returns>
        /// <remarks>
        /// SECURITY NOTE: This method uses string formatting instead of parameterization because
        /// PRAGMA statements in SQLite do not support parameter binding. However, this is safe because
        /// <paramref name="versionNumber"/> is a strongly-typed long (not user-controlled string input),
        /// which eliminates SQL injection risk. The formatted value is validated by the compiler and runtime.
        /// </remarks>
        private static async Task StoreDbVersionNumberAsync(long versionNumber, string databaseName, SxmConnection sxmConnection)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    // Safe: versionNumber is type-safe long, not user input string.
                    await sxmConnection.ExecuteQueryAsync(String.Format("PRAGMA user_version = {0}", versionNumber), default(List<object>)).ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"StoreDbVersionNumberAsync failure for database '{databaseName}' version number '{versionNumber}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"StoreDbVersionNumberAsync failure for database '{databaseName}' version number '{versionNumber}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Execute a CREATE TABLE statement and ensure related synchronization descriptors and triggers exist.
        /// </summary>
        /// <param name="DatabaseNameTableName">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection.</param>
        /// <param name="tableDefinition">Table definition containing DDL and flags.</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        private static async Task ApplyCreateTableStatementAsync(string DatabaseNameTableName, SxmConnection sxmConnection, TableDefinition tableDefinition)
        {
            string? databaseName = default;
            string? tableName = default;

            try
            {
                string[] parts = DatabaseNameTableName.Split('.');
                databaseName = parts[0];
                tableName = parts[1];

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmTransaction.ExecuteTableStatementAsync(tableDefinition.TableSQL).ConfigureFalse();
                    await AddSynchIdAsync(parts, sxmTransaction).ConfigureFalse();

                    await InsertIntoSystemCloudSyncDescriptorAsync(DatabaseNameTableName, databaseName, tableName, sxmTransaction).ConfigureFalse();

                    await CreateCloudSynchTriggersAsync(DatabaseNameTableName, sxmTransaction).ConfigureFalse();
                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }

                await ApplyIndexTableStatementsAsync(DatabaseNameTableName, sxmConnection).ConfigureFalse();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"ApplyCreateTableStatementAsync failure for database '{databaseName}' table '{tableName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ApplyCreateTableStatementAsync failure for database '{databaseName}' table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Create a single table by name in the given database using SQL statements parsed earlier.
        /// </summary>
        /// <param name="databaseName">Name of the database to operate on.</param>
        /// <param name="tableName">Name of the table to create.</param>
        internal static async Task CreateTableAsync(string? databaseName, string tableName)
        {
            if (databaseName == null)
                return;

            string[] parts = { databaseName, tableName };
            string key = string.Format("{0}.{1}", databaseName, tableName);

            try
            {
                SxmConnection? sxmConnection = new SxmConnection(databaseName);
                if (!await DoesTableExistAsync(tableName, sxmConnection).ConfigureFalse())
                {
                    Hashtable tableNamesMap = new Hashtable();
                    TableDefinition? tableDefinition = SxmSqlStatements.TableCreateStatements![key] as TableDefinition;

                   await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                    {
                        await sxmTransaction.ExecuteTableStatementAsync(tableDefinition.TableSQL).ConfigureFalse();
                        await SxmInit.AddSynchIdAsync(parts, sxmTransaction).ConfigureFalse();

                        await SxmInit.InsertIntoSystemCloudSyncDescriptorAsync(key, databaseName, parts[1], sxmTransaction).ConfigureFalse();

                        //await SxmInit.createCloudSynchTriggers(key, tableNamesMap, sxmTransaction).ConfigureFalse();

                        await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"CreateTableAsync failure for database '{databaseName} table '{tableName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"CreateTableAsync failure for database '{databaseName} table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Determines whether the named table exists in the given connection.
        /// </summary>
        /// <param name="tableName">Table name to check.</param>
        /// <param name="sxmConnection">Optional existing connection. If null, a connection is created for the current database.</param>
        /// <returns>True if the table exists; otherwise false.</returns>
        internal static async Task<bool> DoesTableExistAsync(string tableName, SxmConnection sxmConnection)
        {
            try
            {
                string sqlSelect = "SELECT name FROM sqlite_master WHERE type='table' AND name=@p0";
                await sxmConnection.ExecuteQueryAsync(sqlSelect, new List<object> { tableName }).ConfigureFalse();
                return sxmConnection.HasRows();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DoesTableExistAsync failure for table '{tableName}' in database 'sqlite_master'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DoesTableExistAsync failure for table '{tableName}' in database 'sqlite_master'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
            }
        }

        /// <summary>
        /// Execute a DROP TABLE statement and remove related triggers.
        /// </summary>
        /// <param name="DatabaseNameTableName">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection.</param>
        /// <param name="tableDefinition">Table definition containing drop SQL.</param>
        private static async Task ApplyDropTableStatementAsync(string DatabaseNameTableName, SxmConnection sxmConnection, TableDefinition tableDefinition)
        {
            string? databaseName = default;
            string? tableName = default;

            try
            {
                string[] parts = DatabaseNameTableName.Split('.');
                databaseName = parts[0];
                tableName = parts[1];

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmTransaction.ExecuteTableStatementAsync(tableDefinition.TableSQL).ConfigureFalse();
                    await sxmTransaction.ExecuteTableStatementAsync($"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier("update" + tableName)}").ConfigureFalse();
                    await sxmTransaction.ExecuteTableStatementAsync($"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier("delete" + tableName)}").ConfigureFalse();

                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"ApplyDropTableStatementAsync failure for table '{tableName}' database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ApplyDropTableStatementAsync failure for table '{tableName}' database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        // Alter works with 'add', 'drop' and 'rename' column. Don't rename the table.
        /// <summary>
        /// Applies ALTER TABLE statements (add/drop/rename column) for the specified key if required.
        /// </summary>
        /// <param name="DatabaseNameTableName">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection (updated when new connections are created).</param>
        private static async Task ApplyAlterTableStatementsAsync(string DatabaseNameTableName, SxmConnection sxmConnection)
        {
            List<AlterDefinition>? alterStatementsList = null;

            if (SxmSqlStatements.AlterStatements != null)
                alterStatementsList = SxmSqlStatements.AlterStatements[DatabaseNameTableName] as List<AlterDefinition>;

            if (alterStatementsList != null)
            {
                string[] parts = DatabaseNameTableName.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                Hashtable? columnNames = null;
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmConnection.ExecuteQueryAsync($"PRAGMA table_info({SxmHelpers.QuoteIdentifier(tableName)})", default(List<object>)).ConfigureFalse();

                    if (alterStatementsList.Count > 1)
                    {
                        columnNames = new Hashtable();
                        while (sxmConnection.NextRow() == true)
                            columnNames.Add((string)sxmConnection.GetValue("name")!, new Object());
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
                            while (sxmConnection.NextRow() == true)
                            {
                                string columnName = (string)sxmConnection.GetValue("name")!;
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
                                await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                                {
                                    await sxmTransaction1.ExecuteAlterTableAsync(alterDefinition.AlterSQL).ConfigureFalse();
                                    await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
                                }
                            }
                            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                            {
                                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                                SxmLogging.Log(ex, $"ApplyAlterTableStatementsAsync failure for table '{tableName}' database '{databaseName}' SQL Statement '{alterDefinition.AlterSQL}'.");
                                throw;
                            }
                            catch (System.Exception ex)
                            {
                                string errStr = $"ApplyAlterTableStatementsAsync failure for table '{tableName}' database '{databaseName}' SQL Statement '{alterDefinition.AlterSQL}'.";
                                SxmLogging.Log(ex, errStr);
                                throw ExceptionHelper.Wrap(ex, errStr);
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
        internal static async Task<Dictionary<string, string>> GetTableColumnNamesAsync(string? dbName, string queryName, SxmDefines.SqlStatementType sqlStatementType)
        {
            string? tableName = default(string);

            if (sqlStatementType == SxmDefines.SqlStatementType.Select)
                tableName = SxmSqlStatements.SelectStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Insert)
                tableName = SxmSqlStatements.InsertStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Update)
                tableName = SxmSqlStatements.UpdateStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Delete)
                tableName = SxmSqlStatements.DeleteStatements[queryName].TableName;

            if (sqlStatementType == SxmDefines.SqlStatementType.SelectDirect)
                tableName = SxmHelpers.ExtractTableNameFromSelect(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.InsertDirect)
                tableName = SxmHelpers.ExtractTableNameFromInsert(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.UpdateDirect)
                tableName = SxmHelpers.ExtractTableNameFromUpdate(queryName);
            if (sqlStatementType == SxmDefines.SqlStatementType.DeleteDirect)
                tableName = SxmHelpers.ExtractTableNameFromDelete(queryName);

            if (sqlStatementType == SxmDefines.SqlStatementType.Unknown || string.IsNullOrEmpty(tableName))
                throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, queryName));

            return await GetTableColumnNamesAsync(dbName, tableName).ConfigureFalse();
        }

        /// <summary>
        /// Store a cached mapping of column name to column type for the specified table.
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="columnName">Column name.</param>
        /// <param name="columnType">Column type as reported by PRAGMA table_info.</param>
        internal static void AddColumnNameType(string tableName, string columnName, string columnType)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));

            if (_columnNameTypes.TryGetValue(tableName, out var inner))
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
        internal static void RemoveColumnNameType(string tableName, string columnName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(columnName))
                throw new ArgumentNullException(nameof(columnName));

            if (_columnNameTypes.TryGetValue(tableName, out var inner))
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
        internal static async Task<Dictionary<string, string>> GetTableColumnNamesAsync(string? dbName, string? tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            // Fast path: if inner exists return a snapshot.
            if (_columnNameTypes.TryGetValue(tableName, out var existingInner))
                return new Dictionary<string, string>(existingInner, StringComparer.Ordinal);

            // Load into a concurrent inner map.
            var columnNames = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

            SxmConnection sxmConnection = new SxmConnection(dbName);
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
            {
                await sxmConnection.ExecuteQueryAsync($"PRAGMA table_info({SxmHelpers.QuoteIdentifier(tableName)})", default(List<object>)).ConfigureFalse();
                while (sxmConnection.NextRow() == true)
                {
                    string? columnName = (string?)sxmConnection.GetValue("name");
                    string? columnType = (string?)sxmConnection.GetValue("type");

                    if (columnName != null && columnType != null)
                        columnNames.TryAdd(columnName, columnType);
                }
            }

            // Install the loaded concurrent inner map as the live cached instance.
            var winner = _columnNameTypes.GetOrAdd(tableName, columnNames);

            // Return a snapshot Dictionary<string,string> so callers continue to get the same concrete type.
            return new Dictionary<string, string>(winner, StringComparer.Ordinal);
        }

        /// <summary>
        /// Create or update triggers in each database according to the parsed SQL statements file.
        /// Existing triggers are dropped before new ones are created.
        /// </summary>
        /// <param name="connectionMap">Map of database name to active connection. If null, connections will be created as needed.</param>
        private static async Task ApplyTriggerTableStatementsAsync(SxmConnection sxmConnection, string dbName)
        {
            await DropTriggersAsync(sxmConnection).ConfigureFalse();
            await AddTriggersAsync(sxmConnection, dbName).ConfigureFalse();
        }

        internal static async Task DropTriggersAsync(SxmConnection sxmConnection)
        {
            // Delete all triggers in the database.
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
            {
                List<string> existingTriggers = await SxmInit.GetAllTriggersAsync(sxmTransaction.Connection, string.Empty).ConfigureFalse();
                foreach (string existingTrigger in existingTriggers)
                {
                   await sxmTransaction.ExecuteCreateTriggerAsync($"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier(existingTrigger)}").ConfigureFalse();
                }

                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
            }
        }

        internal static async Task AddTriggersAsync(SxmConnection sxmConnection, string dbName)
        {
            // Get all triggers in the SQL Statements file and create them.
            List<TriggerDefinition>? triggerStatementsList = SxmSqlStatements.TriggerStatements?[dbName] as List<TriggerDefinition>;

            if (triggerStatementsList != null)
            {
               await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    // Iterate backwards so we can safely remove successful entries without invalidating the iteration.
                    for (int i = triggerStatementsList.Count - 1; i >= 0; i--)
                    {
                        TriggerDefinition td = triggerStatementsList.ElementAt(i);
                        try
                        {
                            await sxmTransaction.ExecuteCreateTriggerAsync(td.TriggerSQL).ConfigureFalse();

                            // If creation succeeded, remove the entry so the list keeps only those that failed.
                            triggerStatementsList.RemoveAt(i);
                        }
                        catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                        {
                            // If not a 'no such table' error — rethrow unchanged so callers/runtime can handle appropriately.
                            if (!ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
                                throw;
                        }
                    }

                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
        }

        /// <summary>
        /// Apply index create/drop statements for a given table key.
        /// </summary>
        /// <param name="DatabaseNameTableName">Qualified key in the form "database.table".</param>
        /// <param name="connectionMap">Map of database name to active connection (updated when new connections are created).</param>
        private static async Task ApplyIndexTableStatementsAsync(string DatabaseNameTableName, SxmConnection sxmConnection)
        {
            List<IndexDefinition>? indexStatementsList = default(List<IndexDefinition>);

            if (SxmSqlStatements.IndexStatements != null)
                indexStatementsList = SxmSqlStatements.IndexStatements[DatabaseNameTableName] as List<IndexDefinition>;

            if (indexStatementsList != null)
            {
                string[] parts = DatabaseNameTableName.Split('.');
                string databaseName = parts[0];
                string tableName = parts[1];

                Hashtable? indexNames = null;
               await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                {
                    await sxmConnection.ExecuteQueryAsync($"PRAGMA index_list({SxmHelpers.QuoteIdentifier(tableName)})", null as List<object>).ConfigureFalse();

                    if (indexStatementsList.Count > 1)
                    {
                        indexNames = new Hashtable();
                        while (sxmConnection.NextRow() == true)
                            indexNames.Add((string)sxmConnection.GetValue("name")!, new Object());
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
                            while (sxmConnection.NextRow() == true)
                            {
                                string indexName = (string)sxmConnection.GetValue("name")!;
                                if (indexName.Equals(indexDefinition.IndexName) == true)
                                {
                                    indexFound = true;
                                    break;
                                }
                            }
                        }

                        if (indexFound == false && indexDefinition.IndexSQL.StartsWith("CREATE ", true, null) == true)
                        {
                            if (DropExists(indexStatementsList, indexDefinition.IndexName) == false)
                                runit = true;
                        }
                        else
                            if (indexFound == true && indexDefinition.IndexSQL.StartsWith("DROP ", true, null) == true)
                            runit = true;

                        if (runit == true)
                        {
                            try
                            {
                                await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                                {
                                    await sxmTransaction1.ExecuteIndexAsync(indexDefinition.IndexSQL).ConfigureFalse();
                                    await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
                                }
                            }
                            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                            {
                                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                                SxmLogging.Log(ex, $"ApplyIndexTableStatementsAsync failure for table '{tableName}' database '{databaseName}' SQL Statement '{indexDefinition.IndexSQL}'.");
                                throw;
                            }
                            catch (System.Exception ex)
                            {
                                string errStr = $"ApplyIndexTableStatementsAsync failure for table '{tableName}' database '{databaseName}' SQL Statement '{indexDefinition.IndexSQL}'.";
                                SxmLogging.Log(ex, errStr);
                                throw ExceptionHelper.Wrap(ex, errStr);
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
        private static bool DropExists(List<IndexDefinition> indexStatementsList, string indexName)
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
        private static async Task AddSynchIdAsync(string[] parts, SxmUTransaction sxmTransaction)
        {
            string alterSQL = $"ALTER TABLE {SxmHelpers.QuoteIdentifier(parts[1])} ADD COLUMN {SxmHelpers.QuoteIdentifier("synchId")} BLOB NOT NULL DEFAULT ''";
            await sxmTransaction.ExecuteTableStatementAsync(alterSQL).ConfigureFalse()  ;
        }

        /// <summary>
        /// Create the _systemCloudSynchDescriptor table if required and insert a descriptor row for the created table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        /// <param name="sxmTransaction">Active transaction used to execute DDL and inserts.</param>
        private static async Task CreateCloudSyncDescriptorAsync(SxmUTransaction sxmTransaction)
        {
            string databaseTable = "_systemCloudSynchDescriptor";

            string tableSQL = $"CREATE TABLE {SxmHelpers.QuoteIdentifier(databaseTable)} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, cloudSynchFlag INTEGER)";
            await sxmTransaction.ExecuteTableStatementAsync(tableSQL).ConfigureFalse();
        }

        /// <summary>
        /// Insert a row into the _systemCloudSynchDescriptor table for the specified table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="databaseName">Database name.</param>
        /// <param name="tableName">Table name.</param>
        /// <param name="sxmTransaction">Active transaction used to execute the insert.</param>
        private static async Task InsertIntoSystemCloudSyncDescriptorAsync(string key, string databaseName, string tableName, SxmUTransaction sxmTransaction)
        {
            TableDefinition tableDefinition = SxmSqlStatements.TableCreateStatements[key] as TableDefinition;
            List<object> parameterValues = new List<object>();
            parameterValues.Add(databaseName);
            parameterValues.Add(tableName);
            parameterValues.Add(tableDefinition.CloudSynch);
            await sxmTransaction.ExecuteSystemUpdateDirectAsync("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues).ConfigureFalse();
        }


        /// <summary>
        /// Create the _systemCloudSynch table used to queue cloud synchronization actions.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database.</param>
        /// <param name="sxmTransaction">Active transaction used to execute the DDL.</param>
        private static async Task CreateCloudSynchTableAsync(SxmUTransaction sxmTransaction)
        {
            string databaseTable = "_systemCloudSynch";

            string tableSQL = $"CREATE TABLE {SxmHelpers.QuoteIdentifier(databaseTable)} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, action TEXT, synchId BLOB)";
            await sxmTransaction.ExecuteTableStatementAsync(tableSQL).ConfigureFalse();
        }

        /// <summary>
        /// Create triggers that populate the _systemCloudSynch queue for insert/update/delete operations on a table.
        /// </summary>
        /// <param name="key">Qualified key "database.table".</param>
        /// <param name="tableNamesMap">Map used to track created table names per database (unused in current implementation).</param>
        /// <param name="sxmTransaction">Active transaction used to create the triggers.</param>
        private static async Task CreateCloudSynchTriggersAsync(string key, SxmUTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = parts[1];

            TableDefinition? tableDefinition = SxmSqlStatements.TableCreateStatements?[key] as TableDefinition;

            if (tableDefinition?.CloudSynch == SxmDefines.CloudSync || tableDefinition?.CloudSynch == SxmDefines.CloudMove)
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
                await sxmTransaction.ExecuteCreateTriggerAsync(triggerUpdateSql).ConfigureFalse();

                if (tableDefinition?.CloudSynch == SxmDefines.CloudSync) // This is weird, needs fixing. How does it make sense for this to be inside this code block?
                {
                    string quotedTriggerDelete = SxmHelpers.QuoteIdentifier("delete" + databaseTable);
                    string triggerDeleteSql =
                        $"CREATE TRIGGER IF NOT EXISTS {quotedTriggerDelete} DELETE ON {quotedTable} " +
                        $"BEGIN INSERT INTO {quotedSystemTable} (dbName, tableName, action, synchId) " +
                        $"VALUES ('{safeDbName}', '{safeTableNameLiteral}', 'delete', old.synchId); END;";
                    await sxmTransaction.ExecuteCreateTriggerAsync(triggerDeleteSql).ConfigureFalse();
                }
            }
        }

        private static async Task<bool> DoesTableExistAsync(string DatabaseNameTableName, SxmConnection sxmConnection, Hashtable tableNamesMap)
        {
            string[] parts = DatabaseNameTableName.Split('.');
            string databaseName = parts[0];
            string tableName = parts[1];

            {
                await sxmConnection.ExecuteQueryAsync("SELECT name FROM sqlite_master WHERE type='table'", null as List<object>).ConfigureFalse();

                ArrayList tableNames = new ArrayList();
                if (sxmConnection.HasRows())
                {
                    string[] fieldNames = sxmConnection.GetFieldNames();
                    while (sxmConnection.NextRow())
                    {
                        foreach (string fieldName in fieldNames)
                            tableNames.Add(sxmConnection.GetValue(fieldName));
                    }
                }

                // Use indexer assignment instead of Add to avoid ArgumentException when the same
                // databaseName is encountered multiple times during initialization.
                tableNamesMap[databaseName] = tableNames;

                if (IsTableInMap(databaseName, tableName, tableNamesMap))
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
        private static bool IsTableInMap(string databaseName, string tableName, Hashtable tableNamesMap)
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
        internal static async Task<List<string>> GetAllTriggersAsync(SxmConnection? sxmConnection, string tableName)
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
                await sxmConnection.ExecuteQueryAsync(sql, parms).ConfigureFalse();

                if (sxmConnection.HasRows() == true)
                {
                    string[] fieldNames = sxmConnection.GetFieldNames();
                    while (sxmConnection.NextRow() == true)
                    {
                        foreach (string fieldName in fieldNames)
                        {
                            object? value = sxmConnection.GetValue(fieldName);

                            // Treat a missing/DBNULL trigger name as an error (fail fast).
                            if (value == null || value == DBNull.Value)
                            {
                                string err = $"sqlite_master returned null trigger name for table '{tableName ?? "<unknown>"}'.";
                                throw new InvalidOperationException(err);
                            }

                            // Normal path: convert to string (guard against unusual ToString implementations).
                            triggerNames.Add(value?.ToString() ?? string.Empty);
                        }
                    }
                }
            }

            return triggerNames;
        }
    }
}