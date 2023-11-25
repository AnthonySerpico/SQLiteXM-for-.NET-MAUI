using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
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

        // Each TransactionItem represents a single SQL statement that will be executed.
        public void AddSqlStatement(string sqlStatementName, Dictionary<string, object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, sqlStatementParameters));
        }
        public void AddSqlStatement(string sqlStatementName, List<object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, sqlStatementParameters));
        }
    }

    public class TransactionItem
    {
        public string SqlStatementName { get => sqlStatementName; }
        private string sqlStatementName;
        public List<object> SqlStatementParameters { get => sqlStatementParameters; }
        private List<object> sqlStatementParameters;

        internal TransactionItem(string sqlStatementName, Dictionary<string, object> sqlStatementParameters)
        {
            this.sqlStatementName = sqlStatementName;
            this.sqlStatementParameters = new List<object>() { sqlStatementParameters };
        }
        internal TransactionItem(string sqlStatementName, List<object> sqlStatementParameters)
        {
            this.sqlStatementName = sqlStatementName;
            this.sqlStatementParameters = sqlStatementParameters;
        }
    }

    public class DbOperationResponse
    {

    }

    public class SxmHelpers
    {
        private SxmHelpers() { }

        public static async Task<List<DbOperationResponse>> runTransactionObject(TransactionObject transactionObject)
        {
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(transactionObject.DbName))
                {
                    foreach (TransactionItem transactionItem in transactionObject.TransactionItems)
                    {
                        switch (GetDatabaseStatementType(transactionItem.SqlStatementName))
                        {
                            case DatabaseStatementType.select:
                                sxmTransaction.executeQuery(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                                break;

                            case DatabaseStatementType.insert:
                                InsertResponse ir = sxmTransaction.executeInsert(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                break;

                            case DatabaseStatementType.update:
                                sxmTransaction.executeUpdate(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                break;

                            case DatabaseStatementType.delete:
                                sxmTransaction.executeDelete(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
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

        public static async Task<DbOperationResponse> runSqlStatement(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default(string))
        {
            return await runSqlStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public static async Task<DbOperationResponse> runSqlStatement(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default(string))
        {
            DbOperationResponse dbOperationResponse = new DbOperationResponse();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    switch (GetDatabaseStatementType(sqlStatementName))
                    {
                        case DatabaseStatementType.select:
                            await performSelect(sqlStatementName, sqlStatementParameters, dbName);
                            List<Dictionary<string, object?>> selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                            break;

                        case DatabaseStatementType.insert:
                            InsertResponse ir = await performInsert(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case DatabaseStatementType.update:
                            await performUpdate(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case DatabaseStatementType.delete:
                            await performDelete(sqlStatementName, sqlStatementParameters, dbName);
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

            return await Task.FromResult(dbOperationResponse);
        }

        private static DatabaseStatementType GetDatabaseStatementType(string? sqlStatementName)
        {
            if (sqlStatementName == null)
                throw new ArgumentException("A sql statement cannot be null.");

            if (SqlStatements.selectStatements[sqlStatementName] != default)
                return DatabaseStatementType.select;

            if (SqlStatements.insertStatements[sqlStatementName] != default)
                return DatabaseStatementType.insert;

            if (SqlStatements.updateStatements[sqlStatementName] != default)
                return DatabaseStatementType.update;

            if (SqlStatements.deleteStatements[sqlStatementName] != default)
                return DatabaseStatementType.delete;

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName));
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
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", key, dbRow[key]?.GetType().ToString(), key, userObject?.GetType()?.GetProperty(key)?.PropertyType.ToString()));
                }
            }
        }

    }
}
