//using CommunityToolkit.Mvvm.ComponentModel;
using LinqToDB;
using LinqToDB.Mapping;
using SQLiteXM.Internal;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
//using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    [Table(IsColumnAttributeRequired = false)]
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

        [Column, PrimaryKey, Identity]
        public virtual long id { get; set; }

        [Column(DataType = DataType.Blob)]
        public virtual Guid? synchId { get; set; }

        // Needs to throw an exception if databaseName is invalid.
        public SxmEntity(string? databaseName)
        {
            this.databaseName = databaseName;
            initialize();
        }
        // Needs to throw an exception if databaseName is invalid.
        public SxmEntity()
        {
            initialize();
        }

        private void initialize()
        {
            lock (lockObject)
            {
                dbNameValidation();

                // If the database table does not exist, create it. Tables defined in the SqlStatements file are created first, and skipped here.
                if (!columnNameAndTypeDict.ContainsKey(this.GetType().Name))
                {
                    // Process the public properties of the entity.
                    List<MemberInfoWithAlias> propertyInfoWithAliases = getEntityProperties();
                    getColumnNamesAndDataTypes(propertyInfoWithAliases);

                    createTable();

                    List<string> existingStandardIndexes = new List<string>();
                    List<string> existingUniqueIndexes = new List<string>();
                    getIndexTableStatements(ref existingStandardIndexes, ref existingUniqueIndexes);

                    processIndexStatements(IndexType.standard, existingStandardIndexes);
                    processIndexStatements(IndexType.unique, existingUniqueIndexes);

                    processtriggerAttributes();
                }
            }
        }

        private List<MemberInfoWithAlias> getEntityProperties()
        {
            List<MemberInfoWithAlias> propertyInfoWithAliases = new List<MemberInfoWithAlias>();

            foreach (PropertyInfo piItem in GetType().GetProperties())
                propertyInfoWithAliases.Add(new MemberInfoWithAlias(piItem, string.Empty));

            return propertyInfoWithAliases;
        }

        public async Task Save()
        {
            if (AmbientSxmTransaction.Current != null)
            {
                await Save(AmbientSxmTransaction.Current);
            }
            else
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
            if (AmbientSxmTransaction.Current != null)
            {
                await Delete(AmbientSxmTransaction.Current);
            }
            else
            {
                buildDeleteSql();
                if (doesRecordExist())
                    await Delete(deleteGuidDict[this.GetType().Name]).CAF();
            }
        }

        public async Task Delete(SxmTransaction sxmTrans)
        {
            buildDeleteSql();
            if (doesRecordExist())
                await Delete(deleteGuidDict[this.GetType().Name], sxmTrans).CAF();
        }

        // Save Statements.
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

        // Update statements.
        public async Task Update(string sqlStatementName)
        {
            await SxmStatement.PerformUpdate<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }
        public async Task Update(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.PerformUpdate<SxmEntity>(sqlStatementName, this).CAF();
        }

        // Delete statements.
        public async Task Delete(string sqlStatementName)
        {
            await SxmStatement.PerformDelete<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }
        public async Task Delete(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.PerformDelete<SxmEntity>(sqlStatementName, this).CAF();
        }

        private void buildSaveSql()
        {
            Type type = this.GetType();
            if (insertGuidDict.GetValueOrDefault(type.Name) == default(string))
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
            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(new SxmConnection(databaseName)))
                {
                    List<string> ExistingTriggers = SxmInit.getAllTriggers(sxmTransaction.Connection, this.GetType().Name);
                    foreach (string existingTrigger in ExistingTriggers)
                    {
                        sxmTransaction.executeCreateTrigger(string.Format("DROP TRIGGER {0}", existingTrigger));
                    }

                    sxmTransaction.commitTransaction();
                }
            }
            catch (Exception ex) { }

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
                            foreach (KeyValuePair<string, string> kvp in newTriggerNameList)
                                sxmTransaction.executeCreateTrigger(kvp.Value);

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

        private void processIndexStatements(IndexType indexType, List<string> existingIndexes)
        {
            List<string> indexSqlStatements = new List<string>();

            var type = this.GetType();
            string unique = string.Empty;
            string tableName = type.Name;

            IIndexVars[]? firstArray = default(IIndexVars[]);
            IIndexVars[]? secondArray = default(IIndexVars[]);

            if (indexType == IndexType.standard)
            {
                firstArray = (CreateIndex[])type.GetCustomAttributes(typeof(CreateIndex), true);
                secondArray = standardIndexDict.TryGetValue(tableName, out var stdList) ? stdList.ToArray() : Array.Empty<IIndexVars>();
            }
            else if (indexType == IndexType.unique)
            {
                firstArray = (CreateUniqueIndex[])type.GetCustomAttributes(typeof(CreateUniqueIndex), true);
                secondArray = uniqueIndexDict.TryGetValue(tableName, out var uniqList) ? uniqList.ToArray() : Array.Empty<IIndexVars>();

                unique = "UNIQUE";
            }

            // Ensure non-null arrays
            firstArray ??= Array.Empty<IIndexVars>();
            secondArray ??= Array.Empty<IIndexVars>();

            // Normalize index fields to aliases and construct predictable index names.
            // Combine custom attributes from both sources into a single list.
            List<IIndexVars> customAttributes = new List<IIndexVars>(firstArray.Length + secondArray.Length);
            customAttributes.AddRange(firstArray);
            customAttributes.AddRange(secondArray);

            assignIndexNames(customAttributes!, tableName);

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

                        indexSqlStatements.Add(string.Format("CREATE {0} INDEX {1} ON {2} ({3})", unique, myAttribute.indexName, type.Name, indexFields));
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
            catch (Exception ex) 
            { 
                // Throw an exception here.
            }
            finally
            {
                if (indexType == IndexType.standard)
                {
                    if (standardIndexDict.GetValueOrDefault(type.Name) != default(List<IndexPropertyAttributes>))
                        standardIndexDict.Remove(type.Name);
                }

                if (indexType == IndexType.unique)
                {
                    if (uniqueIndexDict.GetValueOrDefault(type.Name) != default(List<IndexPropertyAttributes>))
                        uniqueIndexDict.Remove(type.Name);
                }
            }
        }

        private void assignIndexNames(List<IIndexVars> indexArray, string tableName)
        {
            foreach (IIndexVars iiV in indexArray)
            {
                iiV.indexName = "IDX_" + tableName;

                for (int i = 0; i < iiV.indexFields.Length; i++)
                {
                    iiV.indexName += "_" + iiV.indexFields[i];
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
                        string? indexName = (string?)sxmTransaction.Connection.getValue("name");
                        if(indexName == null)
                            continue;

                        var raw = sxmTransaction.Connection.getValue("unique");
                        bool isUnique = raw != null && Convert.ToInt64(raw) == 1;
                        if (isUnique)
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

        private void reconcileTableColumns()
        {
            Type type = this.GetType();
            Dictionary<string, string> dbTableColumnNameAndType = SxmInit.getTableColumnNames(databaseName, this.GetType().Name);

            try
            {
                foreach (KeyValuePair<string, string> kvp in columnNameAndTypeDict[type.Name])
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} ADD {1} {2}", type.Name, kvp.Key, kvp.Value);

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

                        SxmInit.addColumnNameType(type.Name, kvp.Key, value);
                    }
                }

                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!columnNameAndTypeDict[type.Name].ContainsKey(kvp.Key) && !kvp.Key.Equals("id") && !kvp.Key.Equals("synchId"))
                    {
                        string alterDefinition = string.Format("ALTER TABLE {0} DROP {1}", type.Name, kvp.Key);
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
                reconcileTableColumns();
        }

        private void getColumnNamesAndDataTypes(List<MemberInfoWithAlias> propertyInfoWithAliases)
        {
            if (propertyInfoWithAliases != null && propertyInfoWithAliases.Count > 0)
            {
                var type = GetType();
                columnNameAndTypeDict.TryAdd(this.GetType().Name, new Dictionary<string, string>());

                foreach (MemberInfoWithAlias propertyInfoWithAlias in propertyInfoWithAliases)
                {
                    MemberInfo pi = propertyInfoWithAlias.memberInfo;
                    string piName = pi.Name;

                    // Skip "id" and "synchId" properties
                    if (piName is "id" or "synchId")
                        continue;

                    // Skip properties marked with [NotColumn].
                    if (pi.IsDefined(typeof(NotColumnAttribute), false))
                        continue;

                    // Get the [Column] attribute, if present, otherwise it's null.
                    ColumnAttribute? colAttr = pi.GetCustomAttribute<ColumnAttribute>(inherit: false);

                    // Get the [Table] attribute to check IsColumnAttributeRequired.
                    TableAttribute? tbl = type.GetCustomAttribute<TableAttribute>(inherit: false);
                    bool columnIsRequired = tbl?.IsColumnAttributeRequired ?? false; // Check IsColumnAttributeRequired.
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

                    string fkField = piName;
                    if (!string.IsNullOrEmpty(propertyInfoWithAlias.alias))
                        fkField = propertyInfoWithAlias.alias;

                    if (propertyAttribute.ContainsKey("CreateIndex"))
                    {
                        if (standardIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                            standardIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                        standardIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(fkField, type.Name));
                    }

                    if (propertyAttribute.ContainsKey("CreateUniqueIndex"))
                    {
                        if (uniqueIndexDict.GetValueOrDefault(this.GetType().Name) == default(List<IndexPropertyAttributes>))
                            uniqueIndexDict.Add(this.GetType().Name, new List<IndexPropertyAttributes>());
                        uniqueIndexDict[this.GetType().Name].Add(new IndexPropertyAttributes(fkField, type.Name));
                    }
                    if (propertyAttribute.ContainsKey("CreateForeignKey"))
                    {
                        CreateForeignKey fk = (CreateForeignKey)propertyAttribute["CreateForeignKey"];

                        if (foreignKeyAttributeList == default(List<ForeignKeyAttributes>))
                            foreignKeyAttributeList = new List<ForeignKeyAttributes>();

                        if (!string.IsNullOrEmpty(propertyInfoWithAlias.alias))
                                fkField = propertyInfoWithAlias.alias;

                        foreignKeyAttributeList?.Add(new ForeignKeyAttributes()
                        {
                            fieldName = fkField,
                            foreignTable = fk.foreignTable,
                        });

                        SxmHelpers.CreateAssociation(type, fkField, fk.foreignTable);
                    }

                    var clrType = Nullable.GetUnderlyingType(pi.GetMemberType()) ?? pi.GetMemberType();

                    // Override from ColumnType if specified, for example, [Column(ColumnType = ColumnType.Text)]
                    string? columnType = colAttr?.DataType switch
                    {
                        DataType.Text or DataType.NVarChar or DataType.VarChar or DataType.Char or DataType.NChar => "TEXT",
                        DataType.Int16 or DataType.Int32 or DataType.Int64 => "INTEGER",
                        DataType.UInt16 or DataType.UInt32 => "INTEGER",
                        DataType.UInt64 => "TEXT", // preserve range
                        DataType.Boolean => "INTEGER",
                        DataType.Guid => "TEXT",
                        DataType.Single or DataType.Double => "REAL",
                        DataType.Decimal => "TEXT", // preserve range
                        DataType.DateTime or DataType.Date or DataType.Time => "INTEGER", // ticks
                        DataType.Binary or DataType.Blob or DataType.VarBinary => "BLOB",
                        _ => null
                    };

                    if (columnType != null)
                    {
                        if (clrType == typeof(DateTime) || clrType == typeof(DateOnly) || clrType == typeof(TimeSpan) || clrType == typeof(TimeOnly) || clrType == typeof(DateTimeOffset))
                        {
                            if (!columnType.Equals("TEXT"))
                                columnType = null;
                        }
                        else if (clrType == typeof(Guid))
                        {
                            if (!columnType.Equals("BLOB"))
                                columnType = null;
                        }
                        else
                            columnType = null;
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
                    {
                        if(string.IsNullOrEmpty(propertyInfoWithAlias.alias))
                            columnNameAndTypeDict[type.Name].Add(piName, columnType + notNull);
                        else
                            columnNameAndTypeDict[type.Name].Add(propertyInfoWithAlias.alias, columnType + notNull);
                    }
                }
            }
        }

        private void loadDbValues(Dictionary<string, object?> databaseRecord)
        {
            // Delegate to the consolidated helper to avoid duplication.
            SxmHelpers.loadDbValues(databaseRecord, this);
        }


        // MapAndSave maps properties from the source into this instance and then persists the entity.
        public async Task MapAndSave(object mapSource)
        {
            MapProperties(mapSource);
            // Persist the entity after mapping. Use CAF() to follow project's await pattern.
            await Save().CAF();
        }

        /// <summary>
        /// Copy matching public instance properties from <paramref name="source"/> to <paramref name="destination"/>.
        /// The destination must inherit from SxmEntity. Properties named "id" and "synchId" are ignored.
        /// Only properties with exactly the same PropertyType (no conversions) are copied.
        /// Indexer properties are ignored. Both properties must be public instance properties and the destination property must be writable.
        /// </summary>
        /// <param name="source">Source object to copy values from.</param>
        /// <Destination object that this inherits from SxmEntity to copy values to.</param>
        public void MapProperties(object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Type srcType = source.GetType();
            Type dstType = this.GetType();

            PropertyInfo[] srcProps = srcType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo sp in srcProps)
            {
                // Skip indexers and non-readable properties
                if (sp.GetIndexParameters().Length > 0)
                    continue;
                if (!sp.CanRead)
                    continue;

                // Ignore id and synchId on source
                if (string.Equals(sp.Name, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(sp.Name, "synchId", StringComparison.OrdinalIgnoreCase))
                    continue;

                PropertyInfo? dp = dstType.GetProperty(sp.Name, BindingFlags.Public | BindingFlags.Instance);
                if (dp == null)
                    continue;
                if (dp.GetIndexParameters().Length > 0)
                    continue;
                if (!dp.CanWrite)
                    continue;

                // Ignore destination id and synchId as well
                if (string.Equals(dp.Name, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(dp.Name, "synchId", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only allow exact type match (no conversions) including nullable status
                if (dp.PropertyType != sp.PropertyType)
                    continue;

                object? value = sp.GetValue(source);

                // If null, set only if destination accepts null (nullable or reference type)
                if (value == null)
                {
                    if (Nullable.GetUnderlyingType(dp.PropertyType) != null || !dp.PropertyType.IsValueType)
                    {
                        dp.SetValue(this, null);
                    }

                    continue;
                }

                // Set value directly (types are identical)
                dp.SetValue(this, value);
            }
        }

        private static bool IsNullableType(Type t) => Nullable.GetUnderlyingType(t) != null;

    }
}
