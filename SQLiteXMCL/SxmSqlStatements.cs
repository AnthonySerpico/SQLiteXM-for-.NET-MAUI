using System;
using System.Collections;
using System.Text.Json;

namespace SQLiteXM
{
    public class SxmSqlStatements
    {
        private SxmSqlStatements() 
        {
            //string strJson = JsonSerializer.Serialize<SqlStatements>(SqlStatements);
            //Console.WriteLine(strJson);
        }

        public static void setVersionNumber(double version)
        {
            ProcessSQLStatements.setSqlStatementsVersionNumber = version;
        }

        // Example: addTableDefinition( "testDatabase.userTable", "CREATE TABLE userTable (id INTEGER PRIMARY KEY AUTOINCREMENT, fname TEXT, lname TEXT, recordingNumber INTEGER, recordingLength INTEGER, memberId INTEGER, activatedDateTicks INTEGER, isRegistered BOOL)" );
        // Example: addTableDefinition( "testDatabase.historyTable", "DROP TABLE historyTable");
        public static void addTableDefinition(string tableName, string tableSQL)
        {
            SqlStatements.addTableDefinition(tableName, tableSQL, Defines.NO_CLOUD_SYNCH);
        }

        // Example: addIndexDefinition( "testDatabase.userTable", "nameIDX", "CREATE INDEX nameIDX ON testDatabase(lname, fname)" );
        // Example: addIndexDefinition( "testDatabase.userTable", "lnameIDX", "DROP INDEX lnameIDX" );
        public static void addIndexDefinition(string dbAndTableName, string indexName, string sqlStatement)
        {
            SqlStatements.addIndexDefinition(dbAndTableName, indexName, sqlStatement);
        }

        // Example: addInsertDefinition("insertNewUser", "userTable", "INSERT INTO userTable (fname, lname, recordingNumber, recordingLength, memberId, activatedDateTicks) VALUES(@p0, @p1, @p2, @p3)" )
        public static void addInsertDefinition(string insertName, string tableName, string insertSQL)
        {
            SqlStatements.addInsertDefinition(insertName, tableName, insertSQL);
        }

        // Example: addSelectDefinition( "getUser", "SELECT * FROM userTable WHERE memberId = @p0 LIMIT 50" );
        public static void addSelectDefinition(string selectName, string selectSQL)
        {
            SqlStatements.addSelectDefinition(selectName, selectSQL);
        }

        // Example: addUpdateDefinition( "updateUser", "UPDATE userTable SET recordingLength=@p0, WHERE memberId=@p1" )
        public static void addUpdateDefinition(string updateName, string updateSQL)
        {
            SqlStatements.addUpdateDefinition(updateName, updateSQL);
        }

        // Example: addDeleteDefiniton( "deleteUser", "DELETE FROM userTable WHERE memberId = @p0" );
        public static void addDeleteDefinition(string deleteName, string deleteSQL)
        {
            SqlStatements.addDeleteDefinition(deleteName, deleteSQL);
        }

        // Example: addAlterDefinition( "testDatabase.userTable", "isRegistered", "ALTER TABLE userTable ADD isRegistered BOOL" );
        public static void addAlterDefinition(string dbAndTableName, string columnName, string sqlStatement)
        {
            SqlStatements.addAlterDefinition(dbAndTableName, columnName, sqlStatement);
        }
     }
}

