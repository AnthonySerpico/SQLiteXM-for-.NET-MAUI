using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to convert CLR types to database-storable representations and back.
    /// Methods use culture-invariant formats and stable representations suitable for storing
    /// in SQLite columns (strings, integers or byte arrays). Intended for internal use
    /// by the SQLiteXM library.
    /// </summary>
    internal class SxmColumnDataConverters
    {
        /// <summary>
        /// Convert a <see cref="decimal"/> to an invariant string using the "G29" format
        /// to avoid losing precision when round-tripping.
        /// </summary>
        /// <param name="d">The decimal value to convert.</param>
        /// <returns>Culture-invariant string representation of <paramref name="d"/>.</returns>
        internal static string decimalToString(decimal d) => d.ToString("G29", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert an unsigned 64-bit integer to a zero-padded decimal string with width 20.
        /// This produces a lexicographically sortable fixed-width representation.
        /// </summary>
        /// <param name="ul">The <see cref="ulong"/> value to convert.</param>
        /// <returns>Zero-padded decimal string representation of <paramref name="ul"/>.</returns>
        internal static string uLongToString(ulong ul) => ul.ToString("D20", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="DateTime"/> to Unix time in milliseconds.
        /// The <see cref="DateTime"/> is converted to UTC before computing the Unix timestamp.
        /// </summary>
        /// <param name="dt">The <see cref="DateTime"/> to convert.</param>
        /// <returns>Unix epoch milliseconds representing <paramref name="dt"/> in UTC.</returns>
        internal static long dateTimeToUnixTimeMilliseconds(DateTime dt) => new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeMilliseconds();

        /// <summary>
        /// Convert a <see cref="DateTime"/> to an ISO 8601 round-trip string ("o") using invariant culture.
        /// </summary>
        /// <param name="dt">The <see cref="DateTime"/> to convert.</param>
        /// <returns>ISO 8601 string representation of <paramref name="dt"/>.</returns>
        internal static string dateTimeToString(DateTime dt) => dt.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="TimeSpan"/> to its constant ("c") string representation using invariant culture.
        /// </summary>
        /// <param name="ts">The <see cref="TimeSpan"/> to convert.</param>
        /// <returns>Invariant string representation of <paramref name="ts"/>.</returns>
        internal static string timeSpanToString(TimeSpan ts) => ts.ToString("c", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="TimeSpan"/> to total milliseconds (rounded via cast to <see cref="long"/>).
        /// </summary>
        /// <param name="ts">The <see cref="TimeSpan"/> to convert.</param>
        /// <returns>Total milliseconds contained in <paramref name="ts"/> as a <see cref="long"/>.</returns>
        internal static long timeSpanToTotalMilliseconds(TimeSpan ts) => (long)ts.TotalMilliseconds;

        /// <summary>
        /// Convert a <see cref="TimeOnly"/> to total milliseconds since midnight.
        /// </summary>
        /// <param name="to">The <see cref="TimeOnly"/> value to convert.</param>
        /// <returns>Total milliseconds since midnight represented by <paramref name="to"/>.</returns>
        internal static long timeOnlyToTotalMilliseconds(TimeOnly to) => (long)to.ToTimeSpan().TotalMilliseconds;

        /// <summary>
        /// Convert a <see cref="TimeOnly"/> to a fixed precision string "HH:mm:ss.fffffff" using invariant culture.
        /// </summary>
        /// <param name="to">The <see cref="TimeOnly"/> to convert.</param>
        /// <returns>String representation of <paramref name="to"/> with fractional seconds.</returns>
        internal static string timeOnlyToString(TimeOnly to) => to.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="DateOnly"/> to the number of days since the Unix epoch (1970-01-01).
        /// </summary>
        /// <param name="d">The <see cref="DateOnly"/> to convert.</param>
        /// <returns>Day number offset from Unix epoch (0 for 1970-01-01).</returns>
        internal static int dateOnlyToUnixDayNumber(DateOnly d) => d.DayNumber - DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;

        /// <summary>
        /// Convert a <see cref="DateOnly"/> to a "yyyy-MM-dd" invariant string.
        /// </summary>
        /// <param name="d">The <see cref="DateOnly"/> to convert.</param>
        /// <returns>ISO date string for <paramref name="d"/>.</returns>
        internal static string dateOnlyToString(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>
        /// Convert a <see cref="Guid"/> to its canonical string representation.
        /// </summary>
        /// <param name="g">The <see cref="Guid"/> to convert.</param>
        /// <returns>String representation of <paramref name="g"/>.</returns>
        internal static string guidToString(Guid g) => g.ToString();

        /// <summary>
        /// Convert a <see cref="Guid"/> to its 16-byte RFC 4122 representation.
        /// Uses <see cref="GuidStorageHelpers"/> helper for the conversion.
        /// </summary>
        /// <param name="g">The <see cref="Guid"/> to convert.</param>
        /// <returns>16-byte RFC 4122 representation of <paramref name="g"/>.</returns>
        internal static byte[] guidToRfc4122Bytes(Guid g) => GuidStorageHelpers.ToRfc4122Bytes(g);

        /// <summary>
        /// Convert a <see cref="DateTimeOffset"/> to Unix time in milliseconds.
        /// </summary>
        /// <param name="dto">The <see cref="DateTimeOffset"/> to convert.</param>
        /// <returns>Unix epoch milliseconds representing <paramref name="dto"/>.</returns>
        internal static long dateTimeOffsetToUnixTimeMilliseconds(DateTimeOffset dto) => dto.ToUnixTimeMilliseconds();

        /// <summary>
        /// Convert a <see cref="DateTimeOffset"/> to an ISO 8601 round-trip string ("o") using invariant culture.
        /// </summary>
        /// <param name="dto">The <see cref="DateTimeOffset"/> to convert.</param>
        /// <returns>ISO 8601 string representation of <paramref name="dto"/>.</returns>
        internal static string dateTimeOffsetToString(DateTimeOffset dto) => dto.ToString("o", CultureInfo.InvariantCulture);


        /// <summary>
        /// Parse a decimal from its invariant string representation.
        /// </summary>
        /// <param name="s">String containing the decimal value.</param>
        /// <returns>Parsed <see cref="decimal"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="s"/> is not a valid decimal.</exception>
        internal static decimal decimalFromString(string s) => Decimal.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>
        /// Parse an unsigned 64-bit integer from its invariant string representation.
        /// </summary>
        /// <param name="s">String containing the unsigned long decimal digits.</param>
        /// <returns>Parsed <see cref="ulong"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="s"/> is not a valid unsigned integer.</exception>
        internal static ulong uLongFromString(string s) => UInt64.Parse(s, CultureInfo.InvariantCulture);

        /// <summary>
        /// Create a <see cref="DateTime"/> from Unix time in milliseconds (UTC).
        /// </summary>
        /// <param name="unixMs">Unix epoch milliseconds.</param>
        /// <returns><see cref="DateTime"/> in UTC corresponding to <paramref name="unixMs"/>.</returns>
        internal static DateTime dateTimeFromUnixTimeMilliseconds(long unixMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;

        /// <summary>
        /// Parse a <see cref="DateTime"/> from an ISO 8601 round-trip string using invariant culture.
        /// </summary>
        /// <param name="dateTimeString">The ISO 8601 date/time string.</param>
        /// <returns>Parsed <see cref="DateTime"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateTimeString"/> is not a valid ISO 8601 date/time.</exception>
        internal static DateTime dateTimeFromString(string dateTimeString) => DateTime.Parse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        /// <summary>
        /// Create a <see cref="TimeSpan"/> from total milliseconds.
        /// </summary>
        /// <param name="totalMs">Total milliseconds.</param>
        /// <returns><see cref="TimeSpan"/> equivalent to <paramref name="totalMs"/> milliseconds.</returns>
        internal static TimeSpan timeSpanFromTotalMilliseconds(long totalMs) => TimeSpan.FromMilliseconds(totalMs);

        /// <summary>
        /// Parse a <see cref="TimeSpan"/> from its invariant string representation.
        /// </summary>
        /// <param name="timeSpanString">The time span string.</param>
        /// <returns>Parsed <see cref="TimeSpan"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="timeSpanString"/> is not a valid TimeSpan.</exception>
        internal static TimeSpan timeSpanFromString(string timeSpanString) => TimeSpan.Parse(timeSpanString, CultureInfo.InvariantCulture);

        /// <summary>
        /// Create a <see cref="TimeOnly"/> from total milliseconds since midnight.
        /// </summary>
        /// <param name="totalMs">Total milliseconds since midnight.</param>
        /// <returns><see cref="TimeOnly"/> corresponding to <paramref name="totalMs"/>.</returns>
        internal static TimeOnly timeOnlyFromTotalMilliseconds(long totalMs) => TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(totalMs));

        /// <summary>
        /// Parse a <see cref="TimeOnly"/> from an invariant string.
        /// </summary>
        /// <param name="timeOnlyString">The time string (e.g. "13:45:30").</param>
        /// <returns>Parsed <see cref="TimeOnly"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="timeOnlyString"/> is not a valid time-only string.</exception>
        internal static TimeOnly timeOnlyFromString(string timeOnlyString) => TimeOnly.Parse(timeOnlyString, CultureInfo.InvariantCulture);

        /// <summary>
        /// Create a <see cref="DateOnly"/> from a Unix day number (days since 1970-01-01).
        /// </summary>
        /// <param name="unixDayNumber">Number of days since Unix epoch.</param>
        /// <returns><see cref="DateOnly"/> corresponding to the provided day offset.</returns>
        internal static DateOnly dateOnlyFromUnixDayNumber(long unixDayNumber) => DateOnly.FromDateTime(DateTime.UnixEpoch).AddDays((int)unixDayNumber);

        /// <summary>
        /// Parse a <see cref="DateOnly"/> from an invariant "yyyy-MM-dd" date string.
        /// </summary>
        /// <param name="dateOnlyString">Date string in "yyyy-MM-dd" format.</param>
        /// <returns>Parsed <see cref="DateOnly"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateOnlyString"/> is not a valid date-only string.</exception>
        internal static DateOnly dateOnlyFromString(string dateOnlyString) => DateOnly.Parse(dateOnlyString, CultureInfo.InvariantCulture);

        /// <summary>
        /// Parse a <see cref="Guid"/> from its canonical string representation.
        /// </summary>
        /// <param name="guidString">The GUID string to parse.</param>
        /// <returns>Parsed <see cref="Guid"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="guidString"/> is not a valid GUID.</exception>
        internal static Guid guidFromString(string guidString) => Guid.Parse(guidString);

        /// <summary>
        /// Parse a <see cref="Guid"/> from its 16-byte RFC 4122 representation.
        /// Uses <see cref="GuidStorageHelpers"/> helper for the conversion.
        /// </summary>
        /// <param name="byteArray">16-byte RFC 4122 byte array.</param>
        /// <returns>Parsed <see cref="Guid"/>.</returns>
        internal static Guid guidFromRfc4122Bytes(byte[] byteArray) => GuidStorageHelpers.FromRfc4122Bytes(byteArray);

        /// <summary>
        /// Create a <see cref="DateTimeOffset"/> from Unix time in milliseconds.
        /// </summary>
        /// <param name="unixTimeMs">Unix epoch milliseconds.</param>
        /// <returns><see cref="DateTimeOffset"/> representing <paramref name="unixTimeMs"/>.</returns>
        internal static DateTimeOffset dateTimeOffsetFromUnixTimeMilliseconds(long unixTimeMs) => DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs);

        /// <summary>
        /// Parse a <see cref="DateTimeOffset"/> from an ISO 8601 round-trip string using invariant culture.
        /// </summary>
        /// <param name="dateTimeOffsetString">The ISO 8601 date/time string.</param>
        /// <returns>Parsed <see cref="DateTimeOffset"/>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateTimeOffsetString"/> is not a valid ISO 8601 date/time.</exception>
        internal static DateTimeOffset dateTimeOffsetFromString(string dateTimeOffsetString) => DateTimeOffset.Parse(dateTimeOffsetString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}