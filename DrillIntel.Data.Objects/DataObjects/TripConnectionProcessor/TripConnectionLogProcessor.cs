using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.Data.Objects.DataObjects.Services;

namespace DrillIntel.Data.Objects.DataObjects.TripConnectionProcessor
{
    public class TripConnectionLogProcessor
    {
        // Original VB created and opened its own SQL Server DataService. New: the SQLite
        // IDataServiceDIntel connection is supplied by the caller and remains caller-owned.
        public IDataServiceDIntel? objLocalConn { get; set; }
        public rigState? objRigState { get; set; }
        public Well? objWell { get; set; }

        public Dictionary<int, TripConnectionPoint> connectionPoints { get; } = new();
        public int ProcessStatus { get; private set; }
        public double MIN_OFFBOTTOM_DEPTH { get; set; } = 100;
        public double MAX_TIME { get; set; } = 5 * 60;
        public double MIN_TIME { get; set; } = 1 * 60;
        public bool ApplyDurationLimits { get; set; }
        public DateTime DayTimeHoursFrom { get; set; }
        public DateTime DayTimeHoursTo { get; set; }
        public string LastError { get; private set; } = string.Empty;

        public void ProcessPoints(TimeLog? objTimeLog, DateTime fromDate, DateTime toDate)
        {
            ProcessStatus = 1;
            LastError = string.Empty;
            connectionPoints.Clear();

            try
            {
                if (objTimeLog == null)
                {
                    LastError = "TimeLog object is null.";
                    return;
                }
                if (objLocalConn == null || !objLocalConn.IsConnectionOpen())
                {
                    LastError = "An open SQLite DataService connection is required.";
                    return;
                }

                string tableName = objTimeLog.getDataTableName(objLocalConn);
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    LastError = "Data table name for TimeLog could not be determined.";
                    return;
                }

                // Original VB compared datetime values through SQL Server's datetime type.
                // New: DrillIntel stores SQLite DATETIME values as text (commonly
                // "dd-MMM-yyyy HH:mm:ss"), so SQL string bounds using ISO dates can exclude
                // every row. Read in insertion order, parse timestamps, and apply true date bounds.
                string quotedTable = "[" + tableName.Replace("]", "]]", StringComparison.Ordinal) + "]";
                string dateColumn = RigStateService.GetDateTimeColumnSql(objLocalConn, tableName);
                string depthColumn = RigStateService.IsColumnAvailable(objLocalConn, tableName, "DEPTH") ? "[DEPTH]" : "0.0 AS [DEPTH]";
                string holeDepthColumn = RigStateService.IsColumnAvailable(objLocalConn, tableName, "HDTH") ? "[HDTH]" : depthColumn.Contains(" AS ", StringComparison.OrdinalIgnoreCase) ? "0.0 AS [HDTH]" : "[DEPTH] AS [HDTH]";
                string hookloadColumn = RigStateService.IsColumnAvailable(objLocalConn, tableName, "HKLD") ? "[HKLD]" : "0.0 AS [HKLD]";
                bool hasRigState = RigStateService.IsColumnAvailable(objLocalConn, tableName, "RIG_STATE");
                string rigStateColumn = hasRigState ? "[RIG_STATE]" : "0 AS [RIG_STATE]";
                string durationColumn = RigStateService.IsColumnAvailable(objLocalConn, tableName, "TIME_DURATION") ? "[TIME_DURATION]" : "0.0 AS [TIME_DURATION]";
                string rigStateFilter = hasRigState ? "WHERE [RIG_STATE] IS NOT NULL " : string.Empty;
                DataTable sourceData = objLocalConn.GetTable(
                    $"SELECT {dateColumn}, {depthColumn}, {holeDepthColumn}, {hookloadColumn}, {rigStateColumn}, {durationColumn} " +
                    $"FROM {quotedTable} {rigStateFilter}ORDER BY rowid");
                DataTable data = new();
                data.Columns.Add("DATETIME", typeof(DateTime));
                data.Columns.Add("DEPTH", typeof(double));
                data.Columns.Add("HDTH", typeof(double));
                data.Columns.Add("HKLD", typeof(double));
                data.Columns.Add("RIG_STATE", typeof(int));
                data.Columns.Add("TIME_DURATION", typeof(double));
                foreach (DataRow sourceRow in sourceData.Rows)
                {
                    DateTime rowDate = RigStateService.ParseDate(sourceRow["DATETIME"]);
                    if (rowDate == DateTime.MinValue || rowDate < fromDate || rowDate > toDate)
                        continue;

                    data.Rows.Add(
                        rowDate,
                        DataService.checkNull(sourceRow["DEPTH"], 0d),
                        DataService.checkNull(sourceRow["HDTH"], 0d),
                        DataService.checkNull(sourceRow["HKLD"], 0d),
                        DataService.checkNull(sourceRow["RIG_STATE"], 0),
                        DataService.checkNull(sourceRow["TIME_DURATION"], 0d));
                }

                for (int i = 0; i < data.Rows.Count; i++)
                {
                    int rigState = DataService.checkNull(data.Rows[i]["RIG_STATE"], 0);
                    double hookload = DataService.checkNull(data.Rows[i]["HKLD"], 0d);
                    if (rigState == 2 || hookload < (objRigState?.HookloadCutOff ?? 90d))
                    {
                        int forwardPointer = i;
                        detectAndAddPoints(data, i, ref forwardPointer);
                        i = forwardPointer;
                    }
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            finally
            {
                ProcessStatus = 0;
            }
        }

        public void detectAndAddPoints(DataTable objData, int currentRowIndex, ref int forwardPointer)
        {
            try
            {
                if (objData == null || currentRowIndex < 0 || currentRowIndex >= objData.Rows.Count)
                {
                    forwardPointer = currentRowIndex;
                    return;
                }

                double hookloadCutoff = objRigState?.HookloadCutOff ?? 90d;
                DataRow current = objData.Rows[currentRowIndex];
                double inSlipsDepth = DataService.checkNull(current["DEPTH"], 0d);
                double holeDepth = DataService.checkNull(current["HDTH"], 0d);
                DateTime slipStartDate = DataService.checkNull(current["DATETIME"], DateTime.MinValue);
                int startRigState = DataService.checkNull(current["RIG_STATE"], 0);
                int slipEndRowIndex = -1;
                DateTime slipEndDate = DateTime.MinValue;

                for (int i = currentRowIndex; i < objData.Rows.Count; i++)
                {
                    int rigState = DataService.checkNull(objData.Rows[i]["RIG_STATE"], 0);
                    double hookload = DataService.checkNull(objData.Rows[i]["HKLD"], 0d);
                    // Original VB scanned until hookload exceeded the cutoff, even after
                    // leaving In Slips. New: end an In Slips interval at its rig-state edge.
                    if (i > currentRowIndex && startRigState == 2 && rigState != 2)
                    {
                        slipEndRowIndex = i;
                        slipEndDate = DataService.checkNull(objData.Rows[i]["DATETIME"], DateTime.MinValue);
                        break;
                    }
                    if (hookload > hookloadCutoff)
                    {
                        slipEndRowIndex = i;
                        slipEndDate = DataService.checkNull(objData.Rows[i]["DATETIME"], DateTime.MinValue);
                        break;
                    }
                }

                if (slipEndRowIndex < 0)
                {
                    forwardPointer = objData.Rows.Count - 1;
                    return;
                }

                forwardPointer = slipEndRowIndex + 1;
                if (Math.Abs(holeDepth - inSlipsDepth) < MIN_OFFBOTTOM_DEPTH)
                    return;

                double elapsedSeconds = Math.Abs((slipEndDate - slipStartDate).TotalSeconds);
                // Original VB declared MIN_TIME and MAX_TIME but did not filter the measured
                // duration. New: apply those limits only when explicitly enabled in chart settings.
                if (ApplyDurationLimits && (elapsedSeconds < MIN_TIME || elapsedSeconds > MAX_TIME))
                    return;

                var point = new TripConnectionPoint
                {
                    Depth = inSlipsDepth,
                    SlipsToSlips = elapsedSeconds,
                    SlipsToSlipsSeconds = elapsedSeconds,
                    TotalSeconds = elapsedSeconds,
                    DayNightStatus = determineDayOrNight(slipStartDate),
                    FromDate = slipStartDate,
                    ToDate = slipEndDate
                };

                for (int i = currentRowIndex; i <= slipEndRowIndex; i++)
                {
                    DataRow row = objData.Rows[i];
                    int state = DataService.checkNull(row["RIG_STATE"], 0);
                    double duration = DataService.checkNull(row["TIME_DURATION"], 0d);
                    if (state != 0 && state != 1 && state != 19)
                    {
                        point.SlipsToSlipsRigStates[state] = point.SlipsToSlipsRigStates.TryGetValue(state, out double existing)
                            ? existing + duration
                            : duration;
                    }
                }

                connectionPoints.Add(connectionPoints.Count + 1, point);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        public ConnectionLogPoint.cnDayNightTime determineDayOrNight(DateTime paramDate)
        {
            try
            {
                // Original used application-global timezone conversion helpers that are not
                // part of DrillIntel.Data.Objects. New: convert UTC with the well's timezone
                // when configured, otherwise retain the timestamp's local clock value.
                DateTime localDate = paramDate;
                if (objWell?.wellDateFormat == Well.wDateFormatUTC && !string.IsNullOrWhiteSpace(objWell.timeZone))
                {
                    try
                    {
                        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(objWell.timeZone);
                        localDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(paramDate, DateTimeKind.Utc), zone);
                    }
                    catch { localDate = paramDate; }
                }

                DateTime from = new(localDate.Year, localDate.Month, localDate.Day,
                    DayTimeHoursFrom.Hour, DayTimeHoursFrom.Minute, 0);
                DateTime to = new(localDate.Year, localDate.Month, localDate.Day,
                    DayTimeHoursTo.Hour, DayTimeHoursTo.Minute, 0);
                return localDate >= from && localDate <= to
                    ? ConnectionLogPoint.cnDayNightTime.DayTime
                    : ConnectionLogPoint.cnDayNightTime.NightTime;
            }
            catch
            {
                return ConnectionLogPoint.cnDayNightTime.DayTime;
            }
        }
    }
}
