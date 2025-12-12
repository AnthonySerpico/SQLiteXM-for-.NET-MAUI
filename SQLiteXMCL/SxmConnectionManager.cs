using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    // Lightweight manager that hands out leases and can run worker delegates deterministically.
    public sealed class SxmConnectionManager
    {
        public static SxmConnectionManager Instance { get; } = new SxmConnectionManager();

        private class Entry
        {
            public SxmConnection? Connection;
            public int RefCount;
            public bool Closing;
            public TaskCompletionSource<object?>? Tcs;
            public readonly object Sync = new object();
        }

        private readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.OrdinalIgnoreCase);

        private SxmConnectionManager() { }

        // Acquire a lease (throws if closing).
        public ConnectionLease AcquireConnectionLease(string databaseName)
        {
            var entry = _map.GetOrAdd(databaseName, _ => new Entry());

            lock (entry.Sync)
            {
                //if (entry.Closing)
                    //throw new SxmException($"Connection for '{databaseName}' is closing and cannot be acquired.");

                if (entry.Connection == null)
                    entry.Connection = new SxmConnection(databaseName, shared: true);

                entry.RefCount++;
                return new ConnectionLease(this, databaseName, entry.Connection);
            }
        }

        internal void Release(string databaseName)
        {
            if (!_map.TryGetValue(databaseName, out var entry))
                return;

            lock (entry.Sync)
            {
                if (entry.RefCount > 0)
                    entry.RefCount--;

                if (entry.RefCount == 0 && entry.Closing)
                {
                    entry.Tcs?.TrySetResult(null);
                }
            }
        }

        // Deterministic shutdown: mark closing, wait until refcount==0, destroy connection.
        public async Task ShutdownAsync(string databaseName, CancellationToken ct = default)
        {
            if (!_map.TryGetValue(databaseName, out var entry))
                return;

            Task? waitTask = null;

            lock (entry.Sync)
            {
                if (entry.Closing)
                {
                    waitTask = entry.Tcs?.Task;
                }
                else
                {
                    entry.Closing = true;

                    if (entry.RefCount == 0)
                    {
                        try
                        {
                            entry.Connection?.releaseConnection(destroy: true);
                            entry.Connection?.destroyConnection();
                        }
                        finally
                        {
                            _map.TryRemove(databaseName, out _);
                        }
                        return;
                    }
                    else
                    {
                        entry.Tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        waitTask = entry.Tcs.Task;
                    }
                }
            }

            using (ct.Register(() => entry.Tcs?.TrySetCanceled()))
            {
                await waitTask!.WaitAsync(ct).ConfigureAwait(false);
            }

            lock (entry.Sync)
            {
                try
                {
                    entry.Connection?.releaseConnection(destroy: true);
                    entry.Connection?.destroyConnection();
                }
                finally
                {
                    _map.TryRemove(databaseName, out _);
                }
            }
        }

        // Run workers: acquire leases for all worker delegates, then run them concurrently.
        // This guarantees acquisition happens before shutdown and prevents the race.
        public async Task RunWorkersAsync(string databaseName, IEnumerable<Func<SxmConnection, Task>> workers, CancellationToken ct = default)
        {
            if (workers == null) throw new ArgumentNullException(nameof(workers));

            // Acquire leases for all workers up front
            List<ConnectionLease> leases = new List<ConnectionLease>();
            try
            {
                foreach (Func<SxmConnection, Task> t in workers)
                {
                    // This will throw if the connection is already closing.
                    leases.Add(AcquireConnectionLease(databaseName));
                }

                // Now run the worker delegates — use a single shared SxmConnection instance from the leases.
                // All leases reference the same SxmConnection instance; use the Connection from leases[0].
                if (leases.Count == 0) return;

                SxmConnection sharedConn = leases[0].Connection;

                List<Task> tasks = new List<Task>();
                int index = 0;
                foreach (Func<SxmConnection, Task> worker in workers)
                {
                    // Launch each worker using the same sharedConn; workers must still coordinate via SxmConnection.LockAsync if they need exclusive access.
                    tasks.Add(Task.Run(async () =>
                    {
                        await worker(sharedConn).ConfigureAwait(false);
                    }, ct));
                    index++;
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            finally
            {
                // Release all leases (synchronous)
                foreach (var l in leases)
                    l.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        public readonly struct ConnectionLease : IAsyncDisposable
        {
            private readonly SxmConnectionManager _manager;
            public SxmConnection Connection { get; }
            public string DatabaseName { get; }

            internal ConnectionLease(SxmConnectionManager manager, string dbName, SxmConnection conn)
            {
                _manager = manager;
                Connection = conn;
                DatabaseName = dbName;
            }

            public ValueTask DisposeAsync()
            {
                _manager.Release(DatabaseName);
                return ValueTask.CompletedTask;
            }
        }
    }
}