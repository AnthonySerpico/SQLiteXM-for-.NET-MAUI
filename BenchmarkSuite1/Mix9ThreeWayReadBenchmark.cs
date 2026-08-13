using BenchmarkDotNet.Attributes;
using LinqToDB;
using SQLiteXM;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VSDiagnostics;

namespace SQLiteXM.Benchmarks;
[CPUUsageDiagnoser]
public class Mix9ThreeWayReadBenchmark
{
    private const string DbName = "bench_db";
    private static string _folder = string.Empty;
    private static string _jsonPath = string.Empty;
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class BenchArtist : SxmEntity
    {
        public string? Name { get; set; }
    }

    [GlobalSetup]
    public void Setup()
    {
        _folder = Path.Combine(Path.GetTempPath(), "SQLiteXM.Bench.Mix9_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _jsonPath = Path.Combine(_folder, "statements.json");
        // Config with a named SELECT statement so RunStatementAsync("GetBenchArtists", ...) resolves.
        string json = $$"""
        {
          "version": 1,
          "databases": [ { "database": "{{DbName}}", "isDefault": true } ],
          "select": [
            {
              "Statement Name": "GetBenchArtists",
              "Statement": "SELECT id, Name FROM BenchArtist ORDER BY id LIMIT 30",
              "Table Name": "BenchArtist"
            }
          ]
        }
        """;
        File.WriteAllText(_jsonPath, json);
        var opts = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = _folder
        };
        using (var stream = File.OpenRead(_jsonPath))
        {
            SxmDatabase.InitializeAsync(stream, opts).GetAwaiter().GetResult();
        }

        SxmDatabase.RegisterEntitiesAsync(typeof(BenchArtist)).GetAwaiter().GetResult();
        // Seed 100 rows so the query has real work to do.
        SeedAsync().GetAwaiter().GetResult();
        // Warm-up: run the same block once so LinqToDB expression/materializer JIT,
        // SqliteConnection first-open, and any static caches are all primed BEFORE measurement.
        RunMix9BlockAsync().GetAwaiter().GetResult();
        RunMix9BlockAsync().GetAwaiter().GetResult();
    }

    private static async Task SeedAsync()
    {
        await using var ctx = new SxmTransaction(DbName);
        for (int i = 0; i < 100; i++)
        {
            var a = new BenchArtist
            {
                Name = "A_" + i
            };
            await a.SaveAsync();
        }
    }

    // Reproduces Mix9Example.RunAsync exactly:
    //   LINQ Count + named RunStatementAsync + embedded RunStatementAsync
    private static async Task RunMix9BlockAsync()
    {
        await using var ctx = new SxmTransaction(DbName);
        int albumCount = ctx.GetTable<BenchArtist>().Count();
        var artistRevenue = await ctx.RunStatementAsync("GetBenchArtists", new Dictionary<string, object?>());
        var trackRow = await ctx.RunStatementAsync("SELECT COUNT(*) AS TrackCount FROM BenchArtist", new Dictionary<string, object?>());
        // Prevent DCE
        if (albumCount < 0 || artistRevenue == null || trackRow == null)
            throw new InvalidOperationException();
    }

    // Fast-path comparison: Mix10-style block (LINQ + entity DML + LINQ + rollback), NO RunStatementAsync.
    private static async Task RunFastBlockAsync()
    {
        await using var ctx = new SxmTransaction(DbName);
        int before = ctx.GetTable<BenchArtist>().Count();
        var a = new BenchArtist
        {
            Name = "_bench_" + Guid.NewGuid().ToString("N")
        };
        await a.SaveAsync();
        int during = ctx.GetTable<BenchArtist>().Count();
        await ctx.RollbackTransactionAsync();
        if (before < 0 || during < 0)
            throw new InvalidOperationException();
    }

    [Benchmark(Description = "Mix9: LINQ + Named SQL + Embedded SQL (slow path)")]
    public async Task ThreeWayRead() => await RunMix9BlockAsync();
    [Benchmark(Description = "Mix10-style: LINQ + Entity + LINQ + Rollback (fast path)")]
    public async Task EntityDmlRollback() => await RunFastBlockAsync();
}