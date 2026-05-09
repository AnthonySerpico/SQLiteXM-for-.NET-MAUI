using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Defines a test collection that runs sequentially.
/// Tests in this collection will not run in parallel with each other or with other tests.
/// Use this for tests that reset SQLiteXM static state via CleanupTestDataAsync().
/// </summary>
[CollectionDefinition("Sequential", DisableParallelization = true)]
public class SequentialCollection
{
}
