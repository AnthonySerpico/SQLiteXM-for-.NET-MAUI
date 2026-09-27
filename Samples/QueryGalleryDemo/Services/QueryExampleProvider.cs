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
        examples.AddRange(GetBenchmarkExamples());

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
            QueryCategory.Benchmarks => GetBenchmarkExamples(),
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetAllArtistsRaw"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTracksWithArtistAlbum"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTopSellingTracks"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetCustomerPurchaseStats"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetGenrePopularity"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetPlaylistDetails"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetArtistRevenue"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetExpensiveTracksByGenre"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetCustomersByCountryWithStats"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetMonthlyRevenueTrend"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTopCustomersWithDetails"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetTracksWithPriceTier"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetAlbumCompletion"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetEmployeePerformance"");
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
                Code = @"var results = await SxmSql.RunStatementAsync(""GetPlaylistPopularity"");
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
    /// Benchmark examples live in Samples/QueryGalleryDemo/Examples/Benchmarks/BenchmarksExamples.cs.
    /// </summary>
    private static List<QueryExample> GetBenchmarkExamples()
    {
        return QueryGalleryDemo.Examples.Generated.GeneratedQueryExamples.All
            .Where(e => e.Category == QueryCategory.Benchmarks)
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
}
