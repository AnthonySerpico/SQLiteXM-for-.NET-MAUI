using LinqToDB.Mapping;
using SQLiteXM.Internal;
using System.Reflection;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Base class for mapped entities that provides automatic table/index/trigger initialization
    /// plus convenience persistence operations (Save, Update, Delete).
    /// Derived types are automatically inspected for column/index/foreign key attributes when an
    /// instance is constructed, and the database schema is created or reconciled as required.
    /// </summary>
    [Table(IsColumnAttributeRequired = false)]
    public class SxmEntity
    {
        // Prevent multiple concurrent initializations for the same entity type.
        private static object lockObject = new object();
        // Prevent multiple concurrent initializations for the same entity type.
        private static readonly HashSet<string> initializingTypes = new HashSet<string>(StringComparer.Ordinal);


        private static Dictionary<string, string> insertGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, string> updateGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, string> deleteGuidDict = new Dictionary<string, string>();
        private static Dictionary<string, List<IndexPropertyAttributes>> uniqueIndexDict = new Dictionary<string, List<IndexPropertyAttributes>>();
        private static Dictionary<string, List<IndexPropertyAttributes>> standardIndexDict = new Dictionary<string, List<IndexPropertyAttributes>>();
        private static Dictionary<string, Dictionary<string, string>> columnNameAndTypeDict = new Dictionary<string, Dictionary<string, string>>();

        private SxmEntityState _pendingState = SxmEntityState.None;

        /// <summary>
        /// Mark this entity as pending insert (used by change-tracking/transaction logic).
        /// </summary>
        internal void MarkAsInsert() => _pendingState = SxmEntityState.Insert;
        /// <summary>
        /// Mark this entity as pending update (used by change-tracking/transaction logic).
        /// </summary>
        internal void MarkAsUpdate() => _pendingState = SxmEntityState.Update;
        /// <summary>
        /// Mark this entity as pending delete (used by change-tracking/transaction logic).
        /// </summary>
        internal void MarkAsDelete() => _pendingState = SxmEntityState.Delete;
        /// <summary>
        /// Gets the current pending state for this entity (Insert/Update/Delete/None).
        /// </summary>
        internal SxmEntityState PendingState => _pendingState;

        private string? databaseName = SxmConnection.ImplicitDatabaseName;
        private List<ForeignKeyAttributes>? foreignKeyAttributeList = default(List<ForeignKeyAttributes>);

        /// <summary>
        /// Primary key column. Mapped to the SQLite INTEGER PRIMARY KEY AUTOINCREMENT column named "id".
        /// </summary>
        [Column, PrimaryKey, Identity]
        public virtual long id { get; set; }

        /// <summary>
        /// Optional synchronization identifier stored in the database as a BLOB.
        /// </summary>
        [Column(DataType = DataType.Blob)]
        public virtual Guid? synchId { get; set; }

        // Needs to throw an exception if databaseName is invalid.
        /// <summary>
        /// Create an entity instance bound to the specified database name.
        /// Construction triggers schema/index/trigger initialization for the entity's type.
        /// </summary>
        /// <param name="databaseName">Database name to use for initialization. If null, an implicit DB name is created.</param>
        public SxmEntity(string? databaseName)
        {
            this.databaseName = databaseName;
            initialize();
        }
        // Needs to throw an exception if databaseName is invalid.
        /// <summary>
        /// Create an entity instance using the implicit database name.
        /// Construction triggers schema/index/trigger initialization for the entity's type.
        /// </summary>
        public SxmEntity()
        {
            initialize();
        }

        /// <summary>
        /// Ensure the entity type has been initialized (table, indexes, triggers). This method is intentionally synchronous
        /// from the caller's perspective but runs the heavy work on the thread pool to avoid UI deadlocks.
        /// </summary>
        private void initialize()
        {
            string typeName = this.GetType().Name;

            // Short critical section: validate DB name and mark this type as initializing.
            lock (lockObject)
            {
                dbNameValidation();

                // Already initialized or already initializing -> nothing to do.
                if (columnNameAndTypeDict.ContainsKey(typeName) || initializingTypes.Contains(typeName))
                    return;

                initializingTypes.Add(typeName);
            }

            try
            {
                // Run the async initialization on the thread‑pool and block synchronously.
                // Using Task.Run avoids deadlocks on synchronization contexts (UI thread).
                Task.Run(async () =>
                {
                    // Process the public properties of the entity.
                    List<MemberInfoWithAlias> propertyInfoWithAliases = getEntityProperties();
                    getColumnNamesAndDataTypes(propertyInfoWithAliases);

                    await createTable().ConfigureAwait(false);

                    List<string> existingStandardIndexes = new List<string>();
                    List<string> existingUniqueIndexes = new List<string>();
                    await getIndexTableStatements(existingStandardIndexes, existingUniqueIndexes).ConfigureAwait(false);

                    await processIndexStatements(IndexType.standard, existingStandardIndexes).ConfigureAwait(false);
                    await processIndexStatements(IndexType.unique, existingUniqueIndexes).ConfigureAwait(false);

                    await processtriggerAttributes().ConfigureAwait(false);
                }).GetAwaiter().GetResult();
            }
            catch
            {
                // On failure, allow retry by removing the initializing marker.
                lock (lockObject)
                {
                    initializingTypes.Remove(typeName);
                }
                throw;
            }
            finally
            {
                // Initialization succeeded — remove the in-progress marker.
                lock (lockObject)
                {
                    initializingTypes.Remove(typeName);
                }
            }
        }

        /// <summary>
        /// Collect public instance properties for the current type and return them wrapped with alias info.
        /// </summary>
        /// <returns>List of MemberInfoWithAlias for the current entity type.</returns>
        private List<MemberInfoWithAlias> getEntityProperties()
        {
            List<MemberInfoWithAlias> propertyInfoWithAliases = new List<MemberInfoWithAlias>();

            foreach (PropertyInfo piItem in GetType().GetProperties())
                propertyInfoWithAliases.Add(new MemberInfoWithAlias(piItem, string.Empty));

            return propertyInfoWithAliases;
        }

        /// <summary>
        /// Persist this entity. If the row does not exist an INSERT is performed; otherwise an UPDATE is performed.
        /// Uses the ambient <see cref="SxmTransaction"/> if present.
        /// </summary>
        public async Task Save()
        {
            // Calls save passing the SxmTransaction from the ambient context.
            await Save(SxmAmbientTransaction.Current);
        }

        /// <summary>
        /// Persist this entity using the supplied transaction (if non-null). Performs insert or update depending on existence.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to use; if null a standalone connection is used.</param>
        public async Task Save(SxmTransaction? sxmTrans)
        {
            if (!await doesRecordExist(sxmTrans))
            {
                buildSaveSql();
                if (sxmTrans == null)
                    await Insert(insertGuidDict[this.GetType().Name]).CAF();
                else
                    await Insert(insertGuidDict[this.GetType().Name], sxmTrans).CAF();
            }
            else
            {
                buildUpdateSql();
                if (sxmTrans == null)
                    await Update(updateGuidDict[this.GetType().Name]).CAF();
                else
                    await Update(updateGuidDict[this.GetType().Name], sxmTrans).CAF();
            }
        }

        // Save Statements.
        private async Task Insert(string sqlStatementName)
        {
            {
                Dictionary<string, object?> result = await SxmStatement.Insert<SxmEntity>(sqlStatementName, this, databaseName).CAF();
                loadDbValues(result);
            }
        }
        private async Task Insert(string sqlStatementName, SxmTransaction sxmTrans)
        {
            {
                Dictionary<string, object?> result = await sxmTrans.Insert<SxmEntity>(sqlStatementName, this).CAF();
                loadDbValues(result);
            }
        }

        // Update statements.
        private async Task Update(string sqlStatementName)
        {
            await SxmStatement.Update<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }
        private async Task Update(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.Update<SxmEntity>(sqlStatementName, this).CAF();
        }

        /// <summary>
        /// Delete this entity from the database. Uses the ambient <see cref="SxmTransaction"/> if present.
        /// </summary>
        public async Task Delete()
        {
            // Calls delete passing the SxmTransaction from the ambient context.
            await Delete(SxmAmbientTransaction.Current);
        }

        /// <summary>
        /// Delete this entity using the provided transaction (if any). No-op if the record does not exist.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to use; if null a standalone connection is used.</param>
        public async Task Delete(SxmTransaction? sxmTrans)
        {
            // If a transaction/connection is provided, check existence using that connection
            // so we see uncommitted rows that live in the same transaction.
            if (!await doesRecordExist(sxmTrans))
                return;

            buildDeleteSql();

            // If no transaction supplied, perform non-transactional delete; otherwise use the provided transaction.
            if (sxmTrans == null)
                await Delete(deleteGuidDict[this.GetType().Name]).CAF();
            else
                await Delete(deleteGuidDict[this.GetType().Name], sxmTrans).CAF();
        }

        // Delete statements.
        private async Task Delete(string sqlStatementName)
        {
            await SxmStatement.Delete<SxmEntity>(sqlStatementName, this, databaseName).CAF();
        }
        private async Task Delete(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.Delete<SxmEntity>(sqlStatementName, this).CAF();
        }

        /// <summary>
        /// Build the cached INSERT SQL for this entity type if not already present.
        /// The SQL and its GUID key are stored in the static statement cache.
        /// </summary>
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
                SxmSqlStatements.addInsertDefinition(insertGuidDict[this.GetType().Name], this.GetType().Name, insertStatement);
            }
        }

        /// <summary>
        /// Build the cached UPDATE SQL for this entity type if not already present.
        /// </summary>
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
                SxmSqlStatements.addUpdateDefinition(updateGuidDict[this.GetType().Name], this.GetType().Name, updateStatement);
            }
        }

        /// <summary>
        /// Build the cached DELETE SQL for this entity type if not already present.
        /// </summary>
        private void buildDeleteSql()
        {
            if (deleteGuidDict.GetValueOrDefault(this.GetType().Name) == default(string))
            {
                string updateStatement = string.Format("DELETE FROM {0} WHERE id=@id", this.GetType().Name);
                deleteGuidDict.Add(this.GetType().Name, Guid.NewGuid().ToString());
                SxmSqlStatements.addDeleteDefinition(deleteGuidDict[this.GetType().Name], this.GetType().Name, updateStatement);
            }
        }

        /// <summary>
        /// Recreate triggers specified by the CreateTrigger attributes on the type.
        /// Existing triggers for the table are dropped before new ones are created.
        /// </summary>
        private async Task processtriggerAttributes()
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                {
                    List<string> ExistingTriggers = await SxmInit.getAllTriggers(sxmTransaction.Connection, this.GetType().Name);
                    foreach (string existingTrigger in ExistingTriggers)
                    {
                        await sxmTransaction.executeCreateTriggerAsync(string.Format("DROP TRIGGER {0}", existingTrigger));
                    }

                    await sxmTransaction.commitTransactionAsync();
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
                        await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                        {
                            foreach (KeyValuePair<string, string> kvp in newTriggerNameList)
                                await sxmTransaction.executeCreateTriggerAsync(kvp.Value);

                            await sxmTransaction.commitTransactionAsync();
                        }
                    }
                    catch (Exception ex) { }
                }
            }
        }

        /// <summary>
        /// Attempt to parse the table name referenced inside a CREATE TRIGGER SQL string.
        /// Returns null if a name cannot be extracted.
        /// This parser is conservative and relies on patterns like "BEFORE/AFTER/INSTEAD ... &lt;table&gt;".
        /// </summary>
        /// <param name="triggerSql">The CREATE TRIGGER SQL text.</param>
        /// <returns>Parsed table name or null if not found.</returns>
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

        /// <summary>
        /// Create or drop indexes for the current type based on attribute definitions and existing indexes.
        /// </summary>
        /// <param name="indexType">Index type (standard or unique).</param>
        /// <param name="existingIndexes">List of existing index names for the table.</param>
        private async Task processIndexStatements(IndexType indexType, List<string> existingIndexes)
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
                    await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                    {
                        foreach (string indexStatement in indexSqlStatements)
                            await sxmTransaction1.executeIndexAsync(indexStatement);

                        await sxmTransaction1.commitTransactionAsync();
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

        /// <summary>
        /// Assign a deterministic index name for each index attribute based on table and field names.
        /// </summary>
        /// <param name="indexArray">List of index descriptors to name.</param>
        /// <param name="tableName">Table name to include in the index name.</param>
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

        /// <summary>
        /// Query the database for the list of index names on the table and populate the provided lists.
        /// </summary>
        private async Task getIndexTableStatements(List<string> existingStandardIndexes, List<string> existingUniqueIndexes)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                {
                    await sxmTransaction.Connection.executeQueryAsync(String.Format("PRAGMA index_list({0})", this.GetType().Name), null as List<object>);

                    while (sxmTransaction.Connection.nextRow() == true)
                    {
                        string? indexName = (string?)sxmTransaction.Connection.getValue("name");
                        if (indexName == null)
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

        /// <summary>
        /// Ensure table columns in the database match the type definition. Adds missing columns and removes extraneous ones.
        /// </summary>
        private async Task reconcileTableColumns()
        {
            Type type = this.GetType();
            Dictionary<string, string> dbTableColumnNameAndType = await SxmInit.getTableColumnNames(databaseName, this.GetType().Name);

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

                        await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                        {
                            await sxmTransaction1.executeAlterTableAsync(alterDefinition);
                            await sxmTransaction1.commitTransactionAsync();
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
                        await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)))
                        {
                            await sxmTransaction1.executeAlterTableAsync(alterDefinition);
                            await sxmTransaction1.commitTransactionAsync();
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

        /// <summary>
        /// Check whether the record for this entity exists using optional transaction context.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to examine; if provided the check will use the transaction's connection.</param>
        /// <returns>True if a row with the current id exists, otherwise false.</returns>
        private async Task<bool> doesRecordExist(SxmTransaction? sxmTrans)
        {
            bool exists = false;

            if (sxmTrans != null && sxmTrans.Connection != null)
            {
                exists = await doesRecordExist(sxmTrans.Connection).ConfigureAwait(false);
            }
            else
            {
                exists = await doesRecordExist().ConfigureAwait(false);
            }

            return exists;
        }


        // New helper: check existence using provided connection (uses same connection/transaction)
        private async Task<bool> doesRecordExist(SxmConnection conn)
        {
            if (conn == null) return false;

            try
            {
                if (id > 0)
                {
                    string sqlSelect = string.Format("SELECT id FROM {0} WHERE id = {1}", this.GetType().Name, id);
                    await conn.executeQueryAsync(sqlSelect, default(List<object>)).ConfigureAwait(false);
                    if (conn.hasRows() == true)
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private async Task<bool> doesRecordExist()
        {
            SxmConnection? sxmConnection = default(SxmConnection);
            try
            {
                if (id > 0)
                {
                    sxmConnection = new SxmConnection(databaseName);
                    string sqlSelect = string.Format("SELECT id FROM {0} WHERE id = {1}", this.GetType().Name, id);
                    await sxmConnection.executeQueryAsync(sqlSelect, default(List<object>));
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

        /// <summary>
        /// Validate or create the implicit database name when none was supplied. Throws if a valid name cannot be determined.
        /// </summary>
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

        /// <summary>
        /// Create the table for the current type if it does not exist; otherwise reconcile columns.
        /// </summary>
        private async Task createTable()
        {
            if (!await SxmInit.doesTableExist(this.GetType().Name, default(SxmConnection)))
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

                SxmSqlStatements.addTableDefinition(string.Format("{0}.{1}", this.databaseName, this.GetType().Name), tableStatement);
                await SxmInit.createTable(this.databaseName, this.GetType().Name);
                SxmSqlStatements.removeTableDefinitions();
            }
            else
                await reconcileTableColumns();
        }

        /// <summary>
        /// Inspect the provided properties and populate the internal column-to-SQL-type mapping for this type.
        /// Respects [Column], [NotColumn], index and foreign key attributes and will populate index dictionaries used later.
        /// </summary>
        /// <param name="propertyInfoWithAliases">List of members with optional alias names.</param>
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

                    // Safe, null-free (preferred here).
                    PropertyInfo? propertyInfo = pi as PropertyInfo;
                    if (propertyInfo == null) continue;  // Should not happen.
                    Type clrType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    //clrType = Nullable.GetUnderlyingType(pi.GetMemberType()) ?? pi.GetMemberType();

                    // Override from ColumnType if specified, for example, [Column(ColumnType = ColumnType.Text)]
                    string? columnType = colAttr?.DataType switch
                    {
                        DataType.Text or DataType.NChar or DataType.NVarChar or DataType.Char or DataType.VarChar => "TEXT",
                        DataType.Int16 or DataType.Int32 or DataType.UInt16 or DataType.UInt32 or DataType.Int64 or DataType.Long => "INTEGER",
                        DataType.Boolean or DataType.DateTime or DataType.Date or DataType.Time => "INTEGER", // unix.milliseconds for time types
                        DataType.Decimal or DataType.UInt64 => "TEXT",  // preserve range
                        DataType.Guid => "TEXT",
                        DataType.Single or DataType.Double => "REAL",
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
                        columnType = clrType == typeof(decimal) ? "TEXT" :
                                     clrType == typeof(string) ? "TEXT" :
                                     clrType == typeof(ulong) ? "TEXT" :
                                     clrType == typeof(Guid) ? "TEXT" :

                                     clrType == typeof(DateTimeOffset) ? "INTEGER" :
                                     clrType == typeof(TimeSpan) ? "INTEGER" :
                                     clrType == typeof(DateOnly) ? "INTEGER" :
                                     clrType == typeof(TimeOnly) ? "INTEGER" :
                                     clrType == typeof(DateTime) ? "INTEGER" :
                                     clrType == typeof(ushort) ? "INTEGER" :
                                     clrType == typeof(sbyte) ? "INTEGER" :
                                     clrType == typeof(short) ? "INTEGER" :
                                     clrType == typeof(long) ? "INTEGER" :
                                     clrType == typeof(uint) ? "INTEGER" :
                                     clrType == typeof(byte) ? "INTEGER" :
                                     clrType == typeof(bool) ? "INTEGER" :
                                     clrType == typeof(int) ? "INTEGER" :

                                     clrType == typeof(double) ? "REAL" :
                                     clrType == typeof(float) ? "REAL" :
                                     
                                     clrType == typeof(byte[]) ? "BLOB" :
                                     null;
                    }

                    if (columnType != null)
                    {
                        if (string.IsNullOrEmpty(propertyInfoWithAlias.alias))
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
        /// <summary>
        /// Copy matching public instance properties from <paramref name="mapSource"/> into this instance and persist.
        /// Useful for mapping values from DTOs or other objects and saving in a single operation.
        /// </summary>
        /// <param name="mapSource">Source object to map values from.</param>
        public async Task MapAndSave(object mapSource)
        {
            MapProperties(mapSource);
            // Persist the entity after mapping. Use CAF() to follow project's await pattern.
            await Save().CAF();
        }

        /// <summary>
        /// Copy matching public instance properties from <paramref name="source"/> to this instance.
        /// The destination must inherit from SxmEntity. Properties named "id" and "synchId" are ignored.
        /// Only properties with exactly the same PropertyType (no conversions) are copied.
        /// Indexer properties are ignored. Both properties must be public instance properties and the destination property must be writable.
        /// </summary>
        /// <param name="source">Source object to copy values from.</param>
        private void MapProperties(object source)
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