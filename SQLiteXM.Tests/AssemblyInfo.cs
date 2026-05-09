using Xunit;

// Disable test parallelization globally to prevent tests from interfering with each other
// when using CleanupTestDataAsync() which resets static SQLiteXM state
[assembly: CollectionBehavior(DisableTestParallelization = true)]
