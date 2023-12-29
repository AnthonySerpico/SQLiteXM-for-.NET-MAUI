using System.ComponentModel.DataAnnotations;
using System.Reflection;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    interface IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public IndexSource indexSource { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class CreateIndex : Attribute, IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public IndexSource indexSource { get; set; } = IndexSource.classAttribute;

        public CreateIndex(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDX";
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateIndex(string indexField)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX";
            foreach (string field in this.indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateIndex()
        {
        }
    }

    public class IndexPropertyAttributes : IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public IndexSource indexSource { get; set; } = IndexSource.propertiesAttribute;

        public IndexPropertyAttributes(string indexField)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX";
            foreach (string field in this.indexFields)
            {
                this.indexName += "_" + field;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class CreateUnique : Attribute, IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public IndexSource indexSource { get; set; } = IndexSource.classAttribute;


        public CreateUnique(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDXU";
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateUnique()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class CreateTrigger : Attribute
    {
        public string triggerSql { get; set; }

        public CreateTrigger(string triggerSql)
        {
            this.triggerSql = triggerSql;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class Exclude : Attribute
    {
        public bool exclude { get; set; } = true;

        public Exclude()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class RequiredNotNull : Attribute
    {
        public object? defaultValue { get; set; } = default(object?);

        public RequiredNotNull()
        {
        }
    }

    public class SxmEntity
    {
        private bool mustReconcile = true;
        private static object lockObject = new object();
        private static string? insertGuid = default(string);
        private static string? updateGuid = default(string);
        private static string? deleteGuid = default(string);
        private static List<IndexPropertyAttributes>? standardIndexPropertyAttributesList;
        private static List<IndexPropertyAttributes>? uniqueIndexPropertyAttributesList;
        private string? databaseName = SxmConnection.ImplicitDatabaseName;
        private static Dictionary<string, string> columnNameAndType = new Dictionary<string, string>();

        public virtual long id { get; set; }
        public virtual string synchId { get; set; }

        public SxmEntity(string? databaseName)
        {
            this.databaseName = databaseName;
            initialize();
        }
        public SxmEntity()
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

                    List<string> existingIndexes = getIndexTableStatements(IndexType.standard);
                    processIndexAttributes(IndexType.standard, existingIndexes);

                    existingIndexes.Clear();
                    existingIndexes = getIndexTableStatements(IndexType.unique);
                    processIndexAttributes(IndexType.unique, existingIndexes);

                    processtriggerAttributes();
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
                Dictionary<string, object?> result = await SxmStatement.PerformInsert<SxmEntity>(sqlStatementName, this, databaseName);
                loadDbValues(result);
            }
        }
        public async Task Save(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (!doesRecordExist())
            {
                Dictionary<string, object?> result = await sxmTrans.PerformInsert<SxmEntity>(sqlStatementName, this);
                loadDbValues(result);
            }
        }

        public async Task Update(string sqlStatementName)
        {
            if (doesRecordExist())
                await SxmStatement.PerformUpdate<SxmEntity>(sqlStatementName, this, databaseName);
        }

        public async Task Update(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (doesRecordExist())
                await sxmTrans.PerformUpdate<SxmEntity>(sqlStatementName, this);
        }

        public async Task Delete(string sqlStatementName)
        {
            if (doesRecordExist())
                await SxmStatement.PerformDelete<SxmEntity>(sqlStatementName, this, databaseName);
        }
        public async Task Delete(string sqlStatementName, SxmTransaction sxmTrans)
        {
            if (doesRecordExist())
                await sxmTrans.PerformDelete<SxmEntity>(sqlStatementName, this);
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

        protected void processtriggerAttributes()
        {
            int conditionOffset = 0;
            string? triggerName = default(string);
            List<string> triggerNameList = new List<string>();

            var customAttributes = (CreateTrigger[])this.GetType().GetCustomAttributes(typeof(CreateTrigger), true);

            if (customAttributes != null && customAttributes.Length > 0)
            {
                foreach (var myAttribute in customAttributes)
                {
                    string? triggerSql = myAttribute.triggerSql;


                    if ((conditionOffset = triggerSql.IndexOf(" before ", StringComparison.OrdinalIgnoreCase)) == -1)
                    {
                        if ((conditionOffset = triggerSql.IndexOf(" after ", StringComparison.OrdinalIgnoreCase)) == -1)
                        {
                            conditionOffset = triggerSql.IndexOf(" instead ", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    if (conditionOffset == -1)
                        return;

                    int schemaDivider = triggerSql.IndexOf('.');
                    if (schemaDivider != -1 && schemaDivider < conditionOffset)
                    {
                        int endTableName = triggerSql.IndexOf(' ', schemaDivider);
                        ++schemaDivider;
                        if (triggerSql[endTableName - 1].Equals("'"))
                            --endTableName;

                        triggerName = triggerSql.Substring(schemaDivider, endTableName - schemaDivider);
                    }
                    else
                    {
                        --conditionOffset;
                        while (triggerSql[conditionOffset] == ' ')
                            --conditionOffset;

                        int startTableName = conditionOffset;
                        if (!triggerSql[conditionOffset].Equals("'"))
                            ++conditionOffset;

                        while (triggerSql[startTableName] != ' ')
                            --startTableName;
                        ++startTableName;

                        if (triggerSql[startTableName].Equals("'"))
                            ++startTableName;

                        triggerName = triggerSql.Substring(startTableName, conditionOffset - startTableName);
                    }

                    if (triggerName == default(string))
                        return;

                    triggerNameList.Add(triggerName);
                    SxmConnection? sxmConnection = new SxmConnection(databaseName);
                    try
                    {
                        List<string> triggerList = SxmInit.getAllTriggers(sxmConnection);
                        if (!triggerList.Contains(triggerName))
                        {
                            using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                            {
                                sxmTransaction.executeCreateTrigger(triggerSql);

                                foreach (var triggerAttribute in customAttributes)
                                {
                                    if (!triggerNameList.Contains(triggerAttribute.triggerSql))
                                    {
                                        sxmTransaction.executeCreateTrigger(string.Format("DROP TRIGGER {0}", triggerAttribute.triggerSql));
                                    }
                                }

                                sxmTransaction.commitTransaction();
                            }
                        }

                        foreach (var triggerAttribute in customAttributes)
                        {
                            if(!triggerNameList.Contains(triggerAttribute.triggerSql))
                            {

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
            }
        }

        private void processIndexAttributes(IndexType indexType, List<string> existingIndexes)
        {
            string extra = string.Empty;
            List<string> indexSqlStatements = new List<string>();
            SxmConnection? sxmConnection = default(SxmConnection);
            IIndexVars[]? customAttributes = default(IIndexVars[]);

            if (indexType == IndexType.standard || indexType == IndexType.standardAttribute)
            {
                IIndexVars[] firstArray = (CreateIndex[])this.GetType().GetCustomAttributes(typeof(CreateIndex), true);
                IIndexVars[] secondArray = standardIndexPropertyAttributesList?.ToArray();
                if (secondArray == default(IIndexVars[]))
                    secondArray = new IIndexVars[0];

                customAttributes = new IIndexVars[firstArray.Length + secondArray.Length];
                Array.Copy(firstArray, customAttributes, firstArray.Length);
                Array.Copy(secondArray, 0, customAttributes, firstArray.Length, secondArray.Length);
            }

            if (indexType == IndexType.unique  || indexType == IndexType.uniqueAttribute)
            {
                IIndexVars[] firstArray = (CreateUnique[])this.GetType().GetCustomAttributes(typeof(CreateUnique), true);
                IIndexVars[] secondArray = uniqueIndexPropertyAttributesList?.ToArray();
                if (secondArray == default(IIndexVars[]))
                    secondArray = new IIndexVars[0];

                customAttributes = new IIndexVars[firstArray.Length + secondArray.Length];
                Array.Copy(firstArray, customAttributes, firstArray.Length);
                Array.Copy(secondArray, 0, customAttributes, firstArray.Length, secondArray.Length);

                extra = "UNIQUE";
            }

            try
            {
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

                            indexSqlStatements.Add(string.Format("CREATE {0} INDEX {1} ON {2} ({3})", extra, myAttribute.indexName, this.GetType().Name, indexFields));
                        }
                    }

                    foreach (string indexName in existingIndexes)
                    {
                        bool found = false;

                        foreach (IIndexVars customAttribute in customAttributes)
                        {
                            if (customAttribute.indexName.Equals(indexName) )
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

                    if (indexSqlStatements.Count > 0)
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

                if (indexType == IndexType.standardAttribute)
                {
                    if (standardIndexPropertyAttributesList != default(List<IndexPropertyAttributes>))
                        standardIndexPropertyAttributesList = default(List<IndexPropertyAttributes>);
                }

                if (indexType == IndexType.uniqueAttribute)
                {
                    if (uniqueIndexPropertyAttributesList != default(List<IndexPropertyAttributes>))
                        uniqueIndexPropertyAttributesList = default(List<IndexPropertyAttributes>);
                }
            }
        }

        private List<string> getIndexTableStatements(IndexType indexType)
        {
            List<string> indexNames = new List<string>();
            SxmConnection sxmConnection = new SxmConnection(databaseName);

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                {
                    sxmConnection.executeQuery(String.Format("PRAGMA index_list({0})", this.GetType().Name), null as List<object>);

                    while (sxmConnection.nextRow() == true)
                    {
                        string indexName = (string)sxmConnection.getValue("name");
                        if (indexType == IndexType.unique || indexType == IndexType.uniqueAttribute)
                        {
                            if ((long)sxmConnection.getValue("unique") == 1)
                                indexNames.Add(indexName);
                        }
                        if (indexType == IndexType.standard || indexType == IndexType.standardAttribute)
                        {
                            if ((long)sxmConnection.getValue("unique") == 0)
                            {
                                indexNames.Add(indexName);
                            }
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

                        int offset = 0;
                        string? value = default(string);

                        if((offset = kvp.Value.IndexOf(' ')) != -1)
                            value = kvp.Value.Substring(0, offset);
                        else
                            value = kvp.Value;
                        SxmInit.addColumnNameType(this.GetType().Name, kvp.Key, value);
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

                        SxmInit.removeColumnNameType(this.GetType().Name, kvp.Key);
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

            if (!SxmInit.doesTableExist(this.GetType().Name, default(SxmConnection)))
            {
                string tableStatement = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT", this.GetType().Name);

                foreach (KeyValuePair<string, string> kvp in columnNameAndType)
                    tableStatement += string.Format(", {0} {1}", kvp.Key, kvp.Value);

                tableStatement += ")";

                SqlStatements.addTableDefinition(string.Format("{0}.{1}", this.databaseName, this.GetType().Name), tableStatement);
                SxmInit.createTable(this.databaseName, this.GetType().Name);
                SqlStatements.removeTableDefinitions();
            }
        }

        private void getColumnNamesAndDataTypes()
        {
            PropertyInfo[]? thisPropertyInfo = this.GetType().GetProperties();

            foreach (PropertyInfo pi in thisPropertyInfo)
            {
                string piType = pi.PropertyType.Name;
                string piName = pi.Name;
                Dictionary<string, object> propertyAttribute = pi.GetCustomAttributes(false).ToDictionary(a => a.GetType().Name, a => a);

                string notNull = string.Empty;
                string? columnType = default(string);

                if (!piName.Equals("id") && !piName.Equals("synchId") && !propertyAttribute.ContainsKey("Exclude"))
                {
                    if (propertyAttribute.ContainsKey("RequiredNotNull"))
                    {
                        RequiredNotNull nn = (RequiredNotNull)propertyAttribute["RequiredNotNull"];
                        if(nn.defaultValue != null)
                            notNull = $" not null default {nn.defaultValue}";
                        else
                            notNull = " not null";
                    }

                    if (propertyAttribute.ContainsKey("CreateIndex"))
                    {
                        if (standardIndexPropertyAttributesList == default(List<IndexPropertyAttributes>))
                            standardIndexPropertyAttributesList = new List<IndexPropertyAttributes>();
                        standardIndexPropertyAttributesList?.Add(new IndexPropertyAttributes(piName));
                    }

                    if (propertyAttribute.ContainsKey("CreateUnique"))
                    {
                        if (uniqueIndexPropertyAttributesList == default(List<IndexPropertyAttributes>))
                            uniqueIndexPropertyAttributesList = new List<IndexPropertyAttributes>();
                        uniqueIndexPropertyAttributesList?.Add(new IndexPropertyAttributes(piName));
                    }

                    Type? underlyingType = Nullable.GetUnderlyingType(pi.PropertyType);
                    if(underlyingType != null)
                    {
                        piType = underlyingType.Name;
                    }

                    if (piType == typeof(int).Name)
                        columnType = "int";

                    else if (piType == typeof(string).Name)
                        columnType = "text";

                    else if (piType == typeof(long).Name)
                        columnType = "long";

                    else if (piType == typeof(ulong).Name)  // Large values will overflow unless this is defined as text.
                        columnType = "text";

                    else if (piType == typeof(float).Name)
                        columnType = "float";

                    else if (piType == typeof(short).Name)
                        columnType = "short";

                    else if (piType == typeof(ushort).Name)
                        columnType = "ushort";

                    else if (piType == typeof(uint).Name)
                        columnType = "uint";

                    else if (piType == typeof(sbyte).Name)
                        columnType = "sbyte";

                    else if (piType == typeof(byte).Name)
                        columnType = "byte";

                    else if (piType == typeof(double).Name)
                        columnType = "double";

                    else if (piType == typeof(string).Name)
                        columnType = "text";

                    else if (piType == typeof(Guid).Name)
                        columnType = "text";

                    else if (piType == typeof(decimal).Name)  // Large values will overflow unless this is defined as text.
                        columnType = "text";

                    else if (piType == typeof(bool).Name)
                        columnType = "bool";

                    else if (piType == typeof(DateTime).Name)
                        columnType = "DateTime";

                    else if (piType == typeof(DateTimeOffset).Name)
                        columnType = "DateTimeOffset";

                    else if (piType == typeof(TimeSpan).Name)
                        columnType = "TimeSpan";

                    else if (piType == typeof(DateOnly).Name)
                        columnType = "DateOnly";

                    else if (piType == typeof(TimeOnly).Name)
                        columnType = "TimeOnly";

                    if (columnType != null)
                        columnNameAndType.Add(piName, columnType + notNull);
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

                            else if (piType == typeof(Guid).Name)
                                pi.SetValue(this, Guid.Parse((string)kvp.Value));

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
