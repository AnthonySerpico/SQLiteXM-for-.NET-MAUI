using System.Diagnostics;
using System.Runtime.InteropServices;
using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Benchmarks;

/// <summary>
/// One line of benchmark output. Every benchmark returns a list of these so results render
/// consistently in the gallery's results pane.
/// </summary>
internal sealed record BenchRow(
    string Scenario,
    int Iterations,
    double Total_Milliseconds,
    double Avg_Milliseconds,
    string Ratio,
    string Note);

/// <summary>
/// Shared helpers for the Benchmarks category. Kept intentionally tiny so the displayed
/// RunAsync body remains the real story.
/// </summary>
internal static class Bench
{
    public static BenchRow Row(string scenario, int iterations, long totalMs, double baselineMs, string note = "")
    {
        double avg = iterations == 0 ? 0 : (double)totalMs / iterations;
        string ratio = baselineMs <= 0 ? "1.00x (baseline)" : $"{totalMs / baselineMs:0.00}x";
        return new BenchRow(scenario, iterations, totalMs, Math.Round(avg, 3), ratio, note);
    }

    public static BenchRow Environment()
    {
#if DEBUG
        const string config = "Debug";
#else
        const string config = "Release";
#endif
        string note = $"{config} | {DeviceInfo.Platform} {DeviceInfo.VersionString} | {RuntimeInformation.FrameworkDescription} | {(DeviceInfo.DeviceType == DeviceType.Virtual ? "Emulator/Simulator" : "Physical device")}";
        return new BenchRow("Environment", 0, 0, 0, "-", note);
    }
}

[QueryExample(
    id: "bench_1",
    name: "Insert: Single Transaction vs. Individual Inserts vs. BulkInsertAsync",
    description: "500 inserts inside one SxmTransaction. The same 500 inserts with NO transaction - a commit for each insert. The same 500 inserts via the LINQ BulkInsertAsync and via SxmSql.BulkInsertAsync.",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
Warm up with a couple of inserts so JIT and connection costs are excluded

Scenario A: 500 SaveAsync calls inside one SxmTransaction - auto-commit
Scenario B: 500 SaveAsync calls with NO transaction - each insert auto-commits
Scenario C: 500 entities passed to ctx.GetTable<Track>().BulkInsertAsync inside one SxmTransaction
Scenario D: 500 entities passed to SxmSql.BulkInsertAsync, which opens and commits its own transaction

All scenarios are timed with Stopwatch and compared

**Key Concepts:**
- Every SQLite commit is an fsync - it dominates the cost of a small insert
- Batching writes in one transaction is the single biggest SQLite optimization
- Expect a 10x-100x difference depending on storage
- SaveAsync enlists automatically in the ambient SxmTransaction
- Both BulkInsertAsync variants issue multi-row INSERT ... RETURNING statements and still populate id/synchId on every entity
- Use the LINQ variant to take part in a larger SxmTransaction; use SxmSql.BulkInsertAsync for a standalone insert
""")]
internal sealed class Bench1Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Count = 500;
        var marker = $"_Bench1_{Guid.NewGuid():N}";

        Track NewTrack(string suffix) => new()
        {
            Name = marker + suffix, AlbumId = 1, MediaTypeId = 1, GenreId = 1,
            Milliseconds = 180000, UnitPrice = 0.99m
        };

        // Warm-up (auto-commit path)
        await NewTrack("_warm").SaveAsync();

        // Scenario A: one transaction + one commit for all inserts
        long singleTxMs;
        var sw = Stopwatch.StartNew();

        await using (var ctx = new SxmTransaction("Chinook"))
        {
            for (int i = 0; i < Count; i++)
                await NewTrack($"_A{i}").SaveAsync();
        }

        sw.Stop();
        singleTxMs = sw.ElapsedMilliseconds;

        // Scenario B: no transaction + one commit for each insert (auto-commit)
        sw.Restart();

        for (int i = 0; i < Count; i++)
            await NewTrack($"_B{i}").SaveAsync();

        sw.Stop();
        long autoCommitMs = sw.ElapsedMilliseconds;

        // Scenario C: one transaction, multi-row INSERT ... RETURNING via BulkInsertAsync.
        // Swept over batch sizes to expose per-statement vs. per-parameter costs.
        async Task<(long ms, int inserted, long firstId)> RunBulkAsync(int batchRows, string tag)
        {
            var bsw = Stopwatch.StartNew();
            int inserted;
            long firstId;
            await using (var ctx = new SxmTransaction("Chinook"))
            {
                var tracks = new List<Track>(Count);
                for (int i = 0; i < Count; i++)
                    tracks.Add(NewTrack($"_{tag}{i}"));

                inserted = await ctx.GetTable<Track>().BulkInsertAsync(tracks, batchRows);
                firstId = tracks[0].id;   // populated by BulkInsertAsync, same as SaveAsync
            }
            bsw.Stop();
            return (bsw.ElapsedMilliseconds, inserted, firstId);
        }

        var bulk15  = await RunBulkAsync(15,  "C15_");

        // Scenario D: SxmSql.BulkInsertAsync - standalone, opens and commits its own transaction.
        var tracksD = new List<Track>(Count);
        for (int i = 0; i < Count; i++)
            tracksD.Add(NewTrack($"_D{i}"));

        sw.Restart();
        int insertedD = await SxmSql.BulkInsertAsync(tracksD, 15);
        sw.Stop();
        long sxmSqlBulkMs = sw.ElapsedMilliseconds;
        long firstIdD = tracksD[0].id;   // populated by SxmSql.BulkInsertAsync

        // Cleanup Scenario A + warm-up rows
        int deleted;
        await using (var cleanup = new SxmTransaction("Chinook"))
        {
            deleted = cleanup.GetTable<Track>().Where(t => t.Name.StartsWith(marker)).Delete();
            await cleanup.CommitTransactionAsync();
        }

        return new List<BenchRow>
        {
            Bench.Row("Single SxmTransaction",     Count, singleTxMs,   0, $"{Count} 'NewTrack.SaveAsync(...)' inside transaction"),
            Bench.Row("Auto-commit per SaveAsync", Count, autoCommitMs, singleTxMs, $"{Count} 'NewTrack.SaveAsync(...)' not in a transaction"),
            Bench.Row("LINQ BulkInsertAsync, 15 rows/stmt",  bulk15.inserted,  bulk15.ms,  singleTxMs, $"One transaction. {Count} rows inserted. 15 rows inserted per statement"),
            Bench.Row("SxmSql.BulkInsertAsync, 15 rows/stmt", insertedD, sxmSqlBulkMs, singleTxMs, $"One transaction. {Count} rows inserted. 15 rows inserted per statement"),
            //new BenchRow("Cleanup", deleted, 0, 0, "-", $"Deleted {deleted} marker rows"),
            //Bench.Environment()
        };
    }
}

[QueryExample(
    id: "bench_2",
    name: "Insert: Transaction Batch Size Sweep",
    description: "300 inserts as 1 tx, 3 tx of 100, 30 tx of 10, and 300 tx of 1",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Insert the same 300 tracks four times, varying how many rows share a transaction
2. Each batch opens an SxmTransaction, saves N rows, then rolls back
3. Rollback keeps the database unchanged while still paying the per-transaction overhead
4. Times are reported relative to the single-transaction baseline

**Key Concepts:**
- Cost is roughly (rows x row-cost) + (transactions x commit-cost)
- Very large batches give diminishing returns but never hurt on SQLite
- Pick a batch size that bounds memory/latency, not one that minimizes commits at all costs
- Useful when importing data from an API in pages
""")]
internal sealed class Bench2Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Total = 300;
        var marker = $"_Bench2_{Guid.NewGuid():N}";

        async Task<long> RunBatchesAsync(int batchSize)
        {
            var sw = Stopwatch.StartNew();
            for (int start = 0; start < Total; start += batchSize)
            {
                await using var ctx = new SxmTransaction("Chinook");
                for (int i = start; i < start + batchSize && i < Total; i++)
                {
                    await new Track
                    {
                        Name = $"{marker}_{batchSize}_{i}", AlbumId = 1, MediaTypeId = 1, GenreId = 1,
                        Milliseconds = 180000, UnitPrice = 0.99m
                    }.SaveAsync();
                }
                await ctx.RollbackTransactionAsync();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Warm-up
        await RunBatchesAsync(Total);

        long oneTx      = await RunBatchesAsync(300);
        long threeTx    = await RunBatchesAsync(100);
        long thirtyTx   = await RunBatchesAsync(10);
        long perRowTx   = await RunBatchesAsync(1);

        return new List<BenchRow>
        {
            Bench.Row("1 tx x 300 rows",  Total, oneTx,    0,     "Baseline"),
            Bench.Row("3 tx x 100 rows",  Total, threeTx,  oneTx, "Reasonable page size"),
            Bench.Row("30 tx x 10 rows",  Total, thirtyTx, oneTx, "Commit overhead visible"),
            Bench.Row("300 tx x 1 row",   Total, perRowTx, oneTx, "Worst case: commit per row"),
            //Bench.Environment()
        };
    }
}

[QueryExample(
    id: "bench_3",
    name: "Read: LINQ vs. Named SQL vs. Embedded SQL",
    description: "The same Track/Album/Artist join executed three ways, 20 iterations each",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Warm up each path once so LinqToDB's expression cache and SQLite's statement cache are primed
2. LINQ: a three-table join projected to an anonymous type via GetTable<T>()
3. Named SQL: ctx.RunStatementAsync("GetTracksWithArtistAlbum") from SqlStatements.json
4. Embedded SQL: the equivalent SELECT passed as a literal string
5. All three run on the same SxmTransaction connection

**Key Concepts:**
- LINQ pays for expression translation; caching makes repeat executions cheap
- Raw SQL paths skip translation but return Dictionary rows instead of typed objects
- The differences are usually small compared to the query itself - measure before deciding
- All three share one connection and one transaction context
""")]
internal sealed class Bench3Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Iterations = 20;
        await using var ctx = new SxmTransaction("Chinook");

        const string embeddedSql =
            "SELECT t.Name AS TrackName, a.Title AS AlbumTitle, ar.Name AS ArtistName, t.Milliseconds, g.Name AS GenreName " +
            "FROM Track t INNER JOIN Album a ON t.AlbumId = a.id INNER JOIN Artist ar ON a.ArtistId = ar.id " +
            "LEFT JOIN Genre g ON t.GenreId = g.id ORDER BY ar.Name, a.Title LIMIT 100";

        int RunLinq() =>
            (from t in ctx.GetTable<Track>()
             join a in ctx.GetTable<Album>() on t.AlbumId equals a.id
             join ar in ctx.GetTable<Artist>() on a.ArtistId equals ar.id
             join g in ctx.GetTable<Genre>() on t.GenreId equals g.id into gj
             from g in gj.DefaultIfEmpty()
             orderby ar.Name, a.Title
             select new { TrackName = t.Name, AlbumTitle = a.Title, ArtistName = ar.Name, t.Milliseconds, GenreName = g.Name })
            .Take(100)
            .ToList()
            .Count;

        // Warm-up
        RunLinq();
        await ctx.RunStatementAsync("GetTracksWithArtistAlbum");
        await ctx.RunStatementAsync(embeddedSql);

        var sw = Stopwatch.StartNew();
        int linqRows = 0;
        for (int i = 0; i < Iterations; i++) linqRows = RunLinq();
        long linqMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int namedRows = 0;
        for (int i = 0; i < Iterations; i++) namedRows = (await ctx.RunStatementAsync("GetTracksWithArtistAlbum")).Count;
        long namedMs = sw.ElapsedMilliseconds;

        sw.Restart();
        int embeddedRows = 0;
        for (int i = 0; i < Iterations; i++) embeddedRows = (await ctx.RunStatementAsync(embeddedSql)).Count;
        long embeddedMs = sw.ElapsedMilliseconds;

        return new List<BenchRow>
        {
            Bench.Row("LINQ (typed projection)", Iterations, linqMs,     0,      $"{linqRows} rows/iteration"),
            Bench.Row("Named SQL (JSON)",        Iterations, namedMs,    linqMs, $"{namedRows} rows/iteration"),
            Bench.Row("Embedded SQL (literal)",  Iterations, embeddedMs, linqMs, $"{embeddedRows} rows/iteration"),
            //Bench.Environment()
        };
    }
}

[QueryExample(
    id: "bench_4",
    name: "Filter: Indexed vs. Non-Indexed Column",
    description: "COUNT with WHERE on Track.GenreId (indexed) vs. Track.Composer (no index), on the stock ~3,500-row table and again after bulk inserting 100,000 rows. 50 iterations each.",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Track.GenreId carries [Index]; Track.Composer has none; (GenreId, UnitPrice) is a compound index
2. Every query is a COUNT so SQLite does all the work - no entities are materialized, only lookup cost is timed
3. Pass 1 runs the three filters 50 times each against the stock table (~3,500 rows)
4. 100,000 marker tracks are then bulk inserted; 500 of them share one GenreId and one Composer so the indexed and scanned filters return the same number of rows
5. Pass 2 repeats the same three filters against ~103,500 rows
6. The marker rows are deleted; the database contents are unchanged (the file keeps its freed pages until a VACUUM)

**Key Concepts:**
- An indexed equality filter is a B-tree seek; a non-indexed one is a full table scan
- On 3,500 rows the whole table fits in a few pages, so a scan is nearly free and the two look alike
- On 100,000 rows the seek cost barely moves while the scan cost grows with the table - that is the decisive difference
- The compound index answers GenreId && UnitPrice entirely from the index without touching the table
- The [Index] attributes on the models are what make this work
""")]
internal sealed class Bench4Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Iterations = 50;
        const int MarkerRows = 100_000;
        const int MatchingRows = 500;
        var marker = $"_Bench4_{Guid.NewGuid():N}";
        var composer = marker + "_composer";

        // Use the genre with the fewest existing tracks so both filters return ~MatchingRows in pass 2.
        long genreId;
        long otherGenreId;
        await using (var ctx = new SxmTransaction("Chinook"))
        {
            var genreIds = ctx.GetTable<Genre>().OrderByDescending(g => g.id).Select(g => g.id).Take(2).ToList();
            genreId = genreIds[0];
            otherGenreId = genreIds[1];
        }

        async Task<(long idxMs, int idxRows, long scanMs, int scanRows, long compMs, int compRows)> RunPassAsync()
        {
            await using var ctx = new SxmTransaction("Chinook");
            var tracks = ctx.GetTable<Track>();

            Task<int> Indexed()    => tracks.Where(t => t.GenreId == genreId).CountAsync();
            Task<int> NonIndexed() => tracks.Where(t => t.Composer == composer).CountAsync();
            Task<int> Compound()   => tracks.Where(t => t.GenreId == genreId && t.UnitPrice > 0.5m).CountAsync();

            // Warm-up
            await Indexed(); await NonIndexed(); await Compound();

            var sw = Stopwatch.StartNew();
            int idxRows = 0;
            for (int i = 0; i < Iterations; i++) idxRows = await Indexed();
            long idxMs = sw.ElapsedMilliseconds;

            sw.Restart();
            int scanRows = 0;
            for (int i = 0; i < Iterations; i++) scanRows = await NonIndexed();
            long scanMs = sw.ElapsedMilliseconds;

            sw.Restart();
            int compRows = 0;
            for (int i = 0; i < Iterations; i++) compRows = await Compound();
            long compMs = sw.ElapsedMilliseconds;

            return (idxMs, idxRows, scanMs, scanRows, compMs, compRows);
        }

        // Pass 1: stock table (~3,500 rows)
        var small = await RunPassAsync();

        // Grow the table. The first MatchingRows share genreId + composer (half priced above 0.5);
        // the rest use a different genre and composer so they only add scan volume.
        var markers = new List<Track>(MarkerRows);
        for (int i = 0; i < MarkerRows; i++)
        {
            bool match = i < MatchingRows;
            markers.Add(new Track
            {
                Name = $"{marker}_{i}", AlbumId = 1, MediaTypeId = 1,
                GenreId = match ? genreId : otherGenreId,
                Composer = match ? composer : marker + "_other",
                Milliseconds = 180000,
                UnitPrice = (i % 2 == 0) ? 0.99m : 0.49m
            });
        }
        await SxmSql.BulkInsertAsync(markers, 50);

        // Pass 2: ~103,500 rows
        var large = await RunPassAsync();

        // Cleanup
        await using (var cleanup = new SxmTransaction("Chinook"))
        {
            cleanup.GetTable<Track>().Where(t => t.Name.StartsWith(marker)).Delete();
            await cleanup.CommitTransactionAsync();
        }

        return new List<BenchRow>
        {
            Bench.Row("3.5K rows: GenreId == x (indexed)",              Iterations, small.idxMs,  0,           $"{small.idxRows} rows"),
            Bench.Row("3.5K rows: Composer == y (no index, scan)",      Iterations, small.scanMs, small.idxMs, $"{small.scanRows} rows"),
            Bench.Row("3.5K rows: GenreId && UnitPrice (compound idx)", Iterations, small.compMs, small.idxMs, $"{small.compRows} rows"),
            Bench.Row("100K rows: GenreId == x (indexed)",              Iterations, large.idxMs,  0,           $"{large.idxRows} rows"),
            Bench.Row("100K rows: Composer == y (no index, scan)",      Iterations, large.scanMs, large.idxMs, $"{large.scanRows} rows"),
            Bench.Row("100K rows: GenreId && UnitPrice (compound idx)", Iterations, large.compMs, large.idxMs, $"{large.compRows} rows"),
            //Bench.Environment()
        };
    }
}

[QueryExample(
    id: "bench_5",
    name: "Projection: Full Entity vs. Select Columns",
    description: "Materialize all ~3,500 tracks as entities vs. two-column anonymous projection",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Scenario A: ToList() on GetTable<Track>() hydrates every column into a Track entity
2. Scenario B: Select(t => new { t.Name, t.UnitPrice }) reads two columns
3. Scenario C: Select(t => t.id) reads a single scalar per row
4. Each runs 10 times after a warm-up

**Key Concepts:**
- Hydration cost scales with columns x rows, not just rows
- Entity materialization also runs SxmEntity bookkeeping per object
- Project to exactly what the UI needs for list views; load full entities for editing
- SQLite reads fewer pages when fewer columns are requested from a covering index
""")]
internal sealed class Bench5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        const int Iterations = 10;
        using var ctx = new SxmTransaction("Chinook");

        int FullEntity() => ctx.GetTable<Track>().ToList().Count;
        int TwoColumns() => ctx.GetTable<Track>().Select(t => new { t.Name, t.UnitPrice }).ToList().Count;
        int Scalar()     => ctx.GetTable<Track>().Select(t => t.id).ToList().Count;

        // Warm-up
        FullEntity(); TwoColumns(); Scalar();

        var sw = Stopwatch.StartNew();
        int rows = 0;
        for (int i = 0; i < Iterations; i++) rows = FullEntity();
        long fullMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < Iterations; i++) TwoColumns();
        long twoMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < Iterations; i++) Scalar();
        long scalarMs = sw.ElapsedMilliseconds;

        return Task.FromResult<object>(new List<BenchRow>
        {
            Bench.Row("Full Track entity",        Iterations, fullMs,   0,      $"{rows} rows x all columns"),
            Bench.Row("Select { Name, UnitPrice }", Iterations, twoMs,    fullMs, $"{rows} rows x 2 columns"),
            Bench.Row("Select id (scalar)",       Iterations, scalarMs, fullMs, $"{rows} rows x 1 column"),
            //Bench.Environment()
        });
    }
}

[QueryExample(
    id: "bench_6",
    name: "Pagination: OFFSET vs. Keyset",
    description: "Skip/Take at page 1, 20 and 60 vs. keyset paging (id > last), 30 iterations",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Page size is 50 tracks ordered by id
2. OFFSET paging: Skip(page x 50).Take(50) for pages 1, 20 and 60
3. Keyset paging: Where(t => t.id > lastSeenId).Take(50) positioned at the same depth as page 60
4. Each variant runs 30 times after a warm-up

**Key Concepts:**
- OFFSET makes SQLite walk and discard every skipped row - cost grows with page number
- Keyset (seek) paging uses the index to jump straight to the boundary - cost stays flat
- Keyset requires a stable, unique sort key (id is perfect)
- Use OFFSET for small, shallow lists; keyset for infinite scroll
""")]
internal sealed class Bench6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        const int Iterations = 30;
        const int PageSize = 50;
        using var ctx = new SxmTransaction("Chinook");

        int OffsetPage(int page) =>
            ctx.GetTable<Track>().OrderBy(t => t.id).Skip(page * PageSize).Take(PageSize).ToList().Count;

        // Find the id boundary for "page 60" so keyset starts at the same depth
        long lastSeenId = ctx.GetTable<Track>().OrderBy(t => t.id).Skip(60 * PageSize - 1).Select(t => t.id).First();

        int KeysetPage() =>
            ctx.GetTable<Track>().Where(t => t.id > lastSeenId).OrderBy(t => t.id).Take(PageSize).ToList().Count;

        // Warm-up
        OffsetPage(0); OffsetPage(60); KeysetPage();

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) OffsetPage(0);
        long page1Ms = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < Iterations; i++) OffsetPage(20);
        long page20Ms = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < Iterations; i++) OffsetPage(60);
        long page60Ms = sw.ElapsedMilliseconds;

        sw.Restart();
        int keysetRows = 0;
        for (int i = 0; i < Iterations; i++) keysetRows = KeysetPage();
        long keysetMs = sw.ElapsedMilliseconds;

        return Task.FromResult<object>(new List<BenchRow>
        {
            Bench.Row("OFFSET page 1  (Skip 0)",     Iterations, page1Ms,  0,       "Baseline"),
            Bench.Row("OFFSET page 20 (Skip 1000)",  Iterations, page20Ms, page1Ms, "Skips 1,000 rows each call"),
            Bench.Row("OFFSET page 60 (Skip 3000)",  Iterations, page60Ms, page1Ms, "Skips 3,000 rows each call"),
            Bench.Row("Keyset  (id > lastSeen)",     Iterations, keysetMs, page1Ms, $"Same depth as page 60, {keysetRows} rows"),
            //Bench.Environment()
        });
    }
}

[QueryExample(
    id: "bench_7",
    name: "Aggregation: In SQL vs. In Memory",
    description: "GROUP BY GenreId with COUNT/AVG pushed to SQLite vs. ToList() then LINQ-to-Objects",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Scenario A: GroupBy on the IQueryable so SQLite executes GROUP BY, COUNT and AVG
2. Scenario B: ToList() every Track, then GroupBy in C# with LINQ-to-Objects
3. Both produce identical results: track count and average duration per genre
4. Each runs 10 times after a warm-up

**Key Concepts:**
- Pushing aggregation to the database returns 25 rows instead of 3,500
- In-memory aggregation pays hydration cost for every row it then throws away
- The IQueryable/IEnumerable boundary is where this decision is made - watch for accidental ToList()
- Same LINQ syntax, radically different execution plan
""")]
internal sealed class Bench7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        const int Iterations = 10;
        using var ctx = new SxmTransaction("Chinook");

        int InSql() =>
            ctx.GetTable<Track>()
               .GroupBy(t => t.GenreId)
               .Select(g => new { GenreId = g.Key, Count = g.Count(), AvgMs = g.Average(t => (double)t.Milliseconds) })
               .ToList()
               .Count;

        int InMemory() =>
            ctx.GetTable<Track>()
               .ToList()
               .GroupBy(t => t.GenreId)
               .Select(g => new { GenreId = g.Key, Count = g.Count(), AvgMs = g.Average(t => (double)t.Milliseconds) })
               .ToList()
               .Count;

        // Warm-up
        InSql(); InMemory();

        var sw = Stopwatch.StartNew();
        int groups = 0;
        for (int i = 0; i < Iterations; i++) groups = InSql();
        long sqlMs = sw.ElapsedMilliseconds;

        sw.Restart();
        for (int i = 0; i < Iterations; i++) InMemory();
        long memMs = sw.ElapsedMilliseconds;

        return Task.FromResult<object>(new List<BenchRow>
        {
            Bench.Row("GROUP BY in SQLite",          Iterations, sqlMs, 0,     $"{groups} group rows transferred"),
            Bench.Row("ToList() then GroupBy in C#", Iterations, memMs, sqlMs, "All tracks transferred first"),
            //Bench.Environment()
        });
    }
}

[QueryExample(
    id: "bench_8",
    name: "Existence: Any() vs. Count() > 0 vs. FirstOrDefault()",
    description: "Three idioms for 'does a matching row exist?' on InvoiceLine, selective and broad predicates",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Broad predicate: Quantity >= 1 matches almost every InvoiceLine
2. Selective predicate: TrackId equal to a specific track matches a handful
3. Each idiom (Any, Count() > 0, FirstOrDefault() != null) runs 100 times per predicate
4. Times are compared against Any()

**Key Concepts:**
- Any() -> SELECT EXISTS(...) stops at the first match
- Count() > 0 forces SQLite to count every matching row before comparing
- FirstOrDefault() also stops early but hydrates a full entity
- The gap is largest when many rows match - exactly the broad case
""")]
internal sealed class Bench8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        const int Iterations = 100;
        using var ctx = new SxmTransaction("Chinook");

        long trackId = ctx.GetTable<InvoiceLine>().OrderBy(l => l.id).Select(l => l.TrackId).First();

        bool AnyBroad()   => ctx.GetTable<InvoiceLine>().Any(l => l.Quantity >= 1);
        bool CountBroad() => ctx.GetTable<InvoiceLine>().Count(l => l.Quantity >= 1) > 0;
        bool FirstBroad() => ctx.GetTable<InvoiceLine>().FirstOrDefault(l => l.Quantity >= 1) != null;

        bool AnySel()   => ctx.GetTable<InvoiceLine>().Any(l => l.TrackId == trackId);
        bool CountSel() => ctx.GetTable<InvoiceLine>().Count(l => l.TrackId == trackId) > 0;
        bool FirstSel() => ctx.GetTable<InvoiceLine>().FirstOrDefault(l => l.TrackId == trackId) != null;

        // Warm-up
        AnyBroad(); CountBroad(); FirstBroad(); AnySel(); CountSel(); FirstSel();

        long Time(Func<bool> f)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < Iterations; i++) f();
            return sw.ElapsedMilliseconds;
        }

        long anyBroadMs = Time(AnyBroad), countBroadMs = Time(CountBroad), firstBroadMs = Time(FirstBroad);
        long anySelMs   = Time(AnySel),   countSelMs   = Time(CountSel),   firstSelMs   = Time(FirstSel);

        return Task.FromResult<object>(new List<BenchRow>
        {
            Bench.Row("Broad: Any()",                    Iterations, anyBroadMs,   0,          "Matches ~all rows"),
            Bench.Row("Broad: Count() > 0",              Iterations, countBroadMs, anyBroadMs, "Counts every match"),
            Bench.Row("Broad: FirstOrDefault() != null", Iterations, firstBroadMs, anyBroadMs, "Hydrates one entity"),
            Bench.Row("Selective: Any()",                Iterations, anySelMs,     0,          "Matches a few rows"),
            Bench.Row("Selective: Count() > 0",          Iterations, countSelMs,   anySelMs,   ""),
            Bench.Row("Selective: FirstOrDefault()",     Iterations, firstSelMs,   anySelMs,   ""),
            //Bench.Environment()
        });
    }
}

[QueryExample(
    id: "bench_9",
    name: "Update: Entity Round-Trip vs. Set-Based",
    description: "Load 200 tracks, mutate and SaveAsync each vs. one LINQ Set(...).UpdateAsync()",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Pick the first 200 tracks by id
2. Scenario A: load them, change UnitPrice on each entity, SaveAsync each (200 UPDATE statements)
3. Scenario B: a single Set(t => t.UnitPrice, t => t.UnitPrice + 0.01m).UpdateAsync() (1 UPDATE statement)
4. Each scenario runs inside its own SxmTransaction and is rolled back

**Key Concepts:**
- Entity round-trips are ideal for editing one record with validation and change tracking
- Set-based updates move the work into SQLite and issue one statement
- Both enlist in the ambient transaction and roll back atomically
- N statements vs. 1 statement is the whole story here
""")]
internal sealed class Bench9Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Count = 200;

        // Warm-up: touch both paths once
        await using (var warm = new SxmTransaction("Chinook"))
        {
            var t = warm.GetTable<Track>().OrderBy(x => x.id).First();
            t.UnitPrice += 0.01m;
            await t.SaveAsync();
            await SxmLinqExtensions.Set(warm.GetTable<Track>().Where(x => x.id == t.id), x => x.UnitPrice, x => x.UnitPrice + 0.01m).UpdateAsync();
            await warm.RollbackTransactionAsync();
        }

        long entityMs;
        await using (var ctx = new SxmTransaction("Chinook"))
        {
            var tracks = ctx.GetTable<Track>().OrderBy(t => t.id).Take(Count).ToList();

            var sw = Stopwatch.StartNew();
            foreach (var t in tracks)
            {
                t.UnitPrice += 0.01m;
                await t.SaveAsync();
            }
            sw.Stop();
            entityMs = sw.ElapsedMilliseconds;

            await ctx.RollbackTransactionAsync();
        }

        long setBasedMs;
        int updated;
        await using (var ctx = new SxmTransaction("Chinook"))
        {
            long maxId = ctx.GetTable<Track>().OrderBy(t => t.id).Skip(Count - 1).Select(t => t.id).First();

            var sw = Stopwatch.StartNew();
            updated = await SxmLinqExtensions.Set(
                              ctx.GetTable<Track>().Where(t => t.id <= maxId),
                              t => t.UnitPrice,
                              t => t.UnitPrice + 0.01m)
                          .UpdateAsync();
            sw.Stop();
            setBasedMs = sw.ElapsedMilliseconds;

            await ctx.RollbackTransactionAsync();
        }

        return new List<BenchRow>
        {
            Bench.Row("Entity: load + SaveAsync each", Count,   entityMs,   0,        $"{Count} UPDATE statements (rolled back)"),
            Bench.Row("Set-based: Set(...).UpdateAsync()", updated, setBasedMs, entityMs, "1 UPDATE statement (rolled back)"),
            //Bench.Environment()
        };
    }
}

[QueryExample(
    id: "bench_10",
    name: "Cold vs. Warm: Query Cache Effect",
    description: "First execution of a parameterized LINQ query vs. 100 subsequent runs with varying parameters",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Build a brand-new query shape (Track by AlbumId with ordering and Take) and time its FIRST execution
2. Run the same shape 100 more times, changing the AlbumId parameter each time
3. Report cold time, warm average, and how many cold runs the warm loop would fit in
4. Also time 100 brand-new SxmTransaction open/close cycles to show connection cost

**Key Concepts:**
- The first LINQ execution pays expression parsing and SQL generation
- LinqToDB caches the translated query; parameters change without re-translating
- This is why every other benchmark here does a warm-up pass first
- Opening a connection is not free either - reuse an SxmTransaction across related work
""")]
internal sealed class Bench10Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        const int WarmIterations = 100;
        using var ctx = new SxmTransaction("Chinook");

        var albumIds = ctx.GetTable<Album>().OrderBy(a => a.id).Select(a => a.id).Take(WarmIterations).ToList();

        int QueryByAlbum(long albumId) =>
            ctx.GetTable<Track>()
               .Where(t => t.AlbumId == albumId && t.UnitPrice > 0m)
               .OrderByDescending(t => t.Milliseconds)
               .Take(25)
               .ToList()
               .Count;

        // Cold: very first execution of this query shape in this process
        var sw = Stopwatch.StartNew();
        QueryByAlbum(albumIds[0]);
        sw.Stop();
        long coldMs = sw.ElapsedMilliseconds;
        double coldTicksMs = sw.Elapsed.TotalMilliseconds;

        // Warm: same shape, different parameter each time
        sw.Restart();
        for (int i = 0; i < WarmIterations; i++) QueryByAlbum(albumIds[i % albumIds.Count]);
        sw.Stop();
        long warmMs = sw.ElapsedMilliseconds;

        // Connection churn: open/close a fresh SxmTransaction per iteration
        sw.Restart();
        for (int i = 0; i < WarmIterations; i++)
        {
            using var fresh = new SxmTransaction("Chinook");
            fresh.GetTable<Genre>().Any();
        }
        sw.Stop();
        long churnMs = sw.ElapsedMilliseconds;

        double warmAvg = (double)warmMs / WarmIterations;
        string fits = warmAvg <= 0 ? "n/a" : $"{coldTicksMs / warmAvg:0.0}x one warm run";

        return Task.FromResult<object>(new List<BenchRow>
        {
            Bench.Row("Cold (first execution)",         1,              coldMs,  0,      $"Translate + prepare + run; ~{fits}"),
            Bench.Row("Warm (100 runs, varying param)", WarmIterations, warmMs,  coldMs, "Cached translation, prepared statement"),
            Bench.Row("New SxmTransaction per query",   WarmIterations, churnMs, coldMs, "Connection open/close cost per iteration"),
            //Bench.Environment()
        });
    }
}

[QueryExample(
    id: "bench_11",
    name: "Insert at Scale: SaveAsync Loop vs. LINQ BulkInsertAsync vs. SxmSql.BulkInsertAsync (100,000 rows)",
    description: "100,000 inserts via SaveAsync inside one SxmTransaction vs. the same 100,000 via the LINQ BulkInsertAsync and via SxmSql.BulkInsertAsync. All rows are deleted afterwards.",
    category: QueryCategory.Benchmarks,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
Warm up with a single insert so JIT and connection costs are excluded

1. Scenario A: 100,000 SaveAsync calls inside one SxmTransaction
2. Scenario B: 100,000 entities passed to ctx.GetTable<Track>().BulkInsertAsync inside one SxmTransaction
3. Scenario C: 100,000 entities passed to SxmSql.BulkInsertAsync, which opens and commits its own transaction

Delete every inserted row so repeated runs do not grow the table
Entities are constructed before the Stopwatch starts, so only database work is timed

**Key Concepts:**
- At a few hundred rows the paths are indistinguishable; fixed costs (connection, BEGIN, commit fsync) dominate
- At 100,000 rows both BulkInsertAsync variants run in ~58% of the SaveAsync loop's time (e.g. 1.96 s vs 3.37 s)
- The saving is per-row overhead (prepare, last_insert_rowid, synchId UPDATE) - the B-tree/index work is identical
- Each bulk path reuses one prepared statement across batches; only parameter values are rebound
- Default is 20 rows per statement - smaller batches bind faster in Microsoft.Data.Sqlite once prepare is amortized
- All paths leave id and synchId populated on every entity
- Use the LINQ variant to take part in a larger SxmTransaction; use SxmSql.BulkInsertAsync for a standalone insert
""")]
internal sealed class Bench11Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        const int Count = 100_000;
        var marker = $"_Bench11_{Guid.NewGuid():N}";

        Track NewTrack(string suffix) => new()
        {
            Name = marker + suffix, AlbumId = 1, MediaTypeId = 1, GenreId = 1,
            Milliseconds = 180000, UnitPrice = 0.99m
        };

        // Warm-up
        await NewTrack("_warm").SaveAsync();

        List<Track> BuildTracks(string tag)
        {
            var tracks = new List<Track>(Count);
            for (int i = 0; i < Count; i++)
                tracks.Add(NewTrack($"_{tag}{i}"));
            return tracks;
        }

        // Scenario A: SaveAsync per row, one transaction
        async Task<long> RunSaveAsync(string tag)
        {
            var tracks = BuildTracks(tag);

            var sw = Stopwatch.StartNew();
            await using (var ctx = new SxmTransaction("Chinook"))
            {
                foreach (var track in tracks)
                    await track.SaveAsync();
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Scenario B: BulkInsertAsync, one transaction
        async Task<long> RunBulkAsync(string tag)
        {
            var tracks = BuildTracks(tag);

            var sw = Stopwatch.StartNew();
            await using (var ctx = new SxmTransaction("Chinook"))
            {
                await ctx.GetTable<Track>().BulkInsertAsync(tracks, 20);
            }
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        // Scenario C: SxmSql.BulkInsertAsync - standalone, opens and commits its own transaction
        async Task<long> RunSxmSqlBulkAsync(string tag)
        {
            var tracks = BuildTracks(tag);

            var sw = Stopwatch.StartNew();
            await SxmSql.BulkInsertAsync(tracks, 20);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        long saveMs1 = await RunSaveAsync("A1_");
        long bulkMs1 = await RunBulkAsync("B1_");
        long sxmSqlBulkMs1 = await RunSxmSqlBulkAsync("C1_");

        // Cleanup: remove all rows from all scenarios plus the warm-up row
        int deleted;
        await using (var cleanup = new SxmTransaction("Chinook"))
        {
            deleted = cleanup.GetTable<Track>().Where(t => t.Name.StartsWith(marker)).Delete();
            await cleanup.CommitTransactionAsync();
        }

        return new List<BenchRow>
        {
            Bench.Row("SaveAsync loop, one SxmTransaction", Count, saveMs1, 0, $"{Count} 'NewTrack.SaveAsync(...)' inside transaction"),
            Bench.Row("LINQ BulkInsertAsync, one SxmTransaction", Count, bulkMs1, saveMs1, $"One transaction. {Count} rows inserted. 20 rows inserted per statement"),
            Bench.Row("SxmSql.BulkInsertAsync, one SxmTransaction", Count, sxmSqlBulkMs1, saveMs1, $"One transaction. {Count} rows inserted. 20 rows inserted per statement"),
            //new BenchRow("Cleanup", deleted, 0, 0, "-", $"Deleted {deleted} marker rows"),
        };
    }
}
