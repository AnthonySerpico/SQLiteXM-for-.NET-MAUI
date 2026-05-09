using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// xUnit collection definition for SQLiteXM tests.
/// All tests in this collection run sequentially to ensure proper database initialization.
/// </summary>
[CollectionDefinition("SQLiteXM Tests", DisableParallelization = true)]
public class SQLiteXMTestCollection
{
    // This class is never instantiated. It's just a marker for xUnit.
}
