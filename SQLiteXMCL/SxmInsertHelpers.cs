using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmInsertHelpers
    {
        internal static async Task<Dictionary<string, object?>> performInsert(string sqlStatementName, List<object> sqlStatementParameters, string? dbName = default)
        {
            Dictionary<string, object?> ir;

            try
            {
                using (SxmUTransaction sxmTransaction = new SxmUTransaction(dbName))
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

        internal static async Task<Dictionary<string, object?>> performInsertTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;

            try
            {
                ir = sxmTransaction.executeInsert(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir);
        }
    }
}
