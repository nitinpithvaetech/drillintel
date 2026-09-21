using System;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Encapsulates the lifecycle of an open project file (.dintel / .drint),
    /// managing its write coordinator, reader connections, and processor contexts.
    /// </summary>
    public interface IDrintProjectScope : IAsyncDisposable, IDisposable
    {
        string ProjectFilePath { get; }
        string ProjectName { get; }
        bool IsOpen { get; }
        IDrintWriteCoordinator WriteCoordinator { get; }

        IDrintProcessorContext CreateProcessorContext();
        IDataServiceDIntel CreateReadConnection();
        Task CloseAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Event arguments for project scope lifecycle changes.
    /// </summary>
    public sealed class ProjectScopeChangedEventArgs : EventArgs
    {
        public IDrintProjectScope? OldScope { get; }
        public IDrintProjectScope? NewScope { get; }

        public ProjectScopeChangedEventArgs(IDrintProjectScope? oldScope, IDrintProjectScope? newScope)
        {
            OldScope = oldScope;
            NewScope = newScope;
        }
    }

    /// <summary>
    /// Application-wide manager that governs the currently open project scope,
    /// guaranteeing clean shutdown of writers and readers during project switches.
    /// </summary>
    public interface IDrintProjectScopeManager : IAsyncDisposable, IDisposable
    {
        IDrintProjectScope? CurrentScope { get; }
        bool HasActiveProject { get; }

        event EventHandler<ProjectScopeChangedEventArgs>? ScopeChanged;

        Task<IDrintProjectScope> OpenProjectAsync(string dbFilePath, CancellationToken ct = default);
        Task CloseProjectAsync(CancellationToken ct = default);
    }
}

