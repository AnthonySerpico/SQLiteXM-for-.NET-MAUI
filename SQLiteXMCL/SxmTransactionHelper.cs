using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        // For adding positional parameters.
        public void AddTransactionItem(string sqlStatementName, List<object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem<List<object>>(sqlStatementName, sqlStatementParameters));
        }
        // For adding name/value parameters. 
        public void AddTransactionItem(string sqlStatementName, Dictionary<string, object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem<Dictionary<string, object>>(sqlStatementName, sqlStatementParameters));
        }
        // For adding parameters from user supplied object.
        public void AddTransactionItem<T>(string sqlStatementName, T sqlStatementParameters) where T : class, new()
        {
            transactionItems.Add(new TransactionItem<object>(DbName!, sqlStatementName, sqlStatementParameters));
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, sqlStatementParameters);
            transactionItems.Add(new TransactionItem<Dictionary<string, object>>(sqlStatementName, selectParameterValues));
        }
    }

    // Each TransactionItem represents a single SQL statement that will be executed.
    public class TransactionItem<T> where T : class, new()
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
        internal TransactionItem(string dbName, string sqlStatementName, T userObjectParameters)
        {
            this.sqlStatementName = sqlStatementName;

            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, SxmHelpers.GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = SxmHelpers.loadParamaterValues<T>(columnNames, userObjectParameters);
            this.sqlStatementParameters = new List<object>() { selectParameterValues };
        }
    }

    // Each DbOperationResponse represents the results of a single SQL statement that was executed.
    public class DbOperationResponse
    {
        internal List<Dictionary<string, object?>>? recordData { get; set; }
        public List<Dictionary<string, object?>>? RecordData { get => recordData; }
        internal SqlStatementType sqlStatementType { get; set; }
        public SqlStatementType SqlStatementType { get => sqlStatementType; }
        internal string? sqlStatementName { get; set; }
        string? SqlStatementName { get => sqlStatementName; }
    }
    public class DbOperationResponse<T> where T : class
    {
        internal T? recordData { get; set; }
        public T? RecordData { get => recordData; }
        internal SqlStatementType sqlStatementType { get; set; }
        public SqlStatementType SqlStatementType { get => sqlStatementType; }
        internal string? sqlStatementName { get; set; }
        string? SqlStatementName { get => sqlStatementName; }
    }


    public class SxmTransactionHelper
    {
        //List<DbOperationResponse<List<user1>>> hhh = await SxmHelpers.runTransactionObject<user1>(new TransactionObject());
        public static async Task<List<DbOperationResponse<List<T>>>> runTransactionObject<T>(TransactionObject transactionObject) where T : class, new()
        {
            List<DbOperationResponse> defaultDbOperationResponseList = await runTransactionObject(transactionObject);
            List<DbOperationResponse<List<T>>> userDbOperationResponseList = new List<DbOperationResponse<List<T>>>();

            for (int indexer = defaultDbOperationResponseList.Count - 1; indexer >= 0; --indexer)
            {
                DbOperationResponse dbOperationResponse = defaultDbOperationResponseList[indexer];
                DbOperationResponse<List<T>> userDbOperationResponse = new DbOperationResponse<List<T>>();
                if (dbOperationResponse.recordData != default)
                    userDbOperationResponse.recordData = SxmHelpers.populateUserRecord<T>(dbOperationResponse.recordData);

                userDbOperationResponse.sqlStatementName = dbOperationResponse.sqlStatementName;
                userDbOperationResponse.sqlStatementType = dbOperationResponse.sqlStatementType;
                userDbOperationResponseList.Insert(0, userDbOperationResponse);

                defaultDbOperationResponseList.RemoveAt(indexer);
            }

            return userDbOperationResponseList;
        }

        public static async Task<List<DbOperationResponse>> runTransactionObject(TransactionObject transactionObject)
        {
            List<DbOperationResponse> dbOperationResponseList = new List<DbOperationResponse>();

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(transactionObject.DbName))
                {
                    foreach (Object transObject in transactionObject.TransactionItems)
                    {
                        DbOperationResponse responseObject = new DbOperationResponse();

                        if (transObject.GetType() == typeof(TransactionItem<List<object>>))
                        {
                            TransactionItem<List<object>> transactionItem = (TransactionItem<List<object>>)transObject;
                            switch (SxmHelpers.GetDatabaseStatementType(transactionItem.SqlStatementName))
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

                        if (transObject.GetType() == typeof(TransactionItem<Dictionary<string, object>>))
                        {
                            TransactionItem<Dictionary<string, object>> transactionItem = (TransactionItem<Dictionary<string, object>>)transObject;
                            switch (SxmHelpers.GetDatabaseStatementType(transactionItem.SqlStatementName))
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

        private static DbOperationResponse<List<T>> loadUserResponseObject<T>(DbOperationResponse dbOperationResponse) where T : class, new()
        {
            DbOperationResponse<List<T>> userDbOperationResponse = new DbOperationResponse<List<T>>();
            if (dbOperationResponse.recordData != default)
                userDbOperationResponse.recordData = SxmHelpers.populateUserRecord<T>(dbOperationResponse.recordData);

            return userDbOperationResponse;
        }

    }
}
