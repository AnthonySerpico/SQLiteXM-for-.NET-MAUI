using System.Collections;
using System.Collections.Concurrent;

namespace SQLiteXM
{
    public class DatabaseDescriptor
    {
        private static ConcurrentDictionary<string, DatabaseDescriptor> dbDescriptors = new();

        // Database settings.
        private string databaseName; // Required.
        public string DatabaseName
        {
            get { return databaseName; }
        }
        private Environment.SpecialFolder databaseFolder; // Optional. Default: Environment.SpecialFolder.MyDocuments.
        public Environment.SpecialFolder DatabaseFolder
        {
            get { return databaseFolder; }
        }

        // Logging settings.
        public string logfileName; // Optional. Default: Same as database name with .log extension.
        public int logfileMaxSize = 1024 * 1024; // Optional. Default: 1MB.
        public Environment.SpecialFolder logfileFolder = Environment.SpecialFolder.MyDocuments; // Optional: Environment.SpecialFolder.MyDocuments.
        public bool noLog = false;

        internal DatabaseDescriptor(Environment.SpecialFolder databaseFolder = Environment.SpecialFolder.MyDocuments)
        {
            string databaseName = ProcessSQLStatements.retreiveDatabaseName;

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
                    SQLiteXM.Logging logger = new Logging(logfileName, logfileFolder, logfileMaxSize, noLog);
                    Logging.loggers.TryAdd(databaseName, logger);
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

        public static DatabaseDescriptor? getDescriptor(string dbName)
        {
            if (dbName == null) return null;
            dbDescriptors.TryGetValue(dbName, out var desc);
            return desc;
        }

        public static ArrayList getDatabaseNames()
        {
            ArrayList dbNames = new ArrayList();
            foreach (var key in dbDescriptors.Keys)
                dbNames.Add(key);

            return dbNames;
        }

        public override string ToString()
        {
            return databaseName;
        }
    }
}