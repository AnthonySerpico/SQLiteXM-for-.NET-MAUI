using SQLiteXM.Internal.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SQLiteXM
{
    /// <summary>
    /// Provides simple file-based logging for database-specific operations.
    /// </summary>
    /// <remarks>
    /// Instances are stored in the <see cref="_loggers"/> dictionary keyed by database name.
    /// Logging entries are queued and flushed to disk by a single background writer task.
    /// </remarks>
    public class SxmLogging : System.IDisposable
    {
        private readonly long _maxLogSize;
        private readonly string _logPath;

        /// <summary>
        /// Map of database name to logger instance.
        /// </summary>
        private static readonly ConcurrentDictionary<string, SxmLogging> _loggers = new ConcurrentDictionary<string, SxmLogging>();
        private readonly Channel<string> _writeChannel;
        private readonly CancellationTokenSource _cts;
        private readonly Task _backgroundWriterTask;

        // Tracks number of dropped log entries when the channel is full.
        private long _droppedCount;

        // Maximum characters retained for exception text to avoid very large queued entries on mobile.
        private const int _maxExceptionTextLength = 2048;

        internal static void SxmLoggingFactory(string logFileName, string logPathSpecialFolder, long maxLogSize)
        {
            string databaseName = Path.GetFileNameWithoutExtension(logFileName);

            // Create a database-specific logger only when the extracted name is non-empty.
            if (!string.IsNullOrEmpty(databaseName))
            {
                _loggers.GetOrAdd(databaseName, _ => new SxmLogging(logFileName, logPathSpecialFolder, maxLogSize));
            }

            // Ensure a default (general) logger exists under the empty-string key.
            _loggers.GetOrAdd(string.Empty, _ => new SxmLogging("defaultsxmlog.log", logPathSpecialFolder, maxLogSize));
        }

        /// <summary>
        /// Creates a new <see cref="SxmLogging"/> instance that writes to a log file located in the specified special folder.
        /// </summary>
        /// <param name="logFileName">The log file name (for example "app.log").</param>
        /// <param name="logPathSpecialFolder">Special folder where the log file will be stored.</param>
        /// <param name="maxLogSize">Maximum allowed log file size in bytes before rotation occurs.</param>
        private SxmLogging(string logFileName, string logPathSpecialFolder, long maxLogSize)
        {
            this._maxLogSize = maxLogSize;
            string folder = logPathSpecialFolder;
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            _logPath = Path.Combine(folder, logFileName);

            // Bounded channel prevents unbounded memory growth.
            // Capacity: 250. SingleReader true, multiple writers allowed.
            // FullMode: DropOldest => preserve most recent logs, discard oldest when full.
            var options = new BoundedChannelOptions(250)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            };

            _writeChannel = Channel.CreateBounded<string>(options);
            _cts = new CancellationTokenSource();
            _backgroundWriterTask = Task.Run(() => ProcessQueueAsync(_cts.Token), CancellationToken.None);
        }

        /// <summary>
        /// Routes an exception to the logger instance associated with the supplied database name.
        /// </summary>
        /// <param name="dbName">
        /// Database name used as the key to locate the logger instance. If <c>null</c> the call is ignored.
        /// </param>
        /// <param name="ex">The exception to log. Must not be <c>null</c>.</param>
        /// <param name="method">
        /// Optional name of the calling member. When the caller omits this argument the compiler will
        /// supply the caller member name via <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/>.
        /// Callers may still pass an explicit value (for example <c>nameof(SomeMethod)</c>) if desired.
        /// </param>
        /// <param name="logLevel">Optional log level label (defaults to <c>"Error"</c>).</param>
        /// <remarks>
        /// - This helper is a convenience wrapper that resolves the per-database <see cref="_loggers"/> entry
        ///   and forwards the exception to the instance logger's non-static <see cref="Log(System.Exception,string?,string)"/> method.
        /// - Using <see cref="System.Runtime.CompilerServices.CallerMemberNameAttribute"/> reduces copy/paste errors
        ///   because callers can omit the <c>method</c> parameter and have the compiler provide the caller name.
        /// - Existing call sites that explicitly supply <c>method</c> (for example <c>nameof(...) </c>) remain valid.
        /// - The method is intentionally tolerant: if <paramref name="dbName"/> is <c>null</c> or no logger exists
        ///   for the name, the call is a no-op to avoid cascading failures during error handling.
        /// </remarks>
        static internal void Log(System.Exception ex, string logLevel = "Error", [System.Runtime.CompilerServices.CallerMemberName] string method = "")
        {
            if (!SxmDatabaseOptions.IsLoggingEnabled())
                return;

            string? dbName = SxmDatabaseDescriptor.DefaultDatabase;

            if (dbName != null)
            {
                if (_loggers.TryGetValue(dbName, out var log))
                    log.WriteLog(ex, method, logLevel);
            }
        }

        /// <summary>
        /// Enqueues exception details to be written asynchronously by the background writer.
        /// </summary>
        /// <param name="ex">The exception to write.</param>
        /// <param name="method">The method name associated with the exception.</param>
        /// <param name="logLevel">Label indicating the log level.</param>
        /// <remarks>
        /// Very large exception text is trimmed to avoid excessive memory use on mobile devices.
        /// Exceptions thrown while attempting to enqueue are swallowed to avoid throwing while handling another exception.
        /// </remarks>
        private void WriteLog(System.Exception ex, string? method, string logLevel)
        {
            try
            {
                // Build trimmed exception text to limit per-entry size.
                string exceptionText = ex?.ToString() ?? "<unknown exception>";
                exceptionText = TruncateExceptionText(exceptionText, _maxExceptionTextLength);
                method ??= "<unknown>";

                StringBuilder errorLogText = new StringBuilder();
                errorLogText.AppendFormat("Method: {0}" + Environment.NewLine, method);
                errorLogText.AppendFormat("{0} {1}{2}", (ex is SxmWarning) ? "Warning: " : "Exception: ", exceptionText, Environment.NewLine);

                if (ex != null)
                {
                    errorLogText.AppendFormat("Source: {0}" + Environment.NewLine, ex.Source);
                    //errorLogText.AppendFormat("Call Stack: {0}" + Environment.NewLine, GetExceptionDetails(ex, _MaxExceptionTextLength / 2));
                }

                StringBuilder entryBuilder = new StringBuilder();
                entryBuilder.Append("******************************************************* ").Append(logLevel).Append(Environment.NewLine);
                entryBuilder.Append("Time Stamp: ");
                entryBuilder.Append(DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.CreateSpecificCulture("en-US")));
                entryBuilder.Append(" (UTC)  ");
                entryBuilder.Append(DateTime.UtcNow.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss.fff tt", CultureInfo.CreateSpecificCulture("en-US")));
                entryBuilder.Append(" (Local Time)");
                entryBuilder.Append(Environment.NewLine);
                entryBuilder.Append(errorLogText.ToString()).Append(Environment.NewLine);
                entryBuilder.Append("*******************************************************").Append(Environment.NewLine).Append(Environment.NewLine);

                // Try to write to the bounded channel. With DropOldest FullMode the channel should accept new entries
                // but if TryWrite returns false for any reason increment droppedCount as a fallback.
                if (!_writeChannel.Writer.TryWrite(entryBuilder.ToString()))
                {
                    Interlocked.Increment(ref _droppedCount);
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
                var reader = _writeChannel.Reader;
                while (await reader.WaitToReadAsync(token).ConfigureFalse())
                {
                    while (reader.TryRead(out var entry))
                    {
                        try
                        {
                            // If entries were dropped while the writer was busy, record a short summary first.
                            long dropped = Interlocked.Exchange(ref _droppedCount, 0);
                            if (dropped > 0)
                            {
                                string summary = $"*** Dropped {dropped} log entries due to full log queue. ***{Environment.NewLine}";
                                await File.AppendAllTextAsync(_logPath, summary, Encoding.UTF8, token).ConfigureFalse();
                            }

                            // Ensure directory exists in case it was removed after construction.
                            string? directory = Path.GetDirectoryName(_logPath);
                            if (!string.IsNullOrEmpty(directory))
                                Directory.CreateDirectory(directory);

                            // Use append semantics for each entry.
                            await File.AppendAllTextAsync(_logPath, entry, Encoding.UTF8, token).ConfigureFalse();

                            // Rotate if file exceeded the configured max size.
                            try
                            {
                                var fileInfo = new FileInfo(_logPath);
                                if (fileInfo.Exists && fileInfo.Length > _maxLogSize)
                                {
                                    await RotateLogFileAsync(token).ConfigureFalse();
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
            string ext = Path.GetExtension(_logPath);
            string fileNameOnly = Path.GetFileNameWithoutExtension(_logPath);
            string dir = Path.GetDirectoryName(_logPath) ?? string.Empty;
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
                        File.Replace(_logPath, oldLogPath, null);
                    }
                    else
                    {
                        // Destination doesn't exist — simple move is fine (atomic on same volume).
                        File.Move(_logPath, oldLogPath);
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
                        await Task.Delay(delayMs, token).ConfigureFalse();
                    }
                    catch
                    {
                        // Cancellation requested; exit early.
                        return;
                    }
                }
            }
        }

        private string GetExceptionDetails(System.Exception ex, int maxText = 2048)
        {
            if (ex == null) return string.Empty;
            var sb = new System.Text.StringBuilder();
            System.Exception? cur = ex;
            while (cur != null)
            {
                sb.AppendLine($"{cur.GetType().FullName}: {cur.Message}");
                // Fast fallback textual trace
                if (!string.IsNullOrEmpty(cur.StackTrace))
                {
                    sb.AppendLine(cur.StackTrace);
                }
                else
                {
                    // Structured frames (file/line require PDBs)
                    var st = new System.Diagnostics.StackTrace(cur, true);
                    var frames = st.GetFrames();
                    if (frames != null)
                    {
                        foreach (var f in frames)
                        {
                            var m = f.GetMethod();
                            sb.Append("   at ");
                            sb.Append(m?.DeclaringType?.FullName ?? "<unknown>");
                            sb.Append(".");
                            sb.Append(m?.Name ?? "<unknown>");
                            sb.Append(" in ");
                            sb.Append(f.GetFileName() ?? "<unknown file>");
                            sb.Append(":line ");
                            sb.Append(f.GetFileLineNumber());
                            sb.AppendLine();
                        }
                    }
                }

                cur = cur.InnerException;
                if (cur != null) sb.AppendLine("--- Inner Exception ---");
            }

            var text = sb.ToString();
            return text.Length <= maxText ? text : text.Substring(0, maxText) + "... [truncated]";
        }

        /// <summary>
        /// Signals the background writer to stop, waits for pending entries to be flushed and releases resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Stop accepting new writes, signal cancellation and wait for background task to finish.
                _writeChannel.Writer.TryComplete();
                _cts.Cancel();

                // Wait a short time for background writer to finish flushing. Avoid indefinite block.
                _backgroundWriterTask.Wait(5000);
            }
            catch
            {
                // Swallow any disposal exceptions.
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}