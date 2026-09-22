using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Shared implementation for multi-row <c>INSERT ... VALUES (...), (...) RETURNING id</c> used by
    /// <see cref="SxmLinqExtensions.BulkInsertAsync{T}"/> and <see cref="SxmSql.BulkInsertAsync{T}"/>.
    /// The caller supplies the executor so the same SQL/id-mapping logic runs against either an
    /// <see cref="SxmTransaction"/> or an <see cref="SxmUTransaction"/>.
    /// </summary>
    internal static class SxmBulkInsertHelpers
    {
        // Parameters per INSERT statement are capped well below SQLite's SQLITE_MAX_VARIABLE_NUMBER (32766).
        // Microsoft.Data.Sqlite binds parameters by name (linear lookup per parameter), so statement cost grows
        // quadratically with parameter count; ~4500 parameters/statement measured 3x slower than 50-row batches.
        internal const int MaxParameters = 1000;

        // Default rows per INSERT statement. With the prepared statement reused across batches the remaining
        // per-statement cost is parameter binding, so smaller batches win: on a 9-column table with 100,000 rows,
        // 20 rows/statement measured ~10% faster than 50.
        internal const int DefaultBatchRows = 20;

        /// <summary>
        /// Executes <paramref name="sql"/> with positional parameters <c>@p0..@pN</c> and returns the result rows.
        /// </summary>
        internal delegate Task<List<Dictionary<string, object?>>> QueryExecutor(string sql, object?[] parameters);

        /// <summary>
        /// Validates the entity list: non-null, no null elements, every entity new (<c>id == 0</c>).
        /// </summary>
        internal static void ValidateNewEntities<T>(IReadOnlyList<T> entities, string paramName) where T : SxmEntity
        {
            if (entities == null) throw new ArgumentNullException(paramName);

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i] ?? throw new ArgumentException($"Entity at index {i} is null.", paramName);
                if (entity.id != 0)
                    throw new InvalidOperationException(
                        $"BulkInsertAsync requires new entities (id == 0). Entity at index {i} has id {entity.id}. Use SaveAsync to update an existing entity.");
            }
        }

        /// <summary>
        /// Builds and runs batched multi-row INSERT statements for <paramref name="entities"/>, assigning
        /// <c>id</c> (from RETURNING) and <c>synchId</c> (pre-generated) to every entity.
        /// </summary>
        /// <returns>The number of rows inserted.</returns>
        internal static async Task<int> InsertAsync<T>(QueryExecutor execute, IReadOnlyList<T> entities, int batchRows, CancellationToken cancellationToken)
            where T : SxmEntity
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            if (batchRows < 1) throw new ArgumentOutOfRangeException(nameof(batchRows), "batchRows must be at least 1.");
            if (entities.Count == 0) return 0;

            string tableName = typeof(T).Name;
            string quotedTable = SxmHelpers.QuoteIdentifier(tableName);

            if (!SxmEntity._columnNameAndTypeDict.TryGetValue(tableName, out var perTypeColumns))
                throw new InvalidOperationException($"Column map for type '{tableName}' is not initialized. Schema must be registered via SxmDatabase.RegisterEntitiesAsync before using entities.");

            // Same column set/order as SaveAsync's INSERT, plus synchId (pre-generated here instead of the per-row
            // UPDATE that SaveAsync performs after last_insert_rowid()). synchId is a system column added by
            // SxmDatabase.AddSynchIdAsync (BLOB DEFAULT randomblob(16)) and is not part of the registered column map,
            // so it is appended explicitly.
            var columns = perTypeColumns
                .Where(kvp => !string.Equals(kvp.Key, "id", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(kvp.Key, "synchId", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Append(new KeyValuePair<string, string>("synchId", "BLOB"))
                .ToArray();

            var columnMap = new Dictionary<string, string>(columns.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in columns)
                columnMap[kvp.Key] = kvp.Value;

            foreach (var entity in entities)
                if (entity.synchId == null)
                    entity.synchId = Guid.NewGuid();

            string columnList = string.Join(", ", columns.Select(c => SxmHelpers.QuoteIdentifier(c.Key)));
            int batchSize = columns.Length == 0 ? 1 : Math.Min(batchRows, Math.Max(1, MaxParameters / columns.Length));

            int inserted = 0;
            var sql = new StringBuilder();

            for (int start = 0; start < entities.Count; start += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int count = Math.Min(batchSize, entities.Count - start);
                var parameters = new object?[count * columns.Length];

                sql.Clear();
                if (columns.Length == 0)
                {
                    sql.Append("INSERT INTO ").Append(quotedTable).Append(" DEFAULT VALUES RETURNING id");
                }
                else
                {
                    sql.Append("INSERT INTO ").Append(quotedTable).Append(" (").Append(columnList).Append(") VALUES ");

                    int p = 0;
                    for (int r = 0; r < count; r++)
                    {
                        var values = SxmHelpers.LoadParameterValues(columnMap, entities[start + r]);

                        if (r > 0) sql.Append(", ");
                        sql.Append('(');
                        for (int c = 0; c < columns.Length; c++)
                        {
                            if (c > 0) sql.Append(", ");
                            sql.Append("@p").Append(p);
                            parameters[p++] = values.TryGetValue(columns[c].Key, out var v) ? v : DBNull.Value;
                        }
                        sql.Append(')');
                    }

                    sql.Append(" RETURNING id");
                }

                var returned = await execute(sql.ToString(), parameters).ConfigureFalse();
                if (returned.Count != count)
                    throw new InvalidOperationException($"BulkInsertAsync expected {count} generated ids for '{tableName}' but received {returned.Count}.");

                // RETURNING order is unspecified; a single multi-row INSERT on a rowid table allocates
                // consecutive ids in VALUES order, so sort and verify contiguity before mapping positionally.
                var ids = new long[count];
                for (int i = 0; i < count; i++)
                    ids[i] = Convert.ToInt64(returned[i]["id"], System.Globalization.CultureInfo.InvariantCulture);
                Array.Sort(ids);

                long firstId = ids[0];
                long lastId = ids[count - 1];
                if (lastId - firstId + 1 != count)
                    throw new InvalidOperationException($"BulkInsertAsync received non-contiguous ids for '{tableName}' ({firstId}..{lastId} for {count} rows); cannot map ids to entities.");

                // Same post-insert state SaveAsync produces: id from the database, synchId as written.
                for (int i = 0; i < count; i++)
                    entities[start + i].id = ids[i];

                inserted += count;
            }

            return inserted;
        }
    }
}
