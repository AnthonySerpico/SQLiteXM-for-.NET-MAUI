using LinqToDB.Mapping;
using SQLiteXM.Internal.Threading;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Linq.Expressions;
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
    public static class SxmAssociationMapper
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
        /// - Locates the CLR source type by table name (types deriving from <see cref="SxmEntity"/>)
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

                                //Type baseType = typeof(SxmEntity);
                                //Type? sourceType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(x => x.Name == tableName && x.Namespace?.Equals("SQLiteXM", StringComparison.Ordinal) != true && baseType.IsAssignableFrom(x) && x != baseType).FirstOrDefault();
                                Type? sourceType = FindSourceTypeByTableName(tableName);
                                currentSourceType = sourceType?.FullName ?? "null";

                                // Do not map associations when the foreign key does not map to the primary 'id' field of the foreign table.
                                string? to = (string?)sxmConnection.GetValue("to");
                                if (!string.Equals(to, nameof(SxmEntity.id), StringComparison.OrdinalIgnoreCase))
                                {
                                    // log a warning so maintainers see skipped FK
                                    string msg = $"Skipping FK on table '{tableName}' column '{currentSourceKey}' -> '{currentTargetTableName}.{to}'. Mapper expects target column 'id'.";
                                    SxmLogging.Log(new System.Exception(msg), "Warning", nameof(AttachAssociationAsync));
                                    continue; // skip registration for this row.
                                }

                                if (sourceType != default && !string.IsNullOrEmpty(sourceKey) && !string.IsNullOrEmpty(targetTableName))
                                {
                                    lock (_schemaLock)
                                    {
                                        SxmHelpers.CreateAssociation(sourceType, sourceKey!, targetTableName!);
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
        /// Find a loadable CLR type whose simple name matches the table name and derives from <see cref="SxmEntity"/>.
        /// Uses a safe assembly enumeration (handles ReflectionTypeLoadException) and skips assemblies that cannot be inspected.
        /// </summary>
        /// <param name="tableName">CLR type simple name to find.</param>
        /// <returns>Matching <see cref="Type"/> or null when not found.</returns>
        private static Type? FindSourceTypeByTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return null;

            Type baseType = typeof(SxmEntity);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] asmTypes;
                try
                {
                    asmTypes = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException rtlEx)
                {
                    asmTypes = rtlEx.Types?.Where(t => t != null).Cast<Type>().ToArray() ?? Array.Empty<Type>();
                }
                catch
                {
                    // Unable to inspect this assembly — skip it.
                    continue;
                }

                foreach (var t in asmTypes)
                {
                    if (t == null) continue;

                    if (t.Name == tableName
                        && t.Namespace?.Equals("SQLiteXM", StringComparison.Ordinal) != true
                        && baseType.IsAssignableFrom(t)
                        && t != baseType)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

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
        /// This method attempts to register an association using LinqToDB's fluent API:
        /// 1. It first builds an expression for the navigation property: <c>(TSource s) => s.Navigation</c>.
        /// 2. It tries to use the <c>Property(...).HasAttribute(AssociationAttribute)</c> route when available.
        /// 3. If that path is not available it falls back to calling <c>Association(navigation, keyExpression, [canBeNull])</c>
        ///    where <c>(TSource s, TTarget t) => s.thisKey == t.id</c> is the equality expression.
        /// 
        /// The method finalizes the registration by calling <c>builder.Build()</c> so subsequent contexts see the mapping.
        ///
        /// Note: This method uses reflection and mutates the shared <see cref="_schema"/> via <see cref="FluentMappingBuilder.Build"/>.
        /// Calling this concurrently from multiple threads may lead to races in mapping registration. Prefer invoking
        /// during application initialization or synchronize externally.
        /// </remarks>
        internal static void ConfigureAssociation(
            Type sourceType,
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
            var navProp = sourceType.GetProperty(navigationPropertyName, BindingFlags.Public | BindingFlags.Instance)
                         ?? throw new ArgumentException($"Property '{navigationPropertyName}' not found on {sourceType.Name}.");
            var targetType = navProp.PropertyType;
            if (!typeof(SxmEntity).IsAssignableFrom(targetType))
                throw new ArgumentException($"Navigation property '{navigationPropertyName}' must derive from SxmEntity.");

            // Build (TSource s) => s.Navigation
            var sNav = Expression.Parameter(sourceType, "s");
            var navBody = Expression.Property(sNav, navProp);
            var navLambda = Expression.Lambda(typeof(Func<,>).MakeGenericType(sourceType, targetType), navBody, sNav);

            var assocAttr = new AssociationAttribute
            {
                ThisKey = thisKey,
                OtherKey = nameof(SxmEntity.id),
                CanBeNull = canBeNull
            };

            var builder = new FluentMappingBuilder(_schema);

            // -------- Entity<TSource>() (handle overload differences) ----------
            var entityGen = typeof(FluentMappingBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Entity" && m.IsGenericMethodDefinition)
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault()
                ?? throw new InvalidOperationException("FluentMappingBuilder.Entity<T>(...) overload not found.");

            var entityParams = entityGen.GetParameters();
            object?[] entityArgs = entityParams.Length == 0
                ? Array.Empty<object?>()
                : entityParams.Select(p =>
                      p.HasDefaultValue ? p.DefaultValue :
                      p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();

            var entityBuilder = entityGen.MakeGenericMethod(sourceType).Invoke(builder, entityArgs)
                ?? throw new InvalidOperationException("Failed to invoke FluentMappingBuilder.Entity<T>().");

            // Try the Property<TProp>(...) + HasAttribute(...) path first
            var propertyGen = entityBuilder.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Property" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1)
                .FirstOrDefault();

            if (propertyGen != null)
            {
                var propertyMethod = propertyGen.MakeGenericMethod(targetType);
                var propertyBuilder = propertyMethod.Invoke(entityBuilder, new object[] { navLambda })!;

                var hasAttr = propertyBuilder.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "HasAttribute"
                                         && m.GetParameters().Length == 1
                                         && typeof(Attribute).IsAssignableFrom(m.GetParameters()[0].ParameterType));
                if (hasAttr != null)
                {
                    hasAttr.Invoke(propertyBuilder, new object[] { assocAttr });
                    // Finalize mapping so descriptors (and new contexts) see the association
                    lock (_schemaLock) { builder.Build(); }
                    return;
                }
            }

            // Fallback to Association(...) builder if Property/HasAttribute not available
            // Build (TSource s, TTarget t) => s.thisKey == t.id
            var leftProp = sourceType.GetProperty(thisKey, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new ArgumentException($"FK column '{thisKey}' not found on {sourceType.Name}.");
            var idProp = targetType.GetProperty(nameof(SxmEntity.id), BindingFlags.Public | BindingFlags.Instance)
                        ?? throw new ArgumentException($"Primary key 'id' not found on {targetType.Name}.");

            var s = Expression.Parameter(sourceType, "s");
            var t = Expression.Parameter(targetType, "t");
            Expression left = Expression.Property(s, leftProp);
            Expression right = Expression.Property(t, idProp);

            // Coerce FK/PK to a common type if needed (e.g., int -> long)
            if (left.Type != right.Type)
            {
                try
                {
                    if (!right.Type.IsAssignableFrom(left.Type))
                        left = Expression.Convert(left, right.Type);
                }
                catch
                {
                    // Last resort: compare as strings
                    left = Expression.Call(left, nameof(object.ToString), Type.EmptyTypes);
                    right = Expression.Call(right, nameof(object.ToString), Type.EmptyTypes);
                }
            }

            var eqBody = Expression.Equal(left, right);
            var keyLambda = Expression.Lambda(
                typeof(Func<,,>).MakeGenericType(sourceType, targetType, typeof(bool)),
                eqBody, s, t);

            // Find Association<TProp>(..., ..., bool?) overload
            var assocGen = entityBuilder.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "Association" && m.IsGenericMethodDefinition)
                .OrderByDescending(m => m.GetParameters().Length) // prefer overloads with canBeNull
                .FirstOrDefault();

            if (assocGen != null)
            {
                var assocMethod = assocGen.MakeGenericMethod(targetType);
                var assocParams = assocMethod.GetParameters();

                if (assocParams.Length == 2)
                    assocMethod.Invoke(entityBuilder, new object[] { navLambda, keyLambda });
                else if (assocParams.Length == 3 && assocParams[2].ParameterType == typeof(bool))
                    assocMethod.Invoke(entityBuilder, new object[] { navLambda, keyLambda, canBeNull });

                // Finalize mapping
                lock (_schemaLock) { builder.Build(); }
            }
            else
            {
                // Nothing applied; still finalize builder to keep state consistent
                lock (_schemaLock) { builder.Build(); }
            }
        }
    }
}