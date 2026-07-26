using LinqToDB.Common;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Xml.Linq;
using static SQLiteXM.SxmDefines;
using static SQLiteXM.SxmSerialization;
using static SxmQueryProcessor;
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
    public static class SxmDatabase
    {
        /// <summary>
        /// Cache mapping table name -> (column name -> column type) using thread-safe concurrent dictionaries.
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _columnNameTypes = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.Ordinal);

        /// <summary>
        /// Async gate that serializes database initialization.
        /// </summary>
        /// <remarks>
        /// Only one InitializeAsync call may execute at a time. The initialization process
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
        /// - Initialization flag and cached metadata in SxmDatabase
        /// - Entity schema caches in SxmEntity (column maps, index bags, initialization tasks)
        /// - Database descriptors and SQL statement caches
        /// - Connection manager state
        /// 
        /// After calling this, you must call InitializeAsync again before using any entities.
        /// </remarks>
        public static async Task ResetForTestingAsync()
        {
            await _initGate.WaitAsync().ConfigureFalse();
            try
            {
                // Reset SxmDatabase state
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
                SxmDatabaseOptions.ResetForTesting();

                // Reset schema registration state
                SxmSchemaRegistration.ResetForTesting();

                // Reset column name/type cache
                _columnNameTypes.Clear();

                // Reset connection string cache
                SxmConnection.ResetForTesting();

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
        /// Internal use only. MAUI apps should use the Stream overload with FileSystem.OpenAppPackageFileAsync.
        /// </summary>
        /// <param name="sqlStatementsFileName">Path to the SQL statements file (absolute or relative).</param>
        /// <param name="databaseOptions">Options for configuring the database.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        private static async Task InitializeAsync(string sqlStatementsFileName, SxmDatabaseOptions? databaseOptions = null)
        {
            await _initGate.WaitAsync().ConfigureFalse();
            try
            {
                if (_initialized)
                    return;

                // Throws immediately if any options are invalid, preventing unexpected database or application behavior.
                // Warnings are logged later, after a database specific logger is built, but do not fail initialization.
                SxmDatabaseOptionsValidator.ValidationResult validationResult = ValidateDatabaseOptions(databaseOptions);

                string fullPathToSqlStatementsFile = ResolveSqlStatementsFile(sqlStatementsFileName);
                SqlStatementsFileType fileType = SxmHelpers.GetSqlStatementsFileType(fullPathToSqlStatementsFile);
                if (fileType == SqlStatementsFileType.Unknown)
                    throw new ArgumentException($"'{sqlStatementsFileName}' is an unknown SQL statements file type. The SQL statements file must be JSON or XML.");

                // Only the first call to 'DatabaseFolder' property setter will actually set the 'DatabaseFolder'.
                // Follow on calls to set will be ignored even if the initial setter value is null.
                // CRITICAL: Must set DatabaseFolder BEFORE any SxmDatabaseDescriptor is created.
                SxmDatabaseDescriptor.DatabaseFolder = databaseOptions?.DatabaseFolderOverride;

                {
                    using FileStream stream = File.OpenRead(fullPathToSqlStatementsFile);
                    await ParseSqlStatementsFile(stream, fileType).ConfigureFalse();
                }

                SxmDatabaseOptions.AddDatabaseNames(databaseOptions);
                // At this point, the SQL statements file has been parsed, and all database definitions have been loaded into SxmProcessSQLStatements.
                // Create descriptors for all parsed databases (not just the default).
                SxmDatabaseDescriptor.SxmDatabaseDescriptorFactory();
                await SxmDatabase.BuildSchemaAsync().ConfigureFalse();

                // Log option warnings if any exist. Need  to wait to log warnings until after a database specific logger is built.
                validationResult.LogValidationWarnings();

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
        /// <param name="databaseOptions">Options for configuring the database.</param>
        /// <returns>A task that completes when initialization is finished.</returns>
        public static async Task InitializeAsync(Stream stream, SxmDatabaseOptions? databaseOptions = null)
        {
            await _initGate.WaitAsync().ConfigureFalse();
            try
            {
                if (_initialized)
                    return;

                // Throws immediately if any options are invalid, preventing unexpected database or application behavior.
                // Warnings are logged later, after a database specific logger is built, but do not fail initialization.
                SxmDatabaseOptionsValidator.ValidationResult validationResult = ValidateDatabaseOptions(databaseOptions);

                // Only the first call to 'DatabaseFolder' property setter will actually set the 'DatabaseFolder'.
                // Follow on calls to set will be ignored even if the initial setter value is null.
                // CRITICAL: Must set DatabaseFolder BEFORE any SxmDatabaseDescriptor is created.
                SxmDatabaseDescriptor.DatabaseFolder = databaseOptions?.DatabaseFolderOverride;

                await ParseSqlStatementsFile(stream, SqlStatementsFileType.Unknown).ConfigureFalse();

                SxmDatabaseOptions.AddDatabaseNames(databaseOptions);
                // At this point, the SQL statements file has been parsed, and all database definitions have been loaded into SxmProcessSQLStatements.
                // Create descriptors for all parsed databases (not just the default).
                SxmDatabaseDescriptor.SxmDatabaseDescriptorFactory();
                await SxmDatabase.BuildSchemaAsync().ConfigureFalse();

                // Log option warnings if any exist. Need  to wait to log warnings until after a database specific logger is built.
                validationResult.LogValidationWarnings();

                // Mark initialization complete only after the full pipeline succeeds.
                // If any step throws, _initialized remains false so a later call can retry.
                _initialized = true;
            }
            finally
            {
                _initGate.Release();
            }
        }

        private static SxmDatabaseOptionsValidator.ValidationResult ValidateDatabaseOptions(SxmDatabaseOptions? databaseOptions)
        {
            // Validate options (null is valid - means use all defaults). Only validates properties that are explicitly set (non-null).
            SxmDatabaseOptionsValidator.ValidationResult validationResult = SxmDatabaseOptionsValidator.Validate(databaseOptions);

            // Throw if validation failed (errors found)
            validationResult.ThrowIfValidationErrors();

            return validationResult;
        }

        /// <summary>
        /// Register entity types and create/migrate their schemas at application startup.
        /// </summary>
        /// <param name="entityTypes">Array of SxmEntity-derived types to register.</param>
        /// <remarks>
        /// Call this once at app startup (e.g., in App.xaml.cs or MauiProgram.cs) after calling <see cref="InitializeAsync"/>.
        /// All tables, indexes, triggers, and foreign keys will be created/migrated.
        /// 
        /// This method replaces the legacy constructor-based schema initialization pattern.
        /// Entity classes registered via this method will not trigger schema creation on instantiation.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if SQLiteXM has not been initialized via <see cref="InitializeAsync"/>.</exception>
        /// <exception cref="ArgumentException">Thrown if any type does not derive from <see cref="SxmEntity"/> or is abstract.</exception>
        public static async Task RegisterEntitiesAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] params Type[] entityTypes)
        {
            EnsureInitialized();

            if (entityTypes == null || entityTypes.Length == 0)
                return;

            // Register all of the entities.
            foreach (var type in entityTypes)
            {
                await SxmSchemaRegistration.RegisterEntitySchemaAsync(type).ConfigureFalse();
            }

            // Add a check to see if there are any unassigned triggers in the TriggerStatements collection. This would indicate
            // that there are triggers defined in the SQL statements file that were not applied to any registered entities, which could
            // be a configuration error worth warning about.
            if (SxmSqlStatements.TriggerStatements.Count > 0 && SxmSqlStatements.TriggerStatements.Any(kvp => kvp.Value.Count > 0))
            {
                IEnumerable<string?> unassignedTriggers = SxmSqlStatements.TriggerStatements
                    .SelectMany((KeyValuePair<string, List<TriggerDefinition>> kvp) => kvp.Value.Select((triggerDefinition, index) => new
                    {
                        Database = kvp.Key,
                        TriggerDefinition = triggerDefinition,
                        Index = index
                    }))
                    .Select((item, i) => $"  [{i + 1}] Unknown Table Name: '{item.TriggerDefinition.TableName}'{Environment.NewLine}      Database: '{item.Database}'{Environment.NewLine}      Trigger SQL: {item.TriggerDefinition.TriggerSQL}");

                string message = $"Check that trigger source table names match registered entity table names.{Environment.NewLine}" + string.Join(Environment.NewLine, unassignedTriggers);
                SxmLogging.Log(new SxmWarning(message), "Warning: Unassigned trigger(s) detected", nameof(RegisterEntitiesAsync));
            }
        }

        /// <summary>
        /// Ensures that the SQLiteXM initialization process has completed.
        /// </summary>
        /// <remarks>
        /// Entity classes and database operations require the ORM to be initialized
        /// via <see cref="SxmDatabase.InitializeAsync"/> before use. This method provides a
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
                    "SQLiteXM has not been initialized. Call SxmDatabase.InitializeAsync(...) before instantiating entity classes.");
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
        private static async Task<bool> BuildSchemaAsync()
        {
            //if (sqlStatementsVersionNumber > currentDbVersionNumber || sqlStatementsVersionNumber == 0)
            long sqlStatementsVersionNumber = SxmProcessSQLStatements.SqlStatementsVersionNumber;  // The value in the current SQL statements file.
            long currentDbVersionNumber = await GetDbVersionNumberAsync(SxmProcessSQLStatements.DefaultDatabaseName).ConfigureFalse();

            SxmConnection? sxmConnection = null;
            foreach (string databaseName in SxmProcessSQLStatements.Databases)
            {
                try
                {
                    // Create system tables in ALL databases, not just the default
                    await CreateSystemTablesAsync(databaseName).ConfigureFalse();

                    sxmConnection = new SxmConnection(databaseName, shared: true);

                    // One time cleanup of all existing triggers before applying any new statements. This ensures that triggers for dropped tables or
                    // columns are removed, and prevents conflicts with triggers being recreated later in this method or in SxmSchemaRegistration.
                    await DropTriggersAsync(sxmConnection, new List<string>()).ConfigureFalse();
                    await StoreDbVersionNumberAsync(sqlStatementsVersionNumber, databaseName, sxmConnection).ConfigureFalse();
                    await SxmAssociationMapper.InitializeAssociationsAsync(databaseName).ConfigureFalse();
                }
                catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                    SxmLogging.Log(ex, $"InitializeAsync failure. Database: '{databaseName}'.");
                    throw;
                }
                catch (System.Exception ex)
                {
                    string errStr = $"InitializeAsync failure. Database: '{databaseName}'.";
                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
                finally
                {
                    if (sxmConnection is not null)
                    {
                        await sxmConnection.DestroyConnectionAsync().ConfigureFalse();
                    }

                    SxmSqlStatements.ClearStatementTables();
                }
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

                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
            }
        }

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
                SxmLogging.Log(ex, $"GetDbVersionNumberAsync failure. Database: '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetDbVersionNumberAsync failure. Database: '{databaseName}'.";
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
                SxmLogging.Log(ex, $"DeleteDbVersionNumberAsync failure. Database: '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DeleteDbVersionNumberAsync failure. Database: '{databaseName}'.";
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
                SxmLogging.Log(ex, $"StoreDbVersionNumberAsync failure. Database: '{databaseName}'. Version number: '{versionNumber}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"StoreDbVersionNumberAsync failure. Database: '{databaseName}'. Version number: '{versionNumber}'.";
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
                        await SxmDatabase.AddSynchIdAsync(parts, sxmTransaction).ConfigureFalse();

                        await SxmDatabase.InsertIntoSystemCloudSyncDescriptorAsync(key, databaseName, parts[1], sxmTransaction).ConfigureFalse();

                        await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"CreateTableAsync failure. Database: '{databaseName}'. Table: '{tableName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"CreateTableAsync failure. Database: '{databaseName}'. Table: '{tableName}'.";
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
                SxmLogging.Log(ex, $"DoesTableExistAsync failure for table '{tableName}'. Database '{sxmConnection.DatabaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DoesTableExistAsync failure for table '{tableName}'. Database '{sxmConnection.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
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
            string? tableName = null;

            if (sqlStatementType == SxmDefines.SqlStatementType.Select)
                tableName = SxmSqlStatements.SelectStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Insert)
                tableName = SxmSqlStatements.InsertStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Update)
                tableName = SxmSqlStatements.UpdateStatements[queryName].TableName;
            if (sqlStatementType == SxmDefines.SqlStatementType.Delete)
                tableName = SxmSqlStatements.DeleteStatements[queryName].TableName;

            if (tableName == null)
            {
                SqlStatementDetails? embeddedSqlStatementDetails = null;
                if (sqlStatementType == SxmDefines.SqlStatementType.SelectDirect)
                    embeddedSqlStatementDetails = SxmQueryProcessor.AnalyzeUserQuery(queryName, dbName);
                if (sqlStatementType == SxmDefines.SqlStatementType.InsertDirect)
                    embeddedSqlStatementDetails = SxmQueryProcessor.AnalyzeUserQuery(queryName, dbName);
                if (sqlStatementType == SxmDefines.SqlStatementType.UpdateDirect)
                    embeddedSqlStatementDetails = SxmQueryProcessor.AnalyzeUserQuery(queryName, dbName);
                if (sqlStatementType == SxmDefines.SqlStatementType.DeleteDirect)
                    embeddedSqlStatementDetails = SxmQueryProcessor.AnalyzeUserQuery(queryName, dbName);

                if (embeddedSqlStatementDetails != null)
                    tableName = embeddedSqlStatementDetails.TargetTableName;
            }

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

#if DEBUG
        /// <summary>
        /// Clear the cached column name/type mapping for a specific table.
        /// **WARNING:** This is intended ONLY for testing scenarios.
        /// </summary>
        /// <param name="tableName">Table name whose column cache should be cleared.</param>
        internal static void ClearColumnCacheForTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            _columnNameTypes.TryRemove(tableName, out _);
        }
#endif

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

        internal static async Task DropTriggersAsync(SxmConnection sxmConnection, List<string> statementsList)
        {
            // Delete all triggers in the database.
            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
            {
                List<string> existingTriggers = await SxmDatabase.GetAllTriggersAsync(sxmTransaction.Connection, string.Empty).ConfigureFalse();
                foreach (string existingTrigger in existingTriggers)
                {
                    string dropTriggerStatement = $"DROP TRIGGER IF EXISTS {SxmHelpers.QuoteIdentifier(existingTrigger)}";
                    await sxmTransaction.ExecuteCreateTriggerAsync(dropTriggerStatement).ConfigureFalse();
                    statementsList.Add(dropTriggerStatement);
                }

                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
            }
        }

        internal static async Task AddTriggersAsync(SxmConnection sxmConnection, string dbName, string tableName, List<string> statementsList)
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
                        TriggerDefinition triggerDefinition = triggerStatementsList.ElementAt(i);
                        try
                        {
                            if (string.Equals(triggerDefinition.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                            {
                                await sxmTransaction.ExecuteCreateTriggerAsync(triggerDefinition.TriggerSQL).ConfigureFalse();

                                // If creation succeeded, remove the entry so the list keeps only those that failed.
                                triggerStatementsList.RemoveAt(i);
                                statementsList.Add(triggerDefinition.TriggerSQL);
                            }
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
            string alterSQL = $"ALTER TABLE {SxmHelpers.QuoteIdentifier(parts[1])} ADD COLUMN {SxmHelpers.QuoteIdentifier("synchId")} BLOB DEFAULT (randomblob(16))";
            await sxmTransaction.ExecuteTableStatementAsync(alterSQL).ConfigureFalse();
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
            if (SxmSqlStatements.TableCreateStatements == null || !SxmSqlStatements.TableCreateStatements.TryGetValue(key, out TableDefinition? tableDefinition) || tableDefinition == null)
                throw new InvalidOperationException($"Table definition not found for key: {key}");

            List<object> parameterValues = new List<object>();
            parameterValues.Add(databaseName);
            parameterValues.Add(tableName);
            parameterValues.Add(tableDefinition.CloudSynch);
            await sxmTransaction.ExecuteSystemUpdateDirectAsync("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues).ConfigureFalse();
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

            if (sxmConnection is not null)
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