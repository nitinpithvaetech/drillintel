using System;

namespace DrillIntel.Data
{
    /// <summary>
    /// SQLite WAL checkpoint modes corresponding to PRAGMA wal_checkpoint(...).
    /// </summary>
    public enum WalCheckpointMode
    {
        /// <summary>
        /// Checkpoints as many frames as possible without blocking any reader or writer.
        /// Ideal for background periodic checkpoints during active calculation runs.
        /// </summary>
        Passive,

        /// <summary>
        /// Waits for readers to release locks, then checkpoints all frames back to the database.
        /// </summary>
        Full,

        /// <summary>
        /// Like Full, but also waits until the log file can be restarted from the beginning.
        /// </summary>
        Restart,

        /// <summary>
        /// Like Restart, but additionally truncates the WAL file to zero bytes.
        /// Used on project shutdown to leave no bloated -wal files on disk.
        /// </summary>
        Truncate
    }

    /// <summary>
    /// Encapsulates the diagnostic result of a SQLite WAL checkpoint operation.
    /// </summary>
    public readonly record struct WalCheckpointResult(
        bool Success,
        int BusyCode,
        int LogFramesCount,
        int CheckpointedFramesCount,
        string? ErrorMessage = null
    )
    {
        public bool WasBlocked => BusyCode != 0;
    }

    /// <summary>
    /// Event arguments passed when an error occurs during an enqueued write execution.
    /// </summary>
    public sealed class WriteErrorEventArgs : EventArgs
    {
        public Exception Exception { get; }
        public string? Sql { get; }
        public DateTime TimestampUtc { get; } = DateTime.UtcNow;

        public WriteErrorEventArgs(Exception exception, string? sql = null)
        {
            Exception = exception;
            Sql = sql;
        }
    }
}

