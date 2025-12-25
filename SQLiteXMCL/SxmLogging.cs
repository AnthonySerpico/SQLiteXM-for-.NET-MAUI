using System.Text;
using System.Collections.Concurrent;

namespace SQLiteXM
{
    /// <summary>
    /// Provides simple file-based logging for database-specific operations.
    /// </summary>
    /// <remarks>
    /// Instances are stored in the <see cref="loggers"/> dictionary keyed by database name.
    /// Logging may be disabled via the constructor parameter <c>noLog</c>.
    /// Thread-safe file writes are synchronized with <see cref="synchLock"/>.
    /// </remarks>
    public class SxmLogging
    {
        private bool noLog;
        private int maxLogSize;
        private string logPath;

        private static readonly object synchLock = new object();

        /// <summary>
        /// Map of database name to logger instance.
        /// </summary>
        internal static ConcurrentDictionary<string, SxmLogging> loggers = new ConcurrentDictionary<string, SxmLogging>();

        /// <summary>
        /// Creates a new <see cref="SxmLogging"/> instance that writes to a log file located in the specified special folder.
        /// </summary>
        /// <param name="logFileName">The log file name (for example "app.log").</param>
        /// <param name="logPathSpecialFolder">Special folder where the log file will be stored.</param>
        /// <param name="maxLogSize">Maximum allowed log file size in bytes before rotation occurs.</param>
        /// <param name="noLog">If true, logging is disabled for this instance.</param>
        internal SxmLogging(string logFileName, Environment.SpecialFolder logPathSpecialFolder, int maxLogSize, bool noLog)
        {
            this.noLog = noLog;
            this.maxLogSize = maxLogSize;
            logPath = Path.Combine(Environment.GetFolderPath(logPathSpecialFolder), logFileName);
        }

        /// <summary>
        /// Routes an exception to the logger associated with the specified database name.
        /// </summary>
        /// <param name="dbName">The database name used as a key to locate the logger. If null the method returns immediately.</param>
        /// <param name="ex">The exception to log.</param>
        /// <param name="method">Optional name of the method where the exception originated.</param>
        /// <param name="logLevel">Optional log level label (defaults to "Error").</param>
        static internal void log(string dbName, System.Exception ex, string? method, string logLevel = "Error")
        {
            if (dbName == null) return;

            if (loggers.TryGetValue(dbName, out var log))
                log.log(ex, method, logLevel);
        }

        /// <summary>
        /// Writes exception details to the configured log file for this instance.
        /// </summary>
        /// <param name="ex">The exception to write.</param>
        /// <param name="method">The method name associated with the exception.</param>
        /// <param name="logLevel">Label indicating the log level.</param>
        /// <remarks>
        /// This method is thread-safe and will rotate the log file when it grows beyond <see cref="maxLogSize"/>.
        /// Exceptions thrown while attempting to log are swallowed to avoid throwing while handling another exception.
        /// </remarks>
        private void log(System.Exception ex, string? method, string logLevel = "Error")
        {
            if (!noLog && !string.IsNullOrEmpty(method))
            {
                try
                {
                    StringBuilder errorLogText = new StringBuilder();
                    errorLogText.AppendFormat("Method: {0}" + Environment.NewLine, method);
                    errorLogText.AppendFormat("Exception: {0}" + Environment.NewLine, ex.ToString());
                    errorLogText.AppendFormat("Source: {0}" + Environment.NewLine, ex.Source);

                    lock (synchLock)
                    {
                        File.AppendAllText(logPath, "******************************************************* " + logLevel + Environment.NewLine, Encoding.UTF8);
                        File.AppendAllText(logPath, "Time Stamp: " + DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", System.Globalization.CultureInfo.CreateSpecificCulture("en-US")) + " (UTC)  " + DateTime.UtcNow.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss.fff tt", System.Globalization.CultureInfo.CreateSpecificCulture("en-US")) + " (Local Time)" + Environment.NewLine, Encoding.UTF8);
                        File.AppendAllText(logPath, errorLogText.ToString() + Environment.NewLine, Encoding.UTF8);
                        File.AppendAllText(logPath, "*************************************************************" + Environment.NewLine + Environment.NewLine, Encoding.UTF8);

                        if ((new FileInfo(logPath)).Length > maxLogSize)
                        {
                            int extOffset = logPath.LastIndexOf(".log");
                            string oldLogPath = logPath.Insert(extOffset, ".old");

                            File.Delete(oldLogPath);
                            File.Move(logPath, oldLogPath);
                        }
                    }
                }
#pragma warning disable 0168
                catch (System.Exception notUsed) { } // Don't want to throw an exception while processing an exception.
#pragma warning restore 0168
            }
        }
    }
}