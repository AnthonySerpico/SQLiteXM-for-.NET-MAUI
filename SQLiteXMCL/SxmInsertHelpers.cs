using LinqToDB.SqlQuery;
using SQLiteXM.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to perform INSERT operations against the SQLiteXM data store.
    /// </summary>
    /// <remarks>
    /// These helpers centralize common insert patterns:
    /// - <see cref="PerformInsertAsync(string, List{object}, string?)"/> creates its own <see cref="SxmUTransaction"/>,
    ///   executes the insert and commits the transaction.
    /// - <see cref="PerformInsertTransAsync(string, List{object}, SxmUTransaction)"/> executes an insert using an
    ///   existing transaction (does not commit — caller is responsible for commit/rollback).
    /// </remarks>
    internal class SxmInsertHelpers
    {
        /// <summary>
        /// Create a transaction, execute an INSERT statement and commit the transaction.
        /// </summary>
        /// <param name="sqlStatementName">
        /// The name/key of the SQL statement to execute. This is looked up by the internal statement registry.
        /// </param>
        /// <param name="sqlStatementParameters">
        /// Ordered list of parameter values for the SQL statement. Use <c>null</c> elements when appropriate.
        /// </param>
        /// <param name="dbName">
        /// Optional database name to bind the transaction to. If <c>null</c>, the default database is used.
        /// </param>
        /// <returns>
        /// A task that completes with a <see cref="Dictionary{String,Object}"/> containing the insert result.
        /// The exact keys/values are produced by <see cref="SxmUTransaction.executeInsertAsync(string, List{object})"/>.
        /// </returns>
        /// <exception cref="System.Exception">
        /// Any exception thrown by the transaction or statement execution is propagated to the caller.
        /// </exception>
        internal static async Task<Dictionary<string, object?>> PerformInsertAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            string? databaseName = null;
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;
                    ir = await sxmTransaction.ExecuteInsertAsync(sqlStatementName, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformInsertAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformInsertAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(ir).CAF();
        }

        /// <summary>
        /// Execute an INSERT statement using an existing <see cref="SxmUTransaction"/>.
        /// </summary>
        /// <param name="sqlStatementName">
        /// The name/key of the SQL statement to execute. Resolved by the internal statement registry.
        /// </param>
        /// <param name="sqlStatementParameters">
        /// Ordered list of parameter values for the SQL statement.
        /// </param>
        /// <param name="sxmTransaction">
        /// The active <see cref="SxmUTransaction"/> to execute the statement on. Caller retains responsibility
        /// for committing or rolling back this transaction.
        /// </param>
        /// <returns>
        /// A task that completes with a <see cref="Dictionary{String,Object}"/> containing the insert result.
        /// The structure of the returned dictionary depends on the underlying statement implementation.
        /// </returns>
        /// <exception cref="System.Exception">
        /// Exceptions thrown by the provided transaction's execution are propagated to the caller.
        /// </exception>
        internal static async Task<Dictionary<string, object?>> PerformInsertTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;
            string? databaseName = default;

            try
            {
                databaseName = sxmTransaction.Connection?.DatabaseName;
                ir = await sxmTransaction.ExecuteInsertAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformInsertTransAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformInsertTransAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(ir).CAF();
        }

        internal static async Task<Dictionary<string, object?>> PerformInsertDirectAsync(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;
            string? databaseName = default;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;
                    ir = await sxmTransaction.ExecuteInsertDirectAsync(sqlStatement, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformInsertDirectAsync failure for SQL Statement '{sqlStatement}' database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformInsertDirectAsync failure for SQL Statement '{sqlStatement}' database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(ir).CAF();
        }

        internal static async Task<Dictionary<string, object?>> PerformInsertDirectTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;
            string? databaseName = default;

            try
            {
                databaseName = sxmTransaction.Connection?.DatabaseName;
                ir = await sxmTransaction.ExecuteInsertDirectAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformInsertDirectTransAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformInsertDirectTransAsync failure for SQL Statement '{sqlStatementName}' database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            return await Task.FromResult(ir).CAF();
        }

    }
}