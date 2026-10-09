using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for the <see cref="SxmSql.RunStatementAsync(string, string?)"/> family of overloads when
/// they are handed direct (inline) SQL rather than the name of a statement declared in
/// statements.json.
///
/// <para>
/// There are eight public overloads. They vary along two axes:
/// </para>
/// <list type="bullet">
/// <item>How parameters are supplied - none at all, a <c>Dictionary&lt;string, object?&gt;</c> of
/// named parameters, a <c>List&lt;object&gt;</c> of positional parameters, or a user object whose
/// properties supply the values.</item>
/// <item>How results come back - either mapped onto a result type <c>TResult</c>, or raw as a
/// <c>List&lt;Dictionary&lt;string, object?&gt;&gt;</c> where each dictionary is one row keyed by
/// column name.</item>
/// </list>
///
/// <para>
/// Named-statement lookup (resolving a statement *name* out of statements.json) is deliberately
/// not covered here, because it requires a statements file that declares named statements. See
/// <see cref="NamedStatementTests"/> for that path.
/// </para>
/// </summary>
[Collection("Sequential")]
public class RunStatementTests : TestBase
{
    /// <summary>
    /// Creates a unique marker string so each test's rows can be found without colliding with
    /// rows left behind by any other test in the shared test database.
    /// </summary>
    private static string NewMarker() => "RunStmt_" + Guid.NewGuid().ToString("N").Substring(0, 12);

    /// <summary>
    /// Inserts a SimpleEntity row directly so the SELECT overloads have known data to read back.
    /// </summary>
    private static async Task<SimpleEntity> InsertEntityAsync(string name, int age, bool isActive)
    {
        var entity = new SimpleEntity { Name = name, Age = age, IsActive = isActive };
        await entity.SaveAsync();
        return entity;
    }

    #region No-parameter overloads

    [Fact]
    public async Task RunStatementAsync_RawResult_WithNoParameters_ShouldReturnRowsKeyedByColumnName()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 41, true);

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            $"SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = '{marker}'");

        rows.Should().HaveCount(1);
        rows[0].Should().ContainKey("Name");
        rows[0]["Name"].Should().Be(marker);
        rows[0]["Age"].Should().Be(41L);
    }

    [Fact]
    public async Task RunStatementAsync_TypedResult_WithNoParameters_ShouldMapColumnsOntoProperties()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 42, true);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            $"SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = '{marker}'");

        results.Should().HaveCount(1);
        results[0].Name.Should().Be(marker);
        results[0].Age.Should().Be(42);
        results[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RunStatementAsync_WithNoMatchingRows_ShouldReturnEmptyListNotNull()
    {
        await InitializeSqliteXMAsync();

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            $"SELECT id, Name FROM SimpleEntity WHERE Name = '{NewMarker()}_absent'");

        rows.Should().NotBeNull();
        rows.Should().BeEmpty();
    }

    #endregion

    #region Dictionary (named parameter) overloads

    [Fact]
    public async Task RunStatementAsync_RawResult_WithDictionaryParameters_ShouldBindByName()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 50, true);

        var parameters = new Dictionary<string, object?> { ["Name"] = marker };

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name", parameters);

        rows.Should().HaveCount(1);
        rows[0]["Name"].Should().Be(marker);
        rows[0]["Age"].Should().Be(50L);
    }

    [Fact]
    public async Task RunStatementAsync_TypedResult_WithDictionaryParameters_ShouldBindByName()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 51, false);

        var parameters = new Dictionary<string, object?> { ["Name"] = marker };

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name", parameters);

        results.Should().HaveCount(1);
        results[0].Age.Should().Be(51);
        results[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RunStatementAsync_WithMultipleDictionaryParameters_ShouldBindEachIndependently()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 60, true);
        await InsertEntityAsync(marker, 70, true);

        // Both rows share a Name, so only the Age parameter distinguishes them. This proves the
        // parameters are bound to distinct placeholders rather than being positionally confused.
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = marker,
            ["Age"] = 70
        };

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name AND Age = @Age",
            parameters);

        results.Should().HaveCount(1);
        results[0].Age.Should().Be(70);
    }

    [Fact]
    public async Task RunStatementAsync_WithEmptyDictionary_ShouldBehaveLikeTheNoParameterOverload()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 52, true);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            $"SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = '{marker}'",
            new Dictionary<string, object?>());

        results.Should().HaveCount(1);
        results[0].Name.Should().Be(marker);
    }

    [Fact]
    public async Task RunStatementAsync_WithNullDictionaryParameterValue_ShouldBindSqlNull()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 53, true);

        // "Name = NULL" is never true in SQL, so binding a null value must produce zero rows
        // rather than being silently dropped or treated as a literal.
        var parameters = new Dictionary<string, object?> { ["Name"] = null };

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name", parameters);

        results.Should().BeEmpty();
    }

    #endregion

    #region List (positional parameter) overloads

    [Fact]
    public async Task RunStatementAsync_RawResult_WithPositionalParameters_ShouldBindInOrder()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 80, true);

        var parameters = new List<object> { marker };

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @p0", parameters);

        rows.Should().HaveCount(1);
        rows[0]["Age"].Should().Be(80L);
    }

    [Fact]
    public async Task RunStatementAsync_TypedResult_WithPositionalParameters_ShouldBindInOrder()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 81, true);
        await InsertEntityAsync(marker, 82, true);

        // Order matters here: @p0 is the name and @p1 is the age. If the two were swapped the
        // query would return nothing, so a single matching row proves ordering is honored.
        var parameters = new List<object> { marker, 82 };

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @p0 AND Age = @p1",
            parameters);

        results.Should().HaveCount(1);
        results[0].Age.Should().Be(82);
    }

    #endregion

    #region Non-SELECT statements

    [Fact]
    public async Task RunStatementAsync_WithParameterizedUpdate_ShouldModifyMatchingRows()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 90, false);

        await SxmSql.RunStatementAsync(
            "UPDATE SimpleEntity SET Age = @NewAge WHERE Name = @Name",
            new Dictionary<string, object?> { ["NewAge"] = 99, ["Name"] = marker });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = marker });

        results.Should().ContainSingle();
        results[0].Age.Should().Be(99);
    }

    [Fact]
    public async Task RunStatementAsync_WithParameterizedDelete_ShouldRemoveMatchingRows()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 91, false);

        await SxmSql.RunStatementAsync(
            "DELETE FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = marker });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = marker });

        results.Should().BeEmpty();
    }

    #endregion

    #region Parameterization safety (SQL injection)

    /// <summary>
    /// The classic injection payload. If a value like this were pasted into the SQL text instead
    /// of being bound as a parameter, the trailing statement would execute and destroy the table.
    /// Because it is bound, SQLite treats the whole thing as an opaque string.
    /// </summary>
    private const string DropTablePayload = "'; DROP TABLE SimpleEntity; --";

    [Fact]
    public async Task RunStatementAsync_WithInjectionPayloadAsNamedParameter_ShouldNotExecuteThePayload()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 10, true);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = DropTablePayload });

        // No row is named that, so the query simply finds nothing...
        results.Should().BeEmpty();

        // ...and, critically, the table is still there with our row in it.
        long surviving = await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM SimpleEntity WHERE Name = '{marker}'");
        surviving.Should().Be(1, "the injected DROP TABLE must never have executed");
    }

    [Fact]
    public async Task RunStatementAsync_WithInjectionPayloadAsPositionalParameter_ShouldNotExecuteThePayload()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();
        await InsertEntityAsync(marker, 11, true);

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @p0",
            new List<object> { DropTablePayload });

        results.Should().BeEmpty();

        long surviving = await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM SimpleEntity WHERE Name = '{marker}'");
        surviving.Should().Be(1, "the injected DROP TABLE must never have executed");
    }

    [Fact]
    public async Task RunStatementAsync_WithInjectionPayloadAsAValue_ShouldStoreAndReturnItVerbatim()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();

        // Store the payload as data, then read it back. It must survive the round trip as a
        // literal string - proving it was treated purely as a value on the way in and out.
        await SxmSql.RunStatementAsync(
            "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)",
            new Dictionary<string, object?>
            {
                ["Name"] = marker + DropTablePayload,
                ["Age"] = 12,
                ["IsActive"] = true
            });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = marker + DropTablePayload });

        results.Should().ContainSingle();
        results[0].Name.Should().Be(marker + DropTablePayload);
    }

    [Fact]
    public async Task RunStatementAsync_WithQuoteHeavyValue_ShouldRoundTripWithoutCorruption()
    {
        await InitializeSqliteXMAsync();
        string marker = NewMarker();

        // Single quotes are the characters that break naive string concatenation. Doubling,
        // stripping, or escaping them incorrectly would all show up as a mismatch here.
        string awkward = $"{marker} O'Brien ''double'' \"quoted\" \\backslash";

        await SxmSql.RunStatementAsync(
            "INSERT INTO SimpleEntity (Name, Age, IsActive) VALUES (@Name, @Age, @IsActive)",
            new Dictionary<string, object?>
            {
                ["Name"] = awkward,
                ["Age"] = 13,
                ["IsActive"] = false
            });

        List<SimpleEntity> results = await SxmSql.RunStatementAsync<SimpleEntity>(
            "SELECT id, Name, Age, IsActive FROM SimpleEntity WHERE Name = @Name",
            new Dictionary<string, object?> { ["Name"] = awkward });

        results.Should().ContainSingle();
        results[0].Name.Should().Be(awkward);
    }

    #endregion

    #region Unresolvable statements

    [Fact]
    public async Task RunStatementAsync_WithUnrecognizableText_ShouldThrowArgumentException()
    {
        await InitializeSqliteXMAsync();

        // The text is neither a known statement name nor parseable SQL, so resolution fails
        // before the statement ever reaches SQLite.
        Func<Task> act = async () => await SxmSql.RunStatementAsync("this is not sql at all");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*could not be found or identified*");
    }

    [Fact]
    public async Task RunStatementAsync_WithEmptyStatementName_ShouldThrowArgumentException()
    {
        await InitializeSqliteXMAsync();

        Func<Task> act = async () => await SxmSql.RunStatementAsync(string.Empty);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    #endregion
}
