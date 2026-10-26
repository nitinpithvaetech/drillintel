using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.Projects;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Services
{
    public class ProcessDrillingConnectionsOptions
    {
        public string WellID { get; set; } = "";
        public string LogID { get; set; } = "";
        public bool SkipConnectionsMoreThan { get; set; } = true;
        public double SkipMinutes { get; set; } = 30.0;
        public string DayTimeHoursFrom { get; set; } = "12:00";
        public bool OverwriteExisting { get; set; } = false;
        public DateTime? CustomFromDate { get; set; }
        public DateTime? CustomToDate { get; set; }
    }

    public interface IProcessDrillingConnectionsService
    {
        Task<CalculationResult> ProcessConnectionsAsync(
            ProcessDrillingConnectionsOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);

        Task<bool> ClearConnectionsAsync(string wellId, CancellationToken ct = default);

        SystemSettings LoadCommonSettings();
    }

    public class ProcessDrillingConnectionsService : IProcessDrillingConnectionsService
    {
        private readonly ProjectSession _session;
        private readonly IAppLogger _logger;

        public ProcessDrillingConnectionsService(ProjectSession session, IAppLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger ?? new AppLogger(nameof(ProcessDrillingConnectionsService));
        }

        /// <summary>
        /// Loads common settings from DrillIntelApp.sqlite table VMX_SETTINGS as required.
        /// Falls back to project database if BaseDatabaseProvider is unavailable.
        /// </summary>
        public SystemSettings LoadCommonSettings()
        {
            var systemSettings = new SystemSettings();
            try
            {
                var baseDb = BaseDatabaseProvider.GetBaseDataService();
                if (baseDb != null && baseDb.IsConnectionOpen())
                {
                    systemSettings.LoadSettings(baseDb);
                    _logger.LogInfo("DrillingConnections: Loaded common settings from Base Database (DrillIntelApp.sqlite - VMX_SETTINGS).");
                    return systemSettings;
                }

                // Fallback to project database
                var projectDb = _session.GetDataService();
                if (projectDb != null)
                {
                    systemSettings.LoadSettings(projectDb);
                    _logger.LogInfo("DrillingConnections: Loaded common settings from Project Database.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"DrillingConnections: Could not load common settings: {ex.Message}");
            }
            return systemSettings;
        }

        public async Task<bool> ClearConnectionsAsync(string wellId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(wellId))
                throw new ArgumentException("WellID cannot be empty.", nameof(wellId));

            _logger.LogInfo($"DrillingConnections: Clearing all database entries for WellID='{wellId}'...");
            return await Task.Run(() =>
            {
                var ds = _session.GetDataService();
                if (ds == null) return false;

                string lastError = "";
                bool success = KPIDrillingConnection.clearConnectionsFromDB(ds, wellId, ref lastError);
                if (!success && !string.IsNullOrEmpty(lastError))
                {
                    _logger.LogError($"DrillingConnections: Failed to clear connections: {lastError}");
                }
                else
                {
                    _logger.LogInfo($"DrillingConnections: All connection entries cleared for WellID='{wellId}'.");
                }
                return success;
            }, ct);
        }

        public async Task<CalculationResult> ProcessConnectionsAsync(
            ProcessDrillingConnectionsOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            var startTime = DateTime.UtcNow;
            _logger.LogInfo($"DrillingConnections: Starting connection calculation for WellID='{options.WellID}'...");

            progress?.Report(new CalculationProgress(5, "Initializing Drilling Connection detection...", 1, 5));

            return await Task.Run(async () =>
            {
                try
                {
                    var ds = _session.GetDataService();
                    if (ds == null)
                    {
                        return new CalculationResult(false, "ProcessDrillingConnections", 0, DateTime.UtcNow - startTime, "Project database service is null.");
                    }

                    // Ensure target tables exist
                    DrillingConnectionsProcessor.EnsureTables(ds);

                    // Load common settings from DrillIntelApp.sqlite
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
                        return new CalculationResult(false, "ProcessDrillingConnections", 0, DateTime.UtcNow - startTime, "No primary timelog found for the well.");
                    }

                    progress?.Report(new CalculationProgress(15, "Determining processing date range...", 2, 5));

                    // Date range resolution
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

                        // If not overwriting, check for last existing connection
                        if (!options.OverwriteExisting)
                        {
                            try
                            {
                                string checkLastSql = $"SELECT TO_DATE FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{wellId.Replace("'", "''")}' ORDER BY TO_DATE DESC LIMIT 1;";
                                DataTable dtLast = ds.GetTable(checkLastSql);
                                if (dtLast != null && dtLast.Rows.Count > 0)
                                {
                                    string val = Convert.ToString(dtLast.Rows[0]["TO_DATE"]) ?? "";
                                    if (DateTime.TryParse(val, out DateTime lastToDate))
                                    {
                                        fromDate = lastToDate;
                                        _logger.LogInfo($"DrillingConnections: Continuing from last saved connection: {fromDate:dd-MMM-yyyy HH:mm:ss}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning($"DrillingConnections: Could not query last connection: {ex.Message}");
                            }
                        }
                    }

                    _logger.LogInfo($"DrillingConnections: Processing date range from {fromDate:dd-MMM-yyyy HH:mm:ss} to {toDate:dd-MMM-yyyy HH:mm:ss}");

                    progress?.Report(new CalculationProgress(25, "Scanning timelog data and detecting connections...", 3, 5));

                    var processor = new DrillingConnectionsProcessor
                    {
                        objLocalConn = ds,
                        WellID = wellId,
                        __localTimeLog = primaryTimeLog,
                        objSystemSettings = commonSettings,
                        DayTimeHoursFrom = options.DayTimeHoursFrom,
                        SkipConnections = options.SkipConnectionsMoreThan,
                        MaxConnectionTimeMinutes = options.SkipMinutes,
                        OverwriteExisting = options.OverwriteExisting,
                        logger = new DrillingConnectionsLoggerBridge(_logger)
                    };

                    var progressBridge = new Progress<double>(percent =>
                    {
                        double mappedPercent = 25.0 + (percent * 0.70); // 25% to 95%
                        progress?.Report(new CalculationProgress(mappedPercent, $"Processing connection points... ({percent:F0}%)", 4, 5));
                    });

                    // Execute points processing directly
                    processor.ProcessPoints(primaryTimeLog, fromDate, toDate, progressBridge, ct);

                    progress?.Report(new CalculationProgress(100, "Connection calculation complete.", 5, 5));
                    _logger.LogInfo($"DrillingConnections: Processed successfully for WellID='{wellId}'. Detected count={processor.connectionPoints.Count}. Duration={DateTime.UtcNow - startTime}.");

                    _session.NotifyDataChanged();

                    return new CalculationResult(
                        true,
                        "ProcessDrillingConnections",
                        processor.connectionPoints.Count,
                        DateTime.UtcNow - startTime,
                        string.Empty);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning($"DrillingConnections: Calculation cancelled for WellID='{options.WellID}'.");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"DrillingConnections: Failed to process connections for WellID='{options.WellID}': {ex.Message}", ex);
                    return new CalculationResult(
                        false,
                        "ProcessDrillingConnections",
                        0,
                        DateTime.UtcNow - startTime,
                        ex.Message);
                }
            }, ct);
        }
    }

    internal class DrillingConnectionsLoggerBridge : IDrillingConnectionsLogger
    {
        private readonly IAppLogger _logger;
        public DrillingConnectionsLoggerBridge(IAppLogger logger) => _logger = logger;
        public void Debug(string message) => _logger.LogInfo($"[DEBUG] {message}");
        public void Info(string message) => _logger.LogInfo(message);
        public void Warn(string message) => _logger.LogWarning(message);
        public void Error(string message) => _logger.LogError(message);
    }
}
