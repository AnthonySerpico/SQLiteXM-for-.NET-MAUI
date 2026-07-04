using Microsoft.Data.Sqlite;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static SQLiteXM.SxmDefines;

public class SqliteMetadataExtractor : IDisposable
{
    private const int SQLITE_INSERT = 18;
    private const int SQLITE_UPDATE = 23;
    private const int SQLITE_DELETE = 9;
    private const int SQLITE_SELECT = 21;
    private const int SQLITE_READ = 20;

    private readonly delegate_authorizer _authDelegate;
    private GCHandle _gcAnchor;
    private readonly SqliteConnection _connection;

    // Internally accessible metadata
    internal SqlStatementType DetectedStatementType { get; private set; } = SqlStatementType.Unknown  ;

    // For modifying queries, this holds the single primary target table
    internal string PrimaryTargetTable { get; private set; } = "UNKNOWN";

    // For SELECT queries, this tracks ALL tables involved in JOINs, subqueries, or CTEs
    private HashSet<string> _involvedTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal SqliteMetadataExtractor(SqliteConnection connection)
    {
        _connection = connection;
        _authDelegate = (object userData, int code, utf8z d1, utf8z d2, utf8z db, utf8z trg) => AuthorizerCallback(userData, code, d1, d2, db, trg);
        _gcAnchor = GCHandle.Alloc(_authDelegate);

        raw.sqlite3_set_authorizer(connection.Handle, _authDelegate, IntPtr.Zero);
    }

    private int AuthorizerCallback(object pUserData, int actionCode, utf8z detail1, utf8z detail2, utf8z dbName, utf8z triggerName)
    {
        // Debug: Log all authorizer events
        System.Diagnostics.Debug.WriteLine($"Authorizer fired: actionCode={actionCode}, detail1={detail1.utf8_to_string()}, detail2={detail2.utf8_to_string()}");

        // Convert utf8z to strings
        string? triggerStr = triggerName.utf8_to_string();

        // Ignore side effects originating from internal triggers/views
        if (!string.IsNullOrEmpty(triggerStr)) return 0;

        switch (actionCode)
        {
            case SQLITE_INSERT:
            case SQLITE_UPDATE:
            case SQLITE_DELETE:
                // ALWAYS override statement type for modifying operations
                DetectedStatementType = (actionCode == SQLITE_INSERT) ? SqlStatementType.InsertDirect :
                                        (actionCode == SQLITE_UPDATE) ? SqlStatementType.UpdateDirect :
                                        SqlStatementType.DeleteDirect;

                // ALWAYS set PrimaryTargetTable from modifying operations - this is the authoritative source
                PrimaryTargetTable = detail1.utf8_to_string() ?? string.Empty;
                break;

            case SQLITE_SELECT:
                if (DetectedStatementType == SqlStatementType.Unknown)
                {
                    DetectedStatementType = SqlStatementType.SelectDirect;
                }
                break;

            case SQLITE_READ:
                // Rule 2: Collect all unique table references safely without overwriting the main action
                string? tableName = detail1.utf8_to_string();
                if (!string.IsNullOrEmpty(tableName))
                {
                    _involvedTables.Add(tableName);

                    // Fallback: set primary table for the first table encountered (typically for SELECT queries)
                    // But don't overwrite if already set by INSERT/UPDATE/DELETE
                    if (PrimaryTargetTable == "UNKNOWN")
                    {
                        PrimaryTargetTable = tableName;
                    }
                }
                break;
        }

        return 0; // SQLITE_OK
    }

    internal void Reset()
    {
        DetectedStatementType = SqlStatementType.Unknown;
        PrimaryTargetTable = "UNKNOWN";
        _involvedTables.Clear();
    }

    public void Dispose()
    {
        // Unhook the authorizer before freeing the delegate
        raw.sqlite3_set_authorizer(_connection.Handle, (delegate_authorizer?)null, IntPtr.Zero);

        if (_gcAnchor.IsAllocated) _gcAnchor.Free();
        GC.SuppressFinalize(this);
    }

    public static bool SqlHasReturningClause(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return false;

        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inBacktick = false;
        bool inBracketIdentifier = false;
        bool inLineComment = false;
        bool inBlockComment = false;

        static bool IsIdentifierChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_';

        const string token = "RETURNING";

        for (int i = 0; i < sql.Length; i++)
        {
            char c = sql[i];
            char next = (i + 1 < sql.Length) ? sql[i + 1] : '\0';

            // Handle exiting line comments
            if (inLineComment)
            {
                if (c == '\n')
                    inLineComment = false;
                continue;
            }

            // Handle exiting block comments
            if (inBlockComment)
            {
                if (c == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            // Handle entering comments
            if (!inSingleQuote && !inDoubleQuote && !inBacktick && !inBracketIdentifier)
            {
                if (c == '-' && next == '-')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }
            }

            // Handle single-quoted strings
            if (!inDoubleQuote && !inBacktick && !inBracketIdentifier)
            {
                if (!inSingleQuote)
                {
                    if (c == '\'')
                    {
                        inSingleQuote = true;
                        continue;
                    }
                }
                else
                {
                    if (c == '\'')
                    {
                        // Escaped quote ('')
                        if (next == '\'')
                        {
                            i++;
                            continue;
                        }

                        inSingleQuote = false;
                    }

                    continue;
                }
            }

            // Handle double-quoted identifiers
            if (!inSingleQuote && !inBacktick && !inBracketIdentifier)
            {
                if (!inDoubleQuote)
                {
                    if (c == '"')
                    {
                        inDoubleQuote = true;
                        continue;
                    }
                }
                else
                {
                    if (c == '"')
                        inDoubleQuote = false;

                    continue;
                }
            }

            // Handle backtick identifiers
            if (!inSingleQuote && !inDoubleQuote && !inBracketIdentifier)
            {
                if (!inBacktick)
                {
                    if (c == '`')
                    {
                        inBacktick = true;
                        continue;
                    }
                }
                else
                {
                    if (c == '`')
                        inBacktick = false;

                    continue;
                }
            }

            // Handle bracketed identifiers
            if (!inSingleQuote && !inDoubleQuote && !inBacktick)
            {
                if (!inBracketIdentifier)
                {
                    if (c == '[')
                    {
                        inBracketIdentifier = true;
                        continue;
                    }
                }
                else
                {
                    if (c == ']')
                        inBracketIdentifier = false;

                    continue;
                }
            }

            // Only search when not inside any quoted/comment region
            if (!inSingleQuote &&
                !inDoubleQuote &&
                !inBacktick &&
                !inBracketIdentifier &&
                !inLineComment &&
                !inBlockComment)
            {
                if (char.ToUpperInvariant(c) != 'R')
                    continue;

                if (sql.Length - i < token.Length)
                    continue;

                bool match = true;

                for (int j = 0; j < token.Length; j++)
                {
                    if (char.ToUpperInvariant(sql[i + j]) != token[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (!match)
                    continue;

                int start = i;
                int end = i + token.Length;

                bool leftBoundary =
                    start == 0 || !IsIdentifierChar(sql[start - 1]);

                bool rightBoundary =
                    end >= sql.Length || !IsIdentifierChar(sql[end]);

                if (leftBoundary && rightBoundary)
                    return true;
            }
        }

        return false;
    }
}
