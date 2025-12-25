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
            ms.SetConverter<decimal, string>(d => SxmColumnDataConverters.decimalToString(d));
            ms.SetConverter<string, decimal>(s => SxmColumnDataConverters.decimalFromString(s));

            // ulong TEXT
            ms.SetConverter<ulong, string>(u => SxmColumnDataConverters.uLongToString(u));
            ms.SetConverter<string, ulong>(s => SxmColumnDataConverters.uLongFromString(s));

            // DateTime TEXT (ISO 8601) DateTime ticks (INTEGER)
            ms.SetConverter<DateTime, string>(d => SxmColumnDataConverters.dateTimeToString(d));
            ms.SetConverter<string, DateTime>(s => SxmColumnDataConverters.dateTimeFromString(s));
            ms.SetConverter<DateTime, long>(d => SxmColumnDataConverters.dateTimeToUnixTimeMilliseconds(d));
            ms.SetConverter<long, DateTime>(t => SxmColumnDataConverters.dateTimeFromUnixTimeMilliseconds(t));

            // DateOnly TEXT + numeric (DayNumber)
            ms.SetConverter<DateOnly, string>(d => SxmColumnDataConverters.dateOnlyToString(d));
            ms.SetConverter<string, DateOnly>(s => SxmColumnDataConverters.dateOnlyFromString(s));
            ms.SetConverter<DateOnly, long>(d => SxmColumnDataConverters.dateOnlyToUnixDayNumber(d));
            ms.SetConverter<long, DateOnly>(l => SxmColumnDataConverters.dateOnlyFromUnixDayNumber(l));

            // TimeOnly TEXT + numeric (Ticks)
            ms.SetConverter<TimeOnly, string>(t => SxmColumnDataConverters.timeOnlyToString(t));
            ms.SetConverter<string, TimeOnly>(s => SxmColumnDataConverters.timeOnlyFromString(s));
            ms.SetConverter<TimeOnly, long>(t => SxmColumnDataConverters.timeOnlyToTotalMilliseconds(t));
            ms.SetConverter<long, TimeOnly>(ticks => SxmColumnDataConverters.timeOnlyFromTotalMilliseconds(ticks));

            // TimeSpan TEXT + numeric (Ticks)
            ms.SetConverter<TimeSpan, string>(t => SxmColumnDataConverters.timeSpanToString(t));
            ms.SetConverter<string, TimeSpan>(s => SxmColumnDataConverters.timeSpanFromString(s));
            ms.SetConverter<TimeSpan, long>(t => SxmColumnDataConverters.timeSpanToTotalMilliseconds(t));
            ms.SetConverter<long, TimeSpan>(ticks => SxmColumnDataConverters.timeSpanFromTotalMilliseconds(ticks));

            // DateTimeOffset TEXT + numeric (Unix ms)
            ms.SetConverter<DateTimeOffset, string>(dto => SxmColumnDataConverters.dateTimeOffsetToString(dto));
            ms.SetConverter<string, DateTimeOffset>(s => SxmColumnDataConverters.dateTimeOffsetFromString(s));
            ms.SetConverter<DateTimeOffset, long>(dto => SxmColumnDataConverters.dateTimeOffsetToUnixTimeMilliseconds(dto));
            ms.SetConverter<long, DateTimeOffset>(msVal => SxmColumnDataConverters.dateTimeOffsetFromUnixTimeMilliseconds(msVal));

            // Guid TEXT + byte[]
            ms.SetConverter<Guid, string>(g => SxmColumnDataConverters.guidToString(g));
            ms.SetConverter<string, Guid>(s => SxmColumnDataConverters.guidFromString(s));
            ms.SetConverter<Guid, byte[]>(g => SxmColumnDataConverters.guidToRfc4122Bytes(g));
            ms.SetConverter<byte[], Guid>(b => SxmColumnDataConverters.guidFromRfc4122Bytes(b));

            return ms;
        }
    }
}