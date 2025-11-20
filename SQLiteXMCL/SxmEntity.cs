using LinqToDB.Mapping;
using SQLiteXM.Internal;
using System.Reflection;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    internal enum SxmEntityState
    {
        None,
        Insert,
        Update,
        Delete
    }

    interface IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public static string? tableName { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
    public class CreateIndex : Attribute, IIndexVars
    {
        public string[] indexFields { get; set; }
        public string indexName { get; set; }
        public static string? tableName { get; set; } // set by the consumer

        public CreateIndex(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDX_" + tableName;
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateIndex(string indexField)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX_" + tableName;
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
        public string? tableName { get; set; } // set by the consumer

        public IndexPropertyAttributes(string indexField, string tableName)
        {
            this.indexFields = new string[] { indexField };

            this.indexName = "IDX_" + tableName;
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
        public static string? tableName { get; set; } // set by the consumer   

        public CreateUnique(string[] indexFields)
        {
            this.indexFields = indexFields;

            this.indexName = "IDX_" + tableName;
            foreach (string field in indexFields)
            {
                this.indexName += "_" + field;
            }
        }

        public CreateUnique()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class IsAColumnAttribute : Attribute
    {
        public ColumnType ColumnType { get; set; }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class NotAColumnAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public bool ColumnAttributeRequired { get; set; } = false;

        public TableAttribute()
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
    public class CreateForeignKey : Attribute
    {
        public string foreignTable { get; set; }

        public CreateForeignKey(string foreignTable)
        {
            this.foreignTable = foreignTable;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class RequiredNotNull : Attribute
    {
        public object defaultValue { get; set; }

        public RequiredNotNull(object defaultValue)
        {
            this.defaultValue = defaultValue;
            if (defaultValue == null)
                throw new ArgumentNullException("RequiredNotNull", "For fields with the attribute 'RequiredNotNull', the default value for the field cannot be null.");
        }
    }

    internal class ForeignKeyAttributes
    {
        public string fieldName { get; set; }
        public string foreignTable { get; set; }
    }

    [Table(ColumnAttributeRequired = false)]
    public class SxmEntity
    {
        private static object lockObject = new object();

        private static Dictionary<string, string> insertGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, string> updateGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, string> deleteGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, List<IndexPropertyAttributes>> uniqueIndexDict = new Dictionary<string, List<IndexPropertyAttributes>>();
        private static Dictionary<string, List<IndexPropertyAttributes>> standardIndexDict = new Dictionary<string, List<IndexPropertyAttributes>>();
        private static Dictionary<string, Dictionary<string, string>> columnNameAndTypeDict = new Dictionary<string, Dictionary<string, string>>();

        private SxmEntityState _pendingState = SxmEntityState.None;
        internal void MarkAsInsert() => _pendingState = SxmEntityState.Insert;
        internal void MarkAsUpdate() => _pendingState = SxmEntityState.Update;
        internal void MarkAsDelete() => _pendingState = SxmEntityState.Delete;
        internal SxmEntityState PendingState => _pendingState;

        private string? databaseName = SxmConnection.ImplicitDatabaseName;
        private List<ForeignKeyAttributes>? foreignKeyAttributeList = default(List<ForeignKeyAttributes>);

        [IsAColumn]
        [Column, PrimaryKey, Identity]
        public virtual long id { get; set; }

        [IsAColumn]
        public virtual string? synchId { get; set; }

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

                if (!columnNameAndTypeDict.ContainsKey(this.GetType().Name))
                {
                    getColumnNamesAndDataTypes();

                    createTable();

                    List<string> existingStandardIndexes = new List<string>();
                    List<string> existingUniqueIndexes = new List<string>();
                    getIndexTableStatements(ref existingStandardIndexes, ref existingUniqueIndexes);

                    processIndexAttributes(IndexType.standard, existingStandardIndexes);
                    processIndexAttributes(IndexType.unique, existingUniqueIndexes);

                    processtriggerAttributes();
                }
            }
        }

        private async Task Save(string sqlStatementName)
        {
            {
                Dictionary<string, object?> result = await SxmStatement.PerformInsert<SxmEntity>(sqlStatementName, this, databaseName).CAF();
                loadDbValues(result);
            }
        }
        public async Task Save(string sqlStatementName, SxmTransaction sxmTrans)
        {
            {
                Dictionary<string, object?> result = await sxmTrans.PerformInsert<SxmEntity>(sqlStatementName, this).CAF();
                loadDbValues(result);
            }
        }

        public async Task Update(string sqlStatementName)
        {
            await SxmStatement.PerformUpdate<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }

        public async Task Update(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.PerformUpdate<SxmEntity>(sqlStatementName, this).CAF();
        }

        public async Task Delete(string sqlStatementName)
        {
            await SxmStatement.PerformDelete<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }
        public async Task Delete(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.PerformDelete<SxmEntity>(sqlStatementName, this).CAF();
        }

        public async Task Save()
        {
            if (!doesRecordExist())
            {
                buildSaveSql();
                await Save(insertGuidDict[this.GetType().Name]).CAF();
            }
            else
            {
                buildUpdateSql();
                await Update(updateGuidDict[this.GetType().Name]).CAF();
            }
        }

        public async Task Save(SxmTransaction sxmTrans)
        {
            if (!doesRecordExist())
            {
                buildSaveSql();
                await Save(insertGuidDict[this.GetType().Name], sxmTrans).CAF();
            }
            else
            {
                buildUpdateSql();
                await Update(updateGuidDict[this.GetType().Name], sxmTrans).CAF();
            }
        }

        public async Task Delete()
        {
            buildDeleteSql();
            if (doesRecordExist())
                await Delete(deleteGuidDict[this.GetType().Name]).CAF();
        }

        public async Task Delete(SxmTransaction sxmTrans)
        {
            buildDeleteSql();
            if (doesRecordExist())
                await Delete(deleteGuidDict[this.GetType().Name], sxmTrans).CAF();
        }

        private void buildSaveSql()
        {
            if (insertGuidDict.GetValueOrDefault(this.GetType().Name) == default(string))
            {
                string insertColumns = string.Empty;
                string insertValues = string.Empty;

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in columnNameAndTypeDict[this.GetType().Name])
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
                insertGuidDict.Add(this.GetType().Name, Guid.NewGuid().ToString());
                SqlStatements.addInsertDefinition(insertGuidDict[this.GetType().Name], this.GetType().Name, insertStatement);
            }
        }

        private void buildUpdateSql()
        {
            if (updateGuidDict.GetValueOrDefault(this.GetType().Name) == default(string))
            {
                string insertColumns = string.Empty;

                int i = 0;
                foreach (KeyValuePair<string, string> kvp in columnNameAndTypeDict[this.GetType().Name])
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
                updateGuidDict.Add(this.GetType().Name, Guid.NewGuid().ToString());
                SqlStatements.addUpdateDefinition(updateGuidDict[this.GetType().Name], this.GetType().Name, updateStatement);
            }
        }

        private void buildDeleteSql()
        {
            if (deleteGuidDict.GetValueOrDefault(this.GetType().Name) == default(string))
            {
                string updateStatement = string.Format("DELETE FROM {0} WHERE id=@id", this.GetType().Name);
                deleteGuidDict.Add(this.GetType().Name, Guid.NewGuid().ToString());
                SqlStatements.addDeleteDefinition(deleteGuidDict[this.GetType().Name], this.GetType().Name, updateStatement);
            }
        }

        private void processtriggerAttributes()
        {
            Dictionary<string, string> newTriggerNameList = new Dictionary<string, string>();

            var customAttributes = (CreateTrigger[])this.GetType().GetCustomAttributes(typeof(CreateTrigger), true);

            if (customAttributes.Length > 0)
            {
                foreach (var myAttribute in customAttributes)
                {
                    string? triggerSql = myAttribute.triggerSql;
                    string? triggerToBeAdded = default(string?);

                    if ((triggerToBeAdded = extractTriggerName(triggerSql)) != default(string?))
                        newTriggerNameList.Add(triggerToBeAdded, triggerSql);
                }

                if (newTriggerNameList.Count > 0)
                {
                    try
                    {

                        using (SxmUTransaction sxmTransaction = new SxmUTransaction(new SxmConnection(databaseName)))
                        {
                            List<string> ExistingTriggers = SxmInit.getAllTriggers(sxmTransaction.Connection);

                            foreach (KeyValuePair<string, string> kvp in newTriggerNameList)
                                if (!ExistingTriggers.Contains(kvp.Key))
                                    sxmTransaction.executeCreateTrigger(kvp.Value);

                            foreach (string existingTrigger in ExistingTriggers)
                            {
                                if (!newTriggerNameList.ContainsKey(existingTrigger))
                                    sxmTransaction.executeCreateTrigger(string.Format("DROP TRIGGER {0}", existingTrigger));
                            }

                            sxmTransaction.commitTransaction();
                        }
                    }
                    catch (Exception ex) { }
                }
            }
        }

        private string? extractTriggerName(string triggerSql)
        {
            int conditionOffset = 0;
            string? triggerToBeAdded = default(string);

            if ((conditionOffset = triggerSql.IndexOf(" before ", StringComparison.OrdinalIgnoreCase)) == -1)
            {
                if ((conditionOffset = triggerSql.IndexOf(" after ", StringComparison.OrdinalIgnoreCase)) == -1)
                {
                    conditionOffset = triggerSql.IndexOf(" instead ", StringComparison.OrdinalIgnoreCase);
                }
            }
            if (conditionOffset == -1)
                return triggerToBeAdded;

            int schemaDivider = triggerSql.IndexOf('.');
            if (schemaDivider != -1 && schemaDivider < conditionOffset)
            {
                int endTableName = triggerSql.IndexOf(' ', schemaDivider);
                ++schemaDivider;
                if (triggerSql[endTableName - 1].Equals("'"))
                    --endTableName;

                triggerToBeAdded = triggerSql.Substring(schemaDivider, endTableName - schemaDivider);
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

                triggerToBeAdded = triggerSql.Substring(startTableName, conditionOffset - startTableName);
            }

            return triggerToBeAdded;
        }

        private void processIndexAttributes(IndexType indexType, List<string> existingIndexes)
        {
            string unique = string.Empty;
            IIndexVars[]? firstArray = default(IIndexVars[]);
            IIndexVars[]? secondArray = default(IIndexVars[]);
            List<string> indexSqlStatements = new List<string>();
            SxmConnection? sxmConnection = default(SxmConnection);
            IIndexVars[]? customAttributes = default(IIndexVars[]);
            string tableName = this.GetType().Name;

            CreateIndex.tableName = tableName;
            CreateUnique.tableName = tableName;

            if (indexType == IndexType.standard)

            {
                firstArray = (CreateIndex[])this.GetType().GetCustomAttributes(typeof(CreateIndex), true);
                secondArray = standardIndexDict.ContainsKey(tableName) ? standardIndexDict[tableName].ToArray() : default(IIndexVars[]);
            }

            if (indexType == IndexType.unique)
            {
                firstArray = (CreateUnique[])this.GetType().GetCustomAttributes(typeof(CreateUnique), true);
                secondArray = uniqueIndexDict.ContainsKey(tableName) ? uniqueIndexDict[tableName].ToArray() : default(IIndexVars[]);

                unique = "UNIQUE";
            }

            if (secondArray == default(IIndexVars[]))
                secondArray = new IIndexVars[0];

            customAttributes = new IIndexVars[firstArray.Length + secondArray.Length];
            Array.Copy(firstArray, customAttributes, firstArray.Length);
            Array.Copy(secondArray, 0, customAttributes, firstArray.Length, secondArray.Length);

            try
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

                        indexSqlStatements.Add(string.Format("CREATE {0} INDEX {1} ON {2} ({3})", unique, myAttribute.indexName, this.GetType().Name, indexFields));
                    }
                }

                foreach (string indexName in existingIndexes)
                {
                    bool found = false;

                    foreach (IIndexVars customAttribute in customAttributes)
                    {
                        if (customAttribute.indexName.Equals(indexName))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        indexSqlStatements.Add(string.Format("DROP INDEX {0}", indexName));
                }

                if (indexSqlStatements.Count > 0)
                {
                    using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(new SxmConnection(databaseName)))
                    {
                        foreach (string indexStatement in indexSqlStatements)
                            sxmTransaction1.executeIndex(indexStatement);

                        sxmTransaction1.commitTransaction();
                    }
                }
            }
            catch (Exception ex) { }
            finally
            {
                if (indexType == IndexType.standard)
                {
                    if (standardIndexDict.GetValueOrDefault(this.GetType().Name) != default(List<IndexPropertyAttributes>))
                        standardIndexDict.Remove(this.GetType().Name);
                }

                if (indexType == IndexType.unique)
                {
                    if (uniqueIndexDict.GetValueOrDefault(this.GetType().Name) != default(List<IndexPropertyAttributes>))
                        uniqueIndexDict.Remove(this.GetType().Name);
                }
            }
        }

        private void getIndexTableStatements(ref List<string> existingStandardIndexes, ref List<string> existingUniqueIndexes)
        {

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(new SxmConnection(databaseName)))
                {
                    sxmTransaction.Connection.executeQuery(String.Format("PRAGMA index_list({0})", this.GetType().Name), null as List<object>);

                    while (sxmTransaction.Connection.nextRow() == true)
                    {
                        string indexName = (string)sxmTransaction.Connection.getValue("name");

                        if ((long)sxmTransaction.Connection.getValue("unique") == 1)
                            existingUniqueIndexes.Add(indexName);
                        else
                            existingStandardIndexes.Add(indexName);
                    }
                }
            }
            catch (Exception ex) { }
            finally
            {
            }
        }

        private void reconcile()
        {
            Dictionary<string, string> dbTableColumnNameAndType = SxmInit.getTableColumnNames(databaseName, this.GetType().Name);

            try
            {
                foreach (KeyValuePair<string, string> kvp in columnNameAndTypeDict[this.GetType().Name])
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} ADD {1} {2}", this.GetType().Name, kvp.Key, kvp.Value);

                        if (foreignKeyAttributeList != default(List<ForeignKeyAttributes>))
                        {
                            foreach (ForeignKeyAttributes attribute in foreignKeyAttributeList)
                            {
                                if (kvp.Key.Equals(attribute.fieldName))
                                {
                                    alterDefinition += $" CONSTRAINT fk_{attribute.fieldName} REFERENCES {attribute.foreignTable}(id)";
                                    foreignKeyAttributeList.Remove(attribute);
                                    break;
                                }
                            }
                        }

                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(new SxmConnection(databaseName)))
                        {
                            sxmTransaction1.executeAlterTable(alterDefinition);
                            sxmTransaction1.commitTransaction();
                        }

                        int offset = 0;
                        string? value = default(string);

                        if ((offset = kvp.Value.IndexOf(' ')) != -1)
                            value = kvp.Value.Substring(0, offset);
                        else
                            value = kvp.Value;

                        SxmInit.addColumnNameType(this.GetType().Name, kvp.Key, value);
                    }
                }

                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!columnNameAndTypeDict[this.GetType().Name].ContainsKey(kvp.Key) && !kvp.Key.Equals("id") && !kvp.Key.Equals("synchId"))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} DROP {1}", this.GetType().Name, kvp.Key);
                        using (SxmUTransaction sxmTransaction1 = new SxmUTransaction(new SxmConnection(databaseName)))
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
            }
        }

        public bool doesRecordExist()
        {
            SxmConnection? sxmConnection = default(SxmConnection);
            try
            {
                if (id > 0)
                {
                    sxmConnection = new SxmConnection(databaseName);
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
                sxmConnection?.destroyConnection();
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
                    sxmConnection?.destroyConnection();

                    this.databaseName = SxmConnection.ImplicitDatabaseName;
                    if (this.databaseName == null)
                        throw new InvalidDataException("The database name cannot be null.");
                }
            }
        }

        private void createTable()
        {
            if (!SxmInit.doesTableExist(this.GetType().Name, default(SxmConnection)))
            {
                string tableStatement = String.Format("CREATE TABLE {0} (id INTEGER PRIMARY KEY AUTOINCREMENT", this.GetType().Name);

                foreach (KeyValuePair<string, string> kvp in columnNameAndTypeDict[this.GetType().Name])
                    tableStatement += string.Format(", {0} {1}", kvp.Key, kvp.Value);

                if (foreignKeyAttributeList != default(List<ForeignKeyAttributes>))
                {
                    foreach (ForeignKeyAttributes attribute in foreignKeyAttributeList)
                        tableStatement += $", FOREIGN KEY({attribute.fieldName}) REFERENCES {attribute.foreignTable}(id)";

                    foreignKeyAttributeList = default(List<ForeignKeyAttributes>);
                }

                tableStatement += ")";

                SqlStatements.addTableDefinition(string.Format("{0}.{1}", this.databaseName, this.GetType().Name), tableStatement);
                SxmInit.createTable(this.databaseName, this.GetType().Name);
                SqlStatements.removeTableDefinitions();
            }
            else
                reconcile();
        }

        private void getColumnNamesAndDataTypes()
        {
            var type = GetType();
            PropertyInfo[]? thisPropertyInfo = type.GetProperties();
            columnNameAndTypeDict.Add(this.GetType().Name, new Dictionary<string, string>());

            foreach (PropertyInfo pi in thisPropertyInfo)
            {
                string piType = pi.PropertyType.Name;
                string piName = pi.Name;

                // Skip "id" and "synchId" properties
                if (piName is "id" or "synchId")
                    continue;

                // Skip properties marked with [NotMapped] or [LinqToDB.Mapping.NotColumn]
                if (pi.IsDefined(typeof(NotAColumnAttribute), false))
                    continue;

                // Get the [Column] attribute, if present, otherwise it's null.
                IsAColumnAttribute? colAttr = pi.GetCustomAttribute<IsAColumnAttribute>(inherit: false);

                // Get the [Table] attribute to check IsAColumnAttributeRequired.
                TableAttribute? tbl = type.GetCustomAttribute<TableAttribute>(inherit: false);
                bool columnIsRequired = tbl?.ColumnAttributeRequired ?? false; // Check IsColumnAttributeRequired.
                if (columnIsRequired && colAttr == null)
                    continue; // Must have [Column] attribute in order to map to a database, but it's missing.

                string notNull = string.Empty;
                Dictionary<string, object> propertyAttribute = pi.GetCustomAttributes(false).ToDictionary(a => a.GetType().Name, a => a);

                if (propertyAttribute.ContainsKey("RequiredNotNull"))
                {
                    RequiredNotNull nn = (RequiredNotNull)propertyAttribute["RequiredNotNull"];
                    if (nn.defaultValue != null)
                        notNull = $" not null default {nn.defaultValue}";
                    else
                        notNull = " not null";
                }

                if (propertyAttribute.ContainsKey("CreateIndex"))
                {
                    if (standardIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                        standardIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                    standardIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(piName, type.Name));
                }

                if (propertyAttribute.ContainsKey("CreateUnique"))
                {
                    if (uniqueIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                        uniqueIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                    uniqueIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(piName, type.Name));
                }
                if (propertyAttribute.ContainsKey("CreateForeignKey"))
                {
                    CreateForeignKey fk = (CreateForeignKey)propertyAttribute["CreateForeignKey"];

                    if (foreignKeyAttributeList == default(List<ForeignKeyAttributes>))
                        foreignKeyAttributeList = new List<ForeignKeyAttributes>();
                    foreignKeyAttributeList?.Add(new ForeignKeyAttributes()
                    {
                        fieldName = piName,
                        foreignTable = fk.foreignTable,
                    });

                    SxmHelpers.CreateAssociation(type, piName, fk.foreignTable);
                }

                var clrType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;

                // Override from ColumnType if specified, for example, [Column(DataType = DataType.Text)]
                string? columnType = colAttr?.ColumnType switch
                {
                    ColumnType.Text or ColumnType.NVarChar or ColumnType.VarChar or ColumnType.Char or ColumnType.NChar => "TEXT",
                    ColumnType.Int16 or ColumnType.Int32 or ColumnType.Int64 => "INTEGER",
                    ColumnType.UInt16 or ColumnType.UInt32 => "INTEGER",
                    ColumnType.UInt64 => "TEXT", // preserve range
                    ColumnType.Boolean => "INTEGER",
                    ColumnType.Single or ColumnType.Double => "REAL",
                    ColumnType.Decimal => "TEXT", // preserve range
                    ColumnType.Guid => "TEXT",
                    ColumnType.DateTime or ColumnType.Date or ColumnType.Time => "INTEGER", // ticks
                    ColumnType.Binary or ColumnType.Blob or ColumnType.VarBinary => "BLOB",
                    _ => null
                };

                if (columnType != null)
                {
                    if (clrType == typeof(DateTime) || clrType == typeof(DateOnly) || clrType == typeof(TimeSpan) || clrType == typeof(TimeOnly) || clrType == typeof(DateTimeOffset))
                    {
                        if (!columnType.Equals("TEXT"))
                            columnType = null;
                    }
                }

                // Fallback to CLR mapping if ColumnType was Undefined.
                if (columnType == null)
                {
                    // Determine CLR (nullable unwrap)
                    columnType = clrType == typeof(int) ? "INTEGER" :
                                 clrType == typeof(string) ? "TEXT" :
                                 clrType == typeof(long) ? "INTEGER" :
                                 clrType == typeof(ulong) ? "TEXT" :
                                 clrType == typeof(float) ? "REAL" :
                                 clrType == typeof(short) ? "INTEGER" :
                                 clrType == typeof(ushort) ? "INTEGER" :
                                 clrType == typeof(uint) ? "INTEGER" :
                                 clrType == typeof(sbyte) ? "INTEGER" :
                                 clrType == typeof(byte) ? "INTEGER" :
                                 clrType == typeof(double) ? "REAL" :
                                 clrType == typeof(Guid) ? "TEXT" :
                                 clrType == typeof(decimal) ? "TEXT" :
                                 clrType == typeof(bool) ? "INTEGER" :
                                 clrType == typeof(byte[]) ? "BLOB" :
                                 clrType == typeof(DateTime) ? "INTEGER" :
                                 clrType == typeof(DateTimeOffset) ? "INTEGER" :
                                 clrType == typeof(TimeSpan) ? "INTEGER" :
                                 clrType == typeof(DateOnly) ? "INTEGER" :
                                 clrType == typeof(TimeOnly) ? "INTEGER" :
                                 null;
                }

                if (columnType != null)
                    columnNameAndTypeDict[type.Name].Add(piName, columnType + notNull);
            }
        }

        /*
                 private void getColumnNamesAndDataTypes()
                {
                    PropertyInfo[]? thisPropertyInfo = this.GetType().GetProperties();
                    columnNameAndTypeDict.Add(this.GetType().Name, new Dictionary<string, string>());

                    foreach (PropertyInfo pi in thisPropertyInfo)
                    {
                        string piType = pi.PropertyType.Name;
                        string piName = pi.Name;
                        Dictionary<string, object> propertyAttribute = pi.GetCustomAttributes(false).ToDictionary(a => a.GetType().Name, a => a);

                        string notNull = string.Empty;
                        string? columnType = default(string);

                        if (!piName.Equals("id") && !piName.Equals("synchId") && propertyAttribute.ContainsKey("ColumnAttribute"))
                        {
                            if (propertyAttribute.ContainsKey("RequiredNotNull"))
                            {
                                RequiredNotNull nn = (RequiredNotNull)propertyAttribute["RequiredNotNull"];
                                if (nn.defaultValue != null)
                                    notNull = $" not null default {nn.defaultValue}";
                                else
                                    notNull = " not null";
                            }

                            if (propertyAttribute.ContainsKey("CreateIndex"))
                            {
                                if (standardIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                                    standardIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                                standardIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(piName));
                            }

                            if (propertyAttribute.ContainsKey("CreateUnique"))
                            {
                                if(uniqueIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                                    uniqueIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                                uniqueIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(piName));
                            }
                            if (propertyAttribute.ContainsKey("CreateForeignKey"))
                            {
                                CreateForeignKey fk = (CreateForeignKey)propertyAttribute["CreateForeignKey"];

                                if (foreignKeyAttributeList == default(List<ForeignKeyAttributes>))
                                    foreignKeyAttributeList = new List<ForeignKeyAttributes>();
                                foreignKeyAttributeList?.Add(new ForeignKeyAttributes()
                                {
                                    fieldName = piName,
                                    foreignTable = fk.foreignTable,
                                });
                            }

                            Type? underlyingType = Nullable.GetUnderlyingType(pi.PropertyType);
                            if (underlyingType != null)
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
                                columnType = "Guid";

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
                                columnNameAndTypeDict[this.GetType().Name].Add(piName, columnType + notNull);
                        }
                    }
                }
        */

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
                            Type? underlyingType = Nullable.GetUnderlyingType(pi.PropertyType);
                            if (underlyingType != null)
                            {
                                piType = underlyingType.Name;
                            }

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
                                    pi.SetValue(this, DateTimeOffset.FromUnixTimeMilliseconds((long)kvp.Value));

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
