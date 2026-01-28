using System.Collections;
using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Describes a SQLite database instance and manages descriptors and related logging.
    /// </summary>
    public class SxmDatabaseDescriptor
    {
        private static ConcurrentBag<string> dbDescriptors = new();

        private static string? defaultDatabase;
        public static string? DefaultDatabase
        {
            get { return defaultDatabase; }
        }

        /// <summary>
        /// Gets the folder in which the database file is stored.
        /// </summary>
        /// <remarks>
        /// Default: <see cref="Environment.SpecialFolder.MyDocuments"/> unless specified when creating the descriptor.
        /// </remarks>
        private readonly static Environment.SpecialFolder databaseFolder = Environment.SpecialFolder.MyDocuments;
        public static Environment.SpecialFolder DatabaseFolder
        {
            get { return databaseFolder; }
        }

        /// <summary>
        /// Creates a new <see cref="SxmDatabaseDescriptor"/> for the database name provided by <see cref="SxmDatabaseDescriptor.DefaultDatabase"/>.
        /// </summary>
        /// <param name="databaseFolder">Optional folder for the database file. Default is <see cref="Environment.SpecialFolder.MyDocuments"/>.</param>
        /// <remarks>
        /// The constructor avoids double-creation of descriptors, validates the database name, ensures the database file exists,
        /// registers the descriptor, and initializes logging for the database.
        /// </remarks>
        internal SxmDatabaseDescriptor()
        {
            string databaseName = SxmProcessSQLStatements.getDatabaseName;

            try
            {
                // Avoid double-creation without relying on a coarse lock.
                if (dbDescriptors.Contains(databaseName))
                    return;

                if (SxmProcessSQLStatements.IsDefaultDatabase)
                {
                    if (SxmDatabaseDescriptor.DefaultDatabase != null)
                        throw new ArgumentException($"Invalid default database. The databse {SxmDatabaseDescriptor.DefaultDatabase} was already set as the default databse when you tried to set the database {databaseName} as the default database. There can only be one default database.");

                    SxmDatabaseDescriptor.defaultDatabase = databaseName;
                }

                CreateDB(databaseName);

                // Add descriptor; if another thread inserted concurrently, skip duplicate registration.
                dbDescriptors.Add(databaseName);
                RegisterLogger(databaseName);
            }
            catch (System.Exception ex)
            {
                throw new SxmException(ex);
            }
        }


        // Example: register logger when constructing a connection (call from your connection/context creation)
        private void RegisterLogger(string databaseName)
        {
            const long defaultMaxLogSize = 4 * 1024 * 1024; // 4 MB
            bool noLog = false; // read from config if applicable
            string logFileName = databaseName + ".log";

            SxmLogging.SxmLoggingFactory(logFileName, DatabaseFolder, defaultMaxLogSize, noLog);
        }

        private void CreateDB(string databaseName)
        {
            string databaseFolderString = Environment.GetFolderPath(databaseFolder);

            if (Directory.Exists(databaseFolderString) == false)
                Directory.CreateDirectory(databaseFolderString);

            string pathToDatabase = Path.Combine(databaseFolderString, databaseName);
            if (File.Exists(pathToDatabase) == false)
                using (File.Create(pathToDatabase)) { }
        }

        internal static bool IsDatabaseDefined(string databaseName)
        {
            return dbDescriptors.Contains(databaseName);
        }

        /// <summary>
        /// Returns the list of registered database names.
        /// </summary>
        /// <returns>An <see cref="ArrayList"/> containing the database names currently registered.</returns>
        internal static List<string> GetDatabaseNames()
        {
            List<string> allItems = dbDescriptors.ToList();
            return allItems;
        }
    }
}