using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Tests the schema-evolution behaviours documented in Docs/schema-evolution.md for indexes, triggers,
/// column drops, and the "unsupported change" contract.
///
/// Pattern: the "old" schema is created with raw SQL (simulating a database produced by a previous
/// version of the application), data is inserted, then the "new" entity is registered and the live
/// schema is inspected via the SQLite pragmas / sqlite_master.
/// </summary>
[Collection("Sequential")]
public class SchemaEvolutionTests : TestBase
{
    // ==================================================================================
    // Entities.  Each maps to a dedicated table name so tests do not interfere.
    // ==================================================================================

    // --- Indexes -------------------------------------------------------------------

    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxAdd : SxmEntity
    {
        public string? Name { get; set; }
        [Index] public string? Email { get; set; }
    }

    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxAddUnique : SxmEntity
    {
        public string? Name { get; set; }
        [UniqueIndex] public string? Email { get; set; }
    }

    [Table(IsColumnAttributeRequired = false)]
    [Index(nameof(FirstName), nameof(LastName))]
    public class EvoIdxAddComposite : SxmEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    [Table(IsColumnAttributeRequired = false)]
    [UniqueIndex(nameof(FirstName), nameof(LastName))]
    public class EvoIdxAddCompositeUnique : SxmEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    // No index attribute: used to verify removal of a pre-existing index.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxRemove : SxmEntity
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
    }

    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxRemoveComposite : SxmEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    // Existing index is UNIQUE; entity says non-unique.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxUniqueToStandard : SxmEntity
    {
        [Index] public string? Email { get; set; }
    }

    // Existing index is non-unique; entity says UNIQUE.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxStandardToUnique : SxmEntity
    {
        [UniqueIndex] public string? Email { get; set; }
    }

    // Existing index is on (FirstName); entity wants (FirstName, LastName).
    [Table(IsColumnAttributeRequired = false)]
    [Index(nameof(FirstName), nameof(LastName))]
    public class EvoIdxCompositeChanged : SxmEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    // Existing table has duplicate emails; entity demands UNIQUE.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxUniqueOverDuplicates : SxmEntity
    {
        [UniqueIndex] public string? Email { get; set; }
    }

    // Already-correct index: second registration must be a no-op.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoIdxIdempotent : SxmEntity
    {
        [Index] public string? Email { get; set; }
        [UniqueIndex] public string? Code { get; set; }
    }

    // --- Triggers ------------------------------------------------------------------

    [Table(IsColumnAttributeRequired = false)]
    public class EvoTrgAudit : SxmEntity
    {
        public string? Source { get; set; }
        public string? Action { get; set; }
    }

    [Table(IsColumnAttributeRequired = false)]
    [Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_EvoTrgAdd_ai AFTER INSERT ON EvoTrgAdd
               BEGIN INSERT INTO EvoTrgAudit (Source, Action) VALUES ('EvoTrgAdd', 'INSERT'); END")]
    public class EvoTrgAdd : SxmEntity
    {
        public string? Name { get; set; }
    }

    // No [Trigger]: a trigger pre-created on this table must be removed on restart.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoTrgRemove : SxmEntity
    {
        public string? Name { get; set; }
    }

    // Trigger body differs from the one pre-created in the database.
    [Table(IsColumnAttributeRequired = false)]
    [Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_EvoTrgModify_ai AFTER INSERT ON EvoTrgModify
               BEGIN INSERT INTO EvoTrgAudit (Source, Action) VALUES ('EvoTrgModify', 'V2'); END")]
    public class EvoTrgModify : SxmEntity
    {
        public string? Name { get; set; }
    }

    // --- Drop column ----------------------------------------------------------------

    [Table(IsColumnAttributeRequired = false)]
    public class EvoDropCol : SxmEntity
    {
        public string? Name { get; set; }
    }

    // Stale column is still referenced by an index -> index removed first, then column dropped.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoDropColIndexed : SxmEntity
    {
        public string? Name { get; set; }
    }

    // Stale column appears ONLY as a SET target in a declared trigger. SQLite does not resolve
    // SET-target identifiers during DROP COLUMN validation, so the drop is permitted.
    [Table(IsColumnAttributeRequired = false)]
    [Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_EvoDropColTriggered_au AFTER UPDATE ON EvoDropColTriggered
               BEGIN UPDATE EvoDropColTriggered SET Legacy = 'touched' WHERE id = NEW.id; END")]
    public class EvoDropColTriggered : SxmEntity
    {
        public string? Name { get; set; }
    }

    // Stale column is referenced in a trigger EXPRESSION (NEW.Legacy). SQLite rejects the drop.
    [Table(IsColumnAttributeRequired = false)]
    [Trigger(@"CREATE TRIGGER IF NOT EXISTS trg_EvoDropColTriggerExpr_au AFTER UPDATE ON EvoDropColTriggerExpr
               WHEN NEW.Legacy IS NOT NULL
               BEGIN UPDATE EvoDropColTriggerExpr SET Name = 'touched' WHERE id = NEW.id; END")]
    public class EvoDropColTriggerExpr : SxmEntity
    {
        public string? Name { get; set; }
    }

    // --- Combined changes / retry ----------------------------------------------------

    // V1 schema: Title (indexed), Obsolete.  V2: Title renamed to Name (still indexed), Email added, Obsolete dropped.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoCombined : SxmEntity
    {
        [Rename("Title")]
        [Index] public string? Name { get; set; }
        public string? Email { get; set; }
    }

    // V1 schema: Name, Email (no index, duplicates present).  V2: Notes added, Email becomes [UniqueIndex].
    // Registration adds Notes (commits) then fails creating the unique index over duplicate data.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoRetry : SxmEntity
    {
        public string? Name { get; set; }
        [UniqueIndex] public string? Email { get; set; }
        public string? Notes { get; set; }
    }

    // --- Unsupported changes ---------------------------------------------------------

    // DB column Age is TEXT; entity says int.
    [Table(IsColumnAttributeRequired = false)]
    public class EvoTypeChange : SxmEntity
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    // DB column Name is nullable; entity says [RequiredNotNull].
    [Table(IsColumnAttributeRequired = false)]
    public class EvoNullabilityChange : SxmEntity
    {
        [RequiredNotNull("n/a")] public string? Name { get; set; }
    }

    // ==================================================================================
    // Helpers
    // ==================================================================================

    private static string Idx(string table, params string[] cols) => "IDX_" + table + "_" + string.Join("_", cols);

    private async Task CreateLegacyTableAsync(string table, string extraColumns)
    {
        await DropTableDirectlyAsync(table);
        ResetSchemaRegistrationFor(GetType().GetNestedType(table)!);
        await ExecuteNonQueryAsync(
            $"CREATE TABLE \"{table}\" (id INTEGER PRIMARY KEY AUTOINCREMENT, synchId BLOB DEFAULT (randomblob(16)), {extraColumns})");
    }

    private static Task RegisterAsync<T>() where T : SxmEntity => SxmDatabase.RegisterEntitiesAsync(typeof(T));

    // ==================================================================================
    // Indexes - add
    // ==================================================================================

    [Fact]
    public async Task AddIndex_OnExistingTable_ShouldCreateStandardIndex()
    {
        const string t = nameof(EvoIdxAdd);
        await CreateLegacyTableAsync(t, "Name TEXT, Email TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Email) VALUES ('a', 'a@x'), ('b', 'b@x')");

        await RegisterAsync<EvoIdxAdd>();

        var indexes = await GetIndexesAsync(t);
        indexes.Should().ContainSingle(i => i.Name == Idx(t, "Email") && !i.Unique);
        (await GetIndexColumnsAsync(Idx(t, "Email"))).Should().Equal("Email");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2, "existing rows must survive");
    }

    [Fact]
    public async Task AddUniqueIndex_OnExistingTable_ShouldCreateAndEnforce()
    {
        const string t = nameof(EvoIdxAddUnique);
        await CreateLegacyTableAsync(t, "Name TEXT, Email TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Email) VALUES ('a', 'a@x')");

        await RegisterAsync<EvoIdxAddUnique>();

        (await GetIndexesAsync(t)).Should().ContainSingle(i => i.Name == Idx(t, "Email") && i.Unique);

        var dup = new EvoIdxAddUnique { Name = "dup", Email = "a@x" };
        Func<Task> act = () => dup.SaveAsync();
        await act.Should().ThrowAsync<Exception>("the new unique index must be enforced");
    }

    [Fact]
    public async Task AddCompositeIndex_OnExistingTable_ShouldCreateWithColumnsInOrder()
    {
        const string t = nameof(EvoIdxAddComposite);
        await CreateLegacyTableAsync(t, "FirstName TEXT, LastName TEXT");

        await RegisterAsync<EvoIdxAddComposite>();

        string name = Idx(t, "FirstName", "LastName");
        (await GetIndexesAsync(t)).Should().ContainSingle(i => i.Name == name && !i.Unique);
        (await GetIndexColumnsAsync(name)).Should().Equal("FirstName", "LastName");
    }

    [Fact]
    public async Task AddCompositeUniqueIndex_OnExistingTable_ShouldCreateUnique()
    {
        const string t = nameof(EvoIdxAddCompositeUnique);
        await CreateLegacyTableAsync(t, "FirstName TEXT, LastName TEXT");

        await RegisterAsync<EvoIdxAddCompositeUnique>();

        string name = Idx(t, "FirstName", "LastName");
        (await GetIndexesAsync(t)).Should().ContainSingle(i => i.Name == name && i.Unique);
    }

    // ==================================================================================
    // Indexes - remove
    // ==================================================================================

    [Fact]
    public async Task RemoveIndex_AttributeRemoved_ShouldDropIndex()
    {
        const string t = nameof(EvoIdxRemove);
        await CreateLegacyTableAsync(t, "Name TEXT, Email TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "Email")} ON {t} (Email)");
        await ExecuteNonQueryAsync($"CREATE UNIQUE INDEX {Idx(t, "Name")} ON {t} (Name)");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Email) VALUES ('a', 'a@x')");

        await RegisterAsync<EvoIdxRemove>();

        (await GetIndexesAsync(t)).Should().BeEmpty("both the standard and the unique index were removed from the entity");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(1);
    }

    [Fact]
    public async Task RemoveCompositeIndex_AttributeRemoved_ShouldDropIndex()
    {
        const string t = nameof(EvoIdxRemoveComposite);
        await CreateLegacyTableAsync(t, "FirstName TEXT, LastName TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "FirstName", "LastName")} ON {t} (FirstName, LastName)");

        await RegisterAsync<EvoIdxRemoveComposite>();

        (await GetIndexesAsync(t)).Should().BeEmpty();
    }

    // ==================================================================================
    // Indexes - modify
    // ==================================================================================

    [Fact]
    public async Task ModifyIndex_UniqueToStandard_ShouldRecreateAsNonUnique()
    {
        const string t = nameof(EvoIdxUniqueToStandard);
        await CreateLegacyTableAsync(t, "Email TEXT");
        await ExecuteNonQueryAsync($"CREATE UNIQUE INDEX {Idx(t, "Email")} ON {t} (Email)");

        await RegisterAsync<EvoIdxUniqueToStandard>();

        (await GetIndexesAsync(t)).Should().ContainSingle(i => i.Name == Idx(t, "Email") && !i.Unique);

        // Duplicates must now be allowed.
        await new EvoIdxUniqueToStandard { Email = "same@x" }.SaveAsync();
        await new EvoIdxUniqueToStandard { Email = "same@x" }.SaveAsync();
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t} WHERE Email = 'same@x'")).Should().Be(2);
    }

    [Fact]
    public async Task ModifyIndex_StandardToUnique_WithUniqueData_ShouldRecreateAsUnique()
    {
        const string t = nameof(EvoIdxStandardToUnique);
        await CreateLegacyTableAsync(t, "Email TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "Email")} ON {t} (Email)");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Email) VALUES ('a@x'), ('b@x')");

        await RegisterAsync<EvoIdxStandardToUnique>();

        (await GetIndexesAsync(t)).Should().ContainSingle(i => i.Name == Idx(t, "Email") && i.Unique);

        Func<Task> act = () => new EvoIdxStandardToUnique { Email = "a@x" }.SaveAsync();
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ModifyIndex_StandardToUnique_WithDuplicateData_ShouldFailAndLeaveDataIntact()
    {
        const string t = nameof(EvoIdxUniqueOverDuplicates);
        await CreateLegacyTableAsync(t, "Email TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Email) VALUES ('dup@x'), ('dup@x')");

        Func<Task> act = () => RegisterAsync<EvoIdxUniqueOverDuplicates>();

        await act.Should().ThrowAsync<Exception>("the documented 'Caution!' case: unique index over duplicate data must fail registration");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2, "failed migration must not touch data");
        (await GetIndexesAsync(t)).Should().BeEmpty("no partial index may be left behind");

        // Leave registration state clean for other tests.
        ResetSchemaRegistrationFor(typeof(EvoIdxUniqueOverDuplicates));
    }

    [Fact]
    public async Task ModifyCompositeIndex_ColumnsChanged_ShouldDropOldAndCreateNew()
    {
        const string t = nameof(EvoIdxCompositeChanged);
        await CreateLegacyTableAsync(t, "FirstName TEXT, LastName TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "FirstName")} ON {t} (FirstName)");

        await RegisterAsync<EvoIdxCompositeChanged>();

        var indexes = await GetIndexesAsync(t);
        indexes.Should().NotContain(i => i.Name == Idx(t, "FirstName"), "old index must be dropped");
        indexes.Should().ContainSingle(i => i.Name == Idx(t, "FirstName", "LastName") && !i.Unique);
    }

    [Fact]
    public async Task Index_ReRegistration_WhenSchemaAlreadyMatches_ShouldBeNoOp()
    {
        const string t = nameof(EvoIdxIdempotent);
        await DropTableDirectlyAsync(t);
        ResetSchemaRegistrationFor(typeof(EvoIdxIdempotent));

        await RegisterAsync<EvoIdxIdempotent>();
        var first = await GetIndexesAsync(t);

        ResetSchemaRegistrationFor(typeof(EvoIdxIdempotent));
        await RegisterAsync<EvoIdxIdempotent>();
        var second = await GetIndexesAsync(t);

        first.Should().BeEquivalentTo(new[] { (Idx(t, "Email"), false), (Idx(t, "Code"), true) });
        second.Should().BeEquivalentTo(first);
    }

    // ==================================================================================
    // Triggers
    // ==================================================================================

    [Fact]
    public async Task AddTrigger_OnExistingTable_ShouldCreateAndFire()
    {
        const string t = nameof(EvoTrgAdd);
        await CreateLegacyTableAsync(nameof(EvoTrgAudit), "Source TEXT, Action TEXT");
        await RegisterAsync<EvoTrgAudit>();
        await CreateLegacyTableAsync(t, "Name TEXT");

        await RegisterAsync<EvoTrgAdd>();

        (await TriggerExistsAsync("trg_EvoTrgAdd_ai")).Should().BeTrue();

        await new EvoTrgAdd { Name = "x" }.SaveAsync();
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM EvoTrgAudit WHERE Source = 'EvoTrgAdd' AND Action = 'INSERT'"))
            .Should().Be(1, "the trigger must actually fire");
    }

    [Fact]
    public async Task RemoveTrigger_AttributeRemoved_ShouldDropTriggerOnRestart()
    {
        const string t = nameof(EvoTrgRemove);
        await CreateLegacyTableAsync(t, "Name TEXT");
        await ExecuteNonQueryAsync(
            $"CREATE TRIGGER trg_EvoTrgRemove_ai AFTER INSERT ON {t} BEGIN UPDATE {t} SET Name = 'legacy' WHERE id = NEW.id; END");
        (await TriggerExistsAsync("trg_EvoTrgRemove_ai")).Should().BeTrue("precondition");

        await RestartSqliteXMAsync();
        await RegisterAsync<EvoTrgRemove>();

        (await TriggerExistsAsync("trg_EvoTrgRemove_ai")).Should().BeFalse("triggers not declared by any entity are removed at initialization");

        var e = new EvoTrgRemove { Name = "fresh" };
        await e.SaveAsync();
        (await ExecuteScalarAsync<string>($"SELECT Name FROM {t} WHERE id = {e.id}")).Should().Be("fresh", "the removed trigger must no longer fire");
    }

    [Fact]
    public async Task ModifyTrigger_BodyChanged_ShouldReplaceTriggerOnRestart()
    {
        const string t = nameof(EvoTrgModify);
        await CreateLegacyTableAsync(nameof(EvoTrgAudit), "Source TEXT, Action TEXT");
        await CreateLegacyTableAsync(t, "Name TEXT");
        await ExecuteNonQueryAsync(
            $"CREATE TRIGGER trg_EvoTrgModify_ai AFTER INSERT ON {t} BEGIN INSERT INTO EvoTrgAudit (Source, Action) VALUES ('EvoTrgModify', 'V1'); END");

        await RestartSqliteXMAsync();
        await RegisterAsync<EvoTrgAudit>();
        await RegisterAsync<EvoTrgModify>();

        (await GetTriggerSqlAsync("trg_EvoTrgModify_ai")).Should().Contain("'V2'").And.NotContain("'V1'");

        await new EvoTrgModify { Name = "x" }.SaveAsync();
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM EvoTrgAudit WHERE Source = 'EvoTrgModify' AND Action = 'V2'")).Should().Be(1);
        (await ExecuteScalarAsync<long>("SELECT COUNT(*) FROM EvoTrgAudit WHERE Source = 'EvoTrgModify' AND Action = 'V1'")).Should().Be(0);
    }

    // ==================================================================================
    // Drop column
    // ==================================================================================

    [Fact]
    public async Task DropColumn_PropertyRemoved_ShouldRemoveColumnAndKeepRows()
    {
        const string t = nameof(EvoDropCol);
        await CreateLegacyTableAsync(t, "Name TEXT, Obsolete TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Obsolete) VALUES ('keep', 'gone')");

        await RegisterAsync<EvoDropCol>();

        (await ColumnExistsAsync(t, "Obsolete")).Should().BeFalse("the column must actually be dropped");
        (await ColumnExistsAsync(t, "Name")).Should().BeTrue();
        (await ColumnExistsAsync(t, "synchId")).Should().BeTrue("system columns are never dropped");
        (await ExecuteScalarAsync<string>($"SELECT Name FROM {t}")).Should().Be("keep");
    }

    [Fact]
    public async Task DropColumn_ReferencedByStaleIndex_ShouldDropIndexThenColumn()
    {
        const string t = nameof(EvoDropColIndexed);
        await CreateLegacyTableAsync(t, "Name TEXT, Obsolete TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "Obsolete")} ON {t} (Obsolete)");

        await RegisterAsync<EvoDropColIndexed>();

        (await GetIndexesAsync(t)).Should().BeEmpty();
        (await ColumnExistsAsync(t, "Obsolete")).Should().BeFalse();
    }

    [Fact]
    public async Task DropColumn_ReferencedByDeclaredTrigger_ShouldDropColumnAndKeepRows()
    {
        // The bundled SQLite does not reject DROP COLUMN for a column referenced only inside a
        // trigger body. Pin the observable contract: registration succeeds, the column is gone,
        // existing rows are intact, and the trigger is still installed.
        const string t = nameof(EvoDropColTriggered);
        await CreateLegacyTableAsync(t, "Name TEXT, Legacy TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Legacy) VALUES ('a', 'b')");

        await RegisterAsync<EvoDropColTriggered>();

        (await ColumnExistsAsync(t, "Legacy")).Should().BeFalse("the column no longer exists on the entity");
        (await ColumnExistsAsync(t, "Name")).Should().BeTrue();
        (await TriggerExistsAsync("trg_EvoDropColTriggered_au")).Should().BeTrue("the declared trigger is still installed");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(1, "dropping a column must not delete rows");
    }

    [Fact]
    public async Task DropColumn_ReferencedInTriggerExpression_ShouldFailAndKeepColumnAndRows()
    {
        // SQLite rejects DROP COLUMN when the column is used in a trigger expression.
        // SQLiteXM must surface that failure and leave the schema and data untouched.
        const string t = nameof(EvoDropColTriggerExpr);
        await CreateLegacyTableAsync(t, "Name TEXT, Legacy TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Legacy) VALUES ('a', 'b')");

        Func<Task> act = () => RegisterAsync<EvoDropColTriggerExpr>();

        await act.Should().ThrowAsync<Exception>("SQLite refuses DROP COLUMN on a column referenced in a trigger expression");
        (await ColumnExistsAsync(t, "Legacy")).Should().BeTrue("the column must be left in place");
        (await TriggerExistsAsync("trg_EvoDropColTriggerExpr_au")).Should().BeTrue();
        (await ExecuteScalarAsync<string>($"SELECT Legacy FROM {t}")).Should().Be("b", "data must be intact");

        ResetSchemaRegistrationFor(typeof(EvoDropColTriggerExpr));
    }

    // ==================================================================================
    // Combined changes in a single registration
    // ==================================================================================

    [Fact]
    public async Task CombinedChange_RenameIndexedColumn_AddColumn_DropColumn_ShouldApplyAllInOneRegistration()
    {
        const string t = nameof(EvoCombined);
        await CreateLegacyTableAsync(t, "Title TEXT, Obsolete TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "Title")} ON {t} (Title)");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Title, Obsolete) VALUES ('first', 'x'), ('second', 'y')");

        await RegisterAsync<EvoCombined>();

        // Rename
        (await ColumnExistsAsync(t, "Title")).Should().BeFalse("Title must be renamed away");
        (await ColumnExistsAsync(t, "Name")).Should().BeTrue("Title must be renamed to Name");

        // Add
        (await ColumnExistsAsync(t, "Email")).Should().BeTrue("Email must be added");

        // Drop
        (await ColumnExistsAsync(t, "Obsolete")).Should().BeFalse("Obsolete must be dropped");

        // Index follows the rename: SQLite rewrites the old index to reference Name, but its name is
        // stale. SQLiteXM must drop the stale-named index and create one under the new generated name.
        var indexes = await GetIndexesAsync(t);
        indexes.Select(i => i.Name).Should().BeEquivalentTo(new[] { Idx(t, "Name") });
        (await GetIndexColumnsAsync(Idx(t, "Name"))).Should().Equal("Name");

        // Data survives the rename and the drop
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2);
        (await ExecuteScalarAsync<string>($"SELECT Name FROM {t} ORDER BY id LIMIT 1")).Should().Be("first");

        // Round-trip through the entity API on the migrated table
        var saved = new EvoCombined { Name = "third", Email = "t@x.com" };
        await saved.SaveAsync();
        saved.id.Should().BeGreaterThan(0);
        (await ExecuteScalarAsync<string>($"SELECT Email FROM {t} WHERE id = {saved.id}")).Should().Be("t@x.com");
    }

    // ==================================================================================
    // Failure handling. The failure-cleanup path must NEVER run for a successful registration,
    // and must fully erase in-memory state (but not database state) for a failed one.
    // ==================================================================================

    [Fact]
    public async Task SuccessfulMigration_ShouldLeaveRegistrationStateFullyIntact()
    {
        // Same V1 -> V2 migration as the combined-change test, but the assertions target the
        // in-memory registration state rather than the schema: nothing the failure path clears
        // may be missing after a registration that did NOT fail.
        const string t = nameof(EvoCombined);
        await CreateLegacyTableAsync(t, "Title TEXT, Obsolete TEXT");
        await ExecuteNonQueryAsync($"CREATE INDEX {Idx(t, "Title")} ON {t} (Title)");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Title, Obsolete) VALUES ('first', 'x')");

        await RegisterAsync<EvoCombined>();

        SxmSchemaRegistration.IsSchemaRegistered(typeof(EvoCombined)).Should().BeTrue("a successful registration must remain registered");
        SxmEntity._columnNameAndTypeDict.Should().ContainKey(t, "the column map must survive a successful registration");
        SxmEntity._columnNameAndTypeDict[t].Keys.Should().Contain(new[] { "Name", "Email" });

        // Entity API works without any "not registered" error.
        var e = new EvoCombined { Name = "ok", Email = "ok@x.com" };
        await e.SaveAsync();
        e.id.Should().BeGreaterThan(0);

        // A second registration of an already-registered type is a silent no-op: still registered, no DDL.
        var indexesBefore = await GetIndexesAsync(t);
        await RegisterAsync<EvoCombined>();
        SxmSchemaRegistration.IsSchemaRegistered(typeof(EvoCombined)).Should().BeTrue();
        (await GetIndexesAsync(t)).Should().BeEquivalentTo(indexesBefore);
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2);
    }

    [Fact]
    public async Task FailedMigration_ShouldLeaveTypeUnregistered_AndDatabaseUntouched()
    {
        const string t = nameof(EvoRetry);
        await CreateLegacyTableAsync(t, "Name TEXT, Email TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Email) VALUES ('a', 'dup@x.com'), ('b', 'dup@x.com')");

        Func<Task> act = () => RegisterAsync<EvoRetry>();
        await act.Should().ThrowAsync<Exception>();

        // In-memory: erased. Continuing to use the entity must fail LOUDLY.
        SxmSchemaRegistration.IsSchemaRegistered(typeof(EvoRetry)).Should().BeFalse("a failed registration must not be reported as registered");
        SxmEntity._columnNameAndTypeDict.Should().NotContainKey(t, "the column map must be cleared on failure");
        Action ctor = () => _ = new EvoRetry();
        ctor.Should().Throw<InvalidOperationException>().WithMessage("*has not been registered*");

        // Database: untouched by the cleanup. Whatever committed before the failure stays committed.
        (await ColumnExistsAsync(t, "Notes")).Should().BeTrue("the add-column step committed before the index step failed");
        (await ColumnExistsAsync(t, "Name")).Should().BeTrue();
        (await ColumnExistsAsync(t, "Email")).Should().BeTrue();
        (await GetIndexesAsync(t)).Should().BeEmpty();
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2, "no data is ever removed by the failure path");
    }

    // ==================================================================================
    // Failure mid-migration, then retry
    // ==================================================================================

    [Fact]
    public async Task FailedMigration_UniqueIndexOverDuplicates_ThenRetryAfterDedupe_ShouldSucceedIdempotently()
    {
        const string t = nameof(EvoRetry);
        string indexName = Idx(t, "Email");

        // V1 database as an older app version would have left it: no Notes column, no index, duplicate emails.
        await CreateLegacyTableAsync(t, "Name TEXT, Email TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Email) VALUES ('a', 'dup@x.com'), ('b', 'dup@x.com'), ('c', 'unique@x.com')");

        // Attempt 1: add-column step commits, unique-index step fails on the duplicate data.
        Func<Task> act = () => RegisterAsync<EvoRetry>();
        await act.Should().ThrowAsync<Exception>("CREATE UNIQUE INDEX must fail while duplicate Email values exist");

        (await ColumnExistsAsync(t, "Notes")).Should().BeTrue("the add-column step committed before the index step failed");
        (await GetIndexesAsync(t)).Should().BeEmpty("the failed unique index must not exist");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(3, "data must be intact after a failed migration");

        // The application resolves the duplicate (what a real upgrade routine would do before retrying).
        await ExecuteNonQueryAsync($"DELETE FROM {t} WHERE Name = 'b'");

        // Attempt 2: retry must complete the remaining work without re-adding the column or erroring.
        await RegisterAsync<EvoRetry>();

        (await ColumnExistsAsync(t, "Notes")).Should().BeTrue();
        var indexes = await GetIndexesAsync(t);
        indexes.Should().ContainSingle(i => i.Name == indexName && i.Unique);
        (await GetIndexColumnsAsync(indexName)).Should().Equal("Email");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(2);

        // The migrated table is fully usable through the entity API and the new constraint is enforced.
        var saved = new EvoRetry { Name = "d", Email = "new@x.com", Notes = "after-retry" };
        await saved.SaveAsync();
        saved.id.Should().BeGreaterThan(0);

        Func<Task> violate = () => new EvoRetry { Name = "e", Email = "new@x.com" }.SaveAsync();
        await violate.Should().ThrowAsync<Exception>("the unique index created on retry must be enforced");
    }

    // ==================================================================================
    // Unsupported changes - pin the observable contract: no rebuild, no exception, schema untouched
    // ==================================================================================

    [Fact]
    public async Task UnsupportedChange_ColumnType_ShouldLeaveExistingTypeUnchanged()
    {
        const string t = nameof(EvoTypeChange);
        await CreateLegacyTableAsync(t, "Name TEXT, Age TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name, Age) VALUES ('a', '42')");

        await RegisterAsync<EvoTypeChange>();

        (await GetColumnTypeAsync(t, "Age")).Should().Be("TEXT", "SQLiteXM does not rebuild tables to change a storage type");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t}")).Should().Be(1);
    }

    [Fact]
    public async Task UnsupportedChange_Nullability_ShouldLeaveExistingConstraintUnchanged()
    {
        const string t = nameof(EvoNullabilityChange);
        await CreateLegacyTableAsync(t, "Name TEXT");
        await ExecuteNonQueryAsync($"INSERT INTO {t} (Name) VALUES (NULL)");

        await RegisterAsync<EvoNullabilityChange>();

        (await IsColumnNotNullAsync(t, "Name")).Should().BeFalse("SQLite cannot add NOT NULL to an existing column; SQLiteXM must not attempt it");
        (await ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {t} WHERE Name IS NULL")).Should().Be(1);
    }
}
