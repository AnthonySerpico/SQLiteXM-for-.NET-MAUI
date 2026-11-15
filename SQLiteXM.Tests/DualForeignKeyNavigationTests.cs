using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests for an entity that declares TWO foreign keys pointing at the SAME target table, each
/// with its own navigation property.
/// </summary>
/// <remarks>
/// <para>
/// SUPPORTED: one foreign key per target table, paired with a single navigation property.
/// This is the shape every other LoadWith test uses and it works correctly. An entity may have
/// several foreign keys as long as they reference DIFFERENT tables (see DualArticle).
/// </para>
/// <para>
/// NOT SUPPORTED: two (or more) foreign keys on the same entity pointing at the same target
/// table. The association registry locates a navigation property by MATCHING ITS CLR TYPE
/// NAME ONLY (SxmHelpers.CreateAssociation), so two properties of that type cannot be told
/// apart. The property that does get registered may be bound to the OTHER foreign key's
/// column, which previously returned the wrong row with no error at all.
/// </para>
/// <para>
/// LoadWith now detects this by counting [ForeignKey] declarations per target table and throws
/// ArgumentException instead of risking a wrong result. Counting declarations is unambiguous:
/// it never assumes a property of the target type was intended as a navigation property.
/// </para>
/// <para>
/// WORKAROUND: write an explicit join instead of using LoadWith. A join states which column
/// joins to which row, so there is no ambiguity to resolve.
/// </para>
/// <para>
/// These tests cover both directions - that ambiguous entities are rejected, and that valid
/// entities are NOT affected - plus the PRAGMA-based discovery path in SxmAssociationMapper.
/// </para>
/// </remarks>
[Collection("Sequential")]
public class DualForeignKeyNavigationTests : TestBase
{
    public DualForeignKeyNavigationTests()
    {
        CleanupTableDataAsync().GetAwaiter().GetResult();
    }

    #region Test Entities

    /// <summary>
    /// Shared target of both foreign keys. Rows are just people; "author" and "co-author" are
    /// roles held by the referencing entity, not properties of the person.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DualPerson : SxmEntity
    {
        public string? PersonName { get; set; }
    }

    /// <summary>
    /// Declares two foreign keys to <see cref="DualPerson"/>, each with its own navigation
    /// property. Both navigation properties are correctly declared: nullable, [NotColumn], and
    /// typed as the target entity. Author is declared FIRST, which matters because the lookup
    /// takes the first type match it finds.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DualPaper : SxmEntity
    {
        public string? Title { get; set; }

        [ForeignKey("DualPerson")]
        public long? AuthorId { get; set; }

        [NotColumn]
        public DualPerson? Author { get; set; }

        [ForeignKey("DualPerson")]
        public long? CoAuthorId { get; set; }

        [NotColumn]
        public DualPerson? CoAuthor { get; set; }
    }

    /// <summary>
    /// Same shape as <see cref="DualPaper"/> but with the navigation properties declared in the
    /// OPPOSITE order to the foreign-key columns: EditorId comes before ReviewerId, while
    /// Reviewer is declared before Editor.
    /// </summary>
    /// <remarks>
    /// This isolates the question of whether the slot-to-column pairing can be silently WRONG.
    /// The column order and the property order are the two inputs that get matched up, and they
    /// are determined independently. If the first column processed is paired with the first
    /// property found, this entity pairs EditorId with Reviewer - a mismatch that returns the
    /// wrong row with no error.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DualReport : SxmEntity
    {
        public string? Title { get; set; }

        [ForeignKey("DualPerson")]
        public long? EditorId { get; set; }

        [ForeignKey("DualPerson")]
        public long? ReviewerId { get; set; }

        // Declared in the reverse order of the columns above.
        [NotColumn]
        public DualPerson? Reviewer { get; set; }

        [NotColumn]
        public DualPerson? Editor { get; set; }
    }

    /// <summary>
    /// A second, unrelated target type used to prove the restriction is scoped to a single target
    /// table rather than to the number of foreign keys on the entity.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DualJournal : SxmEntity
    {
        public string? JournalName { get; set; }
    }

    /// <summary>
    /// The VALID shape: two foreign keys, but each pointing at a DIFFERENT target table, so each
    /// navigation property can be matched to its own foreign key without ambiguity.
    /// </summary>
    /// <remarks>
    /// This is the negative control for the LoadWith restriction. The check must count foreign keys
    /// per target table, not per entity; if it merely counted foreign keys, this entity would be
    /// wrongly rejected even though nothing about it is ambiguous.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class DualArticle : SxmEntity
    {
        public string? Title { get; set; }

        [ForeignKey("DualPerson")]
        public long? WriterId { get; set; }

        [NotColumn]
        public DualPerson? Writer { get; set; }

        [ForeignKey("DualJournal")]
        public long? JournalId { get; set; }

        [NotColumn]
        public DualJournal? Journal { get; set; }
    }

    /// <summary>
    /// THREE foreign keys to the same target table, to confirm the restriction is not specific to
    /// the two-key case and that the error reports the real count.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [Table(IsColumnAttributeRequired = false)]
    public class TriplePanel : SxmEntity
    {
        public string? Topic { get; set; }

        [ForeignKey("DualPerson")]
        public long? ChairId { get; set; }

        [ForeignKey("DualPerson")]
        public long? SpeakerId { get; set; }

        [ForeignKey("DualPerson")]
        public long? ScribeId { get; set; }

        [NotColumn]
        public DualPerson? Chair { get; set; }

        [NotColumn]
        public DualPerson? Speaker { get; set; }

        [NotColumn]
        public DualPerson? Scribe { get; set; }
    }

    #endregion

    #region Setup Helpers

    private static async Task RegisterDualEntitiesAsync()
    {
        await SxmDatabase.RegisterEntitiesAsync(
            typeof(DualPerson),
            typeof(DualJournal),
            typeof(DualPaper),
            typeof(DualReport),
            typeof(DualArticle),
            typeof(TriplePanel));
    }

    /// <summary>
    /// Creates an article whose two foreign keys reference two different TABLES, which is the
    /// supported shape for LoadWith.
    /// </summary>
    private static async Task<(DualPerson Writer, DualJournal Journal)> CreateArticleDataAsync()
    {
        DualPerson writer = new DualPerson { PersonName = "The Writer" };
        await writer.SaveAsync();

        DualJournal journal = new DualJournal { JournalName = "The Journal" };
        await journal.SaveAsync();

        DualArticle article = new DualArticle
        {
            Title = "Distinct Targets Article",
            WriterId = writer.id,
            JournalId = journal.id
        };
        await article.SaveAsync();

        return (writer, journal);
    }

    /// <summary>
    /// Creates a report whose two foreign-key columns reference two DIFFERENT people, so a
    /// mismatched slot-to-column pairing is detectable.
    /// </summary>
    private static async Task<(DualPerson Editor, DualPerson Reviewer)> CreateReportDataAsync()
    {
        DualPerson editor = new DualPerson { PersonName = "The Editor" };
        await editor.SaveAsync();

        DualPerson reviewer = new DualPerson { PersonName = "The Reviewer" };
        await reviewer.SaveAsync();

        DualReport report = new DualReport
        {
            Title = "Reversed Declaration Report",
            EditorId = editor.id,
            ReviewerId = reviewer.id
        };
        await report.SaveAsync();

        return (editor, reviewer);
    }

    /// <summary>
    /// Creates two distinct people and one paper that references a DIFFERENT person in each of
    /// its two foreign-key columns. Distinct values are essential: if both columns held the same
    /// id, a wrong-column load would be indistinguishable from a correct one.
    /// </summary>
    private static async Task<(DualPerson Author, DualPerson CoAuthor)> CreateTestDataAsync()
    {
        DualPerson author = new DualPerson { PersonName = "Primary Writer" };
        await author.SaveAsync();

        DualPerson coAuthor = new DualPerson { PersonName = "Secondary Writer" };
        await coAuthor.SaveAsync();

        DualPaper paper = new DualPaper
        {
            Title = "Shared Target Paper",
            AuthorId = author.id,
            CoAuthorId = coAuthor.id
        };
        await paper.SaveAsync();

        return (author, coAuthor);
    }

    #endregion

    #region Storage (supported)

    [Fact]
    public async Task DualForeignKeys_ShouldBothPersistIndependently()
    {
        // The SCHEMA layer handles two foreign keys to one table correctly: ForeignKeyAttribute
        // allows multiple use and each column gets its own constraint. Only the ASSOCIATION
        // layer used by LoadWith is affected. This test pins down that storage is unaffected.

        // Arrange
        await RegisterDualEntitiesAsync();
        (DualPerson author, DualPerson coAuthor) = await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            DualPaper? paper = await ctx.GetTable<DualPaper>()
                .FirstOrDefaultAsync(p => p.Title == "Shared Target Paper");

            // Assert
            paper.Should().NotBeNull();
            paper!.AuthorId.Should().Be(author.id, "each foreign-key column stores its own value");
            paper.CoAuthorId.Should().Be(coAuthor.id, "the second foreign key is independent of the first");
            paper.AuthorId.Should().NotBe(paper.CoAuthorId, "the two columns reference different rows");
        }
    }

    #endregion

    #region LoadWith (valid entities must NOT be affected)

    [Fact]
    public async Task LoadWith_TwoForeignKeysToDifferentTables_IsAllowedAndLoadsCorrectly()
    {
        // NEGATIVE CONTROL for the restriction.
        //
        // DualArticle also has two foreign keys, but they point at DIFFERENT tables (DualPerson and
        // DualJournal). Nothing is ambiguous here: each navigation property has exactly one foreign
        // key of its own type, so both must still load - and load the RIGHT rows.
        //
        // If the check ever regressed to counting foreign keys per ENTITY instead of per TARGET
        // TABLE, this test would fail while every rejection test kept passing.

        // Arrange
        await RegisterDualEntitiesAsync();
        (DualPerson writer, DualJournal journal) = await CreateArticleDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            DualArticle? article = await ctx.GetTable<DualArticle>()
                .LoadWith(a => a.Writer)
                .LoadWith(a => a.Journal)
                .FirstOrDefaultAsync(a => a.Title == "Distinct Targets Article");

            // Assert
            article.Should().NotBeNull();

            article!.Writer.Should().NotBeNull(
                "one foreign key targets DualPerson, so the association is unambiguous");
            article.Writer!.id.Should().Be(writer.id);
            article.Writer.PersonName.Should().Be("The Writer");

            article.Journal.Should().NotBeNull(
                "one foreign key targets DualJournal, so the association is unambiguous");
            article.Journal!.id.Should().Be(journal.id);
            article.Journal.JournalName.Should().Be("The Journal");
        }
    }

    [Fact]
    public async Task LoadWith_SingleForeignKeyEntity_IsNotRejectedByTheAmbiguityCheck()
    {
        // The ambiguity check runs before the association-registration check, so a mistake in it
        // would surface as the wrong error on ordinary entities. This pins down that a valid
        // single-foreign-key navigation property throws nothing at all.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateArticleDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualArticle> table = ctx.GetTable<DualArticle>();

            Action loadWriter = () => table.LoadWith(a => a.Writer);

            loadWriter.Should().NotThrow(
                "DualArticle declares exactly one foreign key referencing DualPerson");
        }
    }

    #endregion

    #region Schema-discovery path (PRAGMA foreign_key_list)

    /// <summary>
    /// Clears the per-database "already initialized" marker so the PRAGMA-based association
    /// discovery in SxmAssociationMapper can be forced to run again.
    /// </summary>
    private static void ResetAssociationInitFor(string databaseName)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        Type mapperType = typeof(SxmEntity).Assembly.GetType("SQLiteXM.SxmAssociationMapper")!;
        System.Collections.IDictionary? initTasks = mapperType
            .GetField("_associationInitTasks", flags)?
            .GetValue(null) as System.Collections.IDictionary;

        initTasks?.Remove(databaseName);
    }

    /// <summary>
    /// Invokes the internal PRAGMA-driven association discovery and waits for it to finish.
    /// </summary>
    private static async Task RunSchemaDiscoveryAsync(string databaseName)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        Type mapperType = typeof(SxmEntity).Assembly.GetType("SQLiteXM.SxmAssociationMapper")!;
        System.Reflection.MethodInfo initMethod = mapperType.GetMethod("InitializeAssociationsAsync", flags)!;

        await (Task)initMethod.Invoke(null, new object[] { databaseName })!;
    }

    private static List<string> TrackedForeignKeys(Type sourceType, string targetTableName)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        Type helpersType = typeof(SxmEntity).Assembly.GetType("SQLiteXM.SxmHelpers")!;
        System.Reflection.MethodInfo method = helpersType.GetMethod("GetForeignKeyColumnsTargeting", flags)!;

        return (List<string>)method.Invoke(null, new object[] { sourceType, targetTableName })!;
    }

    [Fact]
    public async Task ForeignKeyTracking_RecordsOneEntryPerDistinctColumn()
    {
        // Direct check of the data the LoadWith restriction reads. Asserting the exact column names
        // (not just a count) proves the tracker keys on the real columns rather than accumulating
        // duplicates or unrelated entries.

        // Arrange
        await RegisterDualEntitiesAsync();

        // Assert
        List<string> paperKeys = TrackedForeignKeys(typeof(DualPaper), nameof(DualPerson));
        paperKeys.Should().BeEquivalentTo(
            new[] { "AuthorId", "CoAuthorId" },
            "both foreign keys target DualPerson");

        List<string> articlePersonKeys = TrackedForeignKeys(typeof(DualArticle), nameof(DualPerson));
        articlePersonKeys.Should().BeEquivalentTo(
            new[] { "WriterId" },
            "only one DualArticle foreign key targets DualPerson");

        List<string> articleJournalKeys = TrackedForeignKeys(typeof(DualArticle), nameof(DualJournal));
        articleJournalKeys.Should().BeEquivalentTo(
            new[] { "JournalId" },
            "the other DualArticle foreign key targets a different table");
    }

    [Fact]
    public async Task SchemaDiscovery_RerunDoesNotInflateForeignKeyCounts()
    {
        // THE IDEMPOTENCY GUARANTEE.
        //
        // Foreign keys are discovered twice: from [ForeignKey] attributes during schema
        // registration, and again from PRAGMA foreign_key_list in SxmAssociationMapper. The tracker
        // stores a SET of column names precisely so re-discovery of the SAME column is harmless.
        //
        // If it ever counted occurrences instead, DualArticle (one foreign key per target table)
        // would gain a phantom second entry and LoadWith would start rejecting a VALID entity.

        // Arrange
        await RegisterDualEntitiesAsync();

        List<string> beforePerson = TrackedForeignKeys(typeof(DualArticle), nameof(DualPerson));
        List<string> beforePaper = TrackedForeignKeys(typeof(DualPaper), nameof(DualPerson));

        // Act - force the PRAGMA-based discovery to run again over the same schema.
        ResetAssociationInitFor(TestDatabaseName);
        await RunSchemaDiscoveryAsync(TestDatabaseName);

        // Assert - re-discovery changed nothing.
        TrackedForeignKeys(typeof(DualArticle), nameof(DualPerson)).Should().BeEquivalentTo(
            beforePerson,
            "re-discovering the same foreign key must not add a duplicate entry");

        TrackedForeignKeys(typeof(DualPaper), nameof(DualPerson)).Should().BeEquivalentTo(
            beforePaper,
            "the ambiguous entity's tracked columns must also stay stable");
    }

    [Fact]
    public async Task SchemaDiscovery_Rerun_ValidEntityStillLoadsCorrectly()
    {
        // The behavioral consequence of the idempotency guarantee: after the schema-discovery path
        // has run again, a valid entity must still be accepted by LoadWith AND still load the right
        // rows. This is the end-to-end version of the test above.

        // Arrange
        await RegisterDualEntitiesAsync();
        (DualPerson writer, DualJournal journal) = await CreateArticleDataAsync();

        ResetAssociationInitFor(TestDatabaseName);
        await RunSchemaDiscoveryAsync(TestDatabaseName);

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            DualArticle? article = await ctx.GetTable<DualArticle>()
                .LoadWith(a => a.Writer)
                .LoadWith(a => a.Journal)
                .FirstOrDefaultAsync(a => a.Title == "Distinct Targets Article");

            article.Should().NotBeNull();
            article!.Writer.Should().NotBeNull("schema re-discovery must not invalidate a valid association");
            article.Writer!.id.Should().Be(writer.id, "the correct row must still be loaded");
            article.Journal.Should().NotBeNull();
            article.Journal!.id.Should().Be(journal.id);
        }
    }

    [Fact]
    public async Task SchemaDiscovery_Rerun_AmbiguousEntityIsStillRejected()
    {
        // The restriction must survive the schema-discovery path. If that path recorded foreign keys
        // under a different name format (a column alias, different casing, a quoted identifier), the
        // ambiguous entity could end up tracked under two separate keys of one column each - and
        // LoadWith would silently start permitting the broken case again.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        ResetAssociationInitFor(TestDatabaseName);
        await RunSchemaDiscoveryAsync(TestDatabaseName);

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.Author);

            act.Should().Throw<ArgumentException>(
                    "the ambiguity must still be detected after PRAGMA-based discovery")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");
        }
    }

    [Fact]
    public async Task SchemaDiscovery_AloneRepopulatesForeignKeyTracking()
    {
        // PROOF THAT THE OTHER TESTS IN THIS REGION ARE NOT VACUOUS.
        //
        // The re-run tests above would pass trivially if the PRAGMA path never actually executed or
        // never reached the tracker. This test empties the tracker completely, runs ONLY the
        // schema-discovery path, and requires that discovery alone restores the entries.
        //
        // That proves the PRAGMA path genuinely feeds TrackForeignKeyTarget, which is the assumption
        // the idempotency design rests on.

        // Arrange
        await RegisterDualEntitiesAsync();

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

        Type helpersType = typeof(SxmEntity).Assembly.GetType("SQLiteXM.SxmHelpers")!;
        System.Collections.IDictionary tracking = (System.Collections.IDictionary)helpersType
            .GetField("_foreignKeyTargets", flags)!
            .GetValue(null)!;

        // _foreignKeyTargets is global across every registered entity and database, but schema
        // discovery below only repopulates TestDatabaseName. Snapshot every entry first and restore
        // it in the finally block, otherwise entities registered against another database would
        // lose their tracking for the rest of the run - which would silently DISABLE the ambiguity
        // check in later tests rather than fail loudly.
        List<System.Collections.DictionaryEntry> snapshot = new List<System.Collections.DictionaryEntry>();
        foreach (System.Collections.DictionaryEntry entry in tracking)
        {
            snapshot.Add(entry);
        }

        try
        {
            tracking.Clear();

            TrackedForeignKeys(typeof(DualPaper), nameof(DualPerson)).Should().BeEmpty(
                "the tracker was just cleared, so the test starts from a known-empty state");

            // Act - schema discovery is the ONLY thing that runs here.
            ResetAssociationInitFor(TestDatabaseName);
            await RunSchemaDiscoveryAsync(TestDatabaseName);

            // Assert
            TrackedForeignKeys(typeof(DualPaper), nameof(DualPerson)).Should().BeEquivalentTo(
                new[] { "AuthorId", "CoAuthorId" },
                "PRAGMA-based discovery must record foreign keys under the same column names as the "
                + "attribute path, otherwise the two paths would track the same key under different "
                + "names and the ambiguity check could be defeated");

            TrackedForeignKeys(typeof(DualArticle), nameof(DualJournal)).Should().BeEquivalentTo(
                new[] { "JournalId" },
                "discovery must also resolve the target table name the same way the attribute path does");
        }
        finally
        {
            // Restore every pre-existing entry, including any this test's discovery run did not
            // recreate. Entries added by discovery are left in place; they are correct.
            foreach (System.Collections.DictionaryEntry entry in snapshot)
            {
                tracking[entry.Key] = entry.Value;
            }
        }
    }

    #endregion

    #region Other LoadWith entry points

    [Fact]
    public async Task LoadWith_ParamsOverload_AlsoEnforcesTheRestriction()
    {
        // The params overload takes Expression<Func<T, object?>>, so the compiler inserts a boxing
        // Convert around the property access. The check must still see through that to the real
        // property type - otherwise the ambiguous case would slip through this entry point.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => (object?)p.Author, p => (object?)p.CoAuthor);

            act.Should().Throw<ArgumentException>(
                    "the params overload must enforce the same restriction as the single-property overload")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");
        }
    }

    [Fact]
    public async Task LoadWith_ParamsOverload_StillAcceptsValidEntities()
    {
        // Matching negative control for the boxed path: unwrapping the Convert must not accidentally
        // make valid entities look ambiguous.

        // Arrange
        await RegisterDualEntitiesAsync();
        (DualPerson writer, DualJournal journal) = await CreateArticleDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            DualArticle? article = await ctx.GetTable<DualArticle>()
                .LoadWith(a => (object?)a.Writer, a => (object?)a.Journal)
                .FirstOrDefaultAsync(a => a.Title == "Distinct Targets Article");

            article.Should().NotBeNull();
            article!.Writer.Should().NotBeNull();
            article.Writer!.id.Should().Be(writer.id);
            article.Journal.Should().NotBeNull();
            article.Journal!.id.Should().Be(journal.id);
        }
    }

    [Fact]
    public async Task LoadWith_NestedPath_IsCheckedAgainstTheFirstHop()
    {
        // Validation resolves nested paths back to the first hop. For the ambiguity check that is
        // the correct target to test, because the first hop is the one declared on T and therefore
        // the one whose foreign keys could be ambiguous.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.Author!.PersonName);

            act.Should().Throw<ArgumentException>(
                    "the first hop 'Author' targets a table referenced by two foreign keys")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");
        }
    }

    [Fact]
    public async Task LoadWith_ScalarColumn_StillReportsNotANavigationProperty()
    {
        // The ambiguity check runs BEFORE the registration check, so it must not swallow or reword
        // the pre-existing error for an ordinary column. AuthorId is a scalar on an entity that also
        // has the ambiguous foreign keys, which makes it the case most likely to be misreported.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.AuthorId);

            act.Should().Throw<ArgumentException>()
               .WithMessage("*is not a navigation property*",
                    "a scalar column must keep its original diagnostic, not the ambiguity message");
        }
    }

    [Fact]
    public async Task LoadWith_ThreeForeignKeysToSameTable_IsRejectedWithAccurateCount()
    {
        // The threshold is "more than one", so three must behave like two. The count and every
        // column name in the message must be accurate, since a user reads them to decide which
        // joins to write.

        // Arrange
        await RegisterDualEntitiesAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<TriplePanel> table = ctx.GetTable<TriplePanel>();

            Action act = () => table.LoadWith(p => p.Speaker);

            ArgumentException ex = act.Should().Throw<ArgumentException>(
                    "three foreign keys to one table are at least as ambiguous as two")
               .Which;

            ex.Message.Should().Contain("declares 3 foreign keys referencing 'DualPerson'",
                "the reported count must match reality, not be hard-coded to 2");
            ex.Message.Should().Contain("ChairId");
            ex.Message.Should().Contain("SpeakerId");
            ex.Message.Should().Contain("ScribeId");
        }
    }

    #endregion

    #region LoadWith (enforced restriction)

    [Fact]
    public async Task LoadWith_FirstNavigationProperty_IsRejected()
    {
        // Author is declared first and used to load successfully (apparently correctly). That was
        // luck: its column order and property order happened to line up. Because DualPaper has two
        // foreign keys to DualPerson, LoadWith now refuses it outright.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.Author);

            act.Should().Throw<ArgumentException>(
                    "LoadWith is unsupported when two foreign keys reference the same target table")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");
        }
    }

    [Fact]
    public async Task LoadWith_SecondNavigationProperty_IsRejected()
    {
        // CoAuthor is declared second and was already rejected, but previously with a misleading
        // "not a navigation property" message. It is now rejected for the real reason.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.CoAuthor);

            act.Should().Throw<ArgumentException>(
                    "the ambiguity is reported for every navigation property targeting the shared table")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");
        }
    }

    [Fact]
    public async Task LoadWith_ErrorMessage_NamesTheColumnsAndSuggestsAJoin()
    {
        // The exception has to be actionable: it must name the competing foreign-key columns and
        // point at the supported alternative.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualPaper> table = ctx.GetTable<DualPaper>();

            Action act = () => table.LoadWith(p => p.Author);

            ArgumentException ex = act.Should().Throw<ArgumentException>().Which;

            ex.Message.Should().Contain("AuthorId", "the competing foreign keys must be named");
            ex.Message.Should().Contain("CoAuthorId", "the competing foreign keys must be named");
            ex.Message.Should().Contain("explicit join", "the supported alternative must be offered");
        }
    }

    [Fact]
    public async Task LoadWith_ReversedDeclarationOrder_IsRejectedInsteadOfLoadingWrongColumn()
    {
        // THIS IS THE CASE THE CHECK EXISTS FOR.
        //
        // DualReport declares its foreign-key columns as EditorId then ReviewerId, but declares its
        // navigation properties in the opposite order: Reviewer then Editor.
        //
        // Association registration matches a foreign key to a navigation property using ONLY the
        // target CLR type (see SxmHelpers.CreateAssociation). It took the first [NotColumn] property
        // of type DualPerson - Reviewer - and wired it to the first foreign key processed - EditorId.
        // LoadWith(r => r.Reviewer) therefore returned the EDITOR, with no error at all.
        //
        // LoadWith now detects the two foreign keys to DualPerson and throws instead. This test
        // guards against regressing to the silent wrong-data behavior.

        // Arrange
        await RegisterDualEntitiesAsync();
        (DualPerson editor, DualPerson reviewer) = await CreateReportDataAsync();

        // Act / Assert
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            SxmTable<DualReport> table = ctx.GetTable<DualReport>();

            Action loadReviewer = () => table.LoadWith(r => r.Reviewer);
            Action loadEditor = () => table.LoadWith(r => r.Editor);

            loadReviewer.Should().Throw<ArgumentException>(
                    "mismatched declaration order previously made Reviewer load from EditorId")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");

            loadEditor.Should().Throw<ArgumentException>(
                    "neither slot can be trusted when two foreign keys share a target table")
               .WithMessage("*declares 2 foreign keys referencing 'DualPerson'*");

            // The stored foreign-key values were never the problem - only the eager-loading pairing.
            DualReport? report = await table
                .FirstOrDefaultAsync(r => r.Title == "Reversed Declaration Report");

            report.Should().NotBeNull();
            report!.EditorId.Should().Be(editor.id);
            report.ReviewerId.Should().Be(reviewer.id);
        }
    }

    #endregion

    #region Workaround

    [Fact]
    public async Task ExplicitJoin_ShouldResolveBothRolesCorrectly()
    {
        // The recommended workaround. A join names the joining column explicitly, so both roles
        // resolve correctly with no reliance on the association registry.

        // Arrange
        await RegisterDualEntitiesAsync();
        await CreateTestDataAsync();

        // Act
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            List<DualPaper> papers = await ctx.GetTable<DualPaper>().ToListAsync();
            List<DualPerson> people = await ctx.GetTable<DualPerson>().ToListAsync();

            DualPaper paper = papers.Single(p => p.Title == "Shared Target Paper");

            string? authorName = people.Single(x => x.id == paper.AuthorId).PersonName;
            string? coAuthorName = people.Single(x => x.id == paper.CoAuthorId).PersonName;

            // Assert
            authorName.Should().Be("Primary Writer");
            coAuthorName.Should().Be("Secondary Writer");
        }
    }

    #endregion
}
