using LinqToDB.SqlQuery;
using SQLiteXM.Internal.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to perform update operations against the SQLiteXM store.
    /// </summary>
    /// <remarks>
    /// These helpers centralize common update patterns:
    /// - create a transaction, execute an update and commit, or
    /// - execute against an existing transaction.
    /// All methods are internal and intended for use by other SQLiteXM components.
    /// The XML documentation is provided so Visual Studio __IntelliSense__ shows method summaries,
    /// parameters and exceptions.
    /// </remarks>
    internal class SxmUpdateHelpers
    {
        /// <summary>
        /// Execute an update identified by <paramref name="sqlStatementName"/> inside a fresh transaction.
        /// The transaction is committed if the update completes successfully.
        /// </summary>
        /// <param name="sqlStatementName">The name/key of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">The parameter values used by the statement (ordered).</param>
        /// <param name="dbName">Optional database name/connection identifier. When null the default database is used.</param>
        /// <returns>A task that completes when the update and commit have finished.</returns>
        /// <exception cref="System.Exception">Exceptions thrown by the transaction or execution are propagated to the caller.</exception>
        internal static async Task PerformUpdateAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.ExecuteUpdateAsync(sqlStatementName, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformUpdateAsync failure for statement '{sqlStatementName}' db '{dbName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformUpdateAsync failure for statement '{sqlStatementName}' db '{dbName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            await Task.CompletedTask.ConfigureFalse();
        }

        /// <summary>
        /// Execute an update identified by <paramref name="sqlStatementName"/> using an existing transaction.
        /// The caller retains responsibility for committing or rolling back <paramref name="sxmTransaction"/>.
        /// </summary>
        /// <param name="sqlStatementName">The name/key of the SQL statement to execute.</param>
        /// <param name="sqlStatementParameters">The parameter values used by the statement (ordered).</param>
        /// <param name="sxmTransaction">An active <see cref="SxmUTransaction"/> to execute against.</param>
        /// <returns>A task that completes when the update has finished.</returns>
        /// <exception cref="System.Exception">Exceptions thrown by the transaction or execution are propagated to the caller.</exception>
        internal static async Task PerformUpdateTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteUpdateAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformUpdateTransAsync failure for statement '{sqlStatementName}' db '{sxmTransaction?.Connection?.DatabaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformUpdateTransAsync failure for statement '{sqlStatementName}' db '{sxmTransaction?.Connection?.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            await Task.CompletedTask.ConfigureFalse();
        }

        /// <summary>
        /// Execute a direct update (bypassing statement caching or mapping) inside a fresh transaction.
        /// The transaction is committed if the update completes successfully.
        /// </summary>
        /// <param name="sqlStatementName">The direct SQL or direct statement identifier to execute.</param>
        /// <param name="sqlStatementParameters">The parameter values used by the statement (ordered).</param>
        /// <param name="dbName">Optional database name/connection identifier. When null the default database is used.</param>
        /// <returns>A task that completes when the update and commit have finished.</returns>
        /// <exception cref="System.Exception">Exceptions thrown by the transaction or execution are propagated to the caller.</exception>
        internal static async Task PerformUpdateDirectAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.ExecuteUpdateDirectAsync(sqlStatementName, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformUpdateDirectAsync failure for statement '{sqlStatementName}' db '{dbName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformUpdateDirectAsync failure for statement '{sqlStatementName}' db '{dbName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            await Task.CompletedTask.ConfigureFalse();
        }

        /// <summary>
        /// Execute a direct update using an existing transaction.
        /// The caller retains responsibility for committing or rolling back <paramref name="sxmTransaction"/>.
        /// </summary>
        /// <param name="sqlStatementName">The direct SQL or direct statement identifier to execute.</param>
        /// <param name="sqlStatementParameters">The parameter values used by the statement (ordered).</param>
        /// <param name="sxmTransaction">An active <see cref="SxmUTransaction"/> to execute against.</param>
        /// <returns>A task that completes when the update has finished.</returns>
        /// <exception cref="System.Exception">Exceptions thrown by the transaction or execution are propagated to the caller.</exception>
        internal static async Task PerformUpdateDirectTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteUpdateDirectAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformUpdateDirectTransAsync failure for statement '{sqlStatementName}' db '{sxmTransaction?.Connection?.DatabaseName}'.");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformUpdateDirectTransAsync failure for statement '{sqlStatementName}' db '{sxmTransaction?.Connection?.DatabaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }

            await Task.CompletedTask.ConfigureFalse();
        }
    }
}