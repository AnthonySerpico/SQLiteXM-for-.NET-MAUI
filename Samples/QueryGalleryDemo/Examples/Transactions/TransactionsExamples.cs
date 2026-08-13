using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.Transactions;

[QueryExample(
    id: "trans_1",
    name: "Basic Transaction - Insert Invoice with Lines",
    description: "Insert invoice + invoice lines atomically",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create SxmTransaction (await using for auto-dispose)
2. Insert invoice record
3. Get generated invoice.id
4. Insert invoice lines referencing invoice.id
5. CommitTransactionAsync() or auto-commit on dispose
6. On error, transaction auto-rolls back

**Key Concepts:**
- ACID transactions ensure all-or-nothing
- Multiple inserts execute atomically
- SaveAsync() enlists in the ambient transaction
- Auto-rollback on exception
- Critical for data integrity with related records
""")]
internal sealed class Trans1Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var transaction = new SxmTransaction("Chinook"))
        try
        {
            var invoice = new Invoice
            {
                CustomerId = 1,
                InvoiceDate = DateTime.Now,
                BillingAddress = "123 Demo St",
                BillingCity = "Portland",
                BillingCountry = "USA",
                Total = 5.97m
            };
            await invoice.SaveAsync();

            var line1 = new InvoiceLine { InvoiceId = invoice.id, TrackId = 1, UnitPrice = 1.99m, Quantity = 1 };
            await line1.SaveAsync();

            var line2 = new InvoiceLine { InvoiceId = invoice.id, TrackId = 2, UnitPrice = 1.99m, Quantity = 2 };
            await line2.SaveAsync();

            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, InvoiceId = invoice.id, TotalAmount = invoice.Total, LineCount = 2 } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

[QueryExample(
    id: "trans_2",
    name: "Transaction Rollback on Error",
    description: "Demonstrate automatic rollback when error occurs",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Begin transaction
2. Insert artist successfully
3. Insert album successfully
4. Exception is thrown
5. Control jumps to catch block
6. Transaction auto-rolls back on dispose
7. Neither artist nor album persists

**Key Concepts:**
- Automatic rollback on uncaught exceptions
- 'await using' ensures proper cleanup
- All operations undone if ANY fails
- Database remains consistent
- No explicit RollbackAsync() needed
""")]
internal sealed class Trans2Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var transaction = new SxmTransaction("Chinook"))
        try
        {
            var artist = new Artist { Name = "Transaction Test Artist" };
            await artist.SaveAsync();

            var album = new Album { Title = "Test Album", ArtistId = artist.id };
            await album.SaveAsync();

            throw new Exception("Simulated error - all changes will be rolled back");

#pragma warning disable CS0162
            await transaction.CommitTransactionAsync();
            return new[] { new { Success = true } };
#pragma warning restore CS0162
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message, Note = "All changes were rolled back" } };
        }
    }
}

[QueryExample(
    id: "trans_3",
    name: "Batch Insert with Transaction",
    description: "Efficiently insert multiple tracks in one transaction",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Begin transaction
2. Loop 50 times
3. Each SaveAsync() adds to transaction
4. All inserts buffered
5. CommitTransactionAsync() writes all at once
6. Measure total time

**Key Concepts:**
- Transactions dramatically improve batch insert speed
- Single commit vs 50 individual commits
- Reduces disk I/O and locking overhead
- Can be 10-100x faster than individual saves
- Essential for bulk data operations
""")]
internal sealed class Trans3Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var transaction = new SxmTransaction("Chinook"))
        try
        {
            var insertedCount = 0;
            var startTime = DateTime.Now;

            for (int i = 1; i <= 50; i++)
            {
                var track = new Track
                {
                    Name = $"Batch Track {i}",
                    AlbumId = 1,
                    MediaTypeId = 1,
                    GenreId = 1,
                    Milliseconds = 180000,
                    UnitPrice = 0.99m
                };
                await track.SaveAsync();
                insertedCount++;
            }

            await transaction.CommitTransactionAsync();

            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            return new[] { new { Success = true, TracksInserted = insertedCount, ElapsedMs = elapsed, Note = "All inserts in single transaction" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

[QueryExample(
    id: "trans_4",
    name: "Update Multiple Tables in Transaction",
    description: "Update artist and all their albums atomically",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Begin transaction
2. Query artist and their albums
3. Update artist name
4. Loop through albums and update titles
5. Commit all updates atomically
6. On error, all updates roll back

**Key Concepts:**
- Multi-table updates in single transaction
- Ensures consistency across related tables
- All updates succeed together or fail together
- Prevents partial updates
- Critical for maintaining referential integrity
""")]
internal sealed class Trans4Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var transaction = new SxmTransaction("Chinook"))
        try
        {
            var artist = transaction.GetTable<Artist>().First();
            var albums = transaction.GetTable<Album>().Where(a => a.ArtistId == artist.id).Take(3).ToList();

            var originalName = artist.Name;
            artist.Name = artist.Name + " (Updated)";
            await artist.SaveAsync();

            foreach (var album in albums)
            {
                album.Title = album.Title + " [Remastered]";
                await album.SaveAsync();
            }

            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, ArtistOriginal = originalName, ArtistUpdated = artist.Name, AlbumsUpdated = albums.Count } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

[QueryExample(
    id: "trans_5",
    name: "Complex Multi-Table Transaction",
    description: "Create playlist, add tracks, update statistics",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Begin transaction
2. Create new playlist (get generated ID)
3. Query top 10 tracks
4. Loop: insert PlaylistTrack junction records
5. All 11 inserts (1 playlist + 10 junction) atomic
6. Commit once

**Key Concepts:**
- Complex workflow with multiple steps
- Parent record created first
- Generated ID used in child records
- M:N relationship populated atomically
- Real-world pattern for composite operations
""")]
internal sealed class Trans5Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var transaction = new SxmTransaction("Chinook"))
        try
        {
            var playlist = new Playlist { Name = $"Transaction Demo Playlist {DateTime.Now:HHmmss}" };
            await playlist.SaveAsync();

            var topTracks = transaction.GetTable<Track>().OrderBy(t => t.Name).Take(10).ToList();

            var trackCount = 0;
            foreach (var track in topTracks)
            {
                var pt = new PlaylistTrack { PlaylistId = playlist.id, TrackId = track.id };
                await pt.SaveAsync();
                trackCount++;
            }

            await transaction.CommitTransactionAsync();

            return new[] { new { Success = true, PlaylistId = playlist.id, PlaylistName = playlist.Name, TracksAdded = trackCount, Note = "All operations committed together" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

[QueryExample(
    id: "trans_6",
    name: "Transaction vs No Transaction Performance",
    description: "Compare performance: transaction vs individual saves",
    category: QueryCategory.Transactions,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Method 1: 20 inserts without transaction (20 commits)
2. Measure time
3. Method 2: 20 inserts with transaction (1 commit)
4. Measure time
5. Calculate speedup factor
6. Return comparison

**Key Concepts:**
- Transactions provide massive performance gains
- Without transaction: each save = disk write
- With transaction: batch all writes
- Typical speedup: 10-50x faster
- Always use transactions for batch operations
""")]
internal sealed class Trans6Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var results = new List<object>();

        var start1 = DateTime.Now;
        for (int i = 1; i <= 20; i++)
        {
            var track = new Track { Name = $"No-Transaction Track {i}", AlbumId = 1, MediaTypeId = 1, GenreId = 1, Milliseconds = 180000, UnitPrice = 0.99m };
            await track.SaveAsync();
        }
        var noTransTime = (DateTime.Now - start1).TotalMilliseconds;

        var start2 = DateTime.Now;
        await using (var transaction = new SxmTransaction("Chinook"))
        {
            for (int i = 1; i <= 20; i++)
            {
                var track = new Track { Name = $"Transaction Track {i}", AlbumId = 1, MediaTypeId = 1, GenreId = 1, Milliseconds = 180000, UnitPrice = 0.99m };
                await track.SaveAsync();
            }
            await transaction.CommitTransactionAsync();
        }
        var transTime = (DateTime.Now - start2).TotalMilliseconds;

        results.Add(new { Method = "Without Transaction", Inserts = 20, TimeMs = noTransTime });
        results.Add(new { Method = "With Transaction", Inserts = 20, TimeMs = transTime, SpeedupFactor = Math.Round(noTransTime / transTime, 2) });

        return results;
    }
}
