using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Defines a test collection that runs sequentially with clean database state.
/// Tests in this collection will not run in parallel with each other or with other tests.
/// Use this for tests that need isolated data and call CleanupTableDataAsync().
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection : ICollectionFixture<SequentialTestFixture>
{
    // This class is never instantiated. It exists solely to define the collection
    // and associate it with the SequentialTestFixture.
}
