using System.Collections;
using System.Collections.Generic;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class DbItem
	{
        public string getSqlStatementName { get => sqlStatementName; }
        private string sqlStatementName;
        public List<object> getParameterValuesList { get => parameterValuesList; }
        private List<object> parameterValuesList;

        DbItem(string sqlStatementName, Dictionary<string, object> parameterValues)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = new List<object>();
            this.parameterValuesList.Add(parameterValues);
        }
        DbItem(string sqlStatementName, object[,] parameterValues)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = new List<object>();
            this.parameterValuesList.Add(parameterValues);
        }
        DbItem(string sqlStatementName, Hashtable parameterValues)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = new List<object>();
            this.parameterValuesList.Add(parameterValues);
        }
        DbItem(string sqlStatementName, List<object> parameterValues)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = parameterValues;
        }
    }

    public class DbOperationResponse
	{

	}

    public class SxmHelpers
    {
        private SxmHelpers()
        {
        }

        public static void populateRecordObject<T>(Dictionary<string, object> dbRow, ref T userObject) where T : class
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
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' {1} to the provided object property '{2}' {3}", key, dbRow[key]?.GetType().ToString(), key, userObject?.GetType()?.GetProperty(key)?.PropertyType.ToString()));
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
						switch(getDbOperationType(dbItem.getSqlStatementName))
						{
							case DbOperationTypes.insert:
                                InsertResponse ir = sxmTransaction.executeInsert(dbItem.getSqlStatementName, dbItem.getParameterValuesList);
                                break;

                            case DbOperationTypes.select:
                                sxmTransaction.executeQuery(dbItem.getSqlStatementName, dbItem.getParameterValuesList);
                                List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                                break;

                            case DbOperationTypes.update:
                                sxmTransaction.executeUpdate(dbItem.getSqlStatementName, dbItem.getParameterValuesList);
                                break;

                            case DbOperationTypes.delete:
                                sxmTransaction.executeDelete(dbItem.getSqlStatementName, dbItem.getParameterValuesList);
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

		private static DbOperationTypes getDbOperationType(string? action)
		{
            if(action == null)
                return DbOperationTypes.unknown;

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
