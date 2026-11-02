using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Services
{
    public class ProcessDrillingStandsOptions
    {
        public string WellID { get; set; } = "";
        public string LogID { get; set; } = "";
        public string DayTimeHoursFrom { get; set; } = "12:00";
        public bool OverwriteExisting { get; set; } = false;
        public DateTime? CustomFromDate { get; set; }
        public DateTime? CustomToDate { get; set; }
    }

    public interface IProcessDrillingStandsService
    {
        Task<CalculationResult> ProcessStandsAsync(
            ProcessDrillingStandsOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<bool> ClearStandsAsync(string wellId, CancellationToken ct = default);

        int GetExistingStandsCount(string wellId);

        SystemSettings LoadCommonSettings();
    }

    public class ProcessDrillingStandsService : IProcessDrillingStandsService
    {
        private readonly ProjectSession _session;
        private readonly IAppLogger _logger;

        public ProcessDrillingStandsService(ProjectSession session, IAppLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger ?? new AppLogger(nameof(ProcessDrillingStandsService));
        }

        public SystemSettings LoadCommonSettings()
        {
            var systemSettings = new SystemSettings();
            try
            {
                var baseDb = BaseDatabaseProvider.GetBaseDataService();
                if (baseDb != null && baseDb.IsConnectionOpen())
                {
                    systemSettings.LoadSettings(baseDb);
                    _logger.LogInfo("DrillingStands: Loaded common settings from Base Database (DrillIntelApp.sqlite - VMX_SETTINGS).");
                    return systemSettings;
                }

                var projectDb = _session.GetDataService();
                if (projectDb != null)
                {
                    systemSettings.LoadSettings(projectDb);
                    _logger.LogInfo("DrillingStands: Loaded common settings from Project Database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"DrillingStands: Could not load common settings: {ex.Message}");
            }
            return systemSettings;
        }

        public async Task<bool> ClearStandsAsync(string wellId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(wellId))
                throw new ArgumentException("WellID cannot be empty.", nameof(wellId));

            _logger.LogInfo($"DrillingStands: Clearing all database entries for WellID='{wellId}'...");
            return await Task.Run(() =>
            {
                var ds = _session.GetDataService();
                if (ds == null) return false;

                string lastError = "";
                bool success = KPIDrlgStand.clearStandsFromDB(ds, wellId, ref lastError);
                if (!success && !string.IsNullOrEmpty(lastError))
                {
                    _logger.LogError($"DrillingStands: Failed to clear stands: {lastError}");
                }
                return success;
            }, ct);
        }

        public int GetExistingStandsCount(string wellId)
        {
            if (string.IsNullOrWhiteSpace(wellId)) return 0;
            try
            {
                var ds = _session.GetDataService();
                if (ds == null) return 0;
                return KPIDrlgStand.GetStandsCount(ds, wellId);
            }
            catch
            {
                return 0;
            }
        }

        public async Task<CalculationResult> ProcessStandsAsync(
            ProcessDrillingStandsOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var startTime = DateTime.UtcNow;
            _logger.LogInfo($"DrillingStands: Starting stand calculation for WellID='{options.WellID}'...");

            progress?.Report(new CalculationProgress(5, "Initializing Drilling Stand processing...", 1, 5));

            return await Task.Run(async () =>
            {
                try
                {
                    var ds = _session.GetDataService();
                    if (ds == null)
                    {
                        return new CalculationResult(false, "ProcessDrillingStands", 0, DateTime.UtcNow - startTime, "Project database service is null.");
                    }

                    // Ensure tables exist
                    DrillingStandProcessor.EnsureTables(ds);

                    // Load common settings
                    var commonSettings = LoadCommonSettings();

                    // Resolve Well & TimeLog
                    var repo = new WellDataRepository(_session);
                    var well = await repo.GetProjectWellAsync();
                    string wellId = !string.IsNullOrWhiteSpace(options.WellID) ? options.WellID : well?.ObjectID ?? "WELL_1";

                    var timeLogs = await repo.GetTimeLogsAsync();
                    TimeLog? primaryTimeLog = null;
                    if (!string.IsNullOrWhiteSpace(options.LogID))
                    {
                        primaryTimeLog = timeLogs.FirstOrDefault(t => t.ObjectID == options.LogID);
                    }
                    if (primaryTimeLog == null)
                    {
                        primaryTimeLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
                    }

                    if (primaryTimeLog == null)
                    {
                        return new CalculationResult(false, "ProcessDrillingStands", 0, DateTime.UtcNow - startTime, "No primary timelog found for the well.");
                    }

                    progress?.Report(new CalculationProgress(15, "Determining processing date range...", 2, 5));

                    DateTime fromDate;
                    DateTime toDate;

                    if (options.CustomFromDate.HasValue && options.CustomToDate.HasValue)
                    {
                        fromDate = options.CustomFromDate.Value;
                        toDate = options.CustomToDate.Value;
                    }
                    else
                    {
                        double firstIndexOA = primaryTimeLog.getFirstIndexOptimized(ds);
                        double lastIndexOA = primaryTimeLog.getLastIndexOptimized(ds);

                        if (firstIndexOA > 0)
                            fromDate = DateTime.FromOADate(firstIndexOA);
                        else
                            fromDate = DateTime.MinValue;

                        if (lastIndexOA > 0)
                            toDate = DateTime.FromOADate(lastIndexOA);
                        else
                            toDate = DateTime.MaxValue;
                    }

                    _logger.LogInfo($"DrillingStands: Processing stands date range from {fromDate:dd-MMM-yyyy HH:mm:ss} to {toDate:dd-MMM-yyyy HH:mm:ss}");

                    progress?.Report(new CalculationProgress(25, "Processing drilling connections to calculate stands...", 3, 5));

                    var processor = new DrillingStandProcessor
                    {
                        objLocalConn = ds,
                        WellID = wellId,
                        __localTimeLog = primaryTimeLog,
                        objSystemSettings = commonSettings,
                        DayTimeHoursFrom = options.DayTimeHoursFrom,
                        OverwriteExisting = options.OverwriteExisting,
                        logger = new DrillingStandLoggerBridge(_logger)
                    };

                    var progressBridge = new Progress<double>(percent =>
                    {
                        double mappedPercent = 25.0 + (percent * 0.70); // 25% to 95%
                        progress?.Report(new CalculationProgress(mappedPercent, $"Processing stands... ({percent:F0}%)", 4, 5));
                    });

                    // Execute points processing directly (non-task based)
                    processor.ProcessPoints(fromDate, toDate, out _, progressBridge, ct);

                    progress?.Report(new CalculationProgress(100, "Stand calculation complete.", 5, 5));
                    _logger.LogInfo($"DrillingStands: Processed successfully for WellID='{wellId}'. Detected count={processor.connectionPoints.Count}. Duration={DateTime.UtcNow - startTime}.");

                    _session.NotifyDataChanged();

                    return new CalculationResult(
                        true,
                        "ProcessDrillingStands",
                        processor.connectionPoints.Count,
                        DateTime.UtcNow - startTime,
                        string.Empty);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"DrillingStands: Stand calculation cancelled for WellID='{options.WellID}'.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"DrillingStands: Failed to process stands for WellID='{options.WellID}': {ex.Message}", ex);
                    return new CalculationResult(
                        false,
                        "ProcessDrillingStands",
                        0,
                        DateTime.UtcNow - startTime,
                        ex.Message);
                }
            }, ct);
        }
    }

    internal class DrillingStandLoggerBridge : IDrillingStandLogger
    {
        private readonly IAppLogger _logger;
        public DrillingStandLoggerBridge(IAppLogger logger) => _logger = logger;
        public void Debug(string message) => _logger.LogInfo($"[DEBUG] {message}");
        public void Info(string message) => _logger.LogInfo(message);
        public void Warn(string message) => _logger.LogWarning(message);
        public void Error(string message) => _logger.LogError(message);
    }
}

