using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_9",
    name: "Three-way read: LINQ + Named SQL + Embedded SQL",
    description: "All-reads sample. Confirms one connection is shared; no transaction opened",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. LINQ COUNT runs against the ctx's DataConnection
2. Named SQL invokes a JSON-registered statement
3. Embedded SQL is a literal SELECT passed as text

**Key Concepts:**
- Read-only means no SQLite BEGIN issued (least-work)
- Every read still goes through the same connection
- Illustrates the three read paths side by side

**Why This Example Runs Slower (1 - 2+ second):**
The named `GetArtistRevenue` statement is intentionally heavy — it joins **Artist -> Album -> Track -> InvoiceLine** (three chained LEFT JOINs) across the full Chinook dataset (~211 artists, ~17.6K albums, ~154K tracks, ~352K invoice lines). SQLite then builds **two temporary B-trees** for the `COUNT(DISTINCT al.id)` and `COUNT(DISTINCT t.id)` aggregates, and a **third temp B-tree** for `ORDER BY TotalRevenue`. Even with every foreign key indexed, that's roughly **~500K index probes plus ~500K B-tree insertions** — pure SQLite execution cost. The SQLiteXM framework overhead for the same call measures ~25 ms in isolation; the rest is the query doing legitimate work on a large dataset.
""")]
internal sealed class Mix9Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        // (1) LINQ
        int albumCount = ctx.GetTable<Album>().Count();

        // (2) Named SQL
        var artistRevenue = await ctx.RunStatementAsync(
            "GetArtistRevenue",
            new Dictionary<string, object?>());

        // (3) Embedded SQL
        var trackRow = await ctx.RunStatementAsync(
            "SELECT COUNT(*) AS TrackCount FROM Track",
            new Dictionary<string, object?>());

        return new[]
        {
            new { Source = "LINQ Count<Album>",       Value = albumCount.ToString() },
            new { Source = "Named GetArtistRevenue",  Value = artistRevenue.Count + " rows" },
            new { Source = "Embedded COUNT(*) Track", Value = trackRow.FirstOrDefault()?["TrackCount"]?.ToString() ?? "0" }
        };
    }
}
