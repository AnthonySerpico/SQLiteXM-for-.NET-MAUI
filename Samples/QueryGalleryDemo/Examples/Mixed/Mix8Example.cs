using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_8",
    name: "Auto-rollback on exception",
    description: "An exception mid-context aborts all mixed work automatically on dispose",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. Entity DML and Named SQL run under a shared context
2. An uncaught exception escapes the context body
3. DisposeAsync detects the failed state and rolls back
4. A fresh context queries and finds none of the marker rows

**Key Concepts:**
- await using guarantees async disposal even on exception
- Failure -> rollback; success -> commit
- Rollback covers every statement executed on the context
""")]
internal sealed class Mix8Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var marker = $"_Mix8_{Guid.NewGuid():N}";
        try
        {
            await using var ctx = new SxmTransaction("Chinook");

            // Entity DML
            await new Artist { Name = marker }.SaveAsync();

            // Named SQL
            _ = await ctx.RunStatementAsync(
                "GetGenrePopularity",
                new Dictionary<string, object?>());

            // Force a failure BEFORE any commit
            throw new InvalidOperationException("Simulated failure - triggers auto-rollback on dispose");
        }
        catch (Exception ex)
        {
            // On dispose, the context detected the error and rolled back.
            await using var probe = new SxmTransaction("Chinook");
            int survivors = probe.GetTable<Artist>().Count(a => a.Name == marker);

            return new[] { new
            {
                Caught = ex.Message,
                MarkerRowsPersisted = survivors,
                Note = "Zero survivors proves the mix was rolled back automatically"
            } };
        }
    }
}
