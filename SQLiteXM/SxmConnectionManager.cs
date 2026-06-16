using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SQLiteXM
{
    /// <summary>
    /// Lightweight manager that hands out connection leases and provides deterministic shutdown
    /// and worker execution semantics for named database connections.
    /// </summary>
    /// <remarks>
    /// The manager maintains a map of named connection entries. Each entry contains a shared
    /// <see cref="SxmConnection"/> instance and a reference count representing active leases.
    /// Callers acquire leases via <see cref="AcquireConnectionLease"/>; leases must be disposed
    /// (preferably via <c>await using</c>) to decrement the reference count. Shutdown is
    /// deterministic: <see cref="ShutdownAsync"/> marks an entry as closing, waits for the
    /// reference count to reach zero (or the <paramref name="ct"/> to cancel), then destroys
    /// the underlying connection. <see cref="RunWorkersAsync"/> acquires leases for all worker
    /// delegates up-front to avoid races with shutdown and executes the workers concurrently
    /// using a shared connection instance.
    /// </remarks>
    public sealed class SxmConnectionManager
    {
        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static SxmConnectionManager Instance { get; } = new SxmConnectionManager();

        private class Entry
        {
            /// <summary>
            /// Shared connection instance for this named entry. May be null until first acquired.
            /// </summary>
            public SxmConnection? Connection;

            /// <summary>
            /// Number of active leases referencing <see cref="Connection"/>.
            /// </summary>
            public int RefCount;

            /// <summary>
            /// True when a shutdown is in progress for this entry.
            /// </summary>
            public bool Closing;

            /// <summary>
            /// TaskCompletionSource used to signal when <see cref="RefCount"/> reaches zero during shutdown.
            /// </summary>
            public TaskCompletionSource<object?>? Tcs;

            /// <summary>
            /// Per-entry synchronization object.
            /// </summary>
            public readonly object Sync = new object();
        }

        private readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.OrdinalIgnoreCase);

        private SxmConnectionManager() { }

        /// <summary>
        /// Acquire a connection lease for the specified database name.
        /// </summary>
        /// <param name="databaseName">Name of the database (case-insensitive) to acquire a connection for.</param>
        /// <returns>A <see cref="ConnectionLease"/> representing the acquired lease. Dispose the lease to release it.</returns>
        /// <remarks>
        /// If the named entry does not yet have a <see cref="SxmConnection"/>, one is created and shared
        /// among all leases for the same database name. The method increments the per-entry reference
        /// count and returns a lease that must be disposed to decrement the count.
        /// </remarks>
        internal ConnectionLease AcquireConnectionLease(string databaseName)
        {
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));
            Entry entry = _map.GetOrAdd(databaseName, _ => new Entry());

            lock (entry.Sync)
            {
                if (entry.Closing)
                {
                    throw new SxmException(new ErrorMessage(SxmDefines.SxmErrorCode.AcquireLease, databaseName));
                }

                if (entry.Connection == null)
                    entry.Connection = new SxmConnection(databaseName, shared: true);

                entry.RefCount++;
                return new ConnectionLease(this, databaseName, entry.Connection);
            }
        }

        /// <summary>
        /// Release a previously acquired lease for the specified database name.
        /// </summary>
        /// <param name="databaseName">The database name whose lease is being released.</param>
        /// <remarks>
        /// Called by <see cref="ConnectionLease.DisposeAsync"/>. If the entry is marked as closing
        /// and the reference count reaches zero this will signal any pending shutdown wait task.
        /// </remarks>
        internal void Release(string databaseName)
        {
            if (!_map.TryGetValue(databaseName, out Entry? entry))
                return;

            lock (entry.Sync)
            {
                if (entry.RefCount == 0)
                {
                    throw new InvalidOperationException($"Release called with no active leases for '{databaseName}'.");
                }

                if (entry.RefCount > 0)
                    entry.RefCount--;

                if (entry.RefCount == 0 && entry.Closing)
                {
                    entry.Tcs?.TrySetResult(null);
                }
            }
        }

        /// <summary>
        /// Deterministically shuts down and destroys the connection for the specified database.
        /// </summary>
        /// <param name="databaseName">The name of the database to shut down.</param>
        /// <param name="ct">Cancellation token used to abort waiting for active leases to be released.</param>
        /// <returns>A task that completes when the connection has been destroyed or the operation is cancelled.</returns>
        /// <remarks>
        /// If no entry exists this method returns immediately. The entry is marked closing and, if leases are
        /// active, this method waits until the per-entry reference count reaches zero (or <paramref name="ct"/>
        /// cancels). When shutdown proceeds, the connection is claimed under the entry lock, removed from the
        /// manager, and released asynchronously via <see cref="SxmConnection.ReleaseConnectionAsync"/>.
        /// Concurrent shutdown calls are safe; the underlying connection is released at most once.
        /// </remarks>
        public async Task ShutdownAsync(string? databaseName, CancellationToken ct = default)
        {
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));

            if (!_map.TryGetValue(databaseName, out Entry? entry))
                return;

            Task? waitTask = null;
            TaskCompletionSource<object?>? tcs = null;
            SxmConnection? connectionToRelease = null;
            bool releaseNow = false;

            lock (entry.Sync)
            {
                if (entry.Closing)
                {
                    if (entry.RefCount == 0)
                    {
                        connectionToRelease = entry.Connection;
                        entry.Connection = null;
                        entry.Tcs = null;
                        _map.TryRemove(databaseName, out _);
                        releaseNow = true;
                    }
                    else
                    {
                        entry.Tcs ??= new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        tcs = entry.Tcs;
                        waitTask = tcs.Task;
                    }
                }
                else
                {
                    entry.Closing = true;

                    if (entry.RefCount == 0)
                    {
                        connectionToRelease = entry.Connection;
                        entry.Connection = null;
                        _map.TryRemove(databaseName, out _);
                        releaseNow = true;
                    }
                    else
                    {
                        entry.Tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        tcs = entry.Tcs;
                        waitTask = tcs.Task;
                    }
                }
            }

            if (releaseNow)
            {
                if (connectionToRelease != null)
                    await connectionToRelease.ReleaseConnectionAsync(destroy: true, ct).ConfigureFalse();

                return;
            }

            if (waitTask != null)
            {
                await waitTask.WaitAsync(ct).ConfigureFalse();
            }

            lock (entry.Sync)
            {
                connectionToRelease = entry.Connection;
                entry.Connection = null;
                _map.TryRemove(databaseName, out _);
            }

            if (connectionToRelease != null)
                await connectionToRelease.ReleaseConnectionAsync(destroy: true, ct).ConfigureFalse();
        }

        /// <summary>
        /// Acquire leases for all provided worker delegates and run the workers concurrently.
        /// </summary>
        /// <param name="databaseName">The database name to use for the shared connection.</param>
        /// <param name="workersEnum">Worker delegates that accept an <see cref="SxmConnection"/> and return a <see cref="Task"/>.</param>
        /// <param name="ct">
        /// Cancellation token observed before each worker is scheduled. Already-started workers are not
        /// cancelled automatically and must observe cancellation through their own mechanisms.
        /// </param>
        /// <returns>A task that completes once all scheduled workers have finished or an exception is thrown.</returns>
        /// <remarks>
        /// This method acquires leases for all workers up-front to prevent races with shutdown. All
        /// workers are executed using the same shared <see cref="SxmConnection"/> instance (the connection
        /// referenced by the first lease). Workers that require exclusive access must coordinate by using
        /// <see cref="SxmConnection.LockAsync"/> (or equivalent) on the shared connection.
        /// </remarks>
        public async Task RunWorkersAsync(string? databaseName, IEnumerable<Func<SxmConnection, Task>> workersEnum, CancellationToken ct = default)
        {
            if (workersEnum == null) throw new ArgumentNullException(nameof(workersEnum));
            if (databaseName == null) throw new ArgumentNullException(nameof(databaseName));

            // Materialize workers to avoid double-enumeration / side-effects.
            //List<Func<SxmConnection, Task>> workerList = workersEnum as List<Func<SxmConnection, Task>> ?? workersEnum.ToList();
            IReadOnlyCollection<Func<SxmConnection, Task>> workerCollection = workersEnum as IReadOnlyCollection<Func<SxmConnection, Task>> ?? workersEnum.ToList();

            foreach (Func<SxmConnection, Task>? worker in workerCollection)
            {
                if (worker == null)
                {
                    throw new ArgumentException("The workers collection must not contain null delegates.", nameof(workersEnum));
                }
            }

            // Acquire leases for all workers up front
            List<ConnectionLease> leases = new List<ConnectionLease>(workerCollection.Count);
            try
            {
                foreach (Func<SxmConnection, Task> _ in workerCollection)
                {
                    // This will throw if the connection is already closing.
                    leases.Add(AcquireConnectionLease(databaseName));
                }

                // Now run the worker delegates — use a single shared SxmConnection instance from the leases.
                // All leases reference the same SxmConnection instance; use the Connection from leases[0].
                if (leases.Count == 0) return;

                SxmConnection sharedConn = leases[0].Connection;

                List<Task> tasks = new List<Task>(workerCollection.Count);
                foreach (Func<SxmConnection, Task> worker in workerCollection)
                {
                    // Launch each worker using the same sharedConn; workers must still coordinate via SxmConnection.LockAsync if they need exclusive access.
                    tasks.Add(Task.Run(async () =>
                    {
                        await worker(sharedConn).ConfigureFalse();
                    }, ct));
                }

                await Task.WhenAll(tasks).ConfigureFalse();
            }
            finally
            {
                // Release all leases asynchronously in reverse acquisition order (LIFO).
                for (int i = leases.Count - 1; i >= 0; i--)
                {
                    ConnectionLease lease = leases[i];
                    try
                    {
                        await lease.DisposeAsync().ConfigureFalse();
                    }
                    catch (Exception ex)
                    {
                        // Best-effort: log and continue releasing remaining leases.
                        SxmLogging.Log(ex, $"Connection lease DisposeAsync failure. Database: '{lease.DatabaseName}'.");
                    }
                }
            }
        }

        /// <summary>
        /// Lightweight lease representing an acquired reference to a shared <see cref="SxmConnection"/>.
        /// </summary>
        /// <remarks>
        /// Disposing the lease (via <see cref="DisposeAsync"/>) releases the underlying reference
        /// held in the manager for the named database. The lease contains the shared connection
        /// instance in <see cref="Connection"/> and the database name in <see cref="DatabaseName"/>.
        /// Prefer using <c>await using</c> to ensure deterministic release.
        /// </remarks>
        internal readonly struct ConnectionLease : IAsyncDisposable
        {
            private readonly SxmConnectionManager _manager;

            /// <summary>
            /// The shared <see cref="SxmConnection"/> instance for the acquired lease.
            /// </summary>
            public SxmConnection Connection { get; }

            /// <summary>
            /// The database name associated with this lease.
            /// </summary>
            public string DatabaseName { get; }

            internal ConnectionLease(SxmConnectionManager manager, string dbName, SxmConnection conn)
            {
                this._manager = manager;
                Connection = conn;
                DatabaseName = dbName;
            }

            /// <summary>
            /// Release the lease back to the manager. This decrements the per-entry reference count.
            /// </summary>
            /// <returns>A completed <see cref="ValueTask"/>. The operation is synchronous.</returns>
            public ValueTask DisposeAsync()
            {
                _manager.Release(DatabaseName);
                return ValueTask.CompletedTask;
            }
        }
    }
}