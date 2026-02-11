using System;
using System.Collections.Generic;

namespace SQLiteXM
{
    /// <summary>
    /// Provides a central registry of named error templates and helpers to retrieve error text and IDs.
    /// </summary>
    /// <remarks>
    /// The registry maps error names to <see cref="T:SQLiteXM.ErrorMessage"/> instances. Use
    /// <see cref="GetErrorText(string)"/> and <see cref="GetErrorID(string)"/> to obtain a formatted error
    /// message or its associated <see cref="T:SQLiteXM.SxmDefines.SxmErrorCode"/> respectively.
    /// </remarks>
    public class SxmErrorMessages
    {
        /// <summary>
        /// Dictionary of named error templates keyed by error name.
        /// </summary>
        /// <remarks>
        /// Each value is an <see cref="T:SQLiteXM.ErrorMessage"/> describing an error text template and its code.
        /// This field is readonly and populated in the static constructor.
        /// </remarks>
        public static readonly Dictionary<string, ErrorMessage> Error = new Dictionary<string, ErrorMessage>();

        static SxmErrorMessages()
        {
            Error.Add("missingSQL", new ErrorMessage("Missing SQL Query.",
                SxmDefines.SxmErrorCode.MissingSQL));
            Error.Add("lockDB", new ErrorMessage("Unable to lock connection to the database: '{0}'.",
                SxmDefines.SxmErrorCode.LockDb));
            Error.Add("dbDescriptorExists", new ErrorMessage("A descriptor already exists for the database: '{0}'.",
                SxmDefines.SxmErrorCode.DbDescriptorExists));
            Error.Add("noDBDescriptorExists", new ErrorMessage("A descriptor could not be found for the database: '{0}'.",
                SxmDefines.SxmErrorCode.NoDbDescriptorExists));
            Error.Add("invalidTableName", new ErrorMessage("The table name '{0}' is invalid.",
                SxmDefines.SxmErrorCode.InvalidTableName));
            Error.Add("noDatabaseExists", new ErrorMessage("The database '{0}' does not exist.",
                SxmDefines.SxmErrorCode.NoDatabaseExists));
            Error.Add("missingSQLStatementHeader", new ErrorMessage("A header in the SQL statements properties file is missing.",
                SxmDefines.SxmErrorCode.MissingSQLStatementHeader));
            Error.Add("unknownSQLStatementHeader", new ErrorMessage("The header '{0}' in the SQL statements properties file is invalid.",
                SxmDefines.SxmErrorCode.UnknownSqlStatementHeader));
            Error.Add("invalidSQLStatementFile", new ErrorMessage("The SQL statements properties file is improperly formatted.",
                SxmDefines.SxmErrorCode.InvalidSqlStatementFile));
            Error.Add("unknownSynchCommand", new ErrorMessage("The table synch command '{0}' is not recognized.",
                SxmDefines.SxmErrorCode.UnknownSynchCommand));
            Error.Add("invalidSQLStatementDefinition", new ErrorMessage("An '{0}' statement in the SQL statements properties file is improperly formatted.",
                SxmDefines.SxmErrorCode.InvalidSqlStatementDefinition));
            Error.Add("noImplicitDBDescriptorExists", new ErrorMessage("An implicit database descriptor could not be found. Did you define more than one database?",
                SxmDefines.SxmErrorCode.NoImplicitDbDescriptorExists));
            Error.Add("unknownErrorName", new ErrorMessage("The error '{0}' could not be fund.",
                SxmDefines.SxmErrorCode.UnknownErrorName));
            Error.Add("innerException", new ErrorMessage("", // Error message from inner exception.
                SxmDefines.SxmErrorCode.InnerException));
            Error.Add("unknownSQLStatement", new ErrorMessage("The SQL statement '{0}' could not be found in the SQL statements properties file.",
                SxmDefines.SxmErrorCode.UnknownSqlStatement));
            Error.Add("invalidDBName", new ErrorMessage("The database name '{0}' is not valid.",
                SxmDefines.SxmErrorCode.InvalidDBName));
            Error.Add("SqliteException", new ErrorMessage("",
                SxmDefines.SxmErrorCode.SqliteException)); // Error message from SQLite.
            Error.Add("userDefined", new ErrorMessage("",
                SxmDefines.SxmErrorCode.UserDefined)); // Error message from user.
            Error.Add("threadLockError", new ErrorMessage("The current thread already has an active instance of SxmSTransaction.",
                SxmDefines.SxmErrorCode.ThreadLockError));
            Error.Add("sxmSTransactionTimeout", new ErrorMessage("Timeout trying to acquire the SxmSTransaction lock.",
                SxmDefines.SxmErrorCode.SxmSTransactionTimeout));
            Error.Add("improperlyFormattedVersionNumber", new ErrorMessage("The database version number '{0}' is improperly formatted. The version number must be a valid double greater than 0.",
                SxmDefines.SxmErrorCode.DbVersionFormatError));
            Error.Add("missingDatabaseName", new ErrorMessage("The database name is missing or is in the wrong spot in the SQL statements file. The database name must be the first field in the SQL statementd file.",
                SxmDefines.SxmErrorCode.DbVersionFormatError));
        }

        /// <summary>
        /// Retrieves the formatted error text for the named error template.
        /// </summary>
        /// <param name="errorName">The key identifying the error template in the registry.</param>
        /// <returns>The formatted error text associated with <paramref name="errorName"/>.</returns>
        /// <remarks>
        /// Throws <see cref="T:SQLiteXM.SxmException"/> when <paramref name="errorName"/> is not found.
        /// The returned text may contain placeholders filled by the <see cref="T:SQLiteXM.ErrorMessage"/> instance.
        /// </remarks>
        public static string GetErrorText(string errorName)
        {
            try
            {
                return ((ErrorMessage)SxmErrorMessages.Error[errorName]).ErrorText;

            }
#pragma warning disable 0168
            catch (SystemException notUsed)
#pragma warning restore 0168
            {
                throw new SxmException(new ErrorMessage("unknownErrorName", errorName));
            }

        }

        /// <summary>
        /// Retrieves the <see cref="T:SQLiteXM.SxmDefines.SxmErrorCode"/> for the named error template.
        /// </summary>
        /// <param name="errorName">The key identifying the error template in the registry.</param>
        /// <returns>The <see cref="T:SQLiteXM.SxmDefines.SxmErrorCode"/> associated with <paramref name="errorName"/>.</returns>
        /// <remarks>
        /// Throws <see cref="T:SQLiteXM.SxmException"/> when <paramref name="errorName"/> is not found.
        /// </remarks>
        public static SxmDefines.SxmErrorCode GetErrorID(string errorName)
        {
            try
            {
                return ((ErrorMessage)SxmErrorMessages.Error[errorName]).ErrorID;
            }
#pragma warning disable 0168
            catch (SystemException notUsed)
#pragma warning restore 0168
            {
                throw new SxmException(new ErrorMessage("unknownErrorName", errorName));
            }
        }

        /// <summary>
        /// Prevents instantiation of the <see cref="T:SQLiteXM.ErrorMessages"/> class.
        /// </summary>
        private SxmErrorMessages() { }
    }

    /// <summary>
    /// Represents a single error template: an error text (possibly with placeholders) and its error code.
    /// </summary>
    public class ErrorMessage
    {
        private SxmDefines.SxmErrorCode _errorId;
        private string _errorText;

        /// <summary>
        /// Creates a new <see cref="T:SQLiteXM.ErrorMessage"/> with a static text and associated error code.
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
        /// <param name="errorName">The name of an existing template in <see cref="P:SQLiteXM.ErrorMessages.error"/>.</param>
        /// <param name="list">Values to substitute into the template placeholders.</param>
        /// <remarks>
        /// The constructor looks up the template by <paramref name="errorName"/> and formats its text with <paramref name="list"/>.
        /// </remarks>
        public ErrorMessage(string errorName, params object[] list)
        {
            this._errorText = String.Format(SxmErrorMessages.Error[errorName].ErrorText, list);
            this._errorId = SxmErrorMessages.Error[errorName].ErrorID;
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