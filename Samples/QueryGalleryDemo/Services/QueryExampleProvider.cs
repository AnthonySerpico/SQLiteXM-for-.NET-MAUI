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
        examples.AddRange(GetTransactionExamples());
        examples.AddRange(GetParameterizedQueryExamples());
        examples.AddRange(GetDataModificationExamples());
        examples.AddRange(GetMixedContextExamples());

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
            QueryCategory.Transactions => GetTransactionExamples(),
            QueryCategory.ParameterizedQueries => GetParameterizedQueryExamples(),
            QueryCategory.DataModification => GetDataModificationExamples(),
            QueryCategory.MixedContext => GetMixedContextExamples(),
            _ => new List<QueryExample>()
        };
    }

    private static List<QueryExample> GetBasicQueryExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Basic)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }


    private static List<QueryExample> GetRelationshipQueryExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Relationships)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }


    private static List<QueryExample> GetAggregationQueryExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Aggregations)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }


    private static List<QueryExample> GetAdvancedLinqExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.AdvancedLinq)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetAllArtistsRaw"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAllArtistsRaw"),
                Explanation = @"**How It Works:**
1. SQL query loaded from SqlStatements.json file
2. RunStatementAsync runs raw SQL
3. Results mapped to Artist entity type
4. Returns strongly-typed list of Artist objects

**Key Concepts:**
• Raw SQL allows full SQLite feature access
• SQL statements stored in JSON for easy maintenance
• Type mapping: SQL rows → C# entities
• Useful when LINQ limitations exist
• Best for complex queries LINQ can't express"
            },
            new QueryExample
            {
                Id = "raw_2",
                Name = "Tracks with Album/Artist (Raw SQL)",
                Description = "Complex JOIN query from SqlStatements.json",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTracksWithArtistAlbum"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithArtistAlbum"),
                Explanation = @"**How It Works:**
1. Load SQL with multiple INNER JOINs
2. Use dynamic type for flexible result shape
3. Execute joins across Track, Album, Artist
4. Return anonymous objects with mixed properties

**Key Concepts:**
• dynamic allows flexible result shapes
• Raw SQL handles complex joins easily
• No entity mapping required for ad-hoc queries
• Good for reporting/analytics queries
• Trade-off: lose compile-time type safety"
            },
            new QueryExample
            {
                Id = "raw_3",
                Name = "Top Selling Tracks (Raw SQL)",
                Description = "Aggregation query with sales data from JSON",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTopSellingTracks"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopSellingTracks"),
                Explanation = @"**How It Works:**
1. SQL aggregates invoice line data
2. GROUP BY to summarize by track
3. COUNT/SUM calculate sales metrics
4. ORDER BY + LIMIT for top N
5. Return ranked results

**Key Concepts:**
• Aggregation functions: COUNT, SUM
• GROUP BY groups rows for summarization
• Raw SQL great for analytics
• LIMIT controls result size
• Common sales reporting pattern"
            },
            new QueryExample
            {
                Id = "raw_4",
                Name = "Customer Purchase Statistics",
                Description = "LEFT JOIN with aggregations for customer analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetCustomerPurchaseStats"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomerPurchaseStats"),
                Explanation = @"**How It Works:**
1. LEFT JOIN ensures all customers included
2. Aggregate purchase data per customer
3. Calculate total spent, order count
4. Handle NULL values for customers with no purchases
5. Return complete customer profile

**Key Concepts:**
• LEFT JOIN includes rows even without matches
• COALESCE/IFNULL handle NULLs gracefully
• Aggregates work with GROUP BY
• Common in customer analytics
• Raw SQL simplifies outer join logic"
            },
            new QueryExample
            {
                Id = "raw_5",
                Name = "Genre Popularity Analysis",
                Description = "GROUP BY with calculated fields",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetGenrePopularity"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetGenrePopularity"),
                Explanation = @"**How It Works:**
1. Join Track and Genre tables
2. GROUP BY genre to aggregate metrics
3. COUNT tracks per genre
4. Calculate average price
5. Order by popularity (track count)

**Key Concepts:**
• GROUP BY creates one row per genre
• COUNT(*) counts rows in each group
• AVG() calculates mean values
• Useful for popularity/trending analysis
• Raw SQL simplifies grouping logic"
            },
            new QueryExample
            {
                Id = "raw_6",
                Name = "Playlist Details with Duration",
                Description = "Multiple LEFT JOINs with SUM aggregation",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetPlaylistDetails"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistDetails"),
                Explanation = @"**How It Works:**
1. Start with Playlist table
2. LEFT JOIN through junction to tracks
3. SUM track durations per playlist
4. COUNT tracks in each playlist
5. Include playlists with zero tracks

**Key Concepts:**
• Multiple LEFT JOINs chain relationships
• SUM() aggregates numeric values
• GROUP BY playlist to summarize
• Handles many-to-many via junction table
• NULL-safe aggregation"
            },
            new QueryExample
            {
                Id = "raw_7",
                Name = "Artist Revenue Report",
                Description = "Complex multi-table JOIN with COALESCE",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetArtistRevenue"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetArtistRevenue"),
                Explanation = @"**How It Works:**
1. Join Artist → Album → Track → InvoiceLine
2. Sum revenue from all sales
3. COALESCE provides default for no sales
4. GROUP BY artist to aggregate
5. ORDER BY revenue descending

**Key Concepts:**
• Multi-table joins trace relationships
• COALESCE(value, 0) handles NULLs
• SUM() calculates total revenue
• Common financial reporting pattern
• Raw SQL simplifies deep joins"
            },
            new QueryExample
            {
                Id = "raw_8",
                Name = "Expensive Tracks by Genre (Subquery)",
                Description = "WHERE clause with subquery for average comparison",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetExpensiveTracksByGenre"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetExpensiveTracksByGenre"),
                Explanation = @"**How It Works:**
1. Subquery calculates AVG price per genre
2. Outer query compares track price to avg
3. Filter tracks above their genre's average
4. Return tracks that are 'expensive' for their genre

**Key Concepts:**
• Subquery in WHERE clause
• Correlated subquery uses outer table
• Compares individual vs. group aggregate
• Complex logic hard to express in LINQ
• Raw SQL enables advanced filtering"
            },
            new QueryExample
            {
                Id = "raw_9",
                Name = "Country Statistics (Nested Query)",
                Description = "Subquery in FROM clause with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetCustomersByCountryWithStats"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetCustomersByCountryWithStats"),
                Explanation = @"**How It Works:**
1. Subquery in FROM becomes derived table
2. Inner query aggregates by country
3. Outer query can further process results
4. Multiple aggregates: COUNT, SUM, AVG
5. Return country-level statistics

**Key Concepts:**
• Derived table (subquery as table source)
• Two-stage aggregation possible
• Complex analytics patterns
• Common in business intelligence
• LINQ struggles with nested aggregates"
            },
            new QueryExample
            {
                Id = "raw_10",
                Name = "Monthly Revenue Trend",
                Description = "Date functions with GROUP BY for time series analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetMonthlyRevenueTrend"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetMonthlyRevenueTrend"),
                Explanation = @"**How It Works:**
1. Extract year/month from invoice date
2. GROUP BY year and month
3. SUM revenue within each period
4. ORDER BY time for trend analysis
5. Return time series data

**Key Concepts:**
• Date/time functions: strftime, YEAR, MONTH
• Time-based grouping
• Revenue trending over time
• Common in dashboards/reports
• SQLite date handling via functions"
            },
            new QueryExample
            {
                Id = "raw_11",
                Name = "Top Customers with Full Details",
                Description = "String concatenation, HAVING clause, multiple aggregates",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTopCustomersWithDetails"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTopCustomersWithDetails"),
                Explanation = @"**How It Works:**
1. Concatenate first + last name
2. Aggregate purchase metrics per customer
3. HAVING filters groups (not rows)
4. Return only high-value customers
5. ORDER BY total spent

**Key Concepts:**
• String concatenation: || operator
• HAVING filters after GROUP BY
• WHERE filters before, HAVING filters after
• Common in CRM/loyalty analysis
• Multiple aggregates per group"
            },
            new QueryExample
            {
                Id = "raw_12",
                Name = "Tracks with Price Tier (CASE)",
                Description = "CASE expression for conditional categorization",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTracksWithPriceTier"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetTracksWithPriceTier"),
                Explanation = @"**How It Works:**
1. CASE expression evaluates conditions
2. Assign tier based on price range
3. Returns 'Budget', 'Standard', 'Premium'
4. Computed column in SELECT
5. Useful for categorization logic

**Key Concepts:**
• CASE = SQL's if-then-else
• Conditional computed columns
• Categorizes data into buckets
• Hard to express in LINQ projections
• Common in pricing/tier analysis"
            },
            new QueryExample
            {
                Id = "raw_13",
                Name = "Album Completion Analysis",
                Description = "Complex aggregation with HAVING filter",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetAlbumCompletion"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetAlbumCompletion"),
                Explanation = @"**How It Works:**
1. Count tracks per album
2. Calculate total duration
3. AVG price across album
4. HAVING filters for 'complete' albums (10+ tracks)
5. Return only substantial albums

**Key Concepts:**
• HAVING with COUNT threshold
• Multiple aggregates in one query
• Filter aggregated results
• Useful for quality/completeness checks
• Raw SQL simplifies complex HAVING"
            },
            new QueryExample
            {
                Id = "raw_14",
                Name = "Employee Performance Report",
                Description = "Self-join with multiple aggregations",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetEmployeePerformance"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetEmployeePerformance"),
                Explanation = @"**How It Works:**
1. Self-join Employee to Employee (manager)
2. Aggregate sales per employee
3. Include manager name via self-join
4. Calculate employee metrics
5. Return hierarchical sales report

**Key Concepts:**
• Self-join: table joins to itself
• Hierarchical data (employee/manager)
• LEFT JOIN handles employees without managers
• Common in org chart queries
• LINQ self-joins are complex"
            },
            new QueryExample
            {
                Id = "raw_15",
                Name = "Playlist Popularity Metrics",
                Description = "Multiple DISTINCT aggregations for variety analysis",
                Category = QueryCategory.RawSql,
                Type = QueryType.RawSql,
                Code = @"var results = await SxmSql.RunStatementAsync(""GetPlaylistPopularity"", new Dictionary<string, object?>());
return results;",
                ActualSqlStatement = sqlStatements.GetValueOrDefault("GetPlaylistPopularity"),
                Explanation = @"**How It Works:**
1. Count total tracks in playlist
2. COUNT(DISTINCT) unique artists
3. COUNT(DISTINCT) unique genres
4. Measure playlist diversity
5. Return variety metrics

**Key Concepts:**
• COUNT(DISTINCT) eliminates duplicates
• Multiple DISTINCT counts in one query
• Measures data variety/diversity
• Common in content analysis
• LINQ DISTINCT in aggregates is tricky"
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
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Performance)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }

    private static List<QueryExample> GetManyToManyExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.ManyToMany)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }

    private static List<QueryExample> GetTransactionExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Transactions)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }

    private static List<QueryExample> GetParameterizedQueryExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.ParameterizedQueries)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }

    private static List<QueryExample> GetDataModificationExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.DataModification)
            .OrderBy(e => NaturalOrderKey(e.Id))
            .ToList();
    }


	/// <summary>
	/// Single source of truth: MixedContext examples are declared as
	/// <c>[QueryExample]</c>-attributed <c>IQueryExampleRunner</c> classes under
	/// <c>Samples/QueryGalleryDemo/Examples/Mixed/</c>. A Roslyn source generator
	/// extracts each <c>RunAsync</c> body verbatim as the displayed <c>Code</c> string,
	/// so display and execution cannot silently drift.
	/// </summary>
	private static List<QueryExample> GetMixedContextExamples()
	{
		return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
			.Where(e => e.Category == QueryCategory.MixedContext)
			.OrderBy(e => NaturalOrderKey(e.Id))
			.ToList();
	}

	/// <summary>
	/// Pads the trailing integer suffix of an id (e.g. "mix_10") with leading zeros so
	/// lexicographic ordering matches natural numeric order ("mix_2" before "mix_10").
	/// </summary>
	private static string NaturalOrderKey(string id)
	{
		int i = id.Length;
		while (i > 0 && char.IsDigit(id[i - 1])) i--;
		if (i == id.Length) return id;
		return id.Substring(0, i) + id.Substring(i).PadLeft(6, '0');
	}

#pragma warning disable IDE0051 // Dead code retained for reference until Phase 2 migrates every category.
	private static List<QueryExample> GetMixedContextExamples_Legacy_UNUSED()
	{
		return new List<QueryExample>
		{
			new QueryExample
			{
				Id = "mix_1",
				Name = "LINQ + Named SQL (read-only)",
				Description = "Run a LINQ query and a named SQL statement inside the same SxmTransaction",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

// (1) LINQ read against the context
var genreNames = ctx.GetTable<Genre>()
					.OrderBy(g => g.Name)
					.Select(g => g.Name)
					.ToList();

// (2) Named SQL from SqlStatements.json - enlists in the same ambient
//     transaction registered by the SxmTransaction ctor.
var popularity = await ctx.RunStatementAsync(
	"GetGenrePopularity",
	new Dictionary<string, object?>());

return new[]
{
	new { Step = "LINQ Genres",           Count = genreNames.Count },
	new { Step = "Named GenrePopularity", Count = popularity.Count }
};
""",
				Explanation = """
**How It Works:**
1. Open an SxmTransaction for the Chinook database
2. Issue a LINQ query on ctx.GetTable<Genre>()
3. Call ctx.RunStatementAsync with a named statement
4. Both share the same underlying connection via the ambient SxmSqlTransaction registered by the context

**Key Concepts:**
- A single SxmTransaction hosts multiple query styles
- Read-only work never opens a SQLite transaction (least-work)
- Named SQL enlists on the ambient transaction automatically
- await using guarantees clean async disposal
"""
			},
			new QueryExample
			{
				Id = "mix_2",
				Name = "LINQ read + Entity DML + Rollback",
				Description = "Insert an entity, observe it via LINQ within the same tx, then roll back",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

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
""",
				Explanation = """
**How It Works:**
1. LINQ COUNT gives the starting artist count
2. new Artist().SaveAsync() enlists on the ambient tx registered by the context
3. A second LINQ COUNT sees the uncommitted row (read-your-writes)
4. RollbackTransactionAsync discards everything before dispose

**Key Concepts:**
- Parameterless SaveAsync() picks up SxmAmbientTransaction.Current
- LINQ reads see uncommitted writes in the same context
- Explicit rollback keeps the demo DB clean between runs
"""
			},
			new QueryExample
			{
				Id = "mix_3",
				Name = "Entity DML + Embedded SQL verify",
				Description = "Insert Artist and Album, verify with an embedded SELECT COUNT(*), rollback",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

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
""",
				Explanation = """
**How It Works:**
1. SaveAsync inserts Artist, then Album referencing the new id
2. Embedded SQL (literal text passed to RunStatementAsync) executes on the same connection
3. Rollback ensures nothing persists

**Key Concepts:**
- Embedded SQL is raw SQL text dispatched by RunStatementAsync
- It sees uncommitted rows written by SaveAsync
- All three statements share one transaction
"""
			},
			new QueryExample
			{
				Id = "mix_4",
				Name = "Named SQL feeds LINQ",
				Description = "Use the result of a named SQL statement as input to a follow-up LINQ query",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

// (1) Named SQL - top genres by popularity
var popularity = await ctx.RunStatementAsync(
	"GetGenrePopularity",
	new Dictionary<string, object?>());

var topGenreNames = popularity
	.Take(3)
	.Select(r => r["Genre"]?.ToString())
	.Where(n => n != null)
	.ToList();

// (2) LINQ - resolve ids, then pull tracks via the same context
var topGenreIds = ctx.GetTable<Genre>()
					 .Where(g => topGenreNames.Contains(g.Name))
					 .Select(g => g.id)
					 .ToList();

var tracks = ctx.GetTable<Track>()
				.Where(t => topGenreIds.Contains(t.GenreId))
				.OrderBy(t => t.Name)
				.Take(10)
				.Select(t => new { t.Name, t.GenreId, t.UnitPrice })
				.ToList();

return tracks;
""",
				Explanation = """
**How It Works:**
1. Named SQL returns aggregate data (top genres)
2. Project the names into a plain List<string>
3. LINQ resolves genre ids, then pulls tracks from the same context

**Key Concepts:**
- Named SQL and LINQ compose naturally inside one context
- All statements share the same connection
- No transaction is opened - both statements are pure reads
"""
			},
			new QueryExample
			{
				Id = "mix_5",
				Name = "LINQ read + Entity DML + Embedded UPDATE",
				Description = "Find genre via LINQ, insert Track, bump price with embedded SQL, verify, rollback",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

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

return new[] { new { Error = (string?)null, updated.id, updated.Name, updated.UnitPrice } };
""",
				Explanation = """
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
"""
			},
			new QueryExample
			{
				Id = "mix_6",
				Name = "Explicit Commit mid-context",
				Description = "Commit early, then continue with a fresh transaction on the same context",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
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
""",
				Explanation = """
**How It Works:**
1. SaveAsync writes inside the first auto-started transaction
2. CommitTransactionAsync ends that tx early
3. A follow-up LINQ bulk Delete starts a fresh transaction under the same context
4. A second CommitTransactionAsync finalizes the cleanup

**Key Concepts:**
- A single SxmTransaction can span multiple sequential transactions
- Explicit commit is optional - dispose auto-commits when no errors occurred
- LINQ bulk Update/Delete lazily starts a transaction on the first write
"""
			},
			new QueryExample
			{
				Id = "mix_7",
				Name = "Rollback discards mixed work",
				Description = "Entity DML + LINQ bulk update + Named SQL, then RollbackTransactionAsync",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

var marker = $"_Mix7_{Guid.NewGuid():N}";

// (1) Entity DML - two scratch artists
await new Artist { Name = marker + "_A" }.SaveAsync();
await new Artist { Name = marker + "_B" }.SaveAsync();

// (2) LINQ bulk UPDATE - rename them
int renamed = ctx.GetTable<Artist>()
				 .Where(a => a.Name.StartsWith(marker))
				 .Set(a => a.Name, a => a.Name + "_renamed")
				 .Update();

// (3) Named SQL runs in the same tx
var popularity = await ctx.RunStatementAsync(
	"GetGenrePopularity",
	new Dictionary<string, object?>());

// (4) Roll everything back
await ctx.RollbackTransactionAsync();

// (5) After rollback, none of the marker rows survive
int survivors = ctx.GetTable<Artist>().Count(a => a.Name.StartsWith(marker));

return new[]
{
	new { Step = "Bulk renamed",   Detail = renamed.ToString() },
	new { Step = "Named SQL rows", Detail = popularity.Count.ToString() },
	new { Step = "After rollback", Detail = $"MarkerRowsRemaining={survivors}" }
};
""",
				Explanation = """
**How It Works:**
1. Entity DML inserts scratch rows
2. LINQ bulk Update renames them via a single SQL statement
3. Named SQL executes read-only work in the same tx
4. RollbackTransactionAsync undoes inserts AND updates atomically
5. A fresh LINQ COUNT confirms zero survivors

**Key Concepts:**
- Rollback is all-or-nothing across every statement in the tx
- LINQ bulk writes participate in the ambient tx just like entity DML
- Named SQL reads see uncommitted work
"""
			},
			new QueryExample
			{
				Id = "mix_8",
				Name = "Auto-rollback on exception",
				Description = "An exception mid-context aborts all mixed work automatically on dispose",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
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
""",
				Explanation = """
**How It Works:**
1. Entity DML and Named SQL run under a shared context
2. An uncaught exception escapes the context body
3. DisposeAsync detects the failed state and rolls back
4. A fresh context queries and finds none of the marker rows

**Key Concepts:**
- await using guarantees async disposal even on exception
- Failure -> rollback; success -> commit
- Rollback covers every statement executed on the context
"""
			},
			new QueryExample
			{
				Id = "mix_9",
				Name = "Three-way read: LINQ + Named SQL + Embedded SQL",
				Description = "All-reads sample. Confirms one connection is shared; no transaction opened",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

// (1) LINQ
int albumCount = ctx.GetTable<Album>().Count();

// (2) Named SQL
var artistRevenue = await ctx.RunStatementAsync(
	"GetArtistRevenue",
	new Dictionary<string, object?>());

// (3) Embedded SQL
var trackRow = await ctx.RunStatementAsync(
	"SELECT COUNT(*) AS TrackCount FROM Track",
	new Dictionary<string, object?>());

return new[]
{
	new { Source = "LINQ Count<Album>",       Value = albumCount.ToString() },
	new { Source = "Named GetArtistRevenue",  Value = artistRevenue.Count + " rows" },
	new { Source = "Embedded COUNT(*) Track", Value = trackRow.FirstOrDefault()?["TrackCount"]?.ToString() ?? "0" }
};
""",
				Explanation = """
**How It Works:**
1. LINQ COUNT runs against the context's DataConnection
2. Named SQL invokes a JSON-registered statement
3. Embedded SQL is a literal SELECT passed as text

**Key Concepts:**
- Read-only means no SQLite BEGIN issued (least-work)
- Every read still goes through the same connection
- Illustrates the three read paths side by side
"""
			},
			new QueryExample
			{
				Id = "mix_10",
				Name = "End-to-end unit of work",
				Description = "LINQ + Entity DML + Embedded SQL + Named SQL cooperating, then rollback",
				Category = QueryCategory.MixedContext,
				Type = QueryType.Mixed,
				Code = """
await using var ctx = new SxmTransaction("Chinook");

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
""",
				Explanation = """
**How It Works:**
1. LINQ finds an anchor Artist
2. Entity SaveAsync creates a scratch Album and Track
3. Embedded SQL counts tracks on that new album inside the same tx
4. Named SQL fetches genre popularity for context
5. A LINQ aggregate sums UnitPrice for the album
6. RollbackTransactionAsync throws all of it away

**Key Concepts:**
- A single SxmTransaction is a unit of work spanning every query style
- The ambient transaction makes SaveAsync / embedded SQL / named SQL / LINQ interoperable
- Rollback (or an exception) atomically discards the entire mix
"""
			}
		};
	}
#pragma warning restore IDE0051
}
