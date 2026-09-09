using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Collection definition for tests exercising the process-wide StartInitialization / EnsureReadyAsync
/// pattern documented in Docs/advanced-initialization-patterns.md.
/// These tests must run sequentially and in isolation because they:
/// 1. Call ResetForTestingAsync() which clears all database state (including the DbReady signal).
/// 2. Reconfigure the database folder and SQL statements for each scenario.
/// 3. Must re-initialize the library after each test so the rest of the suite keeps working.
/// </summary>
[CollectionDefinition("InitializationPattern", DisableParallelization = true)]
public class InitializationPatternCollection
{
    // This class is never instantiated. It's just a marker for xUnit.
}
