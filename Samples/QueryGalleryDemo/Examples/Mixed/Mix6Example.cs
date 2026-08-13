using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Mixed;

[QueryExample(
    id: "mix_6",
    name: "Explicit Commit mid-context",
    description: "Commit early, then continue with a fresh transaction on the same context",
    category: QueryCategory.MixedContext,
    type: QueryType.Mixed,
    explanation: """
**How It Works:**
1. SaveAsync writes inside the first auto-started transaction
2. CommitTransactionAsync ends that tx early
3. A follow-up LINQ bulk Delete starts a fresh transaction under the same context
4. A second CommitTransactionAsync finalizes the cleanup

**Key Concepts:**
- A single SxmTransaction can span multiple sequential transactions
- Explicit commit is optional - dispose auto-commits when no errors occurred
- LINQ bulk Update/Delete lazily starts a transaction on the first write
""")]
internal sealed class Mix6Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using var ctx = new SxmTransaction("Chinook");

        var uniqueName = $"_Mix6_{Guid.NewGuid():N}";

        // (1) Entity DML in the first transaction
        var artist = new Artist { Name = uniqueName };
        await artist.SaveAsync();

        // (2) Commit early - subsequent writes open a NEW transaction
        await ctx.CommitTransactionAsync();

        // (3) LINQ read - visible because it's already committed
        bool visible = ctx.GetTable<Artist>().Any(a => a.Name == uniqueName);

        // (4) LINQ bulk DELETE removes the row in the new tx
        int deleted = ctx.GetTable<Artist>()
                         .Where(a => a.Name == uniqueName)
                         .Delete();

        // (5) Commit the cleanup
        await ctx.CommitTransactionAsync();

        return new[]
        {
            new { Step = "After first commit", Detail = $"VisibleToLinq={visible}, ArtistId={artist.id}" },
            new { Step = "Cleanup",            Detail = $"DeletedRows={deleted}" }
        };
    }
}
