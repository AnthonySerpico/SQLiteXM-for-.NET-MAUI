using LinqToDB.Mapping;
using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SQLiteXM
{
    public static class SxmMapping
    {
        private static readonly Lazy<MappingSchema> _schema = new(Build);
        public static MappingSchema Schema => _schema.Value;

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