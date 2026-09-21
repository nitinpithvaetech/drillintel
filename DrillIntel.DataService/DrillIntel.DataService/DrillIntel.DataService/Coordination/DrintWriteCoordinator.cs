using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Serializes ALL writes to a .drint / .dintel file through a single dedicated DataServiceDIntel
    /// connection running on a background loop with bounded channel backpressure, resilient error
    /// isolation, and guarded WAL checkpointing.
    /// </summary>
    public sealed class DrintWriteCoordinator : IDrintWriteCoordinator
    {
        private readonly string _dbFilePath;
        private readonly int _boundedCapacity;
        private readonly Channel<IWriteRequest> _queue;
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _checkpointLock = new(1, 1);

        private DataServiceDIntel? _writerConnection;
        private Task? _pumpTask;
        private PeriodicTimer? _idleCheckpointTimer;
        private Task? _idleTimerTask;

        private long _writesSinceLastCheckpoint;
        private bool _isRunning;
        private bool _disposed;

        public bool IsRunning => _isRunning;
        public int PendingQueueCount => _queue.Reader.Count;
        public int CheckpointWriteThreshold { get; set; } = 2000;
        public TimeSpan IdleCheckpointInterval { get; set; } = TimeSpan.FromSeconds(30);

        public event EventHandler<WriteErrorEventArgs>? OnWriteError;

        public DrintWriteCoordinator(string dbFilePath, int boundedCapacity = 10000)
        {
            _dbFilePath = dbFilePath ?? throw new ArgumentNullException(nameof(dbFilePath));
            _boundedCapacity = boundedCapacity > 0 ? boundedCapacity : 10000;

            _queue = Channel.CreateBounded<IWriteRequest>(new BoundedChannelOptions(_boundedCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        /// <summary>
        /// Opens the dedicated writer connection and starts the background pump and checkpoint timer.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _writerConnection = new DataServiceDIntel(_dbFilePath);
            if (!_writerConnection.CheckConnection())
            {
                throw new InvalidOperationException($"Could not open write connection to '{_dbFilePath}': {_writerConnection.LastError}");
            }

            _isRunning = true;
            _pumpTask = Task.Run(PumpAsync);
            _idleCheckpointTimer = new PeriodicTimer(IdleCheckpointInterval);
            _idleTimerTask = Task.Run(IdleTimerLoopAsync);
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            Start();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Enqueues a write action. Awaits asynchronously if the bounded channel is full,
        /// providing non-blocking backpressure to the calling processor.
        /// </summary>
        public async Task EnqueueWriteAsync(Action<DataServiceDIntel> work, CancellationToken ct = default)
        {
            EnsureRunning();
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new ActionWriteRequest(work, tcs, ct);

            await _queue.Writer.WriteAsync(request, ct).ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);
        }

        public Task EnqueueWriteAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(new Action<DataServiceDIntel>(work), ct);

        /// <summary>
        /// Enqueues a write func that returns a result (e.g. generated ID or row count).
        /// </summary>
        public async Task<T> EnqueueWriteAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default)
        {
            EnsureRunning();
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new FuncWriteRequest<T>(work, tcs, ct);

            await _queue.Writer.WriteAsync(request, ct).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        public Task<T> EnqueueWriteAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(new Func<DataServiceDIntel, T>(work), ct);

        /// <summary>
        /// Enqueues an atomic transaction batch as a single unit in the write queue.
        /// Retries the entire transaction as a single unit on transient SQLITE_BUSY / LOCKED errors.
        /// </summary>
        public Task EnqueueTransactionAsync(Action<DataServiceDIntel> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(db => ExecuteTransactionWithRetry(db, work), ct);

        public Task EnqueueTransactionAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(db => ExecuteTransactionWithRetry(db, work), ct);

        /// <summary>
        /// Enqueues an atomic transaction batch returning a value.
        /// Retries the entire transaction as a single unit on transient SQLITE_BUSY / LOCKED errors.
        /// </summary>
        public Task<T> EnqueueTransactionAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(db => ExecuteTransactionWithRetry(db, work), ct);

        public Task<T> EnqueueTransactionAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default) =>
            EnqueueWriteAsync(db => ExecuteTransactionWithRetry(db, work), ct);

        private static void ExecuteTransactionWithRetry(DataServiceDIntel db, Action<DataServiceDIntel> work)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    if (!db.BeginTransaction())
                        throw new InvalidOperationException($"Could not start transaction: {db.LastError}");
                    try
                    {
                        work(db);
                        if (!db.Commit())
                            throw new InvalidOperationException($"Commit failed: {db.LastError}");
                        return;
                    }
                    catch
                    {
                        db.RollBack();
                        throw;
                    }
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when ((ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) && attempt < db.MaxRetryAttempts)
                {
                    attempt++;
                    Thread.Sleep(100 * attempt);
                }
            }
        }

        private static T ExecuteTransactionWithRetry<T>(DataServiceDIntel db, Func<DataServiceDIntel, T> work)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    if (!db.BeginTransaction())
                        throw new InvalidOperationException($"Could not start transaction: {db.LastError}");
                    try
                    {
                        var result = work(db);
                        if (!db.Commit())
                            throw new InvalidOperationException($"Commit failed: {db.LastError}");
                        return result;
                    }
                    catch
                    {
                        db.RollBack();
                        throw;
                    }
                }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when ((ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) && attempt < db.MaxRetryAttempts)
                {
                    attempt++;
                    Thread.Sleep(100 * attempt);
                }
            }
        }

        private async Task PumpAsync()
        {
            try
            {
                await foreach (var request in _queue.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                {
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        request.Cancel();
                        continue;
                    }

                    try
                    {
                        request.Execute(_writerConnection!);
                        request.Complete();

                        var count = Interlocked.Increment(ref _writesSinceLastCheckpoint);
                        if (count >= CheckpointWriteThreshold)
                        {
                            TryTriggerBackgroundCheckpoint();
                        }
                    }
                    catch (Exception ex)
                    {
                        request.Fail(ex);
                        try
                        {
                            OnWriteError?.Invoke(this, new WriteErrorEventArgs(ex, request.SqlDescription));
                        }
                        catch { /* error handler safety */ }
                    }
                }
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // Normal shutdown cancellation
            }
        }

        private async Task IdleTimerLoopAsync()
        {
            try
            {
                while (_idleCheckpointTimer != null && await _idleCheckpointTimer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
                {
                    // Only checkpoint on idle if there are no pending writes
                    if (_queue.Reader.Count == 0 && Interlocked.Read(ref _writesSinceLastCheckpoint) > 0)
                    {
                        await CheckpointInternalAsync(WalCheckpointMode.Passive, isNonBlockingGate: true, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { /* normal timer cancellation */ }
        }

        private void TryTriggerBackgroundCheckpoint()
        {
            _ = Task.Run(async () =>
            {
                await CheckpointInternalAsync(WalCheckpointMode.Passive, isNonBlockingGate: true, CancellationToken.None).ConfigureAwait(false);
            });
        }

        /// <summary>
        /// Manually triggers a WAL checkpoint operation, protected by the checkpoint concurrency gate.
        /// </summary>
        public Task<WalCheckpointResult> CheckpointAsync(WalCheckpointMode mode = WalCheckpointMode.Passive, CancellationToken ct = default) =>
            CheckpointInternalAsync(mode, isNonBlockingGate: false, ct);

        private async Task<WalCheckpointResult> CheckpointInternalAsync(WalCheckpointMode mode, bool isNonBlockingGate, CancellationToken ct)
        {
            if (_writerConnection is null || !_writerConnection.IsConnectionOpen())
                return new WalCheckpointResult(false, -1, 0, 0, "Write connection is not open.");

            if (isNonBlockingGate)
            {
                if (!await _checkpointLock.WaitAsync(0, ct).ConfigureAwait(false))
                    return new WalCheckpointResult(false, -1, 0, 0, "Checkpoint currently in progress.");
            }
            else
            {
                await _checkpointLock.WaitAsync(ct).ConfigureAwait(false);
            }

            try
            {
                var modeStr = mode.ToString().ToUpperInvariant();
                var sql = $"PRAGMA wal_checkpoint({modeStr});";
                var dt = _writerConnection.GetTable(sql);

                Interlocked.Exchange(ref _writesSinceLastCheckpoint, 0);

                if (dt.Rows.Count > 0)
                {
                    var busy = Convert.ToInt32(dt.Rows[0][0]);
                    var logFrames = Convert.ToInt32(dt.Rows[0][1]);
                    var checkpointed = Convert.ToInt32(dt.Rows[0][2]);
                    return new WalCheckpointResult(busy == 0, busy, logFrames, checkpointed);
                }

                return new WalCheckpointResult(true, 0, 0, 0);
            }
            catch (Exception ex)
            {
                return new WalCheckpointResult(false, -1, 0, 0, ex.Message);
            }
            finally
            {
                _checkpointLock.Release();
            }
        }

        /// <summary>
        /// Gracefully stops the coordinator: completes the queue, awaits draining of all queued writes,
        /// runs a TRUNCATE checkpoint, and closes the writer connection.
        /// </summary>
        public async Task StopAsync(CancellationToken ct = default)
        {
            if (!_isRunning) return;
            _isRunning = false;

            _idleCheckpointTimer?.Dispose();
            _idleCheckpointTimer = null;

            _queue.Writer.TryComplete();
            if (_pumpTask != null)
            {
                await _pumpTask.ConfigureAwait(false);
            }

            // Execute final TRUNCATE checkpoint to shrink WAL to 0 bytes
            await CheckpointInternalAsync(WalCheckpointMode.Truncate, isNonBlockingGate: false, ct).ConfigureAwait(false);

            _writerConnection?.Dispose();
            _writerConnection = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _cts.Cancel();
            await StopAsync().ConfigureAwait(false);
            _checkpointLock.Dispose();
            _cts.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private void EnsureRunning()
        {
            if (!_isRunning || _disposed)
                throw new InvalidOperationException("Write coordinator is not running or has been stopped.");
        }

        // ---------------------------------------------------------------
        // Internal Request abstractions
        // ---------------------------------------------------------------

        private interface IWriteRequest
        {
            CancellationToken CancellationToken { get; }
            string? SqlDescription { get; }
            void Execute(DataServiceDIntel db);
            void Complete();
            void Fail(Exception ex);
            void Cancel();
        }

        private sealed class ActionWriteRequest : IWriteRequest
        {
            private readonly Action<DataServiceDIntel> _work;
            private readonly TaskCompletionSource _tcs;

            public CancellationToken CancellationToken { get; }
            public string? SqlDescription => null;

            public ActionWriteRequest(Action<DataServiceDIntel> work, TaskCompletionSource tcs, CancellationToken ct)
            {
                _work = work;
                _tcs = tcs;
                CancellationToken = ct;
            }

            public void Execute(DataServiceDIntel db) => _work(db);
            public void Complete() => _tcs.TrySetResult();
            public void Fail(Exception ex) => _tcs.TrySetException(ex);
            public void Cancel() => _tcs.TrySetCanceled(CancellationToken);
        }

        private sealed class FuncWriteRequest<T> : IWriteRequest
        {
            private readonly Func<DataServiceDIntel, T> _work;
            private readonly TaskCompletionSource<T> _tcs;
            private T? _result;

            public CancellationToken CancellationToken { get; }
            public string? SqlDescription => null;

            public FuncWriteRequest(Func<DataServiceDIntel, T> work, TaskCompletionSource<T> tcs, CancellationToken ct)
            {
                _work = work;
                _tcs = tcs;
                CancellationToken = ct;
            }

            public void Execute(DataServiceDIntel db) => _result = _work(db);
            public void Complete() => _tcs.TrySetResult(_result!);
            public void Fail(Exception ex) => _tcs.TrySetException(ex);
            public void Cancel() => _tcs.TrySetCanceled(CancellationToken);
        }
    }
}

