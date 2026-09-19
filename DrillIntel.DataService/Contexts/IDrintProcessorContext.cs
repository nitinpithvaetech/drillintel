using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Contract for calculation processors.
    /// Strictly isolates concurrent direct reads on a dedicated connection while forcing
    /// all writes through the shared coordinator to eliminate SQLite write collisions.
    /// </summary>
    public interface IDrintProcessorContext : IAsyncDisposable, IDisposable
    {
        string LastError { get; }
        bool IsDisposed { get; }

        // Concurrent Reads (Direct)
        DataTable GetTable(string sql, IDictionary<string, object?>? parameters = null);
        Task<DataTable> GetTableAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
        object? GetValue(string sql, IDictionary<string, object?>? parameters = null);
        Task<object?> GetValueAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
        string GetStringValue(string sql);
        double GetDoubleValue(string sql);
        bool TableExists(string tableName);
        bool RecordExists(string sql);

        // Serialized Writes (Routed via Coordinator)
        Task WriteAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
        Task<T> WriteAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default);
        Task<T> WriteAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default);
        Task WriteTransactionAsync(Action<DataServiceDIntel> work, CancellationToken ct = default);
        Task WriteTransactionAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default);
        Task<T> WriteTransactionAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default);
        Task<T> WriteTransactionAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default);
    }
}

