using FluentAssertions;
using SQLiteXM;

namespace SQLiteXM.Tests;

/// <summary>
/// Verifies that Guid values are written and read consistently by every SQLiteXM path.
/// <para>
/// The default storage is a 16-byte BLOB in native .NET order (<see cref="Guid.ToByteArray"/>).
/// The values below are deliberately asymmetric so any byte swapping of the first three Guid
/// components (bytes 0-3, 4-5 and 6-7) produces a different Guid and fails the assertions.
/// </para>
/// </summary>
[Collection("Sequential")]
public class GuidConsistencyTests : TestBase
{
    private const string AllTypesTable = nameof(AllTypesEntity);
    private const string TextTable = nameof(TimeTypeTextEntity);

    private static readonly Guid Asymmetric = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
    private static readonly Guid AllOnes = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid Sample = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    public static IEnumerable<object[]> GuidValues() => new List<object[]>
    {
        new object[] { Asymmetric.ToString() },
        new object[] { Sample.ToString() },
        new object[] { Guid.Empty.ToString() },
        new object[] { AllOnes.ToString() }
    };

    public GuidConsistencyTests()
    {
        InitializeSqliteXMAsync().GetAwaiter().GetResult();
    }

    private static AllTypesEntity NewEntity(Guid value, Guid? nullable) => new AllTypesEntity
    {
        StringValue = "guid-" + Guid.NewGuid().ToString("N"),
        GuidValue = value,
        NullableGuid = nullable,
        DateTimeValue = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DateTimeOffsetValue = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DateOnlyValue = new DateOnly(2024, 1, 1),
        TimeOnlyValue = new TimeOnly(12, 0)
    };

    private async Task<string> RawHexAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<string>($"SELECT hex(\"{column}\") FROM {table} WHERE id = {id}");
    }

    private async Task<string> StorageClassAsync(string table, string column, long id)
    {
        return await ExecuteScalarAsync<string>($"SELECT typeof(\"{column}\") FROM {table} WHERE id = {id}");
    }

    private static async Task<AllTypesEntity> ReadDirectAsync(long id)
    {
        List<AllTypesEntity> rows = await SxmSql.RunStatementAsync<AllTypesEntity>(
            $"SELECT * FROM {AllTypesTable} WHERE id = {id}", TestDatabaseName);
        rows.Should().HaveCount(1);
        return rows[0];
    }

    private async Task<AllTypesEntity> ReadLinqAsync(long id)
    {
        AllTypesEntity? entity = await VerifyEntityExistsInDbAsync<AllTypesEntity>(id);
        entity.Should().NotBeNull();
        return entity!;
    }

    // ------------------------------------------------------------------
    // Raw encoding
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GuidValues))]
    public async Task DirectSave_StoresNative16ByteBlob(string text)
    {
        Guid value = Guid.Parse(text);
        AllTypesEntity entity = NewEntity(value, value);
        await entity.SaveAsync();

        (await StorageClassAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), entity.id)).Should().Be("blob");
        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), entity.id))
            .Should().Be(Convert.ToHexString(value.ToByteArray()));
        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.NullableGuid), entity.id))
            .Should().Be(Convert.ToHexString(value.ToByteArray()));
        (await ExecuteScalarAsync<long>($"SELECT length(\"{nameof(AllTypesEntity.GuidValue)}\") FROM {AllTypesTable} WHERE id = {entity.id}"))
            .Should().Be(16);
    }

    [Fact]
    public async Task AsymmetricGuid_NativeBytes_DifferFromRfc4122Bytes()
    {
        // Guards the test data itself: if native and big-endian layouts were equal,
        // the round-trip tests could not detect a byte-order mistake.
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        string hex = await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), entity.id);

        hex.Should().Be("33221100554477668899AABBCCDDEEFF");
        hex.Should().NotBe(Asymmetric.ToString("N").ToUpperInvariant());
    }

    [Fact]
    public async Task LinqInsert_StoresNative16ByteBlob()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, Asymmetric);
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(entity);
        }

        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), entity.id))
            .Should().Be(Convert.ToHexString(Asymmetric.ToByteArray()));
        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.NullableGuid), entity.id))
            .Should().Be(Convert.ToHexString(Asymmetric.ToByteArray()));
    }

    // ------------------------------------------------------------------
    // Cross-path round trips
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(GuidValues))]
    public async Task DirectSave_ReadThroughDirectAndLinq_ReturnsSameGuid(string text)
    {
        Guid value = Guid.Parse(text);
        AllTypesEntity entity = NewEntity(value, value);
        await entity.SaveAsync();

        AllTypesEntity direct = await ReadDirectAsync(entity.id);
        AllTypesEntity linq = await ReadLinqAsync(entity.id);

        direct.GuidValue.Should().Be(value);
        direct.NullableGuid.Should().Be(value);
        linq.GuidValue.Should().Be(value);
        linq.NullableGuid.Should().Be(value);
        direct.GuidValue.Should().Be(linq.GuidValue);
    }

    [Theory]
    [MemberData(nameof(GuidValues))]
    public async Task LinqInsert_ReadThroughDirectAndLinq_ReturnsSameGuid(string text)
    {
        Guid value = Guid.Parse(text);
        AllTypesEntity entity = NewEntity(value, value);
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(entity);
        }

        AllTypesEntity direct = await ReadDirectAsync(entity.id);
        AllTypesEntity linq = await ReadLinqAsync(entity.id);

        direct.GuidValue.Should().Be(value);
        direct.NullableGuid.Should().Be(value);
        linq.GuidValue.Should().Be(value);
        linq.NullableGuid.Should().Be(value);
    }

    [Fact]
    public async Task RawSqlNativeBlob_ReadThroughDirectAndLinq_ReturnsSameGuid()
    {
        AllTypesEntity entity = NewEntity(Guid.Empty, null);
        await entity.SaveAsync();

        string hex = Convert.ToHexString(Asymmetric.ToByteArray());
        await ExecuteNonQueryAsync(
            $"UPDATE {AllTypesTable} SET \"{nameof(AllTypesEntity.GuidValue)}\" = X'{hex}', \"{nameof(AllTypesEntity.NullableGuid)}\" = X'{hex}' WHERE id = {entity.id}");

        AllTypesEntity direct = await ReadDirectAsync(entity.id);
        AllTypesEntity linq = await ReadLinqAsync(entity.id);

        direct.GuidValue.Should().Be(Asymmetric);
        direct.NullableGuid.Should().Be(Asymmetric);
        linq.GuidValue.Should().Be(Asymmetric);
        linq.NullableGuid.Should().Be(Asymmetric);
    }

    [Fact]
    public async Task RawDictionaryResult_ReturnsNativeBytes()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        List<Dictionary<string, object?>> rows = await SxmSql.RunStatementAsync(
            $"SELECT \"{nameof(AllTypesEntity.GuidValue)}\" FROM {AllTypesTable} WHERE id = {entity.id}", TestDatabaseName);

        rows.Should().HaveCount(1);
        rows[0][nameof(AllTypesEntity.GuidValue)].Should().BeOfType<byte[]>()
            .Which.Should().Equal(Asymmetric.ToByteArray());
    }

    // ------------------------------------------------------------------
    // Nullable Guid
    // ------------------------------------------------------------------

    [Fact]
    public async Task NullableGuid_Null_StoredAsSqlNull_AndReadAsNullByBothPaths()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        (await StorageClassAsync(AllTypesTable, nameof(AllTypesEntity.NullableGuid), entity.id)).Should().Be("null");
        (await ReadDirectAsync(entity.id)).NullableGuid.Should().BeNull();
        (await ReadLinqAsync(entity.id)).NullableGuid.Should().BeNull();
    }

    [Fact]
    public async Task NullableGuid_LinqInsertNull_ReadAsNullByBothPaths()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.InsertAsync(entity);
        }

        (await StorageClassAsync(AllTypesTable, nameof(AllTypesEntity.NullableGuid), entity.id)).Should().Be("null");
        (await ReadDirectAsync(entity.id)).NullableGuid.Should().BeNull();
        (await ReadLinqAsync(entity.id)).NullableGuid.Should().BeNull();
    }

    [Fact]
    public async Task NullableGuid_UpdateValueToNullAndBack_IsConsistent()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, Asymmetric);
        await entity.SaveAsync();

        entity.NullableGuid = null;
        await entity.SaveAsync();
        (await ReadDirectAsync(entity.id)).NullableGuid.Should().BeNull();
        (await ReadLinqAsync(entity.id)).NullableGuid.Should().BeNull();

        entity.NullableGuid = Sample;
        await entity.SaveAsync();
        (await ReadDirectAsync(entity.id)).NullableGuid.Should().Be(Sample);
        (await ReadLinqAsync(entity.id)).NullableGuid.Should().Be(Sample);
    }

    // ------------------------------------------------------------------
    // Updates
    // ------------------------------------------------------------------

    [Fact]
    public async Task DirectUpdate_ChangesGuid_ReadBackByBothPaths()
    {
        AllTypesEntity entity = NewEntity(Sample, null);
        await entity.SaveAsync();

        entity.GuidValue = Asymmetric;
        await entity.SaveAsync();

        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), entity.id))
            .Should().Be(Convert.ToHexString(Asymmetric.ToByteArray()));
        (await ReadDirectAsync(entity.id)).GuidValue.Should().Be(Asymmetric);
        (await ReadLinqAsync(entity.id)).GuidValue.Should().Be(Asymmetric);
    }

    [Fact]
    public async Task LinqSetUpdate_ChangesGuid_ReadBackByBothPaths()
    {
        AllTypesEntity entity = NewEntity(Sample, null);
        await entity.SaveAsync();
        long id = entity.id;

        await using (SxmTransaction ctx = new SxmTransaction(TestDatabaseName))
        {
            await ctx.GetTable<AllTypesEntity>()
                .Where(e => e.id == id)
                .Set(e => e.GuidValue, Asymmetric)
                .Set(e => e.NullableGuid, (Guid?)Asymmetric)
                .UpdateAsync();
        }

        (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), id))
            .Should().Be(Convert.ToHexString(Asymmetric.ToByteArray()));
        AllTypesEntity direct = await ReadDirectAsync(id);
        direct.GuidValue.Should().Be(Asymmetric);
        direct.NullableGuid.Should().Be(Asymmetric);
        (await ReadLinqAsync(id)).GuidValue.Should().Be(Asymmetric);
    }

    // ------------------------------------------------------------------
    // Raw SQL parameters (RunStatementAsync passes values to SQLite as-is;
    // callers must supply Guid.ToByteArray() for BLOB or Guid.ToString() for TEXT)
    // ------------------------------------------------------------------

    private static async Task<List<AllTypesEntity>> SelectByGuidParameterAsync(long id, object value)
    {
        Dictionary<string, object?> parameters = new Dictionary<string, object?>
        {
            ["Id"] = id,
            ["G"] = value
        };

        return await SxmSql.RunStatementAsync<AllTypesEntity>(
            $"SELECT * FROM {AllTypesTable} WHERE id = @Id AND \"{nameof(AllTypesEntity.GuidValue)}\" = @G",
            parameters, TestDatabaseName);
    }

    [Fact]
    public async Task RawSqlParameter_NativeGuidBytes_MatchesStoredRow()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        List<AllTypesEntity> rows = await SelectByGuidParameterAsync(entity.id, Asymmetric.ToByteArray());

        rows.Should().HaveCount(1);
        rows[0].GuidValue.Should().Be(Asymmetric);
    }

    [Fact]
    public async Task RawSqlParameter_BigEndianGuidBytes_DoesNotMatch()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        byte[] bigEndian = Convert.FromHexString(Asymmetric.ToString("N"));
        bigEndian.Should().NotEqual(Asymmetric.ToByteArray());

        List<AllTypesEntity> rows = await SelectByGuidParameterAsync(entity.id, bigEndian);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RawSqlParameter_UnconvertedGuid_IsNotConvertedAndDoesNotMatchBlob()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        List<AllTypesEntity> rows = await SelectByGuidParameterAsync(entity.id, Asymmetric);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RawSqlInsert_NativeGuidBytesParameter_ReadBackByBothPaths()
    {
        AllTypesEntity entity = NewEntity(Guid.Empty, null);
        await entity.SaveAsync();

        Dictionary<string, object?> parameters = new Dictionary<string, object?>
        {
            ["Id"] = entity.id,
            ["G"] = Asymmetric.ToByteArray(),
            ["N"] = Sample.ToByteArray()
        };

        await SxmSql.RunStatementAsync(
            $"UPDATE {AllTypesTable} SET \"{nameof(AllTypesEntity.GuidValue)}\" = @G, \"{nameof(AllTypesEntity.NullableGuid)}\" = @N WHERE id = @Id",
            parameters, TestDatabaseName);

        AllTypesEntity direct = await ReadDirectAsync(entity.id);
        AllTypesEntity linq = await ReadLinqAsync(entity.id);

        direct.GuidValue.Should().Be(Asymmetric);
        direct.NullableGuid.Should().Be(Sample);
        linq.GuidValue.Should().Be(Asymmetric);
        linq.NullableGuid.Should().Be(Sample);
    }

    [Fact]
    public async Task RawSqlParameter_GuidString_MatchesTextOverrideColumn()
    {
        TimeTypeTextEntity entity = new TimeTypeTextEntity
        {
            DateTimeAsText = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            DateTimeOffsetAsText = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero),
            TimeSpanAsText = TimeSpan.FromHours(1),
            DateOnlyAsText = new DateOnly(2023, 6, 15),
            TimeOnlyAsText = new TimeOnly(8, 15, 30),
            GuidAsText = Asymmetric
        };
        await entity.SaveAsync();

        Dictionary<string, object?> parameters = new Dictionary<string, object?>
        {
            ["Id"] = entity.id,
            ["G"] = Asymmetric.ToString()
        };

        List<TimeTypeTextEntity> rows = await SxmSql.RunStatementAsync<TimeTypeTextEntity>(
            $"SELECT * FROM {TextTable} WHERE id = @Id AND \"{nameof(TimeTypeTextEntity.GuidAsText)}\" = @G",
            parameters, TestDatabaseName);

        rows.Should().HaveCount(1);
        rows[0].GuidAsText.Should().Be(Asymmetric);
    }

    [Fact]
    public async Task LinqPredicate_Guid_MatchesRowWrittenByDirectPath()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, Asymmetric);
        await entity.SaveAsync();
        long id = entity.id;

        await using SxmTransaction ctx = new SxmTransaction(TestDatabaseName);
        List<AllTypesEntity> byValue = ctx.GetTable<AllTypesEntity>()
            .Where(e => e.id == id && e.GuidValue == Asymmetric)
            .ToList();
        List<AllTypesEntity> byNullable = ctx.GetTable<AllTypesEntity>()
            .Where(e => e.id == id && e.NullableGuid == Asymmetric)
            .ToList();

        byValue.Should().HaveCount(1);
        byNullable.Should().HaveCount(1);
    }

    // ------------------------------------------------------------------
    // synchId
    // ------------------------------------------------------------------

    [Fact]
    public async Task SynchId_AfterDirectSave_MatchesStoredBytesAndBothReadPaths()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        entity.synchId.Should().NotBeNull();
        Guid synchId = entity.synchId!.Value;

        (await RawHexAsync(AllTypesTable, nameof(SxmEntity.synchId), entity.id))
            .Should().Be(Convert.ToHexString(synchId.ToByteArray()));
        (await ReadDirectAsync(entity.id)).synchId.Should().Be(synchId);
        (await ReadLinqAsync(entity.id)).synchId.Should().Be(synchId);
    }

    [Fact]
    public async Task SynchId_UnchangedByUpdate()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();
        Guid? synchId = entity.synchId;

        entity.GuidValue = Sample;
        await entity.SaveAsync();

        entity.synchId.Should().Be(synchId);
        (await ReadDirectAsync(entity.id)).synchId.Should().Be(synchId);
        (await ReadLinqAsync(entity.id)).synchId.Should().Be(synchId);
    }

    // ------------------------------------------------------------------
    // Bulk insert
    // ------------------------------------------------------------------

    [Fact]
    public async Task BulkInsert_GuidsAndSynchIds_ReadBackByBothPaths()
    {
        List<AllTypesEntity> entities = new List<AllTypesEntity>
        {
            NewEntity(Asymmetric, Asymmetric),
            NewEntity(Guid.Empty, null),
            NewEntity(AllOnes, Sample)
        };

        await SxmSql.BulkInsertAsync(entities, databaseName: TestDatabaseName);

        foreach (AllTypesEntity expected in entities)
        {
            expected.synchId.Should().NotBeNull();
            (await RawHexAsync(AllTypesTable, nameof(AllTypesEntity.GuidValue), expected.id))
                .Should().Be(Convert.ToHexString(expected.GuidValue.ToByteArray()));

            AllTypesEntity direct = await ReadDirectAsync(expected.id);
            AllTypesEntity linq = await ReadLinqAsync(expected.id);

            direct.GuidValue.Should().Be(expected.GuidValue);
            direct.NullableGuid.Should().Be(expected.NullableGuid);
            direct.synchId.Should().Be(expected.synchId);
            linq.GuidValue.Should().Be(expected.GuidValue);
            linq.NullableGuid.Should().Be(expected.NullableGuid);
            linq.synchId.Should().Be(expected.synchId);
        }
    }

    // ------------------------------------------------------------------
    // Many random values
    // ------------------------------------------------------------------

    [Fact]
    public async Task RandomGuids_RoundTripThroughBothPaths()
    {
        for (int i = 0; i < 25; i++)
        {
            Guid value = Guid.NewGuid();
            AllTypesEntity entity = NewEntity(value, value);
            await entity.SaveAsync();

            (await ReadDirectAsync(entity.id)).GuidValue.Should().Be(value);
            (await ReadLinqAsync(entity.id)).GuidValue.Should().Be(value);
        }
    }

    // ------------------------------------------------------------------
    // TEXT override
    // ------------------------------------------------------------------

    [Fact]
    public async Task TextOverride_RoundTripsThroughBothPaths()
    {
        TimeTypeTextEntity entity = new TimeTypeTextEntity
        {
            DateTimeAsText = new DateTime(2023, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            DateTimeOffsetAsText = new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero),
            TimeSpanAsText = TimeSpan.FromHours(1),
            DateOnlyAsText = new DateOnly(2023, 6, 15),
            TimeOnlyAsText = new TimeOnly(8, 15, 30),
            GuidAsText = Asymmetric
        };
        await entity.SaveAsync();

        (await ExecuteScalarAsync<string>($"SELECT \"{nameof(TimeTypeTextEntity.GuidAsText)}\" FROM {TextTable} WHERE id = {entity.id}"))
            .Should().Be(Asymmetric.ToString());

        List<TimeTypeTextEntity> direct = await SxmSql.RunStatementAsync<TimeTypeTextEntity>(
            $"SELECT * FROM {TextTable} WHERE id = {entity.id}", TestDatabaseName);
        direct.Should().HaveCount(1);
        direct[0].GuidAsText.Should().Be(Asymmetric);

        TimeTypeTextEntity? linq = await VerifyEntityExistsInDbAsync<TimeTypeTextEntity>(entity.id);
        linq.Should().NotBeNull();
        linq!.GuidAsText.Should().Be(Asymmetric);
    }

    // ------------------------------------------------------------------
    // Malformed storage
    // ------------------------------------------------------------------

    [Fact]
    public async Task MalformedBlobLength_DirectRead_Throws()
    {
        AllTypesEntity entity = NewEntity(Asymmetric, null);
        await entity.SaveAsync();

        await ExecuteNonQueryAsync(
            $"UPDATE {AllTypesTable} SET \"{nameof(AllTypesEntity.GuidValue)}\" = X'0011223344' WHERE id = {entity.id}");

        Func<Task> act = () => ReadDirectAsync(entity.id);

        await act.Should().ThrowAsync<Exception>();
    }
}
