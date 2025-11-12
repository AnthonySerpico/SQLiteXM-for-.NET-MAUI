using System.Collections;

namespace SQLiteXM
{
    public class SxmInit
    {
        //private static Dictionary<string, Synchronize> synchronized = new Dictionary<string, Synchronize>();
        private static Dictionary<string, Dictionary<string, string>> columnNameTpyes = new Dictionary<string, Dictionary<string, string>>();

        private SxmInit() { }

        public static void initDB()
        {
            SxmInit.initialize();
        }

        public static async Task initDB(string SqlStatementsFileName, Defines.SqlStatementsFileType fileType)
        {
            await parseSqlStatementsFile(SqlStatementsFileName, fileType);
            SxmInit.initialize();
        }

        public static async Task initDB(Stream stream, Defines.SqlStatementsFileType fileType)
        {
            await parseSqlStatementsFile(stream, fileType);
            SxmInit.initialize();
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
        private static async Task parseSqlStatementsFile(string fileName, Defines.SqlStatementsFileType fileType)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            var fullPath = ResolveToBase(fileName);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException(
                    $"The SQL statements file '{fileName}' could not be found. Resolved path: {fullPath}", fullPath);

            await using var stream = File.OpenRead(fullPath);
            await parseSqlStatementsFile(stream, fileType);
        }

        /// <summary>
        /// Parse SQL statement definitions from an open, readable stream.
        /// </summary>
        /// <param name="stream">An open, readable stream positioned at the beginning of the SQL definitions.</param>
        /// <param name="fileType">The format of the SQL definitions (json, xml, or txt).</param>
        /// <exception cref="ArgumentNullException">stream is null.</exception>
        /// <exception cref="ArgumentException">stream is not readable.</exception>
        private static Task parseSqlStatementsFile(Stream stream, Defines.SqlStatementsFileType fileType)
        {
            if (stream is null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

            switch (fileType)
            {
                case Defines.SqlStatementsFileType.json:
                    ProcessSQLStatements.Parse(stream, Defines.SqlStatementsFileType.json);
                    break;

                case Defines.SqlStatementsFileType.xml:
                    ProcessSQLStatements.Parse(stream, Defines.SqlStatementsFileType.xml);
                    break;

                case Defines.SqlStatementsFileType.txt:
                default:
                    using (var reader = new StreamReader(stream, leaveOpen: false))
                        ProcessSQLStatements.Parse(reader);
                    break;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Resolves a relative path against the application's base directory.
        /// </summary>
        private static string ResolveToBase(string path) => Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);

        /*private static async Task parseSqlStatementsFile(string SqlStatementsFileName)
        {
            if (await FileSystem.AppPackageFileExistsAsync(SqlStatementsFileName).ConfigureAwait(false))
            {
                using (Stream stream = await FileSystem.OpenAppPackageFileAsync(SqlStatementsFileName).ConfigureAwait(false))
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

        private static bool initialize() // No synchronize.
        {
            new DatabaseDescriptor();
            return initialize(default(string?));
        }

        private static bool initialize(string? hrAppName)
        {
            Hashtable connectionMap = new Hashtable();
            Hashtable tableNamesMap = new Hashtable();

            try
            {
                long sqlStatementsVersionNumber = ProcessSQLStatements.getSqlStatementsVersionNumber;  // The value in the current SQL statements file.
                long currentDbVersionNumber = getDbVersionNumber();

                if (sqlStatementsVersionNumber > currentDbVersionNumber || sqlStatementsVersionNumber == 0)
                {
                    if (SqlStatements.tableCreateStatements != default(Dictionary<string, TableDefinition>))
                    {
                        foreach (string key in SqlStatements.tableCreateStatements.Keys)
                        {
                            if (doesTableExist(key, connectionMap, tableNamesMap) == false)
                            {
                                TableDefinition tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("CREATE ", true, null) == true)
                                    applyCreateTableStatement(key, connectionMap, tableDefinition, tableNamesMap);
                            }
                            else
                            {
                                TableDefinition tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;
                                if (tableDefinition.TableSQL.StartsWith("DROP ", true, null) == true)
                                    applyDropTableStatement(key, connectionMap, tableDefinition, tableNamesMap);
                                else
                                {
                                    applyAlterTableStatements(key, connectionMap);
                                    applyIndexTableStatements(key, connectionMap);
                                }
                            }
                        }
                    }

                    applyTriggerTableStatements(connectionMap);
                    storeDbVersionNumber(sqlStatementsVersionNumber);
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
                /*foreach (string databaseName in connectionMap.Keys) 
				{
					SxmConnection conn = connectionMap [databaseName] as SxmConnection;
					if (synchSettings.Synch != null) 
					{
						Synchronize synchronize = Synchronize.createSynchronize (conn, hrAppName, synchSettings);
						if (synchronize != null) 
							synchronized.Add (databaseName, synchronize);
					}
					if (conn != null)
						conn.destroyConnection ();
				}*/

                SqlStatements.clearStatementTables();
            }

            return true;
        }

        /*		public static void interruptSynchronize (string databaseName)
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

        public static long getDbVersionNumber()
        {
            long versionNumber = -1;

            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(ProcessSQLStatements.retreiveDatabaseName);
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery("PRAGMA user_version", default(List<object>));

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

        public static void deleteDbVersionNumber()
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(ProcessSQLStatements.retreiveDatabaseName);
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery("PRAGMA user_version = 0", default(List<object>));
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        private static void storeDbVersionNumber(long versionNumber)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(ProcessSQLStatements.retreiveDatabaseName);
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery(String.Format("PRAGMA user_version = {0}", versionNumber), default(List<object>));
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        private static void applyCreateTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
        {
            SxmConnection? sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                sxmConnection = connectionMap[parts[0]] as SxmConnection;
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmTransaction.executeTableStatement(tableDefinition.TableSQL);
                    addSynchID(parts, sxmTransaction);

                    if (!doesTableExist("_systemCloudSynchDescriptor", sxmConnection))
                        addCloudSynchDescriptor(key, tableNamesMap, sxmTransaction);
                    if (!doesTableExist("_systemCloudSynch", sxmConnection))
                        createCloudSynchTable(key, tableNamesMap, sxmTransaction);
                    createCloudSynchTriggers(key, tableNamesMap, sxmTransaction);
                    sxmTransaction.commitTransaction();
                }
                applyIndexTableStatements(key, connectionMap);
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

        internal static void createTable(string databaseName, string tableName)
        {
            string[] parts = { databaseName, tableName };
            string key = string.Format("{0}.{1}", databaseName, tableName);

            SxmConnection? sxmConnection = default(SxmConnection);

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                if (!doesTableExist(tableName, sxmConnection))
                {
                    Hashtable tableNamesMap = new Hashtable();
                    TableDefinition? tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;

                    using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                    {
                        sxmTransaction.executeTableStatement(tableDefinition.TableSQL);
                        SxmInit.addSynchID(parts, sxmTransaction);

                        if (!doesTableExist("_systemCloudSynchDescriptor", sxmConnection))
                            SxmInit.addCloudSynchDescriptor(key, tableNamesMap, sxmTransaction);

                        SxmInit.insertIntoSystemCloudSyncDescriptor(key, databaseName, parts[1], sxmTransaction);

                        if (!doesTableExist("_systemCloudSynch", sxmConnection))
                            SxmInit.createCloudSynchTable(key, tableNamesMap, sxmTransaction);

                        //SxmInit.createCloudSynchTriggers(key, tableNamesMap, sxmTransaction);

                        sxmTransaction.commitTransaction();
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

        internal static bool doesTableExist(string tableName, SxmConnection? sxmConnection)
        {
            bool connectionCreated = false;
            try
            {
                if (sxmConnection == default(SxmConnection))
                {
                    sxmConnection = new SxmConnection(ProcessSQLStatements.retreiveDatabaseName);
                    connectionCreated = true;
                }

                string sqlSelect = string.Format("SELECT name FROM sqlite_master WHERE type='table' AND name='{0}'", tableName);
                sxmConnection.executeQuery(sqlSelect, default(List<object>));
                if (sxmConnection.hasRows() == true)
                    return true;
            }
            catch (Exception)
            {
            }
            finally 
            {
                if(connectionCreated)
                    sxmConnection?.destroyConnection();
             }

            return false;
        }

        private static void applyDropTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
        {
            SxmConnection sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                sxmConnection = connectionMap[parts[0]] as SxmConnection;
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmTransaction.executeTableStatement(tableDefinition.TableSQL);
                    sxmTransaction.executeTableStatement(string.Format("DROP TRIGGER IF EXISTS update{0}", parts[1]));
                    sxmTransaction.executeTableStatement(string.Format("DROP TRIGGER IF EXISTS delete{0}", parts[1]));

                    sxmTransaction.commitTransaction();
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
        private static void applyAlterTableStatements(string key, Hashtable connectionMap)
        {
            SxmConnection sxmConnection = null;
            List<AlterDefinition> alterStatementsList = null;

            if (SqlStatements.alterStatements != null)
                alterStatementsList = SqlStatements.alterStatements[key] as List<AlterDefinition>;

            if (alterStatementsList != null)
            {
                string[] parts = key.Split('.');

                sxmConnection = connectionMap[parts[0]] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(parts[0]);
                    connectionMap.Add(parts[0], sxmConnection);
                }

                Hashtable columnNames = null;
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery(String.Format("PRAGMA table_info({0})", parts[1]), default(List<object>));

                    if (alterStatementsList.Count > 1)
                    {
                        columnNames = new Hashtable();
                        while (sxmConnection.nextRow() == true)
                            columnNames.Add((string)sxmConnection.getValue("name"), new Object());
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
                                string columnName = (string)sxmConnection.getValue("name");
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
                                if(lowerSqlStatement.IndexOf(" drop ") != -1 || lowerSqlStatement.IndexOf(" rename ") != -1)
                                    runit = true;
                            }
                        }

                        if (runit)
                        {
                            try
                            {
                                using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                                {
                                    sxmTransaction1.executeAlterTable(alterDefinition.AlterSQL);
                                    sxmTransaction1.commitTransaction();
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

        internal static Dictionary<string, string> getTableColumnNames(string? dbName, string queryName, Defines.SqlStatementType sqlStatementType)
        {
            string? tableName = default(string);

            if (sqlStatementType == Defines.SqlStatementType.select)
                tableName = SqlStatements.selectStatements[queryName].TableName;
            if (sqlStatementType == Defines.SqlStatementType.insert)
                tableName = SqlStatements.insertStatements[queryName].TableName;
            if (sqlStatementType == Defines.SqlStatementType.update)
                tableName = SqlStatements.updateStatements[queryName].TableName;
            if (sqlStatementType == Defines.SqlStatementType.delete)
                tableName = SqlStatements.deleteStatements[queryName].TableName;
            if (sqlStatementType == Defines.SqlStatementType.unknown)
                throw new SxmException(new ErrorMessage("unknownSQLStatement", queryName));

            return getTableColumnNames(dbName, tableName);
        }

        internal static void addColumnNameType(string tableName, string columnName, string columnType)
        {
            Dictionary<string, string>? columnNameType = default(Dictionary<string, string>);

            if (columnNameTpyes.ContainsKey(tableName))
            {
                columnNameType = columnNameTpyes[tableName];
                columnNameType.Add(columnName, columnType); 
            }
        }

        internal static void removeColumnNameType(string tableName, string columnName)
        {
            Dictionary<string, string>? columnNameType = default(Dictionary<string, string>);

            if (columnNameTpyes.ContainsKey(tableName))
            {
                columnNameType = columnNameTpyes[tableName];
                columnNameType.Remove(columnName);
            }
        }

        internal static Dictionary<string, string> getTableColumnNames(string? dbName, string? tableName)
        {
            if (columnNameTpyes.ContainsKey(tableName))
                return columnNameTpyes[tableName];

            Dictionary<string, string> columnNames = new Dictionary<string, string>();

            SxmConnection sxmConnection = new SxmConnection(dbName);
            using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
            {
                sxmConnection.executeQuery(String.Format("PRAGMA table_info({0})", tableName), default(List<object>));

                while (sxmConnection.nextRow() == true)
                {
                    string? columnName = (string?)sxmConnection.getValue("name");
                    string? columnType = (string?)sxmConnection.getValue("type");
                    if (columnName != null && columnType != null)
                        columnNames.Add(columnName, columnType);
                }
            }

            columnNameTpyes.Add(tableName, columnNames);
            return columnNames;

        }

        internal static void applyTriggerTableStatements(Hashtable? connectionMap)
        {
            List<TriggerDefinition>? triggerStatementsList = default(List<TriggerDefinition>);

            if (SqlStatements.triggerStatements != null)
            {
                ICollection triggerStatementKeys = SqlStatements.triggerStatements.Keys;
                foreach (string dbName in triggerStatementKeys)
                {
                    if(connectionMap == default(Hashtable))
                        connectionMap = new Hashtable();
                    if (connectionMap.Count == 0)
                        connectionMap.Add(dbName, new SxmConnection(dbName));

                    SxmConnection? sxmConnection = (SxmConnection?)connectionMap[dbName];
                    if (sxmConnection != null)
                    {
                        List<string> installedTriggers = getAllTriggers(sxmConnection);
                        triggerStatementsList = SqlStatements.triggerStatements[dbName] as List<TriggerDefinition>;

                        if (triggerStatementsList != null)
                        {
                            using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                            {
                                foreach (TriggerDefinition td in triggerStatementsList)
                                {
                                    if (td.TriggerSQL.StartsWith("drop ", true, null))
                                    {
                                        if (installedTriggers.Contains(td.TriggerName))
                                            sxmTransaction.executeCreateTrigger(td.TriggerSQL);
                                    }
                                    else
                                    {
                                        if (!installedTriggers.Contains(td.TriggerName))
                                            sxmTransaction.executeCreateTrigger(td.TriggerSQL);
                                    }

                                }
                                sxmTransaction.commitTransaction();
                            }
                        }
                    }
                }
            }
        }

        internal static void applyIndexTableStatements(string key, Hashtable connectionMap)
        {
            List<IndexDefinition>? indexStatementsList = default(List<IndexDefinition>);

            if (SqlStatements.indexStatements != null)
                indexStatementsList = SqlStatements.indexStatements[key] as List<IndexDefinition>;

            if (indexStatementsList != null)
            {
                string[] parts = key.Split('.');
                SxmConnection sxmConnection = connectionMap[parts[0]] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(parts[0]);
                    connectionMap.Add(parts[0], sxmConnection);
                }


                Hashtable indexNames = null;
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery(String.Format("PRAGMA index_list({0})", parts[1]), null as List<object>);

                    if (indexStatementsList.Count > 1)
                    {
                        indexNames = new Hashtable();
                        while (sxmConnection.nextRow() == true)
                            indexNames.Add((string)sxmConnection.getValue("name"), new Object());
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
                                string indexName = (string)sxmConnection.getValue("name");
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
                                using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                                {
                                    sxmTransaction1.executeIndex(indexDefinition.IndexSQL);
                                    sxmTransaction1.commitTransaction();
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

        internal static void addSynchID(string[] parts, SxmUTransaction sxmTransaction)
        {
            string alterSQL = String.Format("ALTER TABLE {0} ADD COLUMN synchId TEXT NOT NULL DEFAULT ''", parts[1]);
            sxmTransaction.executeAlterTable(alterSQL);
        }

        internal static void addCloudSynchDescriptor(string key, Hashtable tableNamesMap, SxmUTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = "_systemCloudSynchDescriptor";

            //if (isTableInMap(databaseName, databaseTable, tableNamesMap) == false)
            {
                string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, cloudSynchFlag INTEGER)", databaseTable);
                sxmTransaction.executeTableStatement(tableSQL);
                ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
                if(dbTableNames != default(ArrayList))
                    dbTableNames.Add(databaseTable);
            }

            insertIntoSystemCloudSyncDescriptor(key, databaseName, parts[1], sxmTransaction);
        }

        internal static void insertIntoSystemCloudSyncDescriptor(string key, string databaseName, string tableName, SxmUTransaction sxmTransaction)
        {
            TableDefinition tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;
            List<object> parameterValues = new List<object>();
            parameterValues.Add(databaseName);
            parameterValues.Add(tableName);
            parameterValues.Add(tableDefinition.CloudSynch);
            sxmTransaction.executeSystemUpdateDirect("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues);
        }


        internal static void createCloudSynchTable(string key, Hashtable tableNamesMap, SxmUTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = "_systemCloudSynch";

            //if (isTableInMap(databaseName, databaseTable, tableNamesMap) == false)
            {
                string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, action TEXT, synchId TEXT)", databaseTable);
                sxmTransaction.executeTableStatement(tableSQL);
                ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
                if (dbTableNames != default(ArrayList))
                    dbTableNames.Add(databaseTable);
            }
        }

        internal static void createCloudSynchTriggers(string key, Hashtable tableNamesMap, SxmUTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = parts[1];

            TableDefinition? tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;

            if (tableDefinition?.CloudSynch != Defines.NO_CLOUD_SYNCH)
            {
                string tableSQL = String.Format("CREATE TRIGGER IF NOT EXISTS update{0} UPDATE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, synchId) VALUES ('{1}', '{0}', 'update', new.synchId); END;", databaseTable, databaseName);
                sxmTransaction.executeCreateTrigger(tableSQL);
                if (tableDefinition?.CloudSynch == Defines.CLOUD_SYNCH)
                {
                    tableSQL = String.Format("CREATE TRIGGER IF NOT EXISTS delete{0} DELETE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, synchId) VALUES ('{1}', '{0}', 'delete', old.synchId); END;", databaseTable, databaseName);
                    sxmTransaction.executeCreateTrigger(tableSQL);
                }
            }
        }

        private static bool doesTableExist(string key, Hashtable connectionList, Hashtable tableNamesMap)
        {
            string[] parts = key.Split('.');
            if (parts.Length != 2)
            {
                throw new SxmException(new ErrorMessage("invalidTableName", key));
            }
            else
            {
                string databaseName = parts[0];
                string databaseTable = parts[1];

                SxmConnection sxmConnection = connectionList[databaseName] as SxmConnection;
                if (sxmConnection == null)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    connectionList.Add(databaseName, sxmConnection);

                    sxmConnection.executeQuery("SELECT name FROM sqlite_master WHERE type='table'", null as List<object>);

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

                }

                if (isTableInMap(databaseName, databaseTable, tableNamesMap) == true)
                    return true;
            }

            return false;
        }

        private static bool isTableInMap(string databaseName, string tableName, Hashtable tableNamesMap)
        {
            ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
            if (dbTableNames != null)
            {
                foreach (string dbTableName in dbTableNames)
                {
                    if (dbTableName.Equals(tableName) == true)
                        return true;
                }
            }

            return false;
        }

        internal static List<string> getAllTriggers(SxmConnection? sxmConnection)
        {
            List<string> triggerNames = new List<string>();

            if (sxmConnection != null)
            {
                sxmConnection.executeQuery("SELECT name FROM sqlite_master WHERE type='trigger'", null as List<object>);

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
