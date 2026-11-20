using Xunit.Abstractions;
using Xunit.Sdk;

namespace SQLiteXM.Tests;

/// <summary>
/// Runs the "Concurrency" test collection after all other test collections.
/// </summary>
public class ConcurrencyLastCollectionOrderer : ITestCollectionOrderer
{
    public const string ConcurrencyCollectionName = "Concurrency";

    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> testCollections)
    {
        return testCollections
            .OrderBy(collection => collection.DisplayName == ConcurrencyCollectionName ? 1 : 0)
            .ThenBy(collection => collection.DisplayName, StringComparer.Ordinal);
    }
}
