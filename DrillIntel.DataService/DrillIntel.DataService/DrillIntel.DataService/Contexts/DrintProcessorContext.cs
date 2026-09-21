using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// The isolated execution context provided to each calculation processor
    /// (e.g. ProcessDrillingConn, ProcessRigState, ProcessTripConnection).
    ///
    /// Directly routes reads to the processor's own dedicated DataServiceDIntel connection (WAL),
    /// while strictly forcing all writes through the shared IDrintWriteCoordinator.
    /// Exposes no direct write methods on the reader connection, eliminating write lock collisions.
    /// </summary>
    public class DrintProcessorContext : IDrintProcessorContext
    {
        private readonly DataServiceDIntel _reader;
        private readonly IDrintWriteCoordinator _writer;
        private bool _disposed;

        public string LastError => _reader.LastError;
        public bool IsDisposed => _disposed;

        public DrintProcessorContext(string dbFilePath, IDrintWriteCoordinator sharedWriteCoordinator)
        {
            if (string.IsNullOrWhiteSpace(dbFilePath))
                throw new ArgumentException("Database file path is required.", nameof(dbFilePath));

            _writer = sharedWriteCoordinator ?? throw new ArgumentNullException(nameof(sharedWriteCoordinator));
            _reader = new DataServiceDIntel(dbFilePath);
        }

        // -----------------------------------------------------------------
        // Concurrent Reads (Direct to dedicated connection)
        // -----------------------------------------------------------------

        public DataTable GetTable(string sql, IDictionary<string, object?>? parameters = null)
        {
            ThrowIfDisposed();
            return _reader.GetTable(sql, parameters);
        }

        public Task<DataTable> GetTableAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _reader.GetTableAsync(sql, parameters, ct);
        }

        public object? GetValue(string sql, IDictionary<string, object?>? parameters = null)
        {
            ThrowIfDisposed();
            return _reader.GetValueFromDatabase(sql, parameters);
        }

        public Task<object?> GetValueAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _reader.GetValueAsync(sql, parameters, ct);
        }

        public string GetStringValue(string sql)
        {
            ThrowIfDisposed();
            return _reader.GetStringValueFromDatabase(sql);
        }

        public double GetDoubleValue(string sql)
        {
            ThrowIfDisposed();
            return _reader.GetDoubleValueFromDatabase(sql);
        }

        public bool TableExists(string tableName)
        {
            ThrowIfDisposed();
            return _reader.TableExists(tableName);
        }

        public bool RecordExists(string sql)
        {
            ThrowIfDisposed();
            return _reader.IsRecordExist(sql);
        }

        // -----------------------------------------------------------------
        // Serialized Writes (Enqueued to shared coordinator)
        // -----------------------------------------------------------------

        public Task WriteAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueWriteAsync(db => db.ExecuteNonQuery(sql, parameters), ct);
        }

        public Task<T> WriteAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueWriteAsync(work, ct);
        }

        public Task<T> WriteAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueWriteAsync(work, ct);
        }

        public Task WriteTransactionAsync(Action<DataServiceDIntel> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueTransactionAsync(work, ct);
        }

        public Task WriteTransactionAsync(Action<IDataServiceDIntel> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueTransactionAsync(work, ct);
        }

        public Task<T> WriteTransactionAsync<T>(Func<DataServiceDIntel, T> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueTransactionAsync(work, ct);
        }

        public Task<T> WriteTransactionAsync<T>(Func<IDataServiceDIntel, T> work, CancellationToken ct = default)
        {
            ThrowIfDisposed();
            return _writer.EnqueueTransactionAsync(work, ct);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DrintProcessorContext), "The processor context or its parent project scope has been closed.");
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _reader.Dispose();
            GC.SuppressFinalize(this);
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _reader.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

