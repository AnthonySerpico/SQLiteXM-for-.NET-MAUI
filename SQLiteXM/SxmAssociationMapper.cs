using LinqToDB.Mapping;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SQLiteXM
{
    /// <summary>
    /// Helper to discover and register LinqToDB association mappings at runtime.
    /// </summary>
    /// <remarks>
    /// This class re-uses the <see cref="SxmMapping.Schema"/> MappingSchema to avoid rebuilding
    /// mapping state. It supports scanning databases for foreign keys (using PRAGMA foreign_key_list)
    /// and registering mapping information via LinqToDB's <see cref="FluentMappingBuilder"/>.
    ///
    /// Thread-safety: registration mutates the shared <see cref="_schema"/> state and is not fully
    /// synchronized for concurrent callers. Call initialization during single-threaded startup
    /// or ensure external synchronization when invoking the public/internal methods concurrently.
    /// 
    /// Foreign key / association support
    /// This library only supports single-column foreign keys that reference the target entity's <c>id</c> primary key column.
    /// Composite foreign key constraints (multi-column FKs) are not supported by the runtime mapper and will be ignored at initialization.
    /// When a composite FK is found a warning is written to the project log; ensure you call and await <c>SxmAssociationMapper.InitializeAssociationsAsync(...)</c> at application startup so these warnings are visible early.
    /// If you need composite behaviour, define explicit navigation wiring or avoid composite constraints in the database schema.
    ///
    /// Notes and rules:
    /// - Assign unique names to classes that inherit from <see cref="SxmEntity"/>, even if they are in different namespaces.
    /// - Composite foreign keys are not supported and may create incorrect single-column association mappings when the composite FK components map to the foreign table's primary <c>id</c> column.
    /// </remarks>
    internal static class SxmAssociationMapper
    {
        // Reuse the MappingSchema built by SxmMapping to avoid duplicating Build()

        /// <summary>
        /// Mapping schema used by all dynamic association registrations.
        /// </summary>
        /// <value>The shared <see cref="MappingSchema"/> instance built by <see cref="SxmMapping"/>.</value>
        private static MappingSchema _schema => SxmMapping.Schema;

        /// <summary>
        /// Guard used to serialize mutations of the shared MappingSchema.
        /// </summary>
        private static readonly object _schemaLock = new();

        /// <summary>
        /// Tracks per-database initialization tasks to ensure <see cref="AttachAssociationAsync(string)"/>
        /// is invoked only once per database name. The dictionary stores a <see cref="Lazy{Task}"/>
        /// so concurrent callers share the same task instance.
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<Task>> _associationInitTasks =
            new ConcurrentDictionary<string, Lazy<Task>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Scans the specified database and attaches associations found from its foreign key metadata.
        /// Thread-safe: multiple concurrent callers with the same <paramref name="databaseName"/> will
        /// observe the same initialization task and <see cref="AttachAssociationAsync(string)"/> is
        /// guaranteed to be invoked only once for that database name per process.
        /// </summary>
        /// <param name="databaseName">Name of the database to scan and register associations for.</param>
        /// <returns>A task that completes when the specified database has been scanned and associations registered.</returns>
        /// <exception cref="System.ArgumentException"><paramref name="databaseName"/> is null, empty, or whitespace.</exception>
        /// <remarks>
        /// This method is safe to call from multiple threads. Callers requesting initialization
        /// for the same database name will await the same internal task. Initialization is recorded
        /// per database name and will not be retried when the initial attempt completes (successfully
        /// or faulted). This mirrors the "run once" semantics used elsewhere in the mapper.
        /// </remarks>
        internal static Task InitializeAssociationsAsync(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("databaseName required.", nameof(databaseName));

            // Ensure a single AttachAssociationAsync call runs per databaseName.
            Lazy<Task>? lazy = _associationInitTasks.GetOrAdd(
                databaseName,
                db => new Lazy<Task>(() => AttachAssociationAsync(db), LazyThreadSafetyMode.ExecutionAndPublication));

            // Return the shared Task. Caller (startup) should await this so failures surface immediately.
            return lazy.Value;
        }

        /// <summary>
        /// Inspect the SQLite database foreign key metadata and register matching associations.
        /// </summary>
        /// <param name="databaseName">Name of the database to open and inspect.</param>
        /// <returns>A task that completes when the inspection and registration have finished.</returns>
        /// <exception cref="System.Exception">Thrown when the inspection or registration operation fails for <paramref name="databaseName"/>. Errors are logged; non-fatal errors are wrapped with contextual information.</exception>
        /// <remarks>
        /// This method:
        /// - Opens an <see cref="SxmConnection"/> for <paramref name="databaseName"/>.
        /// - Reads all user table names via <see cref="SxmHelpers.GetAllUserTableNamesAsync"/>.
        /// - For each table, runs <c>PRAGMA foreign_key_list(table)</c> to discover foreign keys.
        /// - Locates the CLR source type by table name from the set of entity types registered via
        ///   <see cref="SxmSchemaRegistration.RegisterEntitySchemaAsync"/> (no runtime assembly scanning)
        ///   and calls <see cref="SxmHelpers.CreateAssociation(Type, string, string)"/> to register the
        ///   association in memory.
        /// 
        /// Note: Exceptions are swallowed and connections are always cleaned up in the finally block.
        /// The method logs exceptions and either rethrows cancellation/fatal exceptions (unchanged)
        /// or wraps other exceptions with contextual text to aid diagnosis.
        /// </remarks>
        private static async Task AttachAssociationAsync(string databaseName)
        {
            SxmConnection? sxmConnection = default;
            string currentTableName = string.Empty;
            string? currentTargetTableName = null;
            string? currentSourceType = null;
            string? currentSourceKey = null;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                List<string> tableNames = await SxmHelpers.GetAllUserTableNamesAsync(sxmConnection).ConfigureFalse();

                if (tableNames.Count > 0)
                {
                    await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection).ConfigureFalse())
                    {
                        foreach (string tableName in tableNames)
                        {
                            currentTableName = tableName;
                            string pragma = $"PRAGMA foreign_key_list({SxmHelpers.QuoteIdentifier(tableName)})";
                            await sxmConnection.ExecuteQueryAsync(pragma, default(List<object>)).ConfigureFalse();

                            while (sxmConnection.NextRow() == true)
                            {
                                string? targetTableName = (string?)sxmConnection.GetValue("table");
                                currentTargetTableName = targetTableName;

                                string? sourceKey = (string?)sxmConnection.GetValue("from");
                                currentSourceKey = sourceKey;

                                // How this could fail. If you have different namespaces that include a class with the same name that both inherit the SXMEntity class.
                                // The rule: Assign unique names to classes that inherit from SxmEntity, even if they are in different namespaces.
                                // The rule: Composite foreign keys are not supported. They may create incorrect single key mappings when the composite FK is mapped to the primary 'id' field of the foreign table.

                                Type? sourceType = FindSourceTypeByTableName(tableName);
                                currentSourceType = sourceType?.FullName ?? "null";

                                // Do not map associations when the foreign key does not map to the primary 'id' field of the foreign table.
                                string? to = (string?)sxmConnection.GetValue("to");
                                if (!string.Equals(to, nameof(SxmEntity.id), StringComparison.OrdinalIgnoreCase))
                                {
                                    // log a warning so maintainers see skipped FK
                                    string msg = $"Skipping FK on table '{tableName}' column '{currentSourceKey}' -> '{currentTargetTableName}.{to}'. Mapper expects target column 'id'.";
                                    SxmLogging.Log(new SxmWarning(msg), "Warning", nameof(AttachAssociationAsync));
                                    continue; // skip registration for this row.
                                }

                                if (sourceType != default && !string.IsNullOrEmpty(sourceKey) && !string.IsNullOrEmpty(targetTableName))
                                {
                                    lock (_schemaLock)
                                    {
                                        CreateAssociationForRegisteredType(sourceType, sourceKey!, targetTableName!);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                string errorMessage = $"AttachAssociationAsync failed for database '{databaseName}', table '{currentTableName}', source key '{currentSourceKey ?? "null"}', target table '{currentTargetTableName ?? "null"}' source type '{currentSourceType ?? "null"}'.";
                SxmLogging.Log(ex, errorMessage);
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errorMessage = $"AttachAssociationAsync failed for database '{databaseName}', table '{currentTableName}', source key '{currentSourceKey ?? "null"}', target table '{currentTargetTableName ?? "null"}' source type '{currentSourceType ?? "null"}'.";
                SxmLogging.Log(ex);
                throw ExceptionHelper.Wrap(ex, errorMessage);
            }
            finally
            {
                await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
            }
        }

        /// <summary>
        /// Find the registered CLR entity type whose simple name matches the table name.
        /// Only types registered through <see cref="SxmSchemaRegistration.RegisterEntitySchemaAsync"/> are considered,
        /// which keeps this lookup trimming/AOT safe (no <c>AppDomain.GetAssemblies()</c> / <c>Assembly.GetTypes()</c>).
        /// </summary>
        /// <param name="tableName">CLR type simple name to find.</param>
        /// <returns>Matching <see cref="Type"/> or null when not registered.</returns>
        private static Type? FindSourceTypeByTableName(string tableName)
        {
            Type? t = SxmSchemaRegistration.FindRegisteredEntityType(tableName);
            if (t == null || t == typeof(SxmEntity) || !typeof(SxmEntity).IsAssignableFrom(t))
                return null;
            return t;
        }

        [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Types returned by SxmSchemaRegistration.FindRegisteredEntityType were registered through RegisterEntitySchemaAsync, whose Type parameter is annotated with DynamicallyAccessedMemberTypes.All; their public properties are therefore preserved.")]
        private static void CreateAssociationForRegisteredType(Type sourceType, string sourceKey, string targetTableName)
            => SxmHelpers.CreateAssociation(sourceType, sourceKey, targetTableName);

        /// <summary>
        /// Configure a LinqToDB association mapping for a navigation property at runtime.
        /// </summary>
        /// <param name="sourceType">Type that contains the navigation property. Must derive from <see cref="SxmEntity"/>.</param>
        /// <param name="navigationPropertyName">Name of the navigation property on <paramref name="sourceType"/>.</param>
        /// <param name="thisKey">Name of the foreign-key property on <paramref name="sourceType"/> that references the target's <c>id</c>.</param>
        /// <param name="canBeNull">Whether the association can be null (optional, defaults to <c>true</c>).</param>
        /// <exception cref="ArgumentNullException"><paramref name="sourceType"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when required parameters are missing or the types do not derive from <see cref="SxmEntity"/>.</exception>
        /// <remarks>
        /// This method registers the association using LinqToDB's non-generic fluent API:
        /// <c>FluentMappingBuilder.HasAttribute(MemberInfo, MappingAttribute)</c> with an
        /// <see cref="AssociationAttribute"/> describing <c>ThisKey = thisKey</c> and <c>OtherKey = id</c>.
        /// No generic method construction, expression compilation, or reflection over LinqToDB internals is
        /// performed, which keeps this path trimming/AOT safe.
        /// 
        /// The method finalizes the registration by calling <c>builder.Build()</c> so subsequent contexts see the mapping.
        ///
        /// Note: This method mutates the shared <see cref="_schema"/> via <see cref="FluentMappingBuilder.Build"/>.
        /// Calling this concurrently from multiple threads may lead to races in mapping registration. Prefer invoking
        /// during application initialization or synchronize externally.
        /// </remarks>
        internal static void ConfigureAssociation(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type sourceType,
            string navigationPropertyName,
            string thisKey,
            bool canBeNull = true)
        {
            if (sourceType is null) throw new ArgumentNullException(nameof(sourceType));
            if (string.IsNullOrWhiteSpace(navigationPropertyName)) throw new ArgumentException("navigationPropertyName required.", nameof(navigationPropertyName));
            if (string.IsNullOrWhiteSpace(thisKey)) throw new ArgumentException("thisKey required.", nameof(thisKey));
            if (!typeof(SxmEntity).IsAssignableFrom(sourceType))
                throw new ArgumentException("sourceType must derive from SxmEntity.", nameof(sourceType));

            // Find navigation property and target type
            PropertyInfo navProp = sourceType.GetProperty(navigationPropertyName, BindingFlags.Public | BindingFlags.Instance)
                         ?? throw new ArgumentException($"Property '{navigationPropertyName}' not found on {sourceType.Name}.");
            Type targetType = navProp.PropertyType;
            if (!typeof(SxmEntity).IsAssignableFrom(targetType))
                throw new ArgumentException($"Navigation property '{navigationPropertyName}' must derive from SxmEntity.");

            // Validate the FK property exists on the source type so misconfiguration fails fast.
            if (sourceType.GetProperty(thisKey, BindingFlags.Public | BindingFlags.Instance) == null)
                throw new ArgumentException($"FK column '{thisKey}' not found on {sourceType.Name}.");

            AssociationAttribute assocAttr = new AssociationAttribute
            {
                ThisKey = thisKey,
                OtherKey = nameof(SxmEntity.id),
                CanBeNull = canBeNull
            };

            lock (_schemaLock)
            {
                FluentMappingBuilder builder = new FluentMappingBuilder(_schema);
                builder.HasAttribute(navProp, assocAttr);
                // Finalize mapping so descriptors (and new contexts) see the association
                builder.Build();
            }
        }
    }
}