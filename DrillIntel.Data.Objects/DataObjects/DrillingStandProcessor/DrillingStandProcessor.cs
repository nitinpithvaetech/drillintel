using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor
{
    #region Logger Abstraction

    public interface IDrillingStandLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public class DefaultDrillingStandLogger : IDrillingStandLogger
    {
        private readonly Action<string>? _logAction;
        private readonly DrillIntel.Data.Objects.Logger.ILog _fileLogger;

        public DefaultDrillingStandLogger(Action<string>? logAction = null)
        {
            _logAction = logAction;
            _fileLogger = new DrillIntel.Data.Objects.Logger.DynamicLogger("DrillingStandProcessor");
        }

        public void Debug(string message)
        {
            _logAction?.Invoke($"[DEBUG] {message}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
            _fileLogger.Debug(message);
        }

        public void Info(string message)
        {
            _logAction?.Invoke($"[INFO] {message}");
            System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
            _fileLogger.Info(message);
        }

        public void Warn(string message)
        {
            _logAction?.Invoke($"[WARN] {message}");
            System.Diagnostics.Debug.WriteLine($"[WARN] {message}");
            _fileLogger.Warn(message);
        }

        public void Error(string message)
        {
            _logAction?.Invoke($"[ERROR] {message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
            _fileLogger.Error(message);
        }
    }

    #endregion

    public class DrillingStandProcessor
    {
        // === External Context Dependencies =====
        public IDataServiceDIntel? objLocalConn { get; set; }
        public TimeLog? __localTimeLog { get; set; } = null;
        public string WellID { get; set; } = "";

        // =======================================
        public Dictionary<int, StandPoint> connectionPoints { get; set; } = new Dictionary<int, StandPoint>();

        public string DayTimeHoursFrom { get; set; } = "12:00";
        public string DayTimeHoursTo { get; set; } = "20:00";
        public bool OverwriteExisting { get; set; } = false;
        public bool ShowOffsetWells { get; set; } = true;
        public double DepthComparisonWindow { get; set; } = 50.0;
        public string LastError { get; set; } = "";

        public SystemSettings objSystemSettings { get; set; } = new SystemSettings();
        public IDrillingStandLogger logger { get; set; } = new DefaultDrillingStandLogger();

        public static void EnsureTables(IDataServiceDIntel db)
        {
            if (db == null) return;
            KPIDrlgStand.EnsureTableExists(db);
        }

        public StandPoint.cnDayNightTime determineDayOrNight(DateTime paramDate)
        {
            try
            {
                DateTime localDate = paramDate.ToLocalTime();
                string dayTime = !string.IsNullOrWhiteSpace(DayTimeHoursFrom) ? DayTimeHoursFrom.Trim() : "12:00";
                if (dayTime.Count(c => c == ':') == 1)
                {
                    dayTime += ":00";
                }

                DateTime dayTimeFrom;
                if (!DateTime.TryParse(localDate.ToString("yyyy-MM-dd") + " " + dayTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out dayTimeFrom))
                {
                    if (!DateTime.TryParse(localDate.ToString("dd-MMM-yyyy") + " " + dayTime, out dayTimeFrom))
                    {
                        dayTimeFrom = new DateTime(localDate.Year, localDate.Month, localDate.Day, 12, 0, 0);
                    }
                }

                DateTime dayTimeTo = dayTimeFrom.AddHours(11).AddMinutes(59).AddSeconds(59);
                return (localDate >= dayTimeFrom && localDate <= dayTimeTo)
                    ? StandPoint.cnDayNightTime.DayTime
                    : StandPoint.cnDayNightTime.NightTime;
            }
            catch (Exception ex)
            {
                logger?.Error("Error in determineDayOrNight: " + ex.Message);
                return StandPoint.cnDayNightTime.DayTime;
            }
        }

        public double calculateROP(TimeLog paramTimeLog, DateTime paramFromDate, DateTime paramToDate)
        {
            return calculateROPInternal(paramTimeLog, paramFromDate, paramToDate, "0,1,19");
        }

        public double calculateRotaryROP(TimeLog paramTimeLog, DateTime paramFromDate, DateTime paramToDate)
        {
            return calculateROPInternal(paramTimeLog, paramFromDate, paramToDate, "0");
        }

        public double calculateSlideROP(TimeLog paramTimeLog, DateTime paramFromDate, DateTime paramToDate)
        {
            return calculateROPInternal(paramTimeLog, paramFromDate, paramToDate, "1,19");
        }

        private double calculateROPInternal(TimeLog paramTimeLog, DateTime fromDate, DateTime toDate, string rigStates)
        {
            try
            {
                if (objLocalConn == null || paramTimeLog == null) return 0.0;
                string dataTableName = paramTimeLog.getDataTableName(objLocalConn);
                if (string.IsNullOrWhiteSpace(dataTableName) || !objLocalConn.TableExists(dataTableName)) return 0.0;

                string dtWhere = GetDateTimeWhereExpression(objLocalConn, dataTableName);
                string fromStr = fromDate.ToString("dd-MMM-yyyy HH:mm:ss");
                string toStr = toDate.ToString("dd-MMM-yyyy HH:mm:ss");

                string durCol = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "TIME_DURATION") ? "[TIME_DURATION]" : "5.0";
                string timeSql = $"SELECT SUM({durCol}) FROM [{dataTableName}] WHERE {dtWhere} >= '{fromStr}' AND {dtWhere} <= '{toStr}' AND RIG_STATE IN ({rigStates});";
                var timeVal = objLocalConn.GetValue(timeSql);
                double sumDuration = 0.0;
                if (timeVal != null && double.TryParse(timeVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDur))
                {
                    sumDuration = parsedDur;
                }

                double sumDepth = 0.0;
                bool hasNextDepth = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "NEXT_DEPTH");
                bool hasFootage = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "FOOTAGE");
                bool hasHdth = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "HDTH");

                if (hasNextDepth && hasHdth)
                {
                    string depthSql = $"SELECT SUM([HDTH] - [NEXT_DEPTH]) FROM [{dataTableName}] WHERE {dtWhere} >= '{fromStr}' AND {dtWhere} <= '{toStr}' AND RIG_STATE IN ({rigStates}) AND [NEXT_DEPTH] > 0 AND [HDTH] >= 0;";
                    var dVal = objLocalConn.GetValue(depthSql);
                    if (dVal != null && double.TryParse(dVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pd))
                    {
                        sumDepth = pd;
                    }
                }
                else if (hasFootage)
                {
                    string depthSql = $"SELECT SUM([FOOTAGE]) FROM [{dataTableName}] WHERE {dtWhere} >= '{fromStr}' AND {dtWhere} <= '{toStr}' AND RIG_STATE IN ({rigStates}) AND [FOOTAGE] >= 0;";
                    var dVal = objLocalConn.GetValue(depthSql);
                    if (dVal != null && double.TryParse(dVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pd))
                    {
                        sumDepth = pd;
                    }
                }
                else if (hasHdth)
                {
                    string depthSql = $"SELECT (MAX([HDTH]) - MIN([HDTH])) FROM [{dataTableName}] WHERE {dtWhere} >= '{fromStr}' AND {dtWhere} <= '{toStr}' AND RIG_STATE IN ({rigStates}) AND [HDTH] >= 0;";
                    var dVal = objLocalConn.GetValue(depthSql);
                    if (dVal != null && double.TryParse(dVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double pd))
                    {
                        sumDepth = pd;
                    }
                }

                if (sumDuration > 0.0 && sumDepth > 0.0)
                {
                    return sumDepth / (sumDuration / 3600.0); // ft/hour
                }
                return 0.0;
            }
            catch (Exception ex)
            {
                logger?.Debug($"calculateROPInternal error: {ex.Message}");
                return 0.0;
            }
        }

        private void populateRigStates(ref StandPoint point, TimeLog paramTimeLog)
        {
            try
            {
                if (objLocalConn == null || paramTimeLog == null) return;
                string dataTableName = paramTimeLog.getDataTableName(objLocalConn);
                if (string.IsNullOrWhiteSpace(dataTableName) || !objLocalConn.TableExists(dataTableName)) return;

                point.RigStates.Clear();
                string dtWhere = GetDateTimeWhereExpression(objLocalConn, dataTableName);
                string fromStr = point.FromDate.ToString("dd-MMM-yyyy HH:mm:ss");
                string toStr = point.ToDate.ToString("dd-MMM-yyyy HH:mm:ss");
                string durCol = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "TIME_DURATION") ? "[TIME_DURATION]" : "5.0";

                string sql = $"SELECT RIG_STATE, SUM({durCol}) AS TOTAL_DUR FROM [{dataTableName}] WHERE {dtWhere} >= '{fromStr}' AND {dtWhere} <= '{toStr}' AND RIG_STATE IS NOT NULL GROUP BY RIG_STATE;";
                DataTable dt = objLocalConn.GetTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (double.TryParse(row["RIG_STATE"]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rs) &&
                            double.TryParse(row["TOTAL_DUR"]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dur))
                        {
                            point.RigStates[rs] = dur;
                        }
                    }
                }
                int a = 1;
            }
            catch (Exception ex)
            {
                logger?.Debug($"populateRigStates error: {ex.Message}");
            }
        }

        public void ProcessPoints(
            DateTime fromDate,
            DateTime toDate,
            out DateTime lastDrillingConnEndDate,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            lastDrillingConnEndDate = DateTime.MinValue;

            try
            {
                if (objLocalConn == null)
                {
                    LastError = "Database connection is not available.";
                    logger?.Error(LastError);
                    return;
                }

                EnsureTables(objLocalConn);

                if (__localTimeLog == null)
                {
                    LastError = "TimeLog object is not specified.";
                    logger?.Error(LastError);
                    return;
                }

                string dataTableName = __localTimeLog.getDataTableName(objLocalConn);
                if (string.IsNullOrWhiteSpace(dataTableName) || !objLocalConn.TableExists(dataTableName))
                {
                    LastError = "TimeLog data table does not exist.";
                    logger?.Error(LastError);
                    return;
                }

                logger.Debug("Processing Drilling Stands for WellID: " + WellID);
                connectionPoints.Clear();

                // Retrieve all drilling connections
                if (!objLocalConn.TableExists("VMX_AKPI_DRLG_CONNECTIONS"))
                {
                    logger.Warn("Table VMX_AKPI_DRLG_CONNECTIONS does not exist. No stands can be detected.");
                    return;
                }

                string sqlConns = $"SELECT DEPTH, FROM_DATE, TO_DATE FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{WellID.Replace("'", "''")}' ORDER BY FROM_DATE ASC;";
                DataTable objData = objLocalConn.GetTable(sqlConns);

                if (objData == null || objData.Rows.Count <= 0)
                {
                    logger.Debug("No drilling connections found for WellID: " + WellID);
                    return;
                }

                logger.Debug($"({objData.Rows.Count}) drilling connections found to process stands from");

                string depthCol = RigStateService.IsColumnAvailable(objLocalConn, dataTableName, "DEPTH") ? "[DEPTH]" : "[HDTH]";
                string dtCol = RigStateService.GetDateTimeColumnSql(objLocalConn, dataTableName);

                for (int i = 1; i < objData.Rows.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (i % 20 == 0)
                    {
                        double pct = Math.Round((i * 100.0) / objData.Rows.Count, 1);
                        progress?.Report(pct);
                    }

                    DataRow curRow = objData.Rows[i];
                    DataRow prevRow = objData.Rows[i - 1];

                    double curDepth = 0.0;
                    if (curRow["DEPTH"] != DBNull.Value && curRow["DEPTH"] != null)
                    {
                        double.TryParse(curRow["DEPTH"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out curDepth);
                    }

                    DateTime connStartDate = ParseDate(curRow["FROM_DATE"]);
                    DateTime connEndDate = ParseDate(curRow["TO_DATE"]);

                    double prevDepth = 0.0;
                    if (prevRow["DEPTH"] != DBNull.Value && prevRow["DEPTH"] != null)
                    {
                        double.TryParse(prevRow["DEPTH"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out prevDepth);
                    }

                    DateTime prevConnStartDate = ParseDate(prevRow["FROM_DATE"]);
                    DateTime prevConnEndDate = ParseDate(prevRow["TO_DATE"]);

                    double standDepth = prevDepth;
                    DateTime standStartDate = prevConnEndDate;
                    DateTime standEndDate = connStartDate;

                    // Skip this stand if there is overlap or invalid window
                    if (standEndDate <= standStartDate)
                    {
                        continue;
                    }

                    var objPoint = new StandPoint
                    {
                        Depth = standDepth,
                        FromDate = standStartDate,
                        ToDate = standEndDate,
                        DayNightStatus = determineDayOrNight(standStartDate)
                    };

                    objPoint.ROP = calculateROP(__localTimeLog, standStartDate, standEndDate);
                    objPoint.RotaryROP = calculateRotaryROP(__localTimeLog, standStartDate, standEndDate);
                    objPoint.SlideROP = calculateSlideROP(__localTimeLog, standStartDate, standEndDate);

                    logger.Debug($"Drilling Stand Detected: Depth {standDepth}, Date {standStartDate:dd-MMM-yyyy HH:mm:ss} - {standEndDate:dd-MMM-yyyy HH:mm:ss}, ROP {objPoint.ROP:F2}");

                    if (objPoint.ROP > 5000)
                    {
                        logger.Debug("Rejecting this stand as ROP is more than 5000");
                        continue;
                    }

                    // POOH Check: bit depth shouldn't go up by more than 90 * 3 stands (270)
                    string fromStr = standStartDate.ToString("dd-MMM-yyyy HH:mm:ss");
                    string toStr = standEndDate.ToString("dd-MMM-yyyy HH:mm:ss");
                    string minDepthSql = $"SELECT MIN({depthCol}) FROM [{dataTableName}] WHERE {dtCol} >= '{fromStr}' AND {dtCol} <= '{toStr}' AND {depthCol} >= 0;";
                    var minDepthVal = objLocalConn.GetValue(minDepthSql);

                    if (minDepthVal != null && double.TryParse(minDepthVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double minDepthInStand))
                    {
                        if (standDepth - minDepthInStand > 270d)
                        {
                            logger.Debug("POOH Detected between these two drilling connections. Adjusting end date.");
                            double thresholdDepth = standDepth - 270d;
                            string minDepthDateSql = $"SELECT {dtCol} FROM [{dataTableName}] WHERE {dtCol} >= '{fromStr}' AND {dtCol} <= '{toStr}' AND {depthCol} <= {thresholdDepth.ToString(CultureInfo.InvariantCulture)} ORDER BY {dtCol} ASC LIMIT 1;";
                            var minDateVal = objLocalConn.GetValue(minDepthDateSql);

                            if (minDateVal != null && DateTime.TryParse(minDateVal.ToString(), out DateTime minDepthDate))
                            {
                                string lastDrillSql = $"SELECT {dtCol} FROM [{dataTableName}] WHERE {dtCol} >= '{fromStr}' AND {dtCol} <= '{minDepthDate:dd-MMM-yyyy HH:mm:ss}' AND RIG_STATE IN (0, 1, 19) ORDER BY {dtCol} DESC LIMIT 1;";
                                var lastDrillVal = objLocalConn.GetValue(lastDrillSql);
                                if (lastDrillVal != null && DateTime.TryParse(lastDrillVal.ToString(), out DateTime lastDrillingDate))
                                {
                                    objPoint.ToDate = lastDrillingDate;
                                }
                            }
                        }
                    }

                    populateRigStates(ref objPoint, __localTimeLog);
                    connectionPoints.Add(connectionPoints.Count + 1, objPoint.getCopy());
                }

                if (objData.Rows.Count > 0)
                {
                    lastDrillingConnEndDate = ParseDate(objData.Rows[objData.Rows.Count - 1]["TO_DATE"]);
                }

                logger.Debug($"Total ({connectionPoints.Count}) drilling stands detected");

                // Save stands to database
                saveStands();

                logger.Debug("Process completed successfully.");
            }
            catch (OperationCanceledException)
            {
                logger?.Warn("ProcessPoints cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                logger?.Error("Caught exception while detecting drilling stands: " + ex.Message + " " + ex.StackTrace);
            }
        }

        private void saveStands()
        {
            try
            {
                if (objLocalConn == null) return;

                if (OverwriteExisting)
                {
                    logger.Debug("Removing existing stands from database for WellID: " + WellID);
                    string clearErr = "";
                    KPIDrlgStand.clearStandsFromDB(objLocalConn, WellID, ref clearErr);
                }

                long startingEntryID = 0;
                try
                {
                    var maxVal = objLocalConn.GetValue($"SELECT MAX(ENTRY_ID) FROM VMX_AKPI_DRLG_STAND WHERE WELL_ID='{WellID.Replace("'", "''")}';");
                    if (maxVal != null && long.TryParse(maxVal.ToString(), out long m))
                    {
                        startingEntryID = m;
                    }
                }
                catch { }

                logger.Debug("Saving drilling stands to the database...");

                string saveErr = "";
                foreach (StandPoint objItem in connectionPoints.Values)
                {
                    KPIDrlgStand? objStand = KPIDrlgStand.createKPIDrlgStand(WellID, objItem, objLocalConn);
                    if (objStand != null)
                    {
                        KPIDrlgStand.addToDatabase(objLocalConn, objStand, ref startingEntryID, ref saveErr);
                    }
                }

                logger.Debug($"Saved {connectionPoints.Count} drilling stands.");
            }
            catch (Exception ex)
            {
                logger?.Error("Caught exception in saveStands: " + ex.Message);
            }
        }

        private static DateTime ParseDate(object? dateVal)
        {
            if (dateVal == null || dateVal == DBNull.Value) return DateTime.MinValue;
            string s = dateVal.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                return dt;
            if (DateTime.TryParse(s, out dt))
                return dt;

            return DateTime.MinValue;
        }
        private static string GetDateTimeWhereExpression(IDataServiceDIntel db, string tableName)
        {
            if (RigStateService.IsColumnAvailable(db, tableName, "DATETIME"))
                return "[DATETIME]";
            if (RigStateService.IsColumnAvailable(db, tableName, "DATE") && RigStateService.IsColumnAvailable(db, tableName, "TIME"))
                return "([DATE] || ' ' || [TIME])";
            if (RigStateService.IsColumnAvailable(db, tableName, "DATE"))
                return "[DATE]";
            if (RigStateService.IsColumnAvailable(db, tableName, "DATE_TIME"))
                return "[DATE_TIME]";
            return "[DATETIME]";
        }
    }
}

