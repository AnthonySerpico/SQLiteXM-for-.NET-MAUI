using Microsoft.Data.Sqlite;
using SQLiteXM;
using System;
using static SQLiteXM.SxmDefines;

public class SxmQueryProcessor
{
    internal static void AnalyzeUserQuery(string userSuppliedSql, ref SqlStatementType sqlStatementType, ref string  targetTableName, string? databaseName = null)
    {
        Microsoft.Data.Sqlite.SqliteConnection? connection = null;
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
                        // 3. CRITICAL: This compiles the statement and triggers the authorizer hooks!
                        command.Prepare();

                        // 4. Access the safely extracted metadata straight from the object properties
                        sqlStatementType = extractor.DetectedStatementType;
                        targetTableName = extractor.PrimaryTargetTable;
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
    }
}
