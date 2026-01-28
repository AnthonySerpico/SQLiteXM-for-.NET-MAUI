using SQLiteXM.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        internal static async Task PerformDelete(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.ExecuteDeleteAsync(sqlStatementName, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception)
            {
                // Preserve original exception semantics — rethrow so callers can observe the original exception.
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        /// <summary>
        /// Execute a named SQL delete statement using an existing transaction.
        /// </summary>
        /// <param name="sqlStatementName">The name/key of the SQL delete statement to execute.</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the named statement.</param>
        /// <param name="sxmTransaction">An already-created <see cref="SxmUTransaction"/>. This method will not commit or dispose it.</param>
        /// <returns>A task that completes when the delete has been executed on the provided transaction.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteDeleteAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        /// <summary>
        /// Execute a direct SQL delete statement in a newly-created transaction and commit it.
        /// </summary>
        /// <param name="sqlStatement">The SQL delete statement to execute (raw SQL).</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the SQL statement.</param>
        /// <param name="dbName">Optional database name. If omitted, the default database is used.</param>
        /// <returns>A task that completes when the delete and commit operations have finished.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteDirect(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.ExecuteDeleteDirectAsync(sqlStatement, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

        /// <summary>
        /// Execute a direct SQL delete statement using an existing transaction.
        /// </summary>
        /// <param name="sqlStatement">The SQL delete statement to execute (raw SQL).</param>
        /// <param name="sqlStatementParameters">Parameters to bind to the SQL statement.</param>
        /// <param name="sxmTransaction">An already-created <see cref="SxmUTransaction"/>. This method will not commit or dispose it.</param>
        /// <returns>A task that completes when the delete has been executed on the provided transaction.</returns>
        /// <exception cref="System.Exception">Any exception thrown by the underlying transaction operations is rethrown unchanged.</exception>
        internal static async Task PerformDeleteDirectTrans(string sqlStatement, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteDeleteDirectAsync(sqlStatement, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            await Task.CompletedTask.CAF();
        }

    }
}