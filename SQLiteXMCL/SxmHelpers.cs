using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using static CoreFoundation.DispatchSource;
using static SQLiteXM.Defines;

namespace SQLiteXM
{
    // A Transaction object represents a series of SQL statements that will be executes as a single transaction.
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

        // For adding name/value parameters. 
        public void AddSqlStatement(string sqlStatementName, Dictionary<string, object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, sqlStatementParameters));
        }
        // For adding positional parameters.
        public void AddSqlStatement(string sqlStatementName, List<object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem(sqlStatementName, sqlStatementParameters));
        }
    }

    // Each TransactionItem represents a single SQL statement that will be executed.
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

    // Each DbOperationResponse represents the results of a single SQL statement that was executed.
    public class DbOperationResponse
    {
        internal List<Dictionary<string, object?>>? recordData;
        internal SqlStatementType sqlStatementType;
        internal string? sqlStatementName;
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
                        DbOperationResponse responseObject = new DbOperationResponse();
                        switch (GetDatabaseStatementType(transactionItem.SqlStatementName))
                        {
                            case SqlStatementType.select:
                                sxmTransaction.executeQuery(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                responseObject.recordData = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                                responseObject.sqlStatementName = transactionItem.SqlStatementName;
                                responseObject.sqlStatementType = SqlStatementType.select;
                                dbOperationResponseList.Add(responseObject);
                                break;

                            case SqlStatementType.insert:
                                responseObject.recordData = new List<Dictionary<string, object?>>(1);
                                responseObject.recordData.Add(sxmTransaction.executeInsert(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters));
                                responseObject.sqlStatementName = transactionItem.SqlStatementName;
                                responseObject.sqlStatementType = SqlStatementType.insert;
                                dbOperationResponseList.Add(responseObject);
                                break;

                            case SqlStatementType.update:
                                sxmTransaction.executeUpdate(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                responseObject.sqlStatementName = transactionItem.SqlStatementName;
                                responseObject.sqlStatementType = SqlStatementType.update;
                                dbOperationResponseList.Add(responseObject);
                                break;

                            case SqlStatementType.delete:
                                sxmTransaction.executeDelete(transactionItem.SqlStatementName, transactionItem.SqlStatementParameters);
                                responseObject.sqlStatementName = transactionItem.SqlStatementName;
                                responseObject.sqlStatementType = SqlStatementType.delete;
                                dbOperationResponseList.Add(responseObject);
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
                        case SqlStatementType.select:
                            dbOperationResponse.recordData = await performSelect(sqlStatementName, sqlStatementParameters, dbName);
                            dbOperationResponse.sqlStatementType = SqlStatementType.select;
                            dbOperationResponse.sqlStatementName = sqlStatementName;
                            break;

                        case SqlStatementType.insert:
                            dbOperationResponse.recordData = new List<Dictionary<string, object?>>(1);
                            dbOperationResponse.recordData.Add(await performInsert(sqlStatementName, sqlStatementParameters, dbName));
                            dbOperationResponse.sqlStatementType = SqlStatementType.insert;
                            dbOperationResponse.sqlStatementName = sqlStatementName;
                            break;

                        case SqlStatementType.update:
                            await performUpdate(sqlStatementName, sqlStatementParameters, dbName);
                            dbOperationResponse.sqlStatementType = SqlStatementType.update;
                            dbOperationResponse.sqlStatementName = sqlStatementName;
                            break;

                        case SqlStatementType.delete:
                            await performDelete(sqlStatementName, sqlStatementParameters, dbName);
                            dbOperationResponse.sqlStatementType = SqlStatementType.delete;
                            dbOperationResponse.sqlStatementName = sqlStatementName;
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

        private static SqlStatementType GetDatabaseStatementType(string? sqlStatementName)
        {
            if (sqlStatementName == null)
                throw new ArgumentException("A sql statement cannot be null.");

            if (SqlStatements.selectStatements[sqlStatementName] != default)
                return SqlStatementType.select;

            if (SqlStatements.insertStatements[sqlStatementName] != default)
                return SqlStatementType.insert;

            if (SqlStatements.updateStatements[sqlStatementName] != default)
                return SqlStatementType.update;

            if (SqlStatements.deleteStatements[sqlStatementName] != default)
                return SqlStatementType.delete;

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName));
        }

        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default)
        {
            return await performSelect(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    sxmTransaction.executeQuery(sqlStatementName, sqlStatementParameters);
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows);
        }

        public static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default)
        {
            return await performInsert(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    ir = sxmTransaction.executeInsert(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir);
        }

        public static async Task performUpdate(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default)
        {
            await performUpdate(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public static async Task performUpdate(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    sxmTransaction.executeUpdate(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask;
        }

        public static async Task performDelete(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default)
        {
            await performDelete(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public static async Task performDelete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    sxmTransaction.executeDelete(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask;
        }

        public static void populateRecordObject<T>(Dictionary<string, object> databaseRows, ref T userObject) where T : class
        {
            ICollection ic = databaseRows.Keys;
            foreach (string key in ic)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    userObject?.GetType().GetProperty(key)?.SetValue(userObject, databaseRows[key]);
                }
                catch (System.ArgumentException)
                {
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", key, databaseRows[key]?.GetType().ToString(), key, userObject?.GetType()?.GetProperty(key)?.PropertyType.ToString()));
                }
            }
        }

        public static List<T> populateRecordObject<T>(List<Dictionary<string, object>> databaseRowsList) where T : class, new()
        {
            List<T> userObjectList = new List<T>();

            foreach (Dictionary<string, object> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                foreach (KeyValuePair<string, object> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
                {
                    T userObject = new T();
                    try
                    {
                        userObject.GetType().GetProperty(kvp.Key)?.SetValue(userObject, kvp.Value);
                        userObjectList.Add(userObject);
                    }
                    catch (System.ArgumentException)
                    {
                        throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", (kvp.Key, kvp.Value?.GetType().ToString(), kvp.Key, userObject.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString())));
                    }
                }
            }

            return userObjectList;
        }
    }
}
