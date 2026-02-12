using System;
using Microsoft.Data.Sqlite;

namespace SQLiteXM
{
    /// <summary>
    /// Exception type used by the SQLiteXM library to provide richer error information.
    /// Adds library-specific error codes into the <see cref="Exception.Data"/> dictionary
    /// for programmatic inspection.
    /// </summary>
    public class SxmException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class.
        /// </summary>
        public SxmException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class using
        /// a library <see cref="ErrorMessage"/>. The message text is used as the
        /// exception message and the library error ID is stored in <see cref="Exception.Data"/>.
        /// </summary>
        /// <param name="errorMessage">The library error message object containing text and an ID.</param>
        public SxmException(ErrorMessage errorMessage)
            : base(errorMessage.ErrorText)
        {
            this.Data.Add("sxmErrorCode", errorMessage.ErrorID);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class with a specified
        /// error message and a reference to the inner exception that is the cause of this exception.
        /// Preserves the original exception as the inner exception and adds a library error ID.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="inner">The exception that is the cause of the current exception.</param>
        public SxmException(string message, Exception inner)
            : base(message, inner)
        {
            this.Data.Add("sxmErrorCode", SxmErrorMessages.Error[SxmDefines.SxmErrorCode.InnerException].ErrorID);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class that wraps
        /// an existing <see cref="Exception"/>. The original exception is stored as the
        /// inner exception and a library "innerException" error ID is added to <see cref="Exception.Data"/>.
        /// </summary>
        /// <param name="inner">The original exception being wrapped.</param>
        public SxmException(Exception inner)
            : base(inner.Message, inner)
        {
            this.Data.Add("sxmErrorCode", SxmErrorMessages.Error[SxmDefines.SxmErrorCode.InnerException].ErrorID);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SxmException"/> class from a
        /// <see cref="SqliteException"/>. The SQLite error code is stored under the
        /// key "sqliteErrorCode" in <see cref="Exception.Data"/> in addition to a library error ID.
        /// </summary>
        /// <param name="sqliteException">The <see cref="SqliteException"/> thrown by the underlying provider.</param>
        public SxmException(Microsoft.Data.Sqlite.SqliteException sqliteException)
            : base(sqliteException.Message)
        {
            this.Data.Add("sxmErrorCode", SxmErrorMessages.Error[SxmDefines.SxmErrorCode.SqliteException].ErrorID);
            this.Data.Add("sqliteErrorCode", sqliteException.ErrorCode);
        }

        /// <summary>
        /// Returns the innermost exception in an exception chain.
        /// If <paramref name="ex"/> has no inner exceptions, the original exception is returned.
        /// </summary>
        /// <param name="ex">The exception to inspect.</param>
        /// <returns>The deepest (innermost) <see cref="Exception"/> found in the chain.</returns>
        public static Exception GetInnermostException(Exception ex)
        {
            if (ex is null) throw new ArgumentNullException(nameof(ex));

            // Walk the InnerException chain to the last exception and return it.
            Exception inner = ex;
            while (inner.InnerException != null)
                inner = inner.InnerException;

            return inner;
        }
    }
}