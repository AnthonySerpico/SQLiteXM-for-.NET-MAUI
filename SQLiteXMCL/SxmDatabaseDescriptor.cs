using System.Collections;
using System.Collections.Concurrent;

namespace SQLiteXM
{
    /// <summary>
    /// Describes a SQLite database instance and manages descriptors and related logging.
    /// </summary>
    public class SxmDatabaseDescriptor
    {
        private static ConcurrentDictionary<string, SxmDatabaseDescriptor> dbDescriptors = new();

        // Database settings.
        private string? databaseName; // Required.
        /// <summary>
        /// Gets the database file name.
        /// </summary>
        public string? DatabaseName
        {
            get { return databaseName; }
        }

        private Environment.SpecialFolder databaseFolder; // Optional. Default: Environment.SpecialFolder.MyDocuments.
        /// <summary>
        /// Gets the folder in which the database file is stored.
        /// </summary>
        /// <remarks>
        /// Default: <see cref="Environment.SpecialFolder.MyDocuments"/> unless specified when creating the descriptor.
        /// </remarks>
        public Environment.SpecialFolder DatabaseFolder
        {
            get { return databaseFolder; }
        }

        // Logging settings.
        /// <summary>
        /// The log file name for this database.
        /// </summary>
        /// <remarks>Default: same as database name with a .log extension.</remarks>
        public string? logfileName; // Optional. Default: Same as database name with .log extension.

        /// <summary>
        /// Maximum size in bytes of the log file.
        /// </summary>
        /// <remarks>Default: 1 MB.</remarks>
        public int logfileMaxSize = 1024 * 1024; // Optional. Default: 1MB.

        /// <summary>
        /// Folder used to store the log file.
        /// </summary>
        /// <remarks>Default: <see cref="Environment.SpecialFolder.MyDocuments"/>.</remarks>
        public Environment.SpecialFolder logfileFolder = Environment.SpecialFolder.MyDocuments; // Optional: Environment.SpecialFolder.MyDocuments.

        /// <summary>
        /// When true, logging is disabled for this database.
        /// </summary>
        public bool noLog = false;

        /// <summary>
        /// Creates a new <see cref="SxmDatabaseDescriptor"/> for the database name provided by <see cref="SxmProcessSQLStatements.retreiveDatabaseName"/>.
        /// </summary>
        /// <param name="databaseFolder">Optional folder for the database file. Default is <see cref="Environment.SpecialFolder.MyDocuments"/>.</param>
        /// <remarks>
        /// The constructor avoids double-creation of descriptors, validates the database name, ensures the database file exists,
        /// registers the descriptor, and initializes logging for the database.
        /// </remarks>
        internal SxmDatabaseDescriptor(Environment.SpecialFolder databaseFolder = Environment.SpecialFolder.MyDocuments)
        {
            string databaseName = SxmProcessSQLStatements.retreiveDatabaseName;

            try
            {
                // Avoid double-creation without relying on a coarse lock.
                if (dbDescriptors.ContainsKey(databaseName))
                    return;

                validateDBName(databaseName);

                this.databaseFolder = databaseFolder;
                this.databaseName = databaseName;
                logfileName = databaseName + ".log";

                createDB();

                // Add descriptor; if another thread inserted concurrently, skip duplicate registration.
                if (dbDescriptors.TryAdd(databaseName, this))
                {
                    SQLiteXM.SxmLogging logger = new SxmLogging(logfileName, logfileFolder, logfileMaxSize, noLog);
                    SxmLogging.loggers.TryAdd(databaseName, logger);
                }
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }
        }

        // Sanity check the database name.
        private void validateDBName(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName) || databaseName.ToLower().Equals("main") || databaseName.ToLower().Equals("temp"))
                throw new SxmException(new ErrorMessage("invalidDBName", databaseName));
        }

        private void createDB()
        {
            string databaseFolderString = Environment.GetFolderPath(databaseFolder);

            if (Directory.Exists(databaseFolderString) == false)
                Directory.CreateDirectory(databaseFolderString);

            string pathToDatabase = Path.Combine(databaseFolderString, databaseName);
            if (File.Exists(pathToDatabase) == false)
                using (File.Create(pathToDatabase)) { }
        }

        /// <summary>
        /// Gets the descriptor for the named database if it exists.
        /// </summary>
        /// <param name="dbName">The database name to look up.</param>
        /// <returns>The descriptor for the database, or null if not found or if <paramref name="dbName"/> is null.</returns>
        public static SxmDatabaseDescriptor? getDescriptor(string dbName)
        {
            if (dbName == null) return null;
            dbDescriptors.TryGetValue(dbName, out var desc);
            return desc;
        }

        /// <summary>
        /// Returns the list of registered database names.
        /// </summary>
        /// <returns>An <see cref="ArrayList"/> containing the database names currently registered.</returns>
        public static ArrayList getDatabaseNames()
        {
            ArrayList dbNames = new ArrayList();
            foreach (var key in dbDescriptors.Keys)
                dbNames.Add(key);

            return dbNames;
        }

        /// <summary>
        /// Returns the database name represented by this descriptor.
        /// </summary>
        /// <returns>The database file name.</returns>
        public override string? ToString()
        {
            return databaseName;
        }
    }
}