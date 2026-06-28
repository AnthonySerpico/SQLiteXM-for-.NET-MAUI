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
                // Rule 1: The first root event locks down the statement context
                if (DetectedStatementType == SqlStatementType.Unknown   )
                {
                    DetectedStatementType = (actionCode == SQLITE_INSERT) ? SqlStatementType.InsertDirect :
                                            (actionCode == SQLITE_UPDATE) ? SqlStatementType.UpdateDirect : SqlStatementType.DeleteDirect   ;
                    PrimaryTargetTable = detail1.utf8_to_string() ?? string.Empty; // Firmly lock the primary table being modified
                }
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
}
