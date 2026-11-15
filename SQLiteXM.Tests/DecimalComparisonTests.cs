using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Characterization tests for SQL-side comparison and ordering of <see cref="decimal"/> columns.
/// </summary>
/// <remarks>
/// SUPPORTED BEHAVIOUR: exact storage, exact round-trip, and equality comparison.
/// NOT SUPPORTED: SQL-side range comparison (&gt; &lt; &gt;= &lt;=), ORDER BY, MIN and MAX.
///
/// SQLite has no native decimal type. SQLiteXM stores decimal as TEXT to preserve exact precision
/// (see SxmColumnDataConverters.DecimalToString, which uses the "G29" format). That choice keeps
/// values exact, but SQLite compares TEXT character by character, and because "G29" is
/// variable width the string order does not match the numeric order: "9.99" sorts AFTER "30"
/// because '9' &gt; '3'.
///
/// Making range comparison work in SQL would require a fixed-width, offset-encoded format. That
/// would cap precision at a chosen scale and force a migration of existing databases, so the
/// limitation is accepted and documented instead.
///
/// These tests therefore assert the ACTUAL behaviour, not the numerically ideal one. If the
/// storage encoding is ever changed, they are the tests that must be revisited. Two companion
/// tests show the supported workaround: materialize first, then filter or sort in memory.
/// </remarks>
[Collection("Sequential")]
public class DecimalComparisonTests : TestBase
{
    public DecimalComparisonTests()
    {
        CleanupTableDataAsync().GetAwaiter().GetResult();
    }

    #region Test Entity

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DecimalRangeEntity : SxmEntity
    {
        public string? Label { get; set; }

        public decimal Amount { get; set; }
    }

    #endregion

    #region Setup Helpers

    private static async Task RegisterAsync()
    {
        await SxmDatabase.RegisterEntitiesAsync(typeof(DecimalRangeEntity));
    }

    /// <summary>
    /// Creates values chosen so that lexicographic ordering and numeric ordering disagree.
    /// Numeric ascending order is: -100.50, 5.00, 9.99, 29.99, 100.00, 1000.00.
    /// </summary>
    private static async Task CreateAmountsAsync()
    {
        decimal[] amounts = { 9.99m, 29.99m, 100.00m, 1000.00m, 5.00m, -100.50m };

        foreach (decimal amount in amounts)
        {
            DecimalRangeEntity row = new DecimalRangeEntity
            {
                Label = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Amount = amount
            };
            await row.SaveAsync();
        }
    }

    #endregion

    #region Round Trip (expected to pass today)

    [Fact]
    public async Task Decimal_ShouldRoundTripExactly()
    {
        // Storing decimal as TEXT is a deliberate choice that preserves precision exactly.
        // This must keep working regardless of how the sort problem is solved.

        // Arrange
        await RegisterAsync();

        DecimalRangeEntity row = new DecimalRangeEntity
        {
            Label = "precise",
            Amount = 12345.6789012345678901234567m
        };
        await row.SaveAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();
            DecimalRangeEntity loaded = await table.SingleAsync(e => e.Label == "precise");

            // Assert
            loaded.Amount.Should().Be(12345.6789012345678901234567m,
                "storing decimal as TEXT exists specifically to avoid floating point precision loss");
        }
    }

    [Fact]
    public async Task Decimal_Equality_ShouldMatchExactValue()
    {
        // Equality works even with variable-width text because identical values produce
        // identical strings. Only RANGE comparison is affected.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            int count = await table.CountAsync(e => e.Amount == 29.99m);

            count.Should().Be(1);
        }
    }

    #endregion

    #region Range Comparison (documented limitation)

    [Fact]
    public async Task Decimal_GreaterThan_IsLexicographic_NotNumeric()
    {
        // DOCUMENTED LIMITATION. Decimal is stored as a variable-width "G29" string, and SQLite
        // compares TEXT character by character. "9.99" > "30" because '9' > '3', so small values
        // can satisfy a greater-than test against a larger number.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<DecimalRangeEntity> rows = await table
                .Where(e => e.Amount > 30m)
                .ToListAsync();

            // Assert: numerically the answer is 100.00 and 1000.00. Lexicographically it is
            // 9.99 and 5.00, because '9' and '5' both sort after '3'.
            rows.Select(r => r.Amount).Should().BeEquivalentTo(
                new[] { 9.99m, 5.00m },
                "SQL-side range comparison on decimal is lexicographic; filter in memory for numeric semantics");
        }
    }

    [Fact]
    public async Task Decimal_RangeFilter_InMemory_IsNumericallyCorrect()
    {
        // THE SUPPORTED WORKAROUND. Materialize first, then filter with LINQ-to-Objects, where
        // real decimal comparison applies.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<DecimalRangeEntity> all = await table.ToListAsync();
            List<decimal> above30 = all
                .Select(e => e.Amount)
                .Where(a => a > 30m)
                .ToList();

            // Assert
            above30.Should().BeEquivalentTo(
                new[] { 100.00m, 1000.00m },
                "in-memory comparison uses real decimal semantics");
        }
    }

    [Fact]
    public async Task Decimal_LessThan_IsLexicographic_NotNumeric()
    {
        // DOCUMENTED LIMITATION. The mirror image of the greater-than case: values that are
        // numerically below the bound are missed because their text sorts above it.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<DecimalRangeEntity> rows = await table
                .Where(e => e.Amount < 10m)
                .ToListAsync();

            // Assert: numerically -100.50, 5.00 and 9.99 all qualify. Lexicographically only
            // "-100.5" sorts below "10", because '5' and '9' both sort above '1'.
            rows.Select(r => r.Amount).Should().BeEquivalentTo(
                new[] { -100.50m },
                "SQL-side range comparison on decimal is lexicographic and silently omits matches");
        }
    }

    [Fact]
    public async Task Decimal_NegativeValues_ShouldSortBelowPositives()
    {
        // Sign handling is the part the ULong zero-padding trick does not cover:
        // "-100.50" must compare below every positive value.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            int belowZero = await table.CountAsync(e => e.Amount < 0m);

            // Assert
            belowZero.Should().Be(1, "exactly one stored value is negative");
        }
    }

    [Fact]
    public async Task Decimal_OrderBy_SortsAsText_NotNumerically()
    {
        // DOCUMENTED LIMITATION. ORDER BY on a decimal column sorts the stored strings.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<DecimalRangeEntity> rows = await table
                .OrderBy(e => e.Amount)
                .ToListAsync();

            // Assert: text order of "-100.5", "100", "1000", "29.99", "5", "9.99".
            // Numeric order would be -100.50, 5.00, 9.99, 29.99, 100.00, 1000.00.
            rows.Select(r => r.Amount).Should().ContainInOrder(
                -100.50m, 100.00m, 1000.00m, 29.99m, 5.00m, 9.99m);
        }
    }

    [Fact]
    public async Task Decimal_OrderBy_InMemory_IsNumericallyCorrect()
    {
        // THE SUPPORTED WORKAROUND for ordering.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<DecimalRangeEntity> all = await table.ToListAsync();
            List<decimal> ordered = all.Select(e => e.Amount).OrderBy(a => a).ToList();

            // Assert
            ordered.Should().ContainInOrder(
                -100.50m, 5.00m, 9.99m, 29.99m, 100.00m, 1000.00m);
        }
    }

    [Fact]
    public async Task Decimal_MaxAsync_ReturnsLexicographicMaximum()
    {
        // DOCUMENTED LIMITATION, and the starkest demonstration of it: MAX over a set that
        // contains 1000.00 returns 9.99, because "9.99" is the largest string.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            decimal max = await table.MaxAsync(e => e.Amount);

            // Assert
            max.Should().Be(9.99m,
                "SQL MAX on a decimal column compares text, not numbers");
        }
    }

    [Fact]
    public async Task Decimal_MinAsync_ReturnsLexicographicMinimum()
    {
        // Here text and numeric order happen to agree, because '-' sorts before every digit.
        // That coincidence is not a guarantee.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            decimal min = await table.MinAsync(e => e.Amount);

            // Assert
            min.Should().Be(-100.50m);
        }
    }

    #endregion

    #region Arithmetic and Aggregation (supported)

    [Fact]
    public async Task Decimal_SumAsync_IsNumericallyCorrect()
    {
        // SUM is NOT affected by the text-storage limitation. SQLite applies numeric affinity
        // when a TEXT value is used in arithmetic, so addition is performed on numbers. Only
        // COMPARISON and ORDERING fall back to character-by-character text rules.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            decimal sum = await table.SumAsync(e => e.Amount);

            // Assert: 9.99 + 29.99 + 100.00 + 1000.00 + 5.00 + (-100.50)
            sum.Should().Be(1044.48m, "SUM on a decimal column is evaluated numerically");
        }
    }

    [Fact]
    public async Task Decimal_AverageAsync_IsNumericallyCorrect()
    {
        // Companion to the SUM test. AVERAGE is likewise numeric.

        // Arrange
        await RegisterAsync();
        await CreateAmountsAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            decimal average = await table.AverageAsync(e => e.Amount);

            // Assert: 1044.48 / 6
            average.Should().Be(174.08m, "AVERAGE on a decimal column is evaluated numerically");
        }
    }

    [Fact]
    public async Task Decimal_BulkUpdateArithmetic_IsNumericallyCorrect()
    {
        // A bulk update that computes a new value from the existing one is arithmetic, not
        // comparison, so it produces numerically correct results.

        // Arrange
        await RegisterAsync();

        DecimalRangeEntity row = new DecimalRangeEntity { Label = "arith", Amount = 9.99m };
        await row.SaveAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            await table
                .Where(e => e.Label == "arith")
                .Set(e => e.Amount, e => e.Amount * 2m)
                .UpdateAsync();

            decimal updated = (await table.SingleAsync(e => e.Label == "arith")).Amount;

            // Assert
            updated.Should().Be(19.98m, "arithmetic in a bulk update is evaluated numerically");
        }
    }

    [Fact]
    public async Task Decimal_HavingOnAggregate_IsNumericallyCorrect()
    {
        // Comparing an AGGREGATE RESULT is numeric, because SUM has already produced a number.
        // This is the difference between comparing a stored column (text) and comparing a
        // computed value (number).

        // Arrange
        await RegisterAsync();

        (string Label, decimal Amount)[] rows =
        {
            ("groupA", 9.99m), ("groupA", 29.99m),      // total 39.98
            ("groupB", 100.00m), ("groupB", 5.00m)      // total 105.00
        };

        foreach ((string label, decimal amount) in rows)
        {
            DecimalRangeEntity entity = new DecimalRangeEntity { Label = label, Amount = amount };
            await entity.SaveAsync();
        }

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DecimalRangeEntity> table = ctx.GetTable<DecimalRangeEntity>();

            List<string> matched = await (
                from e in table
                group e by e.Label into g
                where g.Sum(x => x.Amount) > 100m
                select g.Key
            ).ToListAsync();

            // Assert: only groupB exceeds 100 numerically. A lexicographic comparison would
            // also have returned groupA, because "39.98" sorts above "100".
            matched.Should().BeEquivalentTo(new[] { "groupB" },
                "comparing an aggregate result uses numeric semantics");
        }
    }

    #endregion
}
