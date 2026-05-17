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
    /// Base class for mapped entities that provides persistence operations (Save, Update, Delete)
    /// and property mapping utilities for domain entities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Schema Registration:</strong>
    /// Entity schema (tables, indexes, triggers, foreign keys) must be registered explicitly at
    /// application startup via <see cref="SxmDatabase.RegisterEntitiesAsync(Type[])"/> before instantiating
    /// entities. Entity construction no longer performs schema initialization.
    /// </para>
    /// 
    /// <para>
    /// <strong>Recommended Usage Pattern:</strong>
    /// </para>
    /// <code>
    /// // At application startup (e.g., MauiProgram.cs)
    /// await SxmDatabase.InitializeAsync("statements.json");
    /// await SxmDatabase.RegisterEntitiesAsync(
    ///     typeof(Dog),
    ///     typeof(Cat),
    ///     typeof(Owner)
    /// );
    /// 
    /// // Later: entity construction is lightweight, no schema side effects
    /// var dog = new Dog { Name = "Buddy" };
    /// await dog.SaveAsync();
    /// </code>
    /// 
    /// <para>
    /// <strong>Table Naming and Collision Detection:</strong>
    /// This implementation uses the entity's simple CLR type name (<c>Type.Name</c>) as the
    /// logical table identifier and as the key for internal statement caches. When two different
    /// CLR types share the same simple name (e.g., types with identical names in different
    /// namespaces or assemblies) or when the same simple name is used across different databases,
    /// schema registration detects the collision at startup and throws an
    /// <see cref="InvalidOperationException"/> to fail fast rather than allow silent data corruption.
    /// </para>
    /// 
    /// <para>
    /// <strong>Rationale for Simple Names:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     Failing fast at startup surfaces incorrect usage or naming conflicts immediately,
    ///     preventing subtle, hard-to-diagnose data integrity issues.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Using simple type names keeps SQL table identifiers predictable and stable across
    ///     builds, avoiding changes on refactors that only affect assembly-qualified identity.
    ///     </description>
    ///   </item>
    /// </list>
    /// 
    /// <para>
    /// <strong>How to Avoid Name Collisions:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     Ensure entity class simple names are unique across your solution and referenced
    ///     assemblies, especially when targeting multiple databases.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     Use distinct class names per database when a single solution works with multiple
    ///     databases (e.g., <c>DatabaseAUser</c> and <c>DatabaseBUser</c> instead of two
    ///     <c>User</c> classes).
    ///     </description>
    ///   </item>
    /// </list>
    /// 
    /// <para>
    /// <strong>Entity Lifecycle:</strong>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///     Construction validates that <see cref="SxmDatabase.InitializeAsync"/> has been called
    ///     and resolves the database name from <c>[Table(Database = "...")]</c> attribute
    ///     or the default database.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     <see cref="SaveAsync()"/> automatically determines whether to INSERT or UPDATE
    ///     based on whether the entity exists in the database.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///     The <see cref="id"/> property is automatically populated after INSERT via
    ///     SQLite's <c>last_insert_rowid()</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// 
    /// <para>
    /// <strong>Thread Safety:</strong>
    /// Static caches (<c>_columnNameAndTypeDict</c>, statement GUIDs) are thread-safe.
    /// Entity instances themselves are not thread-safe and should not be shared across threads
    /// without external synchronization.
    /// </para>
    /// </remarks>

    [Table(IsColumnAttributeRequired = false)]
    public class SxmEntity
    {
        // Cache mapping CLR `Type` -> resolved `[Table].Database` (or `null` when missing/empty).
        // Thread-safe ConcurrentDictionary used to avoid repeated reflection on first access.
        private static readonly ConcurrentDictionary<Type, string?> _tableAttributeNameCache = new ConcurrentDictionary<Type, string?>();

        // Statement concurrent GUID caches.
        private static ConcurrentDictionary<string, string> _insertGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static ConcurrentDictionary<string, string> _updateGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static ConcurrentDictionary<string, string> _deleteGuidDict = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        // Column map per type: nested concurrent dictionary for safe concurrent reads/writes.
        // Populated by SxmSchemaRegistration during schema registration; accessed here for SQL statement building.
        internal static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _columnNameAndTypeDict = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private string? _databaseName;

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
        /// Create an entity instance.
        /// </summary>
        /// <remarks>
        /// Schema initialization (table creation, indexes, triggers, etc.) is no longer performed during construction.
        /// Call <see cref="SxmDatabase.RegisterEntitiesAsync"/> at application startup to register entity types
        /// and initialize their schemas explicitly.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the entity type has not been registered via <see cref="SxmDatabase.RegisterEntitiesAsync"/>.
        /// </exception>
        public SxmEntity()
        {
            SxmDatabase.EnsureInitialized();

            // RULE 2: Fail-fast if entity type has not been registered
            Type entityType = GetType();
            if (!SxmSchemaRegistration.IsSchemaRegistered(entityType))
            {
                throw new InvalidOperationException(
                    $"Entity type '{entityType.Name}' has not been registered. " +
                    $"Schema registration is required before creating entity instances. " +
                    $"Add the following to your application startup code (after SxmDatabase.InitializeAsync): " +
                    $"await SxmDatabase.RegisterEntitiesAsync(typeof({entityType.Name}));");
            }

            this._databaseName = ResolveTableAttributeDatabaseName();

            // Validate database name
            ValidateDatabaseName();
        }

        /// <summary>
        /// Validate or resolve the database name. Throws if a valid name cannot be determined.
        /// </summary>
        private void ValidateDatabaseName()
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
        /// Resolve and cache the <see cref="TableAttribute.Database"/> for this entity's CLR type.
        /// The first caller pays the reflection cost; subsequent callers return the cached value.
        /// </summary>
        /// <returns>The configured table/database name from <see cref="TableAttribute"/>, or <c>null</c> when not set.</returns>
        internal string? ResolveTableAttributeDatabaseName()
        {
            Type ctorType = GetType();

            if (_tableAttributeNameCache.TryGetValue(ctorType, out string? cachedName))
                return cachedName;

            string? resolved = _tableAttributeNameCache.GetOrAdd(ctorType, t =>
            {
                TableAttribute? tbl = t.GetCustomAttribute<TableAttribute>(inherit: false);
                string? name = tbl?.DatabaseName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            });

            return resolved;
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
        /// This method is a semantic alias for <see cref="SaveAsync(SxmSqlTransaction?)"/>. 
        /// It ensures that the entity's identity field (Id) is correctly populated 
        /// after a successful insert.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmSqlTransaction"/> to execute within.</param>
        /// <remarks>
        /// Currently, this method behaves the same as <see cref="SaveAsync(SxmSqlTransaction?)"/>:
        /// - If the entity does not exist in the database, it is inserted.
        /// - If the entity exists, it is updated in place.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this method does not delete
        /// existing rows. This preserves triggers, foreign keys, and the entity's identity.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrUpdateAsync(SxmSqlTransaction? sxmTrans)
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
        /// This is a semantic alias for <see cref="SaveAsync(SxmSqlTransaction?)"/>.
        /// </summary>
        /// <param name="sxmTrans">An optional <see cref="SxmSqlTransaction"/> to execute within.</param>
        /// <remarks>
        /// The name "InsertOrReplace" is provided for semantic clarity.
        /// Internally, it behaves identically to <see cref="SaveAsync(SxmSqlTransaction?)"/>:
        /// - Inserts if the entity is new.
        /// - Updates in place if it already exists.
        /// 
        /// Unlike SQLite's native "INSERT OR REPLACE", this implementation preserves 
        /// the entity's identity, foreign keys, and triggers.
        /// </remarks>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public async Task InsertOrReplaceAsync(SxmSqlTransaction? sxmTrans)
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
        /// <param name="sxmTrans">An optional <see cref="SxmSqlTransaction"/> to execute within.</param>
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
        public async Task SaveAsync(SxmSqlTransaction? sxmTrans)
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
        private async Task InsertAsync(string sqlStatementName, SxmSqlTransaction sxmTrans)
        {
            Dictionary<string, object?> result = await sxmTrans.InsertAsync<SxmEntity>(sqlStatementName, this).ConfigureFalse();
            SxmHelpers.LoadDbValues(result, this);
        }

        // Update statements.
        private async Task UpdateAsync(string sqlStatementName)
        {
            await SxmStatement.UpdateAsync<SxmEntity>(sqlStatementName, this, _databaseName).ConfigureFalse();
        }
        private async Task UpdateAsync(string sqlStatementName, SxmSqlTransaction sxmTrans)
        {
            await sxmTrans.UpdateAsync<SxmEntity>(sqlStatementName, this).ConfigureFalse();
        }

        /// <summary>
        /// Delete this entity from the database. Uses the ambient <see cref="SxmSqlTransaction"/> if present.
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
        public async Task DeleteAsync(SxmSqlTransaction? sxmTrans)
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
        private async Task DeleteAsync(string sqlStatementName, SxmSqlTransaction sxmTrans)
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
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Schema must be registered via SxmDatabase.RegisterEntitiesAsync before using entities.");


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
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Schema must be registered via SxmDatabase.RegisterEntitiesAsync before using entities.");

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
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Schema must be registered via SxmDatabase.RegisterEntitiesAsync before using entities.");

            _deleteGuidDict.GetOrAdd(tableName, _ =>
            {
                string deleteStatement = $"DELETE FROM {quotedTable} WHERE {SxmHelpers.QuoteIdentifier("id")}=@id";
                string newGuid = Guid.NewGuid().ToString();

                SxmSqlStatements.AddDeleteDefinition(newGuid, tableName, deleteStatement);
                return newGuid;
            });
        }

        /// <summary>
        /// Check whether the record for this entity exists using optional transaction context.
        /// </summary>
        /// <param name="sxmTrans">Optional transaction to examine; if provided the check will use the transaction's connection.</param>
        /// <returns>True if a row with the current id exists, otherwise false.</returns>
        private async Task<bool> DoesRecordExistAsync(SxmSqlTransaction? sxmTrans)
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
    }
}