using Microsoft.Data.Sqlite;

namespace SQLiteXM
{
    /// <summary>
    /// Represents an operational exception produced by SQLiteXM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQLiteXM throws <see cref="SxmException"/> for ORM-level operational failures.
    /// These exceptions provide stable error codes and preserve the originating failure
    /// through the inner exception and exception metadata.
    /// </para>
    /// <para>
    /// Provider exceptions are <b>not</b> wrapped. A <see cref="SqliteException"/> raised by the
    /// underlying SQLite provider propagates to the caller unchanged, so that its
    /// <c>SqliteErrorCode</c> and <c>SqliteExtendedErrorCode</c> remain directly available.
    /// </para>
    /// <para>
    /// Programmer usage errors — such as invalid arguments, unsupported operations,
    /// or incorrect API usage — are surfaced as standard .NET exceptions and are not wrapped.
    /// </para>
    /// <para>
    /// Cancellation and fatal runtime exceptions are never wrapped and are allowed to
    /// propagate unchanged.
    /// </para>
    /// <para>
    /// Callers handling database failures should therefore expect either a
    /// <see cref="SxmException"/> or a <see cref="SqliteException"/>, while allowing framework
    /// and usage exceptions to propagate normally.
    /// </para>
    /// </remarks>
    public sealed class SxmException : Exception
    {
        /// <summary>
        /// The <see cref="Exception.Data"/> key under which SQLiteXM stores the error code.
        /// </summary>
        internal const string ErrorCodeKey = "sxmErrorCode";

        /// <summary>
        /// The <see cref="Exception.Data"/> key under which SQLiteXM stores operation context.
        /// </summary>
        internal const string ContextKey = "sxmContext";

        /// <summary>
        /// Gets the stable library error code identifying the cause of this exception.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the supported way to branch on a SQLiteXM failure. The same value is also stored
        /// under <c>Data["sxmErrorCode"]</c> for logging and diagnostics, but callers should prefer
        /// this property because it is strongly typed.
        /// </para>
        /// <para>
        /// Provider failures are never reported here. A <see cref="SqliteException"/> propagates to the
        /// caller unchanged and must be caught separately.
        /// </para>
        /// </remarks>
        public SxmDefines.SxmErrorCode ErrorCode { get; }

        /// <summary>
        /// Gets the operation context attached to this exception, or an empty string if none was recorded.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Describes what SQLiteXM was doing when the failure occurred — for example the database,
        /// table, or statement involved. Never returns <see langword="null"/>, so the value can be
        /// logged or concatenated without a null check.
        /// </para>
        /// <para>
        /// Intended for logging and diagnostics only. Do not branch on this value; use <see cref="ErrorCode"/> instead.
        /// </para>
        /// </remarks>
        public string Context => this.Data[ContextKey] as string ?? string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class using a library <see cref="ErrorMessage"/>.
        /// </summary>
        /// <param name="errorMessage">The library error message object containing text and an ID.</param>
        /// <remarks>
        /// Sets <see cref="ErrorCode"/> and stores the same value under <c>Data["sxmErrorCode"]</c>.
        /// </remarks>
        internal SxmException(ErrorMessage errorMessage)
            : base(errorMessage.ErrorText)
        {
            this.ErrorCode = errorMessage.ErrorID;
            this.Data[ErrorCodeKey] = errorMessage.ErrorID;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class with a specified message,
        /// operation context, error code, and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="context">Human-readable description of the SQLiteXM operation that failed.</param>
        /// <param name="code">The category describing which operation failed.</param>
        /// <param name="inner">The exception that is the cause of the current exception.</param>
        /// <remarks>
        /// <para>
        /// Used by <c>ExceptionHelper.Wrap</c>. The three values answer different questions and never
        /// duplicate each other: <paramref name="message"/> describes <i>what</i> went wrong and is taken
        /// from the original exception, <paramref name="context"/> describes <i>what SQLiteXM was doing</i>
        /// at the time, and <paramref name="code"/> identifies <i>which operation</i> failed.
        /// </para>
        /// <para>
        /// <paramref name="context"/> is stored under <c>Data["sxmContext"]</c> and surfaced through
        /// <see cref="Context"/>.
        /// </para>
        /// </remarks>
        internal SxmException(string message, string context, SxmDefines.SxmErrorCode code, Exception inner)
            : base(message, inner)
        {
            this.ErrorCode = code;
            this.Data[ErrorCodeKey] = code;

            if (!string.IsNullOrWhiteSpace(context))
                this.Data[ContextKey] = context;
        }

        /// <summary>
        /// Returns the innermost exception in an exception chain.
        /// If <paramref name="ex"/> has no inner exceptions, the original exception is returned.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns>The deepest (innermost) <see cref="Exception"/> found in the chain.</returns>
        internal static Exception GetInnermostException(Exception ex)
        {
            if (ex is null) throw new ArgumentNullException(nameof(ex));

            // Walk the InnerException chain to the last exception and return it.
            Exception inner = ex;
            while (inner.InnerException != null)
                inner = inner.InnerException;

            return inner;
        }
    }

    internal sealed class SxmWarning : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SxmWarning"/> class with a specified message.
        /// </summary>
        /// <param name="message">The warning message that explains the reason for the warning.</param>
        internal SxmWarning(string message) : base(message)
        {
        }
    }

    internal sealed class SxmInformational : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SxmInformational"/> class with a specified message.
        /// </summary>
        /// <param name="message">The informational message that explains the reason for the information.</param>
        internal SxmInformational(string message) : base(message)
        {
        }

    }
}