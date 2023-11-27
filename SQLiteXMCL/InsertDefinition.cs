namespace SQLiteXM
{
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
}

