using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_7",
    name: "Rollback discards mixed work",
    description: "Entity DML + LINQ bulk update + Named SQL, then RollbackTransactionAsync",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Entity DML inserts scratch rows
2. LINQ bulk Update renames them via a single SQL statement
3. Named SQL executes read-only work in the same tx
4. RollbackTransactionAsync undoes inserts AND updates atomically
5. A fresh LINQ COUNT confirms zero survivors

**Key Concepts:**
- Rollback is all-or-nothing across every statement in the tx
- LINQ bulk writes participate in the ambient tx just like entity DML
- Named SQL reads see uncommitted work
""")]
internal sealed class Mix7Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        var marker = $"_Mix7_{Guid.NewGuid():N}";

        // (1) Entity DML - two scratch artists
        await new Artist { Name = marker + "_A" }.SaveAsync();
        await new Artist { Name = marker + "_B" }.SaveAsync();

        // (2) LINQ bulk UPDATE - rename them
        int renamed = await SQLiteXM.SxmLinqExtensions.Set(
                          ctx.GetTable<Artist>().Where(a => a.Name.StartsWith(marker)),
                          a => a.Name,
                          a => a.Name + "_renamed")
                      .UpdateAsync();

        // (3) Named SQL runs in the same tx
        var popularity = await ctx.RunStatementAsync(
            "GetGenrePopularity",
            new Dictionary<string, object?>());

        // (4) Roll everything back
        await ctx.RollbackTransactionAsync();

        // (5) After rollback, none of the marker rows survive
        int survivors = ctx.GetTable<Artist>().Count(a => a.Name.StartsWith(marker));

        return new[]
        {
            new { Step = "Bulk renamed",   Detail = renamed.ToString() },
            new { Step = "Named SQL rows", Detail = popularity.Count.ToString() },
            new { Step = "After rollback", Detail = $"MarkerRowsRemaining={survivors}" }
        };
    }
}
