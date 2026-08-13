using System.Runtime.CompilerServices;

namespace SQLiteXM.Tests;

/// <summary>
/// Module initializer that ensures tests only run in Debug configuration.
/// This check runs BEFORE any test code executes.
/// </summary>
internal static class BuildConfigurationCheck
{
    [ModuleInitializer]
    internal static void EnsureDebugConfiguration()
    {
#if !DEBUG
        const string errorMessage = 
            "\n" +
            "╔═══════════════════════════════════════════════════════════════════════════╗\n" +
            "║                          ⚠️  CONFIGURATION ERROR  ⚠️                       ║\n" +
            "╠═══════════════════════════════════════════════════════════════════════════╣\n" +
            "║                                                                           ║\n" +
            "║  The SQLiteXM test suite MUST be run in DEBUG configuration.             ║\n" +
            "║                                                                           ║\n" +
            "║  The tests use DEBUG-only features like SxmDatabase.ResetForTestingAsync()║\n" +
            "║  to properly clean up state between tests.                                ║\n" +
            "║                                                                           ║\n" +
            "║  Running in Release mode will cause test failures and unreliable results. ║\n" +
            "║                                                                           ║\n" +
            "║  ➡️  Please switch to Debug configuration and try again.                  ║\n" +
            "║                                                                           ║\n" +
            "╚═══════════════════════════════════════════════════════════════════════════╝\n";

        // Write to console for immediate visibility
        Console.Error.WriteLine(errorMessage);

        // Throw exception to terminate test execution
        throw new InvalidOperationException(
            "SQLiteXM tests MUST be run in Debug configuration. " +
            "The test suite uses DEBUG-only features like SxmDatabase.ResetForTestingAsync() " +
            "to properly clean up state between tests. Running in Release mode will cause test failures. " +
            "Please switch to Debug configuration and try again.");
#endif
    }
}
