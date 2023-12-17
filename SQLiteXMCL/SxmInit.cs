using System.Collections;

namespace SQLiteXM
{
    public class SxmInit
    {
        //private static Dictionary<string, Synchronize> synchronized = new Dictionary<string, Synchronize>();

        private SxmInit() { }

        public static void initDB()
        {
            SxmInit.initialize();
        }

        public static async Task initDB(string SqlStatementsFileName)
        {
            await parseSqlStatementsFile(SqlStatementsFileName);
            SxmInit.initialize();
        }

        private static async Task parseSqlStatementsFile(string SqlStatementsFileName)
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
        }

        private static bool initialize() // No synchronize.
        {
            new DatabaseDescriptor(ProcessSQLStatements.retreiveDatabaseName);
            return initialize(default(string?));
        }

        private static bool initialize(string? hrAppName)
        {
            Hashtable connectionMap = new Hashtable();
            Hashtable tableNamesMap = new Hashtable();

            try
            {
                double sqlStatementsVersionNumber = ProcessSQLStatements.getSqlStatementsVersionNumber;  // The value in the current SQL statements file.
                double currentDbVersionNumber = getDbVersionNumber();

                if (sqlStatementsVersionNumber > currentDbVersionNumber || sqlStatementsVersionNumber == 0)
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

        public static double getDbVersionNumber()
        {
            double versionNumber = -1;

            try
            {
                string targetFile = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "currentSxmDbVersionNumber.txt");
                using FileStream InputStream = System.IO.File.OpenRead(targetFile);
                using StreamReader reader = new StreamReader(InputStream);

                string vNum = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(vNum))
                    versionNumber = Convert.ToDouble(vNum);
            }
            catch (System.Exception) { }

            return versionNumber;
        }

        public static void deleteDbVersionNumber()
        {
            try
            {
                string filepath = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "currentSxmDbVersionNumber.txt");
                if (System.IO.File.Exists(filepath))
                    System.IO.File.Delete(filepath);
            }
            catch (System.Exception) { }
        }

        private static void storeDbVersionNumber(double versionNumber)
        {
            try
            {
                string targetFile = System.IO.Path.Combine(FileSystem.Current.AppDataDirectory, "currentSxmDbVersionNumber.txt");
                using StreamWriter writer = new StreamWriter(targetFile, append: false);

                writer.Write(versionNumber);
                writer.Close();
            }
            catch (System.Exception) { }
        }

        private static void applyCreateTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
        {
            SxmConnection? sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                sxmConnection = connectionMap[parts[0]] as SxmConnection;
                using (SxmTransaction sxmTransaction = new SxmTransaction(sxmConnection))
                {
                    sxmTransaction.executeTableStatement(tableDefinition.TableSQL);
                    addSynchID(parts, sxmTransaction);

                    addCloudSynchDescriptor(key, tableNamesMap, sxmTransaction);
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
                    sxmConnection.logger.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());

                throw new SxmException(ex);
            }
        }

        private static void applyDropTableStatement(string key, Hashtable connectionMap, TableDefinition tableDefinition, Hashtable tableNamesMap)
        {
            SxmConnection sxmConnection = null;

            try
            {
                string[] parts = key.Split('.');
                sxmConnection = connectionMap[parts[0]] as SxmConnection;
                using (SxmTransaction sxmTransaction = new SxmTransaction(sxmConnection))
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
                    sxmConnection.logger.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());

                throw new SxmException(ex);
            }
        }

        private static void applyAlterTableStatements(string key, Hashtable connectionMap)
        {
            SxmConnection sxmConnection = null;
            ArrayList alterStatementsList = null;

            if (SqlStatements.alterStatements != null)
                alterStatementsList = SqlStatements.alterStatements[key] as ArrayList;

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
                using (SxmTransaction sxmTransaction = new SxmTransaction(sxmConnection))
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

                        if (columnFound == false)
                        {
                            try
                            {
                                using (SxmTransaction sxmTransaction1 = new SxmTransaction(sxmConnection))
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
                                sxmConnection.logger.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
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
            Dictionary<string, string> columnNames = new Dictionary<string, string>();

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

            SxmConnection sxmConnection = new SxmConnection(dbName);
            using (SxmTransaction sxmTransaction = new SxmTransaction(sxmConnection))
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

            return columnNames;
        }

        private static void applyIndexTableStatements(string key, Hashtable connectionMap)
        {
            ArrayList indexStatementsList = null;

            if (SqlStatements.indexStatements != null)
                indexStatementsList = SqlStatements.indexStatements[key] as ArrayList;

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
                using (SxmTransaction sxmTransaction = new SxmTransaction(sxmConnection))
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
                                using (SxmTransaction sxmTransaction1 = new SxmTransaction(sxmConnection))
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
                                sxmConnection.logger.log(ex, System.Reflection.MethodBase.GetCurrentMethod()?.ToString());
                                throw new SxmException(ex);
                            }
                        }
                    }
                }
            }
        }

        private static bool dropExists(ArrayList indexStatementsList, string indexName)
        {
            foreach (IndexDefinition indexDefinition in indexStatementsList)
            {
                if (indexDefinition.IndexName.Equals(indexName) == true)
                    if (indexDefinition.IndexSQL.StartsWith("DROP ", true, null) == true)
                        return true;
            }

            return false;
        }

        private static void addSynchID(string[] parts, SxmTransaction sxmTransaction)
        {
            string alterSQL = String.Format("ALTER TABLE {0} ADD COLUMN systemSynchID TEXT NOT NULL DEFAULT ''", parts[1]);
            sxmTransaction.executeAlterTable(alterSQL);
        }

        private static void addCloudSynchDescriptor(string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = "_systemCloudSynchDescriptor";

            if (isTableInMap(databaseName, databaseTable, tableNamesMap) == false)
            {
                string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, cloudSynchFlag INTEGER)", databaseTable);
                sxmTransaction.executeTableStatement(tableSQL);
                ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
                dbTableNames.Add(databaseTable);
            }

            TableDefinition tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;
            List<object> parameterValues = new List<object>();
            parameterValues.Add(databaseName);
            parameterValues.Add(parts[1]);
            parameterValues.Add(tableDefinition.CloudSynch);
            sxmTransaction.executeSystemUpdateDirect("INSERT INTO _systemCloudSynchDescriptor (dbName, tableName, cloudSynchFlag) VALUES(@p0, @p1, @p2)", parameterValues);
        }

        private static void createCloudSynchTable(string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = "_systemCloudSynch";

            if (isTableInMap(databaseName, databaseTable, tableNamesMap) == false)
            {
                string tableSQL = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT, dbName TEXT, tableName TEXT, action TEXT, systemSynchID TEXT)", databaseTable);
                sxmTransaction.executeTableStatement(tableSQL);
                ArrayList dbTableNames = tableNamesMap[databaseName] as ArrayList;
                dbTableNames.Add(databaseTable);
            }
        }

        private static void createCloudSynchTriggers(string key, Hashtable tableNamesMap, SxmTransaction sxmTransaction)
        {
            string[] parts = key.Split('.');
            string databaseName = parts[0];
            string databaseTable = parts[1];

            TableDefinition? tableDefinition = SqlStatements.tableCreateStatements[key] as TableDefinition;

            if (tableDefinition?.CloudSynch != Defines.NO_CLOUD_SYNCH)
            {
                string tableSQL = String.Format("CREATE TRIGGER IF NOT EXISTS update{0} UPDATE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, systemSynchID) VALUES ('{1}', '{0}', 'update', new.systemSynchID); END;", databaseTable, databaseName);
                sxmTransaction.executeCreateTrigger(tableSQL);
                if (tableDefinition?.CloudSynch == Defines.CLOUD_SYNCH)
                {
                    tableSQL = String.Format("CREATE TRIGGER IF NOT EXISTS delete{0} DELETE ON {0} BEGIN INSERT INTO _systemCloudSynch (dbName, tableName, action, systemSynchID) VALUES ('{1}', '{0}', 'delete', old.systemSynchID); END;", databaseTable, databaseName);
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
    }
}
