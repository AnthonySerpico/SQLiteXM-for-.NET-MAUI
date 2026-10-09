namespace SQLiteXM.Tests;

/// <summary>
/// Collection definition for the named-statement tests.
///
/// <para>
/// These tests re-initialize SQLiteXM against their own statements.json so that named SQL
/// statements are registered, which mutates process-wide state. Parallelization is disabled so
/// they never run alongside each other or interleave with another collection's initialization.
/// </para>
/// </summary>
[CollectionDefinition("NamedStatement", DisableParallelization = true)]
public class NamedStatementCollection
{
}
