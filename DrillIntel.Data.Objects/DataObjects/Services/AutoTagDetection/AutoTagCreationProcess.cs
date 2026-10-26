using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.AutoTagDetection;
using DrillIntel.Data.Objects.DataObjects.Models.Util;
using DrillIntel.Data.Objects.DataObjects.Services;
using VirtualTable;

namespace DrillIntel.Data.Objects.DataObjects.Services.AutoTagDetection
{
    #region Supporting Interfaces & Helper Types

    /// <summary>
    /// Logging abstraction providing Debug, Info, Warn, and Error logging
    /// compatible with DrillIntel and legacy eVuMaxPro callers.
    /// </summary>
    public interface ILog
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(string message, Exception? ex);
    }

    /// <summary>
    /// File- and console-backed logger replacing legacy eVuMaxPro DynamicLogger.
    /// Safely writes process logs to ProgramData/DrillIntel/logs/AutoTagDetection.
    /// </summary>
    public class DynamicLogger : ILog
    {
        private readonly string _name;
        private readonly string? _filePath;
        private readonly object _lock = new();

        public DynamicLogger(string name, string? filePath = null)
        {
            _name = name;
            _filePath = filePath;
        }

        // --- [CONVERTED FOR DRILLINTEL: Factory methods supporting legacy spelling 'GetDyamicLogger' and standard 'GetDynamicLogger'] ---
        public static ILog GetDyamicLogger(string name, string filePath) => new DynamicLogger(name, filePath);
        public static ILog GetDynamicLogger(string name, string filePath) => new DynamicLogger(name, filePath);

        public void Debug(string message) => Write("DEBUG", message);
        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message) => Write("ERROR", message);
        public void Error(string message, Exception? ex) =>
            Write("ERROR", ex != null ? $"{message}: {ex.Message}{Environment.NewLine}{ex.StackTrace}" : message);

        private void Write(string level, string message)
        {
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{_name}] {message}";
            System.Diagnostics.Debug.WriteLine(logLine);

            if (!string.IsNullOrWhiteSpace(_filePath))
            {
                try
                {
                    lock (_lock)
                    {
                        string? dir = Path.GetDirectoryName(_filePath);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        File.AppendAllText(_filePath, logLine + Environment.NewLine);
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Represents a phase tag definition used for tag categorization and casing run detection.
    /// </summary>
    public class clsPhaseTagEx
    {
        public string PhaseID { get; set; } = string.Empty;
        public string StepID { get; set; } = string.Empty;
        public string EmphID { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Helper service providing phase mapping resolution for specific operation categories (e.g. CASING).
    /// </summary>
    public static class PhaseMappingEx
    {
        public static Dictionary<int, clsPhaseTagEx> getList(IDataServiceDIntel? objDataService, string category)
        {
            var result = new Dictionary<int, clsPhaseTagEx>();
            if (objDataService == null) return result;

            try
            {
                string tableName = "";
                if (objDataService.TableExists("eVuMaxPhaseMapping"))
                    tableName = "eVuMaxPhaseMapping";
                else if (objDataService.TableExists("VMX_PHASE_MAPPING"))
                    tableName = "VMX_PHASE_MAPPING";

                if (!string.IsNullOrEmpty(tableName))
                {
                    DataTable dt = objDataService.GetTable($"SELECT * FROM [{tableName}] WHERE UPPER(Category) = '{category.ToUpperInvariant()}'");
                    if (dt != null)
                    {
                        int index = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            result[index++] = new clsPhaseTagEx
                            {
                                PhaseID = DataService.checkNull(row["PhaseId"], ""),
                                StepID = DataService.checkNull(row["StepId"], ""),
                                EmphID = DataService.checkNull(row["EmphId"], ""),
                                Category = category
                            };
                        }
                    }
                }
            }
            catch { }

            return result;
        }
    }

    /// <summary>
    /// Legacy Well extension helper providing primary time log resolution.
    /// </summary>
    public static class WellEx
    {
        public static TimeLog? getPrimaryTimeLogWOPlan(ref IDataServiceDIntel objDataService, string wellID)
        {
            return AutoTagCreationProcess.GetPrimaryTimeLog(objDataService, wellID);
        }

        public static TimeLog? getPrimaryTimeLogWOPlan(IDataServiceDIntel objDataService, string wellID)
        {
            return AutoTagCreationProcess.GetPrimaryTimeLog(objDataService, wellID);
        }
    }

    #endregion

    /// <summary>
    /// Automated Tag and Phase Detection Process for DrillIntel.
    /// Analyzes drilling time-log telemetry (Depth, Hole Depth, Rig State, Duration)
    /// to automatically detect and tag operational phases (Rig Up, Drilling, Hole Cleaning/CUC,
    /// Trip Out, Trip In, and Casing runs) and stores the resulting phases into VMX_PHASE_LIST.
    /// Converted from legacy eVuMaxPro AutoTagCreationProcess for SQLite and DrillIntel project format.
    /// </summary>
    public class AutoTagCreationProcess
    {
        #region Public Members & Fields

        public ILog? logger;
        private Thread? objThread;
        private volatile bool _stopRequested = false;

        // --- [CONVERTED FOR DRILLINTEL: Uses IDataServiceDIntel for SQLite abstraction] ---
        public IDataServiceDIntel? objDataService;
        public string WellID = "";
        public string LastError = "";
        public bool ProcessCompleted { get { return __processCompleted; } }
        private bool __processCompleted = false;

        // Token kept for backward compatibility with legacy eVuMax slot manager callers
        public Guid __token = Guid.Empty;

        /// <summary>
        /// Optional explicit TimeLog to process. If null, the primary timelog for WellID is loaded.
        /// </summary>
        public TimeLog? CustomTimeLog { get; set; }

        /// <summary>
        /// Total number of tags detected and recorded into VMX_PHASE_LIST during the run.
        /// </summary>
        public int DetectedTagsCount { get; private set; } = 0;

        /// <summary>
        /// When true, deletes any existing phase tags for this well before detection so that
        /// intervals are cleanly generated without overlapping obsolete tags. Defaults to true.
        /// </summary>
        public bool ClearExistingTags { get; set; } = true;

        /// <summary>
        /// Notifies listeners of status updates and progress percentage (0.0 to 100.0).
        /// </summary>
        public event Action<string, double>? ProgressChanged;

        /// <summary>
        /// Notifies listeners when the process finishes (success flag and error message if any).
        /// </summary>
        public event Action<bool, string>? ProcessFinished;

        public const string RigUp = "RigUp";
        public const string Drilling = "Drilling";
        public const string CUC = "CUC";
        public const string TripOut = "TripOut";
        public const string TripIn = "TripIn";
        public const string Casing = "Casing";

        public const int Activity_RigUp = 10;
        public const int Activity_Drilling = 1;
        public const int Activity_HoleCleaning = 2;
        public const int Activity_TripOut = 3;
        public const int Activity_TripIn = 4;

        #endregion

        #region Process Lifecycle

        /// <summary>
        /// Starts the auto-tag detection process asynchronously on a background worker thread.
        /// </summary>
        public void startProcess()
        {
            try
            {
                _stopRequested = false;

                // --- [CONVERTED FOR DRILLINTEL: Replaced SQL Server AES decryption with SQLite IDataServiceDIntel] ---
                if (objDataService == null || !objDataService.IsConnectionOpen())
                {
                    objDataService = BaseDatabaseProvider.GetBaseDataService();
                }

                if (objDataService == null || !objDataService.IsConnectionOpen())
                {
                    LastError = "Database connection is not open or initialized.";
                    __processCompleted = true;
                    return;
                }

                objThread = new Thread(new ThreadStart(startProcessAsync))
                {
                    IsBackground = true,
                    Name = $"AutoTagProcess_{WellID}"
                };
                objThread.Start();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                __processCompleted = true;
            }
        }

        /// <summary>
        /// Starts the auto-tag detection process with an explicit IDataServiceDIntel instance.
        /// </summary>
        public void startProcess(IDataServiceDIntel dataService)
        {
            objDataService = dataService;
            startProcess();
        }

        /// <summary>
        /// Requests cancellation of the background auto-tag detection process.
        /// Avoids Thread.Abort() which is not supported in .NET 8 / modern runtimes.
        /// </summary>
        public void stopProcess()
        {
            _stopRequested = true;

            try
            {
                logger?.Debug("Stopping auto-tag detection process...");

                if (objThread != null && objThread.IsAlive)
                {
                    // Allow up to 2 seconds for graceful termination
                    objThread.Join(2000);
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Error during stopProcess", ex);
            }
        }

        /// <summary>
        /// Reports progress status and completion percentage to active listeners.
        /// </summary>
        public void ReportProgress(string status, double percent)
        {
            logger?.Debug($"[Progress {percent:F1}%] {status}");
            ProgressChanged?.Invoke(status, Math.Clamp(percent, 0.0, 100.0));
        }

        /// <summary>
        /// Runs the auto-tag creation process asynchronously with progress reporting and cancellation support.
        /// </summary>
        public Task<bool> ExecuteAsync(
            IProgress<double>? progressPercent = null,
            IProgress<string>? progressStatus = null,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();

            Action<string, double> onProgress = (status, pct) =>
            {
                progressStatus?.Report(status);
                progressPercent?.Report(pct);
            };

            Action<bool, string> onFinished = (success, err) =>
            {
                tcs.TrySetResult(success);
            };

            ProgressChanged += onProgress;
            ProcessFinished += onFinished;

            CancellationTokenRegistration reg = default;
            if (cancellationToken.CanBeCanceled)
            {
                reg = cancellationToken.Register(() =>
                {
                    stopProcess();
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            try
            {
                startProcess();
            }
            catch (Exception ex)
            {
                reg.Dispose();
                ProgressChanged -= onProgress;
                ProcessFinished -= onFinished;
                tcs.TrySetException(ex);
                return tcs.Task;
            }

            return tcs.Task.ContinueWith(t =>
            {
                reg.Dispose();
                ProgressChanged -= onProgress;
                ProcessFinished -= onFinished;
                return t.GetAwaiter().GetResult();
            }, TaskScheduler.Default);
        }

        #endregion

        #region Core Detection Engine

        /// <summary>
        /// Main execution routine running on the background thread.
        /// </summary>
        private void startProcessAsync()
        {
            try
            {
                __processCompleted = false;
                DetectedTagsCount = 0;
                ReportProgress("Initializing tag detection process...", 2.0);

                if (objDataService == null)
                {
                    __processCompleted = true;
                    LastError = "Database service is null or unavailable.";
                    ProcessFinished?.Invoke(false, LastError);
                    return;
                }

                // Ensure required SQLite tables exist
                EnsureTablesExist(objDataService);

                // --- [CONVERTED FOR DRILLINTEL: Uses CustomTimeLog if set, otherwise resolves primary TimeLog] ---
                TimeLog? objTimeLog = CustomTimeLog ?? GetPrimaryTimeLog(objDataService, WellID);
                if (objTimeLog != null && string.IsNullOrWhiteSpace(WellID))
                {
                    WellID = objTimeLog.WellID;
                }

                // Determine well name safely
                string wellName = "";
                try
                {
                    var nameVal = objDataService.GetValueFromDatabase($"SELECT WELL_NAME FROM VMX_WELL WHERE WELL_ID='{WellID.Replace("'", "''")}'");
                    wellName = Convert.ToString(nameVal) ?? "";
                }
                catch { }

                if (string.IsNullOrWhiteSpace(wellName) && objTimeLog != null)
                {
                    wellName = !string.IsNullOrWhiteSpace(objTimeLog.nameWell) ? objTimeLog.nameWell : objTimeLog.__WellName;
                }

                // Configure logger pointing to ProgramData/DrillIntel/logs
                string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DrillIntel", "logs", "AutoTagDetection");
                string fileName = Path.Combine(folderPath, $"AutoTagDetectionProcess_{RemoveSpecialChars(wellName, "_")}_{DateTime.Now:yyyy_MMM_dd}.txt");

                if (logger == null)
                {
                    logger = DynamicLogger.GetDyamicLogger("ProcessLog", fileName);
                }

                Dictionary<string, TagMapping> tagMappings = new Dictionary<string, TagMapping>(StringComparer.OrdinalIgnoreCase);

                var objBulkExecutor = new BulkCommandExecutor(objDataService, 100);

                ReportProgress("Starting tag auto detection engine...", 5.0);
                logger.Debug("Starting tag auto detection process ... ");

                if (objTimeLog != null)
                {
                    #region Initialization

                    string ActivityColumnName = "ACTIVITY";
                    string dataTableName = !string.IsNullOrWhiteSpace(objTimeLog.__dataTableName)
                        ? objTimeLog.__dataTableName
                        : TimeLog.getDataTableName(objDataService, objTimeLog.WellID, objTimeLog.WellboreID, objTimeLog.ObjectID);

                    objTimeLog.__dataTableName = dataTableName;

                    // --- [CONVERTED FOR SQLITE: Replaced 'SELECT TOP 1' with SQLite 'LIMIT 1'] ---
                    DataTable sampleRow = objDataService.GetTable($"SELECT * FROM [{dataTableName}] LIMIT 1;");

                    if (sampleRow != null && !sampleRow.Columns.Contains(ActivityColumnName))
                    {
                        logger.Debug("Activity column doesn't exist in the table. Creating it ... ");
                        // --- [CONVERTED FOR SQLITE: Standard SQLite ALTER TABLE ADD COLUMN REAL] ---
                        string strModifySQL = $"ALTER TABLE [{dataTableName}] ADD COLUMN [{ActivityColumnName}] REAL;";
                        if (objDataService.ExecuteNonQuery(strModifySQL))
                        {
                            logger.Debug("Column created successfully ");
                        }
                        else
                        {
                            logger.Debug("Failed to create Activity column in the database. Error returned was " + objDataService.LastError);
                        }
                    }

                    logger.Debug("Reading Activity Mappings ... ");

                    // Load tag mappings from SQLite
                    DataTable objTagMappings = objDataService.GetTable("SELECT * FROM eVuMaxActivityTagMapping;");

                    if (objTagMappings != null && objTagMappings.Rows.Count > 0)
                    {
                        foreach (DataRow objRow in objTagMappings.Rows)
                        {
                            string activityID = DataService.checkNull(objRow["ActivityType"], "").ToString();
                            string phaseId = DataService.checkNull(objRow["PhaseId"], "").ToString();
                            string stepId = DataService.checkNull(objRow["StepId"], "").ToString();
                            string emphId = DataService.checkNull(objRow["EmphId"], "").ToString();

                            TagMapping objMapping = new TagMapping(activityID, phaseId, stepId, emphId);
                            logger.Debug($"Activity: {activityID} - {phaseId} : {stepId} : {emphId}");

                            if (!tagMappings.ContainsKey(objMapping.ActivityType))
                            {
                                tagMappings.Add(objMapping.ActivityType, objMapping);
                            }
                        }
                    }
                    else
                    {
                        // Auto-seed default mappings if table is empty
                        SeedDefaultTagMappings(objDataService, tagMappings);
                    }

                    // Determine process starting date from TimeLog first index
                    DateTime processFromDate = DateTime.MinValue;
                    double firstIndex = objTimeLog.getFirstIndexOptimized(objDataService);
                    if (firstIndex > 0)
                    {
                        try { processFromDate = DateTime.FromOADate(firstIndex); } catch { }
                    }

                    if (processFromDate == DateTime.MinValue)
                    {
                        processFromDate = GetMinDateFromTable(objDataService, dataTableName);
                    }

                    if (ClearExistingTags)
                    {
                        logger.Info($"ClearExistingTags is true. Removing existing tags for well {WellID} before detection.");
                        objDataService.ExecuteNonQuery($"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");
                    }

                    // Retrieve the tags already created for this well
                    DataTable objWellTags = ClearExistingTags
                        ? new DataTable()
                        : objDataService.GetTable($"SELECT PHASE_INDEX,START_DATE,END_DATE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");

                    if (objWellTags != null && objWellTags.Rows.Count > 1)
                    {
                        var sortedExisting = objWellTags.AsEnumerable()
                            .Select(r => new {
                                Index = DataService.checkNull(r["PHASE_INDEX"], 0.0),
                                Start = ParseDate(r["START_DATE"]),
                                End = ParseDate(r["END_DATE"])
                            })
                            .Where(t => t.Start != DateTime.MinValue)
                            .OrderBy(t => t.Start)
                            .ToList();

                        if (sortedExisting.Count > 1)
                        {
                            // Skip last tag as it might have been detected partially during real time data download
                            DateTime tagEndDate = sortedExisting[sortedExisting.Count - 2].End;
                            if (tagEndDate != DateTime.MinValue)
                            {
                                processFromDate = tagEndDate;
                            }
                        }
                    }

                    DateTime activityProcessFromDate = processFromDate;

                    // Query latest activity timestamp using monotonic rowid
                    DataTable lastActivityData = objDataService.GetTable($"SELECT DATETIME FROM [{dataTableName}] WHERE [{ActivityColumnName}] IS NOT NULL ORDER BY rowid DESC LIMIT 1;");

                    if (lastActivityData != null && lastActivityData.Rows.Count > 0)
                    {
                        DateTime __lastActivityDate = DataService.checkNull(lastActivityData.Rows[0]["DATETIME"], DateTime.MinValue);
                        if (__lastActivityDate != DateTime.MinValue)
                        {
                            if (__lastActivityDate < processFromDate)
                            {
                                activityProcessFromDate = __lastActivityDate;
                            }
                        }
                        else
                        {
                            activityProcessFromDate = processFromDate;
                        }
                    }
                    else
                    {
                        activityProcessFromDate = processFromDate;
                    }

                    logger.Debug($"Starting processing from date {activityProcessFromDate:dd-MMM-yyyy HH:mm:ss}");

                    #endregion

                    #region Auto Detection Logic

                    string strSQL = "";
                    DateTime lnDateTime;
                    double lnDepth = 0;
                    double lnHoleDepth = 0;
                    double lnRigState = 0;

                    double standHeight = 120.0; // Stand threshold 120 ft / 36.576 m
                    int drillingTimeLimit = 900; // 15 minutes
                    double DepthThreshold = 300.0; // 300 ft / 91.44 m

                    // Load detection configuration from SQLite settings table
                    DataTable objSettings = objDataService.GetTable("SELECT * FROM eVuMaxTagDetectionSettings LIMIT 1;");

                    if (objSettings != null && objSettings.Rows.Count > 0)
                    {
                        standHeight = DataService.checkNull(objSettings.Rows[0]["StandHeight"], 120.0);
                        drillingTimeLimit = DataService.checkNull(objSettings.Rows[0]["DrillingTimeLimit"], 900);
                        DepthThreshold = DataService.checkNull(objSettings.Rows[0]["DepthThreshold"], 300.0);

                        if (standHeight <= 0) standHeight = 120.0;
                        if (drillingTimeLimit <= 0) drillingTimeLimit = 900;
                        if (DepthThreshold <= 0) DepthThreshold = 300.0;

                        // Check depth unit and convert thresholds accordingly
                        string depthUnitID = "";
                        if (objTimeLog.logCurves.ContainsKey("DEPTH"))
                        {
                            depthUnitID = objTimeLog.logCurves["DEPTH"].VuMaxUnitId;
                            if (string.IsNullOrWhiteSpace(depthUnitID))
                            {
                                depthUnitID = objTimeLog.logCurves["DEPTH"].unit;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(depthUnitID))
                        {
                            depthUnitID = depthUnitID.Trim().ToLowerInvariant();
                            if (depthUnitID == "m" || depthUnitID == "mtr" || depthUnitID == "meter" || depthUnitID == "meters")
                            {
                                DepthThreshold /= 3.33;
                                standHeight /= 3.33;
                            }
                        }
                    }

                    logger.Debug($"Auto Detection Settings: Stand Height: {standHeight.ToString(CultureInfo.InvariantCulture)} ft Drilling Time Limit: {drillingTimeLimit} s Depth Threshold: {DepthThreshold.ToString(CultureInfo.InvariantCulture)} ft");

                    double startDepth = 0;
                    double lastDepth = 0;
                    DateTime lastDateTime = DateTime.MinValue;

                    int startDepthIndex = 0;
                    double MinDepth = 0;
                    int MinDepthIndex = 0;
                    double MaxDepth = 0;
                    int MaxDepthIndex = 0;

                    DateTime startDepthDate = DateTime.MinValue;
                    DateTime MinDepthDate = DateTime.MinValue;
                    DateTime MaxDepthDate = DateTime.MinValue;

                    bool previousDrillingActivityFound = false;
                    bool drillingActivityCheckPerformed = false;
                    DateTime lastTagDate = DateTime.MinValue;

                    logger.Debug("Loading raw data for processing ... ");

                    VirtualTable.VirtualTable objVirtualData = new VirtualTable.VirtualTable();
                    objVirtualData.objDataService = objDataService;
                    objVirtualData.FieldList = "DATETIME,DEPTH,HDTH,RIG_STATE,TIME_DURATION";
                    objVirtualData.KeyField = "DATETIME";
                    objVirtualData.KeyFieldFormatted = "DATETIME";
                    objVirtualData.FrameSize = 1000;
                    objVirtualData.dataTableName = dataTableName;
                    objVirtualData.Condition = $"DATETIME>='{activityProcessFromDate:dd-MMM-yyyy HH:mm:ss}'";
                    objVirtualData.loadData();

                    logger.Debug("Data loaded, starting processing ... ");

                    if (objVirtualData.RecordCount > 0)
                    {
                        startDepth = objVirtualData.getDoubleValue(0, "DEPTH");
                        startDepthDate = objVirtualData.getDateTimeValue(0, "DATETIME");

                        MinDepth = startDepth;
                        MinDepthDate = startDepthDate;
                        MaxDepth = startDepth;
                        MaxDepthDate = startDepthDate;

                        for (int i = 0; i < objVirtualData.RecordCount; i++)
                        {
                            if (_stopRequested) break;

                            lnDateTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                            lnDepth = objVirtualData.getDoubleValue(i, "DEPTH");
                            lnHoleDepth = objVirtualData.getDoubleValue(i, "HDTH");
                            lnRigState = objVirtualData.getDoubleValueWithNull(i, "RIG_STATE", -1d);

                            lastDateTime = lnDateTime;
                            lastDepth = lnDepth;

                            // ###### Detecting Rig Up
                            bool ActivityDetected = false;

                            if (!drillingActivityCheckPerformed)
                            {
                                drillingActivityCheckPerformed = true;
                                // --- [CONVERTED FOR SQLITE: Replaced 'SELECT TOP 1' with 'LIMIT 1'] ---
                                previousDrillingActivityFound = objDataService.IsRecordExist(
                                    $"SELECT DATETIME FROM [{dataTableName}] WHERE DATETIME<'{lnDateTime:dd-MMM-yyyy HH:mm:ss}' AND [{ActivityColumnName}]={Activity_Drilling} LIMIT 1;");
                            }

                            if (previousDrillingActivityFound || lnRigState == 0 || lnRigState == 1 || lnRigState == 19)
                            {
                                // Not rig up; continue detecting other activities
                            }
                            else
                            {
                                // It is rig up. Now find the next drilling and determine if it is continuous for at least 15 min
                                for (int j = i; j < objVirtualData.RecordCount; j++)
                                {
                                    if (_stopRequested) break;
                                    double lnRigState2 = objVirtualData.getDoubleValueWithNull(j, "RIG_STATE", -1d);

                                    if (lnRigState2 == 0 || lnRigState2 == 1 || lnRigState2 == 19)
                                    {
                                        DateTime dtDrillingStartDate = objVirtualData.getDateTimeValue(j, "DATETIME");
                                        bool contDrillingFound = false;

                                        for (int c = j; c < objVirtualData.RecordCount; c++)
                                        {
                                            DateTime dtDateTime3 = objVirtualData.getDateTimeValue(c, "DATETIME");
                                            double lnRigState3 = objVirtualData.getDoubleValueWithNull(c, "RIG_STATE", -1d);

                                            if (lnRigState3 == 0 || lnRigState3 == 1 || lnRigState3 == 19)
                                            {
                                                if (Math.Abs((dtDateTime3 - dtDrillingStartDate).TotalSeconds) >= drillingTimeLimit)
                                                {
                                                    contDrillingFound = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }

                                        if (contDrillingFound)
                                        {
                                            strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_RigUp} " +
                                                     $"WHERE DATETIME>='{lnDateTime:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<'{dtDrillingStartDate:dd-MMM-yyyy HH:mm:ss}';";
                                            objDataService.ExecuteNonQuery(strSQL);

                                            logger.Debug($"Detected Activity : Rig Up : from date {lnDateTime:dd-MMM-yyyy HH:mm:ss} to {dtDrillingStartDate:dd-MMM-yyyy HH:mm:ss}");

                                            i = j;
                                            ActivityDetected = true;
                                            previousDrillingActivityFound = true;
                                            lastTagDate = dtDrillingStartDate;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (ActivityDetected)
                            {
                                continue;
                            }

                            // ####### Detecting Drilling
                            if (lnRigState == 0 || lnRigState == 1 || lnRigState == 19)
                            {
                                // Check if Trip In detection was in progress
                                if (lnDepth > startDepth)
                                {
                                    if ((lnDepth - startDepth) > DepthThreshold)
                                    {
                                        strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_TripIn} " +
                                                 $"WHERE DATETIME>='{startDepthDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{lnDateTime:dd-MMM-yyyy HH:mm:ss}';";
                                        objDataService.ExecuteNonQuery(strSQL);

                                        logger.Debug($"Detected Activity : Trip In : from date {startDepthDate:dd-MMM-yyyy HH:mm:ss} to {lnDateTime:dd-MMM-yyyy HH:mm:ss}");
                                        lastTagDate = lnDateTime;
                                    }
                                }

                                // Resetting tracking anchors
                                startDepth = lnDepth;
                                startDepthDate = lnDateTime;
                                startDepthIndex = i;
                                MaxDepth = lnDepth;
                                MaxDepthDate = lnDateTime;
                                MaxDepthIndex = i;
                                MinDepth = lnDepth;
                                MinDepthDate = lnDateTime;
                                MinDepthIndex = i;
                                bool drillingEndFound = false;

                                for (int j = i; j < objVirtualData.RecordCount; j++)
                                {
                                    if (_stopRequested) break;
                                    DateTime dtDateTime2 = objVirtualData.getDateTimeValue(j, "DATETIME");
                                    double lnRigState2 = objVirtualData.getDoubleValueWithNull(j, "RIG_STATE", -1d);
                                    double lnDepth2 = objVirtualData.getDoubleValue(j, "DEPTH");
                                    double lnHdth2 = objVirtualData.getDoubleValue(j, "HDTH");

                                    if (lnRigState2 == 0 || lnRigState2 == 1 || lnRigState2 == 19 || (lnHdth2 - lnDepth2) <= standHeight)
                                    {
                                        // Still drilling
                                    }
                                    else
                                    {
                                        DateTime dtEndOfDrillingDate = dtDateTime2;
                                        int drillingEndRowIndex = j;

                                        if (lnRigState2 != 0 && lnRigState2 != 1 && lnRigState2 != 19)
                                        {
                                            for (int c = j; c >= i; c--)
                                            {
                                                double lnRigState3 = objVirtualData.getDoubleValueWithNull(c, "RIG_STATE", -1d);
                                                if (lnRigState3 == 0 || lnRigState3 == 1 || lnRigState3 == 19)
                                                {
                                                    dtEndOfDrillingDate = objVirtualData.getDateTimeValue(c, "DATETIME");
                                                    drillingEndRowIndex = c;
                                                    break;
                                                }
                                            }
                                        }

                                        startDepth = objVirtualData.getDoubleValue(drillingEndRowIndex, "DEPTH");
                                        startDepthDate = objVirtualData.getDateTimeValue(drillingEndRowIndex, "DATETIME");
                                        startDepthIndex = j;
                                        MaxDepth = startDepth;
                                        MaxDepthDate = startDepthDate;
                                        MaxDepthIndex = j;
                                        MinDepth = startDepth;
                                        MinDepthDate = startDepthDate;
                                        MinDepthIndex = j;
                                        drillingEndFound = true;

                                        strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_Drilling} " +
                                                 $"WHERE DATETIME>='{lnDateTime:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{dtEndOfDrillingDate:dd-MMM-yyyy HH:mm:ss}';";
                                        objDataService.ExecuteNonQuery(strSQL);

                                        logger.Debug($"Detected Activity : Drilling : from date {lnDateTime:dd-MMM-yyyy HH:mm:ss} to {dtEndOfDrillingDate:dd-MMM-yyyy HH:mm:ss}");

                                        previousDrillingActivityFound = true;
                                        ActivityDetected = true;
                                        lastTagDate = dtEndOfDrillingDate;

                                        i = drillingEndRowIndex + 1;
                                        break;
                                    }
                                }

                                if (!drillingEndFound)
                                {
                                    strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_Drilling} " +
                                             $"WHERE DATETIME>='{lnDateTime:dd-MMM-yyyy HH:mm:ss}';";
                                    objDataService.ExecuteNonQuery(strSQL);

                                    logger.Debug($"Detected Activity : Drilling : from date {lnDateTime:dd-MMM-yyyy HH:mm:ss} to end of the well");

                                    ActivityDetected = true;
                                    lastTagDate = objVirtualData.getDateTimeValue(objVirtualData.RecordCount - 1, "DATETIME");
                                    i = objVirtualData.RecordCount - 1;
                                    break;
                                }
                            }

                            if (ActivityDetected)
                            {
                                continue;
                            }

                            // ####### Detecting Trip Out
                            if (lnDepth < startDepth)
                            {
                                if (lnDepth < MinDepth)
                                {
                                    MinDepth = lnDepth;
                                    MinDepthDate = lnDateTime;
                                    MinDepthIndex = i;
                                }

                                double diff = (startDepth - lnDepth);

                                if (diff > DepthThreshold)
                                {
                                    if (lnDepth > MinDepth)
                                    {
                                        if ((lnDepth - MinDepth) > DepthThreshold)
                                        {
                                            strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_TripOut} " +
                                                     $"WHERE DATETIME>='{startDepthDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{MinDepthDate:dd-MMM-yyyy HH:mm:ss}';";
                                            objDataService.ExecuteNonQuery(strSQL);

                                            logger.Debug($"Detected Activity : Trip Out : from date {startDepthDate:dd-MMM-yyyy HH:mm:ss} to {MinDepthDate:dd-MMM-yyyy HH:mm:ss}");

                                            lastTagDate = MinDepthDate;

                                            // Try to find hole cleaning activity during trip activity
                                            int drillingEndIndex = startDepthIndex;

                                            for (int c = startDepthIndex; c >= 0; c--)
                                            {
                                                double lnRigState2 = objVirtualData.getDoubleValueWithNull(c, "RIG_STATE", -1d);
                                                if (lnRigState2 == 0 || lnRigState2 == 1 || lnRigState2 == 19)
                                                {
                                                    int searchEnd = -1;
                                                    for (int n = c; n >= 0; n--)
                                                    {
                                                        double lnRigState3 = objVirtualData.getDoubleValueWithNull(n, "RIG_STATE", -1d);
                                                        if (lnRigState3 != 0 && lnRigState3 != 1 && lnRigState3 != 19)
                                                        {
                                                            searchEnd = n;
                                                            break;
                                                        }
                                                    }

                                                    if (searchEnd != -1)
                                                    {
                                                        DateTime dtDrlgDate1 = objVirtualData.getDateTimeValue(c, "DATETIME");
                                                        DateTime dtDrlgDate2 = objVirtualData.getDateTimeValue(searchEnd, "DATETIME");

                                                        if (Math.Abs((dtDrlgDate1 - dtDrlgDate2).TotalSeconds) >= drillingTimeLimit)
                                                        {
                                                            drillingEndIndex = c;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            int lastCircIndex = -1;
                                            for (int c = MinDepthIndex; c >= 0; c--)
                                            {
                                                double lnRigState2 = objVirtualData.getDoubleValueWithNull(c, "RIG_STATE", -1d);
                                                if (lnRigState2 == 12 || lnRigState2 == 7 || lnRigState2 == 3 || lnRigState2 == 11)
                                                {
                                                    lastCircIndex = c;
                                                    break;
                                                }
                                            }

                                            if (lastCircIndex != -1 && (drillingEndIndex + 1) <= lastCircIndex)
                                            {
                                                double totalTime = 0;
                                                double ReamingTime = 0;
                                                double CircTime = 0;

                                                DateTime cucStartDate = objVirtualData.getDateTimeValue(drillingEndIndex + 1, "DATETIME");
                                                DateTime cucEndDate = objVirtualData.getDateTimeValue(lastCircIndex, "DATETIME");
                                                double totalCleanupTimeDuration = Math.Abs((cucStartDate - cucEndDate).TotalSeconds) / 60.0;

                                                for (int c = drillingEndIndex + 1; c <= lastCircIndex; c++)
                                                {
                                                    double lnRigState2 = objVirtualData.getDoubleValueWithNull(c, "RIG_STATE", -1d);
                                                    double lnTimeDuration = objVirtualData.getDoubleValue(c, "TIME_DURATION");

                                                    totalTime += lnTimeDuration;

                                                    if (lnRigState2 == 7 || lnRigState2 == 3)
                                                    {
                                                        ReamingTime += lnTimeDuration;
                                                    }

                                                    if (lnRigState2 == 12 || lnRigState2 == 11)
                                                    {
                                                        CircTime += lnTimeDuration;
                                                    }
                                                }

                                                if (totalTime > 0 && ReamingTime > 0 && CircTime > 0)
                                                {
                                                    double reamingPc = (ReamingTime * 100.0) / totalTime;
                                                    if (reamingPc >= 35.0 && totalCleanupTimeDuration >= 20.0)
                                                    {
                                                        strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_HoleCleaning} " +
                                                                 $"WHERE DATETIME>='{cucStartDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{cucEndDate:dd-MMM-yyyy HH:mm:ss}';";
                                                        objDataService.ExecuteNonQuery(strSQL);

                                                        logger.Debug($"Detected Activity : CUC : from date {cucStartDate:dd-MMM-yyyy HH:mm:ss} to {cucEndDate:dd-MMM-yyyy HH:mm:ss}");
                                                    }
                                                }
                                            }

                                            startDepth = MinDepth;
                                            startDepthDate = MinDepthDate;
                                            startDepthIndex = i;
                                            MaxDepth = MinDepth;
                                            MaxDepthDate = MinDepthDate;
                                            MaxDepthIndex = i;
                                            continue;
                                        }
                                    }
                                }
                            }

                            // ####### Detecting Trip In
                            if (lnDepth > startDepth)
                            {
                                if (lnDepth > MaxDepth)
                                {
                                    MaxDepth = lnDepth;
                                    MaxDepthDate = lnDateTime;
                                    MaxDepthIndex = i;
                                }

                                double diff = (lnDepth - startDepth);

                                if (diff > DepthThreshold)
                                {
                                    if (lnDepth < MaxDepth)
                                    {
                                        if ((MaxDepth - lnDepth) > DepthThreshold)
                                        {
                                            strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_TripIn} " +
                                                     $"WHERE DATETIME>='{startDepthDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{MaxDepthDate:dd-MMM-yyyy HH:mm:ss}';";
                                            objDataService.ExecuteNonQuery(strSQL);

                                            logger.Debug($"Detected Activity : Trip In : from date {startDepthDate:dd-MMM-yyyy HH:mm:ss} to {MaxDepthDate:dd-MMM-yyyy HH:mm:ss}");

                                            startDepth = MaxDepth;
                                            startDepthDate = MaxDepthDate;
                                            startDepthIndex = i;
                                            MinDepth = MaxDepth;
                                            MinDepthDate = MaxDepthDate;
                                            MinDepthIndex = i;
                                            lastTagDate = MaxDepthDate;
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Handle edge cases at the end of data buffer
                    if (lastDepth > startDepth && (lastDepth - startDepth) > DepthThreshold)
                    {
                        strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_TripIn} " +
                                 $"WHERE DATETIME>='{lastTagDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{lastDateTime:dd-MMM-yyyy HH:mm:ss}';";
                        objDataService.ExecuteNonQuery(strSQL);

                        logger.Debug($"Detected Activity : Trip In : from date {lastTagDate:dd-MMM-yyyy HH:mm:ss} to {lastDateTime:dd-MMM-yyyy HH:mm:ss}");
                    }

                    if (lastDepth < startDepth && (startDepth - lastDepth) > DepthThreshold)
                    {
                        strSQL = $"UPDATE [{dataTableName}] SET [{ActivityColumnName}]={Activity_TripOut} " +
                                 $"WHERE DATETIME>='{lastTagDate:dd-MMM-yyyy HH:mm:ss}' AND DATETIME<='{lastDateTime:dd-MMM-yyyy HH:mm:ss}';";
                        objDataService.ExecuteNonQuery(strSQL);

                        logger.Debug($"Detected Activity : Trip Out : from date {lastTagDate:dd-MMM-yyyy HH:mm:ss} to {lastDateTime:dd-MMM-yyyy HH:mm:ss}");
                    }

                    #endregion

                    logger.Debug("Creating Tags from activity codes ... ");

                    #region Tag Creation from Activity Codes

                    DateTime __existingLastTagIndexFromDate = DateTime.MinValue;
                    double lastExistingTagId = -1;
                    DateTime lastExistingTagEndDate = DateTime.MinValue;

                    if (objWellTags != null)
                    {
                        var sortedExisting = objWellTags.AsEnumerable()
                            .Select(r => new {
                                Index = Convert.ToDouble(DataService.checkNull(r["PHASE_INDEX"], 0.0)),
                                Start = ParseDate(r["START_DATE"]),
                                End = ParseDate(r["END_DATE"])
                            })
                            .Where(t => t.Start != DateTime.MinValue)
                            .OrderBy(t => t.Start)
                            .ToList();

                        if (sortedExisting.Count == 1)
                        {
                            objDataService.ExecuteNonQuery($"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");
                        }
                        else if (sortedExisting.Count > 1)
                        {
                            double lastTagIndex = sortedExisting[sortedExisting.Count - 1].Index;
                            objDataService.ExecuteNonQuery($"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={lastTagIndex.ToString(CultureInfo.InvariantCulture)};");
                            __existingLastTagIndexFromDate = sortedExisting[sortedExisting.Count - 2].Start;
                            lastExistingTagId = sortedExisting[sortedExisting.Count - 2].Index;
                            lastExistingTagEndDate = sortedExisting[sortedExisting.Count - 2].End;
                        }
                    }

                    logger.Debug($"Starting creating Tags from activity codes from date {processFromDate:dd-MMM-yyyy HH:mm:ss}");

                    VirtualTable.VirtualTable objVirtualDataActivity = new VirtualTable.VirtualTable();
                    objVirtualDataActivity.objDataService = objDataService;
                    objVirtualDataActivity.FieldList = $"DATETIME,{ActivityColumnName}";
                    objVirtualDataActivity.KeyField = "DATETIME";
                    objVirtualDataActivity.OrderBy = "rowid ASC";
                    objVirtualDataActivity.dataTableName = dataTableName;
                    objVirtualDataActivity.Condition = "";
                    objVirtualDataActivity.loadData();

                    if (objVirtualDataActivity.RecordCount > 0)
                    {
                        int startIdx = 0;
                        if (processFromDate != DateTime.MinValue)
                        {
                            for (int k = 0; k < objVirtualDataActivity.RecordCount; k++)
                            {
                                if (objVirtualDataActivity.getDateTimeValue(k, "DATETIME") >= processFromDate)
                                {
                                    startIdx = k;
                                    break;
                                }
                            }
                        }

                        DateTime firstRecDate = objVirtualDataActivity.getDateTimeValue(startIdx, "DATETIME");
                        DateTime currentTagStartDate = (lastExistingTagEndDate != DateTime.MinValue && lastExistingTagEndDate >= firstRecDate) 
                            ? lastExistingTagEndDate 
                            : firstRecDate;
                        double lastTagId = lastExistingTagId;

                        double lastActivity = objVirtualDataActivity.getDoubleValueWithNull(startIdx, ActivityColumnName, -1d);
                        DateTime lastDate = firstRecDate;

                        for (int i = startIdx; i < objVirtualDataActivity.RecordCount; i++)
                        {
                            if (_stopRequested) break;

                            double lnActivity = objVirtualDataActivity.getDoubleValueWithNull(i, ActivityColumnName, -1d);
                            if (lnActivity == -1d) continue;

                            DateTime currentDate = objVirtualDataActivity.getDateTimeValue(i, "DATETIME");

                            if (lnActivity != lastActivity && lastActivity != -1d)
                            {
                                DateTime tagEnd = lastDate;
                                if (tagEnd <= currentTagStartDate) tagEnd = currentTagStartDate.AddSeconds(5);

                                TagMapping objMapping = getTagMapping(lastActivity, currentTagStartDate, tagMappings, objTimeLog);

                                // Close previous tag
                                if (lastTagId > 0)
                                {
                                    strSQL = $"UPDATE VMX_PHASE_LIST SET IS_OPEN=0 WHERE PHASE_INDEX={lastTagId.ToString(CultureInfo.InvariantCulture)} AND WELL_ID='{WellID.Replace("'", "''")}';";
                                    objDataService.ExecuteNonQuery(strSQL);
                                }

                                int newTagIndex = GenerateUniquePhaseIndex(objDataService, WellID);
                                InsertPhaseTag(
                                    objDataService,
                                    newTagIndex,
                                    WellID,
                                    currentTagStartDate,
                                    tagEnd,
                                    objMapping.PhaseId,
                                    objMapping.StepId,
                                    objMapping.EmphId,
                                    notes: "Auto Generated Tag",
                                    isOpen: 1);

                                lastTagId = newTagIndex;
                                currentTagStartDate = tagEnd;
                            }

                            lastActivity = lnActivity;
                            lastDate = currentDate;
                        }

                        // Flush the final activity run
                        if (lastActivity != -1d && lastDate != DateTime.MinValue && lastDate > currentTagStartDate)
                        {
                            TagMapping objMapping = getTagMapping(lastActivity, currentTagStartDate, tagMappings, objTimeLog);

                            if (lastTagId > 0)
                            {
                                strSQL = $"UPDATE VMX_PHASE_LIST SET IS_OPEN=0 WHERE PHASE_INDEX={lastTagId.ToString(CultureInfo.InvariantCulture)} AND WELL_ID='{WellID.Replace("'", "''")}';";
                                objDataService.ExecuteNonQuery(strSQL);
                            }

                            int newTagIndex = GenerateUniquePhaseIndex(objDataService, WellID);
                            InsertPhaseTag(
                                objDataService,
                                newTagIndex,
                                WellID,
                                currentTagStartDate,
                                lastDate,
                                objMapping.PhaseId,
                                objMapping.StepId,
                                objMapping.EmphId,
                                notes: "Auto Generated Tag",
                                isOpen: 1);
                        }
                    }

                    #endregion

                    logger.Debug("Completed creating tags from activity codes ... ");
                    logger.Debug("Checking created tags ... ");

                    #region Adjusting Trip Out Tags that may include some part of Trip In

                    DataTable objNewTagsExRaw = objDataService.GetTable($"SELECT PHASE_INDEX,START_DATE,END_DATE,PHASE_ID,STEP_ID,EMPH_ID FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");

                    var sortedTagsEx = objNewTagsExRaw?.AsEnumerable()
                        .Select(r => new {
                            Row = r,
                            Start = ParseDate(r["START_DATE"]),
                            End = ParseDate(r["END_DATE"])
                        })
                        .Where(t => t.Start != DateTime.MinValue && (__existingLastTagIndexFromDate == DateTime.MinValue || t.Start > __existingLastTagIndexFromDate))
                        .OrderBy(t => t.Start)
                        .Select(t => t.Row)
                        .ToList() ?? new List<DataRow>();

                    if (sortedTagsEx.Count > 0 && tagMappings.ContainsKey(TripOut))
                    {
                        TagMapping objTripOutMapping = tagMappings[TripOut];

                        for (int i = 0; i < sortedTagsEx.Count; i++)
                        {
                            if (_stopRequested) break;
                            DataRow objRow = sortedTagsEx[i];

                            DateTime tagStartDate = ParseDate(objRow["START_DATE"]);
                            DateTime tagEndDate = ParseDate(objRow["END_DATE"]);
                            int phaseIndex = DataService.checkNull(objRow["PHASE_INDEX"], 0);
                            string __tagPhaseId = DataService.checkNull(objRow["PHASE_ID"], "").ToString();
                            string __tagStepId = DataService.checkNull(objRow["STEP_ID"], "").ToString();
                            string __tagEmphId = DataService.checkNull(objRow["EMPH_ID"], "").ToString();

                            if (__tagPhaseId == objTripOutMapping.PhaseId && __tagStepId == objTripOutMapping.StepId && __tagEmphId == objTripOutMapping.EmphId)
                            {
                                var (sRow, eRow) = ResolveTimeRangeToRowIds(objDataService, dataTableName, tagStartDate, tagEndDate);
                                if (sRow.HasValue && eRow.HasValue)
                                {
                                    var dtTagData = objDataService.GetTable($"SELECT DATETIME, DEPTH FROM [{dataTableName}] WHERE rowid >= {sRow.Value} AND rowid <= {eRow.Value};");
                                    if (dtTagData != null && dtTagData.Rows.Count > 0)
                                    {
                                        double minDepth = double.MaxValue;
                                        DateTime minDepthDate = DateTime.MinValue;

                                        for (int t = 0; t < dtTagData.Rows.Count; t++)
                                        {
                                            double __depth = DataService.checkNull(dtTagData.Rows[t]["DEPTH"], 0.0);
                                            DateTime __dt = ParseDate(dtTagData.Rows[t]["DATETIME"]);
                                            if (__depth < minDepth && __dt != DateTime.MinValue)
                                            {
                                                minDepth = __depth;
                                                minDepthDate = __dt;
                                            }
                                        }

                                        double __lastDepth = DataService.checkNull(dtTagData.Rows[dtTagData.Rows.Count - 1]["DEPTH"], 0.0);
                                        double __depthDiff = __lastDepth - minDepth;

                                        if (__depthDiff > 500 && minDepthDate != DateTime.MinValue && minDepthDate > tagStartDate && minDepthDate < tagEndDate)
                                        {
                                            if (i < sortedTagsEx.Count - 1)
                                            {
                                                int nextTagIndex = DataService.checkNull(sortedTagsEx[i + 1]["PHASE_INDEX"], 0);

                                                strSQL = $"UPDATE VMX_PHASE_LIST SET END_DATE='{minDepthDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={phaseIndex};";
                                                objDataService.ExecuteNonQuery(strSQL);

                                                strSQL = $"UPDATE VMX_PHASE_LIST SET START_DATE='{minDepthDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={nextTagIndex};";
                                                objDataService.ExecuteNonQuery(strSQL);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    #endregion

                    logger.Debug("Detecting casing runs ... ");

                    #region Detection of Casing Run Tag from created tags

                    DataTable objNewTagsRaw = objDataService.GetTable($"SELECT PHASE_INDEX,START_DATE,END_DATE,PHASE_ID,STEP_ID,EMPH_ID FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");

                    var sortedTags = objNewTagsRaw?.AsEnumerable()
                        .Select(r => new {
                            Row = r,
                            Start = ParseDate(r["START_DATE"]),
                            End = ParseDate(r["END_DATE"])
                        })
                        .Where(t => t.Start != DateTime.MinValue && (__existingLastTagIndexFromDate == DateTime.MinValue || t.Start > __existingLastTagIndexFromDate))
                        .OrderBy(t => t.Start)
                        .Select(t => t.Row)
                        .ToList() ?? new List<DataRow>();

                    if (sortedTags.Count > 0 && tagMappings.ContainsKey(TripIn) && tagMappings.ContainsKey(TripOut))
                    {
                        TagMapping objTripInMapping = tagMappings[TripIn];
                        TagMapping objTripOutMapping = tagMappings[TripOut];

                        for (int i = 0; i < sortedTags.Count; i++)
                        {
                            if (_stopRequested) break;
                            DataRow objRow = sortedTags[i];

                            DateTime tagStartDate = ParseDate(objRow["START_DATE"]);
                            DateTime tagEndDate = ParseDate(objRow["END_DATE"]);
                            int phaseIndex = DataService.checkNull(objRow["PHASE_INDEX"], 0);
                            string __tagPhaseId = DataService.checkNull(objRow["PHASE_ID"], "").ToString();
                            string __tagStepId = DataService.checkNull(objRow["STEP_ID"], "").ToString();
                            string __tagEmphId = DataService.checkNull(objRow["EMPH_ID"], "").ToString();

                            bool isTripIn = (__tagPhaseId == objTripInMapping.PhaseId && __tagStepId == objTripInMapping.StepId && __tagEmphId == objTripInMapping.EmphId);
                            bool isTripOut = (__tagPhaseId == objTripOutMapping.PhaseId && __tagStepId == objTripOutMapping.StepId && __tagEmphId == objTripOutMapping.EmphId);

                            if (isTripIn || isTripOut)
                            {
                                var (sRow, eRow) = ResolveTimeRangeToRowIds(objDataService, dataTableName, tagStartDate, tagEndDate);
                                var dtTagData = (sRow.HasValue && eRow.HasValue) 
                                    ? objDataService.GetTable($"SELECT DATETIME, DEPTH FROM [{dataTableName}] WHERE rowid >= {sRow.Value} AND rowid <= {eRow.Value};")
                                    : null;

                                if (dtTagData != null && dtTagData.Rows.Count > 1)
                                {
                                    for (int t = 1; t < dtTagData.Rows.Count; t++)
                                    {
                                        double prevDepth = DataService.checkNull(dtTagData.Rows[t - 1]["DEPTH"], 0.0);
                                        double currentDepth = DataService.checkNull(dtTagData.Rows[t]["DEPTH"], 0.0);
                                        double depthDiff = prevDepth - currentDepth;
                                        double prevCasingMaxDepth = -1;

                                        if (depthDiff >= 1000) // Direct depth jump indicating depth reset during casing run
                                        {
                                            Dictionary<int, clsPhaseTagEx> casingTagTypes = PhaseMappingEx.getList(objDataService, "CASING");

                                            if (casingTagTypes.Count == 0 && tagMappings.ContainsKey(Casing))
                                            {
                                                casingTagTypes[0] = new clsPhaseTagEx
                                                {
                                                    PhaseID = tagMappings[Casing].PhaseId,
                                                    StepID = tagMappings[Casing].StepId,
                                                    EmphID = tagMappings[Casing].EmphId,
                                                    Category = Casing
                                                };
                                            }

                                            var priorTags = objNewTagsRaw?.AsEnumerable()
                                                .Where(r => ParseDate(r["START_DATE"]) < tagStartDate)
                                                .OrderBy(r => ParseDate(r["START_DATE"]))
                                                .ToList();

                                            if (priorTags != null)
                                            {
                                                foreach (DataRow tagRow in priorTags)
                                                {
                                                    string __phaseID = DataService.checkNull(tagRow["PHASE_ID"], "").ToString();
                                                    string __stepID = DataService.checkNull(tagRow["STEP_ID"], "").ToString();
                                                    string __emphID = DataService.checkNull(tagRow["EMPH_ID"], "").ToString();
                                                    DateTime __casingTagStartDate = ParseDate(tagRow["START_DATE"]);
                                                    DateTime __casingTagEndDate = ParseDate(tagRow["END_DATE"]);

                                                    foreach (clsPhaseTagEx __casingTagType in casingTagTypes.Values)
                                                    {
                                                        if (__casingTagType.PhaseID == __phaseID && __casingTagType.StepID == __stepID && __casingTagType.EmphID == __emphID)
                                                        {
                                                            var (cSRow, cERow) = ResolveTimeRangeToRowIds(objDataService, dataTableName, __casingTagStartDate, __casingTagEndDate);
                                                            if (cSRow.HasValue && cERow.HasValue)
                                                            {
                                                                prevCasingMaxDepth = objDataService.GetDoubleValueFromDatabase(
                                                                    $"SELECT MAX(DEPTH) FROM [{dataTableName}] WHERE rowid >= {cSRow.Value} AND rowid <= {cERow.Value};");
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            if (isTripIn)
                                            {
                                                #region Handling Casing Tag Creation within Trip In tag

                                                if (prevCasingMaxDepth > 0 && tagMappings.ContainsKey(Casing))
                                                {
                                                    TagMapping objCasingMapping = tagMappings[Casing];
                                                    DateTime __tripInTagEndDate = DateTime.MinValue;

                                                    for (int d = 1; d < dtTagData.Rows.Count; d++)
                                                    {
                                                        double __subDepth = DataService.checkNull(dtTagData.Rows[d]["DEPTH"], 0.0);
                                                        if (__subDepth >= prevCasingMaxDepth)
                                                        {
                                                            __tripInTagEndDate = ParseDate(dtTagData.Rows[d]["DATETIME"]);
                                                            break;
                                                        }
                                                    }

                                                    if (__tripInTagEndDate != DateTime.MinValue && __tripInTagEndDate > tagStartDate && __tripInTagEndDate < tagEndDate)
                                                    {
                                                        strSQL = $"UPDATE VMX_PHASE_LIST SET END_DATE='{__tripInTagEndDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={phaseIndex};";
                                                        objDataService.ExecuteNonQuery(strSQL);

                                                        DateTime __casingTagStartDate = __tripInTagEndDate;
                                                        DateTime __casingTagEndDate = tagEndDate;
                                                        int newTagIndex = GenerateUniquePhaseIndex(objDataService, WellID);
                                                        InsertPhaseTag(
                                                            objDataService,
                                                            newTagIndex,
                                                            WellID,
                                                            __casingTagStartDate,
                                                            __casingTagEndDate,
                                                            objCasingMapping.PhaseId,
                                                            objCasingMapping.StepId,
                                                            objCasingMapping.EmphId,
                                                            notes: "Auto Generated Tag (Casing)",
                                                            isOpen: 1);
                                                    }
                                                }
                                                else if (tagMappings.ContainsKey(Casing))
                                                {
                                                    TagMapping objCasingMapping = tagMappings[Casing];
                                                    strSQL = $"UPDATE VMX_PHASE_LIST SET PHASE_ID='{utilFunctions.quote(objCasingMapping.PhaseId)}', STEP_ID='{utilFunctions.quote(objCasingMapping.StepId)}', EMPH_ID='{utilFunctions.quote(objCasingMapping.EmphId)}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={phaseIndex};";
                                                    objDataService.ExecuteNonQuery(strSQL);
                                                }

                                                #endregion
                                            }
                                            else
                                            {
                                                #region Handling Casing Tag Creation within Trip Out tag

                                                if (prevCasingMaxDepth > 0 && tagMappings.ContainsKey(Casing) && i < sortedTags.Count - 1)
                                                {
                                                    TagMapping objCasingMapping = tagMappings[Casing];

                                                    DateTime nextTagStart = ParseDate(sortedTags[i + 1]["START_DATE"]);
                                                    DateTime nextTagEnd = ParseDate(sortedTags[i + 1]["END_DATE"]);
                                                    int nextPhaseIndex = DataService.checkNull(sortedTags[i + 1]["PHASE_INDEX"], 0);

                                                    var (nSRow, nERow) = ResolveTimeRangeToRowIds(objDataService, dataTableName, nextTagStart, nextTagEnd);
                                                    var dtNextTagData = (nSRow.HasValue && nERow.HasValue)
                                                        ? objDataService.GetTable($"SELECT DATETIME, DEPTH FROM [{dataTableName}] WHERE rowid >= {nSRow.Value} AND rowid <= {nERow.Value};")
                                                        : null;

                                                    DateTime __tripInTagEndDate = DateTime.MinValue;

                                                    if (dtNextTagData != null)
                                                    {
                                                        for (int d = 1; d < dtNextTagData.Rows.Count; d++)
                                                        {
                                                            double __subDepth = DataService.checkNull(dtNextTagData.Rows[d]["DEPTH"], 0.0);
                                                            if (__subDepth >= prevCasingMaxDepth)
                                                            {
                                                                __tripInTagEndDate = ParseDate(dtNextTagData.Rows[d]["DATETIME"]);
                                                                break;
                                                            }
                                                        }
                                                    }

                                                    if (__tripInTagEndDate != DateTime.MinValue && __tripInTagEndDate > nextTagStart && __tripInTagEndDate < nextTagEnd)
                                                    {
                                                        strSQL = $"UPDATE VMX_PHASE_LIST SET END_DATE='{__tripInTagEndDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={nextPhaseIndex};";
                                                        objDataService.ExecuteNonQuery(strSQL);

                                                        DateTime __casingTagStartDate = __tripInTagEndDate;
                                                        DateTime __casingTagEndDate = nextTagEnd;
                                                        int newTagIndex = GenerateUniquePhaseIndex(objDataService, WellID);
                                                        InsertPhaseTag(
                                                            objDataService,
                                                            newTagIndex,
                                                            WellID,
                                                            __casingTagStartDate,
                                                            __casingTagEndDate,
                                                            objCasingMapping.PhaseId,
                                                            objCasingMapping.StepId,
                                                            objCasingMapping.EmphId,
                                                            notes: "Auto Generated Tag (Casing)",
                                                            isOpen: 1);
                                                    }
                                                }
                                                else if (i < sortedTags.Count - 1 && tagMappings.ContainsKey(Casing))
                                                {
                                                    int nextPhaseIndex = DataService.checkNull(sortedTags[i + 1]["PHASE_INDEX"], 0);
                                                    TagMapping objCasingMapping = tagMappings[Casing];

                                                    strSQL = $"UPDATE VMX_PHASE_LIST SET PHASE_ID='{utilFunctions.quote(objCasingMapping.PhaseId)}', STEP_ID='{utilFunctions.quote(objCasingMapping.StepId)}', EMPH_ID='{utilFunctions.quote(objCasingMapping.EmphId)}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={nextPhaseIndex};";
                                                    objDataService.ExecuteNonQuery(strSQL);
                                                }

                                                #endregion
                                            }

                                            break; // Processed depth jump for this tag
                                        }
                                    }
                                }
                            }
                        }
                    }

                    #endregion

                    logger.Debug("Merging minor tags ... ");

                    #region Merge the tags that were detected and has time period less than or equal to 30 seconds

                    if (sortedTags.Count > 0)
                    {
                        for (int i = 0; i < sortedTags.Count; i++)
                        {
                            if (_stopRequested) break;
                            DataRow objRow = sortedTags[i];

                            DateTime tagStartDate = ParseDate(objRow["START_DATE"]);
                            DateTime tagEndDate = ParseDate(objRow["END_DATE"]);
                            int phaseIndex = DataService.checkNull(objRow["PHASE_INDEX"], 0);

                            double timeDiff = Math.Abs((tagEndDate - tagStartDate).TotalSeconds);

                            if (timeDiff <= 30.0)
                            {
                                if (i < sortedTags.Count - 1)
                                {
                                    // Merge forward into next tag
                                    int nextPhaseIndex = DataService.checkNull(sortedTags[i + 1]["PHASE_INDEX"], 0);

                                    strSQL = $"UPDATE VMX_PHASE_LIST SET START_DATE='{tagStartDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={nextPhaseIndex};";
                                    objDataService.ExecuteNonQuery(strSQL);

                                    strSQL = $"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={phaseIndex};";
                                    objDataService.ExecuteNonQuery(strSQL);
                                }
                                else if (i > 0)
                                {
                                    // Merge backward into previous tag
                                    int prevPhaseIndex = DataService.checkNull(sortedTags[i - 1]["PHASE_INDEX"], 0);

                                    strSQL = $"UPDATE VMX_PHASE_LIST SET END_DATE='{tagEndDate:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={prevPhaseIndex};";
                                    objDataService.ExecuteNonQuery(strSQL);

                                    strSQL = $"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={phaseIndex};";
                                    objDataService.ExecuteNonQuery(strSQL);
                                }
                            }
                        }
                    }

                    #endregion

                    logger.Debug("Updating tags ... ");

                    // Close all tags except the most recent one
                    strSQL = $"UPDATE VMX_PHASE_LIST SET IS_OPEN=0 WHERE WELL_ID='{WellID.Replace("'", "''")}';";
                    objDataService.ExecuteNonQuery(strSQL);

                    var allTagsDt = objDataService.GetTable($"SELECT PHASE_INDEX, START_DATE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellID.Replace("'", "''")}';");
                    if (allTagsDt != null && allTagsDt.Rows.Count > 0)
                    {
                        var latestTagRow = allTagsDt.AsEnumerable()
                            .Select(r => new {
                                Index = DataService.checkNull(r["PHASE_INDEX"], 0.0),
                                Start = ParseDate(r["START_DATE"])
                            })
                            .Where(t => t.Start != DateTime.MinValue)
                            .OrderBy(t => t.Start)
                            .LastOrDefault();

                        if (latestTagRow != null && latestTagRow.Index > 0)
                        {
                            strSQL = $"UPDATE VMX_PHASE_LIST SET IS_OPEN=1 WHERE WELL_ID='{WellID.Replace("'", "''")}' AND PHASE_INDEX={latestTagRow.Index.ToString(CultureInfo.InvariantCulture)};";
                            objDataService.ExecuteNonQuery(strSQL);
                        }
                    }

                    logger.Debug("Completed ... ");

                    DateTime __lastDateTime = DateTime.MinValue;
                    double lastIndex = objTimeLog.getLastIndexOptimized(objDataService);
                    if (lastIndex > 0)
                    {
                        try { __lastDateTime = DateTime.FromOADate(lastIndex); } catch { }
                    }
                    if (__lastDateTime == DateTime.MinValue)
                    {
                        __lastDateTime = GetMaxDateFromTable(objDataService, dataTableName);
                    }
                }
                else
                {
                    logger.Debug("No primary time log found for the well. Aborting process ... ");
                }

                logger.Debug("Process completed ... ");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                logger?.Error("Error processing: " + ex.Message, ex);
            }
            finally
            {
                this.__processCompleted = true;
                bool success = string.IsNullOrWhiteSpace(LastError);
                ReportProgress(success ? $"Detection completed successfully. {DetectedTagsCount} phase tags processed." : $"Detection failed: {LastError}", 100.0);
                ProcessFinished?.Invoke(success, LastError);
            }
        }

        #endregion

        #region Tag Mapping Resolution

        /// <summary>
        /// Resolves the phase tag mapping for a given activity code and timestamp,
        /// first checking well-section-specific overrides, then falling back to defaults.
        /// </summary>
        private TagMapping getTagMapping(
            double paramActivityCode,
            DateTime tagStartDate,
            Dictionary<string, TagMapping> defaultMapping,
            TimeLog paramTimeLog)
        {
            try
            {
                TagMapping objMapping = new TagMapping();

                if (objDataService == null) return objMapping;

                // Find hole depth at tag start date
                double holeDepth = GetHoleDepthAtDate(objDataService, paramTimeLog, tagStartDate);
                string wellSectionName = "";

                // Find well section containing this hole depth
                if (holeDepth > 0)
                {
                    string holeDepthStr = holeDepth.ToString(CultureInfo.InvariantCulture);
                    DataTable objWellSection = objDataService.GetTable(
                        $"SELECT SECTION_NAME FROM VMX_WELL_SECTIONS WHERE WELL_ID='{paramTimeLog.WellID.Replace("'", "''")}' AND {holeDepthStr} >= START_DEPTH AND {holeDepthStr} <= END_DEPTH LIMIT 1;");

                    if (objWellSection != null && objWellSection.Rows.Count > 0)
                    {
                        wellSectionName = DataService.checkNull(objWellSection.Rows[0]["SECTION_NAME"], "").ToString();
                    }
                }

                // Check section-wise tag mappings if defined
                DataTable? objWellSectionWiseMapping = null;
                if (!string.IsNullOrWhiteSpace(wellSectionName) && objDataService.TableExists("eVuMaxWellSectionActivityTagMapping"))
                {
                    objWellSectionWiseMapping = objDataService.GetTable(
                        $"SELECT * FROM eVuMaxWellSectionActivityTagMapping WHERE UPPER(TRIM(WellSectionName))='{wellSectionName.Trim().ToUpperInvariant()}' LIMIT 1;");
                }

                if (objWellSectionWiseMapping != null && objWellSectionWiseMapping.Rows.Count > 0)
                {
                    DataRow objRow = objWellSectionWiseMapping.Rows[0];

                    if (paramActivityCode == Activity_RigUp)
                    {
                        objMapping.PhaseId = DataService.checkNull(objRow["RigUp_Phase"], "").ToString();
                        objMapping.StepId = DataService.checkNull(objRow["RigUp_Step"], "").ToString();
                        objMapping.EmphId = DataService.checkNull(objRow["RigUp_Emph"], "").ToString();
                    }
                    else if (paramActivityCode == Activity_Drilling)
                    {
                        objMapping.PhaseId = DataService.checkNull(objRow["Drlg_Phase"], "").ToString();
                        objMapping.StepId = DataService.checkNull(objRow["Drlg_Step"], "").ToString();
                        objMapping.EmphId = DataService.checkNull(objRow["Drlg_Emph"], "").ToString();
                    }
                    else if (paramActivityCode == Activity_HoleCleaning)
                    {
                        objMapping.PhaseId = DataService.checkNull(objRow["CUC_Phase"], "").ToString();
                        objMapping.StepId = DataService.checkNull(objRow["CUC_Step"], "").ToString();
                        objMapping.EmphId = DataService.checkNull(objRow["CUC_Emph"], "").ToString();
                    }
                    else if (paramActivityCode == Activity_TripOut)
                    {
                        objMapping.PhaseId = DataService.checkNull(objRow["TripOut_Phase"], "").ToString();
                        objMapping.StepId = DataService.checkNull(objRow["TripOut_Step"], "").ToString();
                        objMapping.EmphId = DataService.checkNull(objRow["TripOut_Emph"], "").ToString();
                    }
                    else if (paramActivityCode == Activity_TripIn)
                    {
                        objMapping.PhaseId = DataService.checkNull(objRow["TripIn_Phase"], "").ToString();
                        objMapping.StepId = DataService.checkNull(objRow["TripIn_Step"], "").ToString();
                        objMapping.EmphId = DataService.checkNull(objRow["TripIn_Emph"], "").ToString();
                    }
                }
                else
                {
                    // Fallback to default dictionary mappings
                    if (paramActivityCode == Activity_RigUp && defaultMapping.ContainsKey(RigUp))
                    {
                        objMapping = defaultMapping[RigUp].GetCopy();
                    }
                    else if (paramActivityCode == Activity_Drilling && defaultMapping.ContainsKey(Drilling))
                    {
                        objMapping = defaultMapping[Drilling].GetCopy();
                    }
                    else if (paramActivityCode == Activity_HoleCleaning && defaultMapping.ContainsKey(CUC))
                    {
                        objMapping = defaultMapping[CUC].GetCopy();
                    }
                    else if (paramActivityCode == Activity_TripOut && defaultMapping.ContainsKey(TripOut))
                    {
                        objMapping = defaultMapping[TripOut].GetCopy();
                    }
                    else if (paramActivityCode == Activity_TripIn && defaultMapping.ContainsKey(TripIn))
                    {
                        objMapping = defaultMapping[TripIn].GetCopy();
                    }
                }

                return objMapping;
            }
            catch (Exception)
            {
                return new TagMapping();
            }
        }

        #endregion

        #region Database & Primary Log Helpers

        /// <summary>
        /// Retrieves the primary TimeLog for a given WellID from DrillIntel's VMX_TIME_LOG table.
        /// </summary>
        public static TimeLog? GetPrimaryTimeLog(IDataServiceDIntel objDataService, string wellID)
        {
            if (objDataService == null) return null;

            try
            {
                string sql = !string.IsNullOrWhiteSpace(wellID)
                    ? $"SELECT LOG_ID, WELLBORE_ID, DATA_TABLE_NAME, WELL_ID, LOG_NAME FROM VMX_TIME_LOG WHERE WELL_ID='{wellID.Replace("'", "''")}' ORDER BY PRIMARY_LOG DESC LIMIT 1;"
                    : "SELECT LOG_ID, WELLBORE_ID, DATA_TABLE_NAME, WELL_ID, LOG_NAME FROM VMX_TIME_LOG ORDER BY PRIMARY_LOG DESC LIMIT 1;";

                DataTable dt = objDataService.GetTable(sql);

                if (dt != null && dt.Rows.Count > 0)
                {
                    string logId = DataService.checkNull(dt.Rows[0]["LOG_ID"], "");
                    string wellboreId = DataService.checkNull(dt.Rows[0]["WELLBORE_ID"], "");
                    string resolvedWellId = DataService.checkNull(dt.Rows[0]["WELL_ID"], wellID);
                    string lastError = "";

                    TimeLog? log = null;
                    if (!string.IsNullOrWhiteSpace(resolvedWellId) && !string.IsNullOrWhiteSpace(wellboreId) && !string.IsNullOrWhiteSpace(logId))
                    {
                        log = TimeLogService.LoadObject(objDataService, resolvedWellId, wellboreId, logId, ref lastError);
                    }
                    if (log == null && !string.IsNullOrWhiteSpace(logId))
                    {
                        log = TimeLogService.LoadObject(objDataService, logId, ref lastError);
                    }

                    if (log == null)
                    {
                        log = new TimeLog
                        {
                            WellID = resolvedWellId,
                            WellboreID = wellboreId,
                            ObjectID = logId,
                            nameLog = DataService.checkNull(dt.Rows[0]["LOG_NAME"], logId),
                            __dataTableName = DataService.checkNull(dt.Rows[0]["DATA_TABLE_NAME"], "")
                        };
                    }
                    else if (string.IsNullOrWhiteSpace(log.__dataTableName))
                    {
                        log.__dataTableName = DataService.checkNull(dt.Rows[0]["DATA_TABLE_NAME"], "");
                    }

                    return log;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Calculates or retrieves the hole depth (HDTH or DEPTH) in the timelog at a given date.
        /// </summary>
        public static double GetHoleDepthAtDate(IDataServiceDIntel objDataService, TimeLog timeLog, DateTime targetDate)
        {
            if (objDataService == null || timeLog == null || string.IsNullOrWhiteSpace(timeLog.__dataTableName)) return 0.0;

            string tableName = timeLog.__dataTableName;
            string dateStr = targetDate.ToString("dd-MMM-yyyy HH:mm:ss");

            try
            {
                // Try backward lookup first
                string sql = $"SELECT HDTH FROM [{tableName}] WHERE DATETIME <= '{dateStr}' AND HDTH IS NOT NULL ORDER BY DATETIME DESC LIMIT 1;";
                var val = objDataService.GetValue(sql);
                if (val != null && val != DBNull.Value && double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double hdth))
                {
                    return hdth;
                }

                // Try forward lookup if backward had no data
                sql = $"SELECT HDTH FROM [{tableName}] WHERE DATETIME >= '{dateStr}' AND HDTH IS NOT NULL ORDER BY DATETIME ASC LIMIT 1;";
                val = objDataService.GetValue(sql);
                if (val != null && val != DBNull.Value && double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out hdth))
                {
                    return hdth;
                }

                // Fall back to DEPTH curve
                sql = $"SELECT DEPTH FROM [{tableName}] WHERE DATETIME <= '{dateStr}' AND DEPTH IS NOT NULL ORDER BY DATETIME DESC LIMIT 1;";
                val = objDataService.GetValue(sql);
                if (val != null && val != DBNull.Value && double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double depth))
                {
                    return depth;
                }
            }
            catch { }

            return 0.0;
        }

        /// <summary>
        /// Generates a unique 6-digit PHASE_INDEX for a well in VMX_PHASE_LIST.
        /// </summary>
        private static int GenerateUniquePhaseIndex(IDataServiceDIntel objDataService, string wellId)
        {
            int index = Random.Shared.Next(100000, 999999);
            try
            {
                while (objDataService.IsRecordExist($"SELECT PHASE_INDEX FROM VMX_PHASE_LIST WHERE WELL_ID='{wellId.Replace("'", "''")}' AND PHASE_INDEX={index} LIMIT 1;"))
                {
                    index = Random.Shared.Next(100000, 999999);
                }
            }
            catch { }
            return index;
        }

        /// <summary>
        /// Reads the earliest DATETIME record from the specified timelog table using monotonic rowid.
        /// </summary>
        public static DateTime GetMinDateFromTable(IDataServiceDIntel objDataService, string tableName)
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(tableName)) return DateTime.MinValue;
            try
            {
                var dt = objDataService.GetTable($"SELECT DATETIME FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '' ORDER BY rowid ASC LIMIT 1;");
                if (dt != null && dt.Rows.Count > 0)
                {
                    return ParseDate(dt.Rows[0]["DATETIME"]);
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Reads the latest DATETIME record from the specified timelog table using monotonic rowid.
        /// </summary>
        public static DateTime GetMaxDateFromTable(IDataServiceDIntel objDataService, string tableName)
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(tableName)) return DateTime.MinValue;
            try
            {
                var dt = objDataService.GetTable($"SELECT DATETIME FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '' ORDER BY rowid DESC LIMIT 1;");
                if (dt != null && dt.Rows.Count > 0)
                {
                    return ParseDate(dt.Rows[0]["DATETIME"]);
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        /// <summary>
        /// Resolves a DateTime range [fromDate, toDate] into monotonic rowid bounds using binary search.
        /// Bypasses SQLite non-ISO string date ordering limitations.
        /// </summary>
        public static (long? startRow, long? endRow) ResolveTimeRangeToRowIds(
            IDataServiceDIntel dataService,
            string tableName,
            DateTime fromDate,
            DateTime toDate)
        {
            if (dataService == null || string.IsNullOrWhiteSpace(tableName)) return (null, null);

            try
            {
                object? minObj = dataService.GetValue($"SELECT MIN(rowid) FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '';");
                object? maxObj = dataService.GetValue($"SELECT MAX(rowid) FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '';");
                if (minObj == null || minObj == DBNull.Value || maxObj == null || maxObj == DBNull.Value)
                    return (null, null);

                long minRow = Convert.ToInt64(minObj);
                long maxRow = Convert.ToInt64(maxObj);
                if (maxRow < minRow) return (null, null);

                long low = minRow, high = maxRow, startRow = maxRow + 1;
                while (low <= high)
                {
                    long mid = low + (high - low) / 2;
                    using var reader = dataService.ExecuteReader($"SELECT rowid, DATETIME FROM [{tableName}] WHERE rowid >= {mid} ORDER BY rowid ASC LIMIT 1;");
                    if (reader != null && reader.Read())
                    {
                        long r = reader.GetInt64(0);
                        DateTime dt = ParseDate(reader.GetValue(1));
                        if (dt >= fromDate)
                        {
                            startRow = r;
                            high = mid - 1;
                        }
                        else
                        {
                            low = r + 1;
                        }
                    }
                    else high = mid - 1;
                }

                low = minRow; high = maxRow;
                long endRow = minRow - 1;
                while (low <= high)
                {
                    long mid = low + (high - low) / 2;
                    using var reader = dataService.ExecuteReader($"SELECT rowid, DATETIME FROM [{tableName}] WHERE rowid <= {mid} ORDER BY rowid DESC LIMIT 1;");
                    if (reader != null && reader.Read())
                    {
                        long r = reader.GetInt64(0);
                        DateTime dt = ParseDate(reader.GetValue(1));
                        if (dt <= toDate)
                        {
                            endRow = r;
                            low = mid + 1;
                        }
                        else
                        {
                            high = r - 1;
                        }
                    }
                    else low = mid + 1;
                }

                if (startRow > maxRow || endRow < minRow || startRow > endRow)
                    return (null, null);

                return (startRow, endRow);
            }
            catch
            {
                return (null, null);
            }
        }

        public static DateTime ParseDate(object? obj)
        {
            if (obj == null || obj == DBNull.Value) return DateTime.MinValue;
            if (obj is DateTime d) return d;

            string str = obj.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(str)) return DateTime.MinValue;

            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double oa))
            {
                if (oa > 30000 && oa < 70000)
                {
                    try { return DateTime.FromOADate(oa); } catch { }
                }
            }

            string[] formats = new[]
            {
                "dd-MMM-yyyy HH:mm:ss",
                "dd-MMM-yyyy HH:mm",
                "dd-MMM-yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd",
                "MM/dd/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "dd-MM-yyyy HH:mm:ss"
            };

            if (DateTime.TryParseExact(str, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;

            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;

            if (DateTime.TryParse(str, out dt))
                return dt;

            return DateTime.MinValue;
        }

        /// <summary>
        /// Ensures all prerequisite SQLite tables exist to prevent missing table exceptions.
        /// </summary>
        private static void EnsureTablesExist(IDataServiceDIntel objDataService)
        {
            if (objDataService == null) return;

            try
            {
                // VMX_PHASE_LIST
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS VMX_PHASE_LIST (
                        PHASE_INDEX REAL NOT NULL,
                        WELL_ID TEXT NOT NULL,
                        START_DATE TEXT NOT NULL,
                        END_DATE TEXT NOT NULL,
                        PHASE_ID TEXT NOT NULL,
                        STEP_ID TEXT NOT NULL,
                        EMPH_ID TEXT NOT NULL,
                        NOTES TEXT,
                        CREATED_BY TEXT,
                        CREATED_DATE TEXT,
                        MODIFIED_BY TEXT,
                        MODIFIED_DATE TEXT,
                        IS_OPEN INTEGER DEFAULT 0,
                        PRIMARY KEY (PHASE_INDEX, WELL_ID)
                    );");

                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "NOTES", "TEXT");
                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "CREATED_BY", "TEXT");
                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "CREATED_DATE", "TEXT");
                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "MODIFIED_BY", "TEXT");
                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "MODIFIED_DATE", "TEXT");
                EnsureColumnExists(objDataService, "VMX_PHASE_LIST", "IS_OPEN", "INTEGER DEFAULT 0");

                // eVuMaxActivityTagMapping
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS eVuMaxActivityTagMapping (
                        ActivityType TEXT PRIMARY KEY,
                        PhaseId TEXT,
                        StepId TEXT,
                        EmphId TEXT
                    );");

                // eVuMaxTagDetectionSettings
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS eVuMaxTagDetectionSettings (
                        StandHeight REAL DEFAULT 120,
                        DrillingTimeLimit REAL DEFAULT 900,
                        DepthThreshold REAL DEFAULT 300
                    );");

                // If eVuMaxTagDetectionSettings exists and no data is there, insert default values
                var settingsCount = objDataService.GetValue("SELECT COUNT(*) FROM eVuMaxTagDetectionSettings;");
                if (settingsCount == null || Convert.ToInt64(settingsCount) == 0)
                {
                    objDataService.ExecuteNonQuery(@"
                        INSERT INTO eVuMaxTagDetectionSettings (StandHeight, DrillingTimeLimit, DepthThreshold)
                        VALUES (120, 900, 300);");
                }

                // eVuMaxWellSectionActivityTagMapping
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS eVuMaxWellSectionActivityTagMapping (
                        WellSectionName TEXT PRIMARY KEY,
                        RigUp_Phase TEXT, RigUp_Step TEXT, RigUp_Emph TEXT,
                        Drlg_Phase TEXT, Drlg_Step TEXT, Drlg_Emph TEXT,
                        CUC_Phase TEXT, CUC_Step TEXT, CUC_Emph TEXT,
                        TripOut_Phase TEXT, TripOut_Step TEXT, TripOut_Emph TEXT,
                        TripIn_Phase TEXT, TripIn_Step TEXT, TripIn_Emph TEXT
                    );");

                // Clean up legacy web-application tracking tables if present
                if (objDataService.TableExists("eVuMaxWellTaskTracking"))
                {
                    objDataService.ExecuteNonQuery("DROP TABLE IF EXISTS eVuMaxWellTaskTracking;");
                }
                if (objDataService.TableExists("eVuMaxTasks"))
                {
                    objDataService.ExecuteNonQuery("DROP TABLE IF EXISTS eVuMaxTasks;");
                }

                // VMX_WELL_SECTIONS
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS VMX_WELL_SECTIONS (
                        WELL_ID TEXT,
                        SECTION_NAME TEXT,
                        START_DEPTH REAL,
                        END_DEPTH REAL
                    );");
            }
            catch { }
        }

        /// <summary>
        /// Seeds default activity tag mappings into the dictionary and database.
        /// </summary>
        private static void SeedDefaultTagMappings(IDataServiceDIntel objDataService, Dictionary<string, TagMapping> tagMappings)
        {
            var defaults = new[]
            {
                new TagMapping(RigUp, "RIG_UP", "RIG_UP", ""),
                new TagMapping(Drilling, "DRILLING", "DRILLING", ""),
                new TagMapping(CUC, "HOLE_CLEANING", "CUC", ""),
                new TagMapping(TripOut, "TRIP_OUT", "TRIP_OUT", ""),
                new TagMapping(TripIn, "TRIP_IN", "TRIP_IN", ""),
                new TagMapping(Casing, "CASING", "CASING", "")
            };

            foreach (var m in defaults)
            {
                if (!tagMappings.ContainsKey(m.ActivityType))
                {
                    tagMappings.Add(m.ActivityType, m);
                }

                try
                {
                    objDataService.ExecuteNonQuery(
                        $"INSERT OR IGNORE INTO eVuMaxActivityTagMapping (ActivityType, PhaseId, StepId, EmphId) VALUES ('{m.ActivityType}', '{m.PhaseId}', '{m.StepId}', '{m.EmphId}');");
                }
                catch { }
            }
        }

        /// <summary>
        /// Replaces special characters in a string with a delimiter (matching legacy Util.removeSpecialChars).
        /// </summary>
        private static string RemoveSpecialChars(string input, string replacement = "_")
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append(replacement);
            }
            return sb.ToString();
        }

        #endregion

        #region Phase List Helper Methods

        private HashSet<string> _phaseListColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> GetPhaseListColumns(IDataServiceDIntel db)
        {
            if (_phaseListColumns.Count == 0 && db != null)
            {
                try
                {
                    DataTable? dt = db.GetTable("PRAGMA table_info(VMX_PHASE_LIST);");
                    if (dt != null)
                    {
                        foreach (DataRow row in dt.Rows)
                        {
                            string col = Convert.ToString(row["name"]) ?? "";
                            if (!string.IsNullOrWhiteSpace(col))
                            {
                                _phaseListColumns.Add(col);
                            }
                        }
                    }
                }
                catch { }
            }
            return _phaseListColumns;
        }

        private static void EnsureColumnExists(IDataServiceDIntel db, string tableName, string columnName, string columnType)
        {
            try
            {
                DataTable? dt = db.GetTable($"PRAGMA table_info([{tableName}]);");
                if (dt != null)
                {
                    bool exists = dt.AsEnumerable().Any(r => string.Equals(r["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        db.ExecuteNonQuery($"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {columnType};");
                    }
                }
            }
            catch { }
        }

        private bool InsertPhaseTag(
            IDataServiceDIntel db,
            int phaseIndex,
            string wellId,
            DateTime startDate,
            DateTime endDate,
            string phaseId,
            string stepId,
            string emphId,
            string? notes = "Auto Generated Tag",
            string? createdBy = null,
            DateTime? createdDate = null,
            string? modifiedBy = null,
            DateTime? modifiedDate = null,
            int isOpen = 1)
        {
            if (db == null) return false;

            try
            {
                string currentUser = !string.IsNullOrWhiteSpace(createdBy)
                    ? createdBy
                    : (!string.IsNullOrWhiteSpace(db.UserName) ? db.UserName : "SYSTEM");
                DateTime now = createdDate ?? DateTime.Now;
                string nowFormatted = now.ToString("dd-MMM-yyyy HH:mm:ss");

                var availableCols = GetPhaseListColumns(db);

                var colNames = new List<string> { "PHASE_INDEX", "WELL_ID", "START_DATE", "END_DATE", "PHASE_ID", "STEP_ID", "EMPH_ID" };
                var colVals = new List<string>
                {
                    phaseIndex.ToString(CultureInfo.InvariantCulture),
                    $"'{wellId.Replace("'", "''")}'",
                    $"'{startDate:dd-MMM-yyyy HH:mm:ss}'",
                    $"'{endDate:dd-MMM-yyyy HH:mm:ss}'",
                    $"'{utilFunctions.quote(phaseId)}'",
                    $"'{utilFunctions.quote(stepId)}'",
                    $"'{utilFunctions.quote(emphId)}'"
                };

                if (availableCols.Contains("NOTES"))
                {
                    colNames.Add("NOTES");
                    colVals.Add(string.IsNullOrWhiteSpace(notes) ? "NULL" : $"'{utilFunctions.quote(notes)}'");
                }

                if (availableCols.Contains("CREATED_BY"))
                {
                    colNames.Add("CREATED_BY");
                    colVals.Add($"'{utilFunctions.quote(currentUser)}'");
                }

                if (availableCols.Contains("CREATED_DATE"))
                {
                    colNames.Add("CREATED_DATE");
                    colVals.Add($"'{nowFormatted}'");
                }

                if (availableCols.Contains("MODIFIED_BY"))
                {
                    colNames.Add("MODIFIED_BY");
                    colVals.Add($"'{utilFunctions.quote(modifiedBy ?? currentUser)}'");
                }

                if (availableCols.Contains("MODIFIED_DATE"))
                {
                    colNames.Add("MODIFIED_DATE");
                    colVals.Add($"'{nowFormatted}'");
                }

                if (availableCols.Contains("IS_OPEN"))
                {
                    colNames.Add("IS_OPEN");
                    colVals.Add(isOpen.ToString(CultureInfo.InvariantCulture));
                }

                string sql = $"INSERT INTO VMX_PHASE_LIST ({string.Join(",", colNames)}) VALUES ({string.Join(",", colVals)});";
                bool ok = db.ExecuteNonQuery(sql);
                if (!ok)
                {
                    LastError = $"Failed to insert phase tag {phaseIndex} into VMX_PHASE_LIST: {db.LastError}";
                    logger?.Error(LastError);
                    return false;
                }

                DetectedTagsCount++;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"Error inserting phase tag {phaseIndex}: {ex.Message}";
                logger?.Error(LastError);
                return false;
            }
        }

        #endregion

    }
}
