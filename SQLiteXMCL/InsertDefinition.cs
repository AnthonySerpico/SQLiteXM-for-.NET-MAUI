namespace SQLiteXM
{
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

    public class SelectDefinition
    {
        private string? tableName;
        public string TableName
        {
            get { return tableName; }
        }
        private string selectSQL;
        public string SelectSQL
        {
            get { return selectSQL; }
        }

        internal SelectDefinition(string? tableName, string selectSQL)
        {
            this.tableName = tableName;
            this.selectSQL = selectSQL;
        }
    }

    public class UpdateDefinition
    {
        private string updateSQL;
        public string UpdateSQL
        {
            get { return updateSQL; }
        }

        internal UpdateDefinition(string updateSQL)
        {
            this.updateSQL = updateSQL;
        }
    }
    public class DeleteDefinition
    {
        private string deleteSQL;
        public string DeleteSQL
        {
            get { return deleteSQL; }
        }

        internal DeleteDefinition(string deleteSQL)
        {
            this.deleteSQL = deleteSQL;
        }
    }

}

