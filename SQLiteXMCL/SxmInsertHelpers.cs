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
    /// - <see cref="PerformInsert(string, List{object}, string?)"/> creates its own <see cref="SxmUTransaction"/>,
    ///   executes the insert and commits the transaction.
    /// - <see cref="PerformInsertTrans(string, List{object}, SxmUTransaction)"/> executes an insert using an
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
        internal static async Task<Dictionary<string, object?>> PerformInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    ir = await sxmTransaction.ExecuteInsertAsync(sqlStatementName, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception)
            {
                throw;
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
        internal static async Task<Dictionary<string, object?>> PerformInsertTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;

            try
            {
                ir = await sxmTransaction.ExecuteInsertAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir).CAF();
        }

        internal static async Task<Dictionary<string, object?>> PerformInsertDirect(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    ir = await sxmTransaction.ExecuteInsertDirectAsync(sqlStatement, sqlStatementParameters);
                    await sxmTransaction.CommitTransactionAsync();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir).CAF();
        }

        internal static async Task<Dictionary<string, object?>> PerformInsertDirectTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;

            try
            {
                ir = await sxmTransaction.ExecuteInsertDirectAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir).CAF();
        }

    }
}