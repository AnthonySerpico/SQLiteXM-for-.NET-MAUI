using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_5",
    name: "LINQ read + Entity DML + Embedded UPDATE",
    description: "Find genre via LINQ, insert Track, bump price with embedded SQL, verify, rollback",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. LINQ locates the Rock genre
2. Entity SaveAsync inserts a scratch track
3. Embedded UPDATE lifts the price
4. LINQ confirms the new value inside the same transaction
5. Rollback removes both the insert and the update

**Key Concepts:**
- LINQ, Entity DML, and embedded SQL freely interleave
- All observe each other's writes because they share one connection/tx
- Rollback discards every statement in the tx
""")]
internal sealed class Mix5Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        // (1) LINQ read
        var rock = ctx.GetTable<Genre>().FirstOrDefault(g => g.Name == "Rock");
        if (rock == null) return new[] { new { Error = "Rock genre missing" } };

        // (2) Entity DML - scratch track
        var track = new Track
        {
            Name         = "Mix Demo Track",
            AlbumId      = ctx.GetTable<Album>().Select(a => a.id).First(),
            MediaTypeId  = ctx.GetTable<MediaType>().Select(m => m.id).First(),
            GenreId      = rock.id,
            Milliseconds = 200000,
            UnitPrice    = 0.99m
        };
        await track.SaveAsync();

        // (3) Embedded SQL - UPDATE the price
        await ctx.RunStatementAsync(
            $"UPDATE Track SET UnitPrice = 2.99 WHERE id = {track.id}",
            new Dictionary<string, object?>());

        // (4) LINQ verify the update
        var updated = ctx.GetTable<Track>()
                         .Where(t => t.id == track.id)
                         .Select(t => new { t.id, t.Name, t.UnitPrice })
                         .First();

        await ctx.RollbackTransactionAsync();

        return new object[] { new { Error = (string?)null, updated.id, updated.Name, updated.UnitPrice } };
    }
}
