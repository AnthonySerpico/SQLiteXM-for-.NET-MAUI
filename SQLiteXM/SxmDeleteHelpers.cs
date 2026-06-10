using SQLiteXM.Internal.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods for performing delete operations using <see cref="SxmUTransaction"/>.
    /// </summary>
    /// <remarks>
    /// These methods provide small convenience wrappers around <see cref="SxmUTransaction"/>
    /// to execute named SQL delete statements or direct SQL delete statements. Methods that
    /// accept a <see cref="SxmUTransaction"/> operate on the provided transaction and do not
    /// commit it; methods that accept a database name create and commit their own transaction.
    /// XML documentation comments are provided so Visual Studio __IntelliSense__ shows usage info.
    /// </remarks>
    internal class SxmDeleteHelpers
    {
        /// <summary>
        /// Execute a named SQL delete statement in a newly-created transaction and commit it.
        /// </summary>
        /// <param name="sqlStatementName">The name/key of the SQL delete statement to execute (mapped by statements).</param>
        /// <param name="sqlStatementParameters">List of parameters to bind to the named statement.</param>
        /// <param name="dbName">Optional database name. If omitted, the default database is used.</param>
        /// <returns>A task that completes when the delete and commit operations have finished.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteAsync(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            string? databaseName = null;    
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;
                    await sxmTransaction.ExecuteDeleteAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmSqlStatements.DeleteStatements.TryGetValue(sqlStatementName, out DeleteDefinition? deleteDefinition);
                SxmLogging.Log(ex, $"PerformDeleteAsync failure. SQL statement: '{sqlStatementName}'. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {deleteDefinition?.DeleteSQL}");
                throw;
            }
            catch (System.Exception ex)
            {
                SxmSqlStatements.DeleteStatements.TryGetValue(sqlStatementName, out DeleteDefinition? deleteDefinition);
                string errStr = $"PerformDeleteAsync failure. SQL statement: '{sqlStatementName}'. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {deleteDefinition?.DeleteSQL}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Execute a named SQL delete statement using an existing transaction.
        /// </summary>
        /// <param name="sqlStatementName">The name/key of the SQL delete statement to execute.</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the named statement.</param>
        /// <param name="sxmTransaction">An already-created <see cref="SxmUTransaction"/>. This method will not commit or dispose it.</param>
        /// <returns>A task that completes when the delete has been executed on the provided transaction.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteTransAsync(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteDeleteAsync(sqlStatementName, sqlStatementParameters).ConfigureFalse();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmSqlStatements.DeleteStatements.TryGetValue(sqlStatementName, out DeleteDefinition? deleteDefinition);
                SxmLogging.Log(ex, $"PerformDeleteTransAsync failure. SQL statement: '{sqlStatementName}'. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {deleteDefinition?.DeleteSQL}");
                throw;
            }
            catch (System.Exception ex)
            {
                SxmSqlStatements.DeleteStatements.TryGetValue(sqlStatementName, out DeleteDefinition? deleteDefinition);
                string errStr = $"PerformDeleteTransAsync failure. SQL statement: '{sqlStatementName}'. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {deleteDefinition?.DeleteSQL}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Execute a direct SQL delete statement in a newly-created transaction and commit it.
        /// </summary>
        /// <param name="sqlStatement">The SQL delete statement to execute (raw SQL).</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the SQL statement.</param>
        /// <param name="dbName">Optional database name. If omitted, the default database is used.</param>
        /// <returns>A task that completes when the delete and commit operations have finished.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteDirectAsync(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            string? databaseName = default;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    databaseName = sxmTransaction.Connection?.DatabaseName;
                    await sxmTransaction.ExecuteDeleteDirectAsync(sqlStatement, sqlStatementParameters).ConfigureFalse();
                    await sxmTransaction.CommitTransactionAsync().ConfigureFalse();
                }
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformDeleteDirectAsync failure. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformDeleteDirectAsync failure. Database: '{databaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }

        /// <summary>
        /// Execute a direct SQL delete statement using an existing transaction.
        /// </summary>
        /// <param name="sqlStatement">The SQL delete statement to execute (raw SQL).</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the SQL statement.</param>
        /// <param name="sxmTransaction">An already-created <see cref="SxmUTransaction"/>. This method will not commit or dispose it.</param>
        /// <returns>A task that completes when the delete has been executed on the provided transaction.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteDirectTransAsync(string sqlStatement, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteDeleteDirectAsync(sqlStatement, sqlStatementParameters).ConfigureFalse();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                SxmLogging.Log(ex, $"PerformDeleteDirectTransAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}");
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"PerformDeleteDirectTransAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }
    }
}