using System;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Manages SQLite connection lifecycle events during application suspend and resume operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides hooks for mobile application lifecycle events (sleep/resume) to ensure
    /// that SQLite connections are properly quiesced before the application is suspended. This prevents
    /// database corruption and ensures graceful handling of background/foreground transitions.
    /// </para>
    /// <para>
    /// The manager implements a grace period mechanism that allows in-flight operations to complete
    /// before fully blocking new database operations when the application enters a suspended state.
    /// </para>
    /// <para>
    /// This class is thread-safe. Multiple calls to <see cref="OnSleep"/> or <see cref="OnResume"/>
    /// are handled safely using atomic operations.
    /// </para>
    /// </remarks>
    public static class SxmLifecycleManager
    {
        private static TimeSpan _suspendGracePeriod = TimeSpan.FromSeconds(5);

        private static int _suspended; // 0 = active, 1 = suspended

        private static CancellationTokenSource? _cts;

        /// <summary>
        /// Gets or sets the grace period to wait for in-flight operations to complete when the application is suspending.
        /// </summary>
        /// <value>The time span to wait before fully blocking operations. Default is 5 seconds.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when attempting to set a negative time span.</exception>
        /// <remarks>
        /// <para>
        /// When <see cref="OnSleep"/> is called, new database operations are immediately blocked,
        /// but the system waits for this grace period to allow existing operations to complete.
        /// This helps prevent abrupt termination of active database transactions.
        /// </para>
        /// <para>
        /// Setting this value affects future suspend operations only. Active suspend operations
        /// use the grace period that was configured when they started.
        /// </para>
        /// </remarks>
        public static TimeSpan SuspendGracePeriod
        {
            get => _suspendGracePeriod;
            set
            {
                if (value < TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _suspendGracePeriod = value;
            }
        }

        /// <summary>
        /// Called when the application is entering a suspended state (sleep).
        /// Blocks new database operations and waits for in-flight operations to complete.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method should be called from your application's lifecycle suspend handler
        /// (e.g., in MAUI's <c>OnSleep</c> override).
        /// </para>
        /// <para>
        /// The method performs the following actions:
        /// </para>
        /// <list type="number">
        /// <item><description>Atomically marks the system as suspended to prevent duplicate suspend operations</description></item>
        /// <item><description>Calls <see cref="SxmConnection.BlockNewOperations"/> to prevent new database operations</description></item>
        /// <item><description>Waits for the configured <see cref="SuspendGracePeriod"/> to allow existing operations to complete</description></item>
        /// </list>
        /// <para>
        /// If the application is already suspended, this method returns immediately without taking any action.
        /// </para>
        /// <para>
        /// This method blocks for the duration of the grace period. On mobile platforms with strict
        /// suspend timing requirements, ensure the grace period is set appropriately.
        /// </para>
        /// </remarks>
        public static void OnSleep  ()
        {
            if (Interlocked.Exchange(ref _suspended, 1) == 1)
                return;

            SxmConnection.BlockNewOperations();

            try
            {
                Thread.Sleep(_suspendGracePeriod);  // Actually blocks, giving real time
            }
            catch (ArgumentOutOfRangeException)
            {
                // resumed before grace period finished
            }
        }

        /// <summary>
        /// Called when the application is resuming from a suspended state.
        /// Allows database operations to proceed normally.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method should be called from your application's lifecycle resume handler
        /// (e.g., in MAUI's <c>OnResume</c> override).
        /// </para>
        /// <para>
        /// The method atomically marks the system as active and calls <see cref="SxmConnection.AllowNewOperations"/>
        /// to resume normal database operation.
        /// </para>
        /// <para>
        /// If the application is not currently suspended, this method returns immediately without taking any action.
        /// This makes it safe to call <c>OnResume</c> multiple times or when the suspend state is uncertain.
        /// </para>
        /// </remarks>
        public static void OnResume()
        {
            if (Interlocked.Exchange(ref _suspended, 0) == 0)
                return;

            SxmConnection.AllowNewOperations();
        }
    }
}