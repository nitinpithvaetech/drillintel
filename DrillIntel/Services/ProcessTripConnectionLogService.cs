using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.TripConnectionProcessor;
using DrillIntel.Projects;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Services
{
    /// <summary>Options for running trip connection detection against a project's SQLite database.</summary>
    public class ProcessTripConnectionLogOptions
    {
        public string WellID { get; set; } = string.Empty;
        public string LogID { get; set; } = string.Empty;
        public DateTime? CustomFromDate { get; set; }
        public DateTime? CustomToDate { get; set; }
        public double MinimumOffBottomDepth { get; set; } = 100d;
        public DateTime DayTimeHoursFrom { get; set; } = new(2000, 1, 1, 6, 0, 0);
        public DateTime DayTimeHoursTo { get; set; } = new(2000, 1, 1, 18, 0, 0);
    }

    public interface IProcessTripConnectionLogService
    {
        Task<CalculationResult> ProcessTripConnectionsAsync(
            ProcessTripConnectionLogOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Resolves project data and runs TripConnectionLogProcessor using the open SQLite
    /// DataService owned by ProjectSession. Detected points are returned as a count; the
    /// source VB processor did not persist trip connection points.
    /// </summary>
    public class ProcessTripConnectionLogService : IProcessTripConnectionLogService
    {
        private readonly ProjectSession _session;
        private readonly IAppLogger _logger;

        public ProcessTripConnectionLogService(ProjectSession session, IAppLogger? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _logger = logger ?? new AppLogger(nameof(ProcessTripConnectionLogService));
        }

        public async Task<CalculationResult> ProcessTripConnectionsAsync(
            ProcessTripConnectionLogOptions options,
            IProgress<CalculationProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            DateTime startedAt = DateTime.UtcNow;
            progress?.Report(new CalculationProgress(5, "Initializing trip connection detection...", 1, 4));
            try
            {
                ct.ThrowIfCancellationRequested();
                var dataService = _session.GetDataService();
                if (dataService == null || !dataService.IsConnectionOpen())
                {
                    return Failure("Project database service is unavailable.");
                }

                var repository = new WellDataRepository(_session);
                Well? well = await repository.GetProjectWellAsync();
                string wellId = !string.IsNullOrWhiteSpace(options.WellID)
                    ? options.WellID
                    : well?.ObjectID ?? string.Empty;
                well ??= new Well { ObjectID = wellId };

                var timeLogs = await repository.GetTimeLogsAsync();
                TimeLog? timeLog = !string.IsNullOrWhiteSpace(options.LogID)
                    ? timeLogs.FirstOrDefault(log => log.ObjectID == options.LogID)
                    : null;
                timeLog ??= timeLogs.FirstOrDefault(log => log.PrimaryLog) ?? timeLogs.FirstOrDefault();
                if (timeLog == null)
                {
                    return Failure("No primary timelog found for the well.");
                }

                progress?.Report(new CalculationProgress(20, "Determining processing date range...", 2, 4));
                DateTime fromDate;
                DateTime toDate;
                if (options.CustomFromDate.HasValue && options.CustomToDate.HasValue)
                {
                    fromDate = options.CustomFromDate.Value;
                    toDate = options.CustomToDate.Value;
                }
                else
                {
                    double firstIndex = timeLog.getFirstIndexOptimized(dataService);
                    double lastIndex = timeLog.getLastIndexOptimized(dataService);
                    fromDate = firstIndex > 0 ? DateTime.FromOADate(firstIndex) : DateTime.MinValue;
                    toDate = lastIndex > 0 ? DateTime.FromOADate(lastIndex) : DateTime.MaxValue;
                }

                if (fromDate > toDate)
                {
                    return Failure("The processing start date must be earlier than or equal to the end date.");
                }

                SystemSettings settings = LoadCommonSettings(dataService);
                DateTime dayFrom = options.DayTimeHoursFrom;
                DateTime dayTo = options.DayTimeHoursTo;
                if (!TryReadClock(settings.getSetting(SystemSettings.DayTimeFrom), out dayFrom))
                    dayFrom = options.DayTimeHoursFrom;
                if (!TryReadClock(settings.getSetting(SystemSettings.DayTimeTo), out dayTo))
                    dayTo = options.DayTimeHoursTo;

                progress?.Report(new CalculationProgress(30, "Scanning timelog data...", 3, 4));
                ct.ThrowIfCancellationRequested();

                var processor = new TripConnectionLogProcessor
                {
                    objLocalConn = dataService,
                    objRigState = rigState.loadWellRigStateSetup(dataService, wellId) ?? new rigState(),
                    objWell = well,
                    MIN_OFFBOTTOM_DEPTH = options.MinimumOffBottomDepth,
                    DayTimeHoursFrom = dayFrom,
                    DayTimeHoursTo = dayTo
                };

                await Task.Run(() => processor.ProcessPoints(timeLog, fromDate, toDate), ct);
                ct.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(processor.LastError))
                {
                    return Failure(processor.LastError);
                }

                progress?.Report(new CalculationProgress(100, "Trip connection detection complete.", 4, 4));
                _logger.LogInfo($"TripConnections: Detected {processor.connectionPoints.Count} points for WellID='{wellId}'.");
                return new CalculationResult(true, "ProcessTripConnectionLog", processor.connectionPoints.Count,
                    DateTime.UtcNow - startedAt, string.Empty);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"TripConnections: Processing cancelled for WellID='{options.WellID}'.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"TripConnections: Failed for WellID='{options.WellID}': {ex.Message}", ex);
                return Failure(ex.Message);
            }

            CalculationResult Failure(string message) => new(false, "ProcessTripConnectionLog", 0,
                DateTime.UtcNow - startedAt, message);
        }

        private SystemSettings LoadCommonSettings(DrillIntel.Data.IDataServiceDIntel projectDataService)
        {
            var settings = new SystemSettings();
            try
            {
                var baseDataService = BaseDatabaseProvider.GetBaseDataService();
                if (baseDataService != null && baseDataService.IsConnectionOpen())
                    settings.LoadSettings(baseDataService);
                else
                    settings.LoadSettings(projectDataService);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"TripConnections: Could not load day/night settings: {ex.Message}");
            }
            return settings;
        }

        private static bool TryReadClock(string? value, out DateTime clock)
        {
            if (DateTime.TryParse(value, out DateTime parsed))
            {
                clock = new DateTime(2000, 1, 1, parsed.Hour, parsed.Minute, parsed.Second);
                return true;
            }

            clock = default;
            return false;
        }
    }
}
