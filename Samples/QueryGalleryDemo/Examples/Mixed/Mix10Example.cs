using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_10",
    name: "End-to-end unit of work",
    description: "LINQ + Entity DML + Embedded SQL + Named SQL cooperating, then rollback",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. LINQ finds an anchor Artist
2. Entity SaveAsync creates a scratch Album and Track
3. Embedded SQL counts tracks on that new album inside the same tx
4. Named SQL fetches genre popularity for context
5. A LINQ aggregate sums UnitPrice for the album
6. RollbackTransactionAsync throws all of it away

**Key Concepts:**
- A single SxmDbContext is a unit of work spanning every query style
- The ambient transaction makes SaveAsync / embedded SQL / named SQL / LINQ interoperable
- Rollback (or an exception) atomically discards the entire mix
""")]
internal sealed class Mix10Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmDbContext("Chinook");

        // (1) LINQ read - anchor artist
        var anchor = ctx.GetTable<Artist>().OrderBy(a => a.id).First();

        // (2) Entity DML - scratch album and track
        var album = new Album { Title = "_Mix10 Album", ArtistId = anchor.id };
        await album.SaveAsync();

        var track = new Track
        {
            Name         = "_Mix10 Track",
            AlbumId      = album.id,
            MediaTypeId  = ctx.GetTable<MediaType>().Select(m => m.id).First(),
            GenreId      = ctx.GetTable<Genre>().Select(g => g.id).First(),
            Milliseconds = 150000,
            UnitPrice    = 0.99m
        };
        await track.SaveAsync();

        // (3) Embedded SQL - count tracks on this new album (sees uncommitted rows)
        var countRow = await ctx.RunStatementAsync(
            $"SELECT COUNT(*) AS Cnt FROM Track WHERE AlbumId = {album.id}",
            new Dictionary<string, object?>());

        // (4) Named SQL for context
        var genrePopularity = await ctx.RunStatementAsync(
            "GetGenrePopularity",
            new Dictionary<string, object?>());

        // (5) LINQ aggregate - confirm from a different angle
        decimal totalPrice = ctx.GetTable<Track>()
                                .Where(t => t.AlbumId == album.id)
                                .Sum(t => t.UnitPrice);

        await ctx.RollbackTransactionAsync();

        return new[]
        {
            new { Step = "Anchor artist",        Detail = anchor.Name },
            new { Step = "New album id",         Detail = album.id.ToString() },
            new { Step = "New track id",         Detail = track.id.ToString() },
            new { Step = "Embedded COUNT",       Detail = countRow.FirstOrDefault()?["Cnt"]?.ToString() ?? "?" },
            new { Step = "Named popularity",     Detail = genrePopularity.Count + " rows" },
            new { Step = "LINQ SUM(UnitPrice)",  Detail = totalPrice.ToString("N2") },
            new { Step = "Rolled back",          Detail = "Nothing persists" }
        };
    }
}
