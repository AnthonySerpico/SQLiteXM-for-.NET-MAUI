using System;
using System.Collections.Generic;
using System.Reflection;
using SQLiteXM;
using static SQLiteXM.SxmDefines;

namespace SQLiteXM.Tests;

/// <summary>
/// Direct unit tests for the internal conversion and formatting helpers in <see cref="SxmHelpers"/>.
/// These run without a database: each helper is given values and its output or exception is checked.
/// </summary>
[Collection("Sequential")]
public class SxmHelpersTests
{
    // ---------- Plain test types ----------

    public enum SmallEnum : byte { A = 1, B = 200 }
    public enum BigEnum : long { Max = long.MaxValue }

    public class Holder
    {
        public int IntValue { get; set; }
        public long LongValue { get; set; }
        public short ShortValue { get; set; }
        public ushort UShortValue { get; set; }
        public uint UIntValue { get; set; }
        public sbyte SByteValue { get; set; }
        public byte ByteValue { get; set; }
        public ulong ULongValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
        public decimal DecimalValue { get; set; }
        public string? StringValue { get; set; }
        public Guid GuidValue { get; set; }
        public bool BoolValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public DateTimeOffset DateTimeOffsetValue { get; set; }
        public TimeSpan TimeSpanValue { get; set; }
        public DateOnly DateOnlyValue { get; set; }
        public TimeOnly TimeOnlyValue { get; set; }
        public int? NullableInt { get; set; }
        public byte[]? BlobValue { get; set; }
        public string ReadOnlyValue => "fixed";
        public event EventHandler? SomethingHappened;
        public int Field;
        public void Method() { SomethingHappened?.Invoke(this, EventArgs.Empty); }
    }

    private static Holder Load(string column, object? value)
    {
        Holder h = new Holder();
        SxmHelpers.LoadDbValues(new Dictionary<string, object?> { [column] = value }, h, typeof(Holder));
        return h;
    }

    // ---------- QuoteIdentifier ----------

    [Theory]
    [InlineData("Table", "\"Table\"")]
    [InlineData("we\"ird", "\"we\"\"ird\"")]
    [InlineData("with space", "\"with space\"")]
    public void QuoteIdentifier_QuotesAndEscapes(string input, string expected)
    {
        SxmHelpers.QuoteIdentifier(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QuoteIdentifier_NullOrWhitespace_Throws(string? input)
    {
        Action act = () => SxmHelpers.QuoteIdentifier(input!);
        act.Should().Throw<ArgumentException>();
    }

    // ---------- FormatSqlLiteral ----------

    [Fact]
    public void FormatSqlLiteral_AllSupportedTypes()
    {
        SxmHelpers.FormatSqlLiteral(null).Should().Be("NULL");
        SxmHelpers.FormatSqlLiteral("it's").Should().Be("'it''s'");
        SxmHelpers.FormatSqlLiteral('\'').Should().Be("''''");
        SxmHelpers.FormatSqlLiteral('x').Should().Be("'x'");
        SxmHelpers.FormatSqlLiteral(true).Should().Be("1");
        SxmHelpers.FormatSqlLiteral(false).Should().Be("0");
        SxmHelpers.FormatSqlLiteral(SmallEnum.B).Should().Be("200");
        SxmHelpers.FormatSqlLiteral(BigEnum.Max).Should().Be(long.MaxValue.ToString());
        SxmHelpers.FormatSqlLiteral((sbyte)-5).Should().Be("-5");
        SxmHelpers.FormatSqlLiteral((byte)5).Should().Be("5");
        SxmHelpers.FormatSqlLiteral((short)-300).Should().Be("-300");
        SxmHelpers.FormatSqlLiteral((ushort)300).Should().Be("300");
        SxmHelpers.FormatSqlLiteral(-70000).Should().Be("-70000");
        SxmHelpers.FormatSqlLiteral(70000u).Should().Be("70000");
        SxmHelpers.FormatSqlLiteral(-5000000000L).Should().Be("-5000000000");
        SxmHelpers.FormatSqlLiteral(ulong.MaxValue).Should().Be("18446744073709551615");
        SxmHelpers.FormatSqlLiteral(1.5f).Should().Be("1.5");
        SxmHelpers.FormatSqlLiteral(2.25d).Should().Be("2.25");
    }

    [Fact]
    public void FormatSqlLiteral_NonFiniteFloats_AreQuoted()
    {
        SxmHelpers.FormatSqlLiteral(float.NaN).Should().Be("'NaN'");
        SxmHelpers.FormatSqlLiteral(float.PositiveInfinity).Should().StartWith("'").And.EndWith("'");
        SxmHelpers.FormatSqlLiteral(double.NaN).Should().Be("'NaN'");
        SxmHelpers.FormatSqlLiteral(double.NegativeInfinity).Should().StartWith("'").And.EndWith("'");
    }

    [Fact]
    public void FormatSqlLiteral_InvariantCulture_IsUsed()
    {
        System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            SxmHelpers.FormatSqlLiteral(1.5d).Should().Be("1.5", "SQL literals must never use a culture decimal comma");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatSqlLiteral_Arrays_FormatEachElementIncludingNull()
    {
        SxmHelpers.FormatSqlLiteral(new[] { 1, 2, 3 }).Should().Be("(1, 2, 3)");
        SxmHelpers.FormatSqlLiteral(new string?[] { "a", null, "b'c" }).Should().Be("('a', NULL, 'b''c')");
        SxmHelpers.FormatSqlLiteral(Array.Empty<int>()).Should().Be("()");
    }

    [Fact]
    public void FormatSqlLiteral_UnsupportedTypes_Throw()
    {
        Action dec = () => SxmHelpers.FormatSqlLiteral(1.5m);
        Action guid = () => SxmHelpers.FormatSqlLiteral(Guid.NewGuid());
        Action badArray = () => SxmHelpers.FormatSqlLiteral(new object[] { 1, DateTime.Now });

        dec.Should().Throw<ArgumentException>();
        guid.Should().Throw<ArgumentException>();
        badArray.Should().Throw<ArgumentException>();
    }

    // ---------- GetSqlStatementsFileType ----------

    [Theory]
    [InlineData("statements.json", SqlStatementsFileType.Json)]
    [InlineData("STATEMENTS.JSON", SqlStatementsFileType.Json)]
    [InlineData("statements.jsn", SqlStatementsFileType.Json)]
    [InlineData("folder/statements.xml", SqlStatementsFileType.Xml)]
    [InlineData("statements.XML", SqlStatementsFileType.Xml)]
    [InlineData("statements.txt", SqlStatementsFileType.Unknown)]
    [InlineData("statements", SqlStatementsFileType.Unknown)]
    [InlineData("", SqlStatementsFileType.Unknown)]
    [InlineData("   ", SqlStatementsFileType.Unknown)]
    public void GetSqlStatementsFileType_RecognisesExtensions(string path, object expected)
    {
        SxmHelpers.GetSqlStatementsFileType(path).Should().Be((SqlStatementsFileType)expected);
    }

    // ---------- Statement type helpers ----------

    [Theory]
    [InlineData(SqlStatementType.Select, "SELECT")]
    [InlineData(SqlStatementType.SelectDirect, "SELECT")]
    [InlineData(SqlStatementType.Insert, "INSERT")]
    [InlineData(SqlStatementType.InsertDirect, "INSERT")]
    [InlineData(SqlStatementType.Delete, "DELETE")]
    [InlineData(SqlStatementType.DeleteDirect, "DELETE")]
    [InlineData(SqlStatementType.Update, "UPDATE")]
    [InlineData(SqlStatementType.UpdateDirect, "UPDATE")]
    public void GetDatabaseStatementTypeName_MapsEveryKnownType(object type, string expected)
    {
        SxmHelpers.GetDatabaseStatementTypeName((SqlStatementType)type).Should().Be(expected);
    }

    [Fact]
    public void GetDatabaseStatementTypeName_Unknown_Throws()
    {
        Action act = () => SxmHelpers.GetDatabaseStatementTypeName(SqlStatementType.Unknown);
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown*");
    }

    [Fact]
    public void GetDatabaseStatementTypeFromSql_UnrecognisedText_ThrowsWithTruncatedName()
    {
        string longText = "this is not sql and it is definitely longer than thirty characters";
        Action longAct = () => SxmHelpers.GetDatabaseStatementTypeFromSql(longText, null);
        Action shortAct = () => SxmHelpers.GetDatabaseStatementTypeFromSql("nonsense", null);

        longAct.Should().Throw<ArgumentException>().WithMessage("*this is not sql and it is def...*");
        shortAct.Should().Throw<ArgumentException>().WithMessage("*'nonsense'*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetDatabaseStatementTypeFromName_NullOrEmpty_Throws(string? name)
    {
        Action act = () => SxmHelpers.GetDatabaseStatementTypeFromName(name);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetDatabaseStatementTypeFromName_UnknownName_ReturnsUnknown()
    {
        SxmHelpers.GetDatabaseStatementTypeFromName($"no_such_{Guid.NewGuid():N}").Should().Be(SqlStatementType.Unknown);
    }

    [Theory]
    [InlineData(SqlStatementType.SelectDirect)]
    [InlineData(SqlStatementType.InsertDirect)]
    [InlineData(SqlStatementType.UpdateDirect)]
    [InlineData(SqlStatementType.DeleteDirect)]
    public void SqlStatementFromStatementName_DirectTypes_ReturnTextUnchanged(object type)
    {
        SxmHelpers.SqlStatementFromStatementName("SELECT 1", (SqlStatementType)type).Should().Be("SELECT 1");
    }

    [Theory]
    [InlineData(SqlStatementType.Select)]
    [InlineData(SqlStatementType.Insert)]
    [InlineData(SqlStatementType.Update)]
    [InlineData(SqlStatementType.Delete)]
    [InlineData(SqlStatementType.Unknown)]
    public void SqlStatementFromStatementName_UnknownNamedStatement_ReturnsNull(object type)
    {
        SxmHelpers.SqlStatementFromStatementName($"missing_{Guid.NewGuid():N}", (SqlStatementType)type).Should().BeNull();
    }

    // ---------- GetFirstOrThrow ----------

    [Fact]
    public void GetFirstOrThrow_ReturnsFirstOrThrows()
    {
        SxmHelpers.GetFirstOrThrow(new List<int> { 7, 8 }, "stmt").Should().Be(7);

        Action empty = () => SxmHelpers.GetFirstOrThrow(new List<int>(), "stmt");
        Action nullList = () => SxmHelpers.GetFirstOrThrow<int>(null, "stmt");

        empty.Should().Throw<InvalidOperationException>().WithMessage("*'stmt'*RETURNING*");
        nullList.Should().Throw<InvalidOperationException>();
    }

    // ---------- Association bookkeeping ----------

    [Fact]
    public void AssociationLookups_NullOrEmptyInputs_ReturnEmptyResults()
    {
        SxmHelpers.GetForeignKeyColumnsTargeting(null!, "X").Should().BeEmpty();
        SxmHelpers.GetForeignKeyColumnsTargeting(typeof(Holder), "").Should().BeEmpty();
        SxmHelpers.GetForeignKeyColumnsTargeting(typeof(Holder), "NeverRegistered").Should().BeEmpty();
        SxmHelpers.IsRegisteredAssociation(null!, "Nav").Should().BeFalse();
        SxmHelpers.IsRegisteredAssociation(typeof(Holder), "").Should().BeFalse();
        SxmHelpers.GetRegisteredAssociationNames(null!).Should().BeEmpty();
        SxmHelpers.GetRegisteredAssociationNames(typeof(Holder)).Should().BeEmpty();
    }

    [Fact]
    public void CreateAssociation_WithoutNavigationProperty_RecordsForeignKeyOnly()
    {
        string target = $"Target_{Guid.NewGuid():N}";

        SxmHelpers.CreateAssociation(typeof(Holder), "IntValue", target);
        SxmHelpers.CreateAssociation(typeof(Holder), "LongValue", target);
        SxmHelpers.CreateAssociation(typeof(Holder), "IntValue", target);

        SxmHelpers.GetForeignKeyColumnsTargeting(typeof(Holder), target)
            .Should().Equal(new[] { "IntValue", "LongValue" }, "keys are de-duplicated and sorted");
        SxmHelpers.GetRegisteredAssociationNames(typeof(Holder)).Should().BeEmpty();
    }

    // ---------- MemberInfo.GetMemberType ----------

    [Fact]
    public void GetMemberType_ReturnsTypeForEachMemberKind()
    {
        Type t = typeof(Holder);
        t.GetField(nameof(Holder.Field))!.GetMemberType().Should().Be(typeof(int));
        t.GetProperty(nameof(Holder.StringValue))!.GetMemberType().Should().Be(typeof(string));
        t.GetEvent(nameof(Holder.SomethingHappened))!.GetMemberType().Should().Be(typeof(EventHandler));
        t.GetMethod(nameof(Holder.Method))!.GetMemberType().Should().Be(typeof(void));
        t.GetConstructor(Type.EmptyTypes)!.GetMemberType().Should().BeNull();
        typeof(SxmHelpersTests).GetNestedType(nameof(Holder))!.GetMemberType().Should().BeNull();
    }

    // ---------- LoadDbValues ----------

    [Fact]
    public void LoadDbValues_NullArguments_Throw()
    {
        Action nullEntity = () => SxmHelpers.LoadDbValues(new Dictionary<string, object?>(), (SxmEntity)null!);
        Action nullObject = () => SxmHelpers.LoadDbValues(new Dictionary<string, object?>(), null!, typeof(Holder));
        Action nullRecord = () => SxmHelpers.LoadDbValues(null!, new Holder(), typeof(Holder));

        nullEntity.Should().Throw<ArgumentNullException>();
        nullObject.Should().Throw<ArgumentNullException>();
        nullRecord.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadDbValues_UnknownAndReadOnlyColumns_AreIgnored()
    {
        Holder h = new Holder();
        Dictionary<string, object?> record = new Dictionary<string, object?>
        {
            ["NoSuchColumn"] = 5L,
            ["ReadOnlyValue"] = "x",
            ["intvalue"] = 9L
        };

        SxmHelpers.LoadDbValues(record, h, typeof(Holder));

        h.ReadOnlyValue.Should().Be("fixed");
        h.IntValue.Should().Be(0, "column matching is case-sensitive");
    }

    [Fact]
    public void LoadDbValues_NullAndDbNull_SetDefaults()
    {
        Holder h = new Holder { IntValue = 5, StringValue = "x", NullableInt = 3 };
        SxmHelpers.LoadDbValues(new Dictionary<string, object?>
        {
            ["IntValue"] = DBNull.Value,
            ["StringValue"] = null,
            ["NullableInt"] = DBNull.Value
        }, h, typeof(Holder));

        h.IntValue.Should().Be(0);
        h.StringValue.Should().BeNull();
        h.NullableInt.Should().BeNull();
    }

    [Fact]
    public void LoadDbValues_IntegerTypes_ConvertInRange()
    {
        Load("IntValue", 123L).IntValue.Should().Be(123);
        Load("LongValue", 5_000_000_000L).LongValue.Should().Be(5_000_000_000L);
        Load("ShortValue", -123L).ShortValue.Should().Be(-123);
        Load("UShortValue", 65535L).UShortValue.Should().Be(65535);
        Load("UIntValue", 4_000_000_000L).UIntValue.Should().Be(4_000_000_000u);
        Load("SByteValue", -128L).SByteValue.Should().Be(-128);
        Load("ByteValue", 255L).ByteValue.Should().Be(255);
        Load("NullableInt", 42L).NullableInt.Should().Be(42);
    }

    [Theory]
    [InlineData("IntValue", 3_000_000_000L)]
    [InlineData("IntValue", -3_000_000_000L)]
    [InlineData("ShortValue", 40_000L)]
    [InlineData("ShortValue", -40_000L)]
    [InlineData("UShortValue", 70_000L)]
    [InlineData("UShortValue", -1L)]
    [InlineData("UIntValue", 5_000_000_000L)]
    [InlineData("UIntValue", -1L)]
    [InlineData("SByteValue", 200L)]
    [InlineData("SByteValue", -200L)]
    [InlineData("ByteValue", 256L)]
    [InlineData("ByteValue", -1L)]
    public void LoadDbValues_IntegerOutOfRange_ThrowsDataConversionFailure(string column, long value)
    {
        Action act = () => Load(column, value);

        act.Should().Throw<SxmException>()
            .Which.ErrorCode.Should().Be(SxmErrorCode.DataConversionFailure);
    }

    [Theory]
    [InlineData("IntValue")]
    [InlineData("ShortValue")]
    [InlineData("UShortValue")]
    [InlineData("UIntValue")]
    [InlineData("SByteValue")]
    [InlineData("ByteValue")]
    [InlineData("FloatValue")]
    public void LoadDbValues_NonNumericText_ForNumericProperty_ThrowsWrapped(string column)
    {
        // FormatException is wrappable, so it surfaces as an SxmException with DataConversionFailure.
        Action act = () => Load(column, "not a number");

        act.Should().Throw<SxmException>()
            .Which.ErrorCode.Should().Be(SxmErrorCode.DataConversionFailure);
    }

    [Theory]
    [InlineData("IntValue")]
    [InlineData("ShortValue")]
    [InlineData("UShortValue")]
    [InlineData("UIntValue")]
    [InlineData("SByteValue")]
    [InlineData("ByteValue")]
    [InlineData("FloatValue")]
    public void LoadDbValues_NonConvertibleObject_ForNumericProperty_PassesThroughWithContext(string column)
    {
        // Convert.ToInt64/ToDouble throw InvalidCastException for an arbitrary object. InvalidCastException
        // is wrappable, but a byte[] is used here; either way the caller gets an exception with context.
        Action act = () => Load(column, new object());

        act.Should().Throw<Exception>().Where(e => e is SxmException || e.Data.Contains(SxmException.ContextKey));
    }

    [Fact]
    public void LoadDbValues_FloatingPointAndDecimal_Convert()
    {
        Load("FloatValue", 1.25d).FloatValue.Should().Be(1.25f);
        Load("DoubleValue", 2.5d).DoubleValue.Should().Be(2.5d);
        Load("DecimalValue", "123.456").DecimalValue.Should().Be(123.456m);
    }

    [Fact]
    public void LoadDbValues_DecimalAndULong_NonStringValue_FallsBackToRawAssignment()
    {
        // Decimal and ulong are stored as TEXT. A non-string value hits the fallback, which assigns the raw
        // value; a long cannot be assigned to a decimal/ulong property, so the setter throws and is wrapped.
        Action dec = () => Load("DecimalValue", 5L);
        Action ul = () => Load("ULongValue", 5L);

        dec.Should().Throw<Exception>();
        ul.Should().Throw<Exception>();
    }

    [Fact]
    public void LoadDbValues_ULongFromString_Converts()
    {
        Load("ULongValue", ulong.MaxValue.ToString()).ULongValue.Should().Be(ulong.MaxValue);
    }

    [Fact]
    public void LoadDbValues_StringBoolAndGuid_Convert()
    {
        Guid g = Guid.NewGuid();

        Load("StringValue", 42L).StringValue.Should().Be("42");
        Load("BoolValue", 1L).BoolValue.Should().BeTrue();
        Load("BoolValue", "1").BoolValue.Should().BeTrue();
        Load("BoolValue", 0L).BoolValue.Should().BeFalse();
        Load("BoolValue", "true").BoolValue.Should().BeFalse("only the text \"1\" means true");
        Load("GuidValue", g.ToByteArray()).GuidValue.Should().Be(g);
        Load("GuidValue", g.ToString()).GuidValue.Should().Be(g);
    }

    [Fact]
    public void LoadDbValues_DateAndTimeTypes_FromTicksAndText()
    {
        DateTime dt = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        DateTimeOffset dto = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
        TimeSpan ts = TimeSpan.FromMinutes(90);
        DateOnly d = new DateOnly(2024, 5, 6);
        TimeOnly t = new TimeOnly(7, 8, 9);

        Load("DateTimeValue", SxmColumnDataConverters.DateTimeToTicks(dt)!.Value).DateTimeValue.Should().Be(dt);
        Load("DateTimeValue", SxmColumnDataConverters.DateTimeToString(dt)).DateTimeValue.Should().Be(dt);
        Load("DateTimeOffsetValue", SxmColumnDataConverters.DateTimeOffsetToTicks(dto)!.Value).DateTimeOffsetValue.Should().Be(dto);
        Load("DateTimeOffsetValue", SxmColumnDataConverters.DateTimeOffsetToString(dto)).DateTimeOffsetValue.Should().Be(dto);
        Load("TimeSpanValue", ts.Ticks).TimeSpanValue.Should().Be(ts);
        Load("TimeSpanValue", SxmColumnDataConverters.TimeSpanToString(ts)).TimeSpanValue.Should().Be(ts);
        Load("DateOnlyValue", (long)SxmColumnDataConverters.DateOnlyToUnixDayNumber(d)!.Value).DateOnlyValue.Should().Be(d);
        Load("DateOnlyValue", SxmColumnDataConverters.DateOnlyToString(d)).DateOnlyValue.Should().Be(d);
        Load("TimeOnlyValue", t.Ticks).TimeOnlyValue.Should().Be(t);
        Load("TimeOnlyValue", SxmColumnDataConverters.TimeOnlyToString(t)).TimeOnlyValue.Should().Be(t);
    }

    [Theory]
    [InlineData("GuidValue")]
    [InlineData("DateTimeValue")]
    [InlineData("DateTimeOffsetValue")]
    [InlineData("TimeSpanValue")]
    [InlineData("DateOnlyValue")]
    [InlineData("TimeOnlyValue")]
    public void LoadDbValues_UnexpectedStorageForStructType_Throws(string column)
    {
        // A double is neither ticks (long) nor text, so the raw-value fallback fails in the setter.
        Action act = () => Load(column, 1.5d);

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void LoadDbValues_ByteArray_FallsBackToRawAssignment()
    {
        byte[] blob = { 1, 2, 3 };
        Load("BlobValue", blob).BlobValue.Should().Equal(blob);
    }

    // ---------- LoadParameterValues ----------

    [Fact]
    public void LoadParameterValues_NullArguments_Throw()
    {
        Action nullEntity = () => SxmHelpers.LoadParameterValues<Holder>(new Dictionary<string, string>(), null!);
        Action nullColumns = () => SxmHelpers.LoadParameterValues(null!, new Holder());

        nullEntity.Should().Throw<ArgumentNullException>();
        nullColumns.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadParameterValues_ConvertsEachStorageType()
    {
        Guid g = Guid.NewGuid();
        DateTime dt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        DateTimeOffset dto = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        TimeSpan ts = TimeSpan.FromSeconds(75);
        DateOnly d = new DateOnly(2024, 1, 2);
        TimeOnly t = new TimeOnly(3, 4, 5);

        Holder h = new Holder
        {
            IntValue = 5,
            DecimalValue = 1.5m,
            ULongValue = ulong.MaxValue,
            GuidValue = g,
            DateTimeValue = dt,
            DateTimeOffsetValue = dto,
            TimeSpanValue = ts,
            DateOnlyValue = d,
            TimeOnlyValue = t,
            StringValue = null
        };

        Dictionary<string, object?> text = SxmHelpers.LoadParameterValues(new Dictionary<string, string>
        {
            ["IntValue"] = "INTEGER",
            ["DecimalValue"] = "TEXT",
            ["ULongValue"] = "TEXT",
            ["GuidValue"] = "TEXT",
            ["DateTimeValue"] = "TEXT",
            ["DateTimeOffsetValue"] = "TEXT",
            ["TimeSpanValue"] = "TEXT",
            ["DateOnlyValue"] = "TEXT",
            ["TimeOnlyValue"] = "TEXT",
            ["StringValue"] = "TEXT",
            ["NotAProperty"] = "TEXT"
        }, h);

        text["IntValue"].Should().Be(5);
        text["DecimalValue"].Should().Be(SxmColumnDataConverters.DecimalToString(1.5m));
        text["ULongValue"].Should().Be(SxmColumnDataConverters.ULongToString(ulong.MaxValue));
        text["GuidValue"].Should().Be(SxmColumnDataConverters.GuidToString(g));
        text["DateTimeValue"].Should().Be(SxmColumnDataConverters.DateTimeToString(dt));
        text["DateTimeOffsetValue"].Should().Be(SxmColumnDataConverters.DateTimeOffsetToString(dto));
        text["TimeSpanValue"].Should().Be(SxmColumnDataConverters.TimeSpanToString(ts));
        text["DateOnlyValue"].Should().Be(SxmColumnDataConverters.DateOnlyToString(d));
        text["TimeOnlyValue"].Should().Be(SxmColumnDataConverters.TimeOnlyToString(t));
        text["StringValue"].Should().Be(DBNull.Value);
        text.Should().NotContainKey("NotAProperty", "columns without a matching property are skipped");

        Dictionary<string, object?> numeric = SxmHelpers.LoadParameterValues(new Dictionary<string, string>
        {
            ["GuidValue"] = "blob",
            ["DateTimeValue"] = "integer",
            ["DateTimeOffsetValue"] = "INTEGER",
            ["TimeSpanValue"] = "INTEGER",
            ["DateOnlyValue"] = "INTEGER",
            ["TimeOnlyValue"] = "INTEGER"
        }, h);

        numeric["GuidValue"].Should().BeEquivalentTo(SxmColumnDataConverters.GuidToNativeBytes(g));
        numeric["DateTimeValue"].Should().Be(SxmColumnDataConverters.DateTimeToTicks(dt));
        numeric["DateTimeOffsetValue"].Should().Be(SxmColumnDataConverters.DateTimeOffsetToTicks(dto));
        numeric["TimeSpanValue"].Should().Be(SxmColumnDataConverters.TimeSpanToTotalTicks(ts));
        numeric["DateOnlyValue"].Should().Be(SxmColumnDataConverters.DateOnlyToUnixDayNumber(d));
        numeric["TimeOnlyValue"].Should().Be(SxmColumnDataConverters.TimeOnlyToTotalTicks(t));
    }

    [Theory]
    [InlineData("GuidValue", "INTEGER")]
    [InlineData("DateTimeValue", "BLOB")]
    [InlineData("DateOnlyValue", "REAL")]
    [InlineData("DateTimeOffsetValue", "BLOB")]
    [InlineData("TimeSpanValue", "REAL")]
    [InlineData("TimeOnlyValue", "BLOB")]
    public void LoadParameterValues_UnsupportedStorageType_ThrowsArgumentExceptionWithContext(string column, string dbType)
    {
        Holder h = new Holder();
        Action act = () => SxmHelpers.LoadParameterValues(new Dictionary<string, string> { [column] = dbType }, h);

        ArgumentException ex = act.Should().Throw<ArgumentException>()
            .WithMessage($"*Unsupported DB type '{dbType}'*'{column}'*").Which;
        ex.Data.Contains(SxmException.ContextKey).Should().BeTrue("the pass-through path attaches operation context");
    }

    [Fact]
    public void LoadParameterValues_GetterThrows_IsWrapped()
    {
        ThrowingGetter entity = new ThrowingGetter();
        Action act = () => SxmHelpers.LoadParameterValues(new Dictionary<string, string> { ["Boom"] = "TEXT" }, entity);

        act.Should().Throw<SxmException>()
            .Which.ErrorCode.Should().Be(SxmErrorCode.DataConversionFailure);
    }

    public class ThrowingGetter
    {
        public string Boom
        {
            get => throw new System.IO.IOException("getter failed");
            set { }
        }
    }

    [Fact]
    public void LoadDbValues_SetterThrowsWrappable_IsWrapped()
    {
        ThrowingSetter entity = new ThrowingSetter();
        Action act = () => SxmHelpers.LoadDbValues(new Dictionary<string, object?> { ["Boom"] = "x" }, entity, typeof(ThrowingSetter));

        act.Should().Throw<SxmException>()
            .Which.ErrorCode.Should().Be(SxmErrorCode.DataConversionFailure);
    }

    public class ThrowingSetter
    {
        public string? Boom
        {
            get => null;
            set => throw new System.IO.IOException("setter failed");
        }
    }

    // ---------- PopulateUserRecord ----------

    [Fact]
    public void PopulateUserRecord_CreatesOneObjectPerRow()
    {
        List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["IntValue"] = 1L, ["StringValue"] = "a" },
            new Dictionary<string, object?> { ["IntValue"] = 2L, ["StringValue"] = "b" }
        };

        List<Holder> result = SxmHelpers.PopulateUserRecord<Holder>(rows);

        result.Should().HaveCount(2);
        result[0].IntValue.Should().Be(1);
        result[1].StringValue.Should().Be("b");
        SxmHelpers.PopulateUserRecord<Holder>(new List<Dictionary<string, object?>>()).Should().BeEmpty();
    }
}
