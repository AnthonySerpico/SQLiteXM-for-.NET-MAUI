using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SQLiteXM.SxmDefines;
using static SxmQueryProcessor;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods for executing SELECT queries against the SQLiteXM engine.
    /// Returns results as a list of dictionaries where each dictionary represents
    /// a row (column name -> value). Methods are internal and intended for use
    /// by other SQLiteXM components.
    /// </summary>
    internal class SxmSelectHelpers
    {
        /// <summary>
        /// Executes a named SQL statement (one registered with the statement manager)
        /// and returns all selected rows as a list of dictionaries.
        /// </summary>
        /// <param name="sqlStatementName">The registered SQL statement name to execute.</param>
        /// <param name="sqlStatementParameters">Ordered parameters for the statement.</param>
        /// <param name="dbName">Optional database name. If null, the default database is used.</param>
        /// <returns>
        /// A task that resolves to a list of rows. Each row is represented as a
        /// <see cref="Dictionary{String, Object}"/> where the key is the column name and
        /// the value is the column value (nullable).
        /// </returns>
        /// <exception cref="System.Exception">
        /// Propagates any exception thrown while creating the transaction or executing the query.
        /// </exception>
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectAsync(string sqlStatementName, List<object> sqlStatementParameters, SqlStatementDetails statementDetails, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;
            string? databaseName = default;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;

                    await sxmTransaction.ExecuteQueryAsync(sqlStatementName, sqlStatementParameters, statementDetails.SqlStatementType).ConfigureFalse();
                    selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();

                    // Only do this if this is an INSERT
                    if (statementDetails.SqlStatementType == SqlStatementType.Insert || statementDetails.SqlStatementType == SqlStatementType.InsertDirect)
                        await SxmSelectHelpers.FinalizeInsertProcessing(sqlStatementName, selectedRows, sxmTransaction, statementDetails);

                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmSqlStatements.SelectStatements.TryGetValue(sqlStatementName, out SelectDefinition? selectDefinition);
                SxmLogging.Log(ex, $"PerformSelectAsync failure. SQL statement: '{sqlStatementName}'. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {selectDefinition?.SelectSQL}");
                throw;
            }
            catch (System.Exception ex)
            {
                SxmSqlStatements.SelectStatements.TryGetValue(sqlStatementName, out SelectDefinition? selectDefinition);
                string errStr = $"PerformSelectAsync failure. SQL statement: '{sqlStatementName}'. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {selectDefinition?.SelectSQL}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(selectedRows).ConfigureFalse();
        }

        /// <summary>
        /// Executes a named SQL statement using an existing <see cref="SxmUTransaction"/>.
        /// The provided transaction instance is NOT disposed by this method.
        /// </summary>
        /// <param name="sqlStatementName">The registered SQL statement name to execute.</param>
        /// <param name="sqlStatementParameters">Ordered parameters for the statement.</param>
        /// <param name="sxmTransaction">An open <see cref="SxmUTransaction"/> to execute the statement within.</param>
        /// <returns>
        /// A task that resolves to a list of rows represented as dictionaries
        /// (column name -> value).
        /// </returns>
        /// <exception cref="System.Exception">
        /// Propagates any exception thrown while executing the query on the provided transaction.
        /// </exception>
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SqlStatementDetails statementDetails, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.ExecuteQueryAsync(sqlStatementName, sqlStatementParameters, statementDetails.SqlStatementType).ConfigureFalse();
                selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();

                // Only do this if this is an INSERT
                if (statementDetails.SqlStatementType == SqlStatementType.Insert || statementDetails.SqlStatementType == SqlStatementType.InsertDirect)
                    await SxmSelectHelpers.FinalizeInsertProcessing(sqlStatementName, selectedRows, sxmTransaction, statementDetails);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmSqlStatements.SelectStatements.TryGetValue(sqlStatementName, out SelectDefinition? selectDefinition);
                SxmLogging.Log(ex, $"PerformSelectTransAsync failure. SQL statement: '{sqlStatementName}'. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {selectDefinition?.SelectSQL}");
                throw;
            }
            catch (System.Exception ex)
            {
                SxmSqlStatements.SelectStatements.TryGetValue(sqlStatementName, out SelectDefinition? selectDefinition);
                string errStr = $"PerformSelectTransAsync failure. SQL statement: '{sqlStatementName}'. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {selectDefinition?.SelectSQL}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(selectedRows).ConfigureFalse();
        }

        /// <summary>
        /// Executes a raw SQL statement (direct SQL text) and returns all selected rows.
        /// </summary>
        /// <param name="sqlStatement">The raw SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Ordered parameters to bind to the statement.</param>
        /// <param name="dbName">Optional database name. If null, the default database is used.</param>
        /// <returns>
        /// A task that resolves to a list of rows. Each row is represented as a
        /// <see cref="Dictionary{String, Object}"/> where the key is the column name and
        /// the value is the column value (nullable).
        /// </returns>
        /// <exception cref="System.Exception">
        /// Propagates any exception thrown while creating the transaction or executing the query.
        /// </exception>
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectDirectAsync(string sqlStatement, List<object> sqlStatementParameters, SqlStatementDetails statementDetails, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;
            string? databaseName = default;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;

                    await sxmTransaction.ExecuteQueryDirectAsync(sqlStatement, sqlStatementParameters).ConfigureFalse();
                    selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();

                    // Only do this if this is an INSERT
                    if (statementDetails.SqlStatementType == SqlStatementType.Insert || statementDetails.SqlStatementType == SqlStatementType.InsertDirect)
                        await SxmSelectHelpers.FinalizeInsertProcessing(sqlStatement, selectedRows, sxmTransaction, statementDetails);

                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformSelectDirectAsync failure. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformSelectDirectAsync failure. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(selectedRows).ConfigureFalse();
        }

        /// <summary>
        /// Executes a raw SQL statement using an existing <see cref="SxmUTransaction"/>.
        /// The provided transaction instance is NOT disposed by this method.
        /// </summary>
        /// <param name="sqlStatement">The raw SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">Ordered parameters to bind to the statement.</param>
        /// <param name="sxmTransaction">An open <see cref="SxmUTransaction"/> to execute the statement within.</param>
        /// <returns>
        /// A task that resolves to a list of rows represented as dictionaries
        /// (column name -> value).
        /// </returns>
        /// <exception cref="System.Exception">
        /// Propagates any exception thrown while executing the query on the provided transaction.
        /// </exception>
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectDirectTransAsync(string sqlStatement, List<object> sqlStatementParameters, SqlStatementDetails statementDetails, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.ExecuteQueryDirectAsync(sqlStatement, sqlStatementParameters).ConfigureFalse();
                selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();

                // Only do this if this is an INSERT
                if (statementDetails.SqlStatementType == SqlStatementType.Insert || statementDetails.SqlStatementType == SqlStatementType.InsertDirect)
                    await SxmSelectHelpers.FinalizeInsertProcessing(sqlStatement, selectedRows, sxmTransaction, statementDetails);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformSelectDirectTransAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformSelectDirectTransAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(selectedRows).ConfigureFalse();
        }

        private static async Task FinalizeInsertProcessing(string command, List<Dictionary<string, object?>> selectedRows, SxmUTransaction sxmTransaction, SqlStatementDetails statementDetails)
        {
            long recordId = await sxmTransaction.GetLastInsertRowIdAsync().ConfigureFalse();

            // The 'id' is not already in the first selected return row, so we need to add it manually.
            if (selectedRows.Count > 0 && !selectedRows[0].ContainsKey("id"))
            {
                selectedRows[0]["id"] = recordId;
            }
            else if (selectedRows.Count == 0)
            {
                selectedRows.Add(new Dictionary<string, object?> { ["id"] = recordId });
            }

            string tableName = string.Empty;
            if (statementDetails.SqlStatementType == SqlStatementType.Insert)
            {
                if (!SxmSqlStatements.InsertStatements.TryGetValue(command, out InsertDefinition? insertDefinition) || insertDefinition == null)
                {
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.UnknownSqlStatement, command));
                }
                tableName = insertDefinition.TableName;
            }
            else if (statementDetails.SqlStatementType == SqlStatementType.InsertDirect)
            {
                tableName = statementDetails.TargetTableName;
            }

            if (!string.IsNullOrEmpty(tableName))
            {
                byte[]? synchId = await sxmTransaction.GetSynchIdAsync(tableName, recordId).ConfigureFalse();

                // If the 'synchId' is not already in the first selected return row, we need to add it manually. At this point, 
                // we should have at least one row in the selectedRows list, so we can safely add the 'synchId' to the first row.
                if (!selectedRows[0].ContainsKey("synchId"))
                {
                    selectedRows[0]["synchId"] = synchId;
                }
            }
        }
    }
}