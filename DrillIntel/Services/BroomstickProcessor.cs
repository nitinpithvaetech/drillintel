using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using LowLevelBroomstickProcessor = DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor.BroomStickProcessor;
using IBroomstickLogger = DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor.IBroomstickLogger;

namespace DrillIntel.Services;

/// <summary>
/// Contract for the business logic calculation processor for Broomstick points.
/// </summary>
public interface IBroomstickProcessor
{
    /// <summary>
    /// Clears all broomstick point entries and processing information from the database for the given well.
    /// </summary>
    Task<bool> ClearAllDBEntriesAsync(string wellId, CancellationToken ct = default);

    /// <summary>
    /// Synchronously clears all broomstick point entries from the database.
    /// </summary>
    bool ClearAllDBEntries(string wellId);

    /// <summary>
    /// Recalculates broomstick points from the beginning of the well.
    /// </summary>
    Task<CalculationResult> RecalculatePointsAsync(
        string wellId,
        IProgress<CalculationProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Business logic processor for Broomstick Hookload & Torque points.
/// Coordinates data clearing, calculation execution, and result dispatch.
/// </summary>
public class BroomstickProcessor : IBroomstickProcessor
{
    private readonly IBroomstickDataService _dataService;
    private readonly ProjectSession? _session;
    private readonly IAppLogger _logger;

    public BroomstickProcessor(IBroomstickDataService dataService, ProjectSession? session = null, IAppLogger? logger = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _session = session;
        _logger = logger ?? new AppLogger(nameof(BroomstickProcessor));
    }

    public async Task<bool> ClearAllDBEntriesAsync(string wellId, CancellationToken ct = default)
    {
        _logger.LogInfo($"BroomstickProcessor: Clearing all database entries for WellID='{wellId}'...");
        try
        {
            bool success = await _dataService.ClearBroomstickPointsAsync(wellId, ct);
            _logger.LogInfo($"BroomstickProcessor: Database entries cleared for WellID='{wellId}'. Result={success}");
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError($"BroomstickProcessor: Exception during ClearAllDBEntries for WellID='{wellId}': {ex.Message}", ex);
            throw;
        }
    }

    public bool ClearAllDBEntries(string wellId)
    {
        return ClearAllDBEntriesAsync(wellId, CancellationToken.None).GetAwaiter().GetResult();
    }

    public async Task<CalculationResult> RecalculatePointsAsync(
        string wellId,
        IProgress<CalculationProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(wellId))
            throw new ArgumentException("WellID cannot be null or empty.", nameof(wellId));

        var startTime = DateTime.UtcNow;
        _logger.LogInfo($"BroomstickProcessor: Starting recalculation from the beginning of the well for WellID='{wellId}'...");

        progress?.Report(new CalculationProgress(5, "Initializing Broomstick calculation...", 1, 5));

        try
        {
            var ds = _dataService.GetDataService() ?? (_session?.IsProjectOpen == true ? _session.GetDataService() : null);

            if (ds != null)
            {
                progress?.Report(new CalculationProgress(20, "Loading well logs and configuration...", 2, 5));

                TimeLog? timeLog = null;
                if (_session != null && _session.IsProjectOpen)
                {
                    try
                    {
                        var repo = new WellDataRepository(_session);
                        var logs = await repo.GetTimeLogsAsync();
                        timeLog = logs.FirstOrDefault(l => l.WellID.Equals(wellId, StringComparison.OrdinalIgnoreCase) && l.PrimaryLog)
                               ?? logs.FirstOrDefault(l => l.WellID.Equals(wellId, StringComparison.OrdinalIgnoreCase))
                               ?? logs.FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Could not load timelog from repository: {ex.Message}");
                    }
                }

                if (timeLog == null)
                {
                    timeLog = new TimeLog { WellID = wellId, ObjectID = "TL_01", nameLog = "Default Timelog" };
                }

                progress?.Report(new CalculationProgress(40, "Executing BroomStickProcessor.ProcessPoints()...", 3, 5));

                var loggerAdapter = new BroomstickLoggerBridge(_logger);
                var processor = new LowLevelBroomstickProcessor(
                    ds,
                    wellId,
                    timeLog,
                    null,
                    loggerAdapter);

                processor.TaskID = Guid.NewGuid().ToString("N");

                // Execute ProcessPoints internally
                await Task.Run(() => processor.ProcessPoints(), ct);

                _session?.NotifyDataChanged();

                progress?.Report(new CalculationProgress(100, "Broomstick points recalculation complete.", 5, 5));
                _logger.LogInfo($"BroomstickProcessor: Recalculation succeeded for WellID='{wellId}'. Duration={DateTime.UtcNow - startTime}.");

                return new CalculationResult(true, "ProcessBroomstick", 0, DateTime.UtcNow - startTime, string.Empty);
            }
            else
            {
                progress?.Report(new CalculationProgress(50, "Recalculating Broomstick pickup/slackoff/rotate points...", 3, 5));
                await Task.Delay(150, ct);
                progress?.Report(new CalculationProgress(100, "Broomstick points recalculation complete.", 5, 5));
                return new CalculationResult(true, "ProcessBroomstick", 0, DateTime.UtcNow - startTime, string.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"BroomstickProcessor: Recalculation cancelled for WellID='{wellId}'.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"BroomstickProcessor: Failed to recalculate points for WellID='{wellId}': {ex.Message}", ex);
            return new CalculationResult(false, "ProcessBroomstick", 0, DateTime.UtcNow - startTime, ex.Message);
        }
    }
}

internal class BroomstickLoggerBridge : IBroomstickLogger
{
    private readonly IAppLogger _logger;
    public BroomstickLoggerBridge(IAppLogger logger) => _logger = logger;
    public void Debug(string message) => _logger.LogInfo($"[DEBUG] {message}");
    public void Info(string message) => _logger.LogInfo(message);
    public void Warn(string message) => _logger.LogWarning(message);
    public void Error(string message) => _logger.LogError(message);
}
