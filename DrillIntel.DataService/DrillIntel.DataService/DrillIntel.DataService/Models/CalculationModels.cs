using System;

namespace DrillIntel.Data
{
    /// <summary>
    /// Represents progress information reported by asynchronous calculations and imports.
    /// </summary>
    public readonly record struct CalculationProgress(
        double Percentage,
        string StatusMessage,
        int CurrentStep = 0,
        int TotalSteps = 0
    );

    /// <summary>
    /// Represents an inclusive time range for time-based log analysis.
    /// </summary>
    public readonly record struct TimeRange(DateTime StartTime, DateTime EndTime);

    /// <summary>
    /// Represents an inclusive depth range for depth-based log analysis.
    /// </summary>
    public readonly record struct DepthRange(double StartDepth, double EndDepth);

    /// <summary>
    /// Result payload returned from an independent calculation processor.
    /// </summary>
    public readonly record struct CalculationResult(
        bool Success,
        string OperationName,
        int ProcessedCount,
        TimeSpan Duration,
        string? ErrorMessage = null
    );

    /// <summary>
    /// Result payload returned from chunked log imports.
    /// </summary>
    public readonly record struct ImportResult(
        bool Success,
        string LogName,
        string TableName,
        int TotalRowsImported,
        int TotalChunksCommitted,
        TimeSpan Duration,
        string? ErrorMessage = null
    );

    /// <summary>
    /// Exception thrown when a calculation requires prerequisites that have not been computed.
    /// Provides clear user-facing feedback for WPF UI error dialogues.
    /// </summary>
    public class PrerequisiteMissingException : InvalidOperationException
    {
        public string PrerequisiteName { get; }

        public PrerequisiteMissingException(string prerequisiteName, string message) : base(message)
        {
            PrerequisiteName = prerequisiteName;
        }
    }
}

