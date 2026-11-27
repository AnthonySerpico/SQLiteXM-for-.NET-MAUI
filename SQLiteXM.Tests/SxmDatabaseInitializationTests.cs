using System.Reflection;
using System.Text.Json;
using Xunit;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for <see cref="SxmDatabase"/> initialization guard rails: the not-initialized guard, entity
/// registration overloads, stream argument validation and the file-name based initialization path.
/// </summary>
/// <remarks>
/// These tests reset SQLiteXM's process-wide state, so they share the non-parallel
/// "InitializationPattern" collection and restore the standard TestBase configuration in Dispose().
/// </remarks>
[Collection("InitializationPattern")]
public class SxmDatabaseInitializationTests : IDisposable
{
    private static readonly string Folder;
    private readonly string _testId = Guid.NewGuid().ToString("N");
    private bool _disposed;

    static SxmDatabaseInitializationTests()
    {
        Folder = Path.Combine(TestBase.TestRootFolder, "SxmDatabaseInitialization");
        Directory.CreateDirectory(Folder);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            foreach (string file in Directory.GetFiles(Folder, $"*{_testId}*"))
            {
                try { File.Delete(file); } catch { /* best effort cleanup */ }
            }
        }
        catch { /* best effort cleanup */ }

        SxmDatabase.ResetForTestingAsync().GetAwaiter().GetResult();
        SxmDatabaseOptions initOptions = new SxmDatabaseOptions
        {
            DatabaseFolderOverride = Path.Combine(TestBase.TestRootFolder, "test_database")
        };
        Directory.CreateDirectory(initOptions.DatabaseFolderOverride);
        string statementsPath = Path.Combine(initOptions.DatabaseFolderOverride, "statements.json");
        if (!File.Exists(statementsPath))
        {
            var config = new
            {
                version = 1L,
                databases = new[] { new { database = "test_database", isDefault = true } }
            };
            File.WriteAllText(statementsPath, JsonSerializer.Serialize(config));
        }
        using FileStream stream = File.OpenRead(statementsPath);
        SxmDatabase.InitializeAsync(stream, initOptions).GetAwaiter().GetResult();

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
    }

    private string CreateStatementsFile(string extension = ".json")
    {
        string path = Path.Combine(Folder, $"statements_{_testId}{extension}");
        var config = new
        {
            version = 1L,
            databases = new[] { new { database = $"db_{_testId}", isDefault = true } }
        };
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    private SxmDatabaseOptions CreateOptions() => new() { DatabaseFolderOverride = Folder };

    private static Task InvokeFileInitializeAsync(string fileName, SxmDatabaseOptions? options)
    {
        MethodInfo method = typeof(SxmDatabase).GetMethod(
            "InitializeAsync",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(string), typeof(SxmDatabaseOptions) },
            null)!;

        return (Task)method.Invoke(null, new object?[] { fileName, options })!;
    }

    // ---------- Not-initialized guard ----------

    [Fact]
    public async Task EnsureInitialized_AfterReset_Throws()
    {
        await SxmDatabase.ResetForTestingAsync();

        Action act = SxmDatabase.EnsureInitialized;

        act.Should().Throw<InvalidOperationException>().WithMessage("*has not been initialized*");
    }

    [Fact]
    public async Task RegisterEntitiesAsync_BeforeInitialize_ThrowsAndFaultsReadySignal()
    {
        await SxmDatabase.ResetForTestingAsync();

        Func<Task> act = () => SxmDatabase.RegisterEntitiesAsync(typeof(SimpleEntity));

        await act.Should().ThrowAsync<InvalidOperationException>();
        await ((Func<Task>)SxmDatabase.EnsureReadyAsync).Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------- Registration overloads ----------

    [Fact]
    public async Task RegisterEntitiesAsync_EmptyList_CompletesReadySignal()
    {
        await SxmDatabase.ResetForTestingAsync();
        using (FileStream stream = File.OpenRead(CreateStatementsFile()))
            await SxmDatabase.InitializeAsync(stream, CreateOptions());

        await SxmDatabase.RegisterEntitiesAsync();

        Task ready = SxmDatabase.EnsureReadyAsync();
        ready.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterEntityAsync_Generic_RegistersEntity()
    {
        await SxmDatabase.ResetForTestingAsync();
        using (FileStream stream = File.OpenRead(CreateStatementsFile()))
            await SxmDatabase.InitializeAsync(stream, CreateOptions());

#pragma warning disable SXM0001 // Deprecated API is still supported and must keep working.
        await SxmDatabase.RegisterEntityAsync<SimpleEntity>();
#pragma warning restore SXM0001

        SimpleEntity entity = new SimpleEntity { Name = "generic", Age = 1, IsActive = true };
        await entity.SaveAsync();
        entity.id.Should().BeGreaterThan(0);
    }

    // ---------- Stream overload validation ----------

    [Fact]
    public async Task InitializeAsync_NullStream_ThrowsAndLeavesUninitialized()
    {
        await SxmDatabase.ResetForTestingAsync();

        Func<Task> act = () => SxmDatabase.InitializeAsync(null!, CreateOptions());

        await act.Should().ThrowAsync<ArgumentNullException>();
        ((Action)SxmDatabase.EnsureInitialized).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task InitializeAsync_UnreadableStream_Throws()
    {
        await SxmDatabase.ResetForTestingAsync();
        MemoryStream stream = new MemoryStream();
        stream.Dispose();

        Func<Task> act = () => SxmDatabase.InitializeAsync(stream, CreateOptions());

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*readable*");
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_SecondCallIsNoOp()
    {
        await SxmDatabase.ResetForTestingAsync();
        using (FileStream stream = File.OpenRead(CreateStatementsFile()))
            await SxmDatabase.InitializeAsync(stream, CreateOptions());

        // Second call must return without reading the stream at all, so even an unreadable stream is fine.
        MemoryStream unread = new MemoryStream();
        unread.Dispose();
        Func<Task> act = () => SxmDatabase.InitializeAsync(unread, CreateOptions());

        await act.Should().NotThrowAsync();
    }

    // ---------- File-name overload (internal) ----------

    [Fact]
    public async Task InitializeFromFile_ValidJson_Initializes()
    {
        await SxmDatabase.ResetForTestingAsync();

        await InvokeFileInitializeAsync(CreateStatementsFile(), CreateOptions());

        ((Action)SxmDatabase.EnsureInitialized).Should().NotThrow();

        // Already-initialized fast path.
        await InvokeFileInitializeAsync("does-not-matter.json", CreateOptions());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InitializeFromFile_BlankName_ThrowsArgumentNull(string fileName)
    {
        await SxmDatabase.ResetForTestingAsync();

        Func<Task> act = () => InvokeFileInitializeAsync(fileName, CreateOptions());

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeFromFile_MissingRelativeFile_ThrowsFileNotFound()
    {
        await SxmDatabase.ResetForTestingAsync();

        Func<Task> act = () => InvokeFileInitializeAsync($"missing_{_testId}.json", CreateOptions());

        (await act.Should().ThrowAsync<FileNotFoundException>())
            .WithMessage($"*{AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)}*");
    }

    [Fact]
    public async Task InitializeFromFile_UnknownExtension_ThrowsArgumentException()
    {
        await SxmDatabase.ResetForTestingAsync();
        string path = CreateStatementsFile(".dat");

        Func<Task> act = () => InvokeFileInitializeAsync(path, CreateOptions());

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*unknown SQL statements file type*");
        ((Action)SxmDatabase.EnsureInitialized).Should().Throw<InvalidOperationException>();
    }
}

/// <summary>
/// Direct tests for internal <see cref="SxmDatabase"/> helpers: version stamping, table existence,
/// column-name cache maintenance, trigger helpers and statement-based column lookup.
/// </summary>
[Collection("Sequential")]
public class SxmDatabaseHelperTests : TestBase
{
    private static string NewTable() => $"dbi_{Guid.NewGuid():N}";

    private static async Task CreateTableAsync(string table)
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.ExecuteTableStatementAsync($"CREATE TABLE \"{table}\" (id INTEGER PRIMARY KEY, name TEXT, amount REAL)");
        await t.CommitTransactionAsync();
    }

    private static async Task DropTableAsync(string table)
    {
        SxmDatabase.ClearColumnCacheForTable(table);
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.ExecuteTableStatementAsync($"DROP TABLE IF EXISTS \"{table}\"");
        await t.CommitTransactionAsync();
    }

    private static async Task SetUserVersionAsync(long version)
    {
        await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
        await t.ExecuteTableStatementAsync($"PRAGMA user_version = {version}");
        await t.CommitTransactionAsync();
    }

    [Fact]
    public async Task DbVersionNumber_ReadAndReset_RoundTrip()
    {
        long original = await SxmDatabase.GetDbVersionNumberAsync(TestDatabaseName!);
        try
        {
            await SetUserVersionAsync(7);
            (await SxmDatabase.GetDbVersionNumberAsync(TestDatabaseName!)).Should().Be(7);

            await SxmDatabase.DeleteDbVersionNumberAsync(TestDatabaseName!);
            (await SxmDatabase.GetDbVersionNumberAsync(TestDatabaseName!)).Should().Be(0);
        }
        finally
        {
            await SetUserVersionAsync(original);
        }
    }

    [Fact]
    public void SetJournalMode_IsNoOp()
    {
        Action act = SxmDatabase.SetJournalMode;

        act.Should().NotThrow();
    }

    [Fact]
    public async Task DoesTableExist_ReportsPresenceAndAbsence()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await using SxmUTransaction t = SxmUTransaction.Create(TestDatabaseName);
            (await SxmDatabase.DoesTableExistAsync(table, t.Connection!)).Should().BeTrue();
            (await SxmDatabase.DoesTableExistAsync(NewTable(), t.Connection!)).Should().BeFalse();
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task CreateTable_NullDatabase_DoesNothing()
    {
        Func<Task> act = () => SxmDatabase.CreateTableAsync(null, "whatever");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetTableColumnNames_ReturnsColumns_AndSnapshotIsIndependent()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            Dictionary<string, string> first = await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table);
            first.Should().ContainKeys("id", "name", "amount");

            first.Remove("name");
            Dictionary<string, string> second = await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table);
            second.Should().ContainKey("name", "callers receive a snapshot, not the live cache");
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public async Task GetTableColumnNames_NullOrEmptyTable_Throws()
    {
        Func<Task> nullName = () => SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, (string?)null);
        Func<Task> empty = () => SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, string.Empty);

        await nullName.Should().ThrowAsync<Exception>();
        await empty.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ColumnCache_AddRemoveClear_MaintainCachedTable()
    {
        string table = NewTable();
        await CreateTableAsync(table);
        try
        {
            await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table);

            SxmDatabase.AddColumnNameType(table, "extra", "INTEGER");
            (await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table)).Should().ContainKey("extra");

            SxmDatabase.RemoveColumnNameType(table, "extra");
            (await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table)).Should().NotContainKey("extra");

            SxmDatabase.AddColumnNameType(table, "stale", "TEXT");
            SxmDatabase.ClearColumnCacheForTable(table);
            (await SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, table)).Should().NotContainKey("stale");
        }
        finally
        {
            await DropTableAsync(table);
        }
    }

    [Fact]
    public void ColumnCache_UncachedTable_AddAndRemoveAreIgnored()
    {
        string table = NewTable();

        Action act = () =>
        {
            SxmDatabase.AddColumnNameType(table, "c", "TEXT");
            SxmDatabase.RemoveColumnNameType(table, "c");
            SxmDatabase.ClearColumnCacheForTable(table);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetAllTriggers_NullConnection_ReturnsEmpty()
    {
        List<string> triggers = await SxmDatabase.GetAllTriggersAsync(null, string.Empty);

        triggers.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTableColumnNames_UnknownStatementType_Throws()
    {
        Func<Task> act = () => SxmDatabase.GetTableColumnNamesAsync(TestDatabaseName, "anything", SxmDefines.SqlStatementType.Unknown);

        await act.Should().ThrowAsync<Exception>();
    }
}
