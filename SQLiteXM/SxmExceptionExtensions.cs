using System;

namespace SQLiteXM
{
    /// <summary>
    /// Extension methods for reading SQLiteXM diagnostic metadata from any exception.
    /// </summary>
    public static class SxmExceptionExtensions
    {
        /// <summary>
        /// Gets the SQLiteXM operation context attached to an exception, or an empty string if none was recorded.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns>
        /// A human-readable description of the SQLiteXM operation that failed, or
        /// <see cref="string.Empty"/> if no context was attached.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Works for every exception SQLiteXM can surface. On a <see cref="SxmException"/> this returns the
        /// same value as <see cref="SxmException.Context"/>. Its real purpose is the pass-through path:
        /// a <see cref="Microsoft.Data.Sqlite.SqliteException"/> has no SQLiteXM properties, so this is the
        /// supported way to read the context SQLiteXM attached to it before rethrowing.
        /// </para>
        /// <para>
        /// Never returns <see langword="null"/>, so the result can be logged or concatenated directly.
        /// An empty result means no context was recorded — for example on a validation guard that was
        /// thrown rather than wrapped.
        /// </para>
        /// <para>
        /// Intended for logging and diagnostics only. Do not branch on the text; use
        /// <see cref="SxmException.ErrorCode"/> instead.
        /// </para>
        /// </remarks>
        public static string GetSxmContext(this Exception ex)
        {
            if (ex is null)
                return string.Empty;

            try
            {
                return ex.Data[SxmException.ContextKey] as string ?? string.Empty;
            }
            catch (Exception)
            {
                // Exception.Data can be restricted for some exception types.
                // Diagnostics must never mask the original failure.
                return string.Empty;
            }
        }
    }
}
