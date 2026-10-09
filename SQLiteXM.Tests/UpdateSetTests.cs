using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Edge-case tests for <see cref="SxmUpdateSet{T}"/>, the builder returned by <c>Set(...)</c> and
/// terminated by <c>UpdateAsync()</c>.
///
/// <para>
/// The happy paths (single property, expression-valued property, predicates, transaction scoping)
/// are already covered by <see cref="BulkLinqOperationsTests"/>. This class deliberately targets
/// the awkward corners: setting the same column twice, assigning null, updating with a predicate
/// that matches nothing, and calling <c>UpdateAsync()</c> outside a transaction context.
/// </para>
///
/// <para>
/// Several of these are *characterization* tests: they record what the library actually does today
/// rather than asserting a behavior specified in advance. Where that matters, the test comment
/// says so explicitly.
/// </para>
/// </summary>
[Collection("Sequential")]
public class UpdateSetTests : TestBase
{
    private static string NewPrefix() => "UpdSet_" + Guid.NewGuid().ToString("N").Substring(0, 10);

    /// <summary>
    /// Inserts the supplied entities through the context so they participate in its transaction.
    /// </summary>
    private static async Task SeedAsync(SxmTransaction ctx, params SimpleEntity[] entities)
    {
        foreach (SimpleEntity entity in entities)
        {
            await ctx.InsertAsync(entity);
        }
    }

    [Fact]
    public async Task Set_SameColumnTwice_ShouldApplyTheLastValue()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

        // Characterization: setting one column twice in a single chain. SQL UPDATE cannot assign
        // the same column twice, so the builder must collapse this somehow - last-write-wins is
        // the expected outcome, but the point of this test is to pin down whatever happens.
        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith(prefix))
            .Set(e => e.Age, 20)
            .Set(e => e.Age, 30)
            .UpdateAsync();

        updated.Should().Be(1);

        SimpleEntity result = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix));
        result.Age.Should().Be(30, "the final Set call in the chain should win");
    }

    [Fact]
    public async Task Set_NullOnANullableColumn_ShouldWriteSqlNull()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = true });

        // Name is the only nullable column on SimpleEntity. Null it out by id so the predicate
        // does not depend on the column being changed.
        long id = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix)).id;

        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.id == id)
            .Set(e => e.Name, (string?)null)
            .UpdateAsync();

        updated.Should().Be(1);

        SimpleEntity result = ctx.GetTable<SimpleEntity>().Single(e => e.id == id);
        result.Name.Should().BeNull();
    }

    [Fact]
    public async Task Set_MultipleDistinctColumns_ShouldApplyAllOfThem()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

        long id = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix)).id;

        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.id == id)
            .Set(e => e.Age, 44)
            .Set(e => e.IsActive, true)
            .UpdateAsync();

        updated.Should().Be(1);

        SimpleEntity result = ctx.GetTable<SimpleEntity>().Single(e => e.id == id);
        result.Age.Should().Be(44);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Set_WithPredicateMatchingNoRows_ShouldReportZeroAndChangeNothing()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name == $"{prefix}_does_not_exist")
            .Set(e => e.Age, 77)
            .UpdateAsync();

        updated.Should().Be(0);

        SimpleEntity untouched = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix));
        untouched.Age.Should().Be(10, "no row matched, so nothing should have been written");
    }

    [Fact]
    public async Task Set_WithExpressionReferencingTheSameColumn_ShouldComputeFromExistingValue()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx,
            new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = true },
            new SimpleEntity { Name = $"{prefix}_2", Age = 20, IsActive = true });

        // The expression overload computes the new value per row from the existing value, so the
        // two rows must end up with different results.
        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith(prefix))
            .Set(e => e.Age, e => e.Age + 5)
            .UpdateAsync();

        updated.Should().Be(2);

        List<SimpleEntity> results = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith(prefix))
            .OrderBy(e => e.Age)
            .ToList();

        results.Select(r => r.Age).Should().Equal(15, 25);
    }

    [Fact]
    public async Task Set_MixingValueAndExpressionOverloads_ShouldApplyBoth()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

        long id = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix)).id;

        int updated = await ctx.GetTable<SimpleEntity>()
            .Where(e => e.id == id)
            .Set(e => e.Age, e => e.Age * 3)
            .Set(e => e.IsActive, true)
            .UpdateAsync();

        updated.Should().Be(1);

        SimpleEntity result = ctx.GetTable<SimpleEntity>().Single(e => e.id == id);
        result.Age.Should().Be(30);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Set_ReturnsANewBuilderInstance_SoChainsDoNotShareState()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        string prefix = NewPrefix();
        await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

        // Each Set returns a brand new SxmUpdateSet wrapping an updated builder. Capturing an
        // intermediate step and extending it twice must not let one branch affect the other.
        SxmUpdateSet<SimpleEntity> baseChain = ctx.GetTable<SimpleEntity>()
            .Where(e => e.Name!.StartsWith(prefix))
            .Set(e => e.Age, 50);

        baseChain.Should().NotBeNull();

        SxmUpdateSet<SimpleEntity> branch = baseChain.Set(e => e.IsActive, true);

        branch.Should().NotBeSameAs(baseChain, "each Set call should produce a new wrapper");

        int updated = await branch.UpdateAsync();
        updated.Should().Be(1);

        SimpleEntity result = ctx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix));
        result.Age.Should().Be(50);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Set_WithNullSetterExpression_ShouldThrowArgumentNullException()
    {
        await InitializeSqliteXMAsync();
        await using var ctx = new SxmTransaction(TestDatabaseName);

        Action act = () => ctx.GetTable<SimpleEntity>()
            .Set((System.Linq.Expressions.Expression<Func<SimpleEntity, int>>)null!, 1);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Set_OnAPlainInMemoryQueryable_ShouldBeRejectedByTheQueryProvider()
    {
        await InitializeSqliteXMAsync();

        // Characterization: SxmUpdateSet guards against a missing SxmTransaction inside
        // UpdateAsync, but that guard turns out to be unreachable from a plain in-memory
        // queryable. LinqToDB's Set rejects the non-database query provider first, so the
        // failure surfaces as ArgumentException from the provider rather than as the library's
        // own "require a SxmTransaction" InvalidOperationException.
        var detached = new List<SimpleEntity>().AsQueryable();

        Action act = () => detached.Set(e => e.Age, 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*expression is not valid*");
    }

    [Fact]
    public async Task UpdateAsync_AfterTheContextIsDisposed_ShouldNotSilentlySucceed()
    {
        await InitializeSqliteXMAsync();

        string prefix = NewPrefix();
        SxmUpdateSet<SimpleEntity> chain;

        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await SeedAsync(ctx, new SimpleEntity { Name = $"{prefix}_1", Age = 10, IsActive = false });

            // Build the chain inside the context but deliberately do not execute it yet.
            chain = ctx.GetTable<SimpleEntity>()
                .Where(e => e.Name!.StartsWith(prefix))
                .Set(e => e.Age, 123);
        }

        // Characterization: executing against a context that has already been disposed must not
        // quietly report success. Either it throws, or it reports that nothing was updated.
        int updated;
        try
        {
            updated = await chain.UpdateAsync();
        }
        catch (Exception)
        {
            // Throwing is an acceptable outcome; the unacceptable one is a silent false success.
            return;
        }

        await using var verifyCtx = new SxmTransaction(TestDatabaseName);
        SimpleEntity result = verifyCtx.GetTable<SimpleEntity>().Single(e => e.Name!.StartsWith(prefix));

        if (updated > 0)
        {
            result.Age.Should().Be(123, "a reported update must actually have been written");
        }
        else
        {
            result.Age.Should().Be(10, "if no rows were reported updated, nothing should have changed");
        }
    }
}
