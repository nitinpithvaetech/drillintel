using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Service for high-volume chunked bulk log data ingestion (DepthLog and TimeLog).
    /// Commits data in configurable chunks (default ~5,000 rows) via EnqueueTransactionAsync,
    /// releasing the write lock between chunks to prevent blocking concurrent readers and processors.
    /// </summary>
    public interface IBulkLogImportService
    {
        Task<ImportResult> ImportTimeLogChunkedAsync(
            string wellId,
            string logName,
            DataTable data,
            int chunkSize = 5000,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<ImportResult> ImportDepthLogChunkedAsync(
            string wellId,
            string logName,
            DataTable data,
            int chunkSize = 5000,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);
    }
}

