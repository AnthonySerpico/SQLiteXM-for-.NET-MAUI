using System;
using System.Globalization;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to convert CLR types to database-storable representations and back.
    /// Methods use culture-invariant formats and stable representations suitable for storing
    /// in SQLite columns (strings, integers or byte arrays). Intended for internal use
    /// by the SQLiteXM library.
    /// </summary>
    internal static class SxmColumnDataConverters
    {
        // The "To" methods below convert CLR types (nullable) to string or numeric representations
        // suitable for storage in SQLite columns. Null inputs produce null outputs so callers can
        // map database NULLs directly.

        /// <summary>
        /// Convert a nullable <see cref="decimal"/> to an invariant string using the "G29" format
        /// to avoid losing precision when round-tripping. Returns <c>null</c> when <paramref name="d"/> is <c>null</c>.
        /// </summary>
        /// <param name="d">The nullable decimal value to convert.</param>
        /// <returns>Culture-invariant string representation of <paramref name="d"/>, or <c>null</c>.</returns>
        internal static string? DecimalToString(decimal? d)
        {
            return d.HasValue ? d.Value.ToString("G29", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable unsigned 64-bit integer to a zero-padded decimal string with width 20.
        /// Returns <c>null</c> when <paramref name="ul"/> is <c>null</c>.
        /// </summary>
        /// <param name="ul">The nullable <see cref="ulong"/> value to convert.</param>
        /// <returns>Zero-padded decimal string representation of <paramref name="ul"/>, or <c>null</c>.</returns>
        internal static string? ULongToString(ulong? ul)
        {
            return ul.HasValue ? ul.Value.ToString("D20", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="DateTime"/> to Unix time in milliseconds.
        /// The <see cref="DateTime"/> is converted to UTC before computing the Unix timestamp.
        /// Returns <c>null</c> when <paramref name="dt"/> is <c>null</c>.
        /// </summary>
        /// <param name="dt">The nullable <see cref="DateTime"/> to convert.</param>
        /// <returns>Unix epoch milliseconds representing <paramref name="dt"/> in UTC, or <c>null</c>.</returns>
        internal static long? DateTimeToUnixTimeMilliseconds(DateTime? dt)
        {
            return dt.HasValue ? new DateTimeOffset(dt.Value.ToUniversalTime()).ToUnixTimeMilliseconds() : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="DateTime"/> to an ISO 8601 round-trip string ("o") using invariant culture.
        /// Returns <c>null</c> when <paramref name="dt"/> is <c>null</c>.
        /// </summary>
        /// <param name="dt">The nullable <see cref="DateTime"/> to convert.</param>
        /// <returns>ISO 8601 string representation of <paramref name="dt"/>, or <c>null</c>.</returns>
        internal static string? DateTimeToString(DateTime? dt)
        {
            return dt.HasValue ? dt.Value.ToString("o", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeSpan"/> to its constant ("c") string representation using invariant culture.
        /// Returns <c>null</c> when <paramref name="ts"/> is <c>null</c>.
        /// </summary>
        /// <param name="ts">The nullable <see cref="TimeSpan"/> to convert.</param>
        /// <returns>Invariant string representation of <paramref name="ts"/>, or <c>null</c>.</returns>
        internal static string? TimeSpanToString(TimeSpan? ts)
        {
            return ts.HasValue ? ts.Value.ToString("c", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeSpan"/> to total milliseconds (rounded via cast to <see cref="long"/>).
        /// Returns <c>null</c> when <paramref name="ts"/> is <c>null</c>.
        /// </summary>
        /// <param name="ts">The nullable <see cref="TimeSpan"/> to convert.</param>
        /// <returns>Total milliseconds contained in <paramref name="ts"/>, or <c>null</c>.</returns>
        internal static long? TimeSpanToTotalMilliseconds(TimeSpan? ts)
        {
            return ts.HasValue ? (long)ts.Value.TotalMilliseconds : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeOnly"/> to total milliseconds since midnight.
        /// Returns <c>null</c> when <paramref name="to"/> is <c>null</c>.
        /// </summary>
        /// <param name="to">The nullable <see cref="TimeOnly"/> value to convert.</param>
        /// <returns>Total milliseconds since midnight represented by <paramref name="to"/>, or <c>null</c>.</returns>
        internal static long? TimeOnlyToTotalMilliseconds(TimeOnly? to)
        {
            return to.HasValue ? (long)to.Value.ToTimeSpan().TotalMilliseconds : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeSpan"/> to total ticks.
        /// Returns <c>null</c> when <paramref name="ts"/> is <c>null</c>.
        /// </summary>
        /// <param name="ts">The nullable <see cref="TimeSpan"/> to convert.</param>
        /// <returns>Total ticks contained in <paramref name="ts"/>, or <c>null</c>.</returns>
        internal static long? TimeSpanToTotalTicks(TimeSpan? ts)
        {
            return ts.HasValue ? ts.Value.Ticks : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeOnly"/> to total ticks since midnight.
        /// Returns <c>null</c> when <paramref name="to"/> is <c>null</c>.
        /// </summary>
        /// <param name="to">The nullable <see cref="TimeOnly"/> value to convert.</param>
        /// <returns>Total ticks since midnight represented by <paramref name="to"/>, or <c>null</c>.</returns>
        internal static long? TimeOnlyToTotalTicks(TimeOnly? to)
        {
            return to.HasValue ? to.Value.Ticks : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="TimeOnly"/> to a fixed precision string "HH:mm:ss.fffffff" using invariant culture.
        /// Returns <c>null</c> when <paramref name="to"/> is <c>null</c>.
        /// </summary>
        /// <param name="to">The nullable <see cref="TimeOnly"/> to convert.</param>
        /// <returns>String representation of <paramref name="to"/>, or <c>null</c>.</returns>
        internal static string? TimeOnlyToString(TimeOnly? to)
        {
            return to.HasValue ? to.Value.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="DateOnly"/> to the number of days since the Unix epoch (1970-01-01).
        /// Returns <c>null</c> when <paramref name="d"/> is <c>null</c>.
        /// </summary>
        /// <param name="d">The nullable <see cref="DateOnly"/> to convert.</param>
        /// <returns>Day number offset from Unix epoch, or <c>null</c>.</returns>
        internal static int? DateOnlyToUnixDayNumber(DateOnly? d)
        {
            if (!d.HasValue) return null;
            int baseDay = DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
            return d.Value.DayNumber - baseDay;
        }

        /// <summary>
        /// Convert a nullable <see cref="DateOnly"/> to a "yyyy-MM-dd" invariant string.
        /// Returns <c>null</c> when <paramref name="d"/> is <c>null</c>.
        /// </summary>
        /// <param name="d">The nullable <see cref="DateOnly"/> to convert.</param>
        /// <returns>ISO date string for <paramref name="d"/>, or <c>null</c>.</returns>
        internal static string? DateOnlyToString(DateOnly? d)
        {
            return d.HasValue ? d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="Guid"/> to its canonical string representation.
        /// Returns <c>null</c> when <paramref name="g"/> is <c>null</c>.
        /// </summary>
        /// <param name="g">The nullable <see cref="Guid"/> to convert.</param>
        /// <returns>String representation of <paramref name="g"/>, or <c>null</c>.</returns>
        internal static string? GuidToString(Guid? g)
        {
            return g.HasValue ? g.Value.ToString() : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="Guid"/> to its 16-byte RFC 4122 representation (big-endian network byte order).
        /// Returns <c>null</c> when <paramref name="g"/> is <c>null</c>.
        /// </summary>
        /// <remarks>
        /// RFC 4122 format stores all Guid components in big-endian (network) byte order, providing
        /// cross-platform compatibility with Java, Python, PostgreSQL UUID, and other systems.
        /// Use this when interoperability or standards compliance is required.
        /// For .NET-only scenarios with LINQ-to-DB queries, use <see cref="GuidToNativeBytes"/> instead.
        /// </remarks>
        /// <param name="g">The nullable <see cref="Guid"/> to convert.</param>
        /// <returns>16-byte RFC 4122 representation of <paramref name="g"/>, or <c>null</c>.</returns>
        internal static byte[]? GuidToRfc4122Bytes(Guid? g)
        {
            return g.HasValue ? GuidStorageHelpers.ToRfc4122Bytes(g.Value) : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="Guid"/> to its 16-byte native .NET representation (mixed-endian).
        /// Returns <c>null</c> when <paramref name="g"/> is <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Native .NET format uses Guid.ToByteArray() which stores the first 3 components in little-endian
        /// and the last component in big-endian. This is the default format expected by LINQ-to-DB and provides
        /// best performance for .NET-only applications. For cross-platform UUID compatibility, use
        /// <see cref="GuidToRfc4122Bytes"/> instead.
        /// </remarks>
        /// <param name="g">The nullable <see cref="Guid"/> to convert.</param>
        /// <returns>16-byte native .NET representation of <paramref name="g"/>, or <c>null</c>.</returns>
        internal static byte[]? GuidToNativeBytes(Guid? g)
        {
            return g?.ToByteArray();
        }

        /// <summary>
        /// Convert a nullable <see cref="DateTimeOffset"/> to Unix time in milliseconds.
        /// Returns <c>null</c> when <paramref name="dto"/> is <c>null</c>.
        /// </summary>
        /// <param name="dto">The nullable <see cref="DateTimeOffset"/> to convert.</param>
        /// <returns>Unix epoch milliseconds representing <paramref name="dto"/>, or <c>null</c>.</returns>
        internal static long? DateTimeOffsetToUnixTimeMilliseconds(DateTimeOffset? dto)
        {
            return dto.HasValue ? dto.Value.ToUnixTimeMilliseconds() : null;
        }

        /// <summary>
        /// Convert a nullable <see cref="DateTimeOffset"/> to an ISO 8601 round-trip string ("o") using invariant culture.
        /// Returns <c>null</c> when <paramref name="dto"/> is <c>null</c>.
        /// </summary>
        /// <param name="dto">The nullable <see cref="DateTimeOffset"/> to convert.</param>
        /// <returns>ISO 8601 string representation of <paramref name="dto"/>, or <c>null</c>.</returns>
        internal static string? DateTimeOffsetToString(DateTimeOffset? dto)
        {
            return dto.HasValue ? dto.Value.ToString("o", CultureInfo.InvariantCulture) : null;
        }


        // The "From" methods below are the inverse conversions and accept database values that may be NULL.
        // Null database inputs produce null CLR outputs.

        /// <summary>
        /// Parse a decimal from its invariant string representation. Returns <c>null</c> when <paramref name="s"/> is <c>null</c>.
        /// </summary>
        /// <param name="s">String containing the decimal value, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="decimal"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="s"/> is not a valid decimal.</exception>
        internal static decimal? DecimalFromString(string? s)
        {
            return s is null ? (decimal?)null : Decimal.Parse(s, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse an unsigned 64-bit integer from its invariant string representation.
        /// Returns <c>null</c> when <paramref name="s"/> is <c>null</c>.
        /// </summary>
        /// <param name="s">String containing the unsigned long decimal digits, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="ulong"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="s"/> is not a valid unsigned integer.</exception>
        internal static ulong? ULongFromString(string? s)
        {
            return s is null ? (ulong?)null : UInt64.Parse(s, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Create a <see cref="DateTime"/> from Unix time in milliseconds (UTC).
        /// Returns <c>null</c> when <paramref name="unixMs"/> is <c>null</c>.
        /// </summary>
        /// <param name="unixMs">Unix epoch milliseconds, or <c>null</c>.</param>
        /// <returns><see cref="DateTime"/> in UTC corresponding to <paramref name="unixMs"/>, or <c>null</c>.</returns>
        internal static DateTime? DateTimeFromUnixTimeMilliseconds(long? unixMs)
        {
            return unixMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs.Value).UtcDateTime : (DateTime?)null;
        }

        /// <summary>
        /// Parse a <see cref="DateTime"/> from an ISO 8601 round-trip string using invariant culture.
        /// Returns <c>null</c> when <paramref name="dateTimeString"/> is <c>null</c>.
        /// </summary>
        /// <param name="dateTimeString">The ISO 8601 date/time string, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="DateTime"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateTimeString"/> is not a valid ISO 8601 date/time.</exception>
        internal static DateTime? DateTimeFromString(string? dateTimeString)
        {
            return dateTimeString is null ? (DateTime?)null : DateTime.Parse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        /// <summary>
        /// Create a <see cref="TimeSpan"/> from total milliseconds. Returns <c>null</c> when <paramref name="totalMs"/> is <c>null</c>.
        /// </summary>
        /// <param name="totalMs">Total milliseconds, or <c>null</c>.</param>
        /// <returns><see cref="TimeSpan"/> equivalent to <paramref name="totalMs"/> milliseconds, or <c>null</c>.</returns>
        internal static TimeSpan? TimeSpanFromTotalMilliseconds(long? totalMs)
        {
            return totalMs.HasValue ? TimeSpan.FromMilliseconds(totalMs.Value) : (TimeSpan?)null;
        }

        /// <summary>
        /// Create a <see cref="TimeSpan"/> from total ticks.
        /// Returns <c>null</c> when <paramref name="totalTicks"/> is <c>null</c>.
        /// </summary>
        /// <param name="totalTicks">Total ticks, or <c>null</c>.</param>
        /// <returns><see cref="TimeSpan"/> equivalent to <paramref name="totalTicks"/> ticks, or <c>null</c>.</returns>
        internal static TimeSpan? TimeSpanFromTotalTicks(long? totalTicks)
        {
            return totalTicks.HasValue ? TimeSpan.FromTicks(totalTicks.Value) : (TimeSpan?)null;
        }

        /// <summary>
        /// Create a <see cref="TimeOnly"/> from total ticks since midnight.
        /// Returns <c>null</c> when <paramref name="totalTicks"/> is <c>null</c>.
        /// </summary>
        /// <param name="totalTicks">Total ticks since midnight, or <c>null</c>.</param>
        /// <returns><see cref="TimeOnly"/> corresponding to <paramref name="totalTicks"/>, or <c>null</c>.</returns>
        internal static TimeOnly? TimeOnlyFromTotalTicks(long? totalTicks)
        {
            return totalTicks.HasValue ? new TimeOnly(totalTicks.Value) : (TimeOnly?)null;
        }

        /// <summary>
        /// Parse a <see cref="TimeSpan"/> from its invariant string representation.
        /// Returns <c>null</c> when <paramref name="timeSpanString"/> is <c>null</c>.
        /// </summary>
        /// <param name="timeSpanString">The time span string, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="TimeSpan"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="timeSpanString"/> is not a valid TimeSpan.</exception>
        internal static TimeSpan? TimeSpanFromString(string? timeSpanString)
        {
            return timeSpanString is null ? (TimeSpan?)null : TimeSpan.Parse(timeSpanString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Create a <see cref="TimeOnly"/> from total milliseconds since midnight.
        /// Returns <c>null</c> when <paramref name="totalMs"/> is <c>null</c>.
        /// </summary>
        /// <param name="totalMs">Total milliseconds since midnight, or <c>null</c>.</param>
        /// <returns><see cref="TimeOnly"/> corresponding to <paramref name="totalMs"/>, or <c>null</c>.</returns>
        internal static TimeOnly? TimeOnlyFromTotalMilliseconds(long? totalMs)
        {
            return totalMs.HasValue ? TimeOnly.FromTimeSpan(TimeSpan.FromMilliseconds(totalMs.Value)) : (TimeOnly?)null;
        }

        /// <summary>
        /// Parse a <see cref="TimeOnly"/> from an invariant string.
        /// Returns <c>null</c> when <paramref name="timeOnlyString"/> is <c>null</c>.
        /// </summary>
        /// <param name="timeOnlyString">The time string (e.g. "13:45:30"), or <c>null</c>.</param>
        /// <returns>Parsed <see cref="TimeOnly"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="timeOnlyString"/> is not a valid time-only string.</exception>
        internal static TimeOnly? TimeOnlyFromString(string? timeOnlyString)
        {
            return timeOnlyString is null ? (TimeOnly?)null : TimeOnly.Parse(timeOnlyString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Create a <see cref="DateOnly"/> from a Unix day number (days since 1970-01-01).
        /// Returns <c>null</c> when <paramref name="unixDayNumber"/> is <c>null</c>.
        /// </summary>
        /// <param name="unixDayNumber">Number of days since Unix epoch, or <c>null</c>.</param>
        /// <returns><see cref="DateOnly"/> corresponding to the provided day offset, or <c>null</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unixDayNumber"/> is outside the supported <see cref="DateOnly"/> range.</exception>
        internal static DateOnly? DateOnlyFromUnixDayNumber(long? unixDayNumber)
        {
            if (!unixDayNumber.HasValue) return null;

            int baseDay = DateOnly.FromDateTime(DateTime.UnixEpoch).DayNumber;
            long dayNumber = (long)baseDay + unixDayNumber.Value;

            if (dayNumber < DateOnly.MinValue.DayNumber || dayNumber > DateOnly.MaxValue.DayNumber)
                throw new ArgumentOutOfRangeException(nameof(unixDayNumber), "Unix day number is outside the supported DateOnly range.");

            return DateOnly.FromDayNumber((int)dayNumber);
        }

        /// <summary>
        /// Parse a <see cref="DateOnly"/> from an invariant "yyyy-MM-dd" date string.
        /// Returns <c>null</c> when <paramref name="dateOnlyString"/> is <c>null</c>.
        /// </summary>
        /// <param name="dateOnlyString">Date string in "yyyy-MM-dd" format, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="DateOnly"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateOnlyString"/> is not a valid date-only string.</exception>
        internal static DateOnly? DateOnlyFromString(string? dateOnlyString)
        {
            return dateOnlyString is null ? (DateOnly?)null : DateOnly.Parse(dateOnlyString, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parse a <see cref="Guid"/> from its canonical string representation.
        /// Returns <c>null</c> when <paramref name="guidString"/> is <c>null</c>.
        /// </summary>
        /// <param name="guidString">The GUID string to parse, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="Guid"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="guidString"/> is not a valid GUID.</exception>
        internal static Guid? GuidFromString(string? guidString)
        {
            return guidString is null ? (Guid?)null : Guid.Parse(guidString);
        }

        /// <summary>
        /// Parse a <see cref="Guid"/> from its 16-byte RFC 4122 representation (big-endian network byte order).
        /// Returns <c>null</c> when <paramref name="byteArray"/> is <c>null</c>.
        /// When non-<c>null</c> the array must be 16 bytes or an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <remarks>
        /// RFC 4122 format stores all Guid components in big-endian (network) byte order, providing
        /// cross-platform compatibility with Java, Python, PostgreSQL UUID, and other systems.
        /// Use this when interoperability or standards compliance is required.
        /// For .NET-only scenarios with LINQ-to-DB queries, use <see cref="GuidFromNativeBytes"/> instead.
        /// </remarks>
        /// <param name="byteArray">16-byte RFC 4122 byte array, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="Guid"/> or <c>null</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="byteArray"/> is not 16 bytes long when non-<c>null</c>.</exception>
        internal static Guid? GuidFromRfc4122Bytes(byte[]? byteArray)
        {
            if (byteArray is null) return null;
            if (byteArray.Length != 16) throw new ArgumentException("RFC 4122 GUID byte array must be 16 bytes long.", nameof(byteArray));
            return GuidStorageHelpers.FromRfc4122Bytes(byteArray);
        }

        /// <summary>
        /// Parse a <see cref="Guid"/> from its 16-byte native .NET representation (mixed-endian).
        /// Returns <c>null</c> when <paramref name="byteArray"/> is <c>null</c>.
        /// When non-<c>null</c> the array must be 16 bytes or an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <remarks>
        /// Native .NET format uses the byte array produced by Guid.ToByteArray() which stores the first
        /// 3 components in little-endian and the last component in big-endian. This is the default format
        /// expected by LINQ-to-DB and provides best performance for .NET-only applications.
        /// For cross-platform UUID compatibility, use <see cref="GuidFromRfc4122Bytes"/> instead.
        /// </remarks>
        /// <param name="byteArray">16-byte native .NET byte array, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="Guid"/> or <c>null</c>.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="byteArray"/> is not 16 bytes long when non-<c>null</c>.</exception>
        internal static Guid? GuidFromNativeBytes(byte[]? byteArray)
        {
            if (byteArray is null) return null;
            if (byteArray.Length != 16) throw new ArgumentException("Native .NET GUID byte array must be 16 bytes long.", nameof(byteArray));
            return new Guid(byteArray);
        }

        /// <summary>
        /// Create a <see cref="DateTimeOffset"/> from Unix time in milliseconds.
        /// Returns <c>null</c> when <paramref name="unixTimeMs"/> is <c>null</c>.
        /// </summary>
        /// <param name="unixTimeMs">Unix epoch milliseconds, or <c>null</c>.</param>
        /// <returns><see cref="DateTimeOffset"/> representing <paramref name="unixTimeMs"/>, or <c>null</c>.</returns>
        internal static DateTimeOffset? DateTimeOffsetFromUnixTimeMilliseconds(long? unixTimeMs)
        {
            if (!unixTimeMs.HasValue) return null;

            // FromUnixTimeMilliseconds has range limitations
            // Valid range: 0001-01-02T00:00:00.000Z to 9999-12-31T23:59:59.999Z
            // If value is before Unix epoch minimum or after maximum, clamp or throw
            const long minUnixMs = -62135596799999; // 0001-01-02T00:00:00.001Z
            const long maxUnixMs = 253402300799999; // 9999-12-31T23:59:59.999Z

            if (unixTimeMs.Value < minUnixMs)
                return DateTimeOffset.MinValue;
            if (unixTimeMs.Value > maxUnixMs)
                return DateTimeOffset.MaxValue;

            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs.Value);
        }

        /// <summary>
        /// Parse a <see cref="DateTimeOffset"/> from an ISO 8601 round-trip string using invariant culture.
        /// Returns <c>null</c> when <paramref name="dateTimeOffsetString"/> is <c>null</c>.
        /// </summary>
        /// <param name="dateTimeOffsetString">The ISO 8601 date/time string, or <c>null</c>.</param>
        /// <returns>Parsed <see cref="DateTimeOffset"/> or <c>null</c>.</returns>
        /// <exception cref="FormatException">Thrown if <paramref name="dateTimeOffsetString"/> is not a valid ISO 8601 date/time.</exception>
        internal static DateTimeOffset? DateTimeOffsetFromString(string? dateTimeOffsetString)
        {
            return dateTimeOffsetString is null ? (DateTimeOffset?)null : DateTimeOffset.Parse(dateTimeOffsetString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }
}