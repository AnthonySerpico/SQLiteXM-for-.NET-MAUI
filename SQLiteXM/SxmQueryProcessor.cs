using Microsoft.Data.Sqlite;
using SQLiteXM;
using System;
using static SQLiteXM.SxmDefines;

public class SxmQueryProcessor
{
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

    public class SqlStatementDetails
    {
        internal string TargetTableName { get; set; } = string.Empty;
        internal SqlStatementType SqlStatementType { get; set; } = SqlStatementType.Unknown;
    }
}
