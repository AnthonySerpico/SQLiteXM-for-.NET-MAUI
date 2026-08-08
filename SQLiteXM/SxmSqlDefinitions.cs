namespace SQLiteXM
{
    /// <summary>
    /// Represents a SQL table definition (CREATE TABLE statement) and an associated
    /// cloud synchronization indicator.
    /// </summary>
    internal class TableDefinition
    {
        private string _tableSQL;
        /// <summary>
        /// Gets the SQL used to create the table.
        /// </summary>
        public string TableSQL
        {
            get { return _tableSQL; }
        }

        private int _cloudSynch;
        /// <summary>
        /// Gets the cloud synchronization indicator for this table.
        /// The interpretation of the integer value is determined by the caller.
        /// </summary>
        public int CloudSynch
        {
            get { return _cloudSynch; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableDefinition"/> class.
        /// </summary>
        /// <param name="tableSQL">The CREATE TABLE SQL statement.</param>
        /// <param name="cloudSynch">An integer indicating cloud synchronization behavior.</param>
        internal TableDefinition(string tableSQL, int cloudSynch)
        {
            this._tableSQL = tableSQL;
            this._cloudSynch = cloudSynch;
        }
    }

    /// <summary>
    /// Represents a SELECT statement and the table it targets.
    /// </summary>
    internal class SelectDefinition
    {
        private string _tableName;
        /// <summary>
        /// Gets the name of the table targeted by the select statement.
        /// </summary>
        public string TableName
        {
            get { return _tableName; }
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
            this._tableName = tableName;
            this.selectSQL = selectSQL;
        }
    }

    /// <summary>
    /// Represents an INSERT statement for a specific table.
    /// </summary>
    internal class InsertDefinition
    {
        private string _tableName;
        /// <summary>
        /// Gets the name of the table targeted by the insert statement.
        /// </summary>
        public string TableName
        {
            get { return _tableName; }
        }
        private string _insertSQL;
        /// <summary>
        /// Gets the INSERT SQL statement.
        /// </summary>
        public string InsertSQL
        {
            get { return _insertSQL; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InsertDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The target table name.</param>
        /// <param name="insertSQL">The INSERT SQL statement.</param>
        internal InsertDefinition(string tableName, string insertSQL)
        {
            this._tableName = tableName;
            this._insertSQL = insertSQL;
        }
    }

    /// <summary>
    /// Represents an UPDATE statement for a specific table.
    /// </summary>
    internal class UpdateDefinition
    {
        private string _tableName;
        /// <summary>
        /// Gets the name of the table targeted by the update statement.
        /// </summary>
        public string TableName
        {
            get { return _tableName; }
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
            this._tableName = tableName;
            this.updateSQL = updateSQL;
        }
    }

    /// <summary>
    /// Represents a DELETE statement for a specific table.
    /// </summary>
    internal class DeleteDefinition
    {
        private string _tableName;
        /// <summary>
        /// Gets the name of the table targeted by the delete statement.
        /// </summary>
        public string TableName
        {
            get { return _tableName; }
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
            this._tableName = tableName;
            this.deleteSQL = deleteSQL;
        }
    }

    /// <summary>
    /// Represents an index definition, including the CREATE INDEX SQL.
    /// </summary>
    internal class IndexDefinition
    {
        private string _indexName;
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string IndexName
        {
            get { return _indexName; }
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
            this._indexName = indexName;
            this.indexSQL = indexSQL;
        }
    }

    /// <summary>
    /// Represents a trigger definition, including the CREATE TRIGGER SQL.
    /// </summary>
    internal class TriggerDefinition
    {
        /// <summary>
        /// Gets the name of the table associated with the trigger.
        /// </summary>
        public string TableName
        {
            get { return tableName; }
        }
        private string tableName;

        /// <summary>
        /// Gets the CREATE TRIGGER SQL statement.
        /// </summary>
        public string TriggerSQL
        {
            get { return triggerSQL; }
        }
        private string triggerSQL;

        /// <summary>
        /// Initializes a new instance of the <see cref="TriggerDefinition"/> class.
        /// </summary>
        /// <param name="tableName">The name of the source table for the trigger.</param>
        /// <param name="triggerSQL">The CREATE TRIGGER SQL statement.</param>
        internal TriggerDefinition(string tableName, string triggerSQL)
        {
            this.tableName = tableName;
            this.triggerSQL = triggerSQL;
        }
    }

    /// <summary>
    /// Represents an ALTER operation for a column and its associated SQL.
    /// </summary>
    internal class AlterDefinition
    {
        private string _columnName;
        /// <summary>
        /// Gets the name of the column being altered.
        /// </summary>
        public string ColumnName
        {
            get { return _columnName; }
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
            this._columnName = columnName;
            this.alterSQL = alterSQL;
        }
    }
}