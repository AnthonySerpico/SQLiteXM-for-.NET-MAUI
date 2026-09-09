using Microsoft.Data.Sqlite;
using SQLiteXM;
using System;
using static SQLiteXM.SxmDefines;

/// <summary>
/// Internal helper for analyzing user-supplied SQL statements to determine their type and target table.
/// </summary>
/// <remarks>
/// This class uses SQLite's authorizer callback mechanism to safely extract metadata from
/// untrusted SQL without executing the statement. It opens a temporary connection, prepares
/// the statement (triggering authorizer hooks), and captures the statement type and primary table name.
/// </remarks>
internal class SxmQueryProcessor
{
    /// <summary>
    /// Analyzes user-supplied SQL to determine its statement type and primary target table.
    /// </summary>
    /// <param name="userSuppliedSql">The SQL statement to analyze. Can be any valid SQLite SQL.</param>
    /// <param name="databaseName">Optional database name. If null, uses the default database.</param>
    /// <returns>A <see cref="SqlStatementDetails"/> object containing the detected statement type and target table name.</returns>
    /// <remarks>
    /// <para>
    /// This method uses SQLite's native authorizer callback to extract metadata without executing the statement.
    /// It creates a temporary connection, registers an authorizer, prepares the statement, and captures
    /// the operation type (INSERT, UPDATE, DELETE, SELECT) and the primary table involved.
    /// </para>
    /// <para>
    /// If the SQL has syntax errors or references non-existent tables, the method catches the exception
    /// and returns default values (SqlStatementType.Unknown and empty table name).
    /// </para>
    /// <para>
    /// This is an internal method used by the routing logic to distinguish between different SQL statement
    /// types when processing both named and direct SQL statements.
    /// </para>
    /// </remarks>
    internal static SqlStatementDetails AnalyzeUserQuery(string userSuppliedSql, string? databaseName = null)
    {
        Microsoft.Data.Sqlite.SqliteConnection? connection = null;
        SqlStatementDetails embeddedSqlStatementDetails = new();
        try
        {
            string? connectionString = SxmConnection.GetConnectionString(ref databaseName);
            connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();

            // 1. Initialize the metadata listener wrapper over the connection
            using (var extractor = new SqliteMetadataExtractor(connection))
            {
                // 2. Pass the untrusted, user-supplied SQL string to a standard command
                using (var command = new SqliteCommand(userSuppliedSql, connection))
                {
                    try
                    {
                        Console.WriteLine($"AnalyzeUserQuery: Preparing SQL statement: {userSuppliedSql}");
                        // 3. CRITICAL: This compiles the statement and triggers the authorizer hooks!
                        command.Prepare();

                        // 4. Access the safely extracted metadata straight from the object properties
                        embeddedSqlStatementDetails.SqlStatementType = extractor.DetectedStatementType;
                        embeddedSqlStatementDetails.TargetTableName = extractor.PrimaryTargetTable;
                    }
                    catch (SqliteException ex)
                    {
                        // If the user's SQL has a syntax error or mentions tables that 
                        // do not exist, SQLite will throw an exception during .Prepare().
                        Console.WriteLine($"SQL Compilation Error: {ex.Message}");
                    }
                }
            } // The 'using' block disposes the extractor and safely unhooks the native C pointer
        }
        finally
        {
            // Cleanup: Close and dispose the connection
            if (connection != null)
            {
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    connection.Close();
                }
                connection.Dispose();
            }
        }

        return embeddedSqlStatementDetails;
    }

    /// <summary>
    /// Contains metadata extracted from a SQL statement analysis.
    /// </summary>
    /// <remarks>
    /// This class holds the results of analyzing a SQL statement, including the type of operation
    /// (SELECT, INSERT, UPDATE, DELETE) and the primary table being operated on.
    /// </remarks>
    internal class SqlStatementDetails
    {
        internal string TargetTableName { get; set; } = string.Empty;
        internal SqlStatementType SqlStatementType { get; set; } = SqlStatementType.Unknown;
    }
}
