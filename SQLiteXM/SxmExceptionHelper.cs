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
        /// Two distinct groups are treated as non-wrappable, for different reasons:
        /// </para>
        /// <para>
        /// 1. <b>Deliberate pass-through.</b> <see cref="Microsoft.Data.Sqlite.SqliteException"/> is part of
        /// SQLiteXM's public failure contract. It propagates unchanged so callers retain direct access to
        /// <c>SqliteErrorCode</c> and <c>SqliteExtendedErrorCode</c>, which are required for recovery
        /// decisions such as retrying on <c>SQLITE_BUSY</c> or <c>SQLITE_LOCKED</c>.
        /// </para>
        /// <para>
        /// 2. <b>Do not interfere.</b> Cancellation, fatal runtime conditions, already-normalized
        /// <see cref="SxmException"/> instances, and programmer misuse are rethrown as-is to preserve
        /// expected .NET exception behavior.
        /// </para>
        /// <para>
        /// Everything else is an operational failure and is normalized through <see cref="SxmException"/>.
        /// </para>
        /// </remarks>
        internal static bool IsNonWrappable(Exception ex)
        {
            return
                   // Group 1: deliberate pass-through — part of the public failure contract.
                   ex is Microsoft.Data.Sqlite.SqliteException

                   // Group 2: do not interfere.
                   || ex is SxmException
                   || ex is OperationCanceledException
                   || ex is OutOfMemoryException
                   || ex is AppDomainUnloadedException
                   || ex is ThreadInterruptedException
                   || ex is ArgumentException
                   || ex is InvalidOperationException
                   || ex is NotSupportedException
                   || ex is NotImplementedException;
        }

        /// <summary>
        /// Attaches operation context to an exception that is about to be rethrown unchanged.
        /// </summary>
        /// <param name="ex">The exception being rethrown.</param>
        /// <param name="context">Human-readable description of the operation that failed.</param>
        /// <remarks>
        /// <para>
        /// Used with the non-wrappable path, where <c>throw;</c> preserves the original exception type
        /// and stack trace but would otherwise leave the caller without any indication of which database,
        /// table, or statement was involved. The context is stored under <c>Data["sxmContext"]</c>.
        /// </para>
        /// <para>
        /// An existing context value is not overwritten, so the innermost (most specific) operation wins
        /// as the exception unwinds through nested calls.
        /// </para>
        /// </remarks>
        internal static void AddContext(Exception ex, string? context)
        {
            if (ex is null || string.IsNullOrWhiteSpace(context))
                return;

            try
            {
                if (!ex.Data.Contains(SxmException.ContextKey))
                    ex.Data[SxmException.ContextKey] = context;
            }
            catch (System.Exception)
            {
                // Exception.Data can be read-only or reject entries for some exception types.
                // Diagnostics must never mask the original failure.
            }
        }

        /// <summary>
        /// Normalizes any exception into a <see cref="SxmException"/> while preserving
        /// important provider-specific metadata and avoiding double-wrapping.
        /// </summary>
        /// <param name="ex">The exception to normalize.</param>
        /// <param name="code">The category describing which operation failed.</param>
        /// <param name="context">Human-readable description of the SQLiteXM operation that failed.</param>
        /// <remarks>
        /// DESIGN GOALS:
        /// 
        /// 1. Preserve meaning:
        ///    If the exception is already an SxmException, return it unchanged.
        ///    This guarantees we never lose an existing SxmErrorCode or metadata.
        ///
        /// 2. Provide consistent wrapping:
        ///    All other exceptions become SxmException so callers always receive a
        ///    predictable contract from the ORM.
        ///
        /// 3. Preserve stack traces:
        ///    We never use "throw ex". The original exception remains InnerException.
        ///
        /// 4. Classify the failure:
        ///    <paramref name="code"/> tells the caller which operation failed without
        ///    requiring them to inspect the inner exception first. The inner exception
        ///    is still carried and remains the authoritative root cause.
        ///
        /// 5. Separate "what failed" from "what we were doing":
        ///    The wrapper's message is taken from <paramref name="ex"/> so the root-cause
        ///    text is visible at the top level without unwrapping — the original exception
        ///    may be a TargetInvocationException whose own message says nothing useful.
        ///    <paramref name="context"/> is attached separately via <see cref="SxmException.Context"/>
        ///    so the two never hold the same string.
        ///
        /// NOTE: SqliteException is intentionally absent here. It is classified as
        /// non-wrappable by <see cref="IsNonWrappable"/> and propagates to callers unchanged,
        /// so it never reaches this method through the standard catch pattern.
        /// </remarks>
        internal static SxmException Wrap(Exception ex, SxmDefines.SxmErrorCode code, string context)
        {
            // ------------------------------------------------------------
            // Case 1: Already normalized — return as-is.
            // ------------------------------------------------------------
            if (ex is SxmException sxm)
                return sxm;

            // ------------------------------------------------------------
            // Case 2: General exception.
            //
            // We normalize everything else into SxmException so that
            // upstream layers never need to handle arbitrary exception types.
            //
            // Surface the original exception's message at the top level. A few
            // exception types carry no message of their own, so fall back to the
            // context string to guarantee the wrapper is never blank.
            // ------------------------------------------------------------
            string message = string.IsNullOrWhiteSpace(ex.Message) ? context : ex.Message;

            // A few call sites construct the inner exception from the same text they pass as
            // context. Storing it twice would add noise without adding information, so the
            // context is dropped when it is identical to the message that is already visible.
            string effectiveContext = string.Equals(message, context, StringComparison.Ordinal) ? string.Empty : context;

            return new SxmException(message, effectiveContext, code, ex);
        }
    }
}