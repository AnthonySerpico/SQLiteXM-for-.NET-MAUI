namespace QueryGalleryDemo.Models;

/// <summary>
/// Metadata about a query example
/// </summary>
public class QueryExample
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public QueryCategory Category { get; set; }
    public QueryType Type { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// For RawSql queries, contains the actual SQL statement from SqlStatements.json
    /// </summary>
    public string? ActualSqlStatement { get; set; }
}

/// <summary>
/// Type of query (LINQ or Raw SQL)
/// </summary>
public enum QueryType
{
    Linq,
    RawSql
}
