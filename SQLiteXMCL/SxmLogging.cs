using System.Text;
using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Provides simple file-based logging for database-specific operations.
    /// </summary>
    /// <remarks>
    /// Instances are stored in the <see cref="loggers"/> dictionary keyed by database name.
    /// Logging may be disabled via the constructor parameter <c>noLog</c>.
    /// Logging entries are queued and flushed to disk by a single background writer task.
    /// </remarks>
    public class SxmLogging : System.IDisposable
    {
        private readonly bool noLog;
        private readonly long maxLogSize;
        private readonly string logPath;

        /// <summary>
        /// Map of database name to logger instance.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SxmLogging> loggers = new ConcurrentDictionary<string, SxmLogging>();
        private readonly Channel<string> writeChannel;
        private readonly CancellationTokenSource cts;
        private readonly Task backgroundWriterTask;

        // Tracks number of dropped log entries when the channel is full.
        private long droppedCount;

        // Maximum characters retained for exception text to avoid very large queued entries on mobile.
        private const int MaxExceptionTextLength = 2048;

        internal static void SxmLoggingFactory(string logFileName, Environment.SpecialFolder logPathSpecialFolder, long maxLogSize, bool noLog)
        {
            string databaseName = Path.GetFileNameWithoutExtension(logFileName);
            if (!SxmLogging.loggers.TryGetValue(databaseName, out SxmLogging? value))
            {

                SxmLogging? logger = new SxmLogging(logFileName, logPathSpecialFolder, maxLogSize, noLog);
                SxmLogging.loggers.TryAdd(databaseName, logger);
            }
        }

        /// <summary>
        /// Creates a new <see cref="SxmLogging"/> instance that writes to a log file located in the specified special folder.
        /// </summary>
        /// <param name="logFileName">The log file name (for example "app.log").</param>
        /// <param name="logPathSpecialFolder">Special folder where the log file will be stored.</param>
        /// <param name="maxLogSize">Maximum allowed log file size in bytes before rotation occurs.</param>
        /// <param name="noLog">If true, logging is disabled for this instance.</param>
        private SxmLogging(string logFileName, Environment.SpecialFolder logPathSpecialFolder, long maxLogSize, bool noLog)
        {
            this.noLog = noLog;
            this.maxLogSize = maxLogSize;
            string folder = Environment.GetFolderPath(logPathSpecialFolder);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            logPath = Path.Combine(folder, logFileName);

            // Bounded channel prevents unbounded memory growth.
            // Capacity: 250. SingleReader true, multiple writers allowed.
            // FullMode: DropOldest => preserve most recent logs, discard oldest when full.
            var options = new BoundedChannelOptions(250)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropNewest
            };

            writeChannel = Channel.CreateBounded<string>(options);
            cts = new CancellationTokenSource();
            backgroundWriterTask = Task.Run(() => ProcessQueueAsync(cts.Token), CancellationToken.None);
        }

        /// <summary>
        /// Routes an exception to the logger associated with the specified database name.
        /// </summary>
        /// <param name="dbName">The database name used as a key to locate the logger. If null the method returns immediately.</param>
        /// <param name="ex">The exception to log.</param>
        /// <param name="method">Optional name of the method where the exception originated.</param>
        /// <param name="logLevel">Optional log level label (defaults to "Error").</param>
        static internal void Log(string dbName, System.Exception ex, string? method, string logLevel = "Error")
        {
            if (dbName == null) return;

            if (loggers.TryGetValue(dbName, out var log))
                log.Log(ex, method, logLevel);
        }

        /// <summary>
        /// Enqueues exception details to be written asynchronously by the background writer.
        /// </summary>
        /// <param name="ex">The exception to write.</param>
        /// <param name="method">The method name associated with the exception.</param>
        /// <param name="logLevel">Label indicating the log level.</param>
        /// <remarks>
        /// This method mirrors the original behavior: logging occurs only when <c>!noLog</c> and <c>method</c> is not null or empty.
        /// Very large exception text is trimmed to avoid excessive memory use on mobile devices.
        /// Exceptions thrown while attempting to enqueue are swallowed to avoid throwing while handling another exception.
        /// </remarks>
        private void Log(System.Exception ex, string? method, string logLevel = "Error")
        {
            if (noLog || string.IsNullOrEmpty(method))
                return;

            try
            {
                // Build trimmed exception text to limit per-entry size.
                string exceptionText = ex?.ToString() ?? string.Empty;
                exceptionText = TruncateExceptionText(exceptionText, MaxExceptionTextLength);

                StringBuilder errorLogText = new StringBuilder();
                errorLogText.AppendFormat("Method: {0}" + Environment.NewLine, method);
                errorLogText.AppendFormat("Exception: {0}" + Environment.NewLine, exceptionText);
                errorLogText.AppendFormat("Source: {0}" + Environment.NewLine, ex.Source);

                StringBuilder entryBuilder = new StringBuilder();
                entryBuilder.Append("******************************************************* ").Append(logLevel).Append(Environment.NewLine);
                entryBuilder.Append("Time Stamp: ");
                entryBuilder.Append(DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.CreateSpecificCulture("en-US")));
                entryBuilder.Append(" (UTC)  ");
                entryBuilder.Append(DateTime.UtcNow.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.CreateSpecificCulture("en-US")));
                entryBuilder.Append(" (Local Time)");
                entryBuilder.Append(Environment.NewLine);
                entryBuilder.Append(errorLogText.ToString()).Append(Environment.NewLine);
                entryBuilder.Append("*************************************************************").Append(Environment.NewLine).Append(Environment.NewLine);

                // Try to write to the bounded channel. With DropOldest FullMode the channel should accept new entries
                // but if TryWrite returns false for any reason increment droppedCount as a fallback.
                if (!writeChannel.Writer.TryWrite(entryBuilder.ToString()))
                {
                    Interlocked.Increment(ref droppedCount);
                }
            }
#pragma warning disable 0168
            catch (System.Exception)
            {
                // Swallow exceptions to avoid throwing while processing another exception - preserve original behavior.
            }
#pragma warning restore 0168
        }

        /// <summary>
        /// Truncates exception text to a maximum length and appends a marker when truncation occurs.
        /// </summary>
        /// <param name="text">The exception text to truncate.</param>
        /// <param name="maxLength">Maximum number of characters to retain.</param>
        /// <returns>Original text if shorter than <paramref name="maxLength"/>; otherwise a truncated string with a marker.</returns>
        private static string TruncateExceptionText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            // Keep the start of the stack trace (message + top frames) and mark truncation.
            return text.Substring(0, maxLength) + "... [truncated]";
        }

        /// <summary>
        /// Background loop reading entries from the channel and flushing them to disk.
        /// </summary>
        /// <param name="token">Cancellation token used to stop the background writer.</param>
        private async Task ProcessQueueAsync(CancellationToken token)
        {
            try
            {
                var reader = writeChannel.Reader;
                while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var entry))
                    {
                        try
                        {
                            // If entries were dropped while the writer was busy, record a short summary first.
                            long dropped = Interlocked.Exchange(ref droppedCount, 0);
                            if (dropped > 0)
                            {
                                string summary = $"*** Dropped {dropped} log entries due to full log queue. ***{Environment.NewLine}";
                                await File.AppendAllTextAsync(logPath, summary, Encoding.UTF8, token).ConfigureAwait(false);
                            }

                            // Ensure directory exists in case it was removed after construction.
                            string? directory = Path.GetDirectoryName(logPath);
                            if (!string.IsNullOrEmpty(directory))
                                Directory.CreateDirectory(directory);

                            // Use append semantics for each entry.
                            await File.AppendAllTextAsync(logPath, entry, Encoding.UTF8, token).ConfigureAwait(false);

                            // Rotate if file exceeded the configured max size.
                            try
                            {
                                var fileInfo = new FileInfo(logPath);
                                if (fileInfo.Exists && fileInfo.Length > maxLogSize)
                                {
                                    await RotateLogFileAsync(token).ConfigureAwait(false);
                                }
                            }
                            catch
                            {
                                // Swallow rotation exceptions to avoid logging causing crashes.
                            }
                        }
                        catch
                        {
                            // Swallow write exceptions to avoid logging causing crashes.
                        }
                    }
                }
            }
            catch
            {
                // If the reader loop exits unexpectedly, swallow exceptions to avoid secondary failures.
            }
        }

        /// <summary>
        /// Attempts to rotate the current log file to the .old name using an atomic replace when possible.
        /// Retries a few times on transient failures and swallows exceptions to preserve original behavior.
        /// </summary>
        /// <param name="token">Cancellation token used to abort retries.</param>
        private async Task RotateLogFileAsync(CancellationToken token)
        {
            // Build names
            string ext = Path.GetExtension(logPath);
            string fileNameOnly = Path.GetFileNameWithoutExtension(logPath);
            string dir = Path.GetDirectoryName(logPath) ?? string.Empty;
            string oldLogFileName = fileNameOnly + ".old" + ext;
            string oldLogPath = Path.Combine(dir, oldLogFileName);

            const int maxAttempts = 3;
            const int delayMs = 150;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // If the destination (oldLogPath) exists, use File.Replace to atomically replace it with logPath.
                    // File.Replace deletes the source (logPath) on success.
                    if (File.Exists(oldLogPath))
                    {
                        // Use null for backup name: we don't need a separate backup file.
                        File.Replace(logPath, oldLogPath, null);
                    }
                    else
                    {
                        // Destination doesn't exist — simple move is fine (atomic on same volume).
                        File.Move(logPath, oldLogPath);
                    }

                    // Rotation succeeded.
                    return;
                }
                catch (System.Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"SxmLogging.RotateLogFileAsync attempt {attempt} failed: {ex.Message}");
#endif
                    // On the last attempt, swallow the exception (preserve original behavior).
                    if (attempt == maxAttempts || token.IsCancellationRequested)
                        return;

                    try
                    {
                        await Task.Delay(delayMs, token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Cancellation requested; exit early.
                        return;
                    }
                }
            }
        }


        /// <summary>
        /// Signals the background writer to stop, waits for pending entries to be flushed and releases resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Stop accepting new writes, signal cancellation and wait for background task to finish.
                writeChannel.Writer.TryComplete();
                cts.Cancel();

                // Wait a short time for background writer to finish flushing. Avoid indefinite block.
                backgroundWriterTask.Wait(5000);
            }
            catch
            {
                // Swallow any disposal exceptions.
            }
            finally
            {
                cts.Dispose();
            }
        }
    }
}