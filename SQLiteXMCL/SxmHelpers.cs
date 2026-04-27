using SQLiteXM.Internal.Threading;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
//using static CoreFoundation.DispatchSource;
using static SQLiteXM.SxmDefines;
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

            if (sxmConnection != null)
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

            throw new ArgumentException($"The sql statement type could not be found. Statement type: {statementType.ToString()}");
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

            if (SxmSqlStatements.SelectStatements.ContainsKey(sqlStatementName))
                return SqlStatementType.Select;

            if (SxmSqlStatements.UpdateStatements.ContainsKey(sqlStatementName))
                return SqlStatementType.Update;

            if (SxmSqlStatements.DeleteStatements.ContainsKey(sqlStatementName))
                return SqlStatementType.Delete;

            if (SxmSqlStatements.InsertStatements.ContainsKey(sqlStatementName))
                return SqlStatementType.Insert;

            // Not a SQL statement in the SQL statements file? Direct SQL statements are processed here.
            SqlStatementType sqlStatementType = GetSqlStatementType(sqlStatementName);

            if (sqlStatementType == SqlStatementType.Unknown)
                throw new ArgumentException(string.Format("The sql statement '{0}' could not be found or identified.", sqlStatementName.Length > 30 ? (sqlStatementName.Substring(0, 29) + "...") : sqlStatementName));

            return sqlStatementType;
        }

        /// <summary>
        /// Extracts the table name from a SQLite INSERT/REPLACE statement.
        /// Simplified, depth-aware scanner that skips single- and double-quoted literals
        /// and quoted identifiers. Assumes valid SQLite SQL and no comments.
        /// </summary>
        /// <remarks>
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments or malformed statements.
        /// </remarks>
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
        /// <remarks>
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments or malformed statements.
        /// </remarks>
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
        /// <remarks>
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments or malformed statements.
        /// </remarks>
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
        /// <remarks>
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments or malformed statements.
        /// </remarks>
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

        /// <summary>
        /// Scans the SQL span for the first occurrence of any of the specified keywords
        /// that appears at the top level (i.e., not inside parentheses) and not inside
        /// quoted strings or quoted identifiers.
        /// </summary>
        /// <param name="s">
        /// The SQL text to scan.
        /// </param>
        /// <param name="keywords">
        /// One or more keywords to locate (case-insensitive).
        /// </param>
        /// <returns>
        /// The zero-based character index of the first matching keyword occurrence,
        /// or <c>-1</c> if none is found.
        /// </returns>
        /// <remarks>
        /// This is a lightweight scanner used by table-name and statement-type extraction
        /// helpers. It tracks parentheses depth and skips:
        /// <list type="bullet">
        /// <item><description>Single-quoted string literals (with '' escaping)</description></item>
        /// <item><description>Double-quoted identifiers (with "" escaping)</description></item>
        /// </list>
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments or malformed SQL.
        /// </remarks>
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

        /// <summary>
        /// Advances an index from the opening single-quote of a SQLite string literal
        /// to the index of its closing single-quote.
        /// </summary>
        /// <param name="s">
        /// The SQL text span containing the string literal.
        /// </param>
        /// <param name="i">
        /// The index of the opening single-quote character (<c>'</c>).
        /// </param>
        /// <returns>
        /// The index of the closing single-quote character. If no closing quote is found,
        /// returns the last valid index in the span.
        /// </returns>
        /// <remarks>
        /// SQLite string literals escape a single quote by doubling it (<c>''</c>).
        /// This method recognizes that escape sequence and continues scanning.
        /// Assumes valid SQLite SQL input without comments; it is not a general SQL lexer.
        /// </remarks>
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

        /// <summary>
        /// Advances an index from the opening double-quote of a quoted SQLite identifier
        /// to the index of its closing double-quote.
        /// </summary>
        /// <param name="s">
        /// The SQL text span containing the quoted identifier.
        /// </param>
        /// <param name="i">
        /// The index of the opening double-quote character (<c>"</c>).
        /// </param>
        /// <returns>
        /// The index of the closing double-quote character. If no closing quote is found,
        /// returns the last valid index in the span.
        /// </returns>
        /// <remarks>
        /// SQLite quoted identifiers escape a double quote by doubling it (<c>""</c>).
        /// This method recognizes that escape sequence and continues scanning.
        /// Assumes valid SQLite SQL input without comments; it is not a general SQL lexer.
        /// </remarks>
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

        /// <summary>
        /// Determines whether a keyword occurs at the specified position and is delimited
        /// by token boundaries suitable for SQLite keyword scanning.
        /// </summary>
        /// <param name="s">
        /// The SQL text span.
        /// </param>
        /// <param name="pos">
        /// The position to test for a keyword match.
        /// </param>
        /// <param name="keyword">
        /// The keyword to match (case-insensitive).
        /// </param>
        /// <returns>
        /// <c>true</c> if the keyword matches at <paramref name="pos"/> and is properly delimited;
        /// otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This helper enforces simple token-boundary rules:
        /// <list type="bullet">
        /// <item><description>The keyword must match case-insensitively at the specified position.</description></item>
        /// <item><description>The preceding character must be start-of-span or whitespace.</description></item>
        /// <item><description>The following character must be whitespace or a punctuation character that can begin a token.</description></item>
        /// </list>
        /// These rules are intentionally lightweight and designed for trusted, well-formed
        /// SQLite SQL without comments.
        /// </remarks>
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

        /// <summary>
        /// Advances the parsing position past any contiguous whitespace characters.
        /// </summary>
        /// <param name="s">
        /// The SQL text span.
        /// </param>
        /// <param name="p">
        /// The current parsing position. Updated to the first non-whitespace character position.
        /// </param>
        /// <remarks>
        /// This helper centralizes whitespace skipping for lightweight SQL scanning routines.
        /// Whitespace is determined using <see cref="char.IsWhiteSpace(char)"/>.
        /// </remarks>
        private static void SkipSpaces(ReadOnlySpan<char> s, ref int p)
        {
            while (p < s.Length && char.IsWhiteSpace(s[p])) p++;
        }

        /// <summary>
        /// Parses a multipart SQL identifier from the provided span and returns the
        /// right-most identifier component (for example, extracting <c>TableName</c>
        /// from <c>schema.TableName</c>).
        /// </summary>
        /// <param name="s">
        /// The SQL text span containing the identifier to parse.
        /// </param>
        /// <param name="p">
        /// The current parsing position within <paramref name="s"/>. The position is
        /// advanced past the parsed identifier components.
        /// </param>
        /// <returns>
        /// The right-most identifier component, or an empty string if no valid identifier
        /// could be parsed.
        /// </returns>
        /// <remarks>
        /// Supports SQLite identifier formats including:
        /// <list type="bullet">
        /// <item><description>Unquoted identifiers (letters, digits, '_' or '$')</description></item>
        /// <item><description>Double-quoted identifiers with escaped quotes ("")</description></item>
        /// <item><description>Bracketed identifiers ([identifier])</description></item>
        /// <item><description>Backtick-quoted identifiers (`identifier`)</description></item>
        /// </list>
        ///
        /// This method performs lightweight token parsing rather than full SQL parsing.
        /// Assumes valid SQLite SQL input without comments or malformed identifier syntax.
        /// Parsing stops at whitespace, parentheses, or when identifier components end.
        /// </remarks>
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

        /// <summary>
        /// Determines the <see cref="SqlStatementType"/> of a SQL command by examining
        /// the leading SQLite keyword sequence.
        /// </summary>
        /// <param name="sql">
        /// The SQL statement text to analyze.
        /// </param>
        /// <returns>
        /// A <see cref="SqlStatementType"/> representing the detected command type,
        /// or <see cref="SqlStatementType.Unknown"/> if the statement cannot be classified.
        /// </returns>
        /// <remarks>
        /// This method performs lightweight keyword scanning rather than full SQL parsing.
        /// It supports direct statements and common table expression (CTE) syntax beginning
        /// with <c>WITH</c>.
        ///
        /// Assumes valid SQLite SQL input without comments. This method does not attempt
        /// to parse or ignore SQL comments, malformed SQL, or vendor-specific extensions.
        /// The detection logic relies on token boundaries and balanced parentheses within
        /// CTE declarations.
        /// </remarks>
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
                            return SqlStatementType.SelectDirect;
                        if (StartsWithKeyword(rest, "INSERT") || StartsWithKeyword(rest, "REPLACE"))
                            return SqlStatementType.InsertDirect;
                        if (StartsWithKeyword(rest, "UPDATE"))
                            return SqlStatementType.UpdateDirect;
                        if (StartsWithKeyword(rest, "DELETE"))
                            return SqlStatementType.DeleteDirect;
                    }

                    i++;
                }
            }

            // Non-WITH statements
            if (StartsWithKeyword(s, "SELECT"))
                return SqlStatementType.SelectDirect;
            if (StartsWithKeyword(s, "INSERT") || StartsWithKeyword(s, "REPLACE"))
                return SqlStatementType.InsertDirect;
            if (StartsWithKeyword(s, "UPDATE"))
                return SqlStatementType.UpdateDirect;
            if (StartsWithKeyword(s, "DELETE"))
                return SqlStatementType.DeleteDirect;

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
                    return SxmColumnDataConverters.DateTimeFromUnixTimeMilliseconds(l);

                if (value is string s)
                    return SxmColumnDataConverters.DateTimeFromString(s);
            }

            // ---- DateTimeOffset ----

            if (targetType == typeof(DateTimeOffset))
            {
                if (value is long l)
                    return SxmColumnDataConverters.DateTimeOffsetFromUnixTimeMilliseconds(l);

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
                            byte[]? b = SxmColumnDataConverters.GuidToRfc4122Bytes((Guid)value);
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
                            long? l = SxmColumnDataConverters.DateTimeToUnixTimeMilliseconds((DateTime)value);
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

                    // DateTimeOffset supports TEXT or INTEGER (Unix ms).
                    else if (targetType == typeof(DateTimeOffset))
                    {
                        if (dbType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
                        {
                            string? s = SxmColumnDataConverters.DateTimeOffsetToString((DateTimeOffset)value);
                            dbValue = (object?)s ?? DBNull.Value;
                        }
                        else if (dbType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase))
                        {
                            long? l = SxmColumnDataConverters.DateTimeOffsetToUnixTimeMilliseconds((DateTimeOffset)value);
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
        /// <param name="sqlStatementName">Name of the SQL statement (used in error text).</param>
        /// <returns>The first element of <paramref name="list"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the list is null or empty.</exception>
        internal static T GetFirstOrThrow<T>(List<T>? list, string sqlStatementName)
        {
            if (list == null || list.Count == 0)
                throw new InvalidOperationException($"Insert statement '{sqlStatementName}' did not return any rows. Ensure the SQL statement returns a row (e.g. use RETURNING) or call a non-returning insert API.");

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