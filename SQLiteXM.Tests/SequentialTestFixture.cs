using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Fixture for Sequential test collection that ensures clean database state.
/// </summary>
public class SequentialTestFixture : IAsyncLifetime
{
    private readonly TestBase _testBase = new TestBase();

    public async Task InitializeAsync()
    {
        // Clean all table data before running Sequential tests
        await _testBase.CleanupTableDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private class TestBase : Tests.TestBase
    {
        // Expose CleanupTableDataAsync publicly for the fixture
        public new async Task CleanupTableDataAsync()
        {
            await base.CleanupTableDataAsync();
        }
    }
}
