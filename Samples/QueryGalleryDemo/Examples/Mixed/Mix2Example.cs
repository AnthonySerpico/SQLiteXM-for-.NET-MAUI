using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_2",
    name: "LINQ read + Entity DML + Rollback",
    description: "Insert an entity, observe it via LINQ within the same tx, then roll back",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. LINQ COUNT gives the starting artist count
2. new Artist().SaveAsync() enlists on the ambient tx registered by the context
3. A second LINQ COUNT sees the uncommitted row (read-your-writes)
4. RollbackTransactionAsync discards everything before dispose

**Key Concepts:**
- Parameterless SaveAsync() picks up SxmAmbientTransaction.Current
- LINQ reads see uncommitted writes in the same context
- Explicit rollback keeps the demo DB clean between runs
""")]
internal sealed class Mix2Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        // (1) LINQ read - baseline count
        int before = ctx.GetTable<Artist>().Count();

        // (2) Entity DML using the parameterless ambient pattern
        var artist = new Artist { Name = $"_MixDemo_{Guid.NewGuid():N}" };
        await artist.SaveAsync();

        // (3) LINQ read again - the new row is visible inside the same tx
        int during = ctx.GetTable<Artist>().Count();

        // (4) Discard the work explicitly - nothing persists after dispose
        await ctx.RollbackTransactionAsync();

        return new[]
        {
            new { Step = "Before insert",        Detail = before.ToString() },
            new { Step = "After insert (in tx)", Detail = during.ToString() },
            new { Step = "Rolled back",          Detail = "Row discarded on RollbackTransactionAsync" }
        };
    }
}
