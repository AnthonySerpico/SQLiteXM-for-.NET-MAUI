using FluentAssertions;
using System.Globalization;

namespace SQLiteXM.Tests;

/// <summary>
/// Verifies the storage classes and exact on-disk encodings documented in supported-data-types.md.
/// <para>
/// Round-trip tests prove a value survives save and load; these tests read the raw SQLite column
/// with <c>typeof()</c> and <c>hex()</c> to prove the documented storage class and encoding.
/// </para>
/// </summary>
[Collection("Sequential")]
public class SupportedDataTypesStorageTests : TestBase
{
    private const string AllTypesTable = nameof(AllTypesEntity);
    private const string TextTable = nameof(TimeTypeTextEntity);

    public SupportedDataTypesStorageTests()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();
    }

    private static AllTypesEntity NewAllTypesEntity() => new AllTypesEntity
    {
        SByteValue = -5,
        ByteValue = 200,
        ShortValue = -1234,
        UShortValue = 60000,
        IntValue = -123456,
        UIntValue = 4000000000,
        LongValue = long.MinValue,
        ULongValue = ulong.MaxValue,
        DecimalValue = 1234.5m,
        FloatValue = 1.5f,
        DoubleValue = 3.25,
        BoolValue = true,
        StringValue = "text",
        GuidValue = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"),
        DateTimeValue = new DateTime(2024, 5, 17, 13, 45, 30, DateTimeKind.Utc),
        DateTimeOffsetValue = new DateTimeOffset(2024, 5, 17, 9, 45, 30, TimeSpan.FromHours(-4)),
        TimeSpanValue = TimeSpan.FromMinutes(90),
        DateOnlyValue = new DateOnly(2024, 12, 25),
        TimeOnlyValue = new TimeOnly(14, 30, 15),
        BlobValue = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }
    };

    private async Task<string> StorageClassAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<string>($"SELECT typeof(\"{column}\") FROM {table} WHERE id = {id}");
    }

    private async Task<string> RawTextAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<string>($"SELECT \"{column}\" FROM {table} WHERE id = {id}");
    }

    private async Task<long> RawIntegerAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<long>($"SELECT \"{column}\" FROM {table} WHERE id = {id}");
    }

    private async Task<string> RawHexAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<string>($"SELECT hex(\"{column}\") FROM {table} WHERE id = {id}");
    }

    private async Task<long> SaveAllTypesAsync(AllTypesEntity entity)
    {
        await entity.SaveAsync();
        return entity.id;
    }

    // ------------------------------------------------------------------
    // Default storage classes
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(AllTypesEntity.SByteValue), "integer")]
    [InlineData(nameof(AllTypesEntity.ByteValue), "integer")]
    [InlineData(nameof(AllTypesEntity.ShortValue), "integer")]
    [InlineData(nameof(AllTypesEntity.UShortValue), "integer")]
    [InlineData(nameof(AllTypesEntity.IntValue), "integer")]
    [InlineData(nameof(AllTypesEntity.UIntValue), "integer")]
    [InlineData(nameof(AllTypesEntity.LongValue), "integer")]
    [InlineData(nameof(AllTypesEntity.BoolValue), "integer")]
    [InlineData(nameof(AllTypesEntity.DateTimeValue), "integer")]
    [InlineData(nameof(AllTypesEntity.DateTimeOffsetValue), "integer")]
    [InlineData(nameof(AllTypesEntity.TimeSpanValue), "integer")]
    [InlineData(nameof(AllTypesEntity.DateOnlyValue), "integer")]
    [InlineData(nameof(AllTypesEntity.TimeOnlyValue), "integer")]
    [InlineData(nameof(AllTypesEntity.ULongValue), "text")]
    [InlineData(nameof(AllTypesEntity.DecimalValue), "text")]
    [InlineData(nameof(AllTypesEntity.StringValue), "text")]
    [InlineData(nameof(AllTypesEntity.FloatValue), "real")]
    [InlineData(nameof(AllTypesEntity.DoubleValue), "real")]
    [InlineData(nameof(AllTypesEntity.GuidValue), "blob")]
    [InlineData(nameof(AllTypesEntity.BlobValue), "blob")]
    public async Task DefaultMapping_NonNullValue_StoredAsDocumentedStorageClass(string column, string expectedStorageClass)
    {
        long id = await SaveAllTypesAsync(NewAllTypesEntity());

        string actual = await StorageClassAsync(AllTypesTable, column, id);

        actual.Should().Be(expectedStorageClass);
    }

    [Fact]
    public async Task Integral_Values_StoredAsExactIntegers()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.SByteValue), id)).Should().Be(-5);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.ByteValue), id)).Should().Be(200);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.ShortValue), id)).Should().Be(-1234);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.UShortValue), id)).Should().Be(60000);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.IntValue), id)).Should().Be(-123456);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.UIntValue), id)).Should().Be(4000000000L);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.LongValue), id)).Should().Be(long.MinValue);
        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.BoolValue), id)).Should().Be(1);
    }

    [Fact]
    public async Task Ulong_And_Decimal_StoredAsExactText()
    {
        long id = await SaveAllTypesAsync(NewAllTypesEntity());

        (await RawTextAsync(AllTypesTable, nameof(AllTypesEntity.ULongValue), id))
            .Should().Be(ulong.MaxValue.ToString("D20", CultureInfo.InvariantCulture));
        (await RawTextAsync(AllTypesTable, nameof(AllTypesEntity.DecimalValue), id))
            .Should().Be("1234.5");
    }

    [Fact]
    public async Task Guid_DefaultBlob_StoredAsNativeGuidBytes()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), id))
            .Should().Be(Convert.ToHexString(entity.GuidValue.ToByteArray()));
    }

    [Fact]
    public async Task ByteArray_StoredAsExactBlobBytes()
    {
        long id = await SaveAllTypesAsync(NewAllTypesEntity());

        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.BlobValue), id)).Should().Be("DEADBEEF");
    }

    [Fact]
    public async Task Float_And_Double_StoredAsRealValues()
    {
        long id = await SaveAllTypesAsync(NewAllTypesEntity());

        double floatRaw = await ExecuteScalarAsync<double>($"SELECT FloatValue FROM {AllTypesTable} WHERE id = {id}");
        double doubleRaw = await ExecuteScalarAsync<double>($"SELECT DoubleValue FROM {AllTypesTable} WHERE id = {id}");

        floatRaw.Should().Be(1.5);
        doubleRaw.Should().Be(3.25);
    }

    // ------------------------------------------------------------------
    // Default date/time encodings (INTEGER)
    // ------------------------------------------------------------------

    [Fact]
    public async Task DateTime_DefaultEncoding_IsUtcTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.DateTimeValue), id))
            .Should().Be(entity.DateTimeValue.Ticks);
    }

    [Fact]
    public async Task DateTimeOffset_DefaultEncoding_IsUtcTicksAndOffsetIsNotStored()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.DateTimeOffsetValue), id))
            .Should().Be(entity.DateTimeOffsetValue.UtcTicks);
    }

    [Fact]
    public async Task DateTimeOffset_SameInstantDifferentOffsets_ProduceIdenticalIntegerStorage()
    {
        var utcForm = new DateTimeOffset(2024, 5, 17, 13, 45, 30, TimeSpan.Zero);
        var localForm = new DateTimeOffset(2024, 5, 17, 9, 45, 30, TimeSpan.FromHours(-4));

        AllTypesEntity utcEntity = NewAllTypesEntity();
        utcEntity.DateTimeOffsetValue = utcForm;
        long utcId = await SaveAllTypesAsync(utcEntity);

        AllTypesEntity localEntity = NewAllTypesEntity();
        localEntity.DateTimeOffsetValue = localForm;
        long localId = await SaveAllTypesAsync(localEntity);

        long utcRaw = await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.DateTimeOffsetValue), utcId);
        long localRaw = await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.DateTimeOffsetValue), localId);

        utcRaw.Should().Be(localRaw);
        utcRaw.Should().Be(utcForm.UtcTicks);
    }

    [Fact]
    public async Task DateOnly_DefaultEncoding_IsDaysSinceUnixEpoch()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        long expectedDays = entity.DateOnlyValue.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.DateOnlyValue), id)).Should().Be(expectedDays);
    }

    [Fact]
    public async Task TimeOnly_DefaultEncoding_IsTotalTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeOnlyValue), id))
            .Should().Be(entity.TimeOnlyValue.ToTimeSpan().Ticks);
    }

    [Fact]
    public async Task TimeSpan_DefaultEncoding_IsTotalTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeSpanValue), id))
            .Should().Be(entity.TimeSpanValue.Ticks);
    }

    [Fact]
    public async Task TimeSpan_SubMillisecondTicks_AreStoredExactly()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeSpanValue = TimeSpan.FromTicks(15_000 + 9_000);
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeSpanValue), id)).Should().Be(24_000);
    }

    [Fact]
    public async Task TimeSpan_SubMillisecondTicks_RoundTripExactly()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeSpanValue = TimeSpan.FromTicks(15_000);
        long id = await SaveAllTypesAsync(entity);

        AllTypesEntity? loaded = await VerifyEntityExistsInDbAsync<AllTypesEntity>(id);

        loaded!.TimeSpanValue.Should().Be(TimeSpan.FromTicks(15_000));
    }

    [Fact]
    public async Task TimeOnly_SubMillisecondTicks_AreStoredExactly()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeOnlyValue = TimeOnly.FromTimeSpan(TimeSpan.FromTicks(TimeSpan.FromSeconds(1).Ticks + 19_000));
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeOnlyValue), id)).Should().Be(10_019_000);
    }

    [Fact]
    public async Task TimeSpan_Negative_IsStoredExactly()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeSpanValue = TimeSpan.FromTicks(-15_000);
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeSpanValue), id)).Should().Be(-15_000);
    }

    [Fact]
    public async Task TimeSpan_Negative_RoundTripsWithTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeSpanValue = TimeSpan.FromMinutes(-90);
        long id = await SaveAllTypesAsync(entity);

        AllTypesEntity? loaded = await VerifyEntityExistsInDbAsync<AllTypesEntity>(id);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeSpanValue), id)).Should().Be(-54_000_000_000);
        loaded!.TimeSpanValue.Should().Be(TimeSpan.FromMinutes(-90));
        loaded!.TimeSpanValue.Should().Be(TimeSpan.FromMinutes(-90));
    }

    [Fact]
    public async Task TimeSpan_DefaultEncoding_LiteralTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeSpanValue = TimeSpan.FromMinutes(90);
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeSpanValue), id)).Should().Be(54_000_000_000);
    }

    [Fact]
    public async Task TimeOnly_DefaultEncoding_LiteralTicks()
    {
        AllTypesEntity entity = NewAllTypesEntity();
        entity.TimeOnlyValue = new TimeOnly(14, 30, 15);
        long id = await SaveAllTypesAsync(entity);

        (await RawIntegerAsync(AllTypesTable, nameof(AllTypesEntity.TimeOnlyValue), id)).Should().Be(522_150_000_000);
    }

    // ------------------------------------------------------------------
    // TEXT overrides
    // ------------------------------------------------------------------

    private static TimeTypeTextEntity NewTextEntity() => new TimeTypeTextEntity
    {
        DateTimeAsText = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        DateTimeOffsetAsText = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
        TimeSpanAsText = TimeSpan.FromHours(2),
        DateOnlyAsText = new DateOnly(2023, 6, 15),
        TimeOnlyAsText = new TimeOnly(8, 15, 30),
        GuidAsText = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")
    };

    private async Task<long> SaveTextEntityAsync(TimeTypeTextEntity entity)
    {
        await entity.SaveAsync();
        return entity.id;
    }

    [Theory]
    [InlineData(nameof(TimeTypeTextEntity.DateTimeAsText))]
    [InlineData(nameof(TimeTypeTextEntity.DateTimeOffsetAsText))]
    [InlineData(nameof(TimeTypeTextEntity.TimeSpanAsText))]
    [InlineData(nameof(TimeTypeTextEntity.DateOnlyAsText))]
    [InlineData(nameof(TimeTypeTextEntity.TimeOnlyAsText))]
    [InlineData(nameof(TimeTypeTextEntity.GuidAsText))]
    public async Task TextOverride_NonNullValue_StoredAsText(string column)
    {
        long id = await SaveTextEntityAsync(NewTextEntity());

        (await StorageClassAsync(TextTable, column, id)).Should().Be("text");
    }

    [Fact]
    public async Task DateTime_TextOverride_IsIso8601RoundTripFormat()
    {
        TimeTypeTextEntity entity = NewTextEntity();
        long id = await SaveTextEntityAsync(entity);

        (await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.DateTimeAsText), id))
            .Should().Be(entity.DateTimeAsText.ToString("o", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task DateTimeOffset_TextOverride_PreservesOriginalOffset()
    {
        TimeTypeTextEntity entity = NewTextEntity();
        long id = await SaveTextEntityAsync(entity);

        string raw = await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.DateTimeOffsetAsText), id);

        raw.Should().Be(entity.DateTimeOffsetAsText.ToString("o", CultureInfo.InvariantCulture));
        raw.Should().EndWith("+02:00");
    }

    [Fact]
    public async Task TimeSpan_TextOverride_IsConstantFormat()
    {
        TimeTypeTextEntity entity = NewTextEntity();
        long id = await SaveTextEntityAsync(entity);

        (await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.TimeSpanAsText), id))
            .Should().Be(entity.TimeSpanAsText.ToString("c", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task DateOnly_TextOverride_IsYyyyMmDd()
    {
        long id = await SaveTextEntityAsync(NewTextEntity());

        (await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.DateOnlyAsText), id)).Should().Be("2023-06-15");
    }

    [Fact]
    public async Task TimeOnly_TextOverride_IsHhMmSsFffffff()
    {
        long id = await SaveTextEntityAsync(NewTextEntity());

        (await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.TimeOnlyAsText), id)).Should().Be("08:15:30.0000000");
    }

    [Fact]
    public async Task Guid_TextOverride_IsCanonicalGuidString()
    {
        long id = await SaveTextEntityAsync(NewTextEntity());

        (await RawTextAsync(TextTable, nameof(TimeTypeTextEntity.GuidAsText), id))
            .Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
    }
}
