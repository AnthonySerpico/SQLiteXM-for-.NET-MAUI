using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_4",
    name: "Named SQL feeds LINQ",
    description: "Use the result of a named SQL statement as input to a follow-up LINQ query",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Named SQL returns aggregate data (top genres)
2. Project the names into a plain List<string>
3. LINQ resolves genre ids, then pulls tracks from the same ctx

**Key Concepts:**
- Named SQL and LINQ compose naturally inside one ctx
- All statements share the same connection
- No transaction is opened - both statements are pure reads
""")]
internal sealed class Mix4Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        // (1) Named SQL - top genres by popularity
        var popularity = await ctx.RunStatementAsync(
            "GetGenrePopularity",
            new Dictionary<string, object?>());

        var topGenreNames = popularity
            .Take(3)
            .Select(r => r["Genre"]?.ToString())
            .Where(n => n != null)
            .ToList();

        // (2) LINQ - resolve ids, then pull tracks via the same ctx
        var topGenreIds = ctx.GetTable<Genre>()
                             .Where(g => topGenreNames.Contains(g.Name))
                             .Select(g => g.id)
                             .ToList();

        var tracks = ctx.GetTable<Track>()
                        .Where(t => t.GenreId != null && topGenreIds.Contains(t.GenreId.Value))
                        .OrderBy(t => t.Name)
                        .Take(10)
                        .Select(t => new { t.Name, t.GenreId, t.UnitPrice })
                        .ToList();

        return tracks;
    }
}
