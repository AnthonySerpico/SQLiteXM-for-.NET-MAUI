using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
//using static CoreFoundation.DispatchSource;
using static SQLiteXM.SxmDefines;
using static SxmQueryProcessor;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SQLiteXM
{
    /// <summary>
    /// Helper utilities for SQLiteXM: mapping between database rows and user entities,
    /// runtime association wiring, and SQL statement helpers.
    /// </summary>
    internal class SxmHelpers
    {
        /// <summary>
        /// Tracks runtime-registered association keys to avoid duplicate registrations.
        /// Key format: "{SourceType.FullName}.{NavigationPropertyName}".
        /// </summary>
        private static readonly ConcurrentDictionary<string, byte> _registeredAssociations = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        /// <summary>
        /// Thread-safe cache storing property metadata for each object type.
        /// 
        /// Maps a <see cref="Type"/> to a read-only dictionary of property names to their corresponding <see cref="PropertyInfo"/> objects.
        /// This cache is used to reduce reflection overhead when materializing objects from database records.
        /// </summary>
        /// <remarks>
        /// - Only public instance properties with setters (<c>CanWrite</c>) are included in the cached dictionaries.
        /// - The dictionaries are read-only (<see cref="IReadOnlyDictionary{String, PropertyInfo}"/>) to prevent accidental modification of cached metadata.
        /// - Thread-safe: multiple threads can retrieve or add property dictionaries without locking, thanks to <see cref="ConcurrentDictionary{TKey, TValue}"/>.
        /// - Property lookups are case-sensitive and use ordinal comparison to match database column keys reliably.
        /// - Improves performance by ensuring reflection is performed at most once per type.
        /// </remarks>
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> _typePropertyCache = new ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>>();

        private SxmHelpers() { }

        /// <summary>
        /// Quote a SQL identifier per SQLite/SQL standard: double embedded double-quotes and wrap in double-quotes.
        /// Throws <see cref="ArgumentException"/> for null/whitespace input.
        /// </summary>
        /// <param name="name">Identifier to quote (table, column, index name, etc.).</param>
        /// <returns>Quoted identifier safe for SQL injection into SQL text.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        internal static string QuoteIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Identifier cannot be null or whitespace.", nameof(name));

            // Per SQL standard / SQLite: double internal double-quotes and wrap in double-quotes.
            return $"\"{name.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// Returns the list of user table names discovered in the internal descriptor table.
        /// </summary>
        /// <param name="sxmConnection">Connection to query; may be null.</param>
        /// <returns>List of table names (may be empty).</returns>
        internal static async Task<List<string>> GetAllUserTableNamesAsync(SxmConnection? sxmConnection)
        {
            List<string> tableNames = new List<string>();

            if (sxmConnection is not null)
            {
                await sxmConnection.ExecuteQueryAsync("SELECT tableName FROM _systemCloudSynchDescriptor", null as List<object>).ConfigureFalse();

                if (sxmConnection.HasRows())
                {
                    while (sxmConnection.NextRow())
                    {
                        if (sxmConnection.GetValue("tableName") is string name && name.Length > 0)
                            tableNames.Add(name);
                    }
                }
            }

            return tableNames;
        }

        /// <summary>
        /// Format a CLR value into a SQL literal suitable for use when the value
        /// originates from an attribute argument (compile-time constant).
        /// </summary>
        /// <remarks>
        /// This routine deliberately only supports the CLR types that C# allows
        /// as attribute arguments:
        /// - bool, char, string
        /// - sbyte, byte, short, ushort, int, uint, long, ulong, float, double
        /// - single-dimensional arrays of the above
        ///
        /// If an unsupported type is supplied the method throws <see cref="ArgumentException"/>.
        /// Use this when you need to convert an attribute-provided default value into a SQL literal.
        /// </remarks>
        /// <param name="value">Attribute-origin value (may be null).</param>
        /// <returns>SQL literal representing <paramref name="value"/>.</returns>
        /// <exception cref="ArgumentException">When <paramref name="value"/> is not an allowed attribute argument type.</exception>
        internal static string FormatSqlLiteral(object? value)
        {
            if (value is null)
                return "NULL";

            // Strings: single-quote escaped
            if (value is string s)
                return $"'{s.Replace("'", "''")}'";

            // Char: quoted and escaped
            if (value is char ch)
                return $"'{ch.ToString().Replace("'", "''")}'";

            // Boolean: sqlite integer form
            if (value is bool b)
                return b ? "1" : "0";

            // Enums: convert to underlying integral value
            if (value is Enum e)
            {
                long v = Convert.ToInt64(e, CultureInfo.InvariantCulture);
                return v.ToString(CultureInfo.InvariantCulture);
            }

            // Integral and floating types
            switch (value)
            {
                case sbyte sb:
                    return sb.ToString(CultureInfo.InvariantCulture);
                case byte bt:
                    return bt.ToString(CultureInfo.InvariantCulture);
                case short sh:
                    return sh.ToString(CultureInfo.InvariantCulture);
                case ushort ush:
                    return ush.ToString(CultureInfo.InvariantCulture);
                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);
                case uint ui:
                    return ui.ToString(CultureInfo.InvariantCulture);
                case long l:
                    return l.ToString(CultureInfo.InvariantCulture);
                case ulong ul:
                    return ul.ToString(CultureInfo.InvariantCulture);
                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f))
                        return $"'{f.ToString(CultureInfo.InvariantCulture)}'";
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        return $"'{d.ToString(CultureInfo.InvariantCulture)}'";
                    return d.ToString("R", CultureInfo.InvariantCulture);
            }

            // Arrays: single-dimensional arrays of allowed element types
            if (value is Array arr)
            {
                var elems = new List<string>(arr.Length);
                foreach (var item in arr)
                {
                    if (item is null)
                    {
                        elems.Add("NULL");
                        continue;
                    }

                    // recursively format element; will throw if element type is not allowed
                    elems.Add(FormatSqlLiteral(item));
                }

                return "(" + string.Join(", ", elems) + ")";
            }

            throw new ArgumentException($"Type '{value.GetType()}' is not a supported attribute argument type.");
        }

        /// <summary>
        /// Determines whether the provided file path refers to a JSON or XML SQL statements file.
        /// </summary>
        /// <param name="filePath">The file path or name to inspect.</param>
        /// <returns>Returns <see cref="SqlStatementsFileType.Json"/>, <see cref="SqlStatementsFileType.Xml"/>, or <see cref="SqlStatementsFileType.Unknown"/>.</returns>
        internal static SqlStatementsFileType GetSqlStatementsFileType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return SqlStatementsFileType.Unknown;
            }

            string? ext = System.IO.Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext))
            {
                return SqlStatementsFileType.Unknown;
            }

            ext = ext.TrimStart('.').ToLowerInvariant();
            return ext switch
            {
                "json" or "jsn" => SqlStatementsFileType.Json,
                "xml" => SqlStatementsFileType.Xml,
                _ => SqlStatementsFileType.Unknown
            };
        }

        /// <summary>
        /// Attempts to register a runtime association (navigation property) for a foreign key.
        /// Conditions:
        /// 1. Navigation property CLR type name must match <paramref name="targetTableName"/>.
        /// 2. Navigation property must be excluded from column mapping (NotColumnAttribute).
        /// 3. Avoid duplicate registration for the same SourceType.PropertyName.
        /// </summary>
        /// <param name="sourceType">Type that contains the foreign key/navigation property.</param>
        /// <param name="sourceKey">Name of the FK column on the source type.</param>
        /// <param name="targetTableName">Name of the target CLR type (table) to match against.</param>
        internal static void CreateAssociation(Type sourceType, string sourceKey, string targetTableName)
        {

            // Attempt to wire an association if a navigation property exists.
            // Conditions:
            // 1. Navigation property must have PropertyType.Name == fk.ForeignTable
            // 2. It must be excluded from column mapping (NotColumn) so schema builder ignores it.
            // 3. Avoid duplicate registration per (SourceType.PropertyName)
            try
            {
                // Find a single navigation property whose CLR type name matches the foreign table name.
                var navProp = sourceType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.PropertyType.Name.Equals(targetTableName, StringComparison.Ordinal) && p.IsDefined(typeof(LinqToDB.Mapping.NotColumnAttribute), false));

                if (navProp != null &&
                    typeof(SxmEntity).IsAssignableFrom(navProp.PropertyType))
                {
                    string assocKey = $"{sourceType.FullName}.{navProp.Name}";
                    if (_registeredAssociations.TryAdd(assocKey, 0))
                    {
                        // Register runtime association: Source.FK -> Target.id
                        SxmAssociationMapper.ConfigureAssociation(
                            sourceType: sourceType,
                            navigationPropertyName: navProp.Name,
                            thisKey: sourceKey,
                            canBeNull: true);
                    }
                }
                else
                {
                    // OPTIONAL: You can log or ignore if no navigation property is found.
                    // This means only the physical FK will exist; navigation-based LINQ won't.
                }
            }
            catch
            {
                // Swallow or log; association registration failure should not break table creation.
            }

        }

        /// <summary>
        /// Returns the SQL keyword string for a given <see cref="SqlStatementType"/>.
        /// </summary>
        /// <param name="statementType">Statement type to convert.</param>
        /// <returns>Uppercase SQL keyword (e.g., "SELECT", "INSERT").</returns>
        /// <exception cref="ArgumentException">Thrown when the statement type is unknown.</exception>
        internal static string GetDatabaseStatementTypeName(SqlStatementType statementType)
        {

            if (statementType == SqlStatementType.Select || statementType == SqlStatementType.SelectDirect)
                return "SELECT";
            if (statementType == SqlStatementType.Insert || statementType == SqlStatementType.InsertDirect)
                return "INSERT";
            if (statementType == SqlStatementType.Delete || statementType == SqlStatementType.DeleteDirect)
                return "DELETE";
            if (statementType == SqlStatementType.Update || statementType == SqlStatementType.UpdateDirect)
                return "UPDATE";

            throw new ArgumentException($"The sql statement type could not be found.{Environment.NewLine}Statement type: {statementType.ToString()}");
        }

        /// <summary>
        /// Resolves a SQL statement name (or inline SQL) to a <see cref="SqlStatementType"/>.
        /// </summary>
        /// <param name="sqlOrStatementName">Named statement key or an inline SQL string (e.g., "SELECT ...").</param>
        /// <returns>Corresponding <see cref="SqlStatementDetails"/>.</returns>
        /// <exception cref="ArgumentException">If <paramref name="sqlOrStatementName"/> is null/empty or cannot be resolved.</exception>
        internal static SqlStatementDetails GetDatabaseStatementTypeFromSql(string? sqlOrStatementName, string? databaseName)
        {
            // Not a SQL statement in the SQL statements file? Perhaps this is a Direct SQL statement embedded in the code.
            string targetTableName = string.Empty;
            SqlStatementDetails sqlStatementDetails = SxmQueryProcessor.AnalyzeUserQuery(sqlOrStatementName!);

            if (sqlStatementDetails.SqlStatementType == SqlStatementType.Unknown)
                throw new ArgumentException(string.Format("The sql statement '{0}' could not be found or identified.", sqlOrStatementName!.Length > 30 ? (sqlOrStatementName.Substring(0, 29) + "...") : sqlOrStatementName));

            return sqlStatementDetails;
        }

        internal static SqlStatementType GetDatabaseStatementTypeFromName(string? sqlOrStatementName)
        {
            if (string.IsNullOrEmpty(sqlOrStatementName))
                throw new ArgumentException("A sql statement name cannot be null or empty.");

            if (SxmSqlStatements.SelectStatements.ContainsKey(sqlOrStatementName))
                return SqlStatementType.Select;

            if (SxmSqlStatements.UpdateStatements.ContainsKey(sqlOrStatementName))
                return SqlStatementType.Update;

            if (SxmSqlStatements.DeleteStatements.ContainsKey(sqlOrStatementName))
                return SqlStatementType.Delete;

            if (SxmSqlStatements.InsertStatements.ContainsKey(sqlOrStatementName))
                return SqlStatementType.Insert;

            return SqlStatementType.Unknown;
        }


        /// <summary>
        /// Creates a list of user objects of type <typeparamref name="TResult"/> from database row dictionaries.
        /// </summary>
        /// <typeparam name="TResult">User entity type with a public parameterless constructor.</typeparam>
        /// <param name="databaseRowsList">List of dictionary rows where keys are column/property names.</param>
        /// <returns>List of populated user objects.</returns>
        internal static List<TResult> PopulateUserRecord<TResult>(List<Dictionary<string, object?>> databaseRowsList) where TResult : class, new()
        {
            List<TResult> userObjectList = new List<TResult>();

            foreach (Dictionary<string, object?> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                TResult userObject = new TResult();
                LoadDbValues(databaseRecord, userObject);
                userObjectList.Add(userObject);
            }

            return userObjectList;
        }

        /// <summary>
        /// Populates the writable properties of <paramref name="userObject"/> from the provided database record dictionary.
        /// 
        /// The method performs the following for each key/value pair:
        /// 1. Matches the dictionary key to a public writable property on the target object (case-sensitive, ordinal comparison).
        /// 2. Handles <c>null</c> or <c>DBNull.Value</c> by assigning <c>default</c> to the property (preserving original behavior).
        /// 3. Unwraps nullable types to their underlying type before conversion.
        /// 4. Converts database values to the target property type using strict conversion methods that enforce range and type safety.
        /// 5. Logs detailed diagnostics for failed conversions, including column name, source type, and target property type.
        /// 
        /// Property metadata is cached per type using a thread-safe <see cref="ConcurrentDictionary{TKey, TValue}"/> to reduce reflection overhead.
        /// This method is safe for multi-threaded usage and preserves all original exception behavior and logging.
        /// </summary>
        /// <param name="databaseRecord">Dictionary mapping column names to database values (may contain <c>null</c> or <c>DBNull.Value</c>).</param>
        /// <param name="userObject">The destination object whose properties will be populated.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="databaseRecord"/> or <paramref name="userObject"/> is <c>null</c>.</exception>
        /// <exception cref="Exception">
        /// Any exception thrown by the underlying strict conversion methods, wrapped or logged as appropriate.
        /// Fatal or non-wrappable exceptions are re-thrown without modification.
        /// </exception>
        /// <remarks>
        /// - The method is fully behavior-preserving with respect to the original implementation.
        /// - Nullables, numeric ranges, dates, times, GUIDs, and boolean conversions are handled exactly as before.
        /// - Thread-safe caching ensures performance improvements without altering correctness.
        /// </remarks>
        internal static void LoadDbValues(Dictionary<string, object?> databaseRecord, object userObject)
        {
            if (userObject == null)
                throw new ArgumentNullException(nameof(userObject));

            if (databaseRecord == null)
                throw new ArgumentNullException(nameof(databaseRecord));

            Type objectType = userObject.GetType();

            IReadOnlyDictionary<string, PropertyInfo> properties = GetCachedProperties(objectType);

            foreach (var kvp in databaseRecord)
            {
                PropertyInfo? pi = null;
                object? value = null;


                try
                {
                    value = kvp.Value;
                    if (!properties.TryGetValue(kvp.Key, out pi))
                        continue;

                    if (value == null || value == DBNull.Value)
                    {
                        // EXACT original behavior: assign default value (null for ref/nullable; default(T) for value types)
                        pi.SetValue(userObject, default);
                        continue;
                    }

                    Type targetType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                    object? converted = ConvertValueStrict(value, targetType, kvp.Key);
                    pi.SetValue(userObject, converted);
                }
                catch (Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    string? userPropertyType = pi?.PropertyType.ToString();
                    string? databasePropertyType = value?.GetType().ToString();

                    string errStr =
                        $"LoadDbValues failure for column '{kvp.Key}' type '{databasePropertyType}' " +
                        $"to provided property '{objectType}.{kvp.Key}' type '{userPropertyType}'.";

                    SxmLogging.Log(ex, errStr);
                    throw;
                }
                catch (Exception ex)
                {
                    string? userPropertyType = pi?.PropertyType.ToString();
                    string? databasePropertyType = value?.GetType().ToString();

                    string errStr =
                        $"LoadDbValues failure for column '{kvp.Key}' type '{databasePropertyType}' " +
                        $"to provided property '{objectType}.{kvp.Key}' type '{userPropertyType}'.";

                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }
        }

        /// <summary>
        /// Retrieves a cached dictionary of all public, writable properties for the given <paramref name="objectType"/>.
        /// 
        /// The dictionary maps property names (case-sensitive, ordinal comparison) to their corresponding <see cref="PropertyInfo"/> objects.
        /// If the type has not been seen before, the properties are retrieved via reflection and cached for future lookups using a
        /// thread-safe <see cref="ConcurrentDictionary{TKey, TValue}"/>.
        /// </summary>
        /// <param name="objectType">The type whose writable properties are being retrieved.</param>
        /// <returns>
        /// An <see cref="IReadOnlyDictionary{String, PropertyInfo}"/> mapping property names to <see cref="PropertyInfo"/> instances.
        /// </returns>
        /// <remarks>
        /// - Only public instance properties with a setter (<c>CanWrite</c>) are included.
        /// - Thread-safe caching ensures that reflection is performed at most once per type, improving performance on repeated calls.
        /// - The returned dictionary is read-only to prevent accidental modification of cached metadata.
        /// - Property name lookups are case-sensitive and use ordinal string comparison to maintain consistency with database keys.
        /// </remarks>
        private static IReadOnlyDictionary<string, PropertyInfo> GetCachedProperties(Type objectType)
        {
            return _typePropertyCache.GetOrAdd(
                objectType,
                type => type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .ToDictionary(p => p.Name, StringComparer.Ordinal));
        }

        /// <summary>
        /// Converts a boxed provider <paramref name="value"/> to the specified
        /// <paramref name="targetType"/> using strict, behavior-preserving rules.
        /// 
        /// The conversion logic mirrors the original implementation exactly,
        /// including:
        /// - Explicit range validation for integer types
        /// - Custom string-based conversions for <see cref="decimal"/> and <see cref="ulong"/>
        /// - Special handling for <see cref="Guid"/>, <see cref="DateTime"/>,
        ///   <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>,
        ///   <see cref="DateOnly"/>, and <see cref="TimeOnly"/>
        /// - Boolean conversion where only the string "1" evaluates to <c>true</c>
        /// 
        /// Custom numeric converters perform logging and wrap exceptions
        /// consistently with the original behavior.
        /// </summary>
        /// <param name="value">
        /// Boxed provider value retrieved from the database record.
        /// </param>
        /// <param name="targetType">
        /// Target CLR type (nullable types should already be unwrapped).
        /// </param>
        /// <param name="columnName">
        /// Column name used for diagnostic logging and error context.
        /// </param>
        /// <returns>
        /// The converted value suitable for assignment to a property of
        /// type <paramref name="targetType"/>.
        /// </returns>
        /// <exception cref="OverflowException">
        /// Thrown when a numeric value exceeds the range of the target type.
        /// </exception>
        /// <exception cref="FormatException">
        /// Thrown when a value cannot be converted to the target type.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when conversion fails and the error is wrapped.
        /// </exception>
        /// <remarks>
        /// This method intentionally avoids generic conversion helpers such as
        /// <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> for integer
        /// types in order to preserve explicit range checking and diagnostic behavior.
        /// </remarks>
        private static object? ConvertValueStrict(object value, Type targetType, string columnName)
        {
            // ---- Integer Types (preserve your converters exactly) ----

            if (targetType == typeof(int))
                return ConvertToInt32(value, columnName);

            if (targetType == typeof(long))
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(short))
                return ConvertToInt16(value, columnName);

            if (targetType == typeof(ushort))
                return ConvertToUInt16(value, columnName);

            if (targetType == typeof(uint))
                return ConvertToUInt32(value, columnName);

            if (targetType == typeof(sbyte))
                return ConvertToSByte(value, columnName);

            if (targetType == typeof(byte))
                return ConvertToByte(value, columnName);

            if (targetType == typeof(ulong))
            {
                // original behavior: string-based conversion only
                if (value is string s)
                    return SxmColumnDataConverters.ULongFromString(s);
            }

            // ---- Floating Point ----

            if (targetType == typeof(float))
                return ConvertToSingle(value, columnName);

            if (targetType == typeof(double))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);

            if (targetType == typeof(decimal))
            {
                // original: string-based conversion only
                if (value is string s)
                    return SxmColumnDataConverters.DecimalFromString(s);
            }

            // ---- String ----

            if (targetType == typeof(string))
                return value.ToString();

            // ---- Guid ----

            if (targetType == typeof(Guid))
            {
                if (value is byte[] bytes)
                    return SxmColumnDataConverters.GuidFromRfc4122Bytes(bytes);

                if (value is string s)
                    return SxmColumnDataConverters.GuidFromString(s);
            }

            // ---- Bool (EXACT original semantics) ----
            // Original: if ToString() == "1" => true, else false
            if (targetType == typeof(bool))
            {
                return value.ToString()!.Equals("1");
            }

            // ---- DateTime ----

            if (targetType == typeof(DateTime))
            {
                if (value is long l)
                    return SxmColumnDataConverters.DateTimeFromTicks(l);

                if (value is string s)
                    return SxmColumnDataConverters.DateTimeFromString(s);
            }

            // ---- DateTimeOffset ----

            if (targetType == typeof(DateTimeOffset))
            {
                if (value is long l)
                    return SxmColumnDataConverters.DateTimeOffsetFromTicks(l);

                if (value is string s)
                    return SxmColumnDataConverters.DateTimeOffsetFromString(s);
            }

            // ---- TimeSpan ----

            if (targetType == typeof(TimeSpan))
            {
                if (value is long l)
                    return SxmColumnDataConverters.TimeSpanFromTotalMilliseconds(l);

                if (value is string s)
                    return SxmColumnDataConverters.TimeSpanFromString(s);
            }

            // ---- DateOnly ----

            if (targetType == typeof(DateOnly))
            {
                if (value is long l)
                    return SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l);

                if (value is string s)
                    return SxmColumnDataConverters.DateOnlyFromString(s);
            }

            // ---- TimeOnly ----

            if (targetType == typeof(TimeOnly))
            {
                if (value is long l)
                    return SxmColumnDataConverters.TimeOnlyFromTotalMilliseconds(l);

                if (value is string s)
                    return SxmColumnDataConverters.TimeOnlyFromString(s);
            }

            // ---- Fallback (original final else) ----

            return value;
        }

        /// <summary>
        /// Convert boxed provider value to <see cref="ushort"/> with range checking and logging.
        /// </summary>
        /// <param name="value">Boxed provider value.</param>
        /// <param name="columnName">Column name for error context.</param>
        /// <returns>Converted <see cref="ushort"/>.</returns>
        private static ushort ConvertToUInt16(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < ushort.MinValue || n > ushort.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside UInt16 range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (ushort)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to UInt16.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to UInt16.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to Int32 with range check and logging.
        /// </summary>
        /// <param name="value">Boxed provider value.</param>
        /// <param name="columnName">Column name for error context.</param>
        /// <returns>Converted int.</returns>
        private static int ConvertToInt32(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < int.MinValue || n > int.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside Int32 range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (int)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to Int32.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to Int32.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to <see cref="sbyte"/> with range checking and logging.
        /// </summary>
        /// <param name="value">Boxed provider value.</param>
        /// <param name="columnName">Column name for error context.</param>
        /// <returns>Converted <see cref="sbyte"/>.</returns>
        private static sbyte ConvertToSByte(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < sbyte.MinValue || n > sbyte.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside SByte range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (sbyte)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to SByte.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to SByte.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to Int16 with range check and logging.
        /// </summary>
        private static short ConvertToInt16(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < short.MinValue || n > short.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside Int16 range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (short)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to Int16.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to Int16.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to Byte with range check and logging.
        /// </summary>
        private static byte ConvertToByte(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < byte.MinValue || n > byte.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside Byte range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (byte)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to Byte.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to Byte.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to UInt32 with range check and logging.
        /// </summary>
        private static uint ConvertToUInt32(object value, string columnName)
        {
            try
            {
                long n = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (n < uint.MinValue || n > uint.MaxValue)
                {
                    string msg = $"Value for column '{columnName}' is outside UInt32 range.";
                    var ex = new OverflowException(msg);
                    SxmLogging.Log(ex, msg);
                    throw ExceptionHelper.Wrap(ex, msg);
                }
                return (uint)n;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to UInt32.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to UInt32.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Convert boxed provider value to Single(float) with logging.
        /// </summary>
        private static float ConvertToSingle(object value, string columnName)
        {
            try
            {
                double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return (float)d;
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                string err = $"Failed converting column '{columnName}' value to Single.";
                SxmLogging.Log(ex, err);
                throw;
            }
            catch (Exception ex)
            {
                string err = $"Failed converting column '{columnName}' value to Single.";
                SxmLogging.Log(ex, err);
                throw ExceptionHelper.Wrap(ex, err);
            }
        }

        /// <summary>
        /// Converts an entity's properties into a dictionary of parameter values suitable for database commands.
        /// Handles storage-type conversions (TEXT, INTEGER, BLOB) for GUID, DateTime, DateOnly,
        /// DateTimeOffset, TimeSpan, TimeOnly, and specialized numeric encodings.
        /// </summary>
        /// <param name="columnsToInclude">
        /// Dictionary mapping column name to database storage type (e.g., "TEXT", "INTEGER", "BLOB").
        /// </param>
        /// <param name="entity">
        /// The entity instance to read values from.
        /// </param>
        /// <returns>
        /// Dictionary mapping column names to values ready for DB insertion or update.
        /// Null values are represented by <see cref="DBNull.Value"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if required arguments are null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when property access, conversion, or storage-type mapping fails.
        /// </exception>
        internal static Dictionary<string, object?> LoadParameterValues(Dictionary<string, string> columnsToInclude, object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (columnsToInclude == null)
                throw new ArgumentNullException(nameof(columnsToInclude));

            Type objectType = entity.GetType();
            IReadOnlyDictionary<string, PropertyInfo> properties = GetCachedProperties(objectType);

            Dictionary<string, object?> returnDictionary = new Dictionary<string, object?>();

            foreach (KeyValuePair<string, string> kvp in columnsToInclude)
            {
                // Keep these outside try so catch blocks can reference them without extra reflection.
                PropertyInfo? pi = null;
                object? value = null;

                // Normalize DB type once per column (avoid repeated ToUpper allocations).
                string dbType = kvp.Value;

                string columnName = kvp.Key;

                try
                {

                    // Subset-entity behavior: if property isn't present, omit it from output (caller controls include list).
                    if (!properties.TryGetValue(columnName, out pi))
                        continue;

                    value = pi.GetValue(entity);

                    // Null maps to DBNull.Value (same contract as your original method).
                    if (value == null)
                    {
                        returnDictionary[columnName] = DBNull.Value;
                        continue;
                    }

                    // Use real types (no .Name string comparisons).
                    Type targetType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;

                    object? dbValue = DBNull.Value;

                    // Decimal and ULong are encoded as TEXT via your converters.
                    if (targetType == typeof(decimal))
                    {
                        string? s = SxmColumnDataConverters.DecimalToString((decimal)value);
                        dbValue = (object?)s ?? DBNull.Value;
                    }
                    else if (targetType == typeof(ulong))
                    {
                        string? s = SxmColumnDataConverters.ULongToString((ulong)value);
                        dbValue = (object?)s ?? DBNull.Value;
                    }

                    // GUID supports TEXT or BLOB encodings.
                    else if (targetType == typeof(Guid))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.GuidToString((Guid)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("BLOB", StringComparison.OrdinalIgnoreCase))
                        {
                            byte[]? b = SxmColumnDataConverters.GuidToNativeBytes((Guid)value);
                            dbValue = (object?)b ?? DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for Guid
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }

                    // DateTime supports TEXT or INTEGER (Unix ms).
                    else if (targetType == typeof(DateTime))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.DateTimeToString((DateTime)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            long? l = SxmColumnDataConverters.DateTimeToTicks((DateTime)value);
                            dbValue = l.HasValue ? (object)l.Value : DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for DateTime
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }

                    // DateOnly supports TEXT or INTEGER (Unix day number).
                    else if (targetType == typeof(DateOnly))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.DateOnlyToString((DateOnly)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            int? i = SxmColumnDataConverters.DateOnlyToUnixDayNumber((DateOnly)value);
                            dbValue = i.HasValue ? (object)i.Value : DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for DateOnly
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }

                    // DateTimeOffset supports TEXT or INTEGER (Ticks).
                    else if (targetType == typeof(DateTimeOffset))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.DateTimeOffsetToString((DateTimeOffset)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            long? l = SxmColumnDataConverters.DateTimeOffsetToTicks((DateTimeOffset)value);
                            dbValue = l.HasValue ? (object)l.Value : DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for DateTimeOffset
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }

                    // TimeSpan supports TEXT or INTEGER (total ms).
                    else if (targetType == typeof(TimeSpan))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.TimeSpanToString((TimeSpan)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            long? l = SxmColumnDataConverters.TimeSpanToTotalMilliseconds((TimeSpan)value);
                            dbValue = l.HasValue ? (object)l.Value : DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for TimeSpan
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }

                    // TimeOnly supports TEXT or INTEGER (total ms).
                    else if (targetType == typeof(TimeOnly))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.TimeOnlyToString((TimeOnly)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            long? l = SxmColumnDataConverters.TimeOnlyToTotalMilliseconds((TimeOnly)value);
                            dbValue = l.HasValue ? (object)l.Value : DBNull.Value;
                        }
                        else
                        {
                            // Unsupported storage type for TimeOnly
                            throw UnsupportedDbType(dbType, columnName, targetType, objectType);
                        }
                    }
                    else
                    {
                        // Default: use value directly (keep non-null).
                        dbValue = value;
                    }

                    // Ensure we never return null (contract says DBNull.Value represents null).
                    returnDictionary[columnName] = dbValue ?? DBNull.Value;
                }
                catch (Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
                {
                    string? userPropertyType = pi?.PropertyType.ToString();
                    string? valueType = value?.GetType().ToString();

                    string errStr =
                        $"LoadParameterValues failure for column '{columnName}' on entity '{objectType}' " +
                        $"property type '{userPropertyType}' value type '{valueType}' could not convert the entity's property.";

                    SxmLogging.Log(ex, errStr);
                    throw;
                }
                catch (Exception ex)
                {
                    string? userPropertyType = pi?.PropertyType.ToString();
                    string? valueType = value?.GetType().ToString();

                    string errStr =
                        $"LoadParameterValues failure for column '{columnName}' on entity '{objectType}' " +
                        $"property type '{userPropertyType}' value type '{valueType}' could not convert the entity's property.";

                    SxmLogging.Log(ex, errStr);
                    throw ExceptionHelper.Wrap(ex, errStr);
                }
            }

            return returnDictionary;
        }


        internal static string? SqlStatementFromStatementName(string sqlOrStatementName, SqlStatementType sqlStatementType)
        {
            string? statement = null;

            switch (sqlStatementType)
            {
                case SqlStatementType.Select:
                    SxmSqlStatements.SelectStatements.TryGetValue(sqlOrStatementName, out SelectDefinition? selectDefinition);
                    statement = selectDefinition?.SelectSQL;
                    break;

                case SqlStatementType.Update:
                    SxmSqlStatements.UpdateStatements.TryGetValue(sqlOrStatementName, out UpdateDefinition? updateDefinition);
                    statement = updateDefinition?.UpdateSQL;
                    break;

                case SqlStatementType.Delete:
                    SxmSqlStatements.DeleteStatements.TryGetValue(sqlOrStatementName, out DeleteDefinition? deleteDefinition);
                    statement = deleteDefinition?.DeleteSQL;
                    break;

                case SqlStatementType.Insert:
                    SxmSqlStatements.InsertStatements.TryGetValue(sqlOrStatementName, out InsertDefinition? insertDefinition);
                    statement = insertDefinition?.InsertSQL;
                    break;

                // Direct SQL statements.
                case SqlStatementType.SelectDirect:
                case SqlStatementType.UpdateDirect:
                case SqlStatementType.DeleteDirect:
                case SqlStatementType.InsertDirect:
                    statement = sqlOrStatementName;
                    break;

                default: break;
            }

            return statement;
        }

        /// <summary>
        /// Creates an <see cref="ArgumentException"/> indicating that the provided database
        /// storage type is not supported for the mapped CLR property type.
        /// </summary>
        /// <param name="dbType">
        /// The storage type specified by the database schema or column mapping
        /// (for example: "TEXT", "INTEGER", "BLOB").
        /// </param>
        /// <param name="columnName">
        /// The database column being processed.
        /// </param>
        /// <param name="targetType">
        /// The CLR type of the entity property being converted.
        /// </param>
        /// <returns>
        /// An <see cref="ArgumentException"/> describing the unsupported mapping.
        /// </returns>
        /// <remarks>
        /// This helper centralizes exception message formatting for unsupported
        /// storage-type conversions. It ensures consistent diagnostics across
        /// parameter-loading operations without duplicating string construction
        /// logic throughout <c>LoadParameterValues</c>.
        /// </remarks>
        static ArgumentException UnsupportedDbType(string dbType, string columnName, Type targetType, Type objectType)
        {
            // Throwing here signals a configuration or mapping error rather than
            // a runtime data issue. The caller decides whether to wrap or propagate.
            return new ArgumentException(
                $"Unsupported DB type '{dbType}' for column '{columnName}' on entity '{objectType}' mapped to CLR type '{targetType}'.");
        }

        /// <summary>
        /// Return the first item from the supplied list or throw a descriptive exception when the list is empty.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="list">List returned from RunStatementAsync.</param>
        /// <param name="sqlOrStatementName">Name of the SQL statement (used in error text).</param>
        /// <returns>The first element of <paramref name="list"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the list is null or empty.</exception>
        internal static T GetFirstOrThrow<T>(List<T>? list, string sqlOrStatementName)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException($"Insert statement '{sqlOrStatementName}' did not return any rows. Ensure the SQL statement returns a row (e.g. use RETURNING) or call a non-returning insert API.");

            return list[0];
        }
    }

    /// <summary>
    /// Helpers for converting GUIDs to/from RFC-4122 (network) byte order suitable for storing as BLOBs.
    /// </summary>
    internal static class GuidStorageHelpers
    {
        /// <summary>
        /// Convert Guid -> 16 bytes in RFC-4122 (network) order.
        /// </summary>
        /// <param name="g">Source Guid.</param>
        /// <returns>16-byte array in RFC-4122 order.</returns>
        internal static byte[] ToRfc4122Bytes(this Guid g)
        {
            Span<byte> b = stackalloc byte[16];
            g.TryWriteBytes(b); // CLR layout
            // Reverse the three little-endian fields -> network order
            Swap(b, 0, 3); Swap(b, 1, 2);
            Swap(b, 4, 5);
            Swap(b, 6, 7);
            return b.ToArray();
        }

        /// <summary>
        /// Convert RFC-4122 16 bytes -> Guid (CLR layout for .NET Guid ctor).
        /// </summary>
        /// <param name="bytes">Span containing 16 RFC-4122 bytes.</param>
        /// <returns>Guid represented by the RFC-4122 bytes.</returns>
        /// <exception cref="ArgumentException">If <paramref name="bytes"/> does not contain exactly 16 bytes.</exception>
        internal static Guid FromRfc4122Bytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != 16) throw new ArgumentException("GUID must be 16 bytes.", nameof(bytes));
            Span<byte> b = stackalloc byte[16];
            bytes.CopyTo(b);
            // Reverse back to CLR layout
            Swap(b, 0, 3); Swap(b, 1, 2);
            Swap(b, 4, 5);
            Swap(b, 6, 7);
            return new Guid(b.ToArray());
        }

        /// <summary>
        /// Convert RFC-4122 16 bytes (array) -> Guid.
        /// </summary>
        /// <param name="bytes">16-byte RFC-4122 byte array.</param>
        /// <returns>Guid constructed from the bytes.</returns>
        internal static Guid FromRfc4122Bytes(byte[] bytes) => FromRfc4122Bytes((ReadOnlySpan<byte>)bytes);

        /// <summary>
        /// Swap two bytes inside a span.
        /// </summary>
        /// <param name="b">Target span.</param>
        /// <param name="i">Index of first byte.</param>
        /// <param name="j">Index of second byte.</param>
        private static void Swap(Span<byte> b, int i, int j)
        {
            byte t = b[i];
            b[i] = b[j];
            b[j] = t;
        }
    }

    internal static class MemberInfoExtensions
    {
        /// <summary>
        /// Gets the underlying Type of the member (e.g., the property type, field type, etc.).
        /// Returns null when a member type does not expose a concrete type in this context.
        /// </summary>
        /// <param name="member">The MemberInfo instance.</param>
        /// <returns>The underlying Type of the member, or null if the type cannot be determined.</returns>
        internal static Type? GetMemberType(this MemberInfo member)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    return ((FieldInfo)member).FieldType;

                case MemberTypes.Property:
                    return ((PropertyInfo)member).PropertyType;

                case MemberTypes.Event:
                    return ((EventInfo)member).EventHandlerType;

                case MemberTypes.Method:
                    return ((MethodInfo)member).ReturnType;

                default:
                    // Other member types like Constructor, TypeInfo, etc., don't have a single "type" property in this context.
                    return null;
            }
        }
    }
}