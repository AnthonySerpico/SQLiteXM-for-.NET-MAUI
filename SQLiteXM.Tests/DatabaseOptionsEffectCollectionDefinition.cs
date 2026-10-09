using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Collection definition for tests that verify <see cref="SxmDatabaseOptions"/> actually take effect
/// on real SQLite connections.
/// These tests must run sequentially and in isolation because they:
/// 1. Call ResetForTestingAsync() which clears all database state (including the registered options).
/// 2. Re-initialize SQLiteXM with a different set of options for each scenario.
/// 3. Must restore the standard TestBase configuration afterwards so the rest of the suite keeps working.
/// </summary>
[CollectionDefinition("DatabaseOptionsEffect", DisableParallelization = true)]
public class DatabaseOptionsEffectCollection
{
    // This class is never instantiated. It's just a marker for xUnit.
}
