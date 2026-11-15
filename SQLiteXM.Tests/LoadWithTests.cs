using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for <see cref="SxmTable{T}.LoadWith{TProperty}"/> and its params overload.
/// </summary>
/// <remarks>
/// LoadWith validates its expression against the runtime association registry populated during
/// entity registration. These tests cover the success paths (a genuine navigation property that
/// eagerly loads a related entity) and every reachable error path (ordinary columns, non-member
/// expressions, null arguments, and entities with no associations at all).
/// </remarks>
[Collection("Sequential")]
public class LoadWithTests : TestBase
{
    public LoadWithTests()
    {
        CleanupTableDataAsync().GetAwaiter().GetResult();
    }

    #region Test Entities

    /// <summary>
    /// Target of the association. Plays the "one" role in many-to-one.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class NavPublisher : SxmEntity
    {
        public string? PublisherName { get; set; }
        public string? Country { get; set; }
    }

    /// <summary>
    /// Second association target, so tests can verify that two DIFFERENT navigation properties
    /// can be eagerly loaded in a single query.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class NavAuthor : SxmEntity
    {
        public string? AuthorName { get; set; }
    }

    /// <summary>
    /// Source of the association. Declares a scalar foreign key plus a [NotColumn] navigation
    /// property, which is the shape SQLiteXM requires to wire an association.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class NavBook : SxmEntity
    {
        public string? Title { get; set; }

        public decimal Price { get; set; }

        // Nullable so a book with no publisher can be represented. SQLite's foreign-key
        // enforcement permits a NULL foreign key but rejects one pointing at a missing row.
        [ForeignKey("NavPublisher")]
        public long? NavPublisherId { get; set; }

        [NotColumn]
        public NavPublisher? NavPublisher { get; set; }

        [ForeignKey("NavAuthor")]
        public long? NavAuthorId { get; set; }

        [NotColumn]
        public NavAuthor? NavAuthor { get; set; }
    }

    /// <summary>
    /// Entity with no foreign key and no navigation property. Used to prove LoadWith reports a
    /// clear error rather than silently doing nothing.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class NavStandalone : SxmEntity
    {
        public string? Label { get; set; }
    }

    #endregion

    #region Setup Helpers

    /// <summary>
    /// Registers the navigation test entities. Registration is idempotent, so calling this from
    /// every test is safe and keeps the entities out of the shared TestBase registration list.
    /// </summary>
    private static async Task RegisterNavEntitiesAsync()
    {
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(NavPublisher),
            typeof(NavAuthor),
            typeof(NavBook),
            typeof(NavStandalone));
    }

    /// <summary>
    /// Creates two publishers and four books, with one book deliberately having no publisher so
    /// LEFT-join semantics can be verified.
    /// </summary>
    private static async Task<(NavPublisher First, NavPublisher Second)> CreateTestDataAsync()
    {
        NavPublisher first = new NavPublisher { PublisherName = "Manning", Country = "US" };
        await first.SaveAsync();

        NavPublisher second = new NavPublisher { PublisherName = "O'Reilly", Country = "US" };
        await second.SaveAsync();

        NavAuthor author = new NavAuthor { AuthorName = "Mark Seemann" };
        await author.SaveAsync();

        NavBook bookOne = new NavBook { Title = "Dependency Injection", Price = 44.99m, NavPublisherId = first.id, NavAuthorId = author.id };
        await bookOne.SaveAsync();

        NavBook bookTwo = new NavBook { Title = "Functional Programming", Price = 39.99m, NavPublisherId = first.id, NavAuthorId = author.id };
        await bookTwo.SaveAsync();

        NavBook bookThree = new NavBook { Title = "Regular Expressions", Price = 29.99m, NavPublisherId = second.id, NavAuthorId = null };
        await bookThree.SaveAsync();

        // Unmatched: no publisher at all. Exercises the LEFT-join path.
        NavBook unmatched = new NavBook { Title = "Unmatched Title", Price = 9.99m, NavPublisherId = null, NavAuthorId = null };
        await unmatched.SaveAsync();

        return (first, second);
    }

    #endregion

    #region Success Paths

    [Fact]
    public async Task LoadWith_ValidNavigationProperty_ShouldPopulateRelatedEntity()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => b.NavPublisher)
                .ToListAsync();

            // Assert
            books.Should().HaveCount(4);

            NavBook loaded = books.Single(b => b.Title == "Dependency Injection");
            loaded.NavPublisher.Should().NotBeNull();
            loaded.NavPublisher!.PublisherName.Should().Be("Manning");
        }
    }

    [Fact]
    public async Task LoadWith_WithoutLoadWith_ShouldLeaveNavigationPropertyNull()
    {
        // Establishes the baseline: the navigation property is only populated because LoadWith
        // was called, not as a side effect of querying.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table.ToListAsync();

            // Assert
            books.Should().HaveCount(4);
            books.Should().OnlyContain(b => b.NavPublisher == null);
        }
    }

    [Fact]
    public async Task LoadWith_UnmatchedForeignKey_ShouldReturnRowWithNullNavigation()
    {
        // Associations are registered with canBeNull: true, producing LEFT-join semantics. A row
        // whose foreign key matches nothing must still be returned.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => b.NavPublisher)
                .ToListAsync();

            // Assert
            NavBook unmatched = books.Single(b => b.Title == "Unmatched Title");
            unmatched.NavPublisher.Should().BeNull();
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_SingleNavigationProperty_ShouldPopulateRelatedEntity()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => (object?)b.NavPublisher)
                .ToListAsync();

            // Assert
            NavBook loaded = books.Single(b => b.Title == "Regular Expressions");
            loaded.NavPublisher.Should().NotBeNull();
            loaded.NavPublisher!.PublisherName.Should().Be("O'Reilly");
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_EmptyArray_ShouldBeNoOpAndReturnAllRows()
    {
        // Covers the boundary where the validation loop body never executes.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith()
                .ToListAsync();

            // Assert
            books.Should().HaveCount(4);
        }
    }

    [Fact]
    public async Task LoadWith_ShouldBeComposableWithWhere()
    {
        // LoadWith must be applied before Where, because Where returns IQueryable<T> rather than
        // SxmTable<T>. This test documents that ordering requirement.
        //
        // The predicate deliberately filters on the foreign-key column rather than the decimal
        // Price column: decimal comparison appears to be evaluated lexicographically, which is a
        // separate concern and would otherwise obscure what this test is verifying.

        // Arrange
        (NavPublisher first, NavPublisher _) = await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => b.NavPublisher)
                .Where(b => b.NavPublisherId == first.id)
                .ToListAsync();

            // Assert
            books.Should().HaveCount(2);
            books.Should().OnlyContain(b => b.NavPublisher != null);
            books.Should().OnlyContain(b => b.NavPublisher!.PublisherName == "Manning");
        }
    }

    [Fact]
    public async Task LoadWith_ShouldReturnNewInstance_LeavingOriginalTableUnchanged()
    {
        // LoadWith is non-mutating: it wraps a new query rather than altering the receiver.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> original = ctx.GetTable<NavBook>();
            SxmTable<NavBook> withLoad = original.LoadWith(b => b.NavPublisher);

            // Assert
            withLoad.Should().NotBeSameAs(original);

            List<NavBook> fromOriginal = await original.ToListAsync();
            fromOriginal.Should().OnlyContain(b => b.NavPublisher == null);
        }
    }

    [Fact]
    public async Task LoadWith_FilteringOnNavigationPropertyNotNull_ShouldExcludeUnmatchedRows()
    {
        // LoadWith uses LEFT-join semantics, so rows whose foreign key matches nothing are still
        // returned with a null navigation property. This test documents the supported way to get
        // INNER-join semantics instead: filter the navigation property itself for null.
        //
        // The test data contains four books, one of which ("Unmatched Title") has no publisher.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            List<NavBook> all = await table
                .LoadWith(b => b.NavPublisher)
                .ToListAsync();

            List<NavBook> matchedOnly = await table
                .LoadWith(b => b.NavPublisher)
                .Where(b => b.NavPublisher != null)
                .ToListAsync();

            // Assert
            all.Should().HaveCount(4, "LEFT-join semantics keep the unmatched row");
            all.Should().Contain(b => b.Title == "Unmatched Title");

            matchedOnly.Should().HaveCount(3, "the unmatched row is filtered out");
            matchedOnly.Should().NotContain(b => b.Title == "Unmatched Title");
            matchedOnly.Should().OnlyContain(b => b.NavPublisher != null);
        }
    }

    #endregion

    #region Error Paths

    [Fact]
    public async Task LoadWith_ScalarColumn_ShouldThrowArgumentException()
    {
        // The compiler accepts this because NavPublisherId is a real member of NavBook, but it is
        // an ordinary mapped column, not an association.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => b.NavPublisherId);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*NavPublisherId*is not a navigation property*");
        }
    }

    [Fact]
    public async Task LoadWith_ScalarColumn_ShouldListAvailableNavigationProperties()
    {
        // The error message must help the caller recover by naming what IS valid.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => b.Title);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*Available navigation properties:*NavPublisher*");
        }
    }

    [Fact]
    public async Task LoadWith_EntityWithNoAssociations_ShouldReportNoNavigationProperties()
    {
        // Covers the branch where GetRegisteredAssociationNames returns an empty list.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavStandalone> table = ctx.GetTable<NavStandalone>();

            Action act = () => table.LoadWith(s => s.Label);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*has no navigation properties*");
        }
    }

    [Fact]
    public async Task LoadWith_NonMemberExpression_ShouldThrowArgumentException()
    {
        // A method call is not a member access, so it cannot be a navigation property selector.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => b.Title!.ToUpperInvariant());

            act.Should().Throw<ArgumentException>()
               .WithMessage("*not a navigation property selector*");
        }
    }

    [Fact]
    public async Task LoadWith_ConstantExpression_ShouldThrowArgumentException()
    {
        // An expression that ignores the parameter entirely has no member to validate.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => "constant");

            act.Should().Throw<ArgumentException>()
               .WithMessage("*not a navigation property selector*");
        }
    }

    [Fact]
    public async Task LoadWith_CapturedObjectMember_ShouldThrowArgumentException()
    {
        // The member chain must be rooted at the lambda parameter. Here it is rooted at a
        // captured local, which is a closure field access, not a member of the entity.

        // Arrange
        await RegisterNavEntitiesAsync();
        NavBook captured = new NavBook { Title = "Captured" };

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => captured.NavPublisher);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*must select a navigation property of 'NavBook'*");
        }
    }

    [Fact]
    public async Task LoadWith_NullExpression_ShouldThrowArgumentNullException()
    {
        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith<NavPublisher>(null!);

            act.Should().Throw<ArgumentNullException>();
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_WithNullElement_ShouldThrowArgumentNullException()
    {
        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(new System.Linq.Expressions.Expression<Func<NavBook, object?>>[] { null! });

            act.Should().Throw<ArgumentNullException>();
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_InvalidSecondExpression_ShouldThrowBeforeExecuting()
    {
        // Validation runs over every expression up front, so an invalid later element is caught
        // even though the first element is valid.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(
                b => (object?)b.NavPublisher,
                b => (object?)b.Price);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*Price*is not a navigation property*");
        }
    }

    [Fact]
    public async Task LoadWith_InvalidExpression_ShouldThrowImmediately_NotOnEnumeration()
    {
        // The error must surface at the call site rather than being deferred until the query is
        // materialized, which is the entire point of validating eagerly.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            bool threwAtCallSite = false;

            try
            {
                table.LoadWith(b => b.Price);
            }
            catch (ArgumentException)
            {
                threwAtCallSite = true;
            }

            threwAtCallSite.Should().BeTrue(
                "LoadWith validates its expression eagerly rather than deferring to enumeration");
        }
    }

    #endregion

    #region Chained Calls And The Non-ITable Fallback

    [Fact]
    public async Task LoadWith_ChainedCalls_ShouldLoadBothNavigationProperties()
    {
        // The first LoadWith returns a wrapper whose inner query is an IQueryable but NOT an
        // ITable. The second LoadWith therefore hits the fallback branch. If that branch silently
        // returns 'this' without applying the load, NavAuthor comes back null and the failure looks
        // like missing data rather than a dropped operation.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => b.NavPublisher)
                .LoadWith(b => b.NavAuthor)
                .ToListAsync();

            // Assert
            NavBook loaded = books.Single(b => b.Title == "Dependency Injection");

            loaded.NavPublisher.Should().NotBeNull(
                "the first LoadWith in the chain must still be applied");
            loaded.NavPublisher!.PublisherName.Should().Be("Manning");

            loaded.NavAuthor.Should().NotBeNull(
                "the second LoadWith must not be silently discarded when the inner query is no longer an ITable");
            loaded.NavAuthor!.AuthorName.Should().Be("Mark Seemann");
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_TwoDistinctNavigationProperties_ShouldLoadBoth()
    {
        // The params overload applies every expression inside a single ITable branch, so this is
        // the path that is expected to work even if chaining does not.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(
                    b => (object?)b.NavPublisher,
                    b => (object?)b.NavAuthor)
                .ToListAsync();

            // Assert
            NavBook loaded = books.Single(b => b.Title == "Functional Programming");
            loaded.NavPublisher.Should().NotBeNull();
            loaded.NavPublisher!.PublisherName.Should().Be("Manning");
            loaded.NavAuthor.Should().NotBeNull();
            loaded.NavAuthor!.AuthorName.Should().Be("Mark Seemann");
        }
    }

    [Fact]
    public async Task LoadWith_ChainedCalls_RepeatedSameProperty_ShouldRemainValid()
    {
        // Loading the same navigation property twice must not corrupt the query or duplicate rows.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();
            List<NavBook> books = await table
                .LoadWith(b => b.NavPublisher)
                .LoadWith(b => b.NavPublisher)
                .ToListAsync();

            // Assert
            books.Should().HaveCount(4, "repeating a LoadWith must not multiply result rows");
            books.Single(b => b.Title == "Dependency Injection").NavPublisher.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LoadWith_ChainedCall_ShouldStillValidateItsExpression()
    {
        // Validation runs before the ITable check, so it must fire on a chained call too rather
        // than being skipped along with the load itself.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table
                .LoadWith(b => b.NavPublisher)
                .LoadWith(b => b.Title);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*Title*is not a navigation property*");
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_NullArray_ShouldThrowArgumentNullException()
    {
        // The params overload guards validation with a null check but then dereferences the array
        // with a null-forgiving operator. An explicit null must produce a meaningful argument
        // exception, not a NullReferenceException leaking from the loop.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith((Expression<Func<NavBook, object?>>[])null!);

            act.Should().Throw<ArgumentNullException>(
                "a null array is a caller error and must be reported as such");
        }
    }

    #endregion

    #region Nested Navigation Paths

    [Fact]
    public async Task LoadWith_NestedPath_ShouldResolveFirstHopAndSucceed()
    {
        // Exercises the loop that walks a multi-hop member expression back to its first hop.
        // NavPublisher is the first hop and is a registered association, so validation must pass.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => b.NavPublisher!.Country);

            act.Should().NotThrow(
                "validation only inspects the first hop, which is a registered association");
        }
    }

    [Fact]
    public async Task LoadWith_NestedPath_InvalidFirstHop_ShouldThrowNamingTheFirstHop()
    {
        // When the first hop is NOT an association the error must name the first hop, not the
        // trailing member, so the caller knows which part of the path is wrong.

        // Arrange
        await RegisterNavEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Action act = () => table.LoadWith(b => b.Title!.Length);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*'Title' is not a navigation property*");
        }
    }

    #endregion

    #region Materialization Regression Coverage

    // The LoadWith work relaxed roughly 40 call sites from requiring ITable<T> to accepting
    // IQueryable<T>. These tests pin that change down: each operator must still work on a plain
    // table AND on a LoadWith-composed query, which is the case that previously threw
    // "Operation requires LinqToDB ITable<T>."

    [Fact]
    public async Task LoadWith_CountAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            int plain = await table.CountAsync();
            int composed = await table.LoadWith(b => b.NavPublisher).CountAsync();

            plain.Should().Be(4);
            composed.Should().Be(4, "LoadWith must not change the number of rows returned");
        }
    }

    [Fact]
    public async Task LoadWith_FirstOrDefaultAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            NavBook? book = await table
                .LoadWith(b => b.NavPublisher)
                .FirstOrDefaultAsync(b => b.Title == "Regular Expressions");

            book.Should().NotBeNull();
            book!.NavPublisher.Should().NotBeNull();
            book.NavPublisher!.PublisherName.Should().Be("O'Reilly");
        }
    }

    [Fact]
    public async Task LoadWith_SingleAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            NavBook book = await table
                .LoadWith(b => b.NavPublisher)
                .SingleAsync(b => b.Title == "Dependency Injection");

            book.NavPublisher.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LoadWith_AnyAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            bool any = await table.LoadWith(b => b.NavPublisher).AnyAsync();
            bool anyMatching = await table
                .LoadWith(b => b.NavPublisher)
                .AnyAsync(b => b.Title == "Unmatched Title");

            any.Should().BeTrue();
            anyMatching.Should().BeTrue();
        }
    }

    [Fact]
    public async Task LoadWith_ToArrayAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            NavBook[] books = await table.LoadWith(b => b.NavPublisher).ToArrayAsync();

            books.Should().HaveCount(4);
            books.Single(b => b.Title == "Dependency Injection").NavPublisher.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LoadWith_LongCountAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            long count = await table.LoadWith(b => b.NavPublisher).LongCountAsync();

            count.Should().Be(4);
        }
    }

    [Fact]
    public async Task LoadWith_ToDictionaryAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            Dictionary<long, NavBook> byId = await table
                .LoadWith(b => b.NavPublisher)
                .ToDictionaryAsync(b => b.id);

            byId.Should().HaveCount(4);
            byId.Values.Single(b => b.Title == "Dependency Injection").NavPublisher.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LoadWith_MaxAndMinAsync_ShouldWorkOnComposedQuery()
    {
        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            string? maxTitle = await table.LoadWith(b => b.NavPublisher).MaxAsync(b => b.Title);
            string? minTitle = await table.LoadWith(b => b.NavPublisher).MinAsync(b => b.Title);

            maxTitle.Should().Be("Unmatched Title");
            minTitle.Should().Be("Dependency Injection");
        }
    }

    #endregion

    #region Isolation

    [Fact]
    public async Task LoadWith_PlainTableOperations_ShouldStillWorkWithoutLoadWith()
    {
        // Guards against the AsInnerQueryable change having broken ordinary, non-LoadWith usage.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> table = ctx.GetTable<NavBook>();

            (await table.CountAsync()).Should().Be(4);
            (await table.AnyAsync()).Should().BeTrue();
            (await table.ToListAsync()).Should().HaveCount(4);
            (await table.ToArrayAsync()).Should().HaveCount(4);
            (await table.LongCountAsync()).Should().Be(4);
            (await table.FirstOrDefaultAsync(b => b.Title == "Unmatched Title")).Should().NotBeNull();
        }
    }

    [Fact]
    public async Task LoadWith_ShouldNotLeakAcrossSeparateQueries()
    {
        // A LoadWith applied to one query must not affect a second, independently obtained table.

        // Arrange
        await RegisterNavEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<NavBook> eager = ctx.GetTable<NavBook>().LoadWith(b => b.NavPublisher);
            List<NavBook> eagerBooks = await eager.ToListAsync();
            eagerBooks.Single(b => b.Title == "Dependency Injection").NavPublisher.Should().NotBeNull();

            SxmTable<NavBook> fresh = ctx.GetTable<NavBook>();
            List<NavBook> freshBooks = await fresh.ToListAsync();
            freshBooks.Single(b => b.Title == "Dependency Injection").NavPublisher.Should().BeNull(
                "a separate query must not inherit eager loading from an earlier one");
        }
    }

    #endregion
}
