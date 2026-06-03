using QueryGalleryDemo.Models;

namespace QueryGalleryDemo.Services;

/// <summary>
/// Provides all query examples organized by category
/// </summary>
public static class QueryExampleProvider
{
    public static List<QueryExample> GetAllExamples()
    {
        var examples = new List<QueryExample>();

        // Add examples from all categories
        examples.AddRange(GetBasicQueryExamples());
        examples.AddRange(GetRelationshipQueryExamples());
        examples.AddRange(GetAggregationQueryExamples());
        examples.AddRange(GetAdvancedLinqExamples());
        examples.AddRange(GetRawSqlExamples());
        examples.AddRange(GetPerformanceExamples());
        examples.AddRange(GetManyToManyExamples());

        return examples;
    }

    public static List<QueryExample> GetExamplesByCategory(QueryCategory category)
    {
        return category switch
        {
            QueryCategory.Basic => GetBasicQueryExamples(),
            QueryCategory.Relationships => GetRelationshipQueryExamples(),
            QueryCategory.Aggregations => GetAggregationQueryExamples(),
            QueryCategory.AdvancedLinq => GetAdvancedLinqExamples(),
            QueryCategory.RawSql => GetRawSqlExamples(),
            QueryCategory.Performance => GetPerformanceExamples(),
            QueryCategory.ManyToMany => GetManyToManyExamples(),
            _ => new List<QueryExample>()
        };
    }

    private static List<QueryExample> GetBasicQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "basic_1",
                Name = "Get All Artists",
                Description = "Simple SELECT query to retrieve all artists",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var artists = context.GetTable<Artist>()
    .OrderBy(a => a.Name)
    .ToList();
return artists;"
            },
            new QueryExample
            {
                Id = "basic_2",
                Name = "Get All Genres",
                Description = "Retrieve all music genres ordered by name",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var genres = context.GetTable<Genre>()
    .OrderBy(g => g.Name)
    .ToList();
return genres;"
            },
            new QueryExample
            {
                Id = "basic_3",
                Name = "Filter Tracks by Genre",
                Description = "Get all Rock tracks using WHERE clause",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var rockGenre = context.GetTable<Genre>()
    .FirstOrDefault(g => g.Name == ""Rock"");

if (rockGenre != null)
{
    var rockTracks = context.GetTable<Track>()
        .Where(t => t.GenreId == rockGenre.id)
        .OrderBy(t => t.Name)
        .Take(50)
        .ToList();
    return rockTracks;
}
return new List<Track>();"
            },
            new QueryExample
            {
                Id = "basic_4",
                Name = "Find Artist by Name",
                Description = "Search for a specific artist using LIKE",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var artists = context.GetTable<Artist>()
    .Where(a => a.Name.Contains(""Zeppelin""))
    .ToList();
return artists;"
            },
            new QueryExample
            {
                Id = "basic_5",
                Name = "Get Tracks by Price Range",
                Description = "Filter tracks between $0.99 and $1.49",
                Category = QueryCategory.Basic,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>()
    .Where(t => t.UnitPrice >= 0.99m && t.UnitPrice <= 1.49m)
    .OrderBy(t => t.UnitPrice)
    .ThenBy(t => t.Name)
    .Take(100)
    .ToList();
return tracks;"
            }
        };
    }

    private static List<QueryExample> GetRelationshipQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "rel_1",
                Name = "Tracks with Album Info",
                Description = "JOIN tracks with their album information",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               orderby track.Name
               select new { track.Name, AlbumTitle = album.Title, track.Milliseconds })
               .Take(50)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_2",
                Name = "Albums with Artist Names",
                Description = "JOIN albums with their artist information",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from album in context.GetTable<Album>()
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               orderby artist.Name, album.Title
               select new { album.Title, ArtistName = artist.Name, album.id })
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "rel_3",
                Name = "Complete Track Information",
                Description = "JOIN tracks with album, artist, and genre",
                Category = QueryCategory.Relationships,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
               from genre in genreGroup.DefaultIfEmpty()
               orderby artist.Name, album.Title, track.TrackNumber
               select new 
               { 
                   TrackName = track.Name,
                   AlbumTitle = album.Title,
                   ArtistName = artist.Name,
                   GenreName = genre != null ? genre.Name : ""Unknown"",
                   track.Milliseconds
               })
               .Take(100)
               .ToList();
return results;"
            }
        };
    }

    private static List<QueryExample> GetAggregationQueryExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "agg_1",
                Name = "Count Tracks by Genre",
                Description = "GROUP BY with COUNT aggregate",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id into genreGroup
               from genre in genreGroup.DefaultIfEmpty()
               group track by new { GenreId = genre != null ? genre.id : 0, GenreName = genre != null ? genre.Name : ""Unknown"" } into g
               select new 
               { 
                   Genre = g.Key.GenreName,
                   TrackCount = g.Count(),
                   TotalDurationMinutes = g.Sum(t => t.Milliseconds) / 1000 / 60
               })
               .OrderByDescending(x => x.TrackCount)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "agg_2",
                Name = "Album Count by Artist",
                Description = "Count how many albums each artist has",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from album in context.GetTable<Album>()
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               group album by new { artist.id, artist.Name } into g
               select new 
               { 
                   ArtistName = g.Key.Name,
                   AlbumCount = g.Count()
               })
               .OrderByDescending(x => x.AlbumCount)
               .Take(20)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "agg_3",
                Name = "Average Track Duration by Genre",
                Description = "Calculate average track length per genre",
                Category = QueryCategory.Aggregations,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join genre in context.GetTable<Genre>() on track.GenreId equals genre.id
               group track by new { genre.id, genre.Name } into g
               select new 
               { 
                   Genre = g.Key.Name,
                   AvgDurationMinutes = g.Average(t => t.Milliseconds) / 1000.0 / 60.0,
                   TrackCount = g.Count()
               })
               .OrderByDescending(x => x.AvgDurationMinutes)
               .ToList();
return results;"
            }
        };
    }

    private static List<QueryExample> GetAdvancedLinqExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "adv_1",
                Name = "Paging with Skip/Take",
                Description = "Implement pagination for large result sets",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
int pageSize = 20;
int pageNumber = 1; // Change to test different pages
var tracks = context.GetTable<Track>()
    .OrderBy(t => t.Name)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "adv_2",
                Name = "Multiple ORDER BY",
                Description = "Sort by multiple columns with different directions",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from track in context.GetTable<Track>()
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               join artist in context.GetTable<Artist>() on album.ArtistId equals artist.id
               orderby artist.Name ascending, album.Title ascending, track.TrackNumber ascending
               select new { artist.Name, album.Title, track.TrackNumber, TrackName = track.Name })
               .Take(100)
               .ToList();
return results;"
            },
            new QueryExample
            {
                Id = "adv_3",
                Name = "Complex WHERE with Multiple Conditions",
                Description = "Combine multiple filter conditions with AND/OR",
                Category = QueryCategory.AdvancedLinq,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>()
    .Where(t => (t.UnitPrice > 1.0m && t.Milliseconds > 300000) || 
                (t.Name.Contains(""Love"") || t.Name.Contains(""Rock"")))
    .OrderBy(t => t.UnitPrice)
    .Take(50)
    .ToList();
return tracks;"
            }
        };
    }

    private static List<QueryExample> GetRawSqlExamples()
    {
        // Load SQL statements from JSON file
        var sqlStatements = LoadSqlStatementsFromJson();

        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "raw_1",
                Name = "Get All Artists (Raw SQL)",
                Description = "Execute raw SQL from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<Artist>(""GetAllArtistsRaw"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAllArtistsRaw")
            },
            new QueryExample
            {
                Id = "raw_2",
                Name = "Tracks with Album/Artist (Raw SQL)",
                Description = "Complex JOIN query from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTracksWithArtistAlbum"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithArtistAlbum")
            },
            new QueryExample
            {
                Id = "raw_3",
                Name = "Top Selling Tracks (Raw SQL)",
                Description = "Aggregation query with sales data from JSON",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmDatabase.ExecuteQueryAsync<dynamic>(""GetTopSellingTracks"");
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopSellingTracks")
            }
        };
    }

    /// <summary>
    /// Loads SQL statements from the SqlStatements.json file
    /// </summary>
    private static Dictionary<string, string> LoadSqlStatementsFromJson()
    {
        var statements = new Dictionary<string, string>();

        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("SqlStatements.json").Result;
            if (stream != null)
            {
                using var reader = new System.IO.StreamReader(stream);
                var json = reader.ReadToEnd();
                var doc = System.Text.Json.JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("select", out var selectArray))
                {
                    foreach (var item in selectArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("Statement Name", out var nameElement) &&
                            item.TryGetProperty("Statement", out var statementElement))
                        {
                            statements[nameElement.GetString() ?? ""] = statementElement.GetString() ?? "";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading SQL statements: {ex.Message}");
        }

        return statements;
    }

    private static List<QueryExample> GetPerformanceExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "perf_1",
                Name = "Query 1000+ Tracks",
                Description = "Performance test with large result set",
                Category = QueryCategory.Performance,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var tracks = context.GetTable<Track>()
    .OrderBy(t => t.Name)
    .Take(1000)
    .ToList();
return tracks;"
            },
            new QueryExample
            {
                Id = "perf_2",
                Name = "Complex Multi-Table Join",
                Description = "JOIN 5 tables with filtering",
                Category = QueryCategory.Performance,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var results = (from invoiceLine in context.GetTable<InvoiceLine>()
               join invoice in context.GetTable<Invoice>() on invoiceLine.InvoiceId equals invoice.id
               join customer in context.GetTable<Customer>() on invoice.CustomerId equals customer.id
               join track in context.GetTable<Track>() on invoiceLine.TrackId equals track.id
               join album in context.GetTable<Album>() on track.AlbumId equals album.id
               where customer.Country == ""USA""
               select new 
               { 
                   CustomerName = customer.FirstName + "" "" + customer.LastName,
                   TrackName = track.Name,
                   AlbumTitle = album.Title,
                   invoiceLine.Quantity,
                   invoiceLine.UnitPrice
               })
               .Take(500)
               .ToList();
return results;"
            }
        };
    }

    private static List<QueryExample> GetManyToManyExamples()
    {
        return new List<QueryExample>
        {
            new QueryExample
            {
                Id = "m2m_1",
                Name = "Tracks in a Playlist",
                Description = "Query many-to-many relationship through junction table",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var playlist = context.GetTable<Playlist>()
    .FirstOrDefault(p => p.Name.Contains(""Music""));

if (playlist != null)
{
    var tracks = (from pt in context.GetTable<PlaylistTrack>()
                  join track in context.GetTable<Track>() on pt.TrackId equals track.id
                  where pt.PlaylistId == playlist.id
                  orderby track.Name
                  select track)
                  .Take(50)
                  .ToList();
    return tracks;
}
return new List<Track>();"
            },
            new QueryExample
            {
                Id = "m2m_2",
                Name = "Playlists Containing Track",
                Description = "Reverse query: find all playlists with a specific track",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
var track = context.GetTable<Track>()
    .FirstOrDefault(t => t.Name.Contains(""Rock""));

if (track != null)
{
    var playlists = (from pt in context.GetTable<PlaylistTrack>()
                     join playlist in context.GetTable<Playlist>() on pt.PlaylistId equals playlist.id
                     where pt.TrackId == track.id
                     select playlist)
                     .ToList();
    return playlists;
}
return new List<Playlist>();"
            },
            new QueryExample
            {
                Id = "m2m_3",
                Name = "Playlist Statistics",
                Description = "Aggregate data across many-to-many relationship",
                Category = QueryCategory.ManyToMany,
                Type = QueryType.Linq,
                Code = @"using var context = new SxmLinqDbContext(""Chinook"");
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
return results;"
            }
        };
    }
}
