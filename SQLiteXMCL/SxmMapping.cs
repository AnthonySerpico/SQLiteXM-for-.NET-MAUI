using LinqToDB;
using LinqToDB.Mapping;
using System.Globalization;

namespace SQLiteXM
{
    public static class SxmMapping
    {
        private static readonly Lazy<MappingSchema> _schema = new(Build);
        public static MappingSchema Schema => _schema.Value;

        private static MappingSchema Build()
        {
            var ms = new MappingSchema();

            // DateTime ticks (INTEGER) default
            ms.SetConverter<DateTime, long>(d => d.Ticks);
            ms.SetConverter<long, DateTime>(t => new DateTime(t));

            // DateTime TEXT (ISO 8601)
            ms.SetConverter<DateTime, string>(d => d.ToString("o", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateTime>(s => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            // decimal / ulong TEXT
            ms.SetConverter<decimal, string>(d => d.ToString(CultureInfo.InvariantCulture));
            ms.SetConverter<string, decimal>(s => decimal.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<ulong, string>(u => u.ToString(CultureInfo.InvariantCulture));
            ms.SetConverter<string, ulong>(s => ulong.Parse(s, CultureInfo.InvariantCulture));

            // DateOnly TEXT + numeric (DayNumber)
            ms.SetConverter<DateOnly, string>(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateOnly>(s => DateOnly.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<DateOnly, int>(d => d.DayNumber);
            ms.SetConverter<int, DateOnly>(n => DateOnly.FromDayNumber(n));
            ms.SetConverter<long, DateOnly>(l => DateOnly.FromDayNumber((int)l)); // in case stored as long

            // TimeOnly TEXT + numeric (Ticks)
            ms.SetConverter<TimeOnly, string>(t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture));
            ms.SetConverter<string, TimeOnly>(s => TimeOnly.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<TimeOnly, long>(t => t.Ticks);
            ms.SetConverter<long, TimeOnly>(ticks => new TimeOnly(ticks));

            // TimeSpan TEXT + numeric (Ticks)
            ms.SetConverter<TimeSpan, string>(t => t.ToString("c", CultureInfo.InvariantCulture));
            ms.SetConverter<string, TimeSpan>(s => TimeSpan.Parse(s, CultureInfo.InvariantCulture));
            ms.SetConverter<TimeSpan, long>(t => t.Ticks);
            ms.SetConverter<long, TimeSpan>(ticks => new TimeSpan(ticks));

            // DateTimeOffset TEXT + numeric (Unix ms)
            ms.SetConverter<DateTimeOffset, string>(dto => dto.ToString("o", CultureInfo.InvariantCulture));
            ms.SetConverter<string, DateTimeOffset>(s => DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            ms.SetConverter<DateTimeOffset, long>(dto => dto.ToUnixTimeMilliseconds());
            ms.SetConverter<long, DateTimeOffset>(msVal => DateTimeOffset.FromUnixTimeMilliseconds(msVal));

            return ms;
        }
    }
}