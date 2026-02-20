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
    public static class SxmErrorMessages
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

            {SxmDefines.SxmErrorCode.DbDescriptorExists, new ErrorMessage("A descriptor already exists for the database: '{0}'.",
                SxmDefines.SxmErrorCode.DbDescriptorExists)},

            {SxmDefines.SxmErrorCode.NoDbDescriptorExists, new ErrorMessage("A descriptor could not be found for the database: '{0}'.",
                SxmDefines.SxmErrorCode.NoDbDescriptorExists)},

            {SxmDefines.SxmErrorCode.InvalidTableName, new ErrorMessage("The table name '{0}' is invalid.",
                SxmDefines.SxmErrorCode.InvalidTableName)},

            {SxmDefines.SxmErrorCode.NoDatabaseExists, new ErrorMessage("The database '{0}' does not exist.",
                SxmDefines.SxmErrorCode.NoDatabaseExists)},

            {SxmDefines.SxmErrorCode.MissingSQLStatementHeader, new ErrorMessage("A header in the SQL statements properties file is missing.",
                SxmDefines.SxmErrorCode.MissingSQLStatementHeader)},

            {SxmDefines.SxmErrorCode.UnknownSqlStatementHeader, new ErrorMessage("The header '{0}' in the SQL statements properties file is invalid.",
                SxmDefines.SxmErrorCode.UnknownSqlStatementHeader)},

            {SxmDefines.SxmErrorCode.InvalidSqlStatementFile, new ErrorMessage("The SQL statements properties file is improperly formatted.",
                SxmDefines.SxmErrorCode.InvalidSqlStatementFile)},

            {SxmDefines.SxmErrorCode.UnknownSynchCommand, new ErrorMessage("The table synch command '{0}' is not recognized.",
                SxmDefines.SxmErrorCode.UnknownSynchCommand)},

            {SxmDefines.SxmErrorCode.InvalidSqlStatementDefinition, new ErrorMessage("An '{0}' statement in the SQL statements properties file is improperly formatted.",
                SxmDefines.SxmErrorCode.InvalidSqlStatementDefinition)},

            {SxmDefines.SxmErrorCode.NoImplicitDbDescriptorExists, new ErrorMessage("An implicit database descriptor could not be found. Did you define more than one database?",
                SxmDefines.SxmErrorCode.NoImplicitDbDescriptorExists)},

            {SxmDefines.SxmErrorCode.UnknownErrorName, new ErrorMessage("The error '{0}' could not be found.",
                SxmDefines.SxmErrorCode.UnknownErrorName)},

            {SxmDefines.SxmErrorCode.InnerException, new ErrorMessage("", // Error message from inner exception.
                SxmDefines.SxmErrorCode.InnerException)},

            {SxmDefines.SxmErrorCode.UnknownSqlStatement, new ErrorMessage("The SQL statement '{0}' could not be found in the SQL statements properties file.",
                SxmDefines.SxmErrorCode.UnknownSqlStatement)},

            {SxmDefines.SxmErrorCode.InvalidDBName, new ErrorMessage("The database name '{0}' is not valid.",
                SxmDefines.SxmErrorCode.InvalidDBName)},

            {SxmDefines.SxmErrorCode.SqliteException, new ErrorMessage("",
                SxmDefines.SxmErrorCode.SqliteException)}, // Error message from SQLite.

            {SxmDefines.SxmErrorCode.UserDefined, new ErrorMessage("",
                SxmDefines.SxmErrorCode.UserDefined)}, // Error message from user.

            {SxmDefines.SxmErrorCode.ThreadLockError, new ErrorMessage("The current thread already has an active instance of SxmSTransaction.",
                SxmDefines.SxmErrorCode.ThreadLockError)},

            {SxmDefines.SxmErrorCode.SxmSTransactionTimeout, new ErrorMessage("Timeout trying to acquire the SxmSTransaction lock.",
                SxmDefines.SxmErrorCode.SxmSTransactionTimeout)},

            {SxmDefines.SxmErrorCode.DbVersionFormatError, new ErrorMessage("The database version number '{0}' is improperly formatted. The version number must be a valid double greater than 0.",
                SxmDefines.SxmErrorCode.DbVersionFormatError)},

            {SxmDefines.SxmErrorCode.MissingDatabaseName, new ErrorMessage("The database name is missing or is in the wrong spot in the SQL statements file. The database name must be the first field in the SQL statements file.",
                SxmDefines.SxmErrorCode.MissingDatabaseName)},

            {SxmDefines.SxmErrorCode.AcquireLease, new ErrorMessage("Connection for '{0}' is closing and cannot be acquired.",
                SxmDefines.SxmErrorCode.AcquireLease)}
        }.ToImmutableDictionary();
        public static IReadOnlyDictionary<SxmDefines.SxmErrorCode, ErrorMessage> Errors => _errors;
    }

    /// <summary>
    /// Represents a single error template: an error text (possibly with placeholders) and its error code.
    /// </summary>
    public sealed class ErrorMessage
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