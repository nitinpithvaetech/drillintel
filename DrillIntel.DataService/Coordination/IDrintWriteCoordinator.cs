using System;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Contract for the coordinator that serializes all SQLite write operations to a .drint / .dintel file.
    /// </summary>
    public interface IDrintWriteCoordinator : IAsyncDisposable, IDisposable
    {
        bool IsRunning { get; }
        int PendingQueueCount { get; }
        int CheckpointWriteThreshold { get; set; }

        event EventHandler<WriteErrorEventArgs>? OnWriteError;

        void Start();
        Task StartAsync(CancellationToken ct = default);

        Task EnqueueWriteAsync(Action<DataServiceDIntel> work, CancellationToken ct = default);
        Task EnqueueWriteAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default);
        Task<T> EnqueueWriteAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default);
        Task<T> EnqueueWriteAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default);

        Task EnqueueTransactionAsync(Action<DataServiceDIntel> work, CancellationToken ct = default);
        Task EnqueueTransactionAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default);
        Task<T> EnqueueTransactionAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default);
        Task<T> EnqueueTransactionAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default);

        Task<WalCheckpointResult> CheckpointAsync(WalCheckpointMode mode = WalCheckpointMode.Passive, CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
    }
}

