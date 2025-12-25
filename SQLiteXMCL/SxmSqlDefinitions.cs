namespace SQLiteXM
{
    /// <summary>
    /// Represents a SQL table definition (CREATE TABLE statement) and an associated
    /// cloud synchronization indicator.
    /// </summary>
    public class TableDefinition
    {
        private string tableSQL;
        /// <summary>
        /// Gets the SQL used to create the table.
        /// </summary>
        public string TableSQL
        {
            get { return tableSQL; }
        }
        private int cloudSynch;
        /// <summary>
        /// Gets the cloud synchronization indicator for this table.
        /// The interpretation of the integer value is determined by the caller.
        /// </summary>
        public int CloudSynch
        {
            get { return cloudSynch; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableDefinition"/> class.
        /// </summary>
        /// <param name="tableSQL">The CREATE TABLE SQL statement.</param>
        /// <param name="cloudSynch">An integer indicating cloud synchronization behavior.</param>
        internal TableDefinition(string tableSQL, int cloudSynch)
        {
            this.tableSQL = tableSQL;
            this.cloudSynch = cloudSynch;
        }
    }

    /// <summary>
    /// Represents a SELECT statement and the table it targets.
    /// </summary>
    public class SelectDefinition
    {
        private string tableName;
        /// <summary>
        /// Gets the name of the table targeted by the select statement.
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }
        private string selectSQL;
        /// <summary>
        /// Gets the SELECT SQL statement.
        /// </summary>
        public string SelectSQL
        {
            get { return selectSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The target table name.</param>
        /// <param name="selectSQL">The SELECT SQL statement.</param>
        internal SelectDefinition(string tableName, string selectSQL)
        {
            this.tableName = tableName;
            this.selectSQL = selectSQL;
        }
    }

    /// <summary>
    /// Represents an INSERT statement for a specific table.
    /// </summary>
    public class InsertDefinition
    {
        private string tableName;
        /// <summary>
        /// Gets the name of the table targeted by the insert statement.
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }
        private string insertSQL;
        /// <summary>
        /// Gets the INSERT SQL statement.
        /// </summary>
        public string InsertSQL
        {
            get { return insertSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InsertDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The target table name.</param>
        /// <param name="insertSQL">The INSERT SQL statement.</param>
        internal InsertDefinition(string tableName, string insertSQL)
        {
            this.tableName = tableName;
            this.insertSQL = insertSQL;
        }
    }

    /// <summary>
    /// Represents an UPDATE statement for a specific table.
    /// </summary>
    public class UpdateDefinition
    {
        private string tableName;
        /// <summary>
        /// Gets the name of the table targeted by the update statement.
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }
        private string updateSQL;
        /// <summary>
        /// Gets the UPDATE SQL statement.
        /// </summary>
        public string UpdateSQL
        {
            get { return updateSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The target table name.</param>
        /// <param name="updateSQL">The UPDATE SQL statement.</param>
        internal UpdateDefinition(string tableName, string updateSQL)
        {
            this.tableName = tableName;
            this.updateSQL = updateSQL;
        }
    }

    /// <summary>
    /// Represents a DELETE statement for a specific table.
    /// </summary>
    public class DeleteDefinition
    {
        private string tableName;
        /// <summary>
        /// Gets the name of the table targeted by the delete statement.
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }
        private string deleteSQL;
        /// <summary>
        /// Gets the DELETE SQL statement.
        /// </summary>
        public string DeleteSQL
        {
            get { return deleteSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The target table name.</param>
        /// <param name="deleteSQL">The DELETE SQL statement.</param>
        internal DeleteDefinition(string tableName, string deleteSQL)
        {
            this.tableName = tableName;
            this.deleteSQL = deleteSQL;
        }
    }

    /// <summary>
    /// Represents an index definition, including the CREATE INDEX SQL.
    /// </summary>
    public class IndexDefinition
    {
        private string indexName;
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string IndexName
        {
            get { return indexName; }
        }
        private string indexSQL;
        /// <summary>
        /// Gets the CREATE INDEX SQL statement.
        /// </summary>
        public string IndexSQL
        {
            get { return indexSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IndexDefinition"/> class.
        /// </summary>
        /// <param name="indexName">The name of the index.</param>
        /// <param name="indexSQL">The CREATE INDEX SQL statement.</param>
        internal IndexDefinition(string indexName, string indexSQL)
        {
            this.indexName = indexName;
            this.indexSQL = indexSQL;
        }
    }

    /// <summary>
    /// Represents a trigger definition, including the CREATE TRIGGER SQL.
    /// </summary>
    public class TriggerDefinition
    {
        private string triggerName;
        /// <summary>
        /// Gets the name of the trigger.
        /// </summary>
        public string TriggerName
        {
            get { return triggerName; }
        }
        private string triggerSQL;
        /// <summary>
        /// Gets the CREATE TRIGGER SQL statement.
        /// </summary>
        public string TriggerSQL
        {
            get { return triggerSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerDefinition"/> class.
        /// </summary>
        /// <param name="triggerName">The name of the trigger.</param>
        /// <param name="triggerSQL">The CREATE TRIGGER SQL statement.</param>
        internal TriggerDefinition(string triggerName, string triggerSQL)
        {
            this.triggerName = triggerName;
            this.triggerSQL = triggerSQL;
        }
    }

    /// <summary>
    /// Represents an ALTER operation for a column and its associated SQL.
    /// </summary>
    public class AlterDefinition
    {
        private string columnName;
        /// <summary>
        /// Gets the name of the column being altered.
        /// </summary>
        public string ColumnName
        {
            get { return columnName; }
        }
        private string alterSQL;
        /// <summary>
        /// Gets the ALTER SQL statement for the column.
        /// </summary>
        public string AlterSQL
        {
            get { return alterSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlterDefinition"/> class.
        /// </summary>
        /// <param name="columnName">The column to be altered.</param>
        /// <param name="alterSQL">The ALTER SQL statement.</param>
        internal AlterDefinition(string columnName, string alterSQL)
        {
            this.columnName = columnName;
            this.alterSQL = alterSQL;
        }
    }
}