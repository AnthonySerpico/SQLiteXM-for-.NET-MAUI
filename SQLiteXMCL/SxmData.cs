using SQLitePCL;
using System.Collections;
using System.Reflection;

namespace SQLiteXM
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CreateIndex : Attribute
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }

        public CreateIndex(string[] indexFields, string indexName)
        {
            this.indexFields = indexFields;
            this.indexName = indexName;
        }
    }

    public class SxmData
    {
        private bool mustReconcile = true;
        private static object lockObject = new object();
        private static string? insertGuid = default(string);
        private static string? updateGuid = default(string);
        private static string? deleteGuid = default(string);
        private string? databaseName = SxmConnection.ImplicitDatabaseName;
        private static Dictionary<string, string> columnNameAndType = new Dictionary<string, string>();

        public virtual long id { get; set; }
        public virtual string synchId { get; set; }

        public SxmData(string? databaseName)
        {
            this.databaseName = databaseName;
            initialize();
        }
        public SxmData()
        {
            initialize();
        }

        private void initialize()
        {
            lock (lockObject)
            {
                dbNameValidation();

                if (columnNameAndType.Count == 0)
                {
                    createTable();
                    reconcile();
                    processIndexAttributes();
                }
            }
        }

        public async Task SaveOrUpdate()
        {
            if (!doesRecordExist())
                await Save();
            else
                await Update();
        }

        public async Task SaveOrUpdate(SxmTransaction sxmTrans)
        {
            if (!doesRecordExist())
                await Save(sxmTrans);
            else
                await Update(sxmTrans);
        }

        public async Task Save(string sqlStatementName)
        {
            if (!doesRecordExist())
            {
                Dictionary<string, object?> result = await SxmStatement.PerformInsert<SxmData>(sqlStatementName, this, databaseName);
                loadDbValues(result);
            }
        }
        public async Task Save(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (!doesRecordExist())
            {
                Dictionary<string, object?> result = await sxmTrans.PerformInsert<SxmData>(sqlStatementName, this);
                loadDbValues(result);
            }
        }

        public async Task Update(string sqlStatementName)
        {
            if (doesRecordExist())
                await SxmStatement.PerformUpdate<SxmData>(sqlStatementName, this, databaseName);
        }

        public async Task Update(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (doesRecordExist())
                await sxmTrans.PerformUpdate<SxmData>(sqlStatementName, this);
        }

        public async Task Delete(string sqlStatementName)
        {
            if (doesRecordExist())
                await SxmStatement.PerformDelete<SxmData>(sqlStatementName, this, databaseName);
        }
        public async Task Delete(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (doesRecordExist())
                await sxmTrans.PerformDelete<SxmData>(sqlStatementName, this);
        }

        public async Task Save()
        {
            buildSaveSql();
            await Save(insertGuid);
        }

        public async Task Save(SxmTransaction sxmTrans)
        {
            buildSaveSql();
            await Save(insertGuid, sxmTrans);
        }

        public async Task Update()
        {
            buildUpdateSql();
            await Update(updateGuid);
        }

        public async Task Update(SxmTransaction sxmTrans)
        {
            buildUpdateSql();
            await Update(updateGuid, sxmTrans);
        }

        public async Task Delete()
        {
            buildDeleteSql();
            await Delete(deleteGuid);
        }

        public async Task Delete(SxmTransaction sxmTrans)
        {
            buildDeleteSql();
            await Delete(deleteGuid, sxmTrans);
        }

        private void buildSaveSql()
        {
            if (insertGuid == default(string))
            {
                string insertColumns = string.Empty;
                string insertValues = string.Empty;

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                {
                    if (!kvp.Key.Equals("synchId") && !kvp.Key.Equals("id"))
                    {
                        if (i > 0)
                        {
                            insertColumns += string.Format(", {0}", kvp.Key);
                            insertValues += string.Format(", @{0}", kvp.Key);
                        }
                        else
                        {
                            insertColumns += string.Format("{0}", kvp.Key);
                            insertValues += string.Format("@{0}", kvp.Key);
                        }
                        ++i;
                    }
                }

                string insertStatement = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", this.GetType().Name, insertColumns, insertValues);
                insertGuid = Guid.NewGuid().ToString();
                SqlStatements.addInsertDefinition(insertGuid, this.GetType().Name, insertStatement);
            }
        }

        private void buildUpdateSql()
        {
            if (updateGuid == default(string))
            {
                string insertColumns = string.Empty;

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                {
                    if (!kvp.Key.Equals("synchId") && !kvp.Key.Equals("id"))
                    {
                        if (i > 0)
                            insertColumns += string.Format(", {0}=@{1}", kvp.Key, kvp.Key);
                        else
                            insertColumns += string.Format("{0}=@{1}", kvp.Key, kvp.Key);

                        ++i;
                    }
                }

                string updateStatement = string.Format("UPDATE {0} SET {1} WHERE id=@id", this.GetType().Name, insertColumns);
                updateGuid = Guid.NewGuid().ToString();
                SqlStatements.addUpdateDefinition(updateGuid, this.GetType().Name, updateStatement);
            }
        }

        private void buildDeleteSql()
        {
            if (deleteGuid == default(string))
            {
                string updateStatement = string.Format("DELETE FROM {0} WHERE id=@id", this.GetType().Name);
                deleteGuid = Guid.NewGuid().ToString();
                SqlStatements.addDeleteDefinition(deleteGuid, this.GetType().Name, updateStatement);
            }
        }

        private void processIndexAttributes()
        {
            List<string> indexSqlStatements = new List<string>();
            SxmConnection sxmConnection = default(SxmConnection);
            string dbAndTableName = string.Format("{0}.{1}", this.databaseName, this.GetType().Name);

            var customAttributes = (CreateIndex[])this.GetType().GetCustomAttributes(typeof(CreateIndex), true);
            try
            {
                string? sqlStatement = default(string);
                List<string> existingIndexes = getIndexTableStatements();

                if (customAttributes != null && customAttributes.Length > 0)
                {
                    foreach (var myAttribute in customAttributes)
                    {
                        if (!existingIndexes.Contains(myAttribute.indexName))
                        {
                            string[] indexes = myAttribute.indexFields;
                            string indexFields = string.Empty;
                            int i = 0;
                            foreach (string indexField in indexes)
                            {
                                if (i == 0)
                                    indexFields += indexField;
                                else
                                    indexFields += ", " + indexField;
                                ++i;
                            }

                            indexSqlStatements.Add(string.Format("CREATE INDEX {0} ON {1} ({2})", myAttribute.indexName, this.GetType().Name, indexFields));
                        }
                    }

                    foreach (string indexName in existingIndexes)
                    {
                        bool found = false;

                        foreach (var myAttribute in customAttributes)
                        {
                            if (myAttribute.indexName.Equals(indexName))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found == false)
                        {
                            indexSqlStatements.Add(string.Format("DROP INDEX {0}", indexName));
                        }
                    }

                    if(indexSqlStatements.Count > 0)
                    {
                        sxmConnection = new SxmConnection(databaseName);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                        {
                            foreach (string indexStatement in indexSqlStatements)
                                sxmTransaction1.executeIndex(indexStatement);

                            sxmTransaction1.commitTransaction();
                        }
                    }
                }
            }
            catch (Exception ex) { }
            finally
            {
                if (sxmConnection != null)
                    sxmConnection.destroyConnection();
            }
        }

        private List<string> getIndexTableStatements()
        {
            List<string> indexNames = new List<string>();
            SxmConnection sxmConnection = new SxmConnection(databaseName);

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery(String.Format("PRAGMA index_list({0})", this.GetType().Name), null as List<object>);

                    while (sxmConnection.nextRow() == true)
                        indexNames.Add((string)sxmConnection.getValue("name"));
                }
            }
            catch (Exception ex) { }
            finally
            {
                if (sxmConnection != null)
                    sxmConnection.destroyConnection();
            }

            return indexNames;
        }

        private void reconcile()
        {
            Dictionary<string, string> dbTableColumnNameAndType = SxmInit.getTableColumnNames(databaseName, this.GetType().Name);

            SxmConnection sxmConnection = new SxmConnection(databaseName);
            try
            {
                foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} ADD {1} {2}", this.GetType().Name, kvp.Key, kvp.Value);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                        {
                            sxmTransaction1.executeAlterTable(alterDefinition);
                            sxmTransaction1.commitTransaction();
                        }
                    }
                }

                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!columnNameAndType.ContainsKey(kvp.Key) && !kvp.Key.Equals("id") && !kvp.Key.Equals("systemSynchID"))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} DROP {1}", this.GetType().Name, kvp.Key);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(sxmConnection))
                        {
                            sxmTransaction1.executeAlterTable(alterDefinition);
                            sxmTransaction1.commitTransaction();
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (sxmConnection != null)
                    sxmConnection.destroyConnection();
            }
        }

        private bool doesRecordExist()
        {
            SxmConnection? sxmConnection = null;
            try
            {
                if (id > 0)
                {
                    sxmConnection = new SxmConnection(databaseName);  // Creates an implicit database name.
                    string sqlSelect = string.Format("SELECT id FROM {0} WHERE id = {1}", this.GetType().Name, id);
                    sxmConnection.executeQuery(sqlSelect, default(List<object>));
                    if (sxmConnection.hasRows() == true)
                        return true;
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (sxmConnection != null)
                    sxmConnection.destroyConnection();
            }

            return false;
        }

        private void dbNameValidation()
        {
            if (this.databaseName == null)
            {
                SxmConnection? sxmConnection = default(SxmConnection);

                try
                {
                    sxmConnection = new SxmConnection(this.databaseName);  // Creates an implicit database name.
                }
                catch (Exception)
                {
                }
                finally
                {
                    if (sxmConnection != default(SxmConnection))
                        sxmConnection.destroyConnection();

                    this.databaseName = SxmConnection.ImplicitDatabaseName;
                    if (this.databaseName == null)
                    {
                        throw new InvalidDataException("The database name cannot be null.");
                    }
                }
            }
        }

        private void createTable()
        {
            getColumnNamesAndDataTypes();
            string tableStatement = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT", this.GetType().Name);

            foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                tableStatement += string.Format(", {0} {1}", kvp.Key, kvp.Value);

            tableStatement += ")";

            SqlStatements.addTableDefinition(string.Format("{0}.{1}", this.databaseName, this.GetType().Name), tableStatement);
            SxmInit.createTable(this.databaseName, this.GetType().Name);
        }

        private void getColumnNamesAndDataTypes()
        {
            PropertyInfo[]? thisPropertyInfo = this.GetType().GetProperties();

            foreach (PropertyInfo pi in thisPropertyInfo)
            {
                string piType = pi.PropertyType.Name;
                string piName = pi.Name;

                if (!piName.Equals("id"))
                {
                    if (piType == typeof(int).Name)
                        columnNameAndType.Add(piName, "int");

                    else if (piType == typeof(string).Name)
                        columnNameAndType.Add(piName, "text");

                    else if (piType == typeof(long).Name)
                        columnNameAndType.Add(piName, "long");

                    else if (piType == typeof(ulong).Name)     // Large values will overflow.
                        columnNameAndType.Add(piName, "ulong");

                    else if (piType == typeof(float).Name)
                        columnNameAndType.Add(piName, "float");

                    else if (piType == typeof(short).Name)
                        columnNameAndType.Add(piName, "short");

                    else if (piType == typeof(ushort).Name)
                        columnNameAndType.Add(piName, "ushort");

                    else if (piType == typeof(uint).Name)
                        columnNameAndType.Add(piName, "uint");

                    else if (piType == typeof(sbyte).Name)
                        columnNameAndType.Add(piName, "sbyte");

                    else if (piType == typeof(byte).Name)
                        columnNameAndType.Add(piName, "byte");

                    else if (piType == typeof(double).Name)
                        columnNameAndType.Add(piName, "double");

                    else if (piType == typeof(string).Name)
                        columnNameAndType.Add(piName, "string");

                    else if (piType == typeof(decimal).Name)
                        columnNameAndType.Add(piName, "text");

                    else if (piType == typeof(bool).Name)
                        columnNameAndType.Add(piName, "bool");

                    else if (piType == typeof(DateTime).Name)
                        columnNameAndType.Add(piName, "DateTime");

                    else if (piType == typeof(DateTimeOffset).Name)
                        columnNameAndType.Add(piName, "DateTimeOffset");

                    else if (piType == typeof(TimeSpan).Name)
                        columnNameAndType.Add(piName, "TimeSpan");

                    else if (piType == typeof(DateOnly).Name)
                        columnNameAndType.Add(piName, "DateOnly");

                    else if (piType == typeof(TimeOnly).Name)
                        columnNameAndType.Add(piName, "TimeOnly");
                }
            }
        }

        private void loadDbValues(Dictionary<string, object?> databaseRecord)
        {
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = this.GetType().GetProperty(kvp.Key);
                    if (pi != null)
                    {
                        if (kvp.Value != DBNull.Value && kvp.Value != null)
                        {
                            string piType = pi.PropertyType.Name;

                            if (piType == typeof(int).Name)
                                pi.SetValue(this, (int)(long)kvp.Value);

                            else if (piType == typeof(long).Name)
                                pi.SetValue(this, (long)kvp.Value);

                            else if (piType == typeof(ulong).Name)    // Large values will overflow.
                                pi.SetValue(this, (ulong)(long)kvp.Value);

                            else if (piType == typeof(float).Name)
                                pi.SetValue(this, (float)(double)kvp.Value);

                            else if (piType == typeof(short).Name)
                                pi.SetValue(this, (short)(long)kvp.Value);

                            else if (piType == typeof(ushort).Name)
                                pi.SetValue(this, (ushort)(long)kvp.Value);

                            else if (piType == typeof(uint).Name)
                                pi.SetValue(this, (uint)(long)kvp.Value);

                            else if (piType == typeof(sbyte).Name)
                                pi.SetValue(this, (sbyte)(long)kvp.Value);

                            else if (piType == typeof(byte).Name)
                                pi.SetValue(this, (byte)(long)kvp.Value);

                            else if (piType == typeof(double).Name)
                                pi.SetValue(this, (double)kvp.Value);

                            else if (piType == typeof(string).Name)
                                pi.SetValue(this, kvp.Value.ToString());

                            else if (piType == typeof(decimal).Name)    // Can be either text or double. Double will lose precision
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(string).Name)
                                    pi.SetValue(this, Decimal.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(long).Name)
                                    pi.SetValue(this, (decimal)(long)kvp.Value);   // Will lose precision.

                                else if (typeName == typeof(double).Name)
                                    pi.SetValue(this, (decimal)(double)kvp.Value);   // Will lose precision.
                            }

                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(this, true);
                                else
                                    pi.SetValue(this, false);
                            }

                            else if (piType == typeof(DateTime).Name)  // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, new DateTime((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateTime.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateTimeOffset).Name)  // Can be either text or DATETIMEOFFSET.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, DateTimeOffset.FromUnixTimeSeconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateTimeOffset.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(TimeSpan).Name)  // Can be either text or TIMESPAN.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, TimeSpan.FromTicks((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, TimeSpan.Parse(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateOnly).Name)  // Must be text.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, DateOnly.FromDayNumber((int)(long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, DateOnly.Parse(kvp.Value.ToString()!));

                                else if (typeName == typeof(int).Name)
                                    pi.SetValue(this, DateOnly.FromDayNumber((int)kvp.Value));
                            }

                            else if (piType == typeof(TimeOnly).Name)    // Can be either text or double for saving ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(this, new TimeOnly((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(this, TimeOnly.Parse(kvp.Value.ToString()!));
                            }

                            else
                            {
                                pi.SetValue(this, kvp.Value);
                            }

                        }
                        else
                            pi.SetValue(this, default);
                    }
                }
                catch (System.ArgumentException)
                {
                    string? userPropertyType = this.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString();
                    string? databasePropertyType = kvp.Value?.GetType().ToString();
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", kvp.Key, databasePropertyType, this.GetType().ToString() + "." + kvp.Key, userPropertyType));
                }
            }
        }
    }
}
