using Microsoft.Maui.Controls;
using System;
using System.Collections;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class DbItem
	{
		public string action;
		public ArrayList parameterValues;
    }

	public class DbOperationResponse
	{

	}

    public class DBAccessCore
    {
        private DBAccessCore()
        {
        }

        public static async Task<List<DbOperationResponse>> performDbOperations(List<DbItem> dbItemList)
		{
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction())
                {
					foreach (DbItem dbItem in dbItemList)
                    {
						switch(getDbOperationType(dbItem.action))
						{
							case DbOperationTypes.insert:
                                InsertResponse ir = sxmTransaction.executeInsert(dbItem.action, dbItem.parameterValues);
                                break;

                            case DbOperationTypes.select:
                                sxmTransaction.executeQuery(dbItem.action, dbItem.parameterValues);
                                List<Hashtable> selectedRows = sxmTransaction.getAllRows();
                                break;

                            case DbOperationTypes.update:
                                sxmTransaction.executeUpdate(dbItem.action, dbItem.parameterValues);
                                break;

                            case DbOperationTypes.delete:
                                sxmTransaction.executeDelete(dbItem.action, dbItem.parameterValues);
                                break;

							default: break;
                        }
                    }

					sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                // It is assumed that processing on the local database wil be successful. If not, all bets are off.
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

        public static async Task<InsertResponse> performInsert(string action, ArrayList parameterValues)
		{
			InsertResponse ir;

            try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction())
				{
                    ir = sxmTransaction.executeInsert(action, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				// It is assumed that processing on the local database wil be successful. If not, all bets are off.
				throw;
			}

			return await Task.FromResult(ir);
        }
 
        public static async Task performDelete(string action, ArrayList parameterValues)
		{
			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction())
				{
					sxmTransaction.executeDelete(action, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				// It is assumed that processing on the local database will be successful. If not, all bets are off.
				throw;
			}

			await Task.CompletedTask;
		}

		public static async Task performUpdate(string action, ArrayList parameterValues)
		{
			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction())
				{
					sxmTransaction.executeUpdate(action, parameterValues);
					sxmTransaction.commitTransaction();
				}
			}
			catch (System.Exception)
			{
				// It is assumed that processing on the local database wil be successful. If not, all bets are off.
				throw;
			}

			await Task.CompletedTask;
		}

		public static async Task<List<Hashtable>> performSelect(string action, ArrayList parameterValues)
		{
			List<Hashtable> selectedRows;

			try
			{
				using (SxmTransaction sxmTransaction = new SxmTransaction())
				{
					sxmTransaction.executeQuery(action, parameterValues);
					selectedRows = sxmTransaction.getAllRows();
				}
			}
			catch (System.Exception)
			{
				// It is assumed that processing on the local database wil be successful. If not, all bets are off.
				throw;
			}

			return await Task.FromResult(selectedRows);
		}
	}
}
