using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    internal class SxmColumnDataConverters
    {
        internal static string decimalToString(decimal d) => d.ToString("G29", CultureInfo.InvariantCulture);
        internal static string uLongToString(ulong ul) => ul.ToString("D20", CultureInfo.InvariantCulture);
        internal static long dateTimeToUnixTimeMilliseconds(DateTime dt) => new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeMilliseconds();
        internal static string dateTimeToString(DateTime dt) => dt.ToString("o", CultureInfo.InvariantCulture);
        internal static string timeSpanToString(TimeSpan ts) => ts.ToString("c", CultureInfo.InvariantCulture);
        internal static long timeSpanToTotalMilliseconds(TimeSpan ts) => (long)ts.TotalMilliseconds;
        internal static long timeOnlyToTotalMilliseconds(TimeOnly to) => (long)to.ToTimeSpan().TotalMilliseconds;
        internal static string timeOnlyToString(TimeOnly to) => to.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
        internal static int dateOnlyToUnixDayNumber(DateOnly d) => d.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
        internal static string dateOnlyToString(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        internal static string guidToString(Guid g) => g.ToString();
        internal static byte[] guidToRfc4122Bytes(Guid g) => GuidStorageHelpers.ToRfc4122Bytes(g);
        internal static long dateTimeOffsetToUnixTimeMilliseconds(DateTimeOffset dto) => dto.ToUnixTimeMilliseconds();
        internal static string dateTimeOffsetToString(DateTimeOffset dto) => dto.ToString("o", CultureInfo.InvariantCulture);


        internal static decimal decimalFromString(string s) => Decimal.Parse(s, CultureInfo.InvariantCulture);
        internal static ulong uLongFromString(string s) => UInt64.Parse(s, CultureInfo.InvariantCulture);
        internal static DateTime dateTimeFromUnixTimeMilliseconds(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
        internal static DateTime dateTimeFromString(string dateTimeString) => DateTime.Parse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        internal static TimeSpan timeSpanFromTotalMilliseconds(long totalMs) => TimeSpan.FromMilliseconds(totalMs);
        internal static TimeSpan timeSpanFromString(string timeSpanString) => TimeSpan.Parse(timeSpanString, CultureInfo.InvariantCulture);
        internal static TimeOnly timeOnlyFromTotalMilliseconds(long totalMs) => TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(totalMs));
        internal static TimeOnly timeOnlyFromString(string timeOnlyString) => TimeOnly.Parse(timeOnlyString, CultureInfo.InvariantCulture);
        internal static DateOnly dateOnlyFromUnixDayNumber(long unixDayNumber) => DateOnly.FromDateTime(DateTime.UnixEpoch).AddDays((int)unixDayNumber);
        internal static DateOnly dateOnlyFromString(string dateOnlyString) => DateOnly.Parse(dateOnlyString, CultureInfo.InvariantCulture);
        internal static Guid guidFromString(string guidString) => Guid.Parse(guidString);
        internal static Guid guidFromRfc4122Bytes(byte[] byteArray) => GuidStorageHelpers.FromRfc4122Bytes(byteArray);
        internal static DateTimeOffset dateTimeOffsetFromUnixTimeMilliseconds(long unixTimeMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs);
        internal static DateTimeOffset dateTimeOffsetFromString(string dateTimeOffsetString) => DateTimeOffset.Parse(dateTimeOffsetString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
