using System.Collections;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Describes a SQLite database instance and manages descriptors and related logging.
    /// </summary>
    internal class SxmDatabaseDescriptor
    {
        private static ConcurrentBag<string> _dbDescriptors = new();

        public static string? DefaultDatabase
        {
            get { return SxmProcessSQLStatements.DefaultDatabaseName; }
        }

        /// <summary>
        /// Gets the folder in which the database file is stored.
        /// </summary>
        /// <remarks>
        /// Default: <see cref="Environment.SpecialFolder.MyDocuments"/> unless specified when creating the descriptor.
        /// </remarks>
/*        private readonly static Environment.SpecialFolder _databaseFolder = Environment.SpecialFolder.LocalApplicationData;

        internal static string DatabaseFolder
        {
            get { 
                if(DatabaseFolderOverride == null)
                    return Path.Combine(Environment.GetFolderPath(_databaseFolder), "SQLiteXM");

                return DatabaseFolderOverride;
            }
        }*/

        private static string? _databaseFolder;
        internal static string? DatabaseFolder
        {
            get => _databaseFolder;
            set
            {
                // Can only be set once.
                if (_databaseFolder is null)
                {
                    if (value == null)
                        _databaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SQLiteXM");
                    else
                        _databaseFolder = value;
                }
            }
        }

        internal static void SxmDatabaseDescriptorFactory()
        {
            // At this point, the SQL statements file has been parsed, and all database definitions have been loaded into SxmProcessSQLStatements.
            // Create descriptors for all parsed databases (not just the default).
            foreach (string dbName in SxmProcessSQLStatements.Databases)
            {
                new SxmDatabaseDescriptor(dbName);
            }
        }

        /// <summary>
        /// Creates a new <see cref="SxmDatabaseDescriptor"/> for the database name provided by <see cref="SxmDatabaseDescriptor.DefaultDatabase"/>.
        /// </summary>
        /// <param name="databaseFolder">Optional folder for the database file. Default is <see cref="Environment.SpecialFolder.MyDocuments"/>.</param>
        /// <remarks>
        /// The constructor avoids double-creation of descriptors, validates the database name, ensures the database file exists,
        /// registers the descriptor, and initializes logging for the database.
        /// </remarks>
        internal SxmDatabaseDescriptor() : this(SxmProcessSQLStatements.DefaultDatabaseName)
        {
        }

        /// <summary>
        /// Creates a new <see cref="SxmDatabaseDescriptor"/> for the specified database name.
        /// </summary>
        /// <param name="databaseName">The name of the database to create a descriptor for.</param>
        /// <remarks>
        /// The constructor avoids double-creation of descriptors, validates the database name, ensures the database file exists,
        /// registers the descriptor, and initializes logging for the database.
        /// </remarks>
        internal SxmDatabaseDescriptor(string databaseName)
        {
            try
            {
                // Avoid double-creation without relying on a coarse lock.
                if (_dbDescriptors.Contains(databaseName))
                    return;

                CreateDB(databaseName);

                // Add descriptor; if another thread inserted concurrently, skip duplicate registration.
                _dbDescriptors.Add(databaseName);
                RegisterLogger(databaseName);
            }
            catch (System.Exception ex) when (ExceptionHelper.IsNonWrappable(ex))
            {
                SxmLogging.Log(ex, $"Ctor failure for class SxmDatabaseDescriptor for database '{databaseName}'.");
                // Cancellation/fatal — rethrow unchanged so callers/runtime can handle appropriately.
                throw;
            }
            catch (System.Exception ex)
            {
                string errStr = $"Ctor failure for class SxmDatabaseDescriptor for database '{databaseName}'.";
                SxmLogging.Log(ex, errStr);
                throw ExceptionHelper.Wrap(ex, errStr);
            }
        }


        // Example: register logger when constructing a connection (call from your connection/context creation)
        private void RegisterLogger(string databaseName)
        {
            const long defaultMaxLogSize = 4 * 1024 * 1024; // 4 MB
            string logFileName = databaseName + ".log";

            string? logFilePath = SxmDatabaseDescriptor.DatabaseFolder;
            if (logFilePath == null)
                throw new InvalidOperationException("Database folder path is not configured for logging.");

            SxmLogging.SxmLoggingFactory(logFileName, logFilePath, defaultMaxLogSize);
        }

        private void CreateDB(string databaseName)
        {
            string? databaseFolderString = SxmDatabaseDescriptor.DatabaseFolder;
            if (databaseFolderString == null)
                throw new InvalidOperationException("Database folder path is not configured.");

            if (Directory.Exists(databaseFolderString) == false)
                Directory.CreateDirectory(databaseFolderString);

            string pathToDatabase = Path.Combine(databaseFolderString, databaseName);
            if (File.Exists(pathToDatabase) == false)
                using (File.Create(pathToDatabase)) { }
        }

        /// <summary>
        /// Gets the size of the SQLite WAL file for the specified database.
        /// </summary>
        /// <param name="databaseName">The database file name (without path).</param>
        /// <returns>The size of the WAL file in bytes, or 0 if the file does not exist.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="databaseName"/> is null or whitespace.</exception>
        public static long GetWalFileSize(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name must be specified.", nameof(databaseName));

            string? databaseFolder = SxmDatabaseDescriptor.DatabaseFolder;
            if (databaseFolder == null)
                throw new InvalidOperationException("Database folder path is not configured.");

            string walFilePath = Path.Combine(databaseFolder, databaseName + "-wal");
            if (!File.Exists(walFilePath))
                return 0;

            FileInfo fileInfo = new(walFilePath);
            return fileInfo.Length;
        }

        internal static bool IsDatabaseDefined(string databaseName)
        {
            return _dbDescriptors.Contains(databaseName);
        }

#if DEBUG
        /// <summary>
        /// Resets all database descriptor state for testing purposes.
        /// **WARNING:** Only call this in test scenarios.
        /// </summary>
        internal static void ResetForTesting()
        {
            _dbDescriptors = new ConcurrentBag<string>();
            _databaseFolder = null;
        }
#endif

        /// <summary>
        /// Returns the list of registered database names.
        /// </summary>
        /// <returns>An <see cref="ArrayList"/> containing the database names currently registered.</returns>
        internal static List<string> GetDatabaseNames()
        {
            List<string> allItems = _dbDescriptors.ToList();
            return allItems;
        }
    }
}