using LinqToDB.Mapping;
using Microsoft.VisualBasic;
using SQLiteXM.Internal;
using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Intrinsics.Arm;
using System.Xml.Linq;
using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;
using static SQLiteXM.SxmDefines;
using System.Diagnostics.CodeAnalysis;

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
        private static readonly object _lockObject = new object();

        /// <summary>
        /// Per-table initialization gate.
        /// 
        /// Maps a table/entity key to a single lazily-created initialization <see cref="Task"/>.
        /// The first caller creates and starts the task; all subsequent callers retrieve the
        /// same task and synchronously wait for it to complete.
        /// 
        /// This guarantees that schema initialization (tables, indexes, triggers, etc.)
        /// runs exactly once per table key and prevents use-before-ready races across threads.
        /// 
        /// IMPORTANT:
        /// The key is derived from the entity type name. Entity classes in different namespaces
        /// MUST NOT share the same class name, or initialization collisions will occur.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<Task>> _initTasks = new();

        // Statement concurrent GUID caches.
        private static ConcurrentDictionary<string, string> _insertGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static ConcurrentDictionary<string, string> _updateGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static ConcurrentDictionary<string, string> _deleteGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // Index dictionaries: thread-safe per-type bags of index descriptors.
        private static readonly ConcurrentDictionary<string, ConcurrentBag<IndexPropertyAttributes>> _uniqueIndexDict = new ConcurrentDictionary<string, ConcurrentBag<IndexPropertyAttributes>>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, ConcurrentBag<IndexPropertyAttributes>> _standardIndexDict = new ConcurrentDictionary<string, ConcurrentBag<IndexPropertyAttributes>>(StringComparer.Ordinal);

        // Column map per type: nested concurrent dictionary for safe concurrent reads/writes.
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _columnNameAndTypeDict = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.Ordinal);

        private string? _databaseName;
        private List<ForeignKeyAttributes>? _foreignKeyAttributeList = default(List<ForeignKeyAttributes>);

        /// <summary>
        /// Primary key column. Mapped to the SQLite INTEGER PRIMARY KEY AUTOINCREMENT column named "id".
        /// </summary>
        /// <remarks>
        /// The database determines this value (AUTOINCREMENT). The ORM reads the generated rowid
        /// (using SQLite's last_insert_rowid on the same connection/transaction) and populates this
        /// property immediately after an INSERT. Consumers must not assign this value; the setter is
        /// internal to prevent external modification. The property is used by the ORM for existence
        /// checks, WHERE clauses, and relationship linking.
        /// </remarks>        
        [SuppressMessage("Naming", "IDE1006:Naming Styles", Justification = "Public column name preserved to match DB schema and external consumers.")]
        [Column, PrimaryKey, Identity]
        public virtual long id { get; set; }

        /// <summary>
        /// Optional synchronization identifier stored in the database as a BLOB.
        /// </summary>
        /// <remarks>
        /// This value is managed by the ORM for synchronization purposes. The ORM may generate or
        /// update the value after insert/update operations and will populate the property from the
        /// database. Consumers may read this value but must not set it; the setter is internal to
        /// prevent accidental external modification. Do not rely on setting this property prior to
        /// Save() unless the ORM is explicitly configured to include it in INSERT statements.
        /// </remarks>
        [SuppressMessage("Naming", "IDE1006:Naming Styles", Justification = "Public column name preserved to match DB schema and external consumers.")]
        [Column(DataType = DataType.Blob)]
        public virtual Guid? synchId { get; internal set; }

        // Needs to throw an exception if databaseName is invalid.
        /// <summary>
        /// Create an entity instance bound to the specified database name.
        /// Construction triggers schema/index/trigger initialization for the entity's type.
        /// </summary>
        /// <param name="databaseName">Database name to use for initialization. If null, an implicit DB name is created.</param>
        public SxmEntity(string? databaseName)
        {
            this._databaseName = databaseName;
            Initialize();
        }
        // Needs to throw an exception if databaseName is invalid.
        /// <summary>
        /// Create an entity instance using the implicit database name.
        /// Construction triggers schema/index/trigger initialization for the entity's type.
        /// </summary>
        public SxmEntity()
        {
            Initialize();
        }

        /// <summary>
        /// Ensures the database schema for this entity type (table, indexes, triggers)
        /// is fully initialized before use.
        /// 
        /// Initialization is guaranteed to:
        ///  - Run exactly once per table/entity key.
        ///  - Block all concurrent callers until the first initialization completes.
        ///  - Propagate initialization failures to all waiting callers.
        ///  - Allow retry on subsequent calls if initialization fails.
        /// 
        /// Internally, initialization work is executed on the thread pool and this method
        /// blocks synchronously until completion, avoiding UI-thread deadlocks while still
        /// providing a synchronous API to callers.
        /// 
        /// IMPORTANT:
        /// The initialization key is based on the entity class name. Entity classes in
        /// different namespaces MUST NOT share the same name, or they will be treated
        /// as the same table during initialization.
        /// </summary>
        private void Initialize()
        {
            // NOTE: Entity class names must be globally unique across namespaces. Do not drop columns until after processing indexes/triggers.
            string tableName = GetType().Name;

            lock (_lockObject)
            {
                DbNameValidation();
            }

            var lazyInit = _initTasks.GetOrAdd(tableName, _ => new Lazy<Task>(
                    () => Task.Run(async () =>
                    {
                        var props = GetEntityProperties();
                        GetColumnNamesAndDataTypes(props);

                        bool newTable;
                        if (!(newTable = await CreateTableAsync().ConfigureAwait(false)))  // Create the table if it does not already exist.
                            await AddColumnsAsync(); // If this is an already existing table in the DB, check to see if new columns were added.

                        var std = new List<string>();
                        var uniq = new List<string>();
                        await GetIndexTableStatementsAsync(std, uniq).ConfigureAwait(false);

                        await ProcessIndexStatementsAsync(IndexType.standard, std).ConfigureAwait(false);
                        await ProcessIndexStatementsAsync(IndexType.unique, uniq).ConfigureAwait(false);
                        await ProcesstriggerAttributesAsync().ConfigureAwait(false);

                        // If this is an already existing table in the DB, drop columns now that everything else has been reconciled.
                        if (!newTable)
                            await DropColumnsAsync().ConfigureAwait(false);
                    }),
                    LazyThreadSafetyMode.ExecutionAndPublication
                )
            );

            try
            {
                lazyInit.Value.GetAwaiter().GetResult();
            }
            catch
            {
                _initTasks.TryRemove(tableName, out _); // allow retry on failure
                throw;
            }
        }

        /// <summary>
        /// Collect public instance properties for the current type and return them wrapped with alias info.
        /// </summary>
        /// <returns>List of MemberInfoWithAlias for the current entity type.</returns>
        private List<MemberInfoWithAlias> GetEntityProperties()
        {
            List<MemberInfoWithAlias> propertyInfoWithAliases = new List<MemberInfoWithAlias>();

            foreach (PropertyInfo piItem in GetType().GetProperties())
                propertyInfoWithAliases.Add(new MemberInfoWithAlias(piItem, string.Empty));

            return propertyInfoWithAliases;
        }

        /// <summary>
        /// Saves the current entity to the database by either inserting it if it is new,
        /// or updating it if it already exists. 
        /// 
        /// This method is a semantic alias for <see cref="SaveAsync"/>. 
        /// It ensures that the entity's identity field (Id) is correctly populated 
        /// after a successful insert.
        /// </summary>
        /// <remarks>
        /// Currently, this method behaves the same as <see cref="SaveAsync"/>:
        /// - If the entity does not exist in the database, it is inserted.
        /// - If the entity exists, it is updated in place.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this method does not delete
        /// existing rows. This preserves triggers, foreign keys, and the entity's identity.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrUpdateAsync()
        {
            await InsertOrReplaceAsync();
        }

        /// <summary>
        /// Saves the current entity to the database using the provided transaction, 
        /// either inserting it if it is new, or updating it if it already exists.
        /// 
        /// This method is a semantic alias for <see cref="SaveAsync(SxmTransaction?)"/>. 
        /// It ensures that the entity's identity field (Id) is correctly populated 
        /// after a successful insert.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmTransaction"/> to execute within.</param>
        /// <remarks>
        /// Currently, this method behaves the same as <see cref="SaveAsync(SxmTransaction?)"/>:
        /// - If the entity does not exist in the database, it is inserted.
        /// - If the entity exists, it is updated in place.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this method does not delete
        /// existing rows. This preserves triggers, foreign keys, and the entity's identity.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrUpdateAsync(SxmTransaction? sxmTrans)
        {
            await InsertOrReplaceAsync(sxmTrans);
        }

        /// <summary>
        /// Saves the current entity to the database by either inserting it if it is new,
        /// or updating it if it already exists. This is a semantic alias for <see cref="SaveAsync"/>.
        /// </summary>
        /// <remarks>
        /// The name "InsertOrReplace" is provided for semantic clarity.
        /// Internally, it behaves identically to <see cref="SaveAsync"/>:
        /// - Inserts if the entity is new.
        /// - Updates in place if it already exists.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this implementation preserves 
        /// the entity's identity, foreign keys, and triggers.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrReplaceAsync()
        {
            await SaveAsync();
        }

        /// <summary>
        /// Saves the current entity to the database using the provided transaction,
        /// either inserting it if it is new, or updating it if it already exists.
        /// This is a semantic alias for <see cref="SaveAsync(SxmTransaction?)"/>.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmTransaction"/> to execute within.</param>
        /// <remarks>
        /// The name "InsertOrReplace" is provided for semantic clarity.
        /// Internally, it behaves identically to <see cref="SaveAsync(SxmTransaction?)"/>:
        /// - Inserts if the entity is new.
        /// - Updates in place if it already exists.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this implementation preserves 
        /// the entity's identity, foreign keys, and triggers.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrReplaceAsync(SxmTransaction? sxmTrans)
        {
            await SaveAsync(sxmTrans);
        }

        /// <summary>
        /// Saves the current entity to the database using the ambient transaction context, if any.
        /// This method automatically determines whether to insert a new record or update an existing one.
        /// </summary>
        /// <remarks>
        /// - If the entity does not exist in the database, an INSERT operation is performed.
        /// - If the entity already exists, an UPDATE operation is performed.
        /// - The entity's identity field (Id) is automatically populated after a successful insert.
        /// - This method respects the current ambient transaction, if available, otherwise executes without a transaction.
        /// - Unlike SQLite's native "INSERT OR REPLACE", this implementation updates existing rows in place
        ///   rather than deleting and reinserting them, preserving triggers, foreign keys, and the entity's identity.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveAsync()
        {
            // Calls save passing the SxmTransaction from the ambient context.
            await SaveAsync(SxmAmbientTransaction.Current);
        }

        /// <summary>
        /// Saves the current entity to the database using the provided transaction, if any.
        /// This method automatically determines whether to insert a new record or update an existing one.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmTransaction"/> to execute within.</param>
        /// <remarks>
        /// - If the entity does not exist in the database, an INSERT operation is performed.
        /// - If the entity already exists, an UPDATE operation is performed.
        /// - The entity's identity field (Id) is automatically populated after a successful insert.
        /// - If <paramref name="sxmTrans"/> is null, the operation is executed without a transaction.
        /// - Unlike SQLite's native "INSERT OR REPLACE", this implementation updates existing rows in place
        ///   rather than deleting and reinserting them, preserving triggers, foreign keys, and the entity's identity.
        /// - Throws <see cref="InvalidOperationException"/> if the insert or update SQL statement is not found for the table.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task SaveAsync(SxmTransaction? sxmTrans)
        {
            string tableName = this.GetType().Name;

            if (!await DoesRecordExistAsync(sxmTrans))
            {
                BuildSaveSql();

                if (!_insertGuidDict.TryGetValue(tableName, out var insertGuid) || string.IsNullOrEmpty(insertGuid))
                    throw new InvalidOperationException($"Insert statement not found for '{tableName}'.");

                if (sxmTrans == null)
                    await InsertAsync(insertGuid).CAF();
                else
                    await InsertAsync(insertGuid, sxmTrans).CAF();
            }
            else
            {
                BuildUpdateSql();

                if (!_updateGuidDict.TryGetValue(tableName, out var updateGuid) || string.IsNullOrEmpty(updateGuid))
                    throw new InvalidOperationException($"Update statement not found for '{tableName}'.");

                if (sxmTrans == null)
                    await UpdateAsync(updateGuid).CAF();
                else
                    await UpdateAsync(updateGuid, sxmTrans).CAF();
            }
        }

        // Save Statements.
        private async Task InsertAsync(string sqlStatementName)
        {
            {
                Dictionary<string, object?> result = await SxmStatement.InsertAsync<SxmEntity>(sqlStatementName, this, _databaseName).CAF();
                SxmHelpers.LoadDbValues(result, this);
            }
        }
        private async Task InsertAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            {
                Dictionary<string, object?> result = await sxmTrans.InsertAsync<SxmEntity>(sqlStatementName, this).CAF();
                SxmHelpers.LoadDbValues(result, this);
            }
        }

        // Update statements.
        private async Task UpdateAsync(string sqlStatementName)
        {
            await SxmStatement.UpdateAsync<SxmEntity>(sqlStatementName, this, _databaseName).CAF();
        }
        private async Task UpdateAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.UpdateAsync<SxmEntity>(sqlStatementName, this).CAF();
        }

        /// <summary>
        /// Delete this entity from the database. Uses the ambient <see cref="SxmTransaction"/> if present.
        /// </summary>
        public async Task DeleteAsync()
        {
            // Calls delete passing the SxmTransaction from the ambient context.
            await DeleteAsync(SxmAmbientTransaction.Current);
        }

        /// <summary>
        /// Delete this entity using the provided transaction (if any). No-op if the record does not exist.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to use; if null a standalone connection is used.</param>
        public async Task DeleteAsync(SxmTransaction? sxmTrans)
        {
            // If a transaction/connection is provided, check existence using that connection
            // so we see uncommitted rows that live in the same transaction.
            if (!await DoesRecordExistAsync(sxmTrans))
                return;

            BuildDeleteSql();
            string tableName = this.GetType().Name;

            if (!_deleteGuidDict.TryGetValue(tableName, out var deleteGuid) || string.IsNullOrEmpty(deleteGuid))
                throw new InvalidOperationException($"Delete statement not found for '{tableName}'.");

            // If no transaction supplied, perform non-transactional delete; otherwise use the provided transaction.
            if (sxmTrans == null)
                await DeleteAsync(deleteGuid).CAF();
            else
                await DeleteAsync(deleteGuid, sxmTrans).CAF();
        }

        // Delete statements.
        private async Task DeleteAsync(string sqlStatementName)
        {
            await SxmStatement.DeleteAsync<SxmEntity>(sqlStatementName, this, _databaseName).CAF();
        }
        private async Task DeleteAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.DeleteAsync<SxmEntity>(sqlStatementName, this).CAF();
        }

        /// <summary>
        /// Build the cached INSERT SQL for this entity type if not already present.
        /// The SQL and its GUID key are stored in the static statement cache.
        /// </summary>
        private void BuildSaveSql()
        {
            Type type = this.GetType();
            string tableName = type.Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            // The per-type column map must already exist. Fail fast if not.
            if (!_columnNameAndTypeDict.TryGetValue(tableName, out var perTypeColumns))
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Call initialize() before building SQL.");


            // Atomically register a GUID and SQL once. The valueFactory will run only when the key is absent.
            _insertGuidDict.GetOrAdd(tableName, _ =>
            {
                var columns = perTypeColumns.Keys
                .Where(k => !string.Equals(k, "synchId", StringComparison.OrdinalIgnoreCase) && !string.Equals(k, "id", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

                string insertStatement;
                if (columns.Length == 0)
                {
                    insertStatement = $"INSERT INTO {quotedTable} DEFAULT VALUES";
                }
                else
                {
                    string insertColumns = string.Join(", ", columns.Select(c => SxmHelpers.QuoteIdentifier(c)));
                    string insertValues = string.Join(", ", columns.Select(c => "@" + c));
                    insertStatement = $"INSERT INTO {quotedTable} ({insertColumns}) VALUES ({insertValues})";
                }

                /*
                string insertColumns = string.Join(", ", columns);
                string insertValues = string.Join(", ", columns.Select(c => "@" + c));
                string insertStatement = string.Format("INSERT INTO {0} ({1}) VALUES ({2})", tableName, insertColumns, insertValues);
                */

                string newGuid = Guid.NewGuid().ToString();
                SxmSqlStatements.AddInsertDefinition(newGuid, tableName, insertStatement);
                return newGuid;
            });
        }

        /// <summary>
        /// Build the cached UPDATE SQL for this entity type if not already present.
        /// </summary>
        private void BuildUpdateSql()
        {
            string tableName = this.GetType().Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            // The per-type column map must already exist. Fail fast if not.
            if (!_columnNameAndTypeDict.TryGetValue(tableName, out var perTypeColumns))
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Call initialize() before building SQL.");

            // Atomically register a GUID and SQL once. The valueFactory will run only when the key is absent.
            _updateGuidDict.GetOrAdd(tableName, _ =>
            {
                var columns = perTypeColumns.Keys
                .Where(k => !string.Equals(k, "synchId", StringComparison.OrdinalIgnoreCase) && !string.Equals(k, "id", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

                string setClause = string.Join(", ", columns.Select(c => $"{SxmHelpers.QuoteIdentifier(c)}=@{c}"));
                string updateStatement = $"UPDATE {quotedTable} SET {setClause} WHERE {SxmHelpers.QuoteIdentifier("id")}=@id";
                string newGuid = Guid.NewGuid().ToString();


                /*string setClause = string.Join(", ", columns.Select(c => $"{c}=@{c}"));
                string updateStatement = string.Format("UPDATE {0} SET {1} WHERE id=@id", tableName, setClause);
                string newGuid = Guid.NewGuid().ToString();*/

                SxmSqlStatements.AddUpdateDefinition(newGuid, tableName, updateStatement);
                return newGuid;
            });
        }

        /// <summary>
        /// Build the cached DELETE SQL for this entity type if not already present.
        /// </summary>
        private void BuildDeleteSql()
        {
            string tableName = this.GetType().Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            // The per-type column map should exist; if not, fail fast so callers can fix initialization ordering.
            if (!_columnNameAndTypeDict.TryGetValue(tableName, out _))
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Call initialize() before building SQL.");

            _deleteGuidDict.GetOrAdd(tableName, _ =>
            {
                string deleteStatement = $"DELETE FROM {quotedTable} WHERE {SxmHelpers.QuoteIdentifier("id")}=@id";
                //string deleteStatement = string.Format("DELETE FROM {0} WHERE id=@id", tableName);
                string newGuid = Guid.NewGuid().ToString();

                SxmSqlStatements.AddDeleteDefinition(newGuid, tableName, deleteStatement);
                return newGuid;
            });
        }

        /// <summary>
        /// Recreate triggers specified by the CreateTrigger attributes on the type.
        /// Existing triggers for the table are dropped before new ones are created.
        /// </summary>
        private async Task ProcesstriggerAttributesAsync()
        {
            string tableName = this.GetType().Name;

            try
            {
                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                {
                    List<string> ExistingTriggers = await SxmInit.GetAllTriggersAsync(sxmTransaction.Connection, tableName);
                    foreach (string existingTrigger in ExistingTriggers)
                    {
                        await sxmTransaction.ExecuteCreateTriggerAsync($"DROP TRIGGER {SxmHelpers.QuoteIdentifier(existingTrigger)}");
                    }

                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"ProcesstriggerAttributesAsync failure for table '{tableName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ProcesstriggerAttributesAsync failure for table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            List<string> newTriggerNameList = new List<string>();
            var customAttributes = (CreateTrigger[])this.GetType().GetCustomAttributes(typeof(CreateTrigger), true);

            if (customAttributes.Length > 0)
            {
                foreach (var myAttribute in customAttributes)
                {
                    string? triggerSql = myAttribute.triggerSql;
                    newTriggerNameList.Add(triggerSql);
                }

                if (newTriggerNameList.Count > 0)
                {
                    try
                    {
                        await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                        {
                            foreach (string trigger in newTriggerNameList)
                                await sxmTransaction.ExecuteCreateTriggerAsync(trigger);

                            await sxmTransaction.CommitTransactionAsync();
                        }
                    }
                    catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                    {
                        // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                        SxmLogging.Log(ex, $"ProcesstriggerAttributesAsync failure for table '{tableName}'.");
                        throw;
                    }
                    catch (System.Exception ex)
                    {
                        string errStr = $"ProcesstriggerAttributesAsync failure for table '{tableName}'.";
                        SxmLogging.Log(ex, errStr);
                        throw ExceptionHelper.Wrap(ex, errStr);
                    }
                }
            }
        }

        /// <summary>
        /// Create or drop indexes for the current type based on attribute definitions and existing indexes.
        /// </summary>
        /// <param name="indexType">Index type (standard or unique).</param>
        /// <param name="existingIndexes">List of existing index names for the table.</param>
        private async Task ProcessIndexStatementsAsync(IndexType indexType, List<string> existingIndexes)
        {
            List<string> indexSqlStatements = new List<string>();

            var type = this.GetType();
            string unique = string.Empty;
            string tableName = type.Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            IIndexVars[]? firstArray = default(IIndexVars[]);
            IIndexVars[]? secondArray = default(IIndexVars[]);

            if (indexType == IndexType.standard)
            {
                firstArray = (CreateIndex[])type.GetCustomAttributes(typeof(CreateIndex), true);
                secondArray = _standardIndexDict.TryGetValue(tableName, out var stdBag) ? stdBag.ToArray() : Array.Empty<IIndexVars>();
            }
            else if (indexType == IndexType.unique)
            {
                firstArray = (CreateUniqueIndex[])type.GetCustomAttributes(typeof(CreateUniqueIndex), true);
                secondArray = _uniqueIndexDict.TryGetValue(tableName, out var uniqBag) ? uniqBag.ToArray() : Array.Empty<IIndexVars>();

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

            AssignIndexNames(customAttributes!, tableName);

            try
            {
                foreach (var myAttribute in customAttributes)
                {
                    if (!existingIndexes.Contains(myAttribute.indexName))
                    {
                        string indexFields = string.Join(", ", myAttribute.indexFields.Select(f => SxmHelpers.QuoteIdentifier(f)));
                        string createIndexSql = $"CREATE {unique} INDEX {SxmHelpers.QuoteIdentifier(myAttribute.indexName)} ON {quotedTable} ({indexFields})";
                        indexSqlStatements.Add(createIndexSql);
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
                        indexSqlStatements.Add($"DROP INDEX {SxmHelpers.QuoteIdentifier(indexName)}");
                }

                if (indexSqlStatements.Count > 0)
                {
                    await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                    {
                        foreach (string indexStatement in indexSqlStatements)
                            await sxmTransaction1.ExecuteIndexAsync(indexStatement);

                        await sxmTransaction1.CommitTransactionAsync();
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"ProcessIndexStatementsAsync failure for table '{tableName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"ProcessIndexStatementsAsync failure for table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
                if (indexType == IndexType.standard)
                {
                    _standardIndexDict.TryRemove(tableName, out _);
                }

                if (indexType == IndexType.unique)
                {
                    _uniqueIndexDict.TryRemove(tableName, out _);
                }
            }
        }

        /// <summary>
        /// Assign a deterministic index name for each index attribute based on table and field names.
        /// </summary>
        /// <param name="indexArray">List of index descriptors to name.</param>
        /// <param name="tableName">Table name to include in the index name.</param>
        private void AssignIndexNames(List<IIndexVars> indexArray, string tableName)
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
        private async Task GetIndexTableStatementsAsync(List<string> existingStandardIndexes, List<string> existingUniqueIndexes)
        {
            string tableName = this.GetType().Name;
            string pragma = $"PRAGMA index_list({SxmHelpers.QuoteIdentifier(tableName)})";

            try
            {

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                {
                    await sxmTransaction.Connection.ExecuteQueryAsync(pragma, null as List<object>);

                    while (sxmTransaction.Connection.NextRow() == true)
                    {
                        string? indexName = (string?)sxmTransaction.Connection.GetValue("name");
                        if (indexName == null)
                            continue;

                        var raw = sxmTransaction.Connection.GetValue("unique");
                        bool isUnique = raw != null && Convert.ToInt64(raw) == 1;
                        if (isUnique)
                            existingUniqueIndexes.Add(indexName);
                        else
                            existingStandardIndexes.Add(indexName);
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"GetIndexTableStatementsAsync failure for table '{tableName}.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"GetIndexTableStatementsAsync failure for table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
            }
        }

        /// <summary>
        /// Ensure table columns in the database match the type definition. Adds missing columns and removes extraneous ones.
        /// </summary>
        private async Task AddColumnsAsync()
        {
            Type type = this.GetType();
            string tableName = type.Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            Dictionary<string, string> dbTableColumnNameAndType = await SxmInit.GetTableColumnNamesAsync(_databaseName, tableName);

            try
            {
                foreach (KeyValuePair<string, string> kvp in _columnNameAndTypeDict[tableName])
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = $"ALTER TABLE {quotedTable} ADD COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)} {kvp.Value}";

                        await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                        {
                            await sxmTransaction1.ExecuteAlterTableAsync(alterDefinition);
                            await sxmTransaction1.CommitTransactionAsync();
                        }

                        int offset = 0;
                        string? value = default(string);

                        if ((offset = kvp.Value.IndexOf(' ')) != -1)
                            value = kvp.Value.Substring(0, offset);
                        else
                            value = kvp.Value;

                        SxmInit.AddColumnNameType(tableName, kvp.Key, value);
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"AddColumnsAsync failure for table '{tableName}.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"AddColumnsAsync failure for table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
            }
        }

        private async Task DropColumnsAsync()
        {
            Type type = this.GetType();
            string tableName = type.Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            Dictionary<string, string> dbTableColumnNameAndType = await SxmInit.GetTableColumnNamesAsync(_databaseName, tableName);

            try
            {
                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!_columnNameAndTypeDict[tableName].ContainsKey(kvp.Key) && !kvp.Key.Equals("id") && !kvp.Key.Equals("synchId"))
                    {
                        string alterDefinition = $"ALTER TABLE {quotedTable} DROP COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)}";
                        await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)))
                        {
                            await sxmTransaction1.ExecuteAlterTableAsync(alterDefinition);
                            await sxmTransaction1.CommitTransactionAsync();
                        }

                        SxmInit.RemoveColumnNameType(tableName, kvp.Key);
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DropColumnsAsync failure for table '{tableName}.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DropColumnsAsync failure for table '{tableName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
            }
        }

        /// <summary>
        /// Check whether the record for this entity exists using optional transaction context.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to examine; if provided the check will use the transaction's connection.</param>
        /// <returns>True if a row with the current id exists, otherwise false.</returns>
        private async Task<bool> DoesRecordExistAsync(SxmTransaction? sxmTrans)
        {
            bool exists = false;

            if (sxmTrans != null && sxmTrans.Connection != null)
            {
                exists = await DoesRecordExistAsync(sxmTrans.Connection).ConfigureAwait(false);
            }
            else
            {
                exists = await DoesRecordExistAsync().ConfigureAwait(false);
            }

            return exists;
        }


        // New helper: check existence using provided connection (uses same connection/transaction)
        private async Task<bool> DoesRecordExistAsync(SxmConnection conn)
        {
            if (conn == null) return false;

            try
            {
                if (id > 0)
                {
                    string tableName = this.GetType().Name;

                    string sqlSelect = $"SELECT {SxmHelpers.QuoteIdentifier("id")} FROM {SxmHelpers.QuoteIdentifier(tableName)} WHERE {SxmHelpers.QuoteIdentifier("id")} = @p0";
                    await conn.ExecuteQueryAsync(sqlSelect, new List<object> { id }).ConfigureAwait(false);
                    if (conn.HasRows() == true)
                        return true;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DoesRecordExistAsync failure for table '{conn.DatabaseName} table '{this.GetType().Name}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DoesRecordExistAsync failure for table '{conn.DatabaseName}' table '{this.GetType().Name}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return false;
        }

        private async Task<bool> DoesRecordExistAsync()
        {
            SxmConnection? sxmConnection = default(SxmConnection);
            try
            {
                if (id > 0)
                {
                    string tableName = this.GetType().Name;

                    sxmConnection = new SxmConnection(_databaseName);
                    string sqlSelect = $"SELECT {SxmHelpers.QuoteIdentifier("id")} FROM {SxmHelpers.QuoteIdentifier(tableName)} WHERE {SxmHelpers.QuoteIdentifier("id")} = @p0";
                    await sxmConnection.ExecuteQueryAsync(sqlSelect, new List<object> { id }).ConfigureAwait(false);
                    if (sxmConnection.HasRows() == true)
                        return true;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DoesRecordExistAsync failure for table '{sxmConnection?.DatabaseName} table '{this.GetType().Name}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"DoesRecordExistAsync failure for table '{sxmConnection?.DatabaseName}' table '{this.GetType().Name}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
            finally
            {
                sxmConnection?.DestroyConnection();
            }

            return false;
        }

        /// <summary>
        /// Validate or create the implicit database name when none was supplied. Throws if a valid name cannot be determined.
        /// </summary>
        private void DbNameValidation()
        {
            if (this._databaseName == null)
            {
                this._databaseName = SxmDatabaseDescriptor.DefaultDatabase;
                if (this._databaseName == null)
                    throw new InvalidDataException("A default database has not been configured in any of your SQL statements files.");
            }
            else
            {
                // Check if database name is in the list of databases.
                if (!SxmDatabaseDescriptor.IsDatabaseDefined(_databaseName))
                    throw new InvalidDataException($"The database '{_databaseName}' has not been configured. Check the spelling matches the database name in your SQL statements file.");
            }
        }

        /// <summary>
        /// Create the table for the current type if it does not exist; otherwise reconcile columns.
        /// </summary>
        private async Task<bool> CreateTableAsync()
        {
            bool tableCreated = false;
            string tableName = this.GetType().Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            SxmConnection sxmConnection = new SxmConnection(_databaseName);
            bool tableExists = await SxmInit.DoesTableExistAsync(tableName, sxmConnection);
            sxmConnection?.DestroyConnection();

            if (!tableExists)
            {
                // Build CREATE TABLE with quoted identifiers.
                tableCreated = true;
                var sb = new System.Text.StringBuilder();
                sb.Append($"CREATE TABLE {quotedTable} (");
                sb.Append($"{SxmHelpers.QuoteIdentifier("id")} INTEGER PRIMARY KEY AUTOINCREMENT");

                foreach (KeyValuePair<string, string> kvp in _columnNameAndTypeDict[tableName])
                {
                    sb.Append(", ");
                    sb.Append($"{SxmHelpers.QuoteIdentifier(kvp.Key)} {kvp.Value}");
                }

                if (_foreignKeyAttributeList != default(List<ForeignKeyAttributes>))
                {
                    foreach (ForeignKeyAttributes attribute in _foreignKeyAttributeList)
                    {
                        sb.Append($", FOREIGN KEY({SxmHelpers.QuoteIdentifier(attribute.fieldName)}) REFERENCES {SxmHelpers.QuoteIdentifier(attribute.foreignTable)}({SxmHelpers.QuoteIdentifier("id")})");
                    }

                    _foreignKeyAttributeList = default(List<ForeignKeyAttributes>);
                }

                sb.Append(")");

                SxmSqlStatements.AddTableDefinition(string.Format("{0}.{1}", this._databaseName, tableName), sb.ToString());
                await SxmInit.CreateTableAsync(this._databaseName, tableName);
                SxmSqlStatements.RemoveTableDefinitions();
            }

            return tableCreated;
        }

        /// <summary>
        /// Inspect the provided properties and populate the internal column-to-SQL-type mapping for this type.
        /// Respects [Column], [NotColumn], index and foreign key attributes and will populate index dictionaries used later.
        /// </summary>
        /// <param name="propertyInfoWithAliases">List of members with optional alias names.</param>
        private void GetColumnNamesAndDataTypes(List<MemberInfoWithAlias> propertyInfoWithAliases)
        {
            if (propertyInfoWithAliases != null && propertyInfoWithAliases.Count > 0)
            {
                var type = GetType();
                var typeName = type.Name;

                // Ensure per-type column map exists.
                _columnNameAndTypeDict.GetOrAdd(typeName, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));

                // Get the [Table] attribute to check IsColumnAttributeRequired.
                TableAttribute? tbl = type.GetCustomAttribute<TableAttribute>(inherit: false);
                bool columnIsRequired = tbl?.IsColumnAttributeRequired ?? false; // Check IsColumnAttributeRequired.

                foreach (MemberInfoWithAlias propertyInfoWithAlias in propertyInfoWithAliases)
                {
                    MemberInfo memberInfo = propertyInfoWithAlias.memberInfo;
                    string memberInfoName = memberInfo.Name;

                    // Skip "id" and "synchId" properties
                    if (memberInfoName is "id" or "synchId")
                        continue;

                    if (memberInfo is not PropertyInfo propertyInfo)
                        continue; // defensive: skip non-property members

                    // Skip properties marked with [NotColumn].
                    if (memberInfo.IsDefined(typeof(NotColumnAttribute), false))
                        continue;

                    // Get the [Column] attribute, if present, otherwise it's null.
                    ColumnAttribute? colAttr = memberInfo.GetCustomAttribute<ColumnAttribute>(inherit: false);
                    if (columnIsRequired && colAttr == null)
                        continue; // Must have [Column] attribute in order to map to a database, but it's missing.

                    // Typed attribute reads (no dictionary, no strings)
                    RequiredNotNull? nn = memberInfo.GetCustomAttribute<RequiredNotNull>(inherit: false);
                    bool hasCreateIndex = memberInfo.IsDefined(typeof(CreateIndex), inherit: false);
                    bool hasCreateUniqueIndex = memberInfo.IsDefined(typeof(CreateUniqueIndex), inherit: false);
                    CreateForeignKey? fk = memberInfo.GetCustomAttribute<CreateForeignKey>(inherit: false);

                    string notNull = string.Empty;
                    if (nn is not null)
                    {
                        notNull = nn.defaultValue is not null ? $" not null default {nn.defaultValue}" : " not null";
                    }

                    // Resolve mapped field name once (member name or alias)
                    string columnName = string.IsNullOrEmpty(propertyInfoWithAlias.alias) ? memberInfoName : propertyInfoWithAlias.alias;

                    // CreateIndex
                    if (hasCreateIndex)
                    {
                        var bag = _standardIndexDict.GetOrAdd(typeName, _ => new ConcurrentBag<IndexPropertyAttributes>());
                        bag.Add(new IndexPropertyAttributes(columnName, typeName));
                    }

                    // CreateUniqueIndex
                    if (hasCreateUniqueIndex)
                    {
                        var bag = _uniqueIndexDict.GetOrAdd(typeName, _ => new ConcurrentBag<IndexPropertyAttributes>());
                        bag.Add(new IndexPropertyAttributes(columnName, typeName));
                    }

                    // CreateForeignKey
                    if (fk is not null)
                    {
                        _foreignKeyAttributeList ??= new List<ForeignKeyAttributes>();

                        _foreignKeyAttributeList.Add(new ForeignKeyAttributes
                        {
                            fieldName = columnName,
                            foreignTable = fk.foreignTable,
                        });

                        SxmHelpers.CreateAssociation(type, columnName, fk.foreignTable);
                    }

                    // Safe, null-free (preferred here).
                    Type clrType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    // Override from ColumnType if specified, for example, [Column(ColumnType = ColumnType.Text)]
                    string? overrideType = colAttr?.DataType switch
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

                    if (overrideType is not null)
                    {
                        bool allowed =
                            (IsTimeType(clrType) && overrideType.Equals("TEXT", StringComparison.OrdinalIgnoreCase)) ||
                            (clrType == typeof(Guid) && overrideType.Equals("BLOB", StringComparison.OrdinalIgnoreCase));

                        if (!allowed)
                            overrideType = null;
                    }

                    string? columnType = overrideType ?? ClrTypeToColumnType(clrType);

                    if (columnType != null)
                    {
                        if (!_columnNameAndTypeDict[typeName].TryAdd(columnName, columnType + notNull))
                        {
                            throw new InvalidOperationException(
                                        $"Duplicate mapped column name '{columnName}' on type '{typeName}'. " +
                                        $"Members '{memberInfoName}' (alias '{propertyInfoWithAlias.alias ?? ""}') " +
                                        $"and another member resolved to the same mapped name.");
                        }
                    }
                }
            }
        }

        private bool IsTimeType(Type clrType)
        {
            return clrType == typeof(DateTimeOffset) ||
                   clrType == typeof(TimeSpan) ||
                   clrType == typeof(DateOnly) ||
                   clrType == typeof(TimeOnly) ||
                   clrType == typeof(DateTime);
        }

        private string? ClrTypeToColumnType(Type clrType)
        {
            string? columnType = clrType == typeof(decimal) ? "TEXT" :
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

            return columnType;
        }

        // MapAndSave maps properties from the source into this instance and then persists the entity.
        /// <summary>
        /// Copy matching public instance properties from <paramref name="mapSource"/> into this instance and persist.
        /// Useful for mapping values from DTOs or other objects and saving in a single operation.
        /// </summary>
        /// <param name="mapSource">Source object to map values from.</param>
        public async Task MapAndSaveAsync(object mapSource)
        {
            MapProperties(mapSource);
            // Persist the entity after mapping. Use CAF() to follow project's await pattern.
            await SaveAsync().CAF();
        }

        /// <summary>
        /// Copy matching public instance properties from <paramref name="source"/> to this instance.
        /// The destination must inherit from SxmEntity. Properties named "id" and "synchId" are ignored.
        /// Only properties with exactly the same PropertyType (no conversions) are copied.
        /// Indexer properties are ignored. Both properties must be public instance properties and the destination property must be writable.
        /// </summary>
        /// <param name="source">Source object to copy values from.</param>
        public void MapProperties(object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Type sourceType = source.GetType();
            Type destinationType = this.GetType();

            TableAttribute? tbl = destinationType.GetCustomAttribute<TableAttribute>(inherit: false);
            bool columnIsRequired = tbl?.IsColumnAttributeRequired ?? false; // Check IsColumnAttributeRequired.

            var destProps = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                           .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                                           .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

            foreach (PropertyInfo sourceProperty in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var destinationProperty = ProcessPropertyInfo(sourceProperty, destProps);
                if (destinationProperty == null)
                    continue;

                ColumnAttribute? colAttr = destinationProperty.GetCustomAttribute<ColumnAttribute>(inherit: false);
                if (columnIsRequired && colAttr == null)
                    continue; // Must have [Column] attribute in order to be mapped, but it's missing.

                object? sourcePropertyValue = sourceProperty.GetValue(source);

                // If null, set only if destination accepts null (nullable or reference type)
                if (sourcePropertyValue == null)
                {
                    bool destAllowsNull = !destinationProperty.PropertyType.IsValueType || Nullable.GetUnderlyingType(destinationProperty.PropertyType) != null;

                    if (destAllowsNull)
                        destinationProperty.SetValue(this, null);

                    continue;
                }

                // Set value directly (types are identical)
                destinationProperty.SetValue(this, sourcePropertyValue);
            }
        }

        private PropertyInfo? ProcessPropertyInfo(PropertyInfo srcProp, IDictionary<string, PropertyInfo> destProps)
        {
            // Skip indexers and non-readable properties
            if (srcProp.GetIndexParameters().Length > 0 || !srcProp.CanRead)
                return null;

            // Ignore id and synchId on source
            if (IsIgnored(srcProp.Name))
                return null;

            if (!destProps.TryGetValue(srcProp.Name, out var destProp))
                return null;

            if (IsIgnored(destProp.Name))
                return null;

            if (destProp.PropertyType != srcProp.PropertyType)
                return null;

            return destProp;
        }

        private static bool IsIgnored(string name) => string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) ||
                                                      string.Equals(name, "synchId", StringComparison.OrdinalIgnoreCase);
    }
}