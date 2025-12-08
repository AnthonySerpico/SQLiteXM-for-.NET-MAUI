using SQLiteXM.Internal;
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
                await using (SxmUTransaction sxmTransaction = SxmUTransaction.Create(dbName))
                {
                    ir = await sxmTransaction.executeInsertAsync(sqlStatementName, sqlStatementParameters);
                    sxmTransaction.commitTransaction();
                }
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir).CAF();
        }

        internal static async Task<Dictionary<string, object?>> performInsertTrans(string sqlStatementName, List<object> sqlStatementParameters, SxmUTransaction sxmTransaction)
        {
            Dictionary<string, object?> ir;

            try
            {
                ir = await sxmTransaction.executeInsertAsync(sqlStatementName, sqlStatementParameters);
            }
            catch (System.Exception)
            {
                throw;
            }

            return await Task.FromResult(ir).CAF();
        }
    }
}
