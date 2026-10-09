using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods for performing DDL operations using <see cref="SxmUTransaction"/>.
    /// </summary>
    internal static class SxmDdlHelpers
    {
        /// <summary>
        /// Execute a table-level DDL statement (for example, CREATE or DROP TABLE) in a newly-created transaction and commit it.
        /// </summary>
        /// <param name="sqlStatement">The DDL SQL text to execute.</param>
        /// <param name="sxmTransaction">The <see cref="SxmUTransaction"/> the DDL statement is executed on. Its connection also supplies the database name used in failure diagnostics.</param>
        /// <returns>A task that completes when the DDL statement has executed.</returns>
        internal static async Task PerformTableStatementAsync(string sqlStatement, SxmUTransaction sxmTransaction)
        {
            try
            {
                await sxmTransaction.ExecuteTableStatementAsync(sqlStatement).ConfigureFalse();
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                // Pass-through: the original exception's message cannot be changed, so Data is the
                // only place this operation detail can reach the caller.
                string context = $"PerformTableStatementAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                ExceptionHelper.AddContext(ex, context);
                SxmLogging.Log(ex, context);
                throw;
            }
            catch (System.Exception ex)
            {
                string context = $"PerformTableStatementAsync failure. Database: '{sxmTransaction?.Connection?.DatabaseName}'.{Environment.NewLine}{Environment.NewLine}Command: {sqlStatement}";
                SxmLogging.Log(ex, context);
                throw ExceptionHelper.Wrap(ex, SxmDefines.SxmErrorCode.SchemaFailure, context);
            }
        }
    }
}
