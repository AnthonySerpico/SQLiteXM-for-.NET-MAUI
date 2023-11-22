using System.Collections;
using System.Collections.Specialized;
using System.Text.Json;

namespace SQLiteXM
{
    public class SxmSqlStatements : SqlStatements
    {
        public double versionNumber { get; set; } = 0.0;

        public new Hashtable? alterStatements { get; set; }
        public  new Hashtable? indexStatements { get; set; }
        public  new Hashtable insertStatements { get; set; }
        public  new Hashtable? tableCreateStatements { get; set; }
        public  new NameValueCollection selectStatements { get; set; }
        public  new NameValueCollection updateStatements { get; set; }
        public  new NameValueCollection deleteStatements { get; set; }

        private SxmSqlStatements() { }

        public static string generateJson()
        {
            SxmSqlStatements sxmSqlStatements = new SxmSqlStatements();

            sxmSqlStatements.alterStatements = SqlStatements.alterStatements;
            sxmSqlStatements.indexStatements = SqlStatements.indexStatements;
            sxmSqlStatements.insertStatements = SqlStatements.insertStatements;
            sxmSqlStatements.tableCreateStatements = SqlStatements.tableCreateStatements;
            sxmSqlStatements.selectStatements = SqlStatements.selectStatements;
            sxmSqlStatements.updateStatements = SqlStatements.updateStatements;
            sxmSqlStatements.deleteStatements = SqlStatements.deleteStatements;
            sxmSqlStatements.versionNumber = ProcessSQLStatements.getSqlStatementsVersionNumber;

            addInsertDefinition("insertNewUser", "userTable", "INSERT INTO userTable (fname, lname, recordingNumber, recordingLength, memberId, activatedDateTicks) VALUES(@p0, @p1, @p2, @p3)");
            string strJson = JsonSerializer.Serialize<SxmSqlStatements>(sxmSqlStatements);
            return strJson;

        }

        public static void setVersionNumber(double version)
        {
            ProcessSQLStatements.setSqlStatementsVersionNumber = version;
        }

        // Example: addTableDefinition( "testDatabase.userTable", "CREATE TABLE userTable (id INTEGER PRIMARY KEY AUTOINCREMENT, fname TEXT, lname TEXT, recordingNumber INTEGER, recordingLength INTEGER, memberId INTEGER, activatedDateTicks INTEGER, isRegistered BOOL)" );
        // Example: addTableDefinition( "testDatabase.historyTable", "DROP TABLE historyTable");

        // Example: addIndexDefinition( "testDatabase.userTable", "nameIDX", "CREATE INDEX nameIDX ON testDatabase(lname, fname)" );
        // Example: addIndexDefinition( "testDatabase.userTable", "lnameIDX", "DROP INDEX lnameIDX" );

        // Example: addInsertDefinition("insertNewUser", "userTable", "INSERT INTO userTable (fname, lname, recordingNumber, recordingLength, memberId, activatedDateTicks) VALUES(@p0, @p1, @p2, @p3)" )

        // Example: addSelectDefinition( "getUser", "SELECT * FROM userTable WHERE memberId = @p0 LIMIT 50" );

        // Example: addUpdateDefinition( "updateUser", "UPDATE userTable SET recordingLength=@p0, WHERE memberId=@p1" )

        // Example: addDeleteDefiniton( "deleteUser", "DELETE FROM userTable WHERE memberId = @p0" );

        // Example: addAlterDefinition( "testDatabase.userTable", "isRegistered", "ALTER TABLE userTable ADD isRegistered BOOL" );
    }
}

