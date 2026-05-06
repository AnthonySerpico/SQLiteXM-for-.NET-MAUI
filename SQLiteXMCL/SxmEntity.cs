using LinqToDB.Mapping;
using LinqToDB.SqlQuery;
using SQLiteXM.Internal.Threading;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using static LinqToDB.DataProvider.SqlServer.SqlServerProviderAdapter;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM
{
    /// <summary>
    /// Base class for mapped entities that provides automatic table/index/trigger initialization
    /// plus convenience persistence operations (Save, Update, Delete).
    /// Derived types are automatically inspected for column/index/foreign key attributes when an
    /// instance is constructed, and the database schema is created or reconciled as required.
    /// </summary>
    /// <remarks>
    /// Design note: this implementation intentionally uses the entity's simple CLR type name
    /// (that is, <c>Type.Name</c>) as the logical table identifier and as the key for several
    /// internal initialization and statement caches. When two different CLR types share the
    /// same simple name (for example, types with identical names in different namespaces or
    /// assemblies) or when the same simple name is used with different database names, the
    /// library detects the condition at runtime and throws an <see cref="InvalidOperationException"/>
    /// to fail fast rather than allow silent data corruption.
    /// 
    /// Rationale:
    /// - Failing fast surfaces incorrect usage or naming conflicts immediately and prevents
    ///   subtle, hard-to-diagnose data integrity issues that could occur if different CLR types
    ///   were implicitly mapped to the same table identifier.
    /// - Using the simple type name keeps SQL table identifiers predictable and stable across
    ///   builds and avoids changing on refactors that only affect assembly-qualified identity.
    /// 
    /// How to avoid collisions:
    /// - Ensure entity class simple names are unique across your solution and referenced assemblies,
    ///   especially when targeting multiple databases.
    /// - Use distinct class names per database when a single solution works with multiple databases.
    /// 
    /// Future alternatives:
    /// - If you prefer tolerant behavior (no runtime exception) you can change the cache-keying
    ///   strategy to include the CLR identity and database name (for example,
    ///   <c>type.AssemblyQualifiedName + \"|\" + databaseName</c>). This is a compatibility-impacting
    ///   change and may require updates in other components that depend on the current simple-name
    ///   based behavior, so it is intentionally not applied automatically.
    /// 
    /// Visibility and maintenance:
    /// - This remark documents the intended behavior for maintainers and consumers so that the
    ///   fail-fast behavior is explicit and discoverable.
    /// </remarks>

    [Table(IsColumnAttributeRequired = false)]
    public class SxmEntity
    {
        // Prevent multiple concurrent initializations for the same entity type.
        private static readonly object _lockObject = new object();

        // Cache mapping CLR `Type` -> resolved `[Table].Name` (or `null` when missing/empty).
        // Thread-safe ConcurrentDictionary used to avoid repeated reflection on first access.
        private static readonly ConcurrentDictionary<Type, string?> _tableAttributeNameCache = new ConcurrentDictionary<Type, string?>();

        // Protect against different CLR types that share the same simple Name (namespace/assembly collisions).
        // Maps simple type Name -> type identity (AssemblyQualifiedName preferred).
        // Used only to detect and fail fast on collisions; NOT used as SQL table identifier.
        private static readonly ConcurrentDictionary<string, string?> _entityTypeMap = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);

        // Protect against using the same simple Name across different databases.
        // Maps simple type Name -> database name where it was first initialized.
        // Used only to detect and fail fast on cross-database collisions for same simple name.
        private static readonly ConcurrentDictionary<string, string?> _entityDatabaseMap = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);

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
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _columnNameAndTypeDict = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>
        /// Create an entity instance using the implicit database name.
        /// Construction triggers schema/index/trigger initialization for the entity's type.
        /// </summary>
        public SxmEntity()
        {
            SxmInit.EnsureInitialized();
            this._databaseName = ResolveTableAttributeName();
            Initialize();
        }

        /// <summary>
        /// Resolve and cache the <see cref="TableAttribute.Name"/> for this entity's CLR type.
        /// The first caller pays the reflection cost; subsequent callers return the cached value.
        /// </summary>
        /// <returns>The configured table/database name from <see cref="TableAttribute"/>, or <c>null</c> when not set.</returns>
        internal string? ResolveTableAttributeName()
        {
            Type ctorType = GetType();

            if (_tableAttributeNameCache.TryGetValue(ctorType, out string? cachedName))
                return cachedName;

            string? resolved = _tableAttributeNameCache.GetOrAdd(ctorType, t =>
            {
                TableAttribute? tbl = t.GetCustomAttribute<TableAttribute>(inherit: false);
                string? name = tbl?.Name;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            });

            return resolved;
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
            var type = GetType();
            string tableName = type.Name;

            // Unique CLR identity for collision detection.
            string typeIdentity = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;

            lock (_lockObject)
            {
                DbNameValidation();

                // Prevent using the same simple table name across different databases.
                // The first database that registers the simple name wins; others must use distinct class names.
                if (!_entityDatabaseMap.TryAdd(tableName, _databaseName))
                {
                    if (_entityDatabaseMap.TryGetValue(tableName, out var existingDb) &&
                        !string.Equals(existingDb, _databaseName, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"CRITICAL: Entity database collision detected. The entity class '{tableName}' is already registered for database '{existingDb}'.\n" +
                            $"Current database attempting registration: '{_databaseName}'.\n" +
                            $"CAUSE: The same entity class name is being used across multiple databases.\n" +
                            $"SOLUTION: Use distinct entity class names for each database, or ensure the same entity class is only used with one database.\n" +
                            $"EXAMPLE: Instead of using 'User' for both databases, use 'DatabaseAUser' and 'DatabaseBUser'.");
                    }
                    // If the existing mapping equals our database, continue - another instance for the same DB registered earlier.
                }

                // Prevent different CLR types (different namespaces/assemblies) that share the same simple Name.
                if (!_entityTypeMap.TryAdd(tableName, typeIdentity))
                {
                    if (_entityTypeMap.TryGetValue(tableName, out var existingTypeIdentity) &&
                        !string.Equals(existingTypeIdentity, typeIdentity, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"CRITICAL: Entity name collision detected. The simple class name '{tableName}' is already registered by another type '{existingTypeIdentity}'.\n" +
                            $"Current type attempting registration: '{typeIdentity}'.\n" +
                            $"CAUSE: Two different classes with the same name ('{tableName}') exist in different namespaces or assemblies.\n" +
                            $"SOLUTION: Rename one of the entity classes to ensure unique simple names across your entire application.\n" +
                            $"IMPORTANT: SQLiteXM uses the simple class name (Type.Name) as the table identifier. " +
                            $"Classes in different namespaces MUST have unique names to prevent schema conflicts and data corruption.");
                    }
                    // If the existing type mapping equals our type identity, continue - another instance of the same CLR type registered earlier.
                }
            }

            Lazy<Task> lazyInit = _initTasks.GetOrAdd(tableName, _ => new Lazy<Task>(
                    () => Task.Run(async () =>
                    {
                        ValidateDynamicallyAccessedMembersAttribute(type);
                        List<MemberInfoWithAlias> props = GetEntityProperties();
                        GetColumnNamesAndDataTypes(props);

                        bool newTable;
                        if (!(newTable = await CreateTableAsync().ConfigureFalse()))  // Create the table if it does not already exist.
                            await AddColumnsAsync().ConfigureFalse(); // If this is an already existing table in the DB, check to see if new columns were added.

                        var std = new List<string>();
                        var uniq = new List<string>();
                        await GetIndexTableStatementsAsync(std, uniq).ConfigureFalse();

                        await ProcessIndexStatementsAsync(IndexType.Standard, std).ConfigureFalse();
                        await ProcessIndexStatementsAsync(IndexType.Unique, uniq).ConfigureFalse();
                        await ProcessTriggerAttributesAsync().ConfigureFalse();

                        // If this is an already existing table in the DB, drop columns now that everything else has been reconciled.
                        if (!newTable)
                            await DropColumnsAsync().ConfigureFalse();
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
                _initTasks.TryRemove(tableName, out _); // allow retry on failure.

                // Best-effort cleanup: remove the mappings only if they still match this attempt's database/type.
                // Do it under the same lock used for registration to avoid races with other threads.
                try
                {
                    lock (_lockObject)
                    {
                        if (_entityTypeMap.TryGetValue(tableName, out var mappedType) &&
                            string.Equals(mappedType, typeIdentity, StringComparison.Ordinal))
                        {
                            _entityTypeMap.TryRemove(tableName, out _);
                        }

                        if (_entityDatabaseMap.TryGetValue(tableName, out var mappedDb) &&
                            string.Equals(mappedDb, _databaseName, StringComparison.Ordinal))
                        {
                            _entityDatabaseMap.TryRemove(tableName, out _);
                        }
                    }
                }
                catch
                {
                    // Swallow cleanup errors to avoid hiding the original exception.
                }

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

            foreach (PropertyInfo piItem in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
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
            await InsertOrReplaceAsync().ConfigureFalse();
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
            await InsertOrReplaceAsync(sxmTrans).ConfigureFalse();
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
            await SaveAsync().ConfigureFalse();
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
            await SaveAsync(sxmTrans).ConfigureFalse();
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
            await SaveAsync(SxmAmbientTransaction.Current).ConfigureFalse();
        }

        /// <summary>
        /// Saves the current entity to the database using the provided transaction, if any.
        /// This method automatically determines whether to insert a new record or update an existing one.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmTransaction"/> to execute within.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the insert or update SQL statement is not found for the table.</exception>
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

            if (!await DoesRecordExistAsync(sxmTrans).ConfigureFalse())
            {
                BuildSaveSql();

                if (!_insertGuidDict.TryGetValue(tableName, out var insertGuid) || string.IsNullOrEmpty(insertGuid))
                    throw new InvalidOperationException($"Insert statement not found for '{tableName}'.");

                if (sxmTrans == null)
                    await InsertAsync(insertGuid).ConfigureFalse();
                else
                    await InsertAsync(insertGuid, sxmTrans).ConfigureFalse();
            }
            else
            {
                BuildUpdateSql();

                if (!_updateGuidDict.TryGetValue(tableName, out var updateGuid) || string.IsNullOrEmpty(updateGuid))
                    throw new InvalidOperationException($"Update statement not found for '{tableName}'.");

                if (sxmTrans == null)
                    await UpdateAsync(updateGuid).ConfigureFalse();
                else
                    await UpdateAsync(updateGuid, sxmTrans).ConfigureFalse();
            }
        }

        // Save Statements.
        private async Task InsertAsync(string sqlStatementName)
        {
            Dictionary<string, object?> result = await SxmStatement.InsertAsync<SxmEntity>(sqlStatementName, this, _databaseName).ConfigureFalse();
            SxmHelpers.LoadDbValues(result, this);
        }
        private async Task InsertAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            Dictionary<string, object?> result = await sxmTrans.InsertAsync<SxmEntity>(sqlStatementName, this).ConfigureFalse();
            SxmHelpers.LoadDbValues(result, this);
        }

        // Update statements.
        private async Task UpdateAsync(string sqlStatementName)
        {
            await SxmStatement.UpdateAsync<SxmEntity>(sqlStatementName, this, _databaseName).ConfigureFalse();
        }
        private async Task UpdateAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.UpdateAsync<SxmEntity>(sqlStatementName, this).ConfigureFalse();
        }

        /// <summary>
        /// Delete this entity from the database. Uses the ambient <see cref="SxmTransaction"/> if present.
        /// </summary>
        public async Task DeleteAsync()
        {
            // Calls delete passing the SxmTransaction from the ambient context.
            await DeleteAsync(SxmAmbientTransaction.Current).ConfigureFalse();
        }

        /// <summary>
        /// Delete this entity using the provided transaction (if any). No-op if the record does not exist.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to use; if null a standalone connection is used.</param>
        public async Task DeleteAsync(SxmTransaction? sxmTrans)
        {
            // If a transaction/connection is provided, check existence using that connection
            // so we see uncommitted rows that live in the same transaction.
            if (!await DoesRecordExistAsync(sxmTrans).ConfigureFalse())
                return;

            BuildDeleteSql();
            string tableName = this.GetType().Name;

            if (!_deleteGuidDict.TryGetValue(tableName, out var deleteGuid) || string.IsNullOrEmpty(deleteGuid))
                throw new InvalidOperationException($"Delete statement not found for '{tableName}'.");

            // If no transaction supplied, perform non-transactional delete; otherwise use the provided transaction.
            if (sxmTrans == null)
                await DeleteAsync(deleteGuid).ConfigureFalse();
            else
                await DeleteAsync(deleteGuid, sxmTrans).ConfigureFalse();
        }

        // Delete statements.
        private async Task DeleteAsync(string sqlStatementName)
        {
            await SxmStatement.DeleteAsync<SxmEntity>(sqlStatementName, this, _databaseName).ConfigureFalse();
        }
        private async Task DeleteAsync(string sqlStatementName, SxmTransaction sxmTrans)
        {
            await sxmTrans.DeleteAsync<SxmEntity>(sqlStatementName, this).ConfigureFalse();
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

                if (columns.Length == 0)
                    throw new InvalidOperationException($"Update statement cannot be built for '{tableName}' because no updatable columns were found. At least one defined column is required.");

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
        private async Task ProcessTriggerAttributesAsync()
        {
            string tableName = this.GetType().Name;
            List<TriggerDefinition> triggerStatementsList = SxmSqlStatements.TriggerStatements[_databaseName] as List<TriggerDefinition>;

            CreateTrigger[] customAttributes = (CreateTrigger[])this.GetType().GetCustomAttributes(typeof(CreateTrigger), true);
            if (customAttributes.Length > 0)
            {
                foreach (CreateTrigger myAttribute in customAttributes)
                {
                    // Guard: skip null or whitespace trigger SQL strings to avoid executing null.
                    if (!string.IsNullOrWhiteSpace(myAttribute.triggerSql))
                    {
                        triggerStatementsList.Add(new TriggerDefinition(myAttribute.triggerSql));
                    }
                }
            }

            if (triggerStatementsList.Count > 0)
            {
                SxmConnection sxmConnection = new SxmConnection(_databaseName, shared: false);
                try
                {
                    await SxmInit.AddTriggersAsync(sxmConnection, _databaseName).ConfigureFalse();
                }
                finally
                {
                    await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
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

            if (indexType == IndexType.Standard)
            {
                firstArray = (CreateIndex[])type.GetCustomAttributes(typeof(CreateIndex), true);
                secondArray = _standardIndexDict.TryGetValue(tableName, out var stdBag) ? stdBag.ToArray() : Array.Empty<IIndexVars>();
            }
            else if (indexType == IndexType.Unique)
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
                    await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)).ConfigureFalse())
                    {
                        foreach (string indexStatement in indexSqlStatements)
                            await sxmTransaction1.ExecuteIndexAsync(indexStatement).ConfigureFalse();

                        await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
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
                if (indexType == IndexType.Standard)
                {
                    _standardIndexDict.TryRemove(tableName, out _);
                }

                if (indexType == IndexType.Unique)
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

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)).ConfigureFalse())
                {
                    await sxmTransaction.Connection.ExecuteQueryAsync(pragma, null as List<object>).ConfigureFalse();

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
                SxmLogging.Log(ex, $"GetIndexTableStatementsAsync failure for table '{tableName}'.");
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

            Dictionary<string, string> dbTableColumnNameAndType = await SxmInit.GetTableColumnNamesAsync(_databaseName, tableName).ConfigureFalse();

            try
            {
                foreach (KeyValuePair<string, string> kvp in _columnNameAndTypeDict[tableName])
                {
                    if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
                    {
                        string alterDefinition = $"ALTER TABLE {quotedTable} ADD COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)} {kvp.Value}";

                        await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)).ConfigureFalse())
                        {
                            await sxmTransaction.ExecuteAlterTableAsync(alterDefinition).ConfigureFalse();
                            await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
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
                SxmLogging.Log(ex, $"AddColumnsAsync failure for table '{tableName}'.");
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

            Dictionary<string, string> dbTableColumnNameAndType = await SxmInit.GetTableColumnNamesAsync(_databaseName, tableName).ConfigureFalse();

            try
            {
                foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
                {
                    if (!_columnNameAndTypeDict[tableName].ContainsKey(kvp.Key) && !IsIgnored(kvp.Key))
                    {
                        string alterDefinition = $"ALTER TABLE {quotedTable} DROP COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)}";
                        await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(_databaseName)).ConfigureFalse())
                        {
                            await sxmTransaction1.ExecuteAlterTableAsync(alterDefinition).ConfigureFalse();
                            await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
                        }

                        SxmInit.RemoveColumnNameType(tableName, kvp.Key);
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DropColumnsAsync failure for table '{tableName}'.");
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
                exists = await DoesRecordExistAsync(sxmTrans.Connection).ConfigureFalse();
            }
            else
            {
                exists = await DoesRecordExistAsync().ConfigureFalse();
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
                    await conn.ExecuteQueryAsync(sqlSelect, new List<object> { id }).ConfigureFalse();
                    if (conn.HasRows() == true)
                        return true;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DoesRecordExistAsync failure for table '{conn.DatabaseName}' table '{this.GetType().Name}'.");
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
                    await sxmConnection.ExecuteQueryAsync(sqlSelect, new List<object> { id }).ConfigureFalse();
                    if (sxmConnection.HasRows() == true)
                        return true;
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"DoesRecordExistAsync failure for table '{sxmConnection?.DatabaseName}' table '{this.GetType().Name}'.");
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
                await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
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

            SxmConnection? sxmConnection = null;
            bool tableExists = false;

            try
            {
                sxmConnection = new SxmConnection(_databaseName);
                tableExists = await SxmInit.DoesTableExistAsync(tableName, sxmConnection).ConfigureFalse();
            }
            finally
            {
                await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
            }

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
                await SxmInit.CreateTableAsync(this._databaseName, tableName).ConfigureFalse();
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
                _columnNameAndTypeDict.GetOrAdd(typeName, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

                // Get the [Table] attribute to check IsColumnAttributeRequired.
                TableAttribute? tbl = type.GetCustomAttribute<TableAttribute>(inherit: false);
                bool columnIsRequired = tbl?.IsColumnAttributeRequired ?? false; // Check IsColumnAttributeRequired.

                foreach (MemberInfoWithAlias propertyInfoWithAlias in propertyInfoWithAliases)
                {
                    MemberInfo memberInfo = propertyInfoWithAlias.memberInfo;
                    string memberInfoName = memberInfo.Name;

                    // Skip "id" and "synchId" properties
                    if (IsIgnored(memberInfoName))
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
                    RequiredNotNull? requiredNotNull = memberInfo.GetCustomAttribute<RequiredNotNull>(inherit: false);
                    bool hasCreateIndex = memberInfo.IsDefined(typeof(CreateIndex), inherit: false);
                    bool hasCreateUniqueIndex = memberInfo.IsDefined(typeof(CreateUniqueIndex), inherit: false);
                    CreateForeignKey? isForeignKey = memberInfo.GetCustomAttribute<CreateForeignKey>(inherit: false);

                    string notNull = string.Empty;
                    if (requiredNotNull is not null)
                    {
                        notNull = requiredNotNull.defaultValue is not null ? $" not null default {SxmHelpers.FormatSqlLiteral(requiredNotNull.defaultValue)}" : " not null";
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
                    if (isForeignKey is not null)
                    {
                        _foreignKeyAttributeList ??= new List<ForeignKeyAttributes>();

                        _foreignKeyAttributeList.Add(new ForeignKeyAttributes
                        {
                            fieldName = columnName,
                            foreignTable = isForeignKey.foreignTable,
                        });

                        SxmHelpers.CreateAssociation(type, columnName, isForeignKey.foreignTable);
                    }

                    // Safe, null-free (preferred here).
                    Type clrType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    // Override from ColumnType if specified, for example, [Column(DataType = ColumnType.Text)]
                    string? overrideType = colAttr?.DataType switch
                    {
                        DataType.Text or DataType.NChar or DataType.NVarChar or DataType.Char or DataType.VarChar => "TEXT",
                        DataType.Int16 or DataType.Int32 or DataType.UInt16 or DataType.UInt32 or DataType.Int64 or DataType.Long => "INTEGER",
                        DataType.Boolean or DataType.DateTime or DataType.Date or DataType.Time => "INTEGER", // unix.milliseconds for time types
                        DataType.Decimal or DataType.UInt64 => "TEXT",  // preserve range
                        DataType.Single or DataType.Double => "REAL",
                        DataType.Guid or DataType.Binary or DataType.Blob or DataType.VarBinary => "BLOB",
                        _ => null
                    };

                    if (overrideType is not null)
                    {
                        bool allowed =
                            (IsTimeType(clrType) && overrideType.Equals("TEXT", StringComparison.OrdinalIgnoreCase)) ||
                            (clrType == typeof(Guid) && overrideType.Equals("TEXT", StringComparison.OrdinalIgnoreCase));

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
                                 clrType == typeof(Guid) ? "BLOB" :

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
            await SaveAsync().ConfigureFalse();
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

        private void ValidateDynamicallyAccessedMembersAttribute(Type entityType)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            // Skip validation on platforms where AOT/trimming is not supported, as the attribute is not required.
            //if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
            {
                bool hasAttribute = entityType
                    .GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), inherit: false)
                    .Cast<DynamicallyAccessedMembersAttribute>()
                    .Any(attr => attr.MemberTypes == DynamicallyAccessedMemberTypes.All);

                if (!hasAttribute)
                {
                    throw new InvalidOperationException(
                        $"The class '{entityType.Name}' is missing required AOT annotations. " +
                        $"Please add: [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] " +
                        $"to the class definition.");
                }
            }
        }
    }
}