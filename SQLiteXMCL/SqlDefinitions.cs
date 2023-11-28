namespace SQLiteXM
{
    public class TableDefinition
    {
        private string tableSQL;
        public string TableSQL
        {
            get { return tableSQL; }
        }
        private int cloudSynch;
        public int CloudSynch
        {
            get { return cloudSynch; }
        }

        internal TableDefinition(string tableSQL, int cloudSynch)
        {
            this.tableSQL = tableSQL;
            this.cloudSynch = cloudSynch;
        }
    }

    public class SelectDefinition
    {
        private string tableName;
        public string TableName
        {
            get { return tableName; }
        }
        private string selectSQL;
        public string SelectSQL
        {
            get { return selectSQL; }
        }

        internal SelectDefinition(string tableName, string selectSQL)
        {
            this.tableName = tableName;
            this.selectSQL = selectSQL;
        }
    }

    public class InsertDefinition
	{
		private string tableName;
		public string TableName
		{
			get { return tableName; }
		}
		private string insertSQL;
		public string InsertSQL
		{
			get { return insertSQL; }
		}

		internal InsertDefinition (string tableName, string insertSQL)
		{
			this.tableName = tableName;
			this.insertSQL = insertSQL;
		}
    }

    public class UpdateDefinition
    {
        private string tableName;
        public string TableName
        {
            get { return tableName; }
        }
        private string updateSQL;
        public string UpdateSQL
        {
            get { return updateSQL; }
        }

        internal UpdateDefinition(string tableName, string updateSQL)
        {
            this.tableName = tableName;
            this.updateSQL = updateSQL;
        }
    }

    public class DeleteDefinition
    {
        private string tableName;
        public string TableName
        {
            get { return tableName; }
        }
        private string deleteSQL;
        public string DeleteSQL
        {
            get { return deleteSQL; }
        }

        internal DeleteDefinition(string tableName, string deleteSQL)
        {
            this.tableName = tableName;
            this.deleteSQL = deleteSQL;
        }
    }

    public class IndexDefinition
    {
        private string indexName;
        public string IndexName
        {
            get { return indexName; }
        }
        private string indexSQL;
        public string IndexSQL
        {
            get { return indexSQL; }
        }

        internal IndexDefinition(string indexName, string indexSQL)
        {
            this.indexName = indexName;
            this.indexSQL = indexSQL;
        }
    }

    public class AlterDefinition
    {
        private string columnName;
        public string ColumnName
        {
            get { return columnName; }
        }
        private string alterSQL;
        public string AlterSQL
        {
            get { return alterSQL; }
        }

        internal AlterDefinition(string columnName, string alterSQL)
        {
            this.columnName = columnName;
            this.alterSQL = alterSQL;
        }
    }
}

