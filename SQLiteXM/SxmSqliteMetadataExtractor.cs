using Microsoft.Data.Sqlite;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static SQLiteXM.SxmDefines;

/// <summary>
/// Safely extracts metadata from SQL statements using SQLite's authorizer callback mechanism.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps a <see cref="SqliteConnection"/> and registers a native authorizer callback
/// that fires when SQLite's query compiler accesses database objects. By listening to these callbacks,
/// the extractor can determine the statement type (INSERT, UPDATE, DELETE, SELECT) and identify
/// the primary table being modified or queried - all without executing the statement.
/// </para>
/// <para>
/// The authorizer callback is unhooked and GC handles are released when this object is disposed.
/// Always use this class within a <c>using</c> block to ensure proper cleanup.
/// </para>
/// <para>
/// This is an internal utility class used by <see cref="SxmQueryProcessor"/> to analyze user-supplied SQL.
/// </para>
/// </remarks>
internal class SqliteMetadataExtractor : IDisposable
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
    /// <summary>
    /// Gets the detected SQL statement type (INSERT, UPDATE, DELETE, SELECT, or Unknown).
    /// </summary>
    /// <value>
    /// The statement type as determined by the authorizer callbacks during statement preparation.
    /// Defaults to <see cref="SqlStatementType.Unknown"/> if no operation was detected.
    /// </value>
    internal SqlStatementType DetectedStatementType { get; private set; } = SqlStatementType.Unknown  ;

    /// <summary>
    /// Gets the name of the primary table targeted by the SQL statement.
    /// </summary>
    /// <value>
    /// For modifying statements (INSERT, UPDATE, DELETE), this is the table being modified.
    /// For SELECT statements, this is the first table encountered in the query.
    /// Defaults to "UNKNOWN" if no table could be determined.
    /// </value>
    // For modifying queries, this holds the single primary target table
    internal string PrimaryTargetTable { get; private set; } = "UNKNOWN";

    // For SELECT queries, this tracks ALL tables involved in JOINs, subqueries, or CTEs
    private HashSet<string> _involvedTables { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteMetadataExtractor"/> class and registers
    /// the authorizer callback on the specified connection.
    /// </summary>
    /// <param name="connection">The SQLite connection to attach the authorizer to. Must be open.</param>
    /// <remarks>
    /// The constructor creates a delegate for the authorizer callback, anchors it with a GC handle
    /// to prevent premature collection, and registers it with SQLite via <c>sqlite3_set_authorizer</c>.
    /// The authorizer will fire when any statement is prepared on this connection.
    /// </remarks>
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

    /// <summary>
    /// Resets the extractor state to allow analyzing another statement.
    /// </summary>
    /// <remarks>
    /// This method clears the detected statement type, primary target table, and involved tables collection.
    /// Call this method before preparing a new statement if reusing the same extractor instance.
    /// </remarks>
    internal void Reset()
    {
        DetectedStatementType = SqlStatementType.Unknown;
        PrimaryTargetTable = "UNKNOWN";
        _involvedTables.Clear();
    }

    /// <summary>
    /// Unhooks the authorizer callback and releases managed resources.
    /// </summary>
    /// <remarks>
    /// This method removes the registered authorizer from the SQLite connection and frees the GC handle
    /// that was anchoring the delegate. Always call Dispose (or use a <c>using</c> block) to ensure
    /// the native callback is properly unregistered.
    /// </remarks>
    public void Dispose()
    {
        // Unhook the authorizer before freeing the delegate
        raw.sqlite3_set_authorizer(_connection.Handle, (delegate_authorizer?)null, IntPtr.Zero);

        if (_gcAnchor.IsAllocated) _gcAnchor.Free();
        GC.SuppressFinalize(this);
    }
}
