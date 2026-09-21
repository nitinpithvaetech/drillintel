using System;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Contract for calculation processors.
    /// Exposes calculations as completely independent methods callable in ANY order from the WPF UI.
    /// Performs runtime prerequisite checks to fail fast if prior dependencies are missing.
    /// </summary>
    public interface IDrintCalculationService
    {
        // Prerequisite checks
        Task<bool> HasTimeLogDataAsync(string wellId, TimeRange? range = null, CancellationToken ct = default);
        Task<bool> HasDepthLogDataAsync(string wellId, DepthRange? range = null, CancellationToken ct = default);
        Task<bool> HasRigStateDataAsync(string wellId, TimeRange? range = null, CancellationToken ct = default);
        Task<bool> HasBroomstickPlanAsync(string wellId, CancellationToken ct = default);

        // Independent Calculations (No fixed pipeline/DAG — callable on demand)
        Task<CalculationResult> CalculateRigStateAsync(
            string wellId,
            TimeRange? range = null,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<CalculationResult> CalculateTripConnectionsAsync(
            string wellId,
            TimeRange? range = null,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<CalculationResult> CalculateDrillingConnectionsAsync(
            string wellId,
            TimeRange? range = null,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<CalculationResult> ProcessBroomstickAsync(
            string wellId,
            DepthRange? range = null,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<CalculationResult> LoadBroomstickPlanAsync(
            string wellId,
            string planFilePath,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);
    }
}

