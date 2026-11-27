using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Covers the public async LINQ helpers that were not exercised elsewhere:
/// every SumAsync/AverageAsync overload (SxmTable and IQueryable, nullable and non-nullable),
/// the predicate overloads of First/FirstOrDefault/Single/SingleOrDefault/Any/Count,
/// and all four Set(...).UpdateAsync() overloads.
/// </summary>
[Collection("Sequential")]
public class LinqAggregateAndPredicateTests : TestBase
{
    private static string NewPrefix() => $"Agg_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

    private static async Task SeedAllTypesAsync(SxmTransaction ctx, string prefix)
    {
        await ctx.InsertAsync(new AllTypesEntity { StringValue = $"{prefix}_1", IntValue = 1, LongValue = 10, FloatValue = 1.5f, DoubleValue = 2.5, DecimalValue = 1.25m });
        await ctx.InsertAsync(new AllTypesEntity { StringValue = $"{prefix}_2", IntValue = 2, LongValue = 20, FloatValue = 2.5f, DoubleValue = 3.5, DecimalValue = 2.25m });
        await ctx.InsertAsync(new AllTypesEntity { StringValue = $"{prefix}_3", IntValue = 3, LongValue = 30, FloatValue = 3.5f, DoubleValue = 4.5, DecimalValue = 3.25m });
    }

    private static async Task SeedNullableAsync(SxmTransaction ctx, string prefix)
    {
        await ctx.InsertAsync(new NullTestEntity { StringValue = prefix, NullableInt = 4, NullableLong = 40, NullableFloat = 1.5f, NullableDouble = 2.5, NullableDecimal = 1.5m });
        await ctx.InsertAsync(new NullTestEntity { StringValue = prefix, NullableInt = 6, NullableLong = 60, NullableFloat = 2.5f, NullableDouble = 3.5, NullableDecimal = 2.5m });
        // A row with all-null numeric values: SQL aggregates ignore NULLs.
        await ctx.InsertAsync(new NullTestEntity { StringValue = prefix });
    }

    // ---------- SumAsync / AverageAsync over IQueryable (non-nullable) ----------

    [Fact]
    public async Task SumAsync_IQueryable_NonNullable_ReturnsExpectedTotals()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await SeedAllTypesAsync(ctx, prefix);
        IQueryable<AllTypesEntity> query = ctx.GetTable<AllTypesEntity>().Where(e => e.StringValue!.StartsWith(prefix));

        (await query.SumAsync(e => e.IntValue)).Should().Be(6);
        (await query.SumAsync(e => e.LongValue)).Should().Be(60L);
        (await query.SumAsync(e => e.FloatValue)).Should().BeApproximately(7.5f, 0.001f);
        (await query.SumAsync(e => e.DoubleValue)).Should().BeApproximately(10.5, 0.0001);
        (await query.SumAsync(e => e.DecimalValue)).Should().Be(6.75m);

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task AverageAsync_IQueryable_NonNullable_ReturnsExpectedAverages()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await SeedAllTypesAsync(ctx, prefix);
        IQueryable<AllTypesEntity> query = ctx.GetTable<AllTypesEntity>().Where(e => e.StringValue!.StartsWith(prefix));

        (await query.AverageAsync(e => e.FloatValue)).Should().BeApproximately(2.5f, 0.001f);
        (await query.AverageAsync(e => e.DoubleValue)).Should().BeApproximately(3.5, 0.0001);
        (await query.AverageAsync(e => e.DecimalValue)).Should().Be(2.25m);

        await ctx.RollbackTransactionAsync();
    }

    // ---------- SumAsync / AverageAsync over IQueryable (nullable) ----------

    [Fact]
    public async Task SumAsync_IQueryable_Nullable_IgnoresNullRows()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await SeedNullableAsync(ctx, prefix);
        IQueryable<NullTestEntity> query = ctx.GetTable<NullTestEntity>().Where(e => e.StringValue == prefix);

        (await query.SumAsync(e => e.NullableInt)).Should().Be(10);
        (await query.SumAsync(e => e.NullableLong)).Should().Be(100L);
        (await query.SumAsync(e => e.NullableFloat)).Should().BeApproximately(4.0f, 0.001f);
        (await query.SumAsync(e => e.NullableDouble)).Should().BeApproximately(6.0, 0.0001);
        (await query.SumAsync(e => e.NullableDecimal)).Should().Be(4.0m);

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task AverageAsync_IQueryable_Nullable_IgnoresNullRows()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await SeedNullableAsync(ctx, prefix);
        IQueryable<NullTestEntity> query = ctx.GetTable<NullTestEntity>().Where(e => e.StringValue == prefix);

        // Two non-null rows; the all-null row must not be counted in the divisor.
        (await query.AverageAsync(e => e.NullableFloat)).Should().BeApproximately(2.0f, 0.001f);
        (await query.AverageAsync(e => e.NullableDouble)).Should().BeApproximately(3.0, 0.0001);
        (await query.AverageAsync(e => e.NullableDecimal)).Should().Be(2.0m);

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task NullableAggregates_OnlyNullValues_ReturnNull()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await ctx.InsertAsync(new NullTestEntity { StringValue = prefix });
        IQueryable<NullTestEntity> query = ctx.GetTable<NullTestEntity>().Where(e => e.StringValue == prefix);

        (await query.AverageAsync(e => e.NullableDouble)).Should().BeNull();
        (await query.AverageAsync(e => e.NullableFloat)).Should().BeNull();
        (await query.AverageAsync(e => e.NullableDecimal)).Should().BeNull();

        await ctx.RollbackTransactionAsync();
    }

    // ---------- SxmTable overloads: must agree with an in-memory computation over the same table ----------

    [Fact]
    public async Task SumAsync_SxmTable_AllOverloads_MatchInMemoryTotals()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        // Whole-table aggregates: empty both tables inside this transaction so rows left by other tests
        // cannot affect the totals. The rollback at the end restores them.
        await ctx.GetTable<AllTypesEntity>().DeleteAsync();
        await ctx.GetTable<NullTestEntity>().DeleteAsync();
        await SeedAllTypesAsync(ctx, prefix);
        await SeedNullableAsync(ctx, prefix);

        List<AllTypesEntity> all = ctx.GetTable<AllTypesEntity>().Select(e => new AllTypesEntity { IntValue = e.IntValue, LongValue = e.LongValue, FloatValue = e.FloatValue, DoubleValue = e.DoubleValue, DecimalValue = e.DecimalValue }).ToList();
        List<NullTestEntity> nulls = ctx.GetTable<NullTestEntity>().ToList();
        SxmTable<AllTypesEntity> table = ctx.GetTable<AllTypesEntity>();
        SxmTable<NullTestEntity> nullTable = ctx.GetTable<NullTestEntity>();

        (await table.SumAsync(e => e.IntValue)).Should().Be(all.Sum(e => e.IntValue));
        (await table.SumAsync(e => e.LongValue)).Should().Be(all.Sum(e => e.LongValue));
        (await table.SumAsync(e => e.FloatValue)).Should().BeApproximately(all.Sum(e => e.FloatValue), 0.01f);
        (await table.SumAsync(e => e.DoubleValue)).Should().BeApproximately(all.Sum(e => e.DoubleValue), 0.0001);
        (await table.SumAsync(e => e.DecimalValue)).Should().Be(all.Sum(e => e.DecimalValue));

        (await nullTable.SumAsync(e => e.NullableInt)).Should().Be(nulls.Sum(e => e.NullableInt));
        (await nullTable.SumAsync(e => e.NullableLong)).Should().Be(nulls.Sum(e => e.NullableLong));
        (await nullTable.SumAsync(e => e.NullableFloat)).Should().BeApproximately(nulls.Sum(e => e.NullableFloat), 0.01f);
        (await nullTable.SumAsync(e => e.NullableDouble)).Should().BeApproximately(nulls.Sum(e => e.NullableDouble), 0.0001);
        (await nullTable.SumAsync(e => e.NullableDecimal)).Should().Be(nulls.Sum(e => e.NullableDecimal));

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task AverageAsync_SxmTable_AllOverloads_MatchInMemoryAverages()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        // Whole-table aggregates: empty both tables inside this transaction so rows left by other tests
        // cannot affect the totals. The rollback at the end restores them.
        await ctx.GetTable<AllTypesEntity>().DeleteAsync();
        await ctx.GetTable<NullTestEntity>().DeleteAsync();
        await SeedAllTypesAsync(ctx, prefix);
        await SeedNullableAsync(ctx, prefix);

        List<AllTypesEntity> all = ctx.GetTable<AllTypesEntity>().Select(e => new AllTypesEntity { IntValue = e.IntValue, LongValue = e.LongValue, FloatValue = e.FloatValue, DoubleValue = e.DoubleValue, DecimalValue = e.DecimalValue }).ToList();
        List<NullTestEntity> nulls = ctx.GetTable<NullTestEntity>().ToList();
        SxmTable<AllTypesEntity> table = ctx.GetTable<AllTypesEntity>();
        SxmTable<NullTestEntity> nullTable = ctx.GetTable<NullTestEntity>();

        (await table.AverageAsync(e => e.FloatValue)).Should().BeApproximately(all.Average(e => e.FloatValue), 0.01f);
        (await table.AverageAsync(e => e.DoubleValue)).Should().BeApproximately(all.Average(e => e.DoubleValue), 0.0001);
        (await table.AverageAsync(e => e.DecimalValue)).Should().BeApproximately(all.Average(e => e.DecimalValue), 0.0001m);

        (await nullTable.AverageAsync(e => e.NullableFloat)).Should().BeApproximately(nulls.Average(e => e.NullableFloat)!.Value, 0.01f);
        (await nullTable.AverageAsync(e => e.NullableDouble)).Should().BeApproximately(nulls.Average(e => e.NullableDouble)!.Value, 0.0001);
        (await nullTable.AverageAsync(e => e.NullableDecimal)).Should().BeApproximately(nulls.Average(e => e.NullableDecimal)!.Value, 0.0001m);

        await ctx.RollbackTransactionAsync();
    }

    // ---------- Argument validation ----------

    [Fact]
    public async Task AggregateHelpers_NullSelector_Throw()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        SxmTable<AllTypesEntity> table = ctx.GetTable<AllTypesEntity>();
        IQueryable<AllTypesEntity> query = table.Where(e => e.IntValue > 0);

        Func<Task> tableSum = () => table.SumAsync((System.Linq.Expressions.Expression<Func<AllTypesEntity, int>>)null!);
        Func<Task> querySum = () => query.SumAsync((System.Linq.Expressions.Expression<Func<AllTypesEntity, int>>)null!);
        Func<Task> tableAvg = () => table.AverageAsync((System.Linq.Expressions.Expression<Func<AllTypesEntity, double>>)null!);
        Func<Task> queryAvg = () => query.AverageAsync((System.Linq.Expressions.Expression<Func<AllTypesEntity, double>>)null!);

        await tableSum.Should().ThrowAsync<ArgumentNullException>();
        await querySum.Should().ThrowAsync<ArgumentNullException>();
        await tableAvg.Should().ThrowAsync<ArgumentNullException>();
        await queryAvg.Should().ThrowAsync<ArgumentNullException>();
        ctx.IsFaulted.Should().BeFalse("argument validation happens before any SQL runs");
    }

    // ---------- Predicate overloads ----------

    [Fact]
    public async Task PredicateOverloads_IQueryable_ReturnExpectedRows()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_A", Age = 10 });
        await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_B", Age = 20 });
        IQueryable<SimpleEntity> query = ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix));

        (await query.FirstAsync(e => e.Age == 20)).Name.Should().Be($"{prefix}_B");
        (await query.FirstOrDefaultAsync(e => e.Age == 99)).Should().BeNull();
        (await query.SingleAsync(e => e.Age == 10)).Name.Should().Be($"{prefix}_A");
        (await query.SingleOrDefaultAsync(e => e.Age == 99)).Should().BeNull();
        (await query.AnyAsync(e => e.Age == 20)).Should().BeTrue();
        (await query.AnyAsync(e => e.Age == 99)).Should().BeFalse();
        (await query.CountAsync(e => e.Age >= 10)).Should().Be(2);

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task PredicateOverloads_SxmTable_ReturnExpectedRows()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();
        string nameA = $"{prefix}_A";
        string nameB = $"{prefix}_B";

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await ctx.InsertAsync(new SimpleEntity { Name = nameA, Age = 10 });
        await ctx.InsertAsync(new SimpleEntity { Name = nameB, Age = 20 });
        SxmTable<SimpleEntity> table = ctx.GetTable<SimpleEntity>();

        (await table.FirstAsync(e => e.Name == nameB)).Age.Should().Be(20);
        (await table.FirstOrDefaultAsync(e => e.Name == "missing_" + prefix)).Should().BeNull();
        (await table.SingleAsync(e => e.Name == nameA)).Age.Should().Be(10);
        (await table.SingleOrDefaultAsync(e => e.Name == "missing_" + prefix)).Should().BeNull();
        (await table.AnyAsync(e => e.Name == nameA)).Should().BeTrue();
        (await table.CountAsync(e => e.Name!.StartsWith(prefix))).Should().Be(2);

        await ctx.RollbackTransactionAsync();
    }

    [Fact]
    public async Task SingleAsync_Predicate_MultipleMatches_Throws()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_A", Age = 5 });
        await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_B", Age = 5 });
        IQueryable<SimpleEntity> query = ctx.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix));

        Func<Task> act = () => query.SingleAsync(e => e.Age == 5);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FirstAsync_Predicate_NoMatch_Throws()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        SxmTable<SimpleEntity> table = ctx.GetTable<SimpleEntity>();

        Func<Task> act = () => table.FirstAsync(e => e.Name == prefix);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------- Set(...).UpdateAsync() overloads ----------

    [Fact]
    public async Task Set_IQueryable_Value_UpdatesOnlyMatchingRowsAndCommits()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_A", Age = 1 });
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_B", Age = 2 });

            int updated = await ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name == $"{prefix}_A")
                .Set(e => e.Age, 100)
                .UpdateAsync();

            updated.Should().Be(1);
        }

        await using SxmTransaction verify = new SxmTransaction(TestDatabaseName);
        List<SimpleEntity> rows = verify.GetTable<SimpleEntity>().Where(e => e.Name!.StartsWith(prefix)).OrderBy(e => e.Name).ToList();
        rows.Select(e => e.Age).Should().Equal(100, 2);
    }

    [Fact]
    public async Task Set_IQueryable_Expression_UpdatesUsingColumnValue()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_A", Age = 7 });

            int updated = await ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, e => e.Age * 2)
                .UpdateAsync();

            updated.Should().Be(1);
        }

        await using SxmTransaction verify = new SxmTransaction(TestDatabaseName);
        verify.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix)).Age.Should().Be(14);
    }

    [Fact]
    public async Task Set_SxmTable_ValueAndExpression_UpdateEveryRow_AndRollBack()
    {
        await InitializeSqliteXMAsync();
        string prefix = NewPrefix();

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(new SimpleEntity { Name = $"{prefix}_A", Age = 3 });
            await ctx.CommitTransactionAsync();
        }

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            int total = ctx.GetTable<SimpleEntity>().Count();

            int updatedByValue = await ctx.GetTable<SimpleEntity>().Set(e => e.IsActive, true).UpdateAsync();
            updatedByValue.Should().Be(total, "a table-level Set updates every row");

            int updatedByExpression = await ctx.GetTable<SimpleEntity>().Set(e => e.Age, e => e.Age + 1).UpdateAsync();
            updatedByExpression.Should().Be(total);

            ctx.GetTable<SimpleEntity>().Single(e => e.Name == $"{prefix}_A").Age.Should().Be(4);

            // Discard the table-wide changes so other tests are unaffected.
            await ctx.RollbackTransactionAsync();
        }

        await using SxmTransaction verify = new SxmTransaction(TestDatabaseName);
        verify.GetTable<SimpleEntity>().Single(e => e.Name == $"{prefix}_A").Age.Should().Be(3, "the update was rolled back");
    }

    [Fact]
    public async Task Set_NullArguments_Throw()
    {
        await InitializeSqliteXMAsync();

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        SxmTable<SimpleEntity> table = ctx.GetTable<SimpleEntity>();
        IQueryable<SimpleEntity> query = table.Where(e => e.Age > 0);

        Action tableSetter = () => table.Set<SimpleEntity, int>(null!, 1);
        Action tableExpression = () => table.Set<SimpleEntity, int>(e => e.Age, (System.Linq.Expressions.Expression<Func<SimpleEntity, int>>)null!);
        Action querySetter = () => query.Set<SimpleEntity, int>(null!, 1);
        Action queryExpression = () => query.Set<SimpleEntity, int>(e => e.Age, (System.Linq.Expressions.Expression<Func<SimpleEntity, int>>)null!);

        tableSetter.Should().Throw<ArgumentNullException>();
        tableExpression.Should().Throw<ArgumentNullException>();
        querySetter.Should().Throw<ArgumentNullException>();
        queryExpression.Should().Throw<ArgumentNullException>();
    }
}
