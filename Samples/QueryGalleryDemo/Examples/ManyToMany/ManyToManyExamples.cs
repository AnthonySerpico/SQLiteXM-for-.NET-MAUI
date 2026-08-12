using LinqToDB;
using QueryGalleryDemo.Examples;
using QueryGalleryDemo.Models;
using SQLiteXM;

namespace QueryGalleryDemo.Examples.ManyToMany;

[QueryExample(
    id: "m2m_1",
    name: "Tracks in a Playlist",
    description: "Query many-to-many relationship through junction table with single query",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Playlist table
2. JOIN to PlaylistTrack (junction table)
3. JOIN to Track table
4. Filter playlists by name containing 'Music'
5. Sort tracks alphabetically
6. Take 50 tracks

**Key Concepts:**
- Many-to-many requires junction table (PlaylistTrack)
- Two joins navigate the relationship
- Junction table links Playlist <-> Track
""")]
internal sealed class M2M1Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var tracks = (from playlist in context.GetTable<Playlist>()
                      join pt in context.GetTable<PlaylistTrack>() on playlist.id equals pt.PlaylistId
                      join track in context.GetTable<Track>() on pt.TrackId equals track.id
                      where playlist.Name.Contains("Music")
                      orderby track.Name
                      select track)
                     .Take(50)
                     .ToList();
        return Task.FromResult<object>(tracks);
    }
}

[QueryExample(
    id: "m2m_2",
    name: "Playlists Containing Track",
    description: "Reverse query: find all playlists with a specific track using single query",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Start with Track table
2. JOIN to PlaylistTrack (junction)
3. JOIN to Playlist table
4. Filter tracks by name
5. Return all matching playlists

**Key Concepts:**
- Reverse navigation of M:N relationship
- Same junction table, different direction
""")]
internal sealed class M2M2Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var playlists = (from track in context.GetTable<Track>()
                         join pt in context.GetTable<PlaylistTrack>() on track.id equals pt.TrackId
                         join playlist in context.GetTable<Playlist>() on pt.PlaylistId equals playlist.id
                         where track.Name.Contains("Track")
                         select playlist)
                        .ToList();
        return Task.FromResult<object>(playlists);
    }
}

[QueryExample(
    id: "m2m_3",
    name: "Playlist Statistics",
    description: "Aggregate data across many-to-many relationship",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. LEFT JOIN Playlist to PlaylistTrack
2. LEFT JOIN to Track
3. GROUP BY playlist
4. COUNT tracks (excluding nulls)
5. SUM duration (excluding nulls)

**Key Concepts:**
- LEFT JOIN with DefaultIfEmpty()
- Aggregation across M:N relationship
- Null-safe counting and summing
""")]
internal sealed class M2M3Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var results = (from playlist in context.GetTable<Playlist>()
                       join pt in context.GetTable<PlaylistTrack>() on playlist.id equals pt.PlaylistId into playlistTracks
                       from pt in playlistTracks.DefaultIfEmpty()
                       join track in context.GetTable<Track>() on pt.TrackId equals track.id into tracks
                       from track in tracks.DefaultIfEmpty()
                       group track by new { playlist.id, playlist.Name } into g
                       select new
                       {
                           PlaylistName = g.Key.Name,
                           TrackCount = g.Count(t => t != null),
                           TotalDurationMinutes = g.Where(t => t != null).Sum(t => t.Milliseconds) / 1000 / 60
                       })
                      .OrderByDescending(x => x.TrackCount)
                      .ToList();
        return Task.FromResult<object>(results);
    }
}

[QueryExample(
    id: "m2m_4",
    name: "Tracks Shared Between Playlists",
    description: "Find tracks that appear in multiple playlists (SQLite-compatible)",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Fetch track-playlist pairs from junction
2. Materialize to memory (SQLite limitation)
3. GROUP BY track
4. COUNT distinct playlists per track
5. Filter tracks in 2+ playlists

**Key Concepts:**
- Two-phase query for SQLite compatibility
- Distinct().Count() done in memory
""")]
internal sealed class M2M4Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var trackPlaylistGroups = (from pt in context.GetTable<PlaylistTrack>()
                                   join track in context.GetTable<Track>() on pt.TrackId equals track.id
                                   select new
                                   {
                                       TrackId = track.id,
                                       TrackName = track.Name,
                                       PlaylistId = pt.PlaylistId
                                   }).ToList();

        var sharedTracks = trackPlaylistGroups
            .GroupBy(x => new { x.TrackId, x.TrackName })
            .Select(g => new
            {
                TrackName = g.Key.TrackName,
                PlaylistCount = g.Select(x => x.PlaylistId).Distinct().Count()
            })
            .Where(x => x.PlaylistCount > 1)
            .OrderByDescending(x => x.PlaylistCount)
            .Take(30)
            .ToList();
        return Task.FromResult<object>(sharedTracks);
    }
}

[QueryExample(
    id: "m2m_5",
    name: "Popular Tracks in Playlists",
    description: "Count how many playlists each track appears in (optimized)",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. JOIN PlaylistTrack -> Track -> Album -> Artist
2. Fetch all relationships to memory
3. GROUP BY track and artist
4. COUNT distinct playlists per track

**Key Concepts:**
- Multi-table join through M:N relationship
- Two-phase for SQLite performance
""")]
internal sealed class M2M5Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var trackData = (from pt in context.GetTable<PlaylistTrack>()
                         join track in context.GetTable<Track>() on pt.TrackId equals track.id
                         join album in context.GetTable<Album>() on track.AlbumId equals album.id
                         join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
                         select new
                         {
                             TrackId = track.id,
                             TrackName = track.Name,
                             ArtistName = artist.Name,
                             PlaylistId = pt.PlaylistId
                         }).ToList();

        var popularTracks = trackData
            .GroupBy(x => new { x.TrackId, x.TrackName, x.ArtistName })
            .Select(g => new
            {
                TrackName = g.Key.TrackName,
                ArtistName = g.Key.ArtistName,
                PlaylistCount = g.Select(x => x.PlaylistId).Distinct().Count()
            })
            .OrderByDescending(x => x.PlaylistCount)
            .Take(20)
            .ToList();
        return Task.FromResult<object>(popularTracks);
    }
}

[QueryExample(
    id: "m2m_6",
    name: "Playlists with Few Tracks",
    description: "Find playlists with fewer than 250 tracks (optimized)",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Count tracks per playlist from junction table
2. Materialize counts
3. Load playlists
4. JOIN in memory
5. Filter playlists with < 250 tracks

**Key Concepts:**
- Two-phase query: aggregate then filter
- GROUP BY on junction table
""")]
internal sealed class M2M6Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var playlistCounts = (from pt in context.GetTable<PlaylistTrack>()
                              group pt by pt.PlaylistId into g
                              select new
                              {
                                  PlaylistId = g.Key,
                                  TrackCount = g.Count()
                              }).ToList();

        var playlists = context.GetTable<Playlist>().ToList();

        var smallPlaylists = (from pc in playlistCounts
                              join p in playlists on pc.PlaylistId equals p.id
                              where pc.TrackCount < 250
                              orderby pc.TrackCount
                              select new
                              {
                                  Name = p.Name,
                                  TrackCount = pc.TrackCount
                              })
                             .Take(20)
                             .ToList();
        return Task.FromResult<object>(smallPlaylists);
    }
}

[QueryExample(
    id: "m2m_7",
    name: "Add Track to Playlist",
    description: "Insert into junction table (many-to-many relationship)",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works (Pattern):**
1. Get Playlist and Track
2. Create new PlaylistTrack junction record
3. Set both foreign keys
4. Call SaveAsync() to persist

**Key Concepts:**
- M:N 'add relationship' = insert into junction table
- No modification to Playlist or Track entities
""")]
internal sealed class M2M7Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var playlist = context.GetTable<Playlist>().FirstOrDefault();
        var track = context.GetTable<Track>().FirstOrDefault();

        object result;
        if (playlist != null && track != null)
        {
            // In a real app: var newEntry = new PlaylistTrack { PlaylistId = playlist.id, TrackId = track.id };
            // await newEntry.SaveAsync();
            result = new[] { new { Message = "Pattern: Create PlaylistTrack with both IDs and SaveAsync()" } };
        }
        else
        {
            result = new[] { new { Message = "No data to demo with" } };
        }
        return Task.FromResult<object>(result);
    }
}

[QueryExample(
    id: "m2m_8",
    name: "Playlist Overlap Analysis",
    description: "Find which playlists share the most tracks",
    category: QueryCategory.ManyToMany,
    type: QueryType.Linq,
    explanation: """
**How It Works:**
1. Self-join PlaylistTrack on TrackId
2. Filter where PlaylistId1 < PlaylistId2 (avoid duplicates)
3. JOIN to Playlist table twice for names
4. GROUP BY playlist pair
5. COUNT shared tracks

**Key Concepts:**
- Self-join pattern on junction table
- Finds M:N overlap/similarity
""")]
internal sealed class M2M8Example : IQueryExampleRunner
{
    public Task<object> RunAsync()
    {
        using var context = new SxmDbContext("Chinook");
        var playlistPairs = (from pt1 in context.GetTable<PlaylistTrack>()
                             join pt2 in context.GetTable<PlaylistTrack>() on pt1.TrackId equals pt2.TrackId
                             where pt1.PlaylistId < pt2.PlaylistId
                             join p1 in context.GetTable<Playlist>() on pt1.PlaylistId equals p1.id
                             join p2 in context.GetTable<Playlist>() on pt2.PlaylistId equals p2.id
                             group pt1 by new { Playlist1Name = p1.Name, Playlist2Name = p2.Name } into g
                             select new
                             {
                                 Playlist1 = g.Key.Playlist1Name,
                                 Playlist2 = g.Key.Playlist2Name,
                                 SharedTracks = g.Count()
                             })
                            .OrderByDescending(x => x.SharedTracks)
                            .Take(10)
                            .ToList();
        return Task.FromResult<object>(playlistPairs);
    }
}
