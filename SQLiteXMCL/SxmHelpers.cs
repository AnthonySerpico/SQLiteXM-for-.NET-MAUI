using System;
using System.Collections;
using System.Reflection;

//using static CoreFoundation.DispatchSource;
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
        public void AddTransactionItem(string sqlStatementName, Dictionary<string, object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem<Dictionary<string, object>>(sqlStatementName, sqlStatementParameters));
        }
        // For adding positional parameters.
        public void AddTransactionItem(string sqlStatementName, List<object> sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem<List<object>>(sqlStatementName, sqlStatementParameters));
        }
        public void AddTransactionItem<T>(string sqlStatementName, T sqlStatementParameters)
        {
            transactionItems.Add(new TransactionItem<object>(DbName!, sqlStatementName, sqlStatementParameters));
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
        internal List<Dictionary<string, object?>>? recordData { get; set;  }
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
        public SqlStatementType SqlStatementType { get => sqlStatementType;  }
        internal string? sqlStatementName { get; set; }
        string? SqlStatementName { get => sqlStatementName; }
    }

    public class SxmHelpers
    {
        private SxmHelpers() { }

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
                    userDbOperationResponse.recordData = populateUserRecord<T>(dbOperationResponse.recordData);

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

                        if(transObject.GetType() == typeof(TransactionItem<List<object>>))
                        { 
                            TransactionItem<List<object>> transactionItem = (TransactionItem<List<object>>)transObject;
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

                        if (transObject.GetType() == typeof(TransactionItem<Dictionary<string, object>>))
                        {
                            TransactionItem<Dictionary<string, object>> transactionItem = (TransactionItem<Dictionary<string, object>>)transObject;
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

        public static async Task<List<M>?> runSqlStatement<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>>? select = await runSqlStatement(sqlStatementName, selectParameterValues, dbName);
            List<M> userRecordList = SxmHelpers.populateUserRecord<M>(select);
            return userRecordList;
        }
        public async static Task<List<T>?> runSqlStatement<T>(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default(string)) where T : class, new()
        {
            List<Dictionary<string, object?>>? runSqlStatementResponse = await runSqlStatement(sqlStatementName, sqlStatementParameters, dbName);
            if (runSqlStatementResponse != default)
                return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);

            return default(List<T>);
        }
        public static async Task<List<Dictionary<string, object?>>?> runSqlStatement<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            return await runSqlStatement(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<List<Dictionary<string, object?>>?> runSqlStatement(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default(string))
        {
            return await runSqlStatement(sqlStatementName, new List<object>(1) { sqlStatementParameters }, dbName);
        }
        public async static Task<List<T>?> runSqlStatement<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default(string)) where T : class, new()
        {
            List<Dictionary<string, object?>>? runSqlStatementResponse = await runSqlStatement(sqlStatementName, sqlStatementParameters, dbName);
            if (runSqlStatementResponse != default)
                return SxmHelpers.populateUserRecord<T>(runSqlStatementResponse);

            return default(List<T>);
        }
        public static async Task<List<Dictionary<string, object?>>?> runSqlStatement(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default(string))
        {
            List<Dictionary<string, object?>>? recordData = default(List<Dictionary<string, object?>>);

            try
            {
                using (SxmTransaction sxmTransaction = new SxmTransaction(dbName))
                {
                    switch (GetDatabaseStatementType(sqlStatementName))
                    {
                        case SqlStatementType.select:
                            recordData = await performSelect(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case SqlStatementType.insert:
                            recordData = new List<Dictionary<string, object?>>(1);
                            recordData.Add(await performInsert(sqlStatementName, sqlStatementParameters, dbName));
                            break;

                        case SqlStatementType.update:
                            await performUpdate(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        case SqlStatementType.delete:
                            await performDelete(sqlStatementName, sqlStatementParameters, dbName);
                            break;

                        default: break;
                    }
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(recordData);
        }

        private static DbOperationResponse<List<T>> loadUserResponseObject<T>(DbOperationResponse dbOperationResponse) where T : class, new()
        {
            DbOperationResponse<List<T>> userDbOperationResponse = new DbOperationResponse<List<T>>();
            if (dbOperationResponse.recordData != default)
                userDbOperationResponse.recordData = populateUserRecord<T>(dbOperationResponse.recordData);

            return userDbOperationResponse;
        }

        internal static SqlStatementType GetDatabaseStatementType(string? sqlStatementName)
        {
            if (sqlStatementName == null)
                throw new ArgumentException("A sql statement cannot be null.");

            if (SqlStatements.selectStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.select;

            if (SqlStatements.insertStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.insert;

            if (SqlStatements.updateStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.update;

            if (SqlStatements.deleteStatements.ContainsKey(sqlStatementName) != default)
                return SqlStatementType.delete;

            throw new ArgumentException(string.Format("The sql statement '{0}' could not be found.", sqlStatementName));
        }

        public static async Task<List<Dictionary<string, object?>>> performSelect<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            return await performSelect(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<List<M>> performSelect<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            List<Dictionary<string, object?>> select = await performSelect(sqlStatementName, selectParameterValues, dbName);
            List<M> userRecordList = SxmHelpers.populateUserRecord<M>(select);
            return userRecordList;
        }
        public static async Task<List<T>> performSelect<T>(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            List<Dictionary<string, object?>> select = await SxmHelpers.performSelect(sqlStatementName, sqlStatementParameters, dbName);
            List<T> userRecordList = SxmHelpers.populateUserRecord<T>(select);
            return userRecordList;
        }
        public static async Task<List<T>> performSelect<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            List<Dictionary<string, object?>> select = await SxmHelpers.performSelect(sqlStatementName, sqlStatementParameters, dbName);
            List<T> userRecordList = SxmHelpers.populateUserRecord<T>(select);
            return userRecordList;
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

        public static async Task<M> performInsert<T, M>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
                                                                                                                                         where M : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            Dictionary<string, object?> select = await performInsert(sqlStatementName, selectParameterValues, dbName);
            
            M userRecordList = SxmHelpers.loadDbValues<M>(select);
            return userRecordList;
        }
        public static async Task<Dictionary<string, object?>> performInsert<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName, GetDatabaseStatementType(sqlStatementName));
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            return await performInsert(sqlStatementName, selectParameterValues, dbName);
        }
        public static async Task<T> performInsert<T>(string sqlStatementName, Dictionary<string, object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await SxmHelpers.performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = SxmHelpers.loadDbValues<T>(select);

            return userRecord;
        }
        public static async Task<T> performInsert<T>(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default) where T : class, new()
        {
            Dictionary<string, object?> select = await SxmHelpers.performInsert(sqlStatementName, sqlStatementParameters, dbName);
            T userRecord = SxmHelpers.loadDbValues<T>(select); 

            return userRecord;
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

/*        public static async Task performDelete<T>(string sqlStatementName, T userObjectParameters, string? dbName = default) where T : class, new()
        {
            List<string> columnNames = SxmInit.getTableColumnNames(dbName, sqlStatementName);
            Dictionary<string, object?> selectParameterValues = loadParamaterValues<T>(columnNames, userObjectParameters);
            await performDelete(sqlStatementName, selectParameterValues, dbName);
        }*/
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

        public static List<T> populateUserRecord<T>(List<Dictionary<string, object?>> databaseRowsList) where T : class, new()
        {
            List<T> userObjectList = new List<T>();

            foreach (Dictionary<string, object?> databaseRecord in databaseRowsList)  // Process each entry (record) in the List.
            {
                T userObject = loadDbValues<T>(databaseRecord);
                userObjectList.Add(userObject);
            }

            return userObjectList;
        }

        public static T loadDbValues<T>(Dictionary<string, object?> databaseRecord) where T : class, new()
        {
            T userObject = new T();
            foreach (KeyValuePair<string, object?> kvp in databaseRecord)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    userObject.GetType().GetProperty(kvp.Key)?.SetValue(userObject, kvp.Value);
                }
                catch (System.ArgumentException)
                {
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' type {1} to the provided object property '{2}' type {3}", (kvp.Key, kvp.Value?.GetType().ToString(), kvp.Key, userObject.GetType()?.GetProperty(kvp.Key)?.PropertyType.ToString())));
                }
            }
            return userObject;
        }

        public static Dictionary<string, object?> loadParamaterValues<T>(List<string> dbColumnNames, T userObject) where T : class, new()
        {
            Dictionary<string, object?> returnDictionary = new Dictionary<string, object> ();
            foreach (string columnName in dbColumnNames)  // Process each entry (column) in the Dictionary.
            {
                try
                {
                    PropertyInfo? pi = userObject.GetType().GetProperty(columnName);  // If the column is in the user supplied object.
                    if (pi != default)
                        returnDictionary.Add(columnName, pi.GetValue(userObject));
                }
                catch (System.ArgumentException)
                {
                    throw new ArgumentException(string.Format("Could not cast the database column '{0}' to the provided object property '{1}' type {2}", (columnName, columnName, userObject.GetType()?.GetProperty(columnName)?.PropertyType.ToString())));
                }
            }
            return returnDictionary;
        }
    }
}
