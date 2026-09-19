using System;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Thread-safe singleton manager governing the active project scope for the desktop application.
    /// Handles opening, closing, and switching .dintel / .drint files without lock leaks.
    /// </summary>
    public sealed class DrintProjectScopeManager : IDrintProjectScopeManager
    {
        private readonly SemaphoreSlim _switchLock = new(1, 1);
        private IDrintProjectScope? _currentScope;
        private bool _disposed;

        public IDrintProjectScope? CurrentScope => _currentScope;
        public bool HasActiveProject => _currentScope != null && _currentScope.IsOpen;

        public event EventHandler<ProjectScopeChangedEventArgs>? ScopeChanged;

        /// <summary>
        /// Opens a project file. If another project is currently open, it is cleanly closed first,
        /// ensuring all readers are disposed and the WAL file is truncated before opening the new file.
        /// </summary>
        public async Task<IDrintProjectScope> OpenProjectAsync(string dbFilePath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dbFilePath))
                throw new ArgumentException("Database file path is required.", nameof(dbFilePath));

            await _switchLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var oldScope = _currentScope;
                if (oldScope != null)
                {
                    await oldScope.CloseAsync(ct).ConfigureAwait(false);
                    await oldScope.DisposeAsync().ConfigureAwait(false);
                    _currentScope = null;
                }

                var newScope = new DrintProjectScope(dbFilePath);
                _currentScope = newScope;

                ScopeChanged?.Invoke(this, new ProjectScopeChangedEventArgs(oldScope, newScope));
                return newScope;
            }
            finally
            {
                _switchLock.Release();
            }
        }

        /// <summary>
        /// Closes the currently active project scope.
        /// </summary>
        public async Task CloseProjectAsync(CancellationToken ct = default)
        {
            await _switchLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var oldScope = _currentScope;
                if (oldScope != null)
                {
                    await oldScope.CloseAsync(ct).ConfigureAwait(false);
                    await oldScope.DisposeAsync().ConfigureAwait(false);
                    _currentScope = null;
                    ScopeChanged?.Invoke(this, new ProjectScopeChangedEventArgs(oldScope, null));
                }
            }
            finally
            {
                _switchLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await CloseProjectAsync().ConfigureAwait(false);
            _switchLock.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}

