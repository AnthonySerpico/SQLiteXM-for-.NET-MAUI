using FluentAssertions;
using SQLiteXM;
using System.Diagnostics.CodeAnalysis;

namespace SQLiteXM.Tests;

/// <summary>
/// Entity dedicated to null-handling verification. Every mapped column is nullable and each
/// storage-type branch of <c>SxmHelpers.LoadParameterValues</c> is represented:
/// TEXT (string, decimal, ulong, Guid-as-text, time-types-as-text), INTEGER (integral, bool,
/// time types by default), REAL (float/double) and BLOB (byte[], Guid by default).
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
[Table(IsColumnAttributeRequired = false)]
public class NullTestEntity : SxmEntity
{
    // TEXT storage
    public string? StringValue { get; set; }
    public decimal? NullableDecimal { get; set; }
    public ulong? NullableULong { get; set; }

    // INTEGER storage
    public int? NullableInt { get; set; }
    public long? NullableLong { get; set; }
    public short? NullableShort { get; set; }
    public byte? NullableByte { get; set; }
    public bool? NullableBool { get; set; }

    // REAL storage
    public double? NullableDouble { get; set; }
    public float? NullableFloat { get; set; }

    // BLOB storage
    public byte[]? BlobValue { get; set; }
    public Guid? GuidBlob { get; set; }

    [Column(DataType = DataType.Text)]
    public Guid? GuidText { get; set; }

    // Time types - default INTEGER encoding
    public DateTime? DateTimeInt { get; set; }
    public DateTimeOffset? DateTimeOffsetInt { get; set; }
    public TimeSpan? TimeSpanInt { get; set; }
    public DateOnly? DateOnlyInt { get; set; }
    public TimeOnly? TimeOnlyInt { get; set; }

    // Time types - TEXT encoding override
    [Column(DataType = DataType.Text)]
    public DateTime? DateTimeText { get; set; }

    [Column(DataType = DataType.Text)]
    public DateTimeOffset? DateTimeOffsetText { get; set; }

    [Column(DataType = DataType.Text)]
    public TimeSpan? TimeSpanText { get; set; }

    [Column(DataType = DataType.Text)]
    public DateOnly? DateOnlyText { get; set; }

    [Column(DataType = DataType.Text)]
    public TimeOnly? TimeOnlyText { get; set; }
}

/// <summary>
/// Dedicated null-handling test suite.
/// <para>
/// Unlike the null assertions scattered through the CRUD, bulk-insert and LINQ suites - which all verify
/// nulls by round-tripping through the entity mapper - these tests assert against the raw SQLite storage
/// using <c>typeof(column)</c>. That distinction matters: the read path converts <see cref="DBNull"/> to
/// <c>default</c>, so a column containing an empty string or empty blob would still surface as a null
/// property. Verifying <c>typeof(column) = 'null'</c> proves a real SQL NULL was written.
/// </para>
/// <para>
/// Coverage: insert, update (non-null to null and null to non-null), bulk insert (all three entry points),
/// LINQ bulk update, the read path, LINQ null filtering, and the negative cases (empty string / empty blob
/// must NOT become NULL).
/// </para>
/// </summary>
[Collection("Sequential")]
public class NullHandlingTests : TestBase
{
    private const string TableName = nameof(NullTestEntity);

    public NullHandlingTests()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Every mapped nullable column on <see cref="NullTestEntity"/>.
    /// </summary>
    private static readonly string[] NullableColumns =
    {
        nameof(NullTestEntity.StringValue),
        nameof(NullTestEntity.NullableDecimal),
        nameof(NullTestEntity.NullableULong),
        nameof(NullTestEntity.NullableInt),
        nameof(NullTestEntity.NullableLong),
        nameof(NullTestEntity.NullableShort),
        nameof(NullTestEntity.NullableByte),
        nameof(NullTestEntity.NullableBool),
        nameof(NullTestEntity.NullableDouble),
        nameof(NullTestEntity.NullableFloat),
        nameof(NullTestEntity.BlobValue),
        nameof(NullTestEntity.GuidBlob),
        nameof(NullTestEntity.GuidText),
        nameof(NullTestEntity.DateTimeInt),
        nameof(NullTestEntity.DateTimeOffsetInt),
        nameof(NullTestEntity.TimeSpanInt),
        nameof(NullTestEntity.DateOnlyInt),
        nameof(NullTestEntity.TimeOnlyInt),
        nameof(NullTestEntity.DateTimeText),
        nameof(NullTestEntity.DateTimeOffsetText),
        nameof(NullTestEntity.TimeSpanText),
        nameof(NullTestEntity.DateOnlyText),
        nameof(NullTestEntity.TimeOnlyText)
    };

    public enum Api { SxmSql, SxmTransaction, Linq }

    public static IEnumerable<object[]> AllApis()
    {
        yield return new object[] { Api.SxmSql };
        yield return new object[] { Api.SxmTransaction };
        yield return new object[] { Api.Linq };
    }

    /// <summary>
    /// Builds an entity where every nullable property carries a non-null value.
    /// </summary>
    private static NullTestEntity NewPopulatedEntity() => new NullTestEntity
    {
        StringValue = "populated",
        NullableDecimal = 1234.5678m,
        NullableULong = ulong.MaxValue,
        NullableInt = 42,
        NullableLong = long.MaxValue,
        NullableShort = 7,
        NullableByte = 3,
        NullableBool = true,
        NullableDouble = 3.25,
        NullableFloat = 1.5f,
        BlobValue = new byte[] { 1, 2, 3, 4 },
        GuidBlob = Guid.NewGuid(),
        GuidText = Guid.NewGuid(),
        DateTimeInt = new DateTime(2024, 5, 17, 13, 45, 30, DateTimeKind.Utc),
        DateTimeOffsetInt = new DateTimeOffset(2024, 5, 17, 13, 45, 30, TimeSpan.Zero),
        TimeSpanInt = TimeSpan.FromMinutes(90),
        DateOnlyInt = new DateOnly(2024, 12, 25),
        TimeOnlyInt = new TimeOnly(14, 30, 0),
        DateTimeText = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        DateTimeOffsetText = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero),
        TimeSpanText = TimeSpan.FromHours(2),
        DateOnlyText = new DateOnly(2023, 6, 15),
        TimeOnlyText = new TimeOnly(8, 15, 0)
    };

    /// <summary>
    /// Clears every nullable property on an existing entity instance.
    /// </summary>
    private static void ClearAllProperties(NullTestEntity entity)
    {
        entity.StringValue = null;
        entity.NullableDecimal = null;
        entity.NullableULong = null;
        entity.NullableInt = null;
        entity.NullableLong = null;
        entity.NullableShort = null;
        entity.NullableByte = null;
        entity.NullableBool = null;
        entity.NullableDouble = null;
        entity.NullableFloat = null;
        entity.BlobValue = null;
        entity.GuidBlob = null;
        entity.GuidText = null;
        entity.DateTimeInt = null;
        entity.DateTimeOffsetInt = null;
        entity.TimeSpanInt = null;
        entity.DateOnlyInt = null;
        entity.TimeOnlyInt = null;
        entity.DateTimeText = null;
        entity.DateTimeOffsetText = null;
        entity.TimeSpanText = null;
        entity.DateOnlyText = null;
        entity.TimeOnlyText = null;
    }

    /// <summary>
    /// Returns the SQLite storage class of a column for a single row ('null', 'integer', 'real', 'text', 'blob').
    /// </summary>
    private async Task<string> StorageClassAsync(string column, long id) =>
        await ExecuteScalarAsync<string>($"SELECT typeof(\"{column}\") FROM {TableName} WHERE id = {id}");

    /// <summary>
    /// Asserts that every mapped nullable column of the row is a real SQL NULL.
    /// </summary>
    private async Task AssertAllColumnsAreSqlNullAsync(long id, string because)
    {
        foreach (var column in NullableColumns)
        {
            var storageClass = await StorageClassAsync(column, id);
            storageClass.Should().Be("null", $"column '{column}' {because}");
        }
    }

    /// <summary>
    /// Asserts that every property of the entity read back from the database is null.
    /// </summary>
    private static void AssertAllPropertiesAreNull(NullTestEntity entity)
    {
        entity.StringValue.Should().BeNull();
        entity.NullableDecimal.Should().BeNull();
        entity.NullableULong.Should().BeNull();
        entity.NullableInt.Should().BeNull();
        entity.NullableLong.Should().BeNull();
        entity.NullableShort.Should().BeNull();
        entity.NullableByte.Should().BeNull();
        entity.NullableBool.Should().BeNull();
        entity.NullableDouble.Should().BeNull();
        entity.NullableFloat.Should().BeNull();
        entity.BlobValue.Should().BeNull();
        entity.GuidBlob.Should().BeNull();
        entity.GuidText.Should().BeNull();
        entity.DateTimeInt.Should().BeNull();
        entity.DateTimeOffsetInt.Should().BeNull();
        entity.TimeSpanInt.Should().BeNull();
        entity.DateOnlyInt.Should().BeNull();
        entity.TimeOnlyInt.Should().BeNull();
        entity.DateTimeText.Should().BeNull();
        entity.DateTimeOffsetText.Should().BeNull();
        entity.TimeSpanText.Should().BeNull();
        entity.DateOnlyText.Should().BeNull();
        entity.TimeOnlyText.Should().BeNull();
    }

    private async Task<int> BulkInsertViaAsync(Api api, List<NullTestEntity> entities)
    {
        switch (api)
        {
            case Api.SxmSql:
                return await SxmSql.BulkInsertAsync(entities, SxmBulkInsertHelpers.DefaultBatchRows, TestDatabaseName);

            case Api.SxmTransaction:
                await using (var ctx = new SxmTransaction(TestDatabaseName))
                    return await ctx.BulkInsertAsync(entities, SxmBulkInsertHelpers.DefaultBatchRows);

            case Api.Linq:
                await using (var ctx = new SxmTransaction(TestDatabaseName))
                    return await ctx.GetTable<NullTestEntity>().BulkInsertAsync(entities, SxmBulkInsertHelpers.DefaultBatchRows);

            default:
                throw new ArgumentOutOfRangeException(nameof(api));
        }
    }

    private async Task DeleteRowsAsync(params long[] ids)
    {
        await using var ctx = new SxmTransaction(TestDatabaseName);
        await ctx.GetTable<NullTestEntity>().Where(e => ids.Contains(e.id)).DeleteAsync();
    }

    // ------------------------------------------------------------------
    // Insert path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Save_Insert_AllPropertiesNull_ShouldStoreSqlNull()
    {
        // Arrange
        var entity = new NullTestEntity();

        // Act
        await entity.SaveAsync();

        // Assert - raw storage is SQL NULL, not empty string / zero / empty blob
        entity.id.Should().BeGreaterThan(0);
        await AssertAllColumnsAreSqlNullAsync(entity.id, "should be stored as SQL NULL on insert");

        await DeleteRowsAsync(entity.id);
    }

    [Fact]
    public async Task Save_Insert_AllPropertiesNull_ShouldRoundTripAsNull()
    {
        // Arrange
        var entity = new NullTestEntity();

        // Act
        await entity.SaveAsync();

        // Assert
        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(entity.id);
        retrieved.Should().NotBeNull("entity should exist in database");
        AssertAllPropertiesAreNull(retrieved!);

        await DeleteRowsAsync(entity.id);
    }

    [Fact]
    public async Task Save_Insert_AllPropertiesPopulated_ShouldNotStoreSqlNull()
    {
        // Arrange - the inverse control for the null tests: non-null values must never become NULL
        var entity = NewPopulatedEntity();

        // Act
        await entity.SaveAsync();

        // Assert
        foreach (var column in NullableColumns)
        {
            var storageClass = await StorageClassAsync(column, entity.id);
            storageClass.Should().NotBe("null", $"column '{column}' had a non-null value");
        }

        await DeleteRowsAsync(entity.id);
    }

    [Fact]
    public async Task Save_Insert_MixedNullAndPopulated_ShouldStoreOnlyNullsAsNull()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var entity = new NullTestEntity
        {
            StringValue = "mixed",
            NullableInt = null,
            GuidBlob = guid,
            GuidText = null,
            NullableBool = false,
            DateTimeInt = null,
            DateTimeText = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        await entity.SaveAsync();

        // Assert - populated columns
        (await StorageClassAsync(nameof(NullTestEntity.StringValue), entity.id)).Should().Be("text");
        (await StorageClassAsync(nameof(NullTestEntity.GuidBlob), entity.id)).Should().Be("blob");
        (await StorageClassAsync(nameof(NullTestEntity.NullableBool), entity.id)).Should().Be("integer", "false must persist as 0, not NULL");
        (await StorageClassAsync(nameof(NullTestEntity.DateTimeText), entity.id)).Should().Be("text");

        // Assert - null columns
        (await StorageClassAsync(nameof(NullTestEntity.NullableInt), entity.id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.GuidText), entity.id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.DateTimeInt), entity.id)).Should().Be("null");

        // Assert - round trip preserves the distinction
        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(entity.id);
        retrieved.Should().NotBeNull();
        retrieved!.StringValue.Should().Be("mixed");
        retrieved.GuidBlob.Should().Be(guid);
        retrieved.NullableBool.Should().BeFalse("false is not null");
        retrieved.NullableInt.Should().BeNull();
        retrieved.GuidText.Should().BeNull();
        retrieved.DateTimeInt.Should().BeNull();

        await DeleteRowsAsync(entity.id);
    }

    // ------------------------------------------------------------------
    // Update path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Save_Update_PopulatedToNull_ShouldStoreSqlNull()
    {
        // Arrange - insert with every column populated
        var entity = NewPopulatedEntity();
        await entity.SaveAsync();
        var id = entity.id;

        (await StorageClassAsync(nameof(NullTestEntity.StringValue), id)).Should().Be("text", "precondition: the row starts populated");

        // Act - clear everything and save again (UPDATE path)
        ClearAllProperties(entity);
        await entity.SaveAsync();

        // Assert
        entity.id.Should().Be(id, "update must not insert a new row");
        await AssertAllColumnsAreSqlNullAsync(id, "should be overwritten with SQL NULL on update");

        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(id);
        retrieved.Should().NotBeNull();
        AssertAllPropertiesAreNull(retrieved!);

        await DeleteRowsAsync(id);
    }

    [Fact]
    public async Task Save_Update_NullToPopulated_ShouldReplaceSqlNull()
    {
        // Arrange - insert an all-null row
        var entity = new NullTestEntity();
        await entity.SaveAsync();
        var id = entity.id;
        await AssertAllColumnsAreSqlNullAsync(id, "precondition: the row starts null");

        // Act
        var guid = Guid.NewGuid();
        entity.StringValue = "now set";
        entity.NullableInt = 5;
        entity.GuidBlob = guid;
        entity.DateTimeInt = new DateTime(2024, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        await entity.SaveAsync();

        // Assert
        (await StorageClassAsync(nameof(NullTestEntity.StringValue), id)).Should().Be("text");
        (await StorageClassAsync(nameof(NullTestEntity.NullableInt), id)).Should().Be("integer");
        (await StorageClassAsync(nameof(NullTestEntity.GuidBlob), id)).Should().Be("blob");
        (await StorageClassAsync(nameof(NullTestEntity.DateTimeInt), id)).Should().Be("integer");

        // Columns left alone must still be NULL
        (await StorageClassAsync(nameof(NullTestEntity.GuidText), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.NullableDecimal), id)).Should().Be("null");

        await DeleteRowsAsync(id);
    }

    [Fact]
    public async Task ContextUpdate_PopulatedToNull_ShouldStoreSqlNull()
    {
        // Arrange
        var entity = NewPopulatedEntity();
        await entity.SaveAsync();
        var id = entity.id;

        // Act - update through the transaction API rather than SaveAsync
        ClearAllProperties(entity);
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.UpdateAsync(entity);
        }

        // Assert
        await AssertAllColumnsAreSqlNullAsync(id, "should be SQL NULL after SxmTransaction.UpdateAsync");

        await DeleteRowsAsync(id);
    }

    // ------------------------------------------------------------------
    // Bulk insert path
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_AllPropertiesNull_ShouldStoreSqlNull(Api api)
    {
        // Arrange
        var entities = new List<NullTestEntity> { new NullTestEntity(), new NullTestEntity() };

        // Act
        int inserted = await BulkInsertViaAsync(api, entities);

        // Assert
        inserted.Should().Be(2);
        foreach (var entity in entities)
        {
            entity.id.Should().BeGreaterThan(0);
            await AssertAllColumnsAreSqlNullAsync(entity.id, $"should be SQL NULL after bulk insert via {api}");
        }

        await DeleteRowsAsync(entities.Select(e => e.id).ToArray());
    }

    [Theory]
    [MemberData(nameof(AllApis))]
    public async Task BulkInsert_MixedNullAndPopulatedRows_ShouldStoreEachValueCorrectly(Api api)
    {
        // Arrange - a populated row and an all-null row in the same batch, so the batch parameter
        // builder has to emit DBNull for some columns and real values for others
        var populated = NewPopulatedEntity();
        var empty = new NullTestEntity();
        var entities = new List<NullTestEntity> { populated, empty };

        // Act
        await BulkInsertViaAsync(api, entities);

        // Assert
        await AssertAllColumnsAreSqlNullAsync(empty.id, $"should be SQL NULL after bulk insert via {api}");

        foreach (var column in NullableColumns)
        {
            var storageClass = await StorageClassAsync(column, populated.id);
            storageClass.Should().NotBe("null", $"column '{column}' of the populated row had a value");
        }

        await DeleteRowsAsync(populated.id, empty.id);
    }

    // ------------------------------------------------------------------
    // LINQ bulk update path
    // ------------------------------------------------------------------

    [Fact]
    public async Task LinqBulkUpdate_SetNull_ShouldStoreSqlNull()
    {
        // Arrange
        var entity = NewPopulatedEntity();
        await entity.SaveAsync();
        var id = entity.id;

        // Explicitly typed locals so the Set(setter, value) overload is selected for a null literal
        string? nullString = null;
        int? nullInt = null;
        byte[]? nullBlob = null;
        Guid? nullGuid = null;
        DateTime? nullDateTime = null;
        decimal? nullDecimal = null;

        // Act
        int updated;
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            updated = await ctx.GetTable<NullTestEntity>()
                .Where(e => e.id == id)
                .Set(e => e.StringValue, nullString)
                .Set(e => e.NullableInt, nullInt)
                .Set(e => e.BlobValue, nullBlob)
                .Set(e => e.GuidBlob, nullGuid)
                .Set(e => e.GuidText, nullGuid)
                .Set(e => e.DateTimeInt, nullDateTime)
                .Set(e => e.DateTimeText, nullDateTime)
                .Set(e => e.NullableDecimal, nullDecimal)
                .UpdateAsync();
        }

        // Assert
        updated.Should().Be(1);
        (await StorageClassAsync(nameof(NullTestEntity.StringValue), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.NullableInt), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.BlobValue), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.GuidBlob), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.GuidText), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.DateTimeInt), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.DateTimeText), id)).Should().Be("null");
        (await StorageClassAsync(nameof(NullTestEntity.NullableDecimal), id)).Should().Be("null");

        // Columns not included in the Set list must keep their values
        (await StorageClassAsync(nameof(NullTestEntity.NullableLong), id)).Should().NotBe("null");

        await DeleteRowsAsync(id);
    }

    // ------------------------------------------------------------------
    // Read path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Read_SqlNullColumns_ShouldMapToNullProperties()
    {
        // Arrange - write a populated row, then null every column with raw SQL so the read path
        // is exercised against values the library itself did not write
        var entity = NewPopulatedEntity();
        await entity.SaveAsync();
        var id = entity.id;

        var setClause = string.Join(", ", NullableColumns.Select(c => $"\"{c}\" = NULL"));
        await ExecuteNonQueryAsync($"UPDATE {TableName} SET {setClause} WHERE id = {id}");

        // Act
        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(id);

        // Assert
        retrieved.Should().NotBeNull("the row still exists, only its columns are NULL");
        retrieved!.id.Should().Be(id);
        AssertAllPropertiesAreNull(retrieved);

        await DeleteRowsAsync(id);
    }

    // ------------------------------------------------------------------
    // LINQ null filtering
    // ------------------------------------------------------------------

    [Fact]
    public async Task Linq_NullComparison_ShouldMatchSqlNullSemantics()
    {
        // Arrange
        string marker = "NullFilter_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var withValue = new NullTestEntity { StringValue = marker, NullableInt = 10 };
        var withNull = new NullTestEntity { StringValue = marker, NullableInt = null };
        await withValue.SaveAsync();
        await withNull.SaveAsync();

        // Act / Assert
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            var table = ctx.GetTable<NullTestEntity>();

            var nullRows = table.Where(e => e.StringValue == marker && e.NullableInt == null).ToList();
            nullRows.Should().HaveCount(1, "only one row has a NULL NullableInt");
            nullRows[0].id.Should().Be(withNull.id);

            var nonNullRows = table.Where(e => e.StringValue == marker && e.NullableInt != null).ToList();
            nonNullRows.Should().HaveCount(1, "only one row has a non-NULL NullableInt");
            nonNullRows[0].id.Should().Be(withValue.id);

            // SQL three-valued logic: a NULL row must not match an equality comparison against a value
            var equalityRows = table.Where(e => e.StringValue == marker && e.NullableInt == 10).ToList();
            equalityRows.Should().HaveCount(1, "NULL never equals 10");
            equalityRows[0].id.Should().Be(withValue.id);
        }

        await DeleteRowsAsync(withValue.id, withNull.id);
    }

    [Fact]
    public async Task Linq_CountWithNullPredicate_ShouldAgreeWithRawSql()
    {
        // Arrange
        string marker = "NullCount_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var entities = new List<NullTestEntity>
        {
            new NullTestEntity { StringValue = marker, GuidBlob = Guid.NewGuid() },
            new NullTestEntity { StringValue = marker, GuidBlob = null },
            new NullTestEntity { StringValue = marker, GuidBlob = null }
        };
        foreach (var entity in entities)
            await entity.SaveAsync();

        // Act
        long rawCount = await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM {TableName} WHERE StringValue = '{marker}' AND GuidBlob IS NULL");

        int linqCount;
        await using (var ctx = new SxmTransaction(TestDatabaseName))
        {
            linqCount = ctx.GetTable<NullTestEntity>()
                .Count(e => e.StringValue == marker && e.GuidBlob == null);
        }

        // Assert
        rawCount.Should().Be(2, "two rows were saved with a null Guid");
        linqCount.Should().Be((int)rawCount, "LINQ '== null' must translate to SQL 'IS NULL'");

        await DeleteRowsAsync(entities.Select(e => e.id).ToArray());
    }

    // ------------------------------------------------------------------
    // Negative cases - empty values must NOT become NULL
    // ------------------------------------------------------------------

    [Fact]
    public async Task Save_EmptyString_ShouldNotBeStoredAsSqlNull()
    {
        // Arrange
        var entity = new NullTestEntity { StringValue = string.Empty };

        // Act
        await entity.SaveAsync();

        // Assert - this is the case the mapper-only tests cannot distinguish from NULL
        (await StorageClassAsync(nameof(NullTestEntity.StringValue), entity.id))
            .Should().Be("text", "an empty string is not a null value");

        long nullCount = await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM {TableName} WHERE id = {entity.id} AND StringValue IS NULL");
        nullCount.Should().Be(0);

        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(entity.id);
        retrieved.Should().NotBeNull();
        retrieved!.StringValue.Should().NotBeNull("an empty string must round trip as an empty string");
        retrieved.StringValue.Should().BeEmpty();

        await DeleteRowsAsync(entity.id);
    }

    [Fact]
    public async Task Save_EmptyBlob_ShouldNotBeStoredAsSqlNull()
    {
        // Arrange
        var entity = new NullTestEntity { BlobValue = Array.Empty<byte>() };

        // Act
        await entity.SaveAsync();

        // Assert
        (await StorageClassAsync(nameof(NullTestEntity.BlobValue), entity.id))
            .Should().Be("blob", "an empty byte array is not a null value");

        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(entity.id);
        retrieved.Should().NotBeNull();
        retrieved!.BlobValue.Should().NotBeNull("an empty blob must round trip as an empty array");
        retrieved.BlobValue.Should().BeEmpty();

        await DeleteRowsAsync(entity.id);
    }

    [Fact]
    public async Task Save_DefaultValueTypes_ShouldNotBeStoredAsSqlNull()
    {
        // Arrange - zero, false and default time values are values, not nulls
        var entity = new NullTestEntity
        {
            NullableInt = 0,
            NullableLong = 0L,
            NullableDouble = 0d,
            NullableFloat = 0f,
            NullableBool = false,
            NullableDecimal = 0m,
            NullableULong = 0UL,
            GuidBlob = Guid.Empty,
            GuidText = Guid.Empty,
            TimeSpanInt = TimeSpan.Zero,
            TimeSpanText = TimeSpan.Zero
        };

        // Act
        await entity.SaveAsync();

        // Assert
        foreach (var column in new[]
        {
            nameof(NullTestEntity.NullableInt),
            nameof(NullTestEntity.NullableLong),
            nameof(NullTestEntity.NullableDouble),
            nameof(NullTestEntity.NullableFloat),
            nameof(NullTestEntity.NullableBool),
            nameof(NullTestEntity.NullableDecimal),
            nameof(NullTestEntity.NullableULong),
            nameof(NullTestEntity.GuidBlob),
            nameof(NullTestEntity.GuidText),
            nameof(NullTestEntity.TimeSpanInt),
            nameof(NullTestEntity.TimeSpanText)
        })
        {
            (await StorageClassAsync(column, entity.id))
                .Should().NotBe("null", $"column '{column}' holds a default value, not null");
        }

        var retrieved = await VerifyEntityExistsInDbAsync<NullTestEntity>(entity.id);
        retrieved.Should().NotBeNull();
        retrieved!.NullableInt.Should().Be(0);
        retrieved.NullableBool.Should().BeFalse();
        retrieved.GuidBlob.Should().Be(Guid.Empty);
        retrieved.GuidText.Should().Be(Guid.Empty);

        await DeleteRowsAsync(entity.id);
    }

    // ------------------------------------------------------------------
    // Schema-level nullability
    // ------------------------------------------------------------------

    [Fact]
    public async Task Schema_NullableProperties_ShouldCreateNullableColumns()
    {
        // Assert - every mapped column of the null test entity must allow NULL
        foreach (var column in NullableColumns)
        {
            (await ColumnExistsAsync(TableName, column))
                .Should().BeTrue($"column '{column}' should be mapped");
            (await IsColumnNotNullAsync(TableName, column))
                .Should().BeFalse($"column '{column}' is nullable on the entity");
        }
    }

    [Fact]
    public async Task Schema_RequiredNotNullProperties_ShouldCreateNotNullColumns()
    {
        // Assert - [RequiredNotNull] must produce a NOT NULL constraint so nulls cannot reach the column
        (await IsColumnNotNullAsync(nameof(RequiredFieldEntity), nameof(RequiredFieldEntity.RequiredName)))
            .Should().BeTrue("RequiredName is marked [RequiredNotNull]");
        (await IsColumnNotNullAsync(nameof(RequiredFieldEntity), nameof(RequiredFieldEntity.RequiredAge)))
            .Should().BeTrue("RequiredAge is marked [RequiredNotNull]");
        (await IsColumnNotNullAsync(nameof(RequiredFieldEntity), nameof(RequiredFieldEntity.OptionalField)))
            .Should().BeFalse("OptionalField has no [RequiredNotNull]");
    }

    // ------------------------------------------------------------------
    // [RequiredNotNull] behavior
    //
    // [RequiredNotNull] is a SCHEMA declaration, not a runtime null-substitution feature.
    // It emits the column as "<type> not null default <literal>" (SxmSchemaRegistration.cs ~line 421),
    // which gives SQLite's standard semantics:
    //
    //   * The DEFAULT is applied only by an INSERT that OMITS the column.
    //   * If the column IS present in the INSERT/UPDATE with a null value, the NOT NULL
    //     constraint rejects it. The DEFAULT is not consulted.
    //
    // Every entity write path (SaveAsync, SxmTransaction.UpdateAsync, and all bulk insert
    // entry points) always includes every mapped column, so saving a null property is a
    // constraint violation by design - surfaced as Microsoft.Data.Sqlite.SqliteException
    // (SQLite error 19). Any non-null value is written normally.
    //
    // The DEFAULT therefore applies to INSERT statements that omit the column, i.e. hand-written
    // SQL such as SxmSql.RunStatementAsync(...) - covered by the last test in this section.
    // ------------------------------------------------------------------

    private const string RequiredTableName = nameof(RequiredFieldEntity);

    private static string NewRequiredMarker() => "ReqNull_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private async Task<long> CountRequiredRowsAsync(string marker) =>
        await ExecuteScalarAsync<long>(
            $"SELECT COUNT(*) FROM {RequiredTableName} WHERE OptionalField = '{marker}'");

    private async Task DeleteRequiredRowsAsync(string marker) =>
        await ExecuteNonQueryAsync($"DELETE FROM {RequiredTableName} WHERE OptionalField = '{marker}'");

    [Fact]
    public async Task RequiredNotNull_Insert_NullValue_ShouldThrowNotNullConstraint_AndWriteNothing()
    {
        // Arrange
        string marker = NewRequiredMarker();
        var entity = new RequiredFieldEntity { RequiredName = null, OptionalField = marker };

        // Act
        Func<Task> act = () => entity.SaveAsync();

        // Assert - the null is rejected by the schema constraint, NOT silently replaced by DefaultValue
        (await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>(
                "[RequiredNotNull] is enforced by a NOT NULL column constraint"))
            .WithMessage("*NOT NULL constraint failed: RequiredFieldEntity.RequiredName*");

        // Assert - the failed insert left nothing behind
        (await CountRequiredRowsAsync(marker)).Should().Be(0, "a rejected insert must not write a row");
        entity.id.Should().Be(0, "id must not be populated when the insert fails");

        await DeleteRequiredRowsAsync(marker);
    }

    [Fact]
    public async Task RequiredNotNull_Update_ToNull_ShouldThrow_AndLeaveExistingValueIntact()
    {
        // Arrange - a valid row first
        string marker = NewRequiredMarker();
        var entity = new RequiredFieldEntity { RequiredName = "original", RequiredAge = 5, OptionalField = marker };
        await entity.SaveAsync();
        var id = entity.id;
        id.Should().BeGreaterThan(0);

        // Act - null out the required property and save again (UPDATE path)
        entity.RequiredName = null;
        Func<Task> act = () => entity.SaveAsync();

        // Assert
        (await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>())
            .WithMessage("*NOT NULL constraint failed: RequiredFieldEntity.RequiredName*");

        // Assert - the rejected update must not have partially modified the row
        var storedName = await ExecuteScalarAsync<string>(
            $"SELECT RequiredName FROM {RequiredTableName} WHERE id = {id}");
        storedName.Should().Be("original", "the failed update must leave the existing value untouched");

        (await ExecuteScalarAsync<string>($"SELECT typeof(RequiredName) FROM {RequiredTableName} WHERE id = {id}"))
            .Should().Be("text", "the column must never hold SQL NULL");

        await DeleteRequiredRowsAsync(marker);
    }

    [Fact]
    public async Task RequiredNotNull_BulkInsert_NullValue_ShouldThrow_AndWriteNothing()
    {
        // Arrange - a valid row and an invalid row in the same batch
        string marker = NewRequiredMarker();
        var entities = new List<RequiredFieldEntity>
        {
            new RequiredFieldEntity { RequiredName = "valid", OptionalField = marker },
            new RequiredFieldEntity { RequiredName = null, OptionalField = marker }
        };

        // Act
        Func<Task> act = () => SxmSql.BulkInsertAsync(entities, SxmBulkInsertHelpers.DefaultBatchRows, TestDatabaseName);

        // Assert
        (await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>())
            .WithMessage("*NOT NULL constraint failed: RequiredFieldEntity.RequiredName*");

        // Assert - the batch is atomic: the valid row must not survive the failed batch
        (await CountRequiredRowsAsync(marker)).Should().Be(0,
            "a bulk insert that violates a NOT NULL constraint must roll back the whole batch");

        await DeleteRequiredRowsAsync(marker);
    }

    [Fact]
    public async Task RequiredNotNull_LinqBulkUpdate_SetNull_ShouldThrow_AndLeaveExistingValueIntact()
    {
        // Arrange
        string marker = NewRequiredMarker();
        var entity = new RequiredFieldEntity { RequiredName = "original", OptionalField = marker };
        await entity.SaveAsync();
        var id = entity.id;

        string? nullName = null;

        // Act
        Func<Task> act = async () =>
        {
            await using var ctx = new SxmTransaction(TestDatabaseName);
            await ctx.GetTable<RequiredFieldEntity>()
                .Where(e => e.id == id)
                .Set(e => e.RequiredName, nullName)
                .UpdateAsync();
        };

        // Assert
        (await act.Should().ThrowAsync<Microsoft.Data.Sqlite.SqliteException>())
            .WithMessage("*NOT NULL constraint failed: RequiredFieldEntity.RequiredName*");

        var storedName = await ExecuteScalarAsync<string>(
            $"SELECT RequiredName FROM {RequiredTableName} WHERE id = {id}");
        storedName.Should().Be("original", "the failed bulk update must leave the existing value untouched");

        await DeleteRequiredRowsAsync(marker);
    }

    [Fact]
    public async Task RequiredNotNull_ValidValue_ShouldSaveNormally()
    {
        // Arrange - control case proving the constraint only rejects nulls
        string marker = NewRequiredMarker();
        var entity = new RequiredFieldEntity { RequiredName = "provided", RequiredAge = 21, OptionalField = marker };

        // Act
        await entity.SaveAsync();

        // Assert
        entity.id.Should().BeGreaterThan(0);
        (await ExecuteScalarAsync<string>($"SELECT RequiredName FROM {RequiredTableName} WHERE id = {entity.id}"))
            .Should().Be("provided");
        (await ExecuteScalarAsync<long>($"SELECT RequiredAge FROM {RequiredTableName} WHERE id = {entity.id}"))
            .Should().Be(21);

        await DeleteRequiredRowsAsync(marker);
    }

    [Fact]
    public async Task RequiredNotNull_DefaultValue_AppliesOnlyWhenColumnIsOmittedFromInsert()
    {
        // Arrange - this documents the real scope of RequiredNotNullAttribute.DefaultValue.
        // It is emitted as a SQL column DEFAULT, so it fires only for an INSERT that omits the
        // column - i.e. a hand-written statement such as SxmSql.RunStatementAsync(...). Every
        // library-generated write path (entity SaveAsync, transaction updates, bulk insert) binds
        // all mapped columns, so the default is NOT a null-substitution mechanism for entity saves
        // (see the throwing tests above).
        string marker = NewRequiredMarker();

        // Act - raw insert that omits both required columns
        await ExecuteNonQueryAsync(
            $"INSERT INTO {RequiredTableName} (OptionalField) VALUES ('{marker}')");

        // Assert - the schema default backfilled the omitted columns
        (await ExecuteScalarAsync<string>(
                $"SELECT RequiredName FROM {RequiredTableName} WHERE OptionalField = '{marker}'"))
            .Should().Be("Default Name", "the column DEFAULT from [RequiredNotNull] applies when omitted");
        (await ExecuteScalarAsync<long>(
                $"SELECT RequiredAge FROM {RequiredTableName} WHERE OptionalField = '{marker}'"))
            .Should().Be(42, "the column DEFAULT from [RequiredNotNull] applies when omitted");

        await DeleteRequiredRowsAsync(marker);
    }
}
