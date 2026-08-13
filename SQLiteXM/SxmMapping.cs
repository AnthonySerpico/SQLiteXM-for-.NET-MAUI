using LinqToDB.Mapping;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM
{
    /// <summary>
    /// Provides a pre-configured <see cref="MappingSchema"/> with custom type converters
    /// used by this library to persist CLR types to SQLite-compatible column representations.
    /// </summary>
    /// <remarks>
    /// The mapping schema registers converters for types that do not map cleanly to SQLite's
    /// native column types (TEXT, INTEGER, BLOB). Converters are centralized here so that
    /// Linq2DB operations (mappings, query generation and parameter conversion) use the
    /// same serialization/deserialization logic implemented in <see cref="SxmColumnDataConverters"/>.
    /// </remarks>
    internal static class SxmMapping
    {
        /// <summary>
        /// Lazily-built <see cref="MappingSchema"/> instance. Construction is deferred until first use.
        /// </summary>
        private static readonly Lazy<MappingSchema> _schema = new(Build);

        /// <summary>
        /// The shared <see cref="MappingSchema"/> with all custom converters registered.
        /// Use this when creating contexts or configuring Linq2DB so the same conversion rules
        /// apply everywhere in the library.
        /// </summary>
        public static MappingSchema Schema => _schema.Value;

        /// <summary>
        /// Build the <see cref="MappingSchema"/> and register all custom converters.
        /// </summary>
        /// <returns>
        /// A fully configured <see cref="MappingSchema"/> that maps CLR types to SQLite-compatible
        /// storage formats and back again.
        /// </returns>
        /// <remarks>
        /// Converters registered here:
        /// - decimal  &lt;-&gt; string (TEXT)
        /// - ulong    &lt;-&gt; string (TEXT)
        /// - DateTime &lt;-&gt; string (ISO 8601) and long (Unix ms)
        /// - DateOnly &lt;-&gt; string and long (DayNumber)
        /// - TimeOnly &lt;-&gt; string and long (total milliseconds)
        /// - TimeSpan &lt;-&gt; string and long (total milliseconds)
        /// - DateTimeOffset &lt;-&gt; string and long (Unix ms)
        /// - Guid     &lt;-&gt; string (TEXT) and byte[] (RFC 4122 BLOB)
        ///
        /// The actual serialization and parsing logic is implemented in <see cref="SxmColumnDataConverters"/>.
        /// Keep converter registrations here so Linq2DB uses consistent behavior for queries, parameters,
        /// and result materialization.
        /// </remarks>
        private static MappingSchema Build()
        {
            var ms = new MappingSchema();

            // CRITICAL: Clear Database property from all entity descriptors
            // SQLiteXM uses the Database property for routing entities to specific database files,
            // but LinqToDB should NOT use it for SQL generation (which would create "database.table" syntax).
            // Since each SxmTransaction connects to a single database file, table names should be unqualified.
            MappingSchema.EntityDescriptorCreatedCallback = (mappingSchema, entityDescriptor) =>
            {
                if (!string.IsNullOrEmpty(entityDescriptor.DatabaseName))
                {
                    // Clear the database name so LinqToDB doesn't qualify table names in SQL
                    entityDescriptor.DatabaseName = null;
                }
            };

            // Configure default data types for CLR types when stored in SQLite
            // This tells LINQ-to-DB what SQLite column type to expect for each CLR type
            ms.SetDataType(typeof(DateTimeOffset), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(DateTimeOffset?), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(DateTime), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(DateTime?), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(TimeSpan), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(TimeSpan?), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(DateOnly), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(DateOnly?), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(TimeOnly), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(TimeOnly?), LinqToDB.DataType.Int64);
            ms.SetDataType(typeof(decimal), LinqToDB.DataType.NVarChar);
            ms.SetDataType(typeof(decimal?), LinqToDB.DataType.NVarChar);
            ms.SetDataType(typeof(ulong), LinqToDB.DataType.NVarChar);
            ms.SetDataType(typeof(ulong?), LinqToDB.DataType.NVarChar);
            ms.SetDataType(typeof(Guid), LinqToDB.DataType.Blob);
            ms.SetDataType(typeof(Guid?), LinqToDB.DataType.Blob);

            // decimal TEXT
            ms.SetConverter<decimal, string?>(d => SxmColumnDataConverters.DecimalToString(d));
            ms.SetConverter<string, decimal?>(s => SxmColumnDataConverters.DecimalFromString(s));

            // ulong TEXT
            ms.SetConverter<ulong, string?>(u => SxmColumnDataConverters.ULongToString(u));
            ms.SetConverter<string, ulong?>(s => SxmColumnDataConverters.ULongFromString(s));

            // DateTime TEXT (ISO 8601) and INTEGER (Ticks)
            ms.SetConverter<DateTime, string?>(d => SxmColumnDataConverters.DateTimeToString(d));
            ms.SetConverter<string, DateTime?>(s => SxmColumnDataConverters.DateTimeFromString(s));
            ms.SetConverter<DateTime, long?>(d => SxmColumnDataConverters.DateTimeToTicks(d));
            ms.SetConverter<long, DateTime?>(t => SxmColumnDataConverters.DateTimeFromTicks(t));
            ms.SetConverter<long, DateTime>(t => SxmColumnDataConverters.DateTimeFromTicks(t) ?? DateTime.MinValue);
            ms.SetConvertExpression<long, DateTime>(
                t => SxmColumnDataConverters.DateTimeFromTicks(t) ?? DateTime.MinValue);
            ms.SetConvertExpression<long, DateTime?>(
                t => SxmColumnDataConverters.DateTimeFromTicks(t));


            // DateOnly TEXT + numeric (DayNumber)
            ms.SetConverter<DateOnly, string?>(d => SxmColumnDataConverters.DateOnlyToString(d));
            ms.SetConverter<string, DateOnly?>(s => SxmColumnDataConverters.DateOnlyFromString(s));
            ms.SetConverter<DateOnly, long?>(d => SxmColumnDataConverters.DateOnlyToUnixDayNumber(d));
            ms.SetConverter<long, DateOnly?>(l => SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l));
            ms.SetConverter<long, DateOnly>(l => SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l) ?? DateOnly.MinValue);
            ms.SetConvertExpression<long, DateOnly>(
                l => SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l) ?? DateOnly.MinValue);
            ms.SetConvertExpression<long, DateOnly?>(
                l => SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l));

            // TimeOnly TEXT + numeric (Ticks)
            ms.SetConverter<TimeOnly, string?>(t => SxmColumnDataConverters.TimeOnlyToString(t));
            ms.SetConverter<string, TimeOnly?>(s => SxmColumnDataConverters.TimeOnlyFromString(s));
            ms.SetConverter<TimeOnly, long?>(t => SxmColumnDataConverters.TimeOnlyToTotalTicks(t));
            ms.SetConverter<long, TimeOnly?>(ticks => SxmColumnDataConverters.TimeOnlyFromTotalTicks(ticks));
            ms.SetConverter<long, TimeOnly>(ticks => SxmColumnDataConverters.TimeOnlyFromTotalTicks(ticks) ?? TimeOnly.MinValue);
            ms.SetConvertExpression<long, TimeOnly>(
                ticks => SxmColumnDataConverters.TimeOnlyFromTotalTicks(ticks) ?? TimeOnly.MinValue);
            ms.SetConvertExpression<long, TimeOnly?>(
                ticks => SxmColumnDataConverters.TimeOnlyFromTotalTicks(ticks));

            // TimeSpan TEXT + numeric (Ticks)
            ms.SetConverter<TimeSpan, string?>(t => SxmColumnDataConverters.TimeSpanToString(t));
            ms.SetConverter<string, TimeSpan?>(s => SxmColumnDataConverters.TimeSpanFromString(s));
            ms.SetConverter<TimeSpan, long?>(t => SxmColumnDataConverters.TimeSpanToTotalTicks(t));
            ms.SetConverter<long, TimeSpan?>(ticks => SxmColumnDataConverters.TimeSpanFromTotalTicks(ticks));
            ms.SetConverter<long, TimeSpan>(ticks => SxmColumnDataConverters.TimeSpanFromTotalTicks(ticks) ?? TimeSpan.Zero);
            ms.SetConvertExpression<long, TimeSpan>(
                ticks => SxmColumnDataConverters.TimeSpanFromTotalTicks(ticks) ?? TimeSpan.Zero);
            ms.SetConvertExpression<long, TimeSpan?>(
                ticks => SxmColumnDataConverters.TimeSpanFromTotalTicks(ticks));

            // DateTimeOffset TEXT + numeric (Ticks)
            ms.SetConverter<DateTimeOffset, string?>(dto => SxmColumnDataConverters.DateTimeOffsetToString(dto));
            ms.SetConverter<string, DateTimeOffset?>(s => SxmColumnDataConverters.DateTimeOffsetFromString(s));
            ms.SetConverter<DateTimeOffset, long?>(dto => SxmColumnDataConverters.DateTimeOffsetToTicks(dto));
            ms.SetConverter<long, DateTimeOffset?>(ticksVal => SxmColumnDataConverters.DateTimeOffsetFromTicks(ticksVal));

            // Add non-nullable version for LINQ-to-DB materialization
            ms.SetConverter<long, DateTimeOffset>(ticksVal => SxmColumnDataConverters.DateTimeOffsetFromTicks(ticksVal) ?? DateTimeOffset.MinValue);

            // CRITICAL: Use SetConvertExpression for reader materialization
            ms.SetConvertExpression<long, DateTimeOffset>(
                ticksVal => SxmColumnDataConverters.DateTimeOffsetFromTicks(ticksVal) ?? DateTimeOffset.MinValue);
            ms.SetConvertExpression<long, DateTimeOffset?>(
                ticksVal => SxmColumnDataConverters.DateTimeOffsetFromTicks(ticksVal));
            ms.SetConvertExpression<DateTimeOffset, long?>(
                dto => SxmColumnDataConverters.DateTimeOffsetToTicks(dto));

            // Guid TEXT + BLOB (native .NET byte order)
            // TEXT: canonical GUID string format
            ms.SetConverter<Guid, string?>(g => SxmColumnDataConverters.GuidToString(g));
            ms.SetConverter<string, Guid?>(s => SxmColumnDataConverters.GuidFromString(s));
            ms.SetConverter<string, Guid>(s => SxmColumnDataConverters.GuidFromString(s) ?? Guid.Empty);
            ms.SetConvertExpression<string, Guid>(
                s => SxmColumnDataConverters.GuidFromString(s) ?? Guid.Empty);
            ms.SetConvertExpression<string, Guid?>(
                s => SxmColumnDataConverters.GuidFromString(s));

            // BLOB: native .NET byte order (Guid.ToByteArray / new Guid(byte[]))
            // This is the default for BLOB storage and provides best LINQ-to-DB compatibility
            ms.SetConverter<Guid, byte[]?>(g => SxmColumnDataConverters.GuidToNativeBytes(g));
            ms.SetConverter<byte[], Guid?>(b => SxmColumnDataConverters.GuidFromNativeBytes(b));
            ms.SetConverter<byte[], Guid>(b => SxmColumnDataConverters.GuidFromNativeBytes(b) ?? Guid.Empty);

            // CRITICAL: Use SetConvertExpression for reader materialization so LinqToDB
            // reads the BLOB as byte[] and converts, instead of calling SqliteDataReader.GetGuid()
            ms.SetConvertExpression<byte[], Guid>(
                b => SxmColumnDataConverters.GuidFromNativeBytes(b) ?? Guid.Empty);
            ms.SetConvertExpression<byte[], Guid?>(
                b => SxmColumnDataConverters.GuidFromNativeBytes(b));

            return ms;
        }
    }
}