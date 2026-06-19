using LinqToDB.Mapping;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM;

/// <summary>
/// Handles deterministic schema registration and initialization for entity types.
/// Separates schema creation from entity instantiation, allowing explicit registration at application startup.
/// </summary>
internal static class SxmSchemaRegistration
{
    // Track registered entity types to prevent duplicate initialization
    private static readonly ConcurrentDictionary<Type, bool> _registeredSchemas = new();

    // Prevent multiple concurrent initializations for the same entity type
    private static readonly object _lockObject = new object();

    // Cache mapping CLR Type -> resolved [Table].Database name
    private static readonly ConcurrentDictionary<Type, string?> _tableAttributeNameCache = new();

    // Protect against different CLR types that share the same simple Name
    private static readonly ConcurrentDictionary<string, string?> _entityTypeMap = new(StringComparer.Ordinal);

    // Protect against using the same simple Name across different databases
    private static readonly ConcurrentDictionary<string, string?> _entityDatabaseMap = new(StringComparer.Ordinal);

    // Per-table initialization gate (same as SxmEntity)
    private static readonly ConcurrentDictionary<string, Lazy<Task>> _initTasks = new();

    // Index dictionaries
    private static readonly ConcurrentDictionary<string, ConcurrentBag<IndexProperties>> _uniqueIndexDict = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ConcurrentBag<IndexProperties>> _standardIndexDict = new(StringComparer.Ordinal);

    /// <summary>
    /// Register and initialize schema for a single entity type.
    /// </summary>
    /// <param name="entityType">The entity type derived from SxmEntity.</param>
    /// <param name="databaseName">Optional database name override.</param>
    /// <exception cref="ArgumentNullException">Thrown when entityType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when entityType does not inherit from SxmEntity or is abstract.</exception>
    public static async Task RegisterEntitySchemaAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type entityType, 
        string? databaseName = null)
    {
        if (entityType == null)
            throw new ArgumentNullException(nameof(entityType));

        // RULE 1: Fail-fast if type does not inherit from SxmEntity
        if (!typeof(SxmEntity).IsAssignableFrom(entityType))
        {
            throw new ArgumentException(
                $"Type '{entityType.Name}' must inherit from SxmEntity. " +
                $"Only entity types derived from SxmEntity can be registered for schema initialization.",
                nameof(entityType));
        }

        if (entityType.IsAbstract)
        {
            throw new ArgumentException(
                $"Type '{entityType.Name}' cannot be abstract. " +
                $"Only concrete entity types can be registered for schema initialization.",
                nameof(entityType));
        }

        // Mark as registered - use TryAdd result for thread-safe idempotency
        if (!_registeredSchemas.TryAdd(entityType, true))
            return; // Already registered by another thread

        // Resolve database name
        string? resolvedDbName = databaseName ?? ResolveTableAttributeDatabaseName(entityType);

        // Validate database name
        ValidateDatabaseName(ref resolvedDbName);

        // Initialize schema
        await InitializeSchemaAsync(entityType, resolvedDbName!).ConfigureFalse();
    }

    /// <summary>
    /// Check if an entity type has been registered.
    /// </summary>
    public static bool IsSchemaRegistered(Type entityType)
    {
        return _registeredSchemas.ContainsKey(entityType);
    }

#if DEBUG
    /// <summary>
    /// Resets all static schema registration state for testing.
    /// **WARNING:** This is intended ONLY for testing scenarios.
    /// </summary>
    internal static void ResetForTesting()
    {
        _registeredSchemas.Clear();
        _tableAttributeNameCache.Clear();
        _entityTypeMap.Clear();
        _entityDatabaseMap.Clear();
        _initTasks.Clear();
        _uniqueIndexDict.Clear();
        _standardIndexDict.Clear();
    }
#endif

    /// <summary>
    /// Resolve the database name from [Table(Database = "...")] attribute.
    /// </summary>
    private static string? ResolveTableAttributeDatabaseName(Type entityType)
    {
        if (_tableAttributeNameCache.TryGetValue(entityType, out string? cachedName))
            return cachedName;

        string? resolved = _tableAttributeNameCache.GetOrAdd(entityType, t =>
        {
            TableAttribute? tbl = t.GetCustomAttribute<TableAttribute>(inherit: false);
            string? name = tbl?.Database;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        });

        return resolved;
    }

    /// <summary>
    /// Validate and resolve database name (same logic as SxmEntity.DbNameValidation).
    /// </summary>
    private static void ValidateDatabaseName(ref string? databaseName)
    {
        if (databaseName == null)
        {
            databaseName = SxmDatabaseDescriptor.DefaultDatabase;
            if (databaseName == null)
                throw new InvalidDataException("A default database has not been configured in any of your SQL statements files.");
        }
        else
        {
            if (!SxmDatabaseDescriptor.IsDatabaseDefined(databaseName))
                throw new InvalidDataException($"The database '{databaseName}' has not been configured. Check the spelling matches the database name in your SQL statements file.");
        }
    }

    /// <summary>
    /// Initialize schema for an entity type (mirrors SxmEntity.Initialize logic).
    /// </summary>
    private static async Task InitializeSchemaAsync(Type entityType, string databaseName)
    {
        string tableName = entityType.Name;
        string typeIdentity = entityType.AssemblyQualifiedName ?? entityType.FullName ?? entityType.Name;

        lock (_lockObject)
        {
            // Prevent using the same simple table name across different databases
            if (!_entityDatabaseMap.TryAdd(tableName, databaseName))
            {
                if (_entityDatabaseMap.TryGetValue(tableName, out var existingDb) &&
                    !string.Equals(existingDb, databaseName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CRITICAL: Entity database collision detected. The entity class '{tableName}' is already registered for database '{existingDb}'.\n" +
                        $"Current database attempting registration: '{databaseName}'.\n" +
                        $"CAUSE: The same entity class name is being used across multiple databases.\n" +
                        $"SOLUTION: Use distinct entity class names for each database, or ensure the same entity class is only used with one database.\n" +
                        $"EXAMPLE: Instead of using 'User' for both databases, use 'DatabaseAUser' and 'DatabaseBUser'.");
                }
            }

            // Prevent different CLR types that share the same simple Name
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
            }
        }

        Lazy<Task> lazyInit = _initTasks.GetOrAdd(tableName, _ => new Lazy<Task>(
                () => Task.Run(async () =>
                {
                    Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                    List<string> ddlStatementsList = new List<string>();

                    List<MemberInfoWithAlias> props = GetEntityProperties(entityType);
                    GetColumnNamesAndDataTypes(entityType, props, databaseName);

                    bool newTable;
                    if (!(newTable = await CreateTableAsync(entityType, databaseName, ddlStatementsList).ConfigureFalse()))
                        await AddColumnsAsync(entityType, databaseName, ddlStatementsList).ConfigureFalse();

                    var std = new List<string>();
                    var uniq = new List<string>();
                    await GetIndexTableStatementsAsync(entityType, databaseName, std, uniq).ConfigureFalse();

                    await ProcessIndexStatementsAsync(entityType, databaseName, IndexType.Standard, std, ddlStatementsList).ConfigureFalse();
                    await ProcessIndexStatementsAsync(entityType, databaseName, IndexType.Unique, uniq, ddlStatementsList).ConfigureFalse();
                    await ProcessTriggerAttributesAsync(entityType, databaseName, ddlStatementsList).ConfigureFalse();

                    if (!newTable)
                        await DropColumnsAsync(entityType, databaseName, ddlStatementsList).ConfigureFalse();

                    stopwatch.Stop();

                    string message = $"{ddlStatementsList.Count} DDL statement(s) executed:{Environment.NewLine}" + string.Join(Environment.NewLine, ddlStatementsList.Select((w, i) => $"  [{i + 1}] {w}"));
                    SxmLogging.Log(new SxmInformational(message), $"{(newTable ? "Creating" : "Synchronizing")} schema. Database: '{databaseName}'. Table: '{tableName}'. Duration: {stopwatch.ElapsedMilliseconds}ms", $"InitializeSchemaAsync");
                }),
                LazyThreadSafetyMode.ExecutionAndPublication
            )
        );

        try
        {
            await lazyInit.Value.ConfigureFalse();
        }
        catch
        {
            _initTasks.TryRemove(tableName, out _); // allow retry on failure

            // Best-effort cleanup
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
                        string.Equals(mappedDb, databaseName, StringComparison.Ordinal))
                    {
                        _entityDatabaseMap.TryRemove(tableName, out _);
                    }
                }
            }
            catch
            {
                // Swallow cleanup errors
            }

            throw;
        }
    }

    private static List<MemberInfoWithAlias> GetEntityProperties(Type entityType)
    {
        List<MemberInfoWithAlias> propertyInfoWithAliases = new List<MemberInfoWithAlias>();

        foreach (PropertyInfo piItem in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            propertyInfoWithAliases.Add(new MemberInfoWithAlias(piItem, string.Empty));

        // Validate rename attributes
        ValidateRenameAttributes(entityType, propertyInfoWithAliases);

        return propertyInfoWithAliases;
    }

    /// <summary>
    /// Validates that [Rename] attributes are used correctly.
    /// </summary>
    /// <remarks>
    /// Enforces the following rules:
    /// <list type="number">
    ///   <item><description>The old property name(s) referenced in [Rename] must NOT exist as current properties.</description></item>
    ///   <item><description>Multiple properties cannot claim to rename from the same old name.</description></item>
    ///   <item><description>A property cannot have [Rename] and [NotColumn] simultaneously.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when validation rules are violated.</exception>
    private static void ValidateRenameAttributes(Type entityType, List<MemberInfoWithAlias> propertyInfoWithAliases)
    {
        var allPropertyNames = new HashSet<string>(
            propertyInfoWithAliases.Select(p => p.MemberInfo.Name),
            StringComparer.OrdinalIgnoreCase
        );

        var renameClaimsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // oldName -> newPropertyName

        foreach (MemberInfoWithAlias property in propertyInfoWithAliases)
        {
            RenameAttribute? renameAttr = property.MemberInfo.GetCustomAttribute<RenameAttribute>(inherit: false);
            if (renameAttr == null)
                continue;

            string newPropertyName = property.MemberInfo.Name;

            // Rule 1: Cannot have both [Rename] and [NotColumn]
            if (property.MemberInfo.IsDefined(typeof(NotColumnAttribute), false))
            {
                throw new InvalidOperationException(
                    $"SCHEMA ERROR in entity '{entityType.Name}': Property '{newPropertyName}' cannot have both [Rename] and [NotColumn] attributes.\n" +
                    $"SOLUTION: Remove one of the attributes. If the property should not be mapped, use [NotColumn] only.");
            }

            // Rule 2: Old property name must NOT exist as a current property
            foreach (string oldName in renameAttr.OldNames)
            {
                if (allPropertyNames.Contains(oldName))
                {
                    throw new InvalidOperationException(
                        $"SCHEMA ERROR in entity '{entityType.Name}': Property '{newPropertyName}' has [Rename(\"{oldName}\")] but property '{oldName}' still exists.\n" +
                        $"CAUSE: The old property must be completely removed from the entity class after renaming.\n" +
                        $"SOLUTION: Remove the '{oldName}' property definition from the '{entityType.Name}' class.\n" +
                        $"EXAMPLE:\n" +
                        $"  // Before (INCORRECT):\n" +
                        $"  public string {oldName} {{ get; set; }}\n" +
                        $"  [Rename(\"{oldName}\")]\n" +
                        $"  public string {newPropertyName} {{ get; set; }}\n\n" +
                        $"  // After (CORRECT):\n" +
                        $"  [Rename(\"{oldName}\")]\n" +
                        $"  public string {newPropertyName} {{ get; set; }}");
                }

                // Rule 3: Multiple properties cannot claim the same old name
                if (renameClaimsMap.TryGetValue(oldName, out var existingClaimant))
                {
                    throw new InvalidOperationException(
                        $"SCHEMA ERROR in entity '{entityType.Name}': Multiple properties claim to rename from '{oldName}'.\n" +
                        $"Property '{existingClaimant}' and property '{newPropertyName}' both have [Rename(\"{oldName}\")].\n" +
                        $"SOLUTION: Only one property can rename from a given old name. Review your rename history and fix the duplicate claim.");
                }

                renameClaimsMap[oldName] = newPropertyName;
            }
        }
    }

    private static void GetColumnNamesAndDataTypes(Type entityType, List<MemberInfoWithAlias> propertyInfoWithAliases, string databaseName)
    {
        if (propertyInfoWithAliases == null || propertyInfoWithAliases.Count == 0)
            return;

        string typeName = entityType.Name;
        var columnDict = SxmEntity._columnNameAndTypeDict.GetOrAdd(typeName, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        if (columnDict.Count > 0)
        {
            throw new InvalidOperationException(
                $"INTERNAL ERROR: Column map for type '{typeName}' was already initialized. " +
                $"This indicates a critical failure in the entity initialization synchronization logic.");
        }

        TableAttribute? tbl = entityType.GetCustomAttribute<TableAttribute>(inherit: false);
        bool columnIsRequired = tbl?.IsColumnAttributeRequired ?? false;

        List<ForeignKeyFields>? foreignKeyAttributeList = null;

        foreach (MemberInfoWithAlias propertyInfoWithAlias in propertyInfoWithAliases)
        {
            MemberInfo memberInfo = propertyInfoWithAlias.MemberInfo;
            string memberInfoName = memberInfo.Name;

            if (IsIgnored(memberInfoName))
                continue;

            if (memberInfo is not PropertyInfo propertyInfo)
                continue;

            if (memberInfo.IsDefined(typeof(NotColumnAttribute), false))
                continue;

            ColumnAttribute? colAttr = memberInfo.GetCustomAttribute<ColumnAttribute>(inherit: false);
            if (columnIsRequired && colAttr == null)
                continue;

            RequiredNotNullAttribute? requiredNotNull = memberInfo.GetCustomAttribute<RequiredNotNullAttribute>(inherit: false);
            bool hasCreateIndex = memberInfo.IsDefined(typeof(IndexAttribute), inherit: false);
            bool hasCreateUniqueIndex = memberInfo.IsDefined(typeof(UniqueIndexAttribute), inherit: false);
            ForeignKeyAttribute? isForeignKey = memberInfo.GetCustomAttribute<ForeignKeyAttribute>(inherit: false);

            string notNull = string.Empty;
            if (requiredNotNull is not null)
            {
                notNull = requiredNotNull.DefaultValue is not null 
                    ? $" not null default {SxmHelpers.FormatSqlLiteral(requiredNotNull.DefaultValue)}" 
                    : " not null";
            }

            string columnName = string.IsNullOrEmpty(propertyInfoWithAlias.Alias) ? memberInfoName : propertyInfoWithAlias.Alias;

            if (hasCreateIndex)
            {
                var bag = _standardIndexDict.GetOrAdd(typeName, _ => new ConcurrentBag<IndexProperties>());
                bag.Add(new IndexProperties(columnName, typeName));
            }

            if (hasCreateUniqueIndex)
            {
                var bag = _uniqueIndexDict.GetOrAdd(typeName, _ => new ConcurrentBag<IndexProperties>());
                bag.Add(new IndexProperties(columnName, typeName));
            }

            if (isForeignKey is not null)
            {
                foreignKeyAttributeList ??= new List<ForeignKeyFields>();
                foreignKeyAttributeList.Add(new ForeignKeyFields
                {
                    fieldName = columnName,
                    ForeignTable = isForeignKey.ForeignTable,
                    OnDelete = isForeignKey.OnDelete
                });

                SxmHelpers.CreateAssociation(entityType, columnName, isForeignKey.ForeignTable);
            }

            Type clrType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

            string? overrideType = colAttr?.DataType switch
            {
                DataType.Text or DataType.NChar or DataType.NVarChar or DataType.Char or DataType.VarChar => "TEXT",
                DataType.Int16 or DataType.Int32 or DataType.UInt16 or DataType.UInt32 or DataType.Int64 or DataType.Long => "INTEGER",
                DataType.Boolean or DataType.DateTime or DataType.Date or DataType.Time => "INTEGER",
                DataType.Decimal or DataType.UInt64 => "TEXT",
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
                if (!columnDict.TryAdd(columnName, columnType + notNull))
                {
                    throw new InvalidOperationException(
                        $"Duplicate mapped column name '{columnName}' on type '{typeName}'. " +
                        $"Members '{memberInfoName}' (alias '{propertyInfoWithAlias.Alias ?? ""}') " +
                        $"and another member resolved to the same mapped name.");
                }
            }
        }

        // Store foreign keys for table creation (will be retrieved in CreateTableAsync)
        if (foreignKeyAttributeList != null)
        {
            // Store in a static dictionary for retrieval during table creation
            _foreignKeyCache.TryAdd(typeName, foreignKeyAttributeList);
        }
    }

    // Cache for foreign keys (needed for table creation)
    private static readonly ConcurrentDictionary<string, List<ForeignKeyFields>> _foreignKeyCache = new();

    private static bool IsTimeType(Type clrType)
    {
        return clrType == typeof(DateTimeOffset) ||
               clrType == typeof(TimeSpan) ||
               clrType == typeof(DateOnly) ||
               clrType == typeof(TimeOnly) ||
               clrType == typeof(DateTime);
    }

    private static string? ClrTypeToColumnType(Type clrType)
    {
        return clrType == typeof(decimal) ? "TEXT" :
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
               }

               /// <summary>
               /// Converts a ForeignKeyDeleteAction enum value to its SQL representation.
               /// </summary>
               /// <param name="action">The foreign key action to convert.</param>
               /// <returns>The SQL clause for the action (e.g., " ON DELETE CASCADE"), or empty string if None.</returns>
               private static string GetForeignKeyActionSql(ForeignKeyDeleteAction action)
               {
                   return action switch
                   {
                       ForeignKeyDeleteAction.Cascade => " ON DELETE CASCADE",
                       ForeignKeyDeleteAction.SetNull => " ON DELETE SET NULL",
                       ForeignKeyDeleteAction.SetDefault => " ON DELETE SET DEFAULT",
                       ForeignKeyDeleteAction.Restrict => " ON DELETE RESTRICT",
                       ForeignKeyDeleteAction.NoAction => " ON DELETE NO ACTION",
                       ForeignKeyDeleteAction.None => string.Empty,
                       _ => string.Empty
                   };
               }

               private static async Task<bool> CreateTableAsync(Type entityType, string databaseName, List<string> ddlStatementsList)
    {
        bool tableCreated = false;
        string tableName = entityType.Name;
        string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

        SxmConnection? sxmConnection = null;
        bool tableExists = false;

        try
        {
            sxmConnection = new SxmConnection(databaseName);
            tableExists = await SxmDatabase.DoesTableExistAsync(tableName, sxmConnection).ConfigureFalse();
        }
        finally
        {
            await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
        }

        if (!tableExists)
        {
            tableCreated = true;
            var sb = new System.Text.StringBuilder();
            sb.Append($"CREATE TABLE {quotedTable} (");
            sb.Append($"{SxmHelpers.QuoteIdentifier("id")} INTEGER PRIMARY KEY AUTOINCREMENT");

            foreach (KeyValuePair<string, string> kvp in SxmEntity._columnNameAndTypeDict[tableName])
            {
                sb.Append(", ");
                sb.Append($"{SxmHelpers.QuoteIdentifier(kvp.Key)} {kvp.Value}");
            }

            if (_foreignKeyCache.TryGetValue(tableName, out var foreignKeyList))
            {
                foreach (ForeignKeyFields attribute in foreignKeyList)
                {
                    string onDeleteClause = GetForeignKeyActionSql(attribute.OnDelete);
                    sb.Append($", FOREIGN KEY({SxmHelpers.QuoteIdentifier(attribute.fieldName!)}) REFERENCES {SxmHelpers.QuoteIdentifier(attribute.ForeignTable!)}({SxmHelpers.QuoteIdentifier("id")}){onDeleteClause}");
                }
                _foreignKeyCache.TryRemove(tableName, out _);
            }

            sb.Append(")");

            ddlStatementsList.Add(sb.ToString());
            SxmSqlStatements.AddTableDefinition(string.Format("{0}.{1}", databaseName, tableName), sb.ToString());
            await SxmDatabase.CreateTableAsync(databaseName, tableName).ConfigureFalse();
            SxmSqlStatements.RemoveTableDefinitions();
        }

        return tableCreated;
    }

    private static async Task AddColumnsAsync(Type entityType, string databaseName, List<string> ddlStatementsList)
    {
        string tableName = entityType.Name;
        string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

        Dictionary<string, string> dbTableColumnNameAndType = await SxmDatabase.GetTableColumnNamesAsync(databaseName, tableName).ConfigureFalse();

        // Step 1: Process column renames first
        await ProcessColumnRenamesAsync(entityType, databaseName, dbTableColumnNameAndType, ddlStatementsList).ConfigureFalse();

        // Step 2: Refresh database column list after renames
        dbTableColumnNameAndType = await SxmDatabase.GetTableColumnNamesAsync(databaseName, tableName).ConfigureFalse();

        // Step 3: Add any new columns that don't exist yet
        foreach (KeyValuePair<string, string> kvp in SxmEntity._columnNameAndTypeDict[tableName])
        {
            if (!dbTableColumnNameAndType.ContainsKey(kvp.Key))
            {
                string alterDefinition = $"ALTER TABLE {quotedTable} ADD COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)} {kvp.Value}";

                await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)).ConfigureFalse())
                {
                    ddlStatementsList.Add(alterDefinition);
                    await sxmTransaction.ExecuteAlterTableAsync(alterDefinition).ConfigureFalse();
                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }

                int offset = 0;
                string? value;

                if ((offset = kvp.Value.IndexOf(' ')) != -1)
                    value = kvp.Value.Substring(0, offset);
                else
                    value = kvp.Value;

                SxmDatabase.AddColumnNameType(tableName, kvp.Key, value);
            }
        }
    }

    /// <summary>
    /// Processes column renames for all properties with [Rename] attributes.
    /// </summary>
    /// <remarks>
    /// <para><strong>Migration Strategy:</strong></para>
    /// <list type="bullet">
    ///   <item><description>For each property with [Rename], search for old column names in reverse order (newest to oldest).</description></item>
    ///   <item><description>If any old column exists, rename it to the current property name (data preserved).</description></item>
    ///   <item><description>If no old column exists, do nothing (new column will be created by AddColumnsAsync).</description></item>
    /// </list>
    /// 
    /// <para><strong>Edge Cases Handled:</strong></para>
    /// <list type="bullet">
    ///   <item><description><strong>Fresh Install:</strong> No old columns exist → no rename, new column created.</description></item>
    ///   <item><description><strong>Skipped Versions:</strong> User upgrades from V1 directly to V3 → oldest historical name is found and renamed.</description></item>
    ///   <item><description><strong>Sequential Upgrade:</strong> User upgrades V1 → V2 → V3 → each rename happens in sequence.</description></item>
    ///   <item><description><strong>Partial History Missing:</strong> Only some historical names exist → first match is renamed.</description></item>
    /// </list>
    /// </remarks>
    private static async Task ProcessColumnRenamesAsync(Type entityType, string databaseName, Dictionary<string, string> dbTableColumnNameAndType, List<string> ddlStatementsList)
    {
        string tableName = entityType.Name;
        string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

        PropertyInfo[] properties = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var renameAttr = prop.GetCustomAttribute<RenameAttribute>(inherit: false);
            if (renameAttr == null)
                continue;

            // Skip if marked as NotColumn (already validated, but double-check for safety)
            if (prop.IsDefined(typeof(NotColumnAttribute), false))
                continue;

            string newColumnName = prop.Name;

            // Check if the new column name already exists (no rename needed)
            if (dbTableColumnNameAndType.ContainsKey(newColumnName))
                continue;

            // Search for old column names in reverse order (newest to oldest)
            // This handles skipped-version upgrades: if "Title" → "Name" → "ProductName",
            // and the database has "Name", we rename "Name" → "ProductName" directly.
            string? foundOldName = null;
            for (int i = renameAttr.OldNames.Length - 1; i >= 0; i--)
            {
                string oldName = renameAttr.OldNames[i];
                if (dbTableColumnNameAndType.ContainsKey(oldName))
                {
                    foundOldName = oldName;
                    break;
                }
            }

            // If no old column exists, do nothing (fresh install or column already renamed)
            if (foundOldName == null)
                continue;

            // Rename the old column to the new column name
            string alterDefinition = $"ALTER TABLE {quotedTable} RENAME COLUMN {SxmHelpers.QuoteIdentifier(foundOldName)} TO {SxmHelpers.QuoteIdentifier(newColumnName)}";

            await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)).ConfigureFalse())
            {
                ddlStatementsList.Add(alterDefinition);
                await sxmTransaction.ExecuteAlterTableAsync(alterDefinition).ConfigureFalse();
                await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
            }

            // Update internal column tracking: remove old name, add new name
            string? columnType = dbTableColumnNameAndType[foundOldName];
            SxmDatabase.RemoveColumnNameType(tableName, foundOldName);

            // Extract base type without constraints for internal tracking
            int offset = columnType.IndexOf(' ');
            string baseType = offset != -1 ? columnType.Substring(0, offset) : columnType;
            SxmDatabase.AddColumnNameType(tableName, newColumnName, baseType);
        }
    }

    private static async Task DropColumnsAsync(Type entityType, string databaseName, List<string> ddlStatementsList)
    {
        string tableName = entityType.Name;
        string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

        Dictionary<string, string> dbTableColumnNameAndType = await SxmDatabase.GetTableColumnNamesAsync(databaseName, tableName).ConfigureFalse();

        foreach (KeyValuePair<string, string> kvp in dbTableColumnNameAndType)
        {
            if (!SxmEntity._columnNameAndTypeDict[tableName].ContainsKey(kvp.Key) && !IsIgnored(kvp.Key))
            {
                string alterDefinition = $"ALTER TABLE {quotedTable} DROP COLUMN {SxmHelpers.QuoteIdentifier(kvp.Key)}";
                await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)).ConfigureFalse())
                {
                    ddlStatementsList.Add(alterDefinition);
                    await sxmTransaction1.ExecuteAlterTableAsync(alterDefinition).ConfigureFalse();
                    await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
                }

                SxmDatabase.RemoveColumnNameType(tableName, kvp.Key);
            }
        }
    }

    private static async Task GetIndexTableStatementsAsync(Type entityType, string databaseName, List<string> existingStandardIndexes, List<string> existingUniqueIndexes)
    {
        string tableName = entityType.Name;
        string pragma = $"PRAGMA index_list({SxmHelpers.QuoteIdentifier(tableName)})";

        await using (SxmUTransaction sxmTransaction = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)).ConfigureFalse())
        {
            await sxmTransaction.Connection!.ExecuteQueryAsync(pragma, null).ConfigureFalse();

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

    private static async Task ProcessIndexStatementsAsync(Type entityType, string databaseName, IndexType indexType, List<string> existingIndexes, List<string> ddlStatementsList)
    {
        List<string> indexSqlStatements = new List<string>();

        string index = "INDEX";
        string tableName = entityType.Name;
        string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

        IIndexProperties[]? firstArray;
        IIndexProperties[]? secondArray;

        if (indexType == IndexType.Standard)
        {
            firstArray = (IndexAttribute[])entityType.GetCustomAttributes(typeof(IndexAttribute), true);
            secondArray = _standardIndexDict.TryGetValue(tableName, out var stdBag) ? stdBag.ToArray() : Array.Empty<IIndexProperties>();
        }
        else if (indexType == IndexType.Unique)
        {
            firstArray = (UniqueIndexAttribute[])entityType.GetCustomAttributes(typeof(UniqueIndexAttribute), true);
            secondArray = _uniqueIndexDict.TryGetValue(tableName, out var uniqBag) ? uniqBag.ToArray() : Array.Empty<IIndexProperties>();
            index = "UNIQUE INDEX";
        }
        else
        {
            return;
        }

        firstArray ??= Array.Empty<IIndexProperties>();
        secondArray ??= Array.Empty<IIndexProperties>();

        List<IIndexProperties> customAttributes = new List<IIndexProperties>(firstArray.Length + secondArray.Length);
        customAttributes.AddRange(firstArray);
        customAttributes.AddRange(secondArray);

        AssignIndexNames(customAttributes, tableName);

        foreach (var myAttribute in customAttributes)
        {
            if (!existingIndexes.Contains(myAttribute.IndexName))
            {
                string indexFields = string.Join(", ", myAttribute.IndexFields.Select(f => SxmHelpers.QuoteIdentifier(f)));
                string createIndexSql = $"CREATE {index} {SxmHelpers.QuoteIdentifier(myAttribute.IndexName)} ON {quotedTable} ({indexFields})";
                indexSqlStatements.Add(createIndexSql);
            }
        }

        foreach (string indexName in existingIndexes)
        {
            bool found = false;

            foreach (IIndexProperties customAttribute in customAttributes)
            {
                if (customAttribute.IndexName.Equals(indexName))
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
            await using (SxmUTransaction sxmTransaction1 = await SxmUTransaction.CreateAsync(new SxmConnection(databaseName)).ConfigureFalse())
            {
                foreach (string indexStatement in indexSqlStatements)
                {
                    ddlStatementsList.Add(indexStatement);
                    await sxmTransaction1.ExecuteIndexAsync(indexStatement).ConfigureFalse();
                }

                await sxmTransaction1.CommitTransactionAsync().ConfigureFalse();
            }
        }

        if (indexType == IndexType.Standard)
            _standardIndexDict.TryRemove(tableName, out _);
        else if (indexType == IndexType.Unique)
            _uniqueIndexDict.TryRemove(tableName, out _);
    }

    private static void AssignIndexNames(List<IIndexProperties> indexArray, string tableName)
    {
        foreach (IIndexProperties iiV in indexArray)
        {
            iiV.IndexName = "IDX_" + tableName;

            for (int i = 0; i < iiV.IndexFields.Length; i++)
            {
                iiV.IndexName += "_" + iiV.IndexFields[i];
            }
        }
    }

    private static async Task ProcessTriggerAttributesAsync(Type entityType, string databaseName, List<string> ddlStatementsList)
    {
        string tableName = entityType.Name;

        // Get or create trigger list for this database
        if (!SxmSqlStatements.TriggerStatements.TryGetValue(databaseName, out List<TriggerDefinition>? triggerStatementsList))
        {
            triggerStatementsList = new List<TriggerDefinition>();
            SxmSqlStatements.TriggerStatements[databaseName] = triggerStatementsList;
        }

        TriggerAttribute[] customAttributes = (TriggerAttribute[])entityType.GetCustomAttributes(typeof(TriggerAttribute), true);
        if (customAttributes.Length > 0)
        {
            foreach (TriggerAttribute myAttribute in customAttributes)
            {
                if (!string.IsNullOrWhiteSpace(myAttribute.TriggerSql))
                {
                    triggerStatementsList.Add(new TriggerDefinition(tableName, myAttribute.TriggerSql));
                }
            }
        }

        if (triggerStatementsList.Count > 0)
        {
            SxmConnection sxmConnection = new SxmConnection(databaseName, shared: false);
            try
            {
                await SxmDatabase.AddTriggersAsync(sxmConnection, databaseName, tableName, ddlStatementsList).ConfigureFalse();
            }
            finally
            {
                await (sxmConnection?.DestroyConnectionAsync() ?? Task.CompletedTask).ConfigureFalse();
            }
        }
    }

    private static bool IsIgnored(string name) => 
        string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "synchId", StringComparison.OrdinalIgnoreCase);
}
