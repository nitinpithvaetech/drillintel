using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using VirtualTable;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor
{
    #region Logger Abstraction
    public interface IDrillingConnectionsLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public class DefaultDrillingConnectionsLogger : IDrillingConnectionsLogger
    {
        private readonly Action<string>? _logAction;

        public DefaultDrillingConnectionsLogger(Action<string>? logAction = null)
        {
            _logAction = logAction;
        }

        public void Debug(string message)
        {
            _logAction?.Invoke($"[DEBUG] {message}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
        }

        public void Info(string message)
        {
            _logAction?.Invoke($"[INFO] {message}");
            System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
        }

        public void Warn(string message)
        {
            _logAction?.Invoke($"[WARN] {message}");
            System.Diagnostics.Debug.WriteLine($"[WARN] {message}");
        }

        public void Error(string message)
        {
            _logAction?.Invoke($"[ERROR] {message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
        }
    }
    #endregion

    public class DrillingConnectionsProcessor
    {
        // === External Context Dependencies =====
        public IDataServiceDIntel? objLocalConn { get; set; }
        public TimeLog? __localTimeLog { get; set; } = null;
        public string WellID { get; set; } = "";
        // =======================================

        public Dictionary<int, ConnectionLogPoint> connectionPoints { get; set; } = new Dictionary<int, ConnectionLogPoint>();
        public double SlotTimeSec { get; set; } = 1800d;
        public double MAX_DEPTH_DIFF { get; set; } = 70d;
        public double MAX_CONN_TIME { get; set; } = 15d;
        public string DayTimeHoursFrom { get; set; } = "06:00:00";
        public string DayTimeHoursTo { get; set; } = "18:00:00";
        public string LastError { get; set; } = "";

        private rigState? objRigState;
        public DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings objSystemSettings { get; set; } = new DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings();

        #region Algorithm Settings
        public bool RUN_DRLG_CHECK { get; set; } = true;
        public bool RUN_HKLD_CHECK { get; set; } = true;
        public bool SkipConnections { get; set; } = true;
        public double MaxConnectionTimeMinutes { get; set; } = 30.0;
        public bool OverwriteExisting { get; set; } = false;
        #endregion

        public IDrillingConnectionsLogger logger { get; set; } = new DefaultDrillingConnectionsLogger();

        public static void EnsureTables(IDataServiceDIntel db)
        {
            if (db == null) return;
            KPIDrillingConnection.EnsureTableExists(db);
            DrlgConnectionLabel.EnsureTableExists(db);
        }

        public ConnectionLogPoint.cnDayNightTime determineDayOrNight(DateTime paramDate)
        {
            try
            {
                DateTime localDate = paramDate.ToLocalTime();

                string dayTime = !string.IsNullOrWhiteSpace(DayTimeHoursFrom)
                    ? DayTimeHoursFrom
                    : objSystemSettings.getSetting(SystemSettings.DayTimeFrom);

                if (string.IsNullOrWhiteSpace(dayTime))
                {
                    dayTime = "06:00:00";
                }

                if (dayTime.Count(c => c == ':') == 1)
                {
                    dayTime += ":00";
                }

                DateTime dayTimeFrom;
                if (!DateTime.TryParse(localDate.ToString("dd-MMM-yyyy") + " " + dayTime, out dayTimeFrom))
                {
                    dayTimeFrom = new DateTime(localDate.Year, localDate.Month, localDate.Day, 6, 0, 0);
                }

                DateTime dayTimeTo = dayTimeFrom.AddHours(11).AddMinutes(59).AddSeconds(59);

                DateTime current = localDate;
                return (current >= dayTimeFrom && current <= dayTimeTo)
                    ? ConnectionLogPoint.cnDayNightTime.DayTime
                    : ConnectionLogPoint.cnDayNightTime.NightTime;
            }
            catch (Exception ex)
            {
                logger?.Error("Error in determineDayOrNight: " + ex.Message + " " + ex.StackTrace);
                return ConnectionLogPoint.cnDayNightTime.DayTime;
            }
        }

        public void saveConnections(ref int savedCount, ref int skippedCount)
        {
            try
            {
                if (objLocalConn == null) return;

                bool skipConnections = SkipConnections;
                double maxConnTime = MaxConnectionTimeMinutes;

                // Also check if system settings override or provide defaults if not set
                if (objSystemSettings.settings.ContainsKey("SkipDrillingConnectionMoreThan"))
                {
                    skipConnections = objSystemSettings.getBooleanSetting("SkipDrillingConnectionMoreThan");
                }
                if (objSystemSettings.settings.ContainsKey("SkipDrillingConnectionMoreThanValue"))
                {
                    double val = objSystemSettings.getNumericSetting("SkipDrillingConnectionMoreThanValue");
                    if (val > 0) maxConnTime = val;
                }

                foreach (ConnectionLogPoint objItem in connectionPoints.Values)
                {
                    bool entryExists = KPIDrillingConnection.isDBEntryExist(objLocalConn, WellID, objItem);

                    if (!entryExists || OverwriteExisting)
                    {
                        var objConnection = KPIDrillingConnection.createKPIDrlgConnection(WellID, objItem, objLocalConn);

                        if (objConnection != null)
                        {
                            objConnection.WellID = WellID;
                            objConnection.UserComment = DrlgConnectionLabel.getComment(objLocalConn, objItem.Depth, WellID);

                            if (maxConnTime > 0 && skipConnections)
                            {
                                double connTime = objConnection.BottomToSlips + objConnection.SlipsToSlips + objConnection.SlipsToBottom;

                                if (connTime > 0)
                                {
                                    connTime = connTime / 60.0;

                                    if (connTime > maxConnTime)
                                    {
                                        logger?.Debug($"Skipping drilling connection @ {objConnection.Depth:F2} as time is {connTime:F2} min. Max time allowed: {maxConnTime:F2} min");
                                        skippedCount++;
                                    }
                                    else
                                    {
                                        string err = "";
                                        if (KPIDrillingConnection.addToDatabase(objLocalConn, objConnection, ref err))
                                        {
                                            savedCount++;
                                        }
                                        else
                                        {
                                            LastError = err;
                                        }
                                    }
                                }
                                else
                                {
                                    string err = "";
                                    if (KPIDrillingConnection.addToDatabase(objLocalConn, objConnection, ref err))
                                    {
                                        savedCount++;
                                    }
                                    else
                                    {
                                        LastError = err;
                                    }
                                }
                            }
                            else
                            {
                                string err = "";
                                if (KPIDrillingConnection.addToDatabase(objLocalConn, objConnection, ref err))
                                {
                                    savedCount++;
                                }
                                else
                                {
                                    LastError = err;
                                }
                            }
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }

                // Remove excluded connections
                string removeErr = "";
                KPIDrillingConnection.removeExcludedConnections(objLocalConn, WellID, ref removeErr);
            }
            catch (Exception ex)
            {
                logger?.Error("Error in saveConnections: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        public void ProcessPoints(TimeLog objTimeLog, DateTime fromDate, DateTime toDate, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (objLocalConn == null)
                {
                    LastError = "DataService connection is null.";
                    return;
                }

                EnsureTables(objLocalConn);

                // Load Settings
                objSystemSettings.LoadSettings(objLocalConn);

                __localTimeLog = objTimeLog;
                connectionPoints.Clear();

                if (objTimeLog == null)
                {
                    LastError = "TimeLog object is null.";
                    return;
                }

                objRigState = rigState.loadWellRigStateSetup(objLocalConn, WellID);
                if (objRigState == null)
                {
                    objRigState = new rigState();
                }

                string dataTableName = objTimeLog.getDataTableName(objLocalConn);
                if (string.IsNullOrWhiteSpace(dataTableName))
                {
                    dataTableName = objTimeLog.__dataTableName;
                }

                if (string.IsNullOrWhiteSpace(dataTableName))
                {
                    LastError = "Data table name for TimeLog could not be determined.";
                    return;
                }

                var objVirtualData = new VirtualTable.VirtualTable
                {
                    objDataService = objLocalConn,
                    FieldList = "DATETIME,DEPTH,HDTH,HKLD,RIG_STATE,TIME_DURATION",
                    KeyField = "DATETIME",
                    KeyFieldFormatted = "DATETIME",
                    FrameSize = 1000,
                    dataTableName = dataTableName,
                    Condition = " DATETIME>='" + fromDate.ToString("dd-MMM-yyyy HH:mm:ss") + "' AND DATETIME<='" + toDate.ToString("dd-MMM-yyyy HH:mm:ss") + "'"
                };

                objVirtualData.loadData();

                logger?.Debug("Processing drilling connections from date " + fromDate.ToString("dd-MMM-yyyy HH:mm:ss") + " to " + toDate.ToString("dd-MMM-yyyy HH:mm:ss"));

                int totalRows = objVirtualData.RecordCount;
                if (totalRows == 0)
                {
                    logger?.Info("No timelog records found in the specified date range.");
                    progress?.Report(100.0);
                    return;
                }

                double percentCompleted = 0.0;

                for (int i = 0; i < totalRows; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE", -1);
                    double lnHkld = objVirtualData.getDoubleValue(i, "HKLD", 0);

                    if (lnRigState == 2d || (lnRigState == 13d && lnHkld < objRigState.HookloadCutOff))
                    {
                        int forwardPointer = i;
                        detectAndAddPoints(objVirtualData, i, ref forwardPointer);
                        i = forwardPointer;
                    }

                    percentCompleted = Math.Round((double)i * 100.0 / totalRows, 2);
                    if (percentCompleted > 100.0) percentCompleted = 100.0;

                    if (i % 100 == 0 || i == totalRows - 1)
                    {
                        progress?.Report(percentCompleted);
                    }
                }

                progress?.Report(100.0);

                // Save connections to database
                int savedCount = 0;
                int skippedCount = 0;
                saveConnections(ref savedCount, ref skippedCount);

                logger?.Info($"Drilling connections processed successfully. Detected: {connectionPoints.Count}, Saved: {savedCount}, Skipped: {skippedCount}.");
            }
            catch (OperationCanceledException)
            {
                logger?.Warn("Process drilling connections cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + Environment.NewLine + ex.StackTrace;
                logger?.Error("Error in ProcessPoints: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        public void detectAndAddPoints(VirtualTable.VirtualTable objData, int currentRowIndex, ref int forwardPointer)
        {
            try
            {
                double lnHKldCutOff = objRigState?.HookloadCutOff ?? 90;
                double lnInSlipsDepth = objData.getDoubleValue(currentRowIndex, "DEPTH");
                double lnHoleDepth = objData.getDoubleValue(currentRowIndex, "HDTH");

                bool isDrillingFound = false;
                bool isNextDrillingFound = false;

                DateTime pointerDate = objData.getDateTimeValue(currentRowIndex, "DATETIME");

                logger?.Debug("Detecting connection @" + pointerDate.ToString("dd-MMM-yyyy HH:mm:ss") + " Depth: " + lnInSlipsDepth.ToString(CultureInfo.InvariantCulture) + " Hole Depth " + lnHoleDepth.ToString(CultureInfo.InvariantCulture));

                int timeThreshold = 5 * 60; // 5 hours in minutes
                DateTime lowerLimit = pointerDate.AddMinutes(-1 * timeThreshold);
                DateTime upperLimit = pointerDate.AddMinutes(timeThreshold);

                DateTime drillingEndTime = DateTime.MinValue;
                DateTime drillingStartTime = DateTime.MinValue;
                DateTime slipsStartTime = DateTime.MinValue;
                DateTime slipsEndTime = DateTime.MinValue;
                DateTime slipsToBottomTime = DateTime.MinValue;

                int drillingEndRowIndex = 0;
                int slipsStartRowIndex = 0;
                int slipsEndRowIndex = 0;
                int slipsToBottomRowIndex = 0;
                int drillingStartRowIndex = 0;
                int depthCounter = 0;

                // Move forward and find the end of the connection and start of the drilling ...
                for (int i = currentRowIndex; i <= objData.RecordCount - 1; i++)
                {
                    double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);
                    drillingStartTime = objData.getDateTimeValue(i, "DATETIME");
                    drillingStartRowIndex = i;

                    if (drillingStartTime > upperLimit)
                    {
                        break;
                    }

                    if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                    {
                        // Check if it's drilling for at least 1 minute ...
                        DateTime currentDate = drillingStartTime;
                        bool isConstantlyDrilling = true;
                        int lnBreakRow = i;

                        for (int j = i; j <= objData.RecordCount - 1; j++)
                        {
                            double lnSubRigState = objData.getDoubleValue(j, "RIG_STATE", -1);
                            DateTime lnSubDate = objData.getDateTimeValue(j, "DATETIME");

                            if (Math.Abs((currentDate - lnSubDate).TotalSeconds) >= 60)
                            {
                                break;
                            }

                            if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                            {
                            }
                            else
                            {
                                isConstantlyDrilling = false;
                                lnBreakRow = j;
                                break;
                            }
                        }

                        if (!RUN_DRLG_CHECK)
                        {
                            isConstantlyDrilling = true;
                        }

                        if (!isConstantlyDrilling)
                        {
                            bool subDrillingRowFound = false;

                            for (int j = lnBreakRow; j <= objData.RecordCount - 1; j++)
                            {
                                double lnSubRigState = objData.getDoubleValue(j, "RIG_STATE", -1);
                                DateTime lnSubDate = objData.getDateTimeValue(j, "DATETIME");

                                if (lnSubDate > upperLimit)
                                {
                                    break;
                                }

                                if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                                {
                                    if (RUN_DRLG_CHECK)
                                    {
                                        int subSubStartRow = j;

                                        for (int ssr = subSubStartRow; ssr <= objData.RecordCount - 1; ssr++)
                                        {
                                            if (ssr == objData.RecordCount - 1)
                                            {
                                                break;
                                            }

                                            double lnsSubRigState = objData.getDoubleValue(ssr, "RIG_STATE", -1);
                                            DateTime lnsSubDate = objData.getDateTimeValue(ssr, "DATETIME");

                                            if (lnsSubRigState == 0d || lnsSubRigState == 1d || lnsSubRigState == 19d)
                                            {
                                                if (Math.Abs((lnsSubDate - lnSubDate).TotalSeconds) >= 60)
                                                {
                                                    subDrillingRowFound = true;
                                                    drillingStartTime = objData.getDateTimeValue(ssr, "DATETIME");
                                                    drillingStartRowIndex = ssr;
                                                    isDrillingFound = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                subSubStartRow = ssr + 1;
                                            }
                                        }

                                        if (subDrillingRowFound) break;
                                    }
                                    else
                                    {
                                        subDrillingRowFound = true;
                                        drillingStartTime = objData.getDateTimeValue(j, "DATETIME");
                                        drillingStartRowIndex = j;
                                        isDrillingFound = true;
                                        break;
                                    }
                                }
                            }

                            if (!RUN_DRLG_CHECK)
                            {
                                subDrillingRowFound = true;
                            }

                            if (!subDrillingRowFound)
                            {
                                isDrillingFound = false;
                                break;
                            }
                        }

                        isDrillingFound = true;
                        break;
                    }
                }

                if (isDrillingFound)
                {
                    logger?.Debug("Drilling found. Drilling start time " + drillingStartTime.ToString("dd-MMM-yyyy HH:mm:ss"));
                }
                else
                {
                    logger?.Debug("Drilling start not found ");
                }

                // Now looking backward ...
                for (int i = currentRowIndex; i >= 0; i--)
                {
                    double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);
                    drillingEndTime = objData.getDateTimeValue(i, "DATETIME");
                    drillingEndRowIndex = i;

                    if (drillingEndTime < lowerLimit)
                    {
                        break;
                    }

                    if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                    {
                        // Add 1 minute and find out if it's drilling continuous ...
                        bool isContinuousDrilling = true;
                        DateTime dtDrillingEndTimeSearchLimit = drillingEndTime.AddSeconds(-60);

                        for (int j = i; j >= 0; j--)
                        {
                            DateTime lnCurrentDateTime = objData.getDateTimeValue(j, "DATETIME");

                            if (lnCurrentDateTime < lowerLimit)
                            {
                                break;
                            }

                            if (lnCurrentDateTime <= dtDrillingEndTimeSearchLimit)
                            {
                                break;
                            }

                            double lnSubRigState = objData.getDoubleValue(j, "RIG_STATE", -1);

                            if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                            {
                            }
                            else
                            {
                                isContinuousDrilling = false;
                                break;
                            }
                        }

                        if (!RUN_DRLG_CHECK)
                        {
                            isContinuousDrilling = true;
                        }

                        if (isContinuousDrilling)
                        {
                            isNextDrillingFound = true;
                            break;
                        }
                    }
                }

                if (isNextDrillingFound)
                {
                    logger?.Debug("Drilling after connection found");
                }
                else
                {
                    logger?.Debug("Drilling after connection not found ");
                }

                if ((!isDrillingFound) || (!isNextDrillingFound))
                {
                    // Find the next drilling point
                    for (int i = currentRowIndex; i < objData.RecordCount; i++)
                    {
                        double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                        if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                        {
                            forwardPointer = i;
                            return;
                        }
                    }

                    forwardPointer = objData.RecordCount - 1;
                    return;
                }
                else
                {
                    logger?.Debug("Valid drilling connection boundaries detected ");
                    forwardPointer = drillingStartRowIndex;
                }

                // Go forward from drillingEndRowIndex to find the slip start time
                if (RUN_HKLD_CHECK)
                {
                    for (int i = drillingEndRowIndex + 1; i <= objData.RecordCount - 1; i++)
                    {
                        double lnHkld = objData.getDoubleValue(i, "HKLD");
                        double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                        if (lnHkld <= lnHKldCutOff || lnRigState == 2d)
                        {
                            slipsStartRowIndex = i;
                            slipsStartTime = objData.getDateTimeValue(i, "DATETIME");
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = drillingEndRowIndex + 1; i <= objData.RecordCount - 1; i++)
                    {
                        double lnHkld = objData.getDoubleValue(i, "HKLD");
                        double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                        if (lnHkld <= lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                        {
                            slipsStartRowIndex = i;
                            slipsStartTime = objData.getDateTimeValue(i, "DATETIME");
                            break;
                        }
                    }
                }

                if (slipsStartRowIndex > 0)
                {
                    logger?.Debug("Slip start found ");

                    // From the beginning of the slips, find the end time of slips
                    if (RUN_HKLD_CHECK)
                    {
                        bool slipEndFound = false;

                        for (int i = slipsStartRowIndex + 1; i < objData.RecordCount; i++)
                        {
                            double lnHkld = objData.getDoubleValue(i, "HKLD");

                            if (lnHkld > lnHKldCutOff)
                            {
                                slipsEndRowIndex = i;
                                slipsEndTime = objData.getDateTimeValue(i, "DATETIME");
                                slipEndFound = true;
                                break;
                            }
                        }

                        if (!slipEndFound)
                        {
                            // Try to find the end by looking at the rig state
                            for (int i = slipsStartRowIndex + 1; i < objData.RecordCount; i++)
                            {
                                double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                                if (lnRigState != 2d && lnRigState != 11d && lnRigState != 12d && lnRigState != 13d && lnRigState != 14d)
                                {
                                    slipsEndRowIndex = i;
                                    slipsEndTime = objData.getDateTimeValue(i, "DATETIME");
                                    slipEndFound = true;
                                    break;
                                }
                            }
                        }

                        if (!slipEndFound)
                        {
                            slipsEndRowIndex = slipsStartRowIndex;
                            slipsEndTime = objData.getDateTimeValue(slipsEndRowIndex, "DATETIME");
                        }
                    }
                    else
                    {
                        for (int i = slipsStartRowIndex + 1; i < objData.RecordCount; i++)
                        {
                            double lnHkld = objData.getDoubleValue(i, "HKLD");
                            double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                            if (lnHkld > lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                            {
                                slipsEndRowIndex = i;
                                slipsEndTime = objData.getDateTimeValue(i, "DATETIME");
                                break;
                            }
                        }
                    }

                    int searchLimitNext = objData.RecordCount - 1;
                    DateTime dtSearchLimitNext = objData.getDateTimeValue(slipsEndRowIndex, "DATETIME").AddHours(5);

                    for (int nd = slipsEndRowIndex; nd <= objData.RecordCount - 1; nd++)
                    {
                        if (objData.getDateTimeValue(nd, "DATETIME") >= dtSearchLimitNext)
                        {
                            searchLimitNext = nd;
                            break;
                        }
                    }

                    if (searchLimitNext <= 0)
                    {
                        searchLimitNext = objData.RecordCount - 1;
                    }

                    // Find the next bottom ...
                    bool nextDrillingFound = false;

                    for (int i = slipsEndRowIndex + 1; i <= searchLimitNext; i++)
                    {
                        double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                        if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                        {
                            DateTime lnEndLimitNextDrillingTime = drillingStartTime.AddSeconds(60);
                            bool isContinuousDrilling = true;

                            for (int j = i; j <= objData.RecordCount - 1; j++)
                            {
                                DateTime lnCurrentDateTime = objData.getDateTimeValue(j, "DATETIME");

                                if (lnCurrentDateTime >= upperLimit || lnCurrentDateTime >= lnEndLimitNextDrillingTime)
                                {
                                    break;
                                }

                                double lnSubRigState = objData.getDoubleValue(j, "RIG_STATE", -1);

                                if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                                {
                                }
                                else
                                {
                                    isContinuousDrilling = false;
                                    break;
                                }
                            }

                            if (!RUN_DRLG_CHECK)
                            {
                                isContinuousDrilling = true;
                            }

                            if (isContinuousDrilling)
                            {
                                nextDrillingFound = true;
                                slipsToBottomRowIndex = i - 1;
                                slipsToBottomTime = objData.getDateTimeValue(i - 1, "DATETIME");
                                break;
                            }
                        }
                    }

                    if (slipsToBottomTime == DateTime.MinValue)
                    {
                        slipsToBottomTime = slipsEndTime;
                    }

                    bool foundHigherHkld = false;

                    if (RUN_HKLD_CHECK)
                    {
                        for (int i = slipsEndRowIndex; i <= slipsToBottomRowIndex; i++)
                        {
                            double lnHkld = objData.getDoubleValue(i, "HKLD");

                            if (lnHkld < lnHKldCutOff)
                            {
                                DateTime lnSubDate = objData.getDateTimeValue(i, "DATETIME");

                                if (lnSubDate > slipsEndTime)
                                {
                                    slipsEndRowIndex = i;
                                    slipsEndTime = objData.getDateTimeValue(i, "DATETIME");
                                }
                            }
                            else
                            {
                                foundHigherHkld = true;
                            }
                        }
                    }
                    else
                    {
                        for (int i = slipsEndRowIndex; i <= slipsToBottomRowIndex; i++)
                        {
                            double lnHkld = objData.getDoubleValue(i, "HKLD");
                            double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);

                            if (lnHkld < lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                            {
                                DateTime lnSubDate = objData.getDateTimeValue(i, "DATETIME");

                                if (lnSubDate > slipsEndTime)
                                {
                                    slipsEndRowIndex = i;
                                    slipsEndTime = objData.getDateTimeValue(i, "DATETIME");
                                }
                            }
                            else
                            {
                                foundHigherHkld = true;
                            }
                        }
                    }

                    double totalTime = Math.Abs((drillingEndTime - slipsToBottomTime).TotalSeconds);
                    double slot1Time = Math.Abs((drillingEndTime - slipsStartTime).TotalSeconds);
                    double slot2Time = Math.Abs((slipsStartTime - slipsEndTime).TotalSeconds);
                    double slot3Time = Math.Abs((slipsEndTime - slipsToBottomTime).TotalSeconds);

                    logger?.Debug($"Slot 1 time: {slot1Time:F1}s, Slot 2 time: {slot2Time:F1}s, Slot 3 time: {slot3Time:F1}s, FoundHigherHkld: {foundHigherHkld}, NextDrillingFound: {nextDrillingFound}");

                    if (slot1Time >= 5 && slot2Time >= 60 &&
                        ((slot3Time >= 5 && nextDrillingFound) || (foundHigherHkld && nextDrillingFound)) &&
                        (slot3Time <= 10800) && (slot2Time < 14400))
                    {
                        logger?.Debug("Drilling Connection Detected ");

                        var objPoint = new ConnectionLogPoint
                        {
                            BottomToSlips = slot1Time,
                            SlipsToSlips = slot2Time,
                            SlipsToBottom = slot3Time,
                            TotalSeconds = slot1Time + slot2Time + slot3Time,
                            FromDate = drillingEndTime,
                            ToDate = slipsToBottomTime,
                            BottomToSlipsSeconds = slot1Time,
                            SlipsToSlipsSeconds = slot2Time,
                            SlipsToBottomSeconds = slot3Time,
                            Depth = lnHoleDepth,
                            DayNightStatus = determineDayOrNight(drillingEndTime)
                        };

                        // Populate rig states
                        for (int i = drillingEndRowIndex - 1; i <= slipsStartRowIndex && i >= 0 && i < objData.RecordCount; i++)
                        {
                            double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);
                            double lnTime = objData.getDoubleValue(i, "TIME_DURATION", 0);

                            if (lnRigState != 0d && lnRigState != 1d && lnRigState != 19d)
                            {
                                if (objPoint.BottomToSlipsRigStates.ContainsKey(lnRigState))
                                    objPoint.BottomToSlipsRigStates[lnRigState] += lnTime;
                                else
                                    objPoint.BottomToSlipsRigStates.Add(lnRigState, lnTime);
                            }
                        }

                        for (int i = slipsStartRowIndex; i <= slipsEndRowIndex && i >= 0 && i < objData.RecordCount; i++)
                        {
                            double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);
                            double lnTime = objData.getDoubleValue(i, "TIME_DURATION", 0);

                            if (lnRigState != 0d && lnRigState != 1d && lnRigState != 19d)
                            {
                                if (objPoint.SlipsToSlipsRigStates.ContainsKey(lnRigState))
                                    objPoint.SlipsToSlipsRigStates[lnRigState] += lnTime;
                                else
                                    objPoint.SlipsToSlipsRigStates.Add(lnRigState, lnTime);
                            }
                        }

                        for (int i = slipsEndRowIndex; i <= slipsToBottomRowIndex && i >= 0 && i < objData.RecordCount; i++)
                        {
                            double lnRigState = objData.getDoubleValue(i, "RIG_STATE", -1);
                            double lnTime = objData.getDoubleValue(i, "TIME_DURATION", 0);

                            if (lnRigState != 0d && lnRigState != 1d && lnRigState != 19d)
                            {
                                if (objPoint.SlipsToBottomRigStates.ContainsKey(lnRigState))
                                    objPoint.SlipsToBottomRigStates[lnRigState] += lnTime;
                                else
                                    objPoint.SlipsToBottomRigStates.Add(lnRigState, lnTime);
                            }
                        }

                        objPoint.NOVStatus = ConnectionLogPoint.cnNOVOSStatus.NotApplicable;
                        connectionPoints.Add(connectionPoints.Count + 1, objPoint);
                    }
                    else
                    {
                        logger?.Debug("Connection did not pass criteria");
                    }

                    depthCounter++;
                }
                else
                {
                    logger?.Debug("Slip start not found ");
                }
            }
            catch (Exception ex)
            {
                logger?.Error("Error in connection detection: " + ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }

        public void detectAndAddPointsRegular(DataTable objData, int currentRowIndex, ref int forwardPointer)
        {
            var vt = new VirtualTable.VirtualTable(objData);
            detectAndAddPoints(vt, currentRowIndex, ref forwardPointer);
        }
    }
}
