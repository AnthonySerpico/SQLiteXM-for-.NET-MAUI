using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Collection definition for multi-database tests.
/// These tests must run sequentially and in isolation because they:
/// 1. Call ResetForTestingAsync() which clears all database state
/// 2. Use different SQL statements configurations
/// 3. Must re-initialize the library after each test
/// </summary>
[CollectionDefinition("MultiDatabase", DisableParallelization = true)]
public class MultiDatabaseCollection
{
    // This class is never instantiated. It's just a marker for xUnit.
}
