using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.DataModification;

[QueryExample(
    id: "mod_1",
    name: "Insert New Track",
    description: "Add a single new track to the database",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create new Track instance
2. Set all required properties
3. Call SaveAsync() to insert
4. ID is auto-generated after save
5. Return confirmation with new ID

**Key Concepts:**
- Create entity, set properties, save pattern
- Primary key auto-populated after SaveAsync()
- Foreign keys (AlbumId) must reference existing records
- Timestamp in name ensures uniqueness
- Basic CRUD: Create
""")]
internal sealed class Mod1Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var track = new Track
        {
            Name = $"New Demo Track {DateTime.Now:HHmmss}",
            AlbumId = 1,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "Demo Composer",
            Milliseconds = 240000,
            Bytes = 4000000,
            UnitPrice = 1.29m,
            TrackNumber = 1
        };
        await track.SaveAsync();
        return new[] { new { Success = true, TrackId = track.id, TrackName = track.Name, Message = "Track inserted successfully" } };
    }
}

[QueryExample(
    id: "mod_2",
    name: "Insert and Get Generated ID",
    description: "Insert related records and retrieve auto-generated IDs",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Insert Artist, get auto-generated ID
2. Use Artist ID as foreign key in Album
3. Insert Album
4. Return generated IDs

**Key Concepts:**
- Auto-generated IDs available immediately after SaveAsync()
- Common parent-child insert pattern
- Foreign key relationship enforced
- Demonstrates ID propagation
""")]
internal sealed class Mod2Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var artist = new Artist { Name = $"New Artist {DateTime.Now:HHmmss}" };
        await artist.SaveAsync();
        var artistId = artist.id;

        var album = new Album { Title = "Debut Album", ArtistId = artistId };
        await album.SaveAsync();

        return new[] { new { ArtistId = artistId, ArtistName = artist.Name, AlbumId = album.id, AlbumTitle = album.Title, Message = "Artist and Album created with auto-generated IDs" } };
    }
}

[QueryExample(
    id: "mod_3",
    name: "Update Track Price",
    description: "Modify a single field on existing record",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Load existing track entity
2. Modify property (UnitPrice)
3. Call SaveAsync() to persist change
4. Only modified field updated in DB
5. Return before/after values

**Key Concepts:**
- Load, modify, save pattern
- Only changed properties updated
- No explicit UPDATE SQL needed
- SaveAsync() generates UPDATE statement
- Basic CRUD: Update
""")]
internal sealed class Mod3Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var context = new SxmTransaction("Chinook"))
        {
            var track = context.GetTable<Track>().First();
            var originalPrice = track.UnitPrice;
            track.UnitPrice = 1.99m;
            await track.SaveAsync();
            return new[] { new { TrackId = track.id, TrackName = track.Name, OriginalPrice = originalPrice, NewPrice = track.UnitPrice, Message = "Price updated successfully" } };
        }
    }
}

[QueryExample(
    id: "mod_4",
    name: "Conditional Update",
    description: "Update records matching specific criteria",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Query records matching criteria
2. Loop through results
3. Update each entity
4. SaveAsync() persists each change

**Key Concepts:**
- Filter + iterate + save pattern
- Bulk conditional updates
- Load-first pattern
- Common business rule application
""")]
internal sealed class Mod4Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var context = new SxmTransaction("Chinook"))
        {
            var cheapTracks = context.GetTable<Track>().Where(t => t.UnitPrice < 1.00m).Take(10).ToList();
            var updateCount = 0;
            foreach (var track in cheapTracks)
            {
                track.UnitPrice = 1.29m;
                await track.SaveAsync();
                updateCount++;
            }
            return new[] { new { TracksUpdated = updateCount, NewPrice = 1.29m, Message = $"Updated {updateCount} tracks to new price" } };
        }
    }
}

[QueryExample(
    id: "mod_5",
    name: "Update With Related Data",
    description: "Update entities filtered via a related lookup",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Look up related entity (Genre)
2. Query records with matching foreign key
3. Loop, calculate updated values
4. SaveAsync() persists each change

**Key Concepts:**
- Related-entity lookup + update
- Calculated updates (percentage)
- Common business logic pattern
""")]
internal sealed class Mod5Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        await using (var context = new SxmTransaction("Chinook"))
        {
            var rockGenre = context.GetTable<Genre>().FirstOrDefault(g => g.Name.Contains("Rock"));
            if (rockGenre != null)
            {
                var rockTracks = context.GetTable<Track>().Where(t => t.GenreId == rockGenre.id).Take(20).ToList();
                var updateCount = 0;
                foreach (var track in rockTracks)
                {
                    track.UnitPrice = track.UnitPrice * 1.10m;
                    await track.SaveAsync();
                    updateCount++;
                }
                return new[] { new { Genre = rockGenre.Name, TracksUpdated = updateCount, PriceIncrease = "10%", Message = $"Updated {updateCount} rock tracks" } };
            }
            return new[] { new { Message = "Rock genre not found" } };
        }
    }
}

[QueryExample(
    id: "mod_6",
    name: "Delete Single Record",
    description: "Remove a single playlist from the database",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create temporary playlist for demo
2. Save and capture auto-generated ID
3. Load entity by ID
4. Call DeleteAsync() to remove
5. Return confirmation

**Key Concepts:**
- Create test data for safe demo
- Load entity before delete
- DeleteAsync() generates DELETE SQL
- ID used to verify correct record
- Basic CRUD: Delete
""")]
internal sealed class Mod6Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var tempPlaylist = new Playlist { Name = $"SingleDeleteDemo-{DateTime.Now:HHmmss}" };
        await tempPlaylist.SaveAsync();
        var savedId = tempPlaylist.id;

        await using (var context = new SxmTransaction("Chinook"))
        {
            var playlist = context.GetTable<Playlist>().FirstOrDefault(p => p.id == savedId);
            if (playlist != null)
            {
                var playlistName = playlist.Name;
                await playlist.DeleteAsync();
                return new[] { new { Success = true, DeletedPlaylist = playlistName, SavedId = savedId, Message = "Playlist deleted successfully" } };
            }
        }

        return new[] { new { Success = false, SavedId = savedId, Message = $"Playlist with ID {savedId} not found after save" } };
    }
}

[QueryExample(
    id: "mod_7",
    name: "Conditional Delete",
    description: "Delete multiple records matching criteria using transaction",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create temporary test playlists
2. Load them by saved IDs
3. Delete each within a transaction
4. Commit atomically

**Key Concepts:**
- Bulk delete within a transaction
- Atomic delete operation
- Common data-cleanup pattern
""")]
internal sealed class Mod7Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var timestamp = DateTime.Now.Ticks;
        var savedIds = new List<long>();

        for (int i = 1; i <= 3; i++)
        {
            var playlist = new Playlist { Name = $"BulkDeleteDemo-{timestamp}-{i}" };
            await playlist.SaveAsync();
            savedIds.Add(playlist.id);
        }

        try
        {
            await using (var context = new SxmTransaction("Chinook"))
            {
                var playlistsToDelete = context.GetTable<Playlist>().Where(p => savedIds.Contains(p.id)).ToList();

                var deleteCount = 0;
                foreach (var playlist in playlistsToDelete)
                {
                    await playlist.DeleteAsync();
                    deleteCount++;
                }

                await context.CommitTransactionAsync();

                return new[] { new
                {
                    PlaylistsCreated = savedIds.Count,
                    PlaylistsDeleted = deleteCount,
                    SavedIds = string.Join(", ", savedIds),
                    Message = $"Created {savedIds.Count} and deleted {deleteCount} temporary playlists (in single transaction)"
                } };
            }
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}

[QueryExample(
    id: "mod_8",
    name: "Delete with Related Records",
    description: "Delete playlist and its junction records atomically",
    category: QueryCategory.DataModification,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Create temporary playlist with tracks
2. Load the playlist and its PlaylistTrack rows
3. Delete junction rows first (FK constraint)
4. Then delete the playlist itself
5. Commit atomically

**Key Concepts:**
- Foreign key constraint handling
- Delete child records before parent
- Transaction ensures referential integrity
- All deletes succeed or all fail
- Demonstrates cascading delete pattern
""")]
internal sealed class Mod8Example : IQueryExampleRunner
{
    public async Task<object> RunAsync()
    {
        var tempPlaylist = new Playlist { Name = $"RelatedDeleteDemo-{DateTime.Now:HHmmss}" };
        await tempPlaylist.SaveAsync();
        var playlistId = tempPlaylist.id;

        var trackIds = new[] { 1, 2, 3 };
        foreach (var trackId in trackIds)
        {
            var playlistTrack = new PlaylistTrack { PlaylistId = playlistId, TrackId = trackId };
            await playlistTrack.SaveAsync();
        }

        try
        {
            await using (var context = new SxmTransaction("Chinook"))
            {
                var playlist = context.GetTable<Playlist>().FirstOrDefault(p => p.id == playlistId);

                if (playlist != null)
                {
                    var playlistTracks = context.GetTable<PlaylistTrack>().Where(pt => pt.PlaylistId == playlist.id).ToList();

                    var trackCount = playlistTracks.Count;
                    foreach (var pt in playlistTracks)
                    {
                        await pt.DeleteAsync();
                    }

                    await playlist.DeleteAsync();

                    await context.CommitTransactionAsync();

                    return new[] { new
                    {
                        Success = true,
                        PlaylistName = playlist.Name,
                        TracksRemoved = trackCount,
                        Message = $"Created playlist with {trackCount} tracks, then deleted all in transaction"
                    } };
                }
            }
            return new[] { new { Success = false, Message = "Playlist not found after creation" } };
        }
        catch (Exception ex)
        {
            return new[] { new { Success = false, Error = ex.Message } };
        }
    }
}
