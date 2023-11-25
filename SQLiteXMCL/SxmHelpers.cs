using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    public class TransactionItem
    {
        public string SqlStatementName { get => sqlStatementName; }
        private string sqlStatementName;
        public List<object> ParameterValuesList { get => parameterValuesList; }
        private List<object> parameterValuesList;

        internal TransactionItem(string sqlStatementName, Dictionary<string, object> parameterDictionary)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = new List<object>() { parameterDictionary };
        }
        internal TransactionItem(string sqlStatementName, List<object> parameterList)
        {
            this.sqlStatementName = sqlStatementName;
            this.parameterValuesList = parameterList;
        }
    }

    public class TransactionObject
    {
        private List<object> transactionItems = new List<object>();
        public List<object> TransactionItems { get => transactionItems; }

        private string? dbName;
        public string? DbName { get => dbName; }

        public TransactionObject(string? dbName = default(string))
        {
            this.dbName = dbName;
        }

        public void AddTransactionItem(string sqlStatementName, Dictionary<string, object> parameterDictionary)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, parameterDictionary));
        }
        public void AddTransactionItem(string sqlStatementName, List<object> parameterList)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, parameterList));
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

        public static async Task<List<DbOperationResponse>> runTransactionObject(TransactionObject transactionObject)
        {
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(transactionObject.DbName))
                {
                    foreach (TransactionItem transactionItem in transactionObject.TransactionItems)
                    {
                        switch (getDbOperationType(transactionItem.SqlStatementName))
                        {
                            case DbOperationTypes.insert:
                                InsertResponse ir = sxmTransaction.executeInsert(transactionItem.SqlStatementName, transactionItem.ParameterValuesList);
                                break;

                            case DbOperationTypes.select:
                                sxmTransaction.executeQuery(transactionItem.SqlStatementName, transactionItem.ParameterValuesList);
                                List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                                break;

                            case DbOperationTypes.update:
                                sxmTransaction.executeUpdate(transactionItem.SqlStatementName, transactionItem.ParameterValuesList);
                                break;

                            case DbOperationTypes.delete:
                                sxmTransaction.executeDelete(transactionItem.SqlStatementName, transactionItem.ParameterValuesList);
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

        public static async Task<List<DbOperationResponse>> runSqlStatement(string sqlStatementName, Dictionary<string, object> statementParameters, string? dbName = default(string))
        {
            return await runSqlStatement(sqlStatementName, new List<object>(1) { statementParameters }, dbName);
        }
        public static async Task<List<DbOperationResponse>> runSqlStatement(string sqlStatementName, List<object> statementParameters, string? dbName = default(string))
        {
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    switch (getDbOperationType(sqlStatementName))
                    {
                        case DbOperationTypes.insert:
                            InsertResponse ir = await performInsert(sqlStatementName, statementParameters, dbName);
                            break;

                        case DbOperationTypes.select:
                            await performSelect(sqlStatementName, statementParameters, dbName);
                            List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                            break;

                        case DbOperationTypes.update:
                            await performUpdate(sqlStatementName, statementParameters, dbName);
                            break;

                        case DbOperationTypes.delete:
                            await performDelete(sqlStatementName, statementParameters, dbName);
                            break;

                        default: break;
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
            if (action == null)
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
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows);
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
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' {1} to the provided object property type '{2}' {3}", key, dbRow[key]?.GetType().ToString(), key, userObject?.GetType()?.GetProperty(key)?.PropertyType.ToString()));
                }
            }
        }

    }
}
