using System;
using System.Collections;
using System.Collections.Specialized;
using System.Text.Json;

namespace SQLiteXM
{
    public class SxmSqlStatements : SxmSqlStatements
    {
        public double version { get; set; } = 0.0;
        public string databaseName { get; set; }

        public  new Hashtable? tableCreateStatements { get; set; }
        public  new Hashtable? alterStatements { get; set; }
        public  new Hashtable? indexStatements { get; set; }
        public  new Dictionary<string, InsertDefinition> insertStatements { get; set; }
        public  new Dictionary<string, SelectDefinition> selectStatements { get; set; }
        public  new Dictionary<string, UpdateDefinition> updateStatements { get; set; }
        public  new Dictionary<string, DeleteDefinition> deleteStatements { get; set; }

        public SxmSqlStatements( string databaseName, double version ) 
        {
            this.databaseName = databaseName;
            this.version = setVersionNumber(version);
        }

        public string generateJson()
        {
            alterStatements = SxmSqlStatements.alterStatements;
            indexStatements = SxmSqlStatements.indexStatements;
            insertStatements = SxmSqlStatements.insertStatements;
            tableCreateStatements = SxmSqlStatements.tableCreateStatements;
            selectStatements = SxmSqlStatements.selectStatements;
            updateStatements = SxmSqlStatements.updateStatements;
            deleteStatements = SxmSqlStatements.deleteStatements;

            string strJson = JsonSerializer.Serialize<SxmSqlStatements>(this);
            return strJson;
        }

        new public void addInsertDefinition(string insertName, string tableName, string insertSQL)
        {
            SxmSqlStatements.addInsertDefinition(insertName, tableName, insertSQL);
        }

        new public void addSelectDefinition(string selectName, string tableName, string insertSQL)
        {
            SxmSqlStatements.addSelectDefinition(selectName, tableName, insertSQL);
        }

        new public void addUpdateDefinition(string updateName, string tableName, string updateSQL)
        {
            SxmSqlStatements.addUpdateDefinition(updateName, tableName, updateSQL);
        }

        new public void addDeleteDefinition(string deleteName, string tableName, string deleteSQL)
        {
            SxmSqlStatements.addDeleteDefinition(deleteName, tableName, deleteSQL);
        }

        new public void addIndexDefinition(string tableName, string indexName, string sqlStatement)
        {
            SxmSqlStatements.addIndexDefinition(databaseName + "." + tableName, indexName, sqlStatement);
        }

        new internal void addAlterDefinition(string tableName, string columnName, string sqlStatement)
        {
            SxmSqlStatements.addAlterDefinition(databaseName + "." + tableName, columnName, sqlStatement);
        }

        new public void addTableDefinition(string tableName, string tableSQL)
        {
            SxmSqlStatements.addTableDefinition(databaseName + "." + tableName, tableSQL);
        }

        private double setVersionNumber(double version)
        {
            if (version < 0)
                throw new SxmException(new ErrorMessage("improperlyFormattedVersionNumber", version));

            ProcessSQLStatements.setSqlStatementsVersionNumber = version;
            return version;
        }

        // Example: addTableDefinition( "testDatabase.userTable", "CREATE TABLE userTable (id INTEGER PRIMARY KEY AUTOINCREMENT, fname TEXT, lname TEXT, recordingNumber INTEGER, recordingLength INTEGER, memberId INTEGER, activatedDateTicks INTEGER, isRegistered BOOL)" );
        // Example: addTableDefinition( "testDatabase.historyTable", "DROP TABLE historyTable");

        // Example: addIndexDefinition( "testDatabase.userTable", "nameIDX", "CREATE INDEX nameIDX ON testDatabase(lname, fname)" );
        // Example: addIndexDefinition( "testDatabase.userTable", "lnameIDX", "DROP INDEX lnameIDX" );

        // Example: addInsertDefinition("insertNewUser", "userTable", "INSERT INTO userTable (fname, lname, recordingNumber, recordingLength, memberId, activatedDateTicks) VALUES(@p0, @p1, @p2, @p3)" );

        // Example: addSelectDefinition( "getUser", "SELECT * FROM userTable WHERE memberId = @p0 LIMIT 50" );

        // Example: addUpdateDefinition( "updateUser", "UPDATE userTable SET recordingLength=@p0, WHERE memberId=@p1" );

        // Example: addDeleteDefiniton( "deleteUser", "DELETE FROM userTable WHERE memberId = @p0" );

        // Example: addAlterDefinition( "testDatabase.userTable", "isRegistered", "ALTER TABLE userTable ADD isRegistered BOOL" );
    }
}

