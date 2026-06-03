namespace QueryGalleryDemo.Models;

/// <summary>
/// Represents the result of a query execution with metadata
/// </summary>
public class QueryResult
{
    public object? Data { get; set; }
    public int RecordCount { get; set; }
    public long ExecutionTimeMs { get; set; }
    public QueryType QueryType { get; set; }
    public string QueryName { get; set; } = string.Empty;
}
