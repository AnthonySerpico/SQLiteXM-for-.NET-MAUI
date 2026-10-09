using FluentAssertions;
using SQLiteXM;
using System.Text.Json;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for executing SQL statements by *name* rather than by inline SQL.
///
/// <para>
/// <c>RunStatementAsync</c> accepts a single string that can be either raw SQL or the name of a
/// statement declared in statements.json. Resolution happens in two stages: the name is first
/// looked up in the registered statement dictionaries, and only if that lookup comes back
/// <c>Unknown</c> is the string re-examined as inline SQL. These tests exercise the first stage,
/// which no other test class covers because the shared test harness writes a statements.json that
/// declares no named statements at all.
/// </para>
///
/// <para>
/// Each test therefore stands up its own database from a statements.json containing
/// <c>select</c> / <c>insert</c> / <c>update</c> / <c>delete</c> entries, then restores the
/// standard harness configuration on dispose.
/// </para>
/// </summary>
[Collection("NamedStatement")]
public class NamedStatementTests : IDisposable
{
    private static readonly string NamedStatementTestFolder =
        Path.Combine(TestBase.TestRootFolder, "NamedStatement");

    private readonly string _testId = Guid.NewGuid().ToString("N");
    private bool _disposed;

    /// <summary>
    /// Statement names are unique per test instance so that statements registered by one test can
    /// never satisfy a lookup performed by another.
    /// </summary>
    private string SelectByName => $"getSimpleByName_{_testId}";
    private string SelectByPosition => $"getSimpleByPosition_{_testId}";
    private string SelectAllActive => $"getActiveSimple_{_testId}";
    private string InsertSimple => $"insertSimple_{_testId}";
    private string UpdateAgeByName => $"updateSimpleAge_{_testId}";
    private string DeleteByName => $"deleteSimpleByName_{_testId}";

    #region Fixture

    public NamedStatementTests()
    {
        Directory.CreateDirectory(NamedStatementTestFolder);
    }

    /// <summary>
    /// Writes a statements.json that declares one statement of each type against SimpleEntity.
    /// </summary>
    /// <remarks>
    /// The field names ("Statement Name", "Table Name", "Statement") and the array names
    /// ("select", "insert", "update", "delete") are the schema the library's loader expects;
    /// see SqlStatements/ExampleSqlStatements.json.
    /// </remarks>
    private string CreateStatementsFile(string databaseName)
    {
        var config = new Dictionary<string, object?>
        {
            ["version"] = 1L,
            ["databases"] = new[] { new { database = databaseName, isDefault = true } },

            ["select"] = new object[]
            {
                // Named parameter style.
                new Dictionary<string, string>
                {
                    ["Statement Name"] = SelectByName,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name"
                },
                // Positional parameter style.
                new Dictionary<string, string>
                {
                    ["Statement Name"] = SelectByPosition,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @p0 AND Age = @p1"
                },
                // No parameters at all.
                new Dictionary<string, string>
                {
                    ["Statement Name"] = SelectAllActive,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE IsActive = 1"
                }
            },

            ["insert"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["Statement Name"] = InsertSimple,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)"
                }
            },

            ["update"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["Statement Name"] = UpdateAgeByName,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "UPDATE SimpleEntity SET Age = @Age WHERE Name = @Name"
                }
            },

            ["delete"] = new object[]
            {
                new Dictionary<string, string>
                {
                    ["Statement Name"] = DeleteByName,
                    ["Table Name"] = "SimpleEntity",
                    ["Statement"] = "DELETE FROM SimpleEntity WHERE Name = @Name"
                }
            }
        };

        string path = Path.Combine(NamedStatementTestFolder, $"statements_{_testId}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    /// <summary>
    /// Resets SQLiteXM and brings it back up against a statements file that declares the named
    /// statements used by these tests.
    /// </summary>
    private async Task InitializeAsync()
    {
        await SxmDatabase.ResetForTestingAsync();

        string databaseName = $"nsdb_{_testId}";
        string statementsPath = CreateStatementsFile(databaseName);

        var options = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = NamedStatementTestFolder
        };

        using (Stream stream = File.OpenRead(statementsPath))
        {
            SxmDatabase.StartInitialization(stream, options, typeof(SimpleEntity));
            await SxmDatabase.EnsureReadyAsync();
        }
    }

    /// <summary>
    /// Seeds a row using the entity API so the SELECT statements have something to find.
    /// </summary>
    private static async Task SeedAsync(string name, int age, bool isActive)
    {
        var entity = new SimpleEntity { Name = name, Age = age, IsActive = isActive };
        await entity.SaveAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            foreach (string file in Directory.GetFiles(NamedStatementTestFolder, $"*{_testId}*"))
            {
                try { File.Delete(file); } catch { /* best effort cleanup */ }
            }
        }
        catch { /* best effort cleanup */ }

        // CRITICAL: Restore the standard TestBase configuration. Every other test class assumes
        // "test_database" is initialized and the standard entities are registered.
        SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();

        var initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = Path.Combine(TestBase.TestRootFolder, "test_database")
        };
        Directory.CreateDirectory(initOptions.DatabaseFolderOverride);

        string testStatementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
        if (!File.Exists(testStatementsPath))
        {
            var config = new
            {
                version = 1L,
                databases = new[] { new { database = "test_database", isDefault = true } }
            };
            File.WriteAllText(testStatementsPath, JsonSerializer.Serialize(config));
        }

        using (Stream stream = File.OpenRead(testStatementsPath))
        {
            SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();
        }

        SxmDatabase.RegisterEntitiesAsync(
            typeof(SimpleEntity),
            typeof(AllTypesEntity),
            typeof(TimeTypeTextEntity),
            typeof(ExplicitColumnEntity),
            typeof(IndexedEntity),
            typeof(ParentEntity),
            typeof(ChildEntity),
            typeof(TriggerEntity),
            typeof(RequiredFieldEntity)).GetAwaiter().GetResult();

        GC.SuppressFinalize(this);
    }

    #endregion

    #region Named statement resolution and execution

    [Fact]
    public async Task NamedSelect_WithNamedParameters_ShouldResolveTheStatementAndReturnRows()
    {
        await InitializeAsync();
        await SeedAsync("Alpha", 31, true);
        await SeedAsync("Beta", 32, true);

        // The first argument is a statement NAME, not SQL. If name resolution failed, the library
        // would try to parse "getSimpleByName_..." as SQL and throw.
        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "Alpha" });

        results.Should().ContainSingle();
        results[0].Name.Should().Be("Alpha");
        results[0].Age.Should().Be(31);
    }

    [Fact]
    public async Task NamedSelect_WithNoParameters_ShouldExecuteUsingTheNoParameterOverload()
    {
        await InitializeAsync();
        await SeedAsync("ActiveOne", 41, true);
        await SeedAsync("ActiveTwo", 42, true);
        await SeedAsync("InactiveOne", 43, false);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(SelectAllActive);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.IsActive);
    }

    [Fact]
    public async Task NamedSelect_WithPositionalParameters_ShouldBindInListOrder()
    {
        await InitializeAsync();
        await SeedAsync("Gamma", 51, true);
        await SeedAsync("Gamma", 52, true);

        // The statement is "... WHERE Name = @p0 AND Age = @p1", so the list order decides which
        // of the two Gamma rows is matched.
        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByPosition,
            new List<object> { "Gamma", 52 });

        results.Should().ContainSingle();
        results[0].Age.Should().Be(52);
    }

    [Fact]
    public async Task NamedSelect_ReturningRawDictionaries_ShouldKeyRowsByColumnName()
    {
        await InitializeAsync();
        await SeedAsync("Delta", 61, true);

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "Delta" });

        rows.Should().ContainSingle();
        rows[0].Should().ContainKey("Name");
        rows[0]["Name"].Should().Be("Delta");
        rows[0]["Age"].Should().Be(61L);
    }

    [Fact]
    public async Task NamedInsert_ShouldAddARowThatNamedSelectCanRead()
    {
        await InitializeAsync();

        await SxmSql.RunStatementAsync(
            InsertSimple,
            new Dictionary<string, object?>
            {
                ["Name"] = "Inserted",
                ["Age"] = 71,
                ["IsActive"] = true
            });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "Inserted" });

        results.Should().ContainSingle();
        results[0].Age.Should().Be(71);
    }

    [Fact]
    public async Task NamedUpdate_ShouldModifyTheMatchingRow()
    {
        await InitializeAsync();
        await SeedAsync("ToUpdate", 81, true);

        await SxmSql.RunStatementAsync(
            UpdateAgeByName,
            new Dictionary<string, object?> { ["Age"] = 88, ["Name"] = "ToUpdate" });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "ToUpdate" });

        results.Should().ContainSingle();
        results[0].Age.Should().Be(88);
    }

    [Fact]
    public async Task NamedDelete_ShouldRemoveTheMatchingRow()
    {
        await InitializeAsync();
        await SeedAsync("ToDelete", 91, true);

        await SxmSql.RunStatementAsync(
            DeleteByName,
            new Dictionary<string, object?> { ["Name"] = "ToDelete" });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "ToDelete" });

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task NamedStatement_ShouldTakePrecedenceOverInlineSqlInterpretation()
    {
        await InitializeAsync();
        await SeedAsync("Precedence", 101, true);

        // A registered name is checked before the string is considered as SQL. Since these
        // statement names are not valid SQL, a successful query proves the name path ran first.
        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "Precedence" });

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task NamedSelect_WithInjectionPayloadAsParameter_ShouldNotExecuteThePayload()
    {
        await InitializeAsync();
        await SeedAsync("Safe", 111, true);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "'; DROP TABLE SimpleEntity; --" });

        results.Should().BeEmpty();

        // The table must still be intact and still hold the seeded row.
        List<SimpleEntity> survivors = await SxmSql.RunStatementAsync<SimpleEntity>(
            SelectByName,
            new Dictionary<string, object?> { ["Name"] = "Safe" });

        survivors.Should().ContainSingle();
    }

    #endregion

    #region Unknown and invalid statement names

    [Fact]
    public async Task UnknownStatementName_ShouldThrowArgumentExceptionNamingTheStatement()
    {
        await InitializeAsync();

        // Resolution fails twice over: the name is in no statement dictionary, and it is not
        // parseable as SQL either. The second failure is the one that surfaces.
        Func<Task> act = async () => await SxmSql.RunStatementAsync("noSuchStatementName");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*could not be found or identified*");
    }

    [Fact]
    public async Task UnknownStatementName_WithParameters_ShouldStillThrowArgumentException()
    {
        await InitializeAsync();

        Func<Task> act = async () => await SxmSql.RunStatementAsync(
            "noSuchStatementName",
            new Dictionary<string, object?> { ["Name"] = "irrelevant" });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*could not be found or identified*");
    }

    [Fact]
    public async Task UnknownStatementName_ThatIsVeryLong_ShouldBeTruncatedInTheMessage()
    {
        await InitializeAsync();

        // Names longer than 30 characters are abbreviated in the error message so a huge SQL
        // blob cannot flood the exception text.
        string longName = new string('z', 80);

        Func<Task> act = async () => await SxmSql.RunStatementAsync(longName);

        var assertion = await act.Should().ThrowAsync<ArgumentException>();
        assertion.Which.Message.Should().Contain("...");
        assertion.Which.Message.Should().NotContain(longName);
    }

    [Fact]
    public async Task EmptyStatementName_ShouldThrowArgumentException()
    {
        await InitializeAsync();

        Func<Task> act = async () => await SxmSql.RunStatementAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task NullStatementName_ShouldThrowArgumentException()
    {
        await InitializeAsync();

        Func<Task> act = async () => await SxmSql.RunStatementAsync(null!);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public async Task StatementNameFromADifferentTestInstance_ShouldNotResolve()
    {
        await InitializeAsync();

        // Statement names are registered per initialization. A name shaped like ours but with a
        // different id must not resolve, confirming lookups are not matching loosely.
        string foreignName = $"getSimpleByName_{Guid.NewGuid():N}";

        Func<Task> act = async () => await SxmSql.RunStatementAsync(foreignName);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*could not be found or identified*");
    }

    #endregion
}
