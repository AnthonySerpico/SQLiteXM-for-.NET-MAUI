using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_1",
    name: "LINQ + Named SQL (read-only)",
    description: "Run a LINQ query and a named SQL statement inside the same SxmDbContext",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Open an SxmDbContext for the Chinook database
2. Issue a LINQ query on ctx.GetTable<Genre>()
3. Call ctx.RunStatementAsync with a named statement
4. Both share the same underlying connection via the ambient SxmSqlTransaction registered by the context

**Key Concepts:**
- A single SxmDbContext hosts multiple query styles
- Read-only work never opens a SQLite transaction (least-work)
- Named SQL enlists on the ambient transaction automatically
- await using guarantees clean async disposal
""")]
internal sealed class Mix1Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        // (1) LINQ read against the context
        var genreNames = ctx.GetTable<Genre>()
                            .OrderBy(g => g.Name)
                            .Select(g => g.Name)
                            .ToList();

        // (2) Named SQL from SqlStatements.json - enlists in the same ambient
        //     transaction registered by the SxmDbContext ctor.
        var popularity = await ctx.RunStatementAsync(
            "GetGenrePopularity",
            new Dictionary<string, object?>());

        return new[]
        {
            new { Step = "LINQ Genres",           Count = genreNames.Count },
            new { Step = "Named GenrePopularity", Count = popularity.Count }
        };
    }
}
