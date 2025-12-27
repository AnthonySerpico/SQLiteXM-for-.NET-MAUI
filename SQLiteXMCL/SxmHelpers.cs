using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using System;

//using static CoreFoundation.DispatchSource;
using static SQLiteXM.SxmDefines;
using System.Text.RegularExpressions;

namespace SQLiteXM
{
    /// <summary>
    /// Helper utilities for SQLiteXM: mapping between database rows and user entities,
    /// runtime association wiring, and SQL statement helpers.
    /// </summary>
    public class SxmHelpers
    {
        /// <summary>
        /// Tracks runtime-registered association keys to avoid duplicate registrations.
        /// Key format: "{SourceType.FullName}.{NavigationPropertyName}".
        /// </summary>
        private static ISet<string> _registeredAssociations = new HashSet<string>();
        private SxmHelpers() { }

        /// <summary>
        /// Returns the list of user table names discovered in the internal descriptor table.
        /// </summary>
        /// <param name="sxmConnection">Connection to query; may be null.</param>
        /// <returns>List of table names (may be empty).</returns>
        internal static async Task<List<string>> getAllUserTableNames(SxmConnection? sxmConnection)
        {
            List<string> tableNames = new List<string>();

            if (sxmConnection != null)
            {
                await sxmConnection.executeQueryAsync("SELECT tableName FROM _systemCloudSynchDescriptor", null as List<object>);

                if (sxmConnection.hasRows() == true)
                {
                    string[] fieldNames = sxmConnection.getFieldNames();
                    while (sxmConnection.nextRow() == true)
                    {
                        foreach (string fieldName in fieldNames)
                            tableNames.Add(sxmConnection.getValue(fieldName).ToString());
                    }
                }
            }

            return tableNames;
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
            // 1. Navigation property must have PropertyType.Name == fk.foreignTable
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
                    if (_registeredAssociations.Add(assocKey))
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

            if (statementType == SqlStatementType.select || statementType == SqlStatementType.selectDirect)
                return "SELECT";
            if (statementType == SqlStatementType.insert || statementType == SqlStatementType.insertDirect)
                return "INSERT";
            if (statementType == SqlStatementType.delete || statementType == SqlStatementType.deleteDirect)
                return "DELETE";
            if (statementType == SqlStatementType.update || statementType == SqlStatementType.updateDirect)
                return "UPDATE";

            throw new ArgumentException("The sql statement type could not be found.");
        }

        /// <summary>
        /// Resolves a SQL statement name (or inline SQL) to a <see cref="SqlStatementType"/>.
        /// </summary>
        /// <param name="sqlStatementName">Named statement key or an inline SQL string (e.g., "SELECT ...").</param>
        /// <returns>Corresponding <see cref="SqlStatementType"/>.</returns>
        /// <exception cref="ArgumentException">If <paramref name="sqlStatementName"/> is null/empty or cannot be resolved.</exception>
        internal static SqlStatementType GetDatabaseStatementType(string? sqlStatementName)
        {
            if (string.IsNullOrEmpty(sqlStatementName))
                throw new ArgumentException("A sql statement name cannot be null or empty.");

            if (SxmSqlStatements.selectStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.select;

            if (SxmSqlStatements.updateStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.update;

            if (SxmSqlStatements.deleteStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.delete;

            if (SxmSqlStatements.insertStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.insert;

            // Not a SQL statement in the SQL statements file? Direct SQL statements are processed here.
            SqlStatementType sqlStatementType = GetSqlStatementType(sqlStatementName);

            if (sqlStatementType == SqlStatementType.unknown)
                throw new ArgumentException(string.Format("The sql statement '{0}' could not be found or identified.", sqlStatementName.Length > 30 ? (sqlStatementName.Substring(0, 29) + "...") : sqlStatementName));

            return sqlStatementType;
        }

        /// <summary>
        /// Extracts the table name from a SQLite INSERT/REPLACE statement.
        /// Simplified, depth-aware scanner that skips single- and double-quoted literals
        /// and quoted identifiers. Assumes valid SQLite SQL and no comments.
        /// </summary>
        /// <param name="insertSql">A SQLite INSERT/REPLACE statement (no comments).</param>
        /// <returns>The bare table name (without schema qualification or quoting).</returns>
        internal static string? ExtractTableNameFromInsert(string insertSql)
        {
            ReadOnlySpan<char> s = insertSql.AsSpan().Trim();
            int pos = FindTopLevelKeyword(s, "INSERT", "REPLACE");
            if (pos < 0) throw new ArgumentException($"The table name for the 'INSERT' statement '{insertSql}' could not be identified.");

            // advance past keyword
            var kw = s.Slice(pos, s.Length - pos).StartsWith("INSERT", StringComparison.OrdinalIgnoreCase) ? "INSERT" : "REPLACE";
            int p = pos + kw.Length;
            SkipSpaces(s, ref p);

            // optional "OR <conflict>"
            if (IsKeywordAt(s, p, "OR"))
            {
                p += 2;
                SkipSpaces(s, ref p);
                // skip a single token (conflict name)
                while (p < s.Length && (char.IsLetterOrDigit(s[p]) || s[p] == '_')) p++;
                SkipSpaces(s, ref p);
            }

            // optional INTO
            if (IsKeywordAt(s, p, "INTO"))
            {
                p += 4;
                SkipSpaces(s, ref p);
            }

            string table = ParseRightMostIdentifier(s, ref p);
            if (string.IsNullOrEmpty(table)) throw new ArgumentException($"The table name for the 'INSERT' statement '{insertSql}' could not be identified.");
            return table;
        }

        /// <summary>
        /// Extracts the table name from a SQLite UPDATE statement.
        /// Simplified, depth-aware scanner that skips single- and double-quoted literals
        /// and quoted identifiers. Assumes valid SQLite SQL and no comments.
        /// </summary>
        /// <param name="updateSql">A SQLite UPDATE statement (no comments).</param>
        /// <returns>The bare table name (without schema qualification or quoting).</returns>
        internal static string ExtractTableNameFromUpdate(string updateSql)
        {
            ReadOnlySpan<char> s = updateSql.AsSpan().Trim();
            int pos = FindTopLevelKeyword(s, "UPDATE");
            if (pos < 0) throw new ArgumentException($"The table name for the 'UPDATE' statement '{updateSql}' could not be identified.");

            int p = pos + 6;
            SkipSpaces(s, ref p);

            // optional OR <conflict>
            if (IsKeywordAt(s, p, "OR"))
            {
                p += 2;
                SkipSpaces(s, ref p);
                while (p < s.Length && (char.IsLetterOrDigit(s[p]) || s[p] == '_')) p++;
                SkipSpaces(s, ref p);
            }

            string table = ParseRightMostIdentifier(s, ref p);
            if (string.IsNullOrEmpty(table)) throw new ArgumentException($"The table name for the 'UPDATE' statement '{updateSql}' could not be identified.");
            return table;
        }

        /// <summary>
        /// Extracts the table name from a SQLite SELECT statement.
        /// Simplified: finds top-level FROM and parses the following identifier (schema-qualified allowed).
        /// Throws when FROM item is a subquery. Assumes valid SQLite SQL and no comments.
        /// </summary>
        /// <param name="selectSql">A SQLite SELECT statement (no comments).</param>
        /// <returns>The bare table name (without schema qualification or quoting).</returns>
        internal static string ExtractTableNameFromSelect(string selectSql)
        {
            ReadOnlySpan<char> s = selectSql.AsSpan().Trim();
            int pos = FindTopLevelKeyword(s, "FROM");
            if (pos < 0) throw new ArgumentException($"The table name for the 'SELECT' statement '{selectSql}' could not be identified.");

            int p = pos + 4;
            SkipSpaces(s, ref p);

            if (p < s.Length && s[p] == '(')
                throw new ArgumentException($"The table name for the 'SELECT' statement '{selectSql}' could not be identified."); // FROM (subquery)

            string table = ParseRightMostIdentifier(s, ref p);
            if (string.IsNullOrEmpty(table)) throw new ArgumentException($"The table name for the 'SELECT' statement '{selectSql}' could not be identified.");
            return table;
        }

        /// <summary>
        /// Extracts the table name from a SQLite DELETE statement.
        /// Simplified, depth-aware scanner that skips single- and double-quoted literals
        /// and quoted identifiers. Assumes valid SQLite SQL and no comments.
        /// </summary>
        /// <param name="deleteSql">A SQLite DELETE statement (no comments).</param>
        /// <returns>The bare table name (without schema qualification or quoting).</returns>
        internal static string ExtractTableNameFromDelete(string deleteSql)
        {
            ReadOnlySpan<char> s = deleteSql.AsSpan().Trim();
            int pos = FindTopLevelKeyword(s, "FROM");
            if (pos < 0) throw new ArgumentException($"The table name for the 'DELETE' statement '{deleteSql}' could not be identified.");

            int p = pos + 4;
            SkipSpaces(s, ref p);

            string table = ParseRightMostIdentifier(s, ref p);
            if (string.IsNullOrEmpty(table)) throw new ArgumentException($"The table name for the 'DELETE' statement '{deleteSql}' could not be identified.");
            return table;
        }

        /* ----- small shared helpers used by the simplified methods ----- */

        private static int FindTopLevelKeyword(ReadOnlySpan<char> s, params string[] keywords)
        {
            int depth = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                // skip single-quoted string literal
                if (c == '\'')
                {
                    i = SkipSingleQuoted(s, i);
                    continue;
                }
                // skip double-quoted identifier
                if (c == '"')
                {
                    i = SkipDoubleQuoted(s, i);
                    continue;
                }
                if (c == '(') { depth++; continue; }
                if (c == ')') { if (depth > 0) depth--; continue; }

                if (depth == 0)
                {
                    foreach (var kw in keywords)
                    {
                        if (IsKeywordAt(s, i, kw)) return i;
                    }
                }
            }
            return -1;
        }

        private static int SkipSingleQuoted(ReadOnlySpan<char> s, int i)
        {
            // i points at opening '
            i++;
            while (i < s.Length)
            {
                if (s[i] == '\'' && i + 1 < s.Length && s[i + 1] == '\'') { i += 2; continue; } // escaped ''
                if (s[i] == '\'') return i;
                i++;
            }
            return s.Length - 1;
        }

        private static int SkipDoubleQuoted(ReadOnlySpan<char> s, int i)
        {
            // i points at opening "
            i++;
            while (i < s.Length)
            {
                if (s[i] == '"' && i + 1 < s.Length && s[i + 1] == '"') { i += 2; continue; } // escaped ""
                if (s[i] == '"') return i;
                i++;
            }
            return s.Length - 1;
        }

        private static bool IsKeywordAt(ReadOnlySpan<char> s, int pos, string keyword)
        {
            if (pos + keyword.Length > s.Length) return false;
            if (!s.Slice(pos, keyword.Length).Equals(keyword.AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
            // previous must be start or whitespace
            if (pos > 0 && !char.IsWhiteSpace(s[pos - 1])) return false;
            // next must be whitespace or punctuation that starts tokens
            if (pos + keyword.Length < s.Length && !char.IsWhiteSpace(s[pos + keyword.Length]))
            {
                char nc = s[pos + keyword.Length];
                if (nc != '(' && nc != '.' && nc != '"' && nc != '[' && nc != '`' && nc != '\'') return false;
            }
            return true;
        }

        private static void SkipSpaces(ReadOnlySpan<char> s, ref int p)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
        }

        private static string ParseRightMostIdentifier(ReadOnlySpan<char> s, ref int p)
        {
            List<string> parts = new List<string>();

            while (p < s.Length)
            {
                SkipSpaces(s, ref p);
                if (p >= s.Length) break;
                if (s[p] == '(') break;

                string part;
                if (s[p] == '"')
                {
                    // quoted identifier with "" escapes
                    int start = p + 1;
                    p = start;
                    var sb = new System.Text.StringBuilder();
                    while (p < s.Length)
                    {
                        if (s[p] == '"' && p + 1 < s.Length && s[p + 1] == '"') { sb.Append('"'); p += 2; continue; }
                        if (s[p] == '"') { p++; break; }
                        sb.Append(s[p]); p++;
                    }
                    part = sb.ToString();
                }
                else if (s[p] == '[')
                {
                    // [identifier]
                    p++;
                    var sb = new System.Text.StringBuilder();
                    while (p < s.Length && s[p] != ']') { sb.Append(s[p]); p++; }
                    if (p < s.Length && s[p] == ']') p++;
                    part = sb.ToString();
                }
                else if (s[p] == '`')
                {
                    p++;
                    var sb = new System.Text.StringBuilder();
                    while (p < s.Length && s[p] != '`') { sb.Append(s[p]); p++; }
                    if (p < s.Length && s[p] == '`') p++;
                    part = sb.ToString();
                }
                else
                {
                    int start = p;
                    while (p < s.Length && (char.IsLetterOrDigit(s[p]) || s[p] == '_' || s[p] == '$')) p++;
                    if (p == start) break;
                    part = s.Slice(start, p - start).ToString();
                }

                if (!string.IsNullOrEmpty(part)) parts.Add(part);

                SkipSpaces(s, ref p);

                if (p < s.Length && s[p] == '.')
                {
                    p++; // consume dot and continue to next component
                    continue;
                }

                break;
            }

            return parts.Count == 0 ? string.Empty : parts[parts.Count - 1];
        }

        private static SqlStatementType GetSqlStatementType(string sql)
        {
            ReadOnlySpan<char> s = sql.AsSpan().TrimStart();

            // Helper to check a keyword with token boundary
            static bool StartsWithKeyword(ReadOnlySpan<char> span, string keyword)
            {
                if (!span.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
                    return false;

                if (span.Length == keyword.Length)
                    return true;

                // Ensure next character is whitespace
                return char.IsWhiteSpace(span[keyword.Length]);
            }

            int i = 0;

            // Handle WITH (CTE)
            if (StartsWithKeyword(s, "WITH"))
            {
                i = 4; // skip WITH
                int depth = 0;

                while (i < s.Length)
                {
                    char c = s[i];

                    if (c == '(') depth++;
                    else if (c == ')') depth--;

                    // End of CTE list → next keyword starts when depth == 0
                    if (depth == 0 && char.IsWhiteSpace(c))
                    {
                        var rest = s[i..].TrimStart();

                        if (StartsWithKeyword(rest, "SELECT"))
                            return SqlStatementType.selectDirect;
                        if (StartsWithKeyword(rest, "INSERT") || StartsWithKeyword(rest, "REPLACE"))
                            return SqlStatementType.insertDirect;
                        if (StartsWithKeyword(rest, "UPDATE"))
                            return SqlStatementType.updateDirect;
                        if (StartsWithKeyword(rest, "DELETE"))
                            return SqlStatementType.deleteDirect;
                    }

                    i++;
                }
            }

            // Non-WITH statements
            if (StartsWithKeyword(s, "SELECT"))
                return SqlStatementType.selectDirect;
            if (StartsWithKeyword(s, "INSERT") || StartsWithKeyword(s, "REPLACE"))
                return SqlStatementType.insertDirect;
            if (StartsWithKeyword(s, "UPDATE"))
                return SqlStatementType.updateDirect;
            if (StartsWithKeyword(s, "DELETE"))
                return SqlStatementType.deleteDirect;

            return SqlStatementType.unknown;
        }


        /// <summary>
        /// Creates a list of user objects of type <typeparamref name="TResult"/> from database row dictionaries.
        /// </summary>
        /// <typeparam name="TResult">User entity type with a public parameterless constructor.</typeparam>
        /// <param name="databaseRowsList">List of dictionary rows where keys are column/property names.</param>
        /// <returns>List of populated user objects.</returns>
        internal static List<TResult> populateUserRecord<TResult>(List<Dictionary<string, object?>> databaseRowsList) where TResult : class, new()
        {
            List<TResult> userObjectList = new List<TResult>();

            foreach (Dictionary<string, object?> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                TResult userObject = new TResult();
                loadDbValues(databaseRecord, userObject);
                userObjectList.Add(userObject);
            }

            return userObjectList;
        }

        /// <summary>
        /// Populates properties on <paramref name="userObject"/> from the provided database record map.
        /// The method matches dictionary keys to property names and converts common DB types to CLR types.
        /// </summary>
        /// <param name="databaseRecord">Dictionary mapping column names to values.</param>
        /// <param name="userObject">Destination object to populate.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="userObject"/> or <paramref name="databaseRecord"/> is null.</exception>
        /// <exception cref="ArgumentException">If a database value cannot be cast to the target property type.</exception>
        internal static void loadDbValues(Dictionary<string, object?> databaseRecord, object userObject)
        {
            if (userObject == null) throw new ArgumentNullException(nameof(userObject));
            if (databaseRecord == null) throw new ArgumentNullException(nameof(databaseRecord));

            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = userObject.GetType().GetProperty(kvp.Key);
                    if (pi != null)
                    {
                        if (kvp.Value != DBNull.Value && kvp.Value != null)
                        {
                            string piType = pi.PropertyType.Name;
                            Type? underlyingType = Nullable.GetUnderlyingType(pi.PropertyType);
                            if (underlyingType != null)
                            {
                                piType = underlyingType.Name;
                            }

                            if (piType == typeof(int).Name)
                                pi.SetValue(userObject, (int)(long)kvp.Value);

                            else if (piType == typeof(long).Name)
                                pi.SetValue(userObject, (long)kvp.Value);

                            else if (piType == typeof(float).Name)
                                pi.SetValue(userObject, (float)(double)kvp.Value);

                            else if (piType == typeof(short).Name)
                                pi.SetValue(userObject, (short)(long)kvp.Value);

                            else if (piType == typeof(ushort).Name)
                                pi.SetValue(userObject, (ushort)(long)kvp.Value);

                            else if (piType == typeof(uint).Name)
                                pi.SetValue(userObject, (uint)(long)kvp.Value);

                            else if (piType == typeof(sbyte).Name)
                                pi.SetValue(userObject, (sbyte)(long)kvp.Value);

                            else if (piType == typeof(byte).Name)
                                pi.SetValue(userObject, (byte)(long)kvp.Value);

                            else if (piType == typeof(double).Name)
                                pi.SetValue(userObject, (double)kvp.Value);

                            else if (piType == typeof(string).Name)
                                pi.SetValue(userObject, kvp.Value.ToString());

                            else if (piType == typeof(decimal).Name)    // Large values will overflow if not text in DB.
                                pi.SetValue(userObject, SxmColumnDataConverters.decimalFromString(kvp.Value.ToString()!));

                            else if (piType == typeof(ulong).Name)    // Large values will overflow if not text in DB.
                                pi.SetValue(userObject, SxmColumnDataConverters.uLongFromString((string)kvp.Value));

                            else if (piType == typeof(Guid).Name)
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(byte[]).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.guidFromRfc4122Bytes((byte[])kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.guidFromString((string)kvp.Value));
                            }

                            else if (piType == typeof(bool).Name)
                            {
                                if (kvp.Value.ToString()!.Equals("1"))
                                    pi.SetValue(userObject, true);
                                else
                                    pi.SetValue(userObject, false);
                            }

                            else if (piType == typeof(DateTime).Name)  // Can be either text or long (ticks).
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateTimeFromUnixTimeMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateTimeFromString(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateTimeOffset).Name)  // Can be either text or INTEGER (unix ms).
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateTimeOffsetFromUnixTimeMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateTimeOffsetFromString(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(TimeSpan).Name)  // Can be either text or TIMESPAN ticks.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.timeSpanFromTotalMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.timeSpanFromString(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(DateOnly).Name)  // Can be text, long (dayNumber) or int.
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateOnlyFromUnixDayNumber((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.dateOnlyFromString(kvp.Value.ToString()!));
                            }

                            else if (piType == typeof(TimeOnly).Name)    // Can be either text or ticks (long).
                            {
                                string typeName = kvp.Value.GetType().Name;
                                if (typeName == typeof(long).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.timeOnlyFromTotalMilliseconds((long)kvp.Value));

                                else if (typeName == typeof(string).Name)
                                    pi.SetValue(userObject, SxmColumnDataConverters.timeOnlyFromString(kvp.Value.ToString()!));
                            }

                            else
                            {
                                pi.SetValue(userObject, kvp.Value);
                            }

                        }
                        else
                            pi.SetValue(userObject, default);
                    }
                }
                catch (System.ArgumentException)
                {
                    string? userPropertyType = userObject.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString();
                    string? databasePropertyType = kvp.Value?.GetType().ToString();
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", kvp.Key, databasePropertyType, userObject.GetType().ToString() + "." + kvp.Key, userPropertyType));
                }
            }
        }

        /// <summary>
        /// Converts a user entity's properties into a dictionary of parameter values suitable for database commands.
        /// Handles type conversions for GUID, DateTime, DateOnly, DateTimeOffset, TimeSpan, TimeOnly and numeric/text encodings.
        /// </summary>
        /// <param name="dbColumnNameType">Dictionary mapping column name to database type (e.g., "TEXT", "INTEGER", "BLOB").</param>
        /// <param name="userObject">The user entity instance to read values from.</param>
        /// <returns>Dictionary mapping column names to values ready for DB insertion/update. Null values are represented by <see cref="DBNull.Value"/>.</returns>
        /// <exception cref="ArgumentException">Propagates if property access or conversion fails.</exception>
        internal static Dictionary<string, object?> loadParamaterValues(Dictionary<string, string> dbColumnNameType, object userObject)
        {
            Dictionary<string, object?> returnDictionary = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, string> kvp in dbColumnNameType)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    string columnName = kvp.Key;
                    PropertyInfo? userObjectPI = userObject.GetType().GetProperty(columnName);  // Get the property from the user supplied object that matches the column anme in the database.

                    if (userObjectPI != default)  // If the column is in the user supplied object.
                    {
                        object? userSuppliedObjectData = userObjectPI.GetValue(userObject);
                        if (userSuppliedObjectData != null)  // If the value of the data for the column in the user supplied object is not null;
                        {
                            string userObjectType = userObjectPI.PropertyType.Name;  // Get the data type of the column in the user supplied object.
                            Type? underlyingType = Nullable.GetUnderlyingType(userObjectPI.PropertyType);
                            if (underlyingType != null)
                            {
                                userObjectType = underlyingType.Name;
                            }

                            if (userObjectType == typeof(decimal).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                returnDictionary.Add(columnName, SxmColumnDataConverters.decimalToString((decimal)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(ulong).Name)  // Is the data type for the column in the user object a ulong?
                            {
                                returnDictionary.Add(columnName, SxmColumnDataConverters.uLongToString((ulong)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(Guid).Name)  // Is the data type for the column in the user object a DateTime?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.guidToString((Guid)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("BLOB"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.guidToRfc4122Bytes((Guid)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(DateTime).Name)  // Is the data type for the column in the user object a DateTime?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateTimeToString((DateTime)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateTimeToUnixTimeMilliseconds((DateTime)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(DateOnly).Name)  // Is the data type for the column in the user object a DateOnly?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateOnlyToString((DateOnly)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateOnlyToUnixDayNumber((DateOnly)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(DateTimeOffset).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateTimeOffsetToString((DateTimeOffset)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.dateTimeOffsetToUnixTimeMilliseconds((DateTimeOffset)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(TimeSpan).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.timeSpanToString((TimeSpan)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.timeSpanToTotalMilliseconds((TimeSpan)userSuppliedObjectData));
                            }

                            else if (userObjectType == typeof(TimeOnly).Name)  // Is the data type for the column in the user object a decimal?
                            {
                                if (kvp.Value.ToUpper().Equals("TEXT"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.timeOnlyToString((TimeOnly)userSuppliedObjectData));

                                else if (kvp.Value.ToUpper().Equals("INTEGER"))
                                    returnDictionary.Add(columnName, SxmColumnDataConverters.timeOnlyToTotalMilliseconds((TimeOnly)userSuppliedObjectData));
                            }
                            else
                            {
                                returnDictionary.Add(columnName, userObjectPI.GetValue(userObject));
                            }
                        }
                        else
                            returnDictionary.Add(columnName, DBNull.Value);
                    }
                }
                catch (System.ArgumentException)
                {
                    throw;
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
            return returnDictionary;
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
        public static byte[] ToRfc4122Bytes(this Guid g)
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