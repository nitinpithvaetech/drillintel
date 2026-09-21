using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Manages the lifecycle of an open project file (.dintel / .drint).
    /// Tracks all active processor contexts and reader connections, guaranteeing that
    /// all readers are closed before executing the final TRUNCATE checkpoint on shutdown.
    /// </summary>
    public sealed class DrintProjectScope : IDrintProjectScope
    {
        private readonly string _projectFilePath;
        private readonly string _projectName;
        private readonly DrintWriteCoordinator _writeCoordinator;
        private readonly ConcurrentDictionary<IDrintProcessorContext, byte> _activeContexts = new();
        private readonly ConcurrentDictionary<DataServiceDIntel, byte> _activeReaders = new();
        private readonly CancellationTokenSource _scopeCts = new();

        private bool _isOpen;
        private bool _disposed;

        public string ProjectFilePath => _projectFilePath;
        public string ProjectName => _projectName;
        public bool IsOpen => _isOpen;
        public IDrintWriteCoordinator WriteCoordinator => _writeCoordinator;
        public CancellationToken ScopeCancellationToken => _scopeCts.Token;

        public DrintProjectScope(string projectFilePath, int writeQueueCapacity = 10000)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
                throw new ArgumentException("Project file path is required.", nameof(projectFilePath));

            _projectFilePath = projectFilePath;
            _projectName = Path.GetFileNameWithoutExtension(projectFilePath);

            _writeCoordinator = new DrintWriteCoordinator(_projectFilePath, writeQueueCapacity);
            _writeCoordinator.Start();
            _isOpen = true;
        }

        /// <summary>
        /// Creates an isolated processor context for a background calculation processor.
        /// Tracks the context so it can be gracefully closed and disposed on project shutdown.
        /// </summary>
        public IDrintProcessorContext CreateProcessorContext()
        {
            EnsureOpen();
            var context = new TrackedProcessorContext(_projectFilePath, _writeCoordinator, this);
            _activeContexts.TryAdd(context, 0);
            return context;
        }

        /// <summary>
        /// Creates a standalone reader connection for queries that do not require processor wrapping.
        /// </summary>
        public IDataServiceDIntel CreateReadConnection()
        {
            EnsureOpen();
            var reader = new DataServiceDIntel(_projectFilePath);
            _activeReaders.TryAdd(reader, 0);
            return reader;
        }

        internal void UnregisterContext(IDrintProcessorContext context)
        {
            _activeContexts.TryRemove(context, out _);
        }

        internal void UnregisterReader(DataServiceDIntel reader)
        {
            _activeReaders.TryRemove(reader, out _);
        }

        /// <summary>
        /// Closes the project in strict sequence:
        /// 1. Signals cancellation to all in-flight operations.
        /// 2. Disposes and closes all active reader contexts and connections.
        /// 3. Executes coordinator shutdown and PRAGMA wal_checkpoint(TRUNCATE) with zero live readers.
        /// </summary>
        public async Task CloseAsync(CancellationToken ct = default)
        {
            if (!_isOpen) return;
            _isOpen = false;

            // 1. Signal cancellation to in-flight scope operations
            _scopeCts.Cancel();

            // 2. Force-close all outstanding processor contexts (disposes their private reader connections)
            foreach (var context in _activeContexts.Keys)
            {
                try { context.Dispose(); } catch { /* best-effort cleanup */ }
            }
            _activeContexts.Clear();

            // 3. Close any standalone reader connections
            foreach (var reader in _activeReaders.Keys)
            {
                try { reader.Dispose(); } catch { /* best-effort cleanup */ }
            }
            _activeReaders.Clear();

            // 4. Now that NO readers hold SQLite locks, stop writer & execute TRUNCATE checkpoint
            await _writeCoordinator.StopAsync(ct).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await CloseAsync().ConfigureAwait(false);
            _writeCoordinator.Dispose();
            _scopeCts.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        private void EnsureOpen()
        {
            if (!_isOpen || _disposed)
                throw new ObjectDisposedException(nameof(DrintProjectScope), $"Project scope for '{_projectFilePath}' is closed or disposed.");
        }

        /// <summary>
        /// Internal wrapper that unregisters itself from the parent scope upon disposal.
        /// </summary>
        private sealed class TrackedProcessorContext : DrintProcessorContext
        {
            private readonly DrintProjectScope _scope;

            public TrackedProcessorContext(string dbFilePath, IDrintWriteCoordinator coordinator, DrintProjectScope scope)
                : base(dbFilePath, coordinator)
            {
                _scope = scope;
            }

            public new void Dispose()
            {
                _scope.UnregisterContext(this);
                base.Dispose();
            }
        }
    }
}

