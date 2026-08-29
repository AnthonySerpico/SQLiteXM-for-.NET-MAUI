using System;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    public static class SxmLifecycleManager
    {
        private static TimeSpan _suspendGracePeriod = TimeSpan.FromSeconds(5);

        private static int _suspended; // 0 = active, 1 = suspended

        private static CancellationTokenSource? _cts;

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

        public static void OnResume()
        {
            if (Interlocked.Exchange(ref _suspended, 0) == 0)
                return;

            SxmConnection.AllowNewOperations();
        }
    }
}