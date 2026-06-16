using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;
            string? databaseName = default;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;
                    await sxmTransaction.ExecuteQueryAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
                    selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();
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
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.ExecuteQueryAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
                selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();
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
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectDirectAsync(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
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
        internal static async Task<List<Dictionary<string, object?>>> PerformSelectDirectTransAsync(string sqlStatement, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.ExecuteQueryDirectAsync(sqlStatement, sqlStatementParameters).ConfigureFalse();
                selectedRows = sxmTransaction.GetAllRows<Dictionary<string, object?>>();
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
    }
}