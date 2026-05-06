using System;

namespace SQLiteXM
{
    /// <summary>
    /// Helper methods to decide whether to wrap or rethrow exceptions according to project policy.
    /// </summary>
    internal static class ExceptionHelper
    {
        /// <summary>
        /// Determines whether an exception should be rethrown unchanged.
        /// </summary>
        /// <param name="ex">The exception to evaluate.</param>
        /// <returns>
        /// <c>true</c> if the exception should not be wrapped; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Exceptions that represent cancellation, fatal runtime conditions,
        /// existing <see cref="SxmException"/> instances, or programmer misuse
        /// are allowed to propagate without wrapping.
        /// </para>
        /// <para>
        /// This preserves expected .NET exception behavior while ensuring that
        /// operational database failures are normalized through
        /// <see cref="SxmException"/>.
        /// </para>
        /// </remarks>
        internal static bool IsNonWrappable(Exception ex)
        {
            // Prefer checking the concrete provider type instead of the Source string.
            if (ex is Microsoft.Data.Sqlite.SqliteException)
                return true;

            return ex is SxmException
                   || ex is OperationCanceledException
                   || ex is TaskCanceledException
                   || ex is OutOfMemoryException
                   || ex is StackOverflowException
                   || ex is AccessViolationException
                   || ex is AppDomainUnloadedException
                   || ex is ThreadAbortException
                   || ex is ThreadInterruptedException
                   || ex is ArgumentException
                   || ex is InvalidOperationException
                   || ex is NotSupportedException
                   || ex is NotImplementedException;
        }

        /// <summary>
        /// Normalizes any exception into a <see cref="SxmException"/> while preserving
        /// important provider-specific metadata and avoiding double-wrapping.
        /// </summary>
        /// <remarks>
        /// DESIGN GOALS:
        /// 
        /// 1. Preserve meaning:
        ///    If the exception is already an SxmException, return it unchanged.
        ///    This guarantees we never lose an existing SxmErrorCode or metadata.
        ///
        /// 2. Preserve provider intelligence:
        ///    SqliteException contains valuable data (ErrorCode). We route through the
        ///    dedicated SxmException constructor so that:
        ///        - sxmErrorCode = SqliteException
        ///        - sqliteErrorCode is captured
        ///
        /// 3. Provide consistent wrapping:
        ///    All other exceptions become SxmException so callers always receive a
        ///    predictable contract from the ORM.
        ///
        /// 4. Preserve stack traces:
        ///    We never use "throw ex". The original exception remains InnerException.
        ///
        /// 5. Avoid unnecessary allocations:
        ///    If no message override is supplied, use the simpler constructor.
        /// </remarks>
        internal static SxmException Wrap(Exception ex, string? message = null)
        {
            // ------------------------------------------------------------
            // Case 1: Already normalized — return as-is.
            // ------------------------------------------------------------
            if (ex is SxmException sxm)
                return sxm;

            // ------------------------------------------------------------
            // Case 2: Provider-specific exception (SQLite).
            //
            // IMPORTANT:
            // Your SxmException(SqliteException) constructor already sets:
            //   Data["sxmErrorCode"] = SqliteException
            //   Data["sqliteErrorCode"] = sqliteEx.ErrorCode
            //
            // We preserve that behavior here.
            // ------------------------------------------------------------
            if (ex is Microsoft.Data.Sqlite.SqliteException sqliteEx)
            {
                // If caller supplied an operation message, prefer it.
                if (!string.IsNullOrWhiteSpace(message))
                    return new SxmException(message, sqliteEx);

                return new SxmException(sqliteEx);
            }

            // ------------------------------------------------------------
            // Case 3: General exception.
            //
            // We normalize everything else into SxmException so that
            // upstream layers never need to handle arbitrary exception types.
            // ------------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(message))
                return new SxmException(message, ex);

            return new SxmException(ex);
        }
    }
}