using LinqToDB.Mapping;
using System.Collections;
using System.Globalization;
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
    /// </remarks>
    public static class SxmAssociationMapper
    {
        // Reuse the MappingSchema built by SxmMapping to avoid duplicating Build()

        /// <summary>
        /// Mapping schema used by all dynamic association registrations.
        /// </summary>
        public static MappingSchema Schema => SxmMapping.Schema;

        /// <summary>
        /// Guard to ensure database scanning / registration runs only once per process.
        /// </summary>
        private static bool _wasMapped = false;

        /// <summary>
        /// Scans all configured databases and attaches associations found from their foreign key
        /// metadata. Safe to call multiple times; registration only runs once.
        /// </summary>
        /// <remarks>
        /// This method enumerates database names using <see cref="SxmDatabaseDescriptor.GetDatabaseNames"/>
        /// and calls <see cref="AttachAssociationAsync(string)"/> for each database.
        /// </remarks>
        internal static async Task InitializeAssociationsAsync()
        {
            if (_wasMapped) return;

            _wasMapped = true;
            foreach (string databaseName in SxmDatabaseDescriptor.GetDatabaseNames())
                await AttachAssociationAsync(databaseName);
        }

        /// <summary>
        /// Inspect the SQLite database foreign key metadata and register matching associations.
        /// </summary>
        /// <param name="databaseName">Name of the database to open and inspect.</param>
        /// <returns>A task that completes when the inspection and registration have finished.</returns>
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
        /// </remarks>
        internal static async Task AttachAssociationAsync(string databaseName)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                List<string> tableNames = await SxmHelpers.GetAllUserTableNamesAsync(sxmConnection);

                if (tableNames.Count > 0)
                {
                    await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(sxmConnection))
                    {
                        foreach (string tableName in tableNames)
                        {
                            await sxmConnection.ExecuteQueryAsync(String.Format("PRAGMA foreign_key_list({0})", tableName), default(List<object>));

                            if (sxmConnection.NextRow() == true)
                            {
                                string? targetTableName = (string?)sxmConnection.GetValue("table");
                                string? sourceKey = (string?)sxmConnection.GetValue("from");

                                // How this could fail. If you have different namespaces that include a class with the same name that both inherit the SXMEntity class.
                                // The rule: assign unique names to classes that inherit from SxmEntity, even if they are in different namespaces.
                                Type baseType = typeof(SxmEntity);
                                Type? sourceType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(x => x.Name == tableName && x.Namespace?.Equals("SQLiteXM", StringComparison.Ordinal) != true && baseType.IsAssignableFrom(x) && x != baseType).FirstOrDefault();

                                if (sourceType != default)
                                {
                                    SxmHelpers.CreateAssociation(sourceType, sourceKey!, targetTableName!);
                                    string? to = (string?)sxmConnection.GetValue("to");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex);
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                SxmLogging.Log(ex);
                throw ExceptionHelper.Wrap(ex, $"AttachAssociationAsync failed for database '{databaseName}'.");
            }
            finally
            {
                sxmConnection?.DestroyConnection();
            }
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

            var builder = new FluentMappingBuilder(Schema);

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
                    builder.Build();
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
                builder.Build();
            }
            else
            {
                // Nothing applied; still finalize builder to keep state consistent
                builder.Build();
            }
        }
    }
}