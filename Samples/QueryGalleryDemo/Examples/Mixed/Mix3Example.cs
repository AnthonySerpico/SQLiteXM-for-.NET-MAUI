using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_3",
    name: "Entity DML + Embedded SQL verify",
    description: "Insert Artist and Album, verify with an embedded SELECT COUNT(*), rollback",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. SaveAsync inserts Artist, then Album referencing the new id
2. Embedded SQL (literal text passed to RunStatementAsync) executes on the same connection
3. Rollback ensures nothing persists

**Key Concepts:**
- Embedded SQL is raw SQL text dispatched by RunStatementAsync
- It sees uncommitted rows written by SaveAsync
- All three statements share one transaction
""")]
internal sealed class Mix3Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        // (1) Entity DML - parent
        var artist = new Artist { Name = $"_Mix3_{Guid.NewGuid():N}" };
        await artist.SaveAsync();

        // (2) Entity DML - child
        var album = new Album { Title = "Mix Demo Album", ArtistId = artist.id };
        await album.SaveAsync();

        // (3) Embedded SQL - literal text passed to RunStatementAsync
        var rows = await ctx.RunStatementAsync(
            $"SELECT COUNT(*) AS ArtistMatches FROM Artist WHERE id = {artist.id}",
            new Dictionary<string, object?>());

        await ctx.RollbackTransactionAsync();

        return new[]
        {
            new { Step = "Inserted",       Detail = $"ArtistId={artist.id}, AlbumId={album.id}" },
            new { Step = "Embedded COUNT", Detail = rows.FirstOrDefault()?["ArtistMatches"]?.ToString() ?? "0" }
        };
    }
}
