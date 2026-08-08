using Microsoft.Data.Sqlite;

namespace SQLiteXM
{
    /// <summary>
    /// Represents an operational exception produced by SQLiteXM.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQLiteXM throws <see cref="SxmException"/> for database and ORM operational failures.
    /// These exceptions provide stable error codes and preserve provider-specific details
    /// (such as underlying SQLite errors) through the inner exception and exception metadata.
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
    /// Callers should typically catch <see cref="SxmException"/> when handling database-related
    /// failures, while allowing framework and usage exceptions to propagate normally.
    /// </para>
    /// </remarks>
    public sealed class SxmException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class using a library <see cref="ErrorMessage"/>.
        /// </summary>
        /// <param name="errorMessage">The library error message object containing text and an ID.</param>
        /// <remarks>
        /// Stores the library error code under <c>Data["sxmErrorCode"]</c>.
        /// </remarks>
        internal SxmException(ErrorMessage errorMessage)
            : base(errorMessage.ErrorText)
        {
            this.Data["sxmErrorCode"] = errorMessage.ErrorID;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class with a specified message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception.</param>
        /// <remarks>
        /// Stores <see cref="SxmDefines.SxmErrorCode.InnerException"/> under <c>Data["sxmErrorCode"]</c>.
        /// </remarks>
        internal SxmException(string message, Exception? inner)
            : base(message, inner)
        {
            this.Data["sxmErrorCode"] = SxmDefines.SxmErrorCode.InnerException;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class that wraps an existing exception.
        /// </summary>
        /// <param name="inner">The original exception being wrapped.</param>
        /// <remarks>
        /// Uses <paramref name="inner"/> as <see cref="Exception.InnerException"/> and stores
        /// <see cref="SxmDefines.SxmErrorCode.InnerException"/> under <c>Data["sxmErrorCode"]</c>.
        /// </remarks>
        internal SxmException(Exception inner)
            : base(inner.Message, inner)
        {
            this.Data["sxmErrorCode"] = SxmDefines.SxmErrorCode.InnerException;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class from a <see cref="SqliteException"/>.
        /// </summary>
        /// <param name="sqliteException">The <see cref="SqliteException"/> thrown by the underlying provider.</param>
        /// <remarks>
        /// Stores <see cref="SxmDefines.SxmErrorCode.SqliteException"/> under <c>Data["sxmErrorCode"]</c> and the provider
        /// error code under <c>Data["sqliteErrorCode"]</c>. The original <see cref="SqliteException"/> is preserved as the
        /// <see cref="Exception.InnerException"/>.
        /// </remarks>
        internal SxmException(Microsoft.Data.Sqlite.SqliteException sqliteException)
            : base(sqliteException.Message, sqliteException)
        {
            this.Data["sxmErrorCode"] = SxmDefines.SxmErrorCode.SqliteException;
            this.Data["sqliteErrorCode"] = sqliteException.ErrorCode;
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