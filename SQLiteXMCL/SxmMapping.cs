using LinqToDB.Mapping;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM
{
    public static class SxmMapping
    {
        private static readonly Lazy<MappingSchema> _schema = new(Build);
        public static MappingSchema Schema => _schema.Value;
        private static bool wasMapped = false;

        private static MappingSchema Build()
        {
            var ms = new MappingSchema();

            // decimal TEXT
            ms.SetConverter<decimal, string>(d => d.ToString(CultureInfo.InvariantCulture));
            ms.SetConverter<string, decimal>(s => decimal.Parse(s, CultureInfo.InvariantCulture));

            // ulong TEXT
            ms.SetConverter<ulong, string>(u => u.ToString("D20", CultureInfo.InvariantCulture));
            ms.SetConverter<string, ulong>(s => ulong.Parse(s, CultureInfo.InvariantCulture));

            // DateTime TEXT (ISO 8601) DateTime ticks (INTEGER)
            ms.SetConverter<DateTime, string>(d => d.ToString("o", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateTime>(s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            ms.SetConverter<DateTime, long>(d => d.Ticks);
            ms.SetConverter<long, DateTime>(t => new DateTime(t));

            // DateOnly TEXT + numeric (DayNumber)
            ms.SetConverter<DateOnly, string>(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateOnly>(s => DateOnly.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<DateOnly, long>(d => d.DayNumber);
            ms.SetConverter<long, DateOnly>(l => DateOnly.FromDayNumber((int)l)); // in case stored as long

            // TimeOnly TEXT + numeric (Ticks)
            ms.SetConverter<TimeOnly, string>(t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
            ms.SetConverter<string, TimeOnly>(s => TimeOnly.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<TimeOnly, long>(t => t.Ticks);
            ms.SetConverter<long, TimeOnly>(ticks => new TimeOnly(ticks));

            // TimeSpan TEXT + numeric (Ticks)
            ms.SetConverter<TimeSpan, string>(t => t.ToString("c", CultureInfo.InvariantCulture));
            ms.SetConverter<string, TimeSpan>(s => TimeSpan.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<TimeSpan, long>(t => t.Ticks);
            ms.SetConverter<long, TimeSpan>(ticks => new TimeSpan(ticks));

            // DateTimeOffset TEXT + numeric (Unix ms)
            ms.SetConverter<DateTimeOffset, string>(dto => dto.ToString("o", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateTimeOffset>(s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            ms.SetConverter<DateTimeOffset, long>(dto => dto.ToUnixTimeMilliseconds());
            ms.SetConverter<long, DateTimeOffset>(msVal => DateTimeOffset.FromUnixTimeMilliseconds(msVal));

            // Guid TEXT + byte[]
            ms.SetConverter<Guid, string>(g => g.ToString());
            ms.SetConverter<string, Guid>(s => Guid.Parse(s));
            ms.SetConverter<Guid, byte[]>(g => g.ToRfc4122Bytes());
            ms.SetConverter<byte[], Guid>(b => GuidStorageHelpers.FromRfc4122Bytes(b));

            return ms;
        }

        public static void InitializeAssociations()
        {
            if(wasMapped) return;

            wasMapped = true;
            ArrayList databaseNames = DatabaseDescriptor.getDatabaseNames();

            foreach (string databaseName in databaseNames)
                AttachAssociation(databaseName);
        }

        public static void AttachAssociation(string databaseName)
        {
            SxmConnection? sxmConnection = default;

            try
            {
                sxmConnection = new SxmConnection(databaseName);
                List<string> tableNames = SxmHelpers.getAllUserTableNames(sxmConnection);

                if(tableNames.Count > 0)
                {
                    using (SxmUTransaction sxmTransaction = new SxmUTransaction(sxmConnection))
                    {
                        foreach (string tableName in tableNames)
                        {
                            sxmConnection.executeQuery(String.Format("PRAGMA foreign_key_list({0})", tableName), default(List<object>));

                            if (sxmConnection.nextRow() == true)
                            {
                                string? targetTableName = (string?)sxmConnection.getValue("table");
                                string? sourceKey = (string?)sxmConnection.getValue("from");

                                // How this could fail. If you have different namespaces that include a class with the same name that both inherit the SXMEntity class.
                                // The rule: assign unique names to classes that inherit from SxmEntity, even if they are in different namespaces.
                                Type BaseType = typeof(SxmEntity);
                                Type? sourceType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(x => x.Name == tableName && x.Namespace?.Equals("SQLiteXM", StringComparison.Ordinal) != true && BaseType.IsAssignableFrom(x) && x != BaseType).FirstOrDefault();

                                if (sourceType != default)
                                {
                                    SxmHelpers.CreateAssociation(sourceType, sourceKey!, targetTableName!);
                                    string? to = (string?)sxmConnection.getValue("to");
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception) { }
            finally
            {
                sxmConnection?.destroyConnection();
            }
        }

        // Runtime (Type-based) version – registers navigation mapping and finalizes it (builder.Build())
        public static void ConfigureAssociation(
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