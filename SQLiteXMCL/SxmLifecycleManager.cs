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

        public static async Task OnSleepAsync()
        {
            if (Interlocked.Exchange(ref _suspended, 1) == 1)
                return;

            // cancel any prior sleep delay
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            SxmConnection.BlockNewOperations();

            try
            {
                await Task.Delay(_suspendGracePeriod, _cts.Token)
                          .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // resumed before grace period finished
            }
        }

        public static void OnResume()
        {
            _cts?.Cancel();

            if (Interlocked.Exchange(ref _suspended, 0) == 0)
                return;

            SxmConnection.AllowNewOperations();
        }
    }
}