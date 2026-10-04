using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace SQLiteXM
{
    /// <summary>
    /// Central registry of SQLiteXM error templates keyed by <see cref="SxmDefines.SxmErrorCode"/>.
    /// </summary>
    /// <remarks>
    /// SQLiteXM constructs <see cref="ErrorMessage"/> instances from these templates and typically throws
    /// <see cref="SxmException"/> to surface a consistent, library-defined failure contract.
    /// </remarks>
    internal static class SxmErrorMessages
    {
        /// <summary>
        /// Dictionary of named error templates keyed by error name.
        /// </summary>
        /// <remarks>
        /// Each value is an <see cref="T:SQLiteXM.ErrorMessage"/> describing an error text template and its code.
        /// This field is readonly and populated in the static constructor.
        /// </remarks>
        private static readonly ImmutableDictionary<SxmDefines.SxmErrorCode, ErrorMessage> _errors = new Dictionary<SxmDefines.SxmErrorCode, ErrorMessage>
        {
            {SxmDefines.SxmErrorCode.MissingSQL, new ErrorMessage("Missing SQL Query.",
                SxmDefines.SxmErrorCode.MissingSQL) },

            {SxmDefines.SxmErrorCode.LockDb, new ErrorMessage("Unable to lock connection to the database: '{0}'.",
                SxmDefines.SxmErrorCode.LockDb)},

            {SxmDefines.SxmErrorCode.NoDatabaseExists, new ErrorMessage("The database '{0}' does not exist.",
                SxmDefines.SxmErrorCode.NoDatabaseExists)},

            {SxmDefines.SxmErrorCode.UnknownSynchCommand, new ErrorMessage("The table synch command '{0}' is not recognized.",
                SxmDefines.SxmErrorCode.UnknownSynchCommand)},

            {SxmDefines.SxmErrorCode.UnknownSqlStatement, new ErrorMessage("The SQL statement '{0}' could not be found in the SQL statements properties file.",
                SxmDefines.SxmErrorCode.UnknownSqlStatement)},

            {SxmDefines.SxmErrorCode.InvalidDBName, new ErrorMessage("The database name '{0}' is not valid.",
                SxmDefines.SxmErrorCode.InvalidDBName)},

            {SxmDefines.SxmErrorCode.DbVersionFormatError, new ErrorMessage("The database version number '{0}' is improperly formatted. The version number must be a valid double greater than 0.",
                SxmDefines.SxmErrorCode.DbVersionFormatError)},

            {SxmDefines.SxmErrorCode.AcquireLease, new ErrorMessage("Connection for '{0}' is closing and cannot be acquired.",
                SxmDefines.SxmErrorCode.AcquireLease)},

            {SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded, new ErrorMessage("Cannot create connection for '{0}': application is backgrounded.",
                SxmDefines.SxmErrorCode.ConnectionBlockedBackgrounded)},

            {SxmDefines.SxmErrorCode.TableHasDependents, new ErrorMessage("The table '{0}' cannot be dropped because the following tables reference it with foreign keys: {1}. SQLite cannot remove a foreign key clause from an existing table, so dropping '{0}' would leave those tables referencing a table that no longer exists and their subsequent writes would fail. Drop the referencing tables first, innermost dependents before their parents.",
                SxmDefines.SxmErrorCode.TableHasDependents)},

            // Wrapped-failure categories. The message text is supplied by the wrap site, which knows the
            // operation that failed, so these templates are intentionally empty.

            {SxmDefines.SxmErrorCode.ConnectionFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.ConnectionFailure)},

            {SxmDefines.SxmErrorCode.LockFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.LockFailure)},

            {SxmDefines.SxmErrorCode.QueryFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.QueryFailure)},

            {SxmDefines.SxmErrorCode.SchemaFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.SchemaFailure)},

            {SxmDefines.SxmErrorCode.DataConversionFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.DataConversionFailure)},

            {SxmDefines.SxmErrorCode.MappingFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.MappingFailure)},

            {SxmDefines.SxmErrorCode.TransactionFailure, new ErrorMessage("",
                SxmDefines.SxmErrorCode.TransactionFailure)}
        }.ToImmutableDictionary();
        public static IReadOnlyDictionary<SxmDefines.SxmErrorCode, ErrorMessage> Errors => _errors;
    }

    /// <summary>
    /// Represents a single error template: an error text (possibly with placeholders) and its error code.
    /// </summary>
    internal sealed class ErrorMessage
    {
        readonly private string _errorText;
        readonly private SxmDefines.SxmErrorCode _errorId;
        private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Creates a new <see cref="ErrorMessage"/> using a static text template and associated error code.
        /// </summary>
        /// <param name="errorText">The error text template. May contain format placeholders.</param>
        /// <param name="errorId">The <see cref="T:SQLiteXM.SxmDefines.SxmErrorCode"/> representing the error type.</param>
        public ErrorMessage(string errorText, SxmDefines.SxmErrorCode errorId)
        {
            this._errorText = errorText;
            this._errorId = errorId;
        }

        /// <summary>
        /// Creates a new formatted <see cref="T:SQLiteXM.ErrorMessage"/> by formatting an existing named template.
        /// </summary>
        /// <param name="errorId">The name of an existing template in <see cref="P:SQLiteXM.SxmErrorMessages.Errors"/>.</param>
        /// <param name="list">Values to substitute into the template placeholders.</param>
        /// <remarks>
        /// The constructor looks up the template by <paramref name="errorId"/> and formats its text with <paramref name="list"/>.
        /// </remarks>
        public ErrorMessage(SxmDefines.SxmErrorCode errorId, params object?[]? list)
        {
            if (!SxmErrorMessages.Errors.TryGetValue(errorId, out var template))
                throw new KeyNotFoundException($"Error template not registered for {errorId}");

            list ??= Array.Empty<object>();
            string errorText = template.ErrorText;

            var matches = PlaceholderRegex.Matches(errorText);
            int maxIndex = -1;

            foreach (Match m in matches)
            {
                // m.Groups[1] is the digits captured by (\d+)
                if (!int.TryParse(m.Groups[1].Value, out int idx))
                    continue;
                if (idx > maxIndex)
                    maxIndex = idx;
            }

            int requiredCount = maxIndex + 1;

            if (list.Length < requiredCount)
            {
                object[] padded = new object[requiredCount];

                for(int i = 0; i < list.Length; i++)
                    padded[i] = list[i] ?? "unknown";

                for (int i = list.Length; i < requiredCount; i++)
                    padded[i] = "unknown";

                list = padded;
            }

            this._errorText = String.Format(errorText, list);
            this._errorId = errorId;
        }

        /// <summary>
        /// Gets the error code for this message.
        /// </summary>
        public SxmDefines.SxmErrorCode ErrorID
        {
            get { return _errorId; }
        }

        /// <summary>
        /// Gets the formatted error text for this message.
        /// </summary>
        public string ErrorText
        {
            get { return _errorText; }
        }
    }
}