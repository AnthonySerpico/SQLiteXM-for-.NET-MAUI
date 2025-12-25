using SQLiteXM.Internal;
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
        internal static async Task<List<Dictionary<string, object?>>> performSelect(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.executeQueryAsync(sqlStatementName, sqlStatementParameters);
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows).CAF();
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
        internal static async Task<List<Dictionary<string, object?>>> performSelectTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.executeQueryAsync(sqlStatementName, sqlStatementParameters);
                selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows).CAF();
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
        internal static async Task<List<Dictionary<string, object?>>> performSelectDirect(string sqlStatement, List<object> sqlStatementParameters, string? dbName = default)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    await sxmTransaction.executeQueryDirectAsync(sqlStatement, sqlStatementParameters);
                    selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows).CAF();
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
        internal static async Task<List<Dictionary<string, object?>>> performSelectDirectTrans(string sqlStatement, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            List<Dictionary<string, object?>> selectedRows;

            try
            {
                await sxmTransaction.executeQueryDirectAsync(sqlStatement, sqlStatementParameters);
                selectedRows = sxmTransaction.getAllRows<Dictionary<string, object?>>();
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(selectedRows).CAF();
        }
    }
}