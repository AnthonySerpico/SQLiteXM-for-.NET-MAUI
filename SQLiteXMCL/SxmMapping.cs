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
    public static class SxmMapping
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

            // decimal TEXT
            ms.SetConverter<decimal, string?>(d => SxmColumnDataConverters.DecimalToString(d));
            ms.SetConverter<string, decimal?>(s => SxmColumnDataConverters.DecimalFromString(s));

            // ulong TEXT
            ms.SetConverter<ulong, string?>(u => SxmColumnDataConverters.ULongToString(u));
            ms.SetConverter<string, ulong?>(s => SxmColumnDataConverters.ULongFromString(s));

            // DateTime TEXT (ISO 8601) DateTime ticks (INTEGER)
            ms.SetConverter<DateTime, string?>(d => SxmColumnDataConverters.DateTimeToString(d));
            ms.SetConverter<string, DateTime?>(s => SxmColumnDataConverters.DateTimeFromString(s));
            ms.SetConverter<DateTime, long?>(d => SxmColumnDataConverters.DateTimeToUnixTimeMilliseconds(d));
            ms.SetConverter<long, DateTime?>(t => SxmColumnDataConverters.DateTimeFromUnixTimeMilliseconds(t));

            // DateOnly TEXT + numeric (DayNumber)
            ms.SetConverter<DateOnly, string?>(d => SxmColumnDataConverters.DateOnlyToString(d));
            ms.SetConverter<string, DateOnly?>(s => SxmColumnDataConverters.DateOnlyFromString(s));
            ms.SetConverter<DateOnly, long?>(d => SxmColumnDataConverters.DateOnlyToUnixDayNumber(d));
            ms.SetConverter<long, DateOnly?>(l => SxmColumnDataConverters.DateOnlyFromUnixDayNumber(l));

            // TimeOnly TEXT + numeric (Ticks)
            ms.SetConverter<TimeOnly, string?>(t => SxmColumnDataConverters.TimeOnlyToString(t));
            ms.SetConverter<string, TimeOnly?>(s => SxmColumnDataConverters.TimeOnlyFromString(s));
            ms.SetConverter<TimeOnly, long?>(t => SxmColumnDataConverters.TimeOnlyToTotalMilliseconds(t));
            ms.SetConverter<long, TimeOnly?>(ticks => SxmColumnDataConverters.TimeOnlyFromTotalMilliseconds(ticks));
            ms.SetConverter<TimeOnly, long?>(t => SxmColumnDataConverters.TimeOnlyToTotalTicks(t));
            ms.SetConverter<long, TimeOnly?>(ticks => SxmColumnDataConverters.TimeOnlyFromTotalTicks(ticks));

            // TimeSpan TEXT + numeric (Ticks)
            ms.SetConverter<TimeSpan, string?>(t => SxmColumnDataConverters.TimeSpanToString(t));
            ms.SetConverter<string, TimeSpan?>(s => SxmColumnDataConverters.TimeSpanFromString(s));
            ms.SetConverter<TimeSpan, long?>(t => SxmColumnDataConverters.TimeSpanToTotalMilliseconds(t));
            ms.SetConverter<long, TimeSpan?>(ticks => SxmColumnDataConverters.TimeSpanFromTotalMilliseconds(ticks));
            ms.SetConverter<TimeSpan, long?>(t => SxmColumnDataConverters.TimeSpanToTotalTicks(t));
            ms.SetConverter<long, TimeSpan?>(ticks => SxmColumnDataConverters.TimeSpanFromTotalTicks(ticks));

            // DateTimeOffset TEXT + numeric (Unix ms)
            ms.SetConverter<DateTimeOffset, string?>(dto => SxmColumnDataConverters.DateTimeOffsetToString(dto));
            ms.SetConverter<string, DateTimeOffset?>(s => SxmColumnDataConverters.DateTimeOffsetFromString(s));
            ms.SetConverter<DateTimeOffset, long?>(dto => SxmColumnDataConverters.DateTimeOffsetToUnixTimeMilliseconds(dto));
            ms.SetConverter<long, DateTimeOffset?>(msVal => SxmColumnDataConverters.DateTimeOffsetFromUnixTimeMilliseconds(msVal));

            // Guid TEXT + byte[]
            ms.SetConverter<Guid, string?>(g => SxmColumnDataConverters.GuidToString(g));
            ms.SetConverter<string, Guid?>(s => SxmColumnDataConverters.GuidFromString(s));
            ms.SetConverter<Guid, byte[]?>(g => SxmColumnDataConverters.GuidToRfc4122Bytes(g));
            ms.SetConverter<byte[], Guid?>(b => SxmColumnDataConverters.GuidFromRfc4122Bytes(b));

            return ms;
        }
    }
}