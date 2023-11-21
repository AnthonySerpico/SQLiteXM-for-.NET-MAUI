using System.Collections;
using System.Collections.Generic;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class DbItem
	{
		public string? sqlStatementName;
		public List<object>? parameterValues;
    }

	public class DbOperationResponse
	{

	}

    public class SxmHelpers
    {
        private SxmHelpers()
        {
        }

        public static void populateTable<T>(Dictionary<string, object> dbRow, ref T userObject) where T : class
        {
            ICollection ic = dbRow.Keys;
            foreach (string key in ic)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    userObject?.GetType().GetProperty(key)?.SetValue(userObject, dbRow[key]);
                }
                catch (System.ArgumentException)
                {
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' {1} to the provided table property '{2}' {3}", key, dbRow[key]?.GetType().ToString(), key, userObject?.GetType()?.GetProperty(key)?.PropertyType.ToString()));
                }
            }
        }

        public static async Task<List<DbOperationResponse>> performDbOperations(List<DbItem> dbItemList, string? dbName = default)
		{
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
					foreach (DbItem dbItem in dbItemList)
                    {
						switch(getDbOperationType(dbItem.sqlStatementName))
						{
							case DbOperationTypes.insert:
                                InsertResponse ir = sxmTransaction.executeInsert(dbItem.sqlStatementName, dbItem.parameterValues);
                                break;

                            case DbOperationTypes.select:
                                sxmTransaction.executeQuery(dbItem.sqlStatementName, dbItem.parameterValues);
                                List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                                break;

                            case DbOperationTypes.update:
                                sxmTransaction.executeUpdate(dbItem.sqlStatementName, dbItem.parameterValues);
                                break;

                            case DbOperationTypes.delete:
                                sxmTransaction.executeDelete(dbItem.sqlStatementName, dbItem.parameterValues);
                                break;

							default: break;
                        }
                    }

					sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(dbOperationResponseList);
        }

		private static DbOperationTypes getDbOperationType(string action)
		{
            if (SqlStatements.selectStatements[action] != default)
                return DbOperationTypes.select;
            
			if (SqlStatements.insertStatements[action] != default)
				return DbOperationTypes.insert;

            if (SqlStatements.updateStatements[action] != default)
                return DbOperationTypes.update;

            if (SqlStatements.deleteStatements[action] != default)
                return DbOperationTypes.delete;

            return DbOperationTypes.unknown;
        }

        public static Tuple<string, object> createNP(string columnName, object columnValue)
        {
            return Tuple.Create<string, object>(columnName, columnValue);
        }

        public static async Task<InsertResponse> performInsert(string sqlStatementName, Dictionary<string, object> parameterValues, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<InsertResponse> performInsert(string sqlStatementName, object[,] parameterValues, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<InsertResponse> performInsert(string sqlStatementName, Hashtable parameterValues, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<InsertResponse> performInsert(string sqlStatementName, List<object> parameterValues, string? dbName = default)
		{
			InsertResponse ir;

            try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
				{
                    ir = sxmTransaction.executeInsert(sqlStatementName, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				throw;
			}

			return await Task.FromResult(ir);
        }

        public static async Task performDelete(string sqlStatementName, Dictionary<string, object> parameterValues, string? dbName = default)
        {
            await performDelete(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performDelete(string sqlStatementName, object[,] parameterValues, string? dbName = default)
        {
            await performDelete(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performDelete(string sqlStatementName, Hashtable parameterValues, string? dbName = default)
        {
            await performDelete(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performDelete(string sqlStatementName, List<object> parameterValues, string? dbName = default)
		{
			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
				{
					sxmTransaction.executeDelete(sqlStatementName, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				throw;
			}

			await Task.CompletedTask;
		}

        public static async Task performUpdate(string sqlStatementName, Dictionary<string, object> parameterValues, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performUpdate(string sqlStatementName, object[,] parameterValues, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performUpdate(string sqlStatementName, Hashtable parameterValues, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task performUpdate(string sqlStatementName, List<object> parameterValues, string? dbName = default)
		{
			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
				{
					sxmTransaction.executeUpdate(sqlStatementName, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				throw;
			}

			await Task.CompletedTask;
		}

        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, Dictionary<string, object> parameterValues, string? dbName = default)
        {
            return await performSelect(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, object[,] parameterValues, string? dbName = default)
        {
            return await performSelect(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, Hashtable parameterValues, string? dbName = default)
        {
            return await performSelect(sqlStatementName, new List<object>(1) { parameterValues }, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, List<object> parameterValues, string? dbName = default)
		{
			List<Dictionary<string, object?>> selectedRows;

			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
				{
					sxmTransaction.executeQuery(sqlStatementName, parameterValues);
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>> ();
				}
			}
			catch (System.Exception)
			{
				throw;
			}

			return await Task.FromResult(selectedRows);
		}
	}
}
