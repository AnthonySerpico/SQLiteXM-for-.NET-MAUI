using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for the three bulk insert entry points:
/// <list type="bullet">
/// <item><description><see cref="SxmSql.BulkInsertAsync{T}"/> - standalone, owns its own transaction.</description></item>
/// <item><description><see cref="SxmTransaction.BulkInsertAsync{T}"/> - runs inside an existing context transaction.</description></item>
/// <item><description><see cref="SxmLinqExtensions.BulkInsertAsync{T}"/> - LINQ-table wrapper over the SxmTransaction method.</description></item>
/// </list>
/// All three share <c>SxmBulkInsertHelpers</c>, so shared behavior (id/synchId population, batching,
/// validation) is verified once per entry point via [Theory], while transaction semantics are verified
/// per entry point where they differ.
/// </summary>
[Collection("Sequential")]
public class BulkInsertTests : TestBase
{
    public BulkInsertTests()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    public enum Api { SxmSql, SxmTransaction, Linq }

    public static IEnumerable<object[]> AllApis()
    {
        yield return new object[] { Api.SxmSql };
        yield return new object[] { Api.SxmTransaction };
        yield return new object[] { Api.Linq };
    }

    private static string NewPrefix() => "Bulk_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private static List<SimpleEntity> BuildEntities(string prefix, int count)
    {
        var list = new List<SimpleEntity>(count);
        for (int i = 0; i < count; i++)
            list.Add(new SimpleEntity { Name = $"{prefix}_{i}", Age = i, IsActive = i % 2 == 0 });
        return list;
    }

    /// <summary>
    /// Runs a bulk insert through the requested API. For the transaction-scoped APIs the transaction
    /// is created and disposed (auto-commit) inside this helper.
    /// </summary>
    private async Task<int> BulkInsertViaAsync<T>(Api api, List<T> entities, int batchRows = SxmBulkInsertHelpers.DefaultBatchRows) where T : SxmEntity
    {
        switch (api)
        {
            case Api.SxmSql:
                return await SxmSql.BulkInsertAsync(entities, batchRows, TestDatabaseName);

            case Api.SxmTransaction:
                await using (var ctx = new SxmTransaction(TestDatabaseName))
                    return await ctx.BulkInsertAsync(entities, batchRows);

            case Api.Linq:
                await using (var ctx = new SxmTransaction(TestDatabaseName))
                    return await ctx.GetTable<T>().BulkInsertAsync(entities, batchRows);

            default:
                throw new ArgumentOutOfRangeException(nameof(api));
        }
    }

    private async Task<List<SimpleEntity>> GetByPrefixAsync(string prefix)
    {
        await using var ctx = new SxmTransaction(TestDatabaseName);
        return ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).OrderBy(e => e.id).ToList();
    }

    private async Task DeleteByPrefixAsync(string prefix)
    {
        await using var ctx = new SxmTransaction(TestDatabaseName);
        await ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).DeleteAsync();
    }

    // ------------------------------------------------------------------
    // Shared behavior - verified for each entry point
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ShouldPersistAllRows_AndReturnCount(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 7);

        int inserted = await BulkInsertViaAsync(api, entities);

        inserted.Should().Be(7);
        var rows = await GetByPrefixAsync(prefix);
        rows.Should().HaveCount(7);
        rows.Select(r => r.Name).Should().BeEquivalentTo(entities.Select(e => e.Name));

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ShouldPopulateIds_MatchingDatabaseRows(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 10);

        await BulkInsertViaAsync(api, entities);

        entities.Should().OnlyContain(e => e.id > 0, "every entity should have a database-generated id");
        entities.Select(e => e.id).Should().OnlyHaveUniqueItems();

        // Positional mapping must be exact: the id assigned to each entity must belong to the row carrying that entity's data.
        foreach (var e in entities)
        {
            var row = await VerifyEntityExistsInDbAsync<SimpleEntity>(e.id);
            row.Should().NotBeNull();
            row!.Name.Should().Be(e.Name, $"id {e.id} should map to the row with Name '{e.Name}'");
            row.Age.Should().Be(e.Age);
            row.IsActive.Should().Be(e.IsActive);
        }

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ShouldPopulateSynchId_MatchingDatabaseRows(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 5);

        await BulkInsertViaAsync(api, entities);

        entities.Should().OnlyContain(e => e.synchId.HasValue && e.synchId != Guid.Empty);
        entities.Select(e => e.synchId).Should().OnlyHaveUniqueItems();

        var rows = await GetByPrefixAsync(prefix);
        foreach (var e in entities)
            rows.Single(r => r.id == e.id).synchId.Should().Be(e.synchId, "synchId written to the database should equal the one assigned in memory");

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ShouldMatchSaveAsyncState(Api api)
    {
        string prefix = NewPrefix();

        var saved = new SimpleEntity { Name = $"{prefix}_saved", Age = 1, IsActive = true };
        await saved.SaveAsync();

        var bulk = new List<SimpleEntity> { new SimpleEntity { Name = $"{prefix}_bulk", Age = 2, IsActive = false } };
        await BulkInsertViaAsync(api, bulk);

        // Same post-insert contract as SaveAsync: id and synchId both populated.
        bulk[0].id.Should().BeGreaterThan(saved.id);
        bulk[0].synchId.Should().NotBeNull();
        saved.synchId.Should().NotBeNull();

        // A bulk-inserted entity is fully usable afterwards: update and delete via SaveAsync/DeleteAsync.
        bulk[0].Age = 99;
        await bulk[0].SaveAsync();
        (await VerifyEntityExistsInDbAsync<SimpleEntity>(bulk[0].id))!.Age.Should().Be(99);

        await bulk[0].DeleteAsync();
        await VerifyEntityNotInDbAsync<SimpleEntity>(bulk[0].id);

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [InlineData(Api.SxmSql, 1)]
    [InlineData(Api.SxmSql, 3)]
    [InlineData(Api.SxmSql, 1000)]
    [InlineData(Api.SxmTransaction, 1)]
    [InlineData(Api.SxmTransaction, 3)]
    [InlineData(Api.SxmTransaction, 1000)]
    [InlineData(Api.Linq, 1)]
    [InlineData(Api.Linq, 3)]
    [InlineData(Api.Linq, 1000)]
    public async Task BulkInsert_WithVariousBatchSizes_ShouldInsertAllRowsCorrectly(Api api, int batchRows)
    {
        // 23 rows: not a multiple of 3, so the last batch is partial; 1000 forces the MaxParameters cap.
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 23);

        int inserted = await BulkInsertViaAsync(api, entities, batchRows);

        inserted.Should().Be(23);
        var rows = await GetByPrefixAsync(prefix);
        rows.Should().HaveCount(23);
        foreach (var e in entities)
            rows.Single(r => r.id == e.id).Name.Should().Be(e.Name);

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_LargeList_ShouldInsertAllRows(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 2500);

        int inserted = await BulkInsertViaAsync(api, entities);

        inserted.Should().Be(2500);
        (await GetByPrefixAsync(prefix)).Should().HaveCount(2500);
        entities.Select(e => e.id).Should().OnlyHaveUniqueItems();

        await DeleteByPrefixAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_EmptyList_ShouldReturnZero(Api api)
    {
        int before = await GetEntityCountFromDb<SimpleEntity>();

        int inserted = await BulkInsertViaAsync(api, new List<SimpleEntity>());

        inserted.Should().Be(0);
        (await GetEntityCountFromDb<SimpleEntity>()).Should().Be(before);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_AllTypesEntity_ShouldRoundTripValues(Api api)
    {
        var guid = Guid.NewGuid();
        var now = new DateTime(2024, 5, 17, 13, 45, 30, DateTimeKind.Utc);
        var entities = new List<AllTypesEntity>
        {
            new AllTypesEntity
            {
                IntValue = 42, LongValue = long.MaxValue, DecimalValue = 12345.6789m, DoubleValue = 3.25,
                BoolValue = true, StringValue = "bulk-alltypes", GuidValue = guid,
                DateTimeValue = now, BlobValue = new byte[] { 1, 2, 3, 4 },
                NullableInt = null, NullableGuid = guid
            },
            new AllTypesEntity
            {
                IntValue = -1, StringValue = null, BoolValue = false, NullableInt = 7, BlobValue = Array.Empty<byte>()
            }
        };

        await BulkInsertViaAsync(api, entities);

        var first = await VerifyEntityExistsInDbAsync<AllTypesEntity>(entities[0].id);
        first.Should().NotBeNull();
        first!.IntValue.Should().Be(42);
        first.LongValue.Should().Be(long.MaxValue);
        first.DecimalValue.Should().Be(12345.6789m);
        first.DoubleValue.Should().Be(3.25);
        first.BoolValue.Should().BeTrue();
        first.StringValue.Should().Be("bulk-alltypes");
        first.GuidValue.Should().Be(guid);
        first.DateTimeValue.Should().Be(now);
        first.BlobValue.Should().Equal(1, 2, 3, 4);
        first.NullableInt.Should().BeNull();
        first.NullableGuid.Should().Be(guid);

        var second = await VerifyEntityExistsInDbAsync<AllTypesEntity>(entities[1].id);
        second.Should().NotBeNull();
        second!.IntValue.Should().Be(-1);
        second.StringValue.Should().BeNull();
        second.BoolValue.Should().BeFalse();
        second.NullableInt.Should().Be(7);

        await using var ctx = new SxmTransaction(TestDatabaseName);
        await ctx.GetTable<AllTypesEntity>().Where(e => e.id == entities[0].id || e.id == entities[1].id).DeleteAsync();
    }

    // ------------------------------------------------------------------
    // Validation - verified for each entry point
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_NullList_ShouldThrowArgumentNullException(Api api)
    {
        Func<Task> act = () => BulkInsertViaAsync<SimpleEntity>(api, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ListContainingNull_ShouldThrowArgumentException_AndWriteNothing(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);
        entities.Insert(1, null!);

        Func<Task> act = () => BulkInsertViaAsync(api, entities);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("index 1");
        (await GetByPrefixAsync(prefix)).Should().BeEmpty("validation must fail before any row is written");
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_EntityWithExistingId_ShouldThrowInvalidOperationException_AndWriteNothing(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);
        entities[2].id = 12345;

        Func<Task> act = () => BulkInsertViaAsync(api, entities);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("index 2").And.Contain("12345");
        (await GetByPrefixAsync(prefix)).Should().BeEmpty("validation must fail before any row is written");
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ZeroBatchRows_ShouldThrowArgumentOutOfRangeException(Api api)
    {
        var entities = BuildEntities(NewPrefix(), 1);

        Func<Task> act = () => BulkInsertViaAsync(api, entities, batchRows: 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(Api.SxmSql)]
    [InlineData(Api.SxmTransaction)]
    public async Task BulkInsert_MixedRuntimeTypes_ShouldThrowArgumentException_AndWriteNothing(Api api)
    {
        string prefix = NewPrefix();
        // Static type is SxmEntity, so T == SxmEntity and every element mismatches; index 0 is reported first.
        var entities = new List<SxmEntity>
        {
            new SimpleEntity { Name = $"{prefix}_a" },
            new AllTypesEntity { StringValue = $"{prefix}_b" }
        };

        Func<Task> act = api == Api.SxmSql
            ? () => SxmSql.BulkInsertAsync(entities, databaseName: TestDatabaseName)
            : async () =>
            {
                await using var ctx = new SxmTransaction(TestDatabaseName);
                await ctx.BulkInsertAsync(entities);
            };

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("requires all entities to be of type 'SxmEntity'").And.Contain("index 0");
        (await GetByPrefixAsync(prefix)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // SxmSql.BulkInsertAsync - standalone transaction semantics
    // ------------------------------------------------------------------

    [Fact]
    public async Task SxmSql_BulkInsert_ShouldCommitWithoutExplicitTransaction()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 4);

        await SxmSql.BulkInsertAsync(entities, databaseName: TestDatabaseName);

        // Visible on an independent raw connection => committed.
        long count = await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM SimpleEntity WHERE Name LIKE '{prefix}%'");
        count.Should().Be(4);

        await DeleteByPrefixAsync(prefix);
    }

    [Fact]
    public async Task SxmSql_BulkInsert_ShouldResolveDatabaseNameFromEntity()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 2);

        // No databaseName argument: resolved from the entity type.
        int inserted = await SxmSql.BulkInsertAsync(entities);

        inserted.Should().Be(2);
        (await GetByPrefixAsync(prefix)).Should().HaveCount(2);

        await DeleteByPrefixAsync(prefix);
    }

    [Fact]
    public async Task SxmSql_BulkInsert_WithCancelledToken_ShouldThrow_AndWriteNothing()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 5);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => SxmSql.BulkInsertAsync(entities, databaseName: TestDatabaseName, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await GetByPrefixAsync(prefix)).Should().BeEmpty("the standalone API must roll back its own transaction on failure");
    }

    // ------------------------------------------------------------------
    // SxmTransaction.BulkInsertAsync - context transaction semantics
    // ------------------------------------------------------------------

    [Fact]
    public async Task SxmTransaction_BulkInsert_ShouldAutoCommitOnDispose()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.BulkInsertAsync(entities);
        }

        (await GetByPrefixAsync(prefix)).Should().HaveCount(3);

        await DeleteByPrefixAsync(prefix);
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_ThenRollback_ShouldNotPersist()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            int inserted = await ctx.BulkInsertAsync(entities);
            inserted.Should().Be(3);
            entities.Should().OnlyContain(e => e.id > 0, "ids are generated before rollback");

            await ctx.RollbackTransactionAsync();
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_ShouldShareTransactionWithLinqAndEntityDml()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.BulkInsertAsync(entities);

            // Entity DML (ambient) and LINQ in the same transaction see and can modify the bulk rows.
            var extra = new SimpleEntity { Name = $"{prefix}_extra", Age = 50 };
            await extra.SaveAsync();

            int updated = await ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, 77)
                .UpdateAsync();
            updated.Should().Be(4);

            // Everything rolls back together.
            await ctx.RollbackTransactionAsync();
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty("bulk rows, SaveAsync row and LINQ update all belong to one transaction");
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_ShouldJoinAmbientTransaction()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);

        await using (var outer = new SxmTransaction(TestDatabaseName))
        {
            await using (var inner = new SxmTransaction(TestDatabaseName))
            {
                await inner.BulkInsertAsync(entities);
            }

            // Inner disposal must not commit - the outer owns the transaction.
            await outer.RollbackTransactionAsync();
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_OnFaultedContext_ShouldBeSkippedAndReturnZero()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 3);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            // Fault the context with a failing statement on the shared transaction.
            Func<Task> bad = () => ctx.RunStatementAsync<SimpleEntity>("SELECT * FROM NoSuchTable_" + prefix);
            await bad.Should().ThrowAsync<Exception>();
            ctx.Faulted.Should().BeTrue();

            int inserted = await ctx.BulkInsertAsync(entities);

            inserted.Should().Be(0, "writes on a faulted context are skipped");
            entities.Should().OnlyContain(e => e.id == 0, "skipped entities must remain untouched");
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var ctx = new SxmTransaction(TestDatabaseName);
        await ctx.DisposeAsync();

        Func<Task> act = () => ctx.BulkInsertAsync(BuildEntities(NewPrefix(), 1));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_MultipleCallsInOneTransaction_ShouldAllCommit()
    {
        string prefix = NewPrefix();
        var first = BuildEntities(prefix + "_A", 5);
        var second = BuildEntities(prefix + "_B", 5);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.BulkInsertAsync(first);
            await ctx.BulkInsertAsync(second);
        }

        (await GetByPrefixAsync(prefix)).Should().HaveCount(10);
        first.Concat(second).Select(e => e.id).Should().OnlyHaveUniqueItems();

        await DeleteByPrefixAsync(prefix);
    }

    // ------------------------------------------------------------------
    // Constraints - unique index violations and foreign-key linked rows
    // ------------------------------------------------------------------

    private static List<IndexedEntity> BuildIndexed(string prefix, int count)
    {
        var list = new List<IndexedEntity>(count);
        for (int i = 0; i < count; i++)
            list.Add(new IndexedEntity { FirstName = $"F{i}", LastName = prefix, Email = $"{prefix}_{i}@test.local", CreatedDate = DateTime.UtcNow });
        return list;
    }

    private async Task<int> CountIndexedAsync(string prefix)
    {
        await using var ctx = new SxmTransaction(TestDatabaseName);
        return ctx.GetTable<IndexedEntity>().Count(e => e.LastName == prefix);
    }

    private async Task DeleteIndexedAsync(string prefix)
    {
        await using var ctx = new SxmTransaction(TestDatabaseName);
        await ctx.GetTable<IndexedEntity>().Where(e => e.LastName == prefix).DeleteAsync();
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_UniqueIndexViolationWithinBatch_ShouldThrow_AndWriteNothing(Api api)
    {
        string prefix = NewPrefix();
        var entities = BuildIndexed(prefix, 5);
        entities[3].Email = entities[1].Email; // duplicate inside the same INSERT statement

        Func<Task> act = () => BulkInsertViaAsync(api, entities);

        await act.Should().ThrowAsync<Exception>();
        (await CountIndexedAsync(prefix)).Should().Be(0, "a failed statement must not leave partial rows");
        entities.Should().OnlyContain(e => e.id == 0, "no ids may be assigned when the statement fails");
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_UniqueIndexViolationInLaterBatch_ShouldRollBackEarlierBatches(Api api)
    {
        // 9 rows at 3 rows/stmt = 3 statements. Batches 1 and 2 succeed; batch 3 violates the unique index.
        string prefix = NewPrefix();
        var entities = BuildIndexed(prefix, 9);
        entities[8].Email = entities[0].Email;

        Func<Task> act = () => BulkInsertViaAsync(api, entities, batchRows: 3);

        await act.Should().ThrowAsync<Exception>();
        (await CountIndexedAsync(prefix)).Should().Be(0, "earlier batches must be rolled back with the failing one");
    }

    [Fact]
    public async Task SxmTransaction_BulkInsert_UniqueIndexViolation_ShouldFaultContext_AndRollBackPriorWork()
    {
        string prefix = NewPrefix();
        var good = BuildEntities(prefix, 3);
        var bad = BuildIndexed(prefix, 2);
        bad[1].Email = bad[0].Email;

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.BulkInsertAsync(good);

            Func<Task> act = () => ctx.BulkInsertAsync(bad);
            await act.Should().ThrowAsync<Exception>();

            ctx.Faulted.Should().BeTrue();
            (await ctx.BulkInsertAsync(BuildEntities(prefix + "_after", 2))).Should().Be(0, "subsequent writes are skipped on a faulted context");
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty("faulted context must roll back on dispose, including work done before the failure");
        (await CountIndexedAsync(prefix)).Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_UniqueIndexViolationAgainstExistingRow_ShouldThrow_AndWriteNothing(Api api)
    {
        string prefix = NewPrefix();
        var existing = new IndexedEntity { FirstName = "X", LastName = prefix, Email = $"{prefix}_existing@test.local" };
        await existing.SaveAsync();

        var entities = BuildIndexed(prefix, 3);
        entities[2].Email = existing.Email;

        Func<Task> act = () => BulkInsertViaAsync(api, entities);

        await act.Should().ThrowAsync<Exception>();
        (await CountIndexedAsync(prefix)).Should().Be(1, "only the pre-existing row should remain");

        await DeleteIndexedAsync(prefix);
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_ChildrenReferencingBulkInsertedParents_ShouldLinkCorrectly(Api api)
    {
        string prefix = NewPrefix();
        var parents = new List<ParentEntity>
        {
            new ParentEntity { ParentName = $"{prefix}_P0" },
            new ParentEntity { ParentName = $"{prefix}_P1" }
        };
        await BulkInsertViaAsync(api, parents);
        parents.Should().OnlyContain(p => p.id > 0);

        var children = new List<ChildEntity>();
        for (int i = 0; i < 6; i++)
            children.Add(new ChildEntity { ChildName = $"{prefix}_C{i}", ParentId = parents[i % 2].id });

        int inserted = await BulkInsertViaAsync(api, children);
        inserted.Should().Be(6);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var joined = (from c in ctx.GetTable<ChildEntity>()
                          join p in ctx.GetTable<ParentEntity>() on c.ParentId equals p.id
                          where p.ParentName!.StartsWith(prefix)
                          select new { c.ChildName, p.ParentName }).ToList();

            joined.Should().HaveCount(6);
            joined.Count(j => j.ParentName!.EndsWith("_P0")).Should().Be(3);
            joined.Count(j => j.ParentName!.EndsWith("_P1")).Should().Be(3);

            await ctx.GetTable<ChildEntity>().Where(c => c.ChildName!.StartsWith(prefix)).DeleteAsync();
            await ctx.GetTable<ParentEntity>().Where(p => p.ParentName!.StartsWith(prefix)).DeleteAsync();
        }
    }

    // ------------------------------------------------------------------
    // SxmLinqExtensions.BulkInsertAsync - LINQ wrapper specifics
    // ------------------------------------------------------------------

    [Fact]
    public async Task Linq_BulkInsert_ShouldBeVisibleToLinqQueriesInSameTransaction()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 4);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var table = ctx.GetTable<SimpleEntity>();
            await table.BulkInsertAsync(entities);

            var seen = table.Where(e => e.Name!.StartsWith(prefix)).ToList();
            seen.Should().HaveCount(4);
            seen.Select(s => s.id).Should().BeEquivalentTo(entities.Select(e => e.id));
        }

        await DeleteByPrefixAsync(prefix);
    }

    [Fact]
    public async Task Linq_BulkInsert_ThenRollback_ShouldNotPersist()
    {
        string prefix = NewPrefix();
        var entities = BuildEntities(prefix, 4);

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.GetTable<SimpleEntity>().BulkInsertAsync(entities);
            await ctx.RollbackTransactionAsync();
        }

        (await GetByPrefixAsync(prefix)).Should().BeEmpty();
    }

    [Fact]
    public async Task Linq_BulkInsert_NullTable_ShouldThrowArgumentNullException()
    {
        SxmTable<SimpleEntity> table = null!;

        Func<Task> act = () => table.BulkInsertAsync(BuildEntities(NewPrefix(), 1));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
