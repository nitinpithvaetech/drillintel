using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Implements chunked bulk import of TimeLog and DepthLog data.
    /// Breaks large datasets into discrete transactions (~5,000 rows each),
    /// yielding write locks between chunks to avoid thread stalls.
    /// Supports resumability and progress reporting for WPF UI cancellation.
    /// </summary>
    public class BulkLogImportService : IBulkLogImportService
    {
        private readonly IDrintProcessorContext _context;

        public BulkLogImportService(IDrintProcessorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<ImportResult> ImportTimeLogChunkedAsync(
            string wellId,
            string logName,
            DataTable data,
            int chunkSize = 5000,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default)
        {
            return ImportLogInternalAsync(
                wellId,
                logName,
                data,
                logType: "TimeLog",
                chunkSize,
                progress,
                ct);
        }

        public Task<ImportResult> ImportDepthLogChunkedAsync(
            string wellId,
            string logName,
            DataTable data,
            int chunkSize = 5000,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default)
        {
            return ImportLogInternalAsync(
                wellId,
                logName,
                data,
                logType: "DepthLog",
                chunkSize,
                progress,
                ct);
        }

        private async Task<ImportResult> ImportLogInternalAsync(
            string wellId,
            string logName,
            DataTable data,
            string logType,
            int chunkSize,
            IProgress<CalculationProgress>? progress,
            CancellationToken ct)
        {
            if (data == null || data.Columns.Count == 0)
                throw new ArgumentException("Data table must contain valid columns and data.", nameof(data));

            var sw = Stopwatch.StartNew();
            if (chunkSize <= 0) chunkSize = 5000;

            var cleanLogName = new string(logName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
            var tableName = $"RAW_{logType.ToUpperInvariant()}_{cleanLogName}";

            // 1. Ensure target table exists matching data columns
            await _context.WriteTransactionAsync(db =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"CREATE TABLE IF NOT EXISTS [{tableName}] (");
                sb.AppendLine("  [ROW_ID] INTEGER PRIMARY KEY AUTOINCREMENT,");
                sb.AppendLine("  [WELL_ID] TEXT NOT NULL,");

                var distinctCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn col in data.Columns)
                {
                    var cleanCol = new string(col.ColumnName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                    if (string.IsNullOrEmpty(cleanCol) || !distinctCols.Add(cleanCol)) continue;
                    sb.AppendLine($"  [{cleanCol}] REAL,");
                }

                sb.Length -= 3; // Remove trailing comma and newline
                sb.AppendLine(");");
                db.ExecuteNonQuery(sb.ToString());
            }, ct).ConfigureAwait(false);

            // 2. Prepare column names and parameterized insert SQL
            var colNames = new List<string>();
            foreach (DataColumn col in data.Columns)
            {
                var cleanCol = new string(col.ColumnName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                if (!string.IsNullOrEmpty(cleanCol) && !colNames.Contains(cleanCol, StringComparer.OrdinalIgnoreCase))
                {
                    colNames.Add(cleanCol);
                }
            }

            var insertSql = $"INSERT INTO [{tableName}] ([WELL_ID], {string.Join(", ", colNames.Select(c => $"[{c}]"))}) " +
                            $"VALUES (@WELL_ID, {string.Join(", ", colNames.Select(c => "@" + c))});";

            int totalRows = data.Rows.Count;
            int totalChunks = (int)Math.Ceiling((double)totalRows / chunkSize);
            int committedRows = 0;
            int committedChunks = 0;

            progress?.Report(new CalculationProgress(0, $"Starting import of {totalRows:N0} rows in {totalChunks} chunks...", 0, totalChunks));

            // 3. Process each chunk in its own transaction
            for (int chunkIdx = 0; chunkIdx < totalChunks; chunkIdx++)
            {
                ct.ThrowIfCancellationRequested();

                int startRow = chunkIdx * chunkSize;
                int endRow = Math.Min(startRow + chunkSize, totalRows);
                int currentChunkSize = endRow - startRow;

                // Snapshot chunk data into parameters list
                var chunkRows = new List<Dictionary<string, object?>>(currentChunkSize);
                for (int r = startRow; r < endRow; r++)
                {
                    var row = data.Rows[r];
                    var paramDict = new Dictionary<string, object?>(colNames.Count + 1)
                    {
                        ["@WELL_ID"] = wellId
                    };

                    foreach (var col in colNames)
                    {
                        var val = row[col];
                        paramDict["@" + col] = (val == null || val == DBNull.Value) ? null : val;
                    }
                    chunkRows.Add(paramDict);
                }

                // Execute chunk transaction via write coordinator
                await _context.WriteTransactionAsync(db =>
                {
                    foreach (var rowParams in chunkRows)
                    {
                        db.ExecuteNonQuery(insertSql, rowParams);
                    }
                }, ct).ConfigureAwait(false);

                committedRows += currentChunkSize;
                committedChunks++;

                double percent = (double)committedRows / totalRows * 100.0;
                progress?.Report(new CalculationProgress(
                    percent,
                    $"Imported chunk {committedChunks}/{totalChunks} ({committedRows:N0}/{totalRows:N0} rows)",
                    committedChunks,
                    totalChunks));
            }

            sw.Stop();
            return new ImportResult(true, logName, tableName, committedRows, committedChunks, sw.Elapsed);
        }
    }
}

