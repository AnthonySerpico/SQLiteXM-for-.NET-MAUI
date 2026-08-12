using System;
using System.Threading.Tasks;
using QueryGalleryDemo.Models;

namespace QueryGalleryDemo.Examples;

/// <summary>
/// Declares a gallery example. The decorated class must implement <see cref="IQueryExampleRunner"/>.
/// The source generator extracts the body of <see cref="IQueryExampleRunner.RunAsync"/> at compile
/// time and uses it as the displayed <c>Code</c> string, making display and execution
/// impossible to drift.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class QueryExampleAttribute : Attribute
{
    public QueryExampleAttribute(
        string id,
        string name,
        string description,
        QueryCategory category,
        QueryType type,
        string explanation)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Type = type;
        Explanation = explanation;
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public QueryCategory Category { get; }
    public QueryType Type { get; }
    public string Explanation { get; }
}

/// <summary>
/// Executes a gallery example. Implementations are discovered by <c>QueryExampleAttribute</c>;
/// the body of <see cref="RunAsync"/> is what the user sees in the gallery UI.
/// </summary>
public interface IQueryExampleRunner
{
    Task<object> RunAsync();
}
