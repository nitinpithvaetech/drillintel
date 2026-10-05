using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;

namespace DrillIntel.Data;

/// <summary>
/// Encapsulates multi-channel log series results for chart rendering.
/// </summary>
public class ChartDataSeriesResult
{
    public enumRTDataSourceType SourceType { get; set; } = enumRTDataSourceType.TimeLog;
    public List<double> IndexValues { get; set; } = new();
    public Dictionary<string, List<double>> ChannelValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> RigStateNumbers { get; set; } = new();
    public List<int> RigStateColors { get; set; } = new();
    public int TotalPointCount => IndexValues.Count;
}

/// <summary>
/// Represents a contiguous time/depth interval classified under a single rig state.
/// </summary>
public class RigStateInterval
{
    public double StartIndex { get; set; }
    public double EndIndex { get; set; }
    public int StateNumber { get; set; }
    public string StateName { get; set; } = string.Empty;
    public int ColorArgb { get; set; }
    public string ColorHex { get; set; } = "#000000";
}

/// <summary>
/// Metadata describing an available curve / channel in a log.
/// </summary>
public class LogChannelMetadata
{
    public string Mnemonic { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public int SampleCount { get; set; }
    public string DataType { get; set; } = "Double";
}

/// <summary>
/// Contract for retrieving high-performance charting data from DrillIntel log repositories.
/// </summary>
public interface IChartDataService
{
    Task<ChartDataSeriesResult> GetTimelogDataAsync(
        string wellId,
        string wellboreId,
        string logId,
        IEnumerable<string> mnemonics,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int maxPoints = 50000,
        CancellationToken cancellationToken = default);

    Task<ChartDataSeriesResult> GetDepthLogDataAsync(
        string wellId,
        string wellboreId,
        string logId,
        IEnumerable<string> mnemonics,
        double? fromDepth = null,
        double? toDepth = null,
        int maxPoints = 50000,
        CancellationToken cancellationToken = default);

    Task<List<RigStateInterval>> GetRigStateIntervalsAsync(
        string logId,
        string? dataTableName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);

    Task<List<LogChannelMetadata>> GetAvailableChannelsAsync(
        string logId,
        enumRTDataSourceType sourceType,
        CancellationToken cancellationToken = default);

    Task<(DateTime MinDate, DateTime MaxDate)?> GetTimeLogDateRangeAsync(
        string logId,
        CancellationToken cancellationToken = default);

    Task<(double MinDepth, double MaxDepth)?> GetDepthLogRangeAsync(
        string logId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service providing fast data querying, decimation, and rig-state interval grouping
/// for TChart and VHTrackConsole graph rendering.
/// </summary>
public class ChartDataService : IChartDataService
{
    private readonly IDataServiceDIntel _dataService;
    private readonly ProjectSession? _session;

    public ChartDataService(IDataServiceDIntel dataService, ProjectSession? session = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _session = session;
    }

    public ChartDataService(ProjectSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (session.IsProjectOpen)
        {
            _dataService = session.GetDataService();
        }
        else
        {
            _dataService = null!;
        }
    }

    /// <summary>
    /// Queries time-log data points for given mnemonics, optionally within a time window [fromDate, toDate].
    /// </summary>
    public async Task<ChartDataSeriesResult> GetTimelogDataAsync(
        string wellId,
        string wellboreId,
        string logId,
        IEnumerable<string> mnemonics,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int maxPoints = 50000,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new ChartDataSeriesResult { SourceType = enumRTDataSourceType.TimeLog };
            var requestedList = mnemonics?.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

            string tableName = ResolveTimeLogTableName(logId);
            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return result;
            }

            var tableColumns = GetTableColumnNames(tableName);
            string timeCol = tableColumns.FirstOrDefault(c => string.Equals(c, "DTIM", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "DATETIME", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "TIMEINDEX", StringComparison.OrdinalIgnoreCase))
                          ?? "";

            if (string.IsNullOrWhiteSpace(timeCol))
            {
                return result;
            }

            var mnemonicToColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var req in requestedList)
            {
                string col = FindMatchingColumn(req, tableColumns);
                if (!string.IsNullOrWhiteSpace(col))
                {
                    mnemonicToColumn[req] = col;
                    result.ChannelValues[req] = new List<double>();
                }
            }

            bool hasRigState = tableColumns.Contains("RIG_STATE", StringComparer.OrdinalIgnoreCase);
            bool hasRigStateColor = tableColumns.Contains("RIG_STATE_COLOR", StringComparer.OrdinalIgnoreCase);

            var distinctCols = mnemonicToColumn.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var selectCols = new List<string> { $"[{timeCol}]" };
            foreach (var col in distinctCols)
            {
                selectCols.Add($"[{col}]");
            }
            if (hasRigState && !distinctCols.Contains("RIG_STATE", StringComparer.OrdinalIgnoreCase)) selectCols.Add("[RIG_STATE]");
            if (hasRigStateColor && !distinctCols.Contains("RIG_STATE_COLOR", StringComparer.OrdinalIgnoreCase)) selectCols.Add("[RIG_STATE_COLOR]");

            var (startRow, endRow) = ResolveTimeRangeToRowIds(tableName, timeCol, fromDate, toDate);
            if (startRow == -1 && endRow == -1)
            {
                return result;
            }

            var whereClauses = new List<string>();
            if (startRow.HasValue)
            {
                whereClauses.Add($"rowid >= {startRow.Value}");
            }
            if (endRow.HasValue)
            {
                whereClauses.Add($"rowid <= {endRow.Value}");
            }

            string sql = $"SELECT {string.Join(", ", selectCols)} FROM [{tableName}]";
            if (whereClauses.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", whereClauses);
            }
            sql += " ORDER BY rowid ASC;";

            cancellationToken.ThrowIfCancellationRequested();

            var colorLookup = new Dictionary<int, int>();
            if (hasRigState)
            {
                var stateSetup = RigStateService.LoadCommonRigStateSetup(_dataService);
                if (stateSetup?.rigStates != null && stateSetup.rigStates.Count > 0)
                {
                    foreach (var item in stateSetup.rigStates.Values)
                    {
                        colorLookup[item.Number] = (int)item.Color;
                    }
                }
                else
                {
                    foreach (var item in RigStateService.DefaultRigStateItems)
                    {
                        colorLookup[item.Number] = item.Color;
                    }
                }
            }

            int estimatedRows = 0;
            if (startRow.HasValue && endRow.HasValue && endRow.Value >= startRow.Value)
            {
                estimatedRows = (int)(endRow.Value - startRow.Value + 1);
            }
            else
            {
                string countSql = $"SELECT COUNT(*) FROM [{tableName}]" + (whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "") + ";";
                object? cntObj = _dataService.GetValue(countSql);
                if (cntObj != null && cntObj != DBNull.Value) int.TryParse(cntObj.ToString(), out estimatedRows);
            }

            if (estimatedRows <= 0)
            {
                return result;
            }

            int step = maxPoints > 0 && estimatedRows > maxPoints ? (int)Math.Ceiling((double)estimatedRows / maxPoints) : 1;
            int capacity = Math.Min(estimatedRows, maxPoints > 0 ? maxPoints + 100 : estimatedRows);

            result.IndexValues.Capacity = capacity;
            foreach (var m in mnemonicToColumn.Keys)
            {
                result.ChannelValues[m].Capacity = capacity;
            }
            if (hasRigState)
            {
                result.RigStateNumbers.Capacity = capacity;
                result.RigStateColors.Capacity = capacity;
            }

            using (var reader = _dataService.ExecuteReader(sql))
            {
                if (reader == null) return result;

                int timeOrdinal = reader.GetOrdinal(timeCol);
                var colOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in mnemonicToColumn)
                {
                    int ord = reader.GetOrdinal(kvp.Value);
                    if (ord >= 0) colOrdinals[kvp.Key] = ord;
                }

                int rigStateOrdinal = -1;
                try { if (hasRigState) rigStateOrdinal = reader.GetOrdinal("RIG_STATE"); } catch { }

                int rigStateColorOrdinal = -1;
                try { if (hasRigStateColor) rigStateColorOrdinal = reader.GetOrdinal("RIG_STATE_COLOR"); } catch { }

                int rowIndex = 0;
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (step > 1 && (rowIndex % step != 0))
                    {
                        rowIndex++;
                        continue;
                    }
                    rowIndex++;

                    object timeObj = reader.GetValue(timeOrdinal);
                    if (!TryParseDateTime(timeObj, out var dtVal) || dtVal == DateTime.MinValue)
                    {
                        continue;
                    }

                    result.IndexValues.Add(dtVal.ToOADate());

                    foreach (var kvp in colOrdinals)
                    {
                        double val = 0.0;
                        if (!reader.IsDBNull(kvp.Value))
                        {
                            try
                            {
                                val = reader.GetDouble(kvp.Value);
                            }
                            catch
                            {
                                object vObj = reader.GetValue(kvp.Value);
                                double.TryParse(vObj?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                            }
                        }
                        result.ChannelValues[kvp.Key].Add(val);
                    }

                    if (hasRigState)
                    {
                        int rsNum = 0;
                        if (rigStateOrdinal >= 0 && !reader.IsDBNull(rigStateOrdinal))
                        {
                            try { rsNum = reader.GetInt32(rigStateOrdinal); }
                            catch
                            {
                                int.TryParse(reader.GetValue(rigStateOrdinal)?.ToString(), out rsNum);
                            }
                        }
                        result.RigStateNumbers.Add(rsNum);

                        int colorArgb = 0;
                        if (rigStateColorOrdinal >= 0 && !reader.IsDBNull(rigStateColorOrdinal))
                        {
                            try { colorArgb = reader.GetInt32(rigStateColorOrdinal); }
                            catch
                            {
                                int.TryParse(reader.GetValue(rigStateColorOrdinal)?.ToString(), out colorArgb);
                            }
                        }
                        if (colorArgb == 0 && colorLookup.TryGetValue(rsNum, out var mappedColor))
                        {
                            colorArgb = mappedColor;
                        }
                        result.RigStateColors.Add(colorArgb);
                    }
                }
            }

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Queries depth-log data points for given mnemonics, optionally within a depth window [fromDepth, toDepth].
    /// </summary>
    public async Task<ChartDataSeriesResult> GetDepthLogDataAsync(
        string wellId,
        string wellboreId,
        string logId,
        IEnumerable<string> mnemonics,
        double? fromDepth = null,
        double? toDepth = null,
        int maxPoints = 50000,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new ChartDataSeriesResult { SourceType = enumRTDataSourceType.DepthLog };
            var requestedList = mnemonics?.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

            string tableName = ResolveDepthLogTableName(logId);
            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return result;
            }

            var tableColumns = GetTableColumnNames(tableName);
            string depthCol = tableColumns.FirstOrDefault(c => string.Equals(c, "DEPTH", StringComparison.OrdinalIgnoreCase))
                           ?? tableColumns.FirstOrDefault(c => string.Equals(c, "HOLE_DEPTH", StringComparison.OrdinalIgnoreCase))
                           ?? tableColumns.FirstOrDefault(c => string.Equals(c, "BIT_DEPTH", StringComparison.OrdinalIgnoreCase))
                           ?? tableColumns.FirstOrDefault(c => string.Equals(c, "INDEX", StringComparison.OrdinalIgnoreCase))
                           ?? "";

            if (string.IsNullOrWhiteSpace(depthCol))
            {
                return result;
            }

            var mnemonicToColumn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var req in requestedList)
            {
                string col = FindMatchingColumn(req, tableColumns);
                if (!string.IsNullOrWhiteSpace(col))
                {
                    mnemonicToColumn[req] = col;
                    result.ChannelValues[req] = new List<double>();
                }
            }

            bool hasRigState = tableColumns.Contains("RIG_STATE", StringComparer.OrdinalIgnoreCase);
            bool hasRigStateColor = tableColumns.Contains("RIG_STATE_COLOR", StringComparer.OrdinalIgnoreCase);

            var distinctCols = mnemonicToColumn.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var selectCols = new List<string> { $"[{depthCol}]" };
            foreach (var col in distinctCols)
            {
                selectCols.Add($"[{col}]");
            }
            if (hasRigState && !distinctCols.Contains("RIG_STATE", StringComparer.OrdinalIgnoreCase)) selectCols.Add("[RIG_STATE]");
            if (hasRigStateColor && !distinctCols.Contains("RIG_STATE_COLOR", StringComparer.OrdinalIgnoreCase)) selectCols.Add("[RIG_STATE_COLOR]");

            var whereClauses = new List<string>();
            if (fromDepth.HasValue)
            {
                whereClauses.Add($"[{depthCol}] >= {fromDepth.Value.ToString(CultureInfo.InvariantCulture)}");
            }
            if (toDepth.HasValue)
            {
                whereClauses.Add($"[{depthCol}] <= {toDepth.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            string sql = $"SELECT {string.Join(", ", selectCols)} FROM [{tableName}]";
            if (whereClauses.Count > 0)
            {
                sql += " WHERE " + string.Join(" AND ", whereClauses);
            }
            sql += $" ORDER BY [{depthCol}] ASC;";

            cancellationToken.ThrowIfCancellationRequested();

            var colorLookup = new Dictionary<int, int>();
            if (hasRigState)
            {
                var stateSetup = RigStateService.LoadCommonRigStateSetup(_dataService);
                if (stateSetup?.rigStates != null && stateSetup.rigStates.Count > 0)
                {
                    foreach (var item in stateSetup.rigStates.Values) colorLookup[item.Number] = (int)item.Color;
                }
                else
                {
                    foreach (var item in RigStateService.DefaultRigStateItems) colorLookup[item.Number] = item.Color;
                }
            }

            int estimatedRows = 0;
            string countSql = $"SELECT COUNT(*) FROM [{tableName}]" + (whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "") + ";";
            object? cntObj = _dataService.GetValue(countSql);
            if (cntObj != null && cntObj != DBNull.Value) int.TryParse(cntObj.ToString(), out estimatedRows);

            if (estimatedRows <= 0) return result;

            int step = maxPoints > 0 && estimatedRows > maxPoints ? (int)Math.Ceiling((double)estimatedRows / maxPoints) : 1;
            int capacity = Math.Min(estimatedRows, maxPoints > 0 ? maxPoints + 100 : estimatedRows);

            result.IndexValues.Capacity = capacity;
            foreach (var m in mnemonicToColumn.Keys) result.ChannelValues[m].Capacity = capacity;
            if (hasRigState)
            {
                result.RigStateNumbers.Capacity = capacity;
                result.RigStateColors.Capacity = capacity;
            }

            using (var reader = _dataService.ExecuteReader(sql))
            {
                if (reader == null) return result;

                int depthOrdinal = reader.GetOrdinal(depthCol);
                var colOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in mnemonicToColumn)
                {
                    int ord = reader.GetOrdinal(kvp.Value);
                    if (ord >= 0) colOrdinals[kvp.Key] = ord;
                }

                int rigStateOrdinal = -1;
                try { if (hasRigState) rigStateOrdinal = reader.GetOrdinal("RIG_STATE"); } catch { }

                int rigStateColorOrdinal = -1;
                try { if (hasRigStateColor) rigStateColorOrdinal = reader.GetOrdinal("RIG_STATE_COLOR"); } catch { }

                int rowIndex = 0;
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (step > 1 && (rowIndex % step != 0))
                    {
                        rowIndex++;
                        continue;
                    }
                    rowIndex++;

                    double depthVal = 0.0;
                    if (!reader.IsDBNull(depthOrdinal))
                    {
                        try { depthVal = reader.GetDouble(depthOrdinal); }
                        catch
                        {
                            double.TryParse(reader.GetValue(depthOrdinal)?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out depthVal);
                        }
                    }

                    result.IndexValues.Add(depthVal);

                    foreach (var kvp in colOrdinals)
                    {
                        double val = 0.0;
                        if (!reader.IsDBNull(kvp.Value))
                        {
                            try { val = reader.GetDouble(kvp.Value); }
                            catch
                            {
                                double.TryParse(reader.GetValue(kvp.Value)?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                            }
                        }
                        result.ChannelValues[kvp.Key].Add(val);
                    }

                    if (hasRigState)
                    {
                        int rsNum = 0;
                        if (rigStateOrdinal >= 0 && !reader.IsDBNull(rigStateOrdinal))
                        {
                            try { rsNum = reader.GetInt32(rigStateOrdinal); }
                            catch
                            {
                                int.TryParse(reader.GetValue(rigStateOrdinal)?.ToString(), out rsNum);
                            }
                        }
                        result.RigStateNumbers.Add(rsNum);

                        int colorArgb = 0;
                        if (rigStateColorOrdinal >= 0 && !reader.IsDBNull(rigStateColorOrdinal))
                        {
                            try { colorArgb = reader.GetInt32(rigStateColorOrdinal); }
                            catch
                            {
                                int.TryParse(reader.GetValue(rigStateColorOrdinal)?.ToString(), out colorArgb);
                            }
                        }
                        if (colorArgb == 0 && colorLookup.TryGetValue(rsNum, out var mappedColor))
                        {
                            colorArgb = mappedColor;
                        }
                        result.RigStateColors.Add(colorArgb);
                    }
                }
            }

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Groups contiguous rig state blocks into RigStateIntervals for background color-banding.
    /// </summary>
    public async Task<List<RigStateInterval>> GetRigStateIntervalsAsync(
        string logId,
        string? dataTableName = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var intervals = new List<RigStateInterval>();
            string tableName = !string.IsNullOrWhiteSpace(dataTableName) ? dataTableName : ResolveTimeLogTableName(logId);
            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return intervals;
            }

            var tableColumns = GetTableColumnNames(tableName);
            if (!tableColumns.Contains("RIG_STATE", StringComparer.OrdinalIgnoreCase))
            {
                return intervals;
            }

            string timeCol = tableColumns.FirstOrDefault(c => string.Equals(c, "DTIM", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "DATETIME", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "TIMEINDEX", StringComparison.OrdinalIgnoreCase))
                          ?? "";

            if (string.IsNullOrWhiteSpace(timeCol)) return intervals;

            var (startRow, endRow) = ResolveTimeRangeToRowIds(tableName, timeCol, fromDate, toDate);
            if (startRow == -1 && endRow == -1)
            {
                return intervals;
            }

            string colorCol = tableColumns.Contains("RIG_STATE_COLOR", StringComparer.OrdinalIgnoreCase) ? "[RIG_STATE_COLOR]" : "0 AS RIG_STATE_COLOR";

            string sql = $"SELECT [{timeCol}], [RIG_STATE], {colorCol} FROM [{tableName}]";
            var whereClauses = new List<string>();
            if (startRow.HasValue) whereClauses.Add($"rowid >= {startRow.Value}");
            if (endRow.HasValue) whereClauses.Add($"rowid <= {endRow.Value}");
            if (whereClauses.Count > 0) sql += " WHERE " + string.Join(" AND ", whereClauses);
            sql += " ORDER BY rowid ASC;";

            cancellationToken.ThrowIfCancellationRequested();

            // Load name lookup
            var stateSetup = RigStateService.LoadCommonRigStateSetup(_dataService);
            var stateNames = new Dictionary<int, string>();
            var stateColors = new Dictionary<int, int>();
            if (stateSetup?.rigStates != null && stateSetup.rigStates.Count > 0)
            {
                foreach (var item in stateSetup.rigStates.Values)
                {
                    stateNames[item.Number] = item.Name;
                    stateColors[item.Number] = (int)item.Color;
                }
            }
            else
            {
                foreach (var item in RigStateService.DefaultRigStateItems)
                {
                    stateNames[item.Number] = item.Name;
                    stateColors[item.Number] = item.Color;
                }
            }

            int currentRS = -1;
            int currentColor = 0;
            double currentStart = 0;
            double lastIndex = 0;
            bool isFirst = true;

            using (var reader = _dataService.ExecuteReader(sql))
            {
                if (reader == null) return intervals;

                int timeOrd = reader.GetOrdinal(timeCol);
                int rsOrd = reader.GetOrdinal("RIG_STATE");
                int clrOrd = -1;
                try { clrOrd = reader.GetOrdinal("RIG_STATE_COLOR"); } catch { }

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double indexVal = 0.0;
                    object tObj = reader.GetValue(timeOrd);
                    if (TryParseDateTime(tObj, out var parsedDt)) indexVal = parsedDt.ToOADate();

                    int rs = 0;
                    if (!reader.IsDBNull(rsOrd))
                    {
                        try { rs = reader.GetInt32(rsOrd); }
                        catch { int.TryParse(reader.GetValue(rsOrd)?.ToString(), out rs); }
                    }

                    int clr = 0;
                    if (clrOrd >= 0 && !reader.IsDBNull(clrOrd))
                    {
                        try { clr = reader.GetInt32(clrOrd); }
                        catch { int.TryParse(reader.GetValue(clrOrd)?.ToString(), out clr); }
                    }
                    if (clr == 0 && stateColors.TryGetValue(rs, out var mappedClr))
                    {
                        clr = mappedClr;
                    }

                    if (isFirst)
                    {
                        isFirst = false;
                        currentRS = rs;
                        currentColor = clr;
                        currentStart = indexVal;
                        lastIndex = indexVal;
                    }
                    else if (rs != currentRS)
                    {
                        stateNames.TryGetValue(currentRS, out var name);
                        intervals.Add(new RigStateInterval
                        {
                            StartIndex = currentStart,
                            EndIndex = lastIndex,
                            StateNumber = currentRS,
                            StateName = name ?? $"State {currentRS}",
                            ColorArgb = currentColor,
                            ColorHex = RigStateService.ConvertColorToHex(currentColor)
                        });

                        currentRS = rs;
                        currentColor = clr;
                        currentStart = indexVal;
                        lastIndex = indexVal;
                    }
                    else
                    {
                        lastIndex = indexVal;
                    }
                }
            }

            if (currentRS >= 0)
            {
                stateNames.TryGetValue(currentRS, out var name);
                intervals.Add(new RigStateInterval
                {
                    StartIndex = currentStart,
                    EndIndex = lastIndex,
                    StateNumber = currentRS,
                    StateName = name ?? $"State {currentRS}",
                    ColorArgb = currentColor,
                    ColorHex = RigStateService.ConvertColorToHex(currentColor)
                });
            }

            return intervals;
        }, cancellationToken);
    }

    /// <summary>
    /// Discovers all available channels and their unit / range metadata for a log.
    /// </summary>
    public async Task<List<LogChannelMetadata>> GetAvailableChannelsAsync(
        string logId,
        enumRTDataSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var list = new List<LogChannelMetadata>();
            string tableName = sourceType == enumRTDataSourceType.TimeLog
                ? ResolveTimeLogTableName(logId)
                : ResolveDepthLogTableName(logId);

            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return list;
            }

            // Check metadata from column table first
            string colTable = sourceType == enumRTDataSourceType.TimeLog ? "VMX_TIME_LOG_COLUMNS" : "VMX_DEPTH_LOG_COLUMNS";
            var metaDict = new Dictionary<string, (string Name, string Unit)>(StringComparer.OrdinalIgnoreCase);

            if (_dataService.TableExists(colTable))
            {
                string sqlMeta = $"SELECT MNEMONIC, NAME, UNIT FROM [{colTable}] WHERE LOG_ID='{logId.Replace("'", "''")}';";
                try
                {
                    DataTable dtMeta = _dataService.GetTable(sqlMeta);
                    if (dtMeta != null)
                    {
                        foreach (DataRow row in dtMeta.Rows)
                        {
                            string mn = row["MNEMONIC"]?.ToString() ?? "";
                            if (!string.IsNullOrWhiteSpace(mn))
                            {
                                metaDict[mn] = (row["NAME"]?.ToString() ?? mn, row["UNIT"]?.ToString() ?? "");
                            }
                        }
                    }
                }
                catch { }
            }

            var cols = GetTableColumnNames(tableName);
            var ignoredColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ID", "DTIM", "TIMEINDEX", "DATETIME", "DEPTH", "HOLE_DEPTH", "BIT_DEPTH", "RIG_STATE", "RIG_STATE_COLOR"
            };

            foreach (var col in cols)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ignoredColumns.Contains(col)) continue;

                string name = col;
                string unit = "";
                if (metaDict.TryGetValue(col, out var info))
                {
                    name = info.Name;
                    unit = info.Unit;
                }

                double minVal = 0;
                double maxVal = 100;
                int count = 0;

                try
                {
                    string minMaxSql = $"SELECT MIN([{col}]), MAX([{col}]), COUNT([{col}]) FROM [{tableName}] WHERE [{col}] IS NOT NULL;";
                    DataTable dtBounds = _dataService.GetTable(minMaxSql);
                    if (dtBounds != null && dtBounds.Rows.Count > 0)
                    {
                        if (dtBounds.Rows[0][0] != DBNull.Value) double.TryParse(dtBounds.Rows[0][0]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out minVal);
                        if (dtBounds.Rows[0][1] != DBNull.Value) double.TryParse(dtBounds.Rows[0][1]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out maxVal);
                        if (dtBounds.Rows[0][2] != DBNull.Value) int.TryParse(dtBounds.Rows[0][2]?.ToString(), out count);
                    }
                }
                catch { }

                list.Add(new LogChannelMetadata
                {
                    Mnemonic = col,
                    Name = name,
                    Unit = unit,
                    MinValue = minVal,
                    MaxValue = maxVal,
                    SampleCount = count
                });
            }

            return list;
        }, cancellationToken);
    }

    public static readonly Dictionary<string, string[]> MnemonicAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BIT_DEPTH"] = new[] { "BIT_DEPTH", "DBIT", "BITDEPTH", "BIT_DEP", "DEPT_BIT", "BITD" },
        ["HOLE_DEPTH"] = new[] { "HOLE_DEPTH", "DMEA", "DEPTH", "HOLEDEPTH", "DEPT_HOLE", "MD", "HOLE_DEP", "DEPT" },
        ["HOOK_LOAD"] = new[] { "HOOK_LOAD", "HKLD", "HOOKLOAD", "HKLA", "HOOK_LD", "HKL", "HOOKLOAD_AVG" },
        ["TORQUE"] = new[] { "TORQUE", "TORQ", "TRQ", "STOR", "SURF_TORQ", "TORQUE_AVG" },
        ["RPM"] = new[] { "RPM", "ROTS", "RPM_SURF", "STRPM", "SURF_RPM", "RPM_AVG" },
        ["SPP"] = new[] { "SPP", "PUMP_PRESS", "SPPA", "STANDPIPE_PRESSURE", "PUMP", "PRESS", "PUMP_PR" },
        ["RIG_STATE"] = new[] { "RIG_STATE", "RIGSTATE", "STATE", "ACT_CODE" }
    };

    public static string FindMatchingColumn(string requestedMnemonic, IEnumerable<string> tableColumns)
    {
        if (string.IsNullOrWhiteSpace(requestedMnemonic)) return "";

        string reqTrim = requestedMnemonic.Trim();
        var colList = tableColumns.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();

        var exact = colList.FirstOrDefault(c => string.Equals(c, reqTrim, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Check direct key lookup in MnemonicAliases
        if (MnemonicAliases.TryGetValue(reqTrim, out var aliases))
        {
            foreach (var alias in aliases)
            {
                var match = colList.FirstOrDefault(c => string.Equals(c, alias, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
        }

        // Check if reqTrim belongs to any alias group and look for other members of the group
        foreach (var group in MnemonicAliases.Values)
        {
            if (group.Any(a => string.Equals(a, reqTrim, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var alias in group)
                {
                    var match = colList.FirstOrDefault(c => string.Equals(c, alias, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
            }
        }

        var partial = colList.FirstOrDefault(c => c.Contains(reqTrim, StringComparison.OrdinalIgnoreCase)
                                               || reqTrim.Contains(c, StringComparison.OrdinalIgnoreCase));
        return partial ?? "";
    }

    public async Task<(DateTime MinDate, DateTime MaxDate)?> GetTimeLogDateRangeAsync(
        string logId,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run<(DateTime MinDate, DateTime MaxDate)?>(() =>
        {
            string tableName = ResolveTimeLogTableName(logId);
            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return null;
            }

            var tableColumns = GetTableColumnNames(tableName);
            string timeCol = tableColumns.FirstOrDefault(c => string.Equals(c, "DTIM", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "DATETIME", StringComparison.OrdinalIgnoreCase))
                          ?? tableColumns.FirstOrDefault(c => string.Equals(c, "TIMEINDEX", StringComparison.OrdinalIgnoreCase))
                          ?? "";

            if (string.IsNullOrWhiteSpace(timeCol)) return null;

            string sqlMin = $"SELECT [{timeCol}] FROM [{tableName}] WHERE [{timeCol}] IS NOT NULL ORDER BY rowid ASC LIMIT 1;";
            string sqlMax = $"SELECT [{timeCol}] FROM [{tableName}] WHERE [{timeCol}] IS NOT NULL ORDER BY rowid DESC LIMIT 1;";

            object? minObj = _dataService.GetValue(sqlMin);
            object? maxObj = _dataService.GetValue(sqlMax);

            if (TryParseDateTime(minObj, out var minDt) && TryParseDateTime(maxObj, out var maxDt))
            {
                if (maxDt >= minDt)
                {
                    return (minDt, maxDt);
                }
                return (maxDt, minDt);
            }

            return null;
        }, cancellationToken);
    }

    public async Task<(double MinDepth, double MaxDepth)?> GetDepthLogRangeAsync(
        string logId,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run<(double MinDepth, double MaxDepth)?>(() =>
        {
            string tableName = ResolveDepthLogTableName(logId);
            if (string.IsNullOrWhiteSpace(tableName) || !_dataService.TableExists(tableName))
            {
                return null;
            }

            var tableColumns = GetTableColumnNames(tableName);
            string depthCol = tableColumns.FirstOrDefault(c => string.Equals(c, "DEPTH", StringComparison.OrdinalIgnoreCase))
                           ?? tableColumns.FirstOrDefault(c => string.Equals(c, "MD", StringComparison.OrdinalIgnoreCase))
                           ?? tableColumns.FirstOrDefault(c => string.Equals(c, "DEPT", StringComparison.OrdinalIgnoreCase))
                           ?? "";

            if (string.IsNullOrWhiteSpace(depthCol)) return null;

            string sql = $"SELECT MIN([{depthCol}]), MAX([{depthCol}]) FROM [{tableName}] WHERE [{depthCol}] IS NOT NULL;";
            DataTable dt = _dataService.GetTable(sql);
            if (dt == null || dt.Rows.Count == 0) return null;

            double minD = 0, maxD = 0;
            if (dt.Rows[0][0] != DBNull.Value && double.TryParse(dt.Rows[0][0]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out minD) &&
                dt.Rows[0][1] != DBNull.Value && double.TryParse(dt.Rows[0][1]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out maxD) &&
                maxD > minD)
            {
                return (minD, maxD);
            }

            return null;
        }, cancellationToken);
    }

    public static bool TryParseDateTime(object? val, out DateTime result)
    {
        result = DateTime.MinValue;
        if (val == null || val == DBNull.Value) return false;

        if (val is DateTime dt)
        {
            result = dt;
            return true;
        }

        string s = val.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(s)) return false;

        // Common drilling date/time formats
        string[] formats = new[]
        {
            "dd-MMM-yyyy HH:mm:ss",
            "dd-MMM-yyyy HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "dd-MM-yyyy HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "dd-MMM-yy HH:mm:ss",
            "dd-MMM-yyyy",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
        {
            return true;
        }

        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDouble))
        {
            try
            {
                result = DateTime.FromOADate(oaDouble);
                return true;
            }
            catch { }
        }

        return false;
    }

    public (long? startRow, long? endRow) ResolveTimeRangeToRowIds(
        string tableName,
        string timeCol,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (!fromDate.HasValue && !toDate.HasValue) return (null, null);

        long minRow;
        long maxRow;

        try
        {
            object? minObj = _dataService.GetValue($"SELECT MIN(rowid) FROM [{tableName}] WHERE [{timeCol}] IS NOT NULL;");
            object? maxObj = _dataService.GetValue($"SELECT MAX(rowid) FROM [{tableName}] WHERE [{timeCol}] IS NOT NULL;");
            if (minObj == null || minObj == DBNull.Value || maxObj == null || maxObj == DBNull.Value)
            {
                return (null, null);
            }

            minRow = Convert.ToInt64(minObj);
            maxRow = Convert.ToInt64(maxObj);
        }
        catch
        {
            return (null, null);
        }

        if (maxRow < minRow) return (-1, -1);

        long startRow = minRow;
        long endRow = maxRow;

        // Binary search for startRow: lowest rowid where date >= fromDate
        if (fromDate.HasValue)
        {
            long low = minRow;
            long high = maxRow;
            long found = maxRow + 1;

            while (low <= high)
            {
                long mid = low + (high - low) / 2;
                using var reader = _dataService.ExecuteReader($"SELECT rowid, [{timeCol}] FROM [{tableName}] WHERE rowid >= {mid} ORDER BY rowid ASC LIMIT 1;");
                if (reader != null && reader.Read())
                {
                    long actualRow = reader.GetInt64(0);
                    object val = reader.GetValue(1);
                    if (TryParseDateTime(val, out var dt))
                    {
                        if (dt >= fromDate.Value)
                        {
                            found = actualRow;
                            high = mid - 1;
                        }
                        else
                        {
                            low = actualRow + 1;
                        }
                    }
                    else
                    {
                        low = actualRow + 1;
                    }
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (found > maxRow)
            {
                return (-1, -1);
            }
            startRow = found;
        }

        // Binary search for endRow: highest rowid where date <= toDate
        if (toDate.HasValue)
        {
            long low = minRow;
            long high = maxRow;
            long found = minRow - 1;

            while (low <= high)
            {
                long mid = low + (high - low) / 2;
                using var reader = _dataService.ExecuteReader($"SELECT rowid, [{timeCol}] FROM [{tableName}] WHERE rowid <= {mid} ORDER BY rowid DESC LIMIT 1;");
                if (reader != null && reader.Read())
                {
                    long actualRow = reader.GetInt64(0);
                    object val = reader.GetValue(1);
                    if (TryParseDateTime(val, out var dt))
                    {
                        if (dt <= toDate.Value)
                        {
                            found = actualRow;
                            low = mid + 1;
                        }
                        else
                        {
                            high = actualRow - 1;
                        }
                    }
                    else
                    {
                        high = actualRow - 1;
                    }
                }
                else
                {
                    low = mid + 1;
                }
            }

            if (found < minRow)
            {
                return (-1, -1);
            }
            endRow = found;
        }

        if (startRow > endRow)
        {
            return (-1, -1);
        }

        return (startRow, endRow);
    }

    private string ResolveTimeLogTableName(string logId)
    {
        if (string.IsNullOrWhiteSpace(logId)) return "";
        if (_dataService.TableExists(logId)) return logId;

        try
        {
            if (_dataService.TableExists("VMX_TIME_LOG"))
            {
                string sql = $"SELECT DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE LOG_ID='{logId.Replace("'", "''")}' LIMIT 1;";
                object? val = _dataService.GetValue(sql);
                if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                {
                    string tbl = val.ToString()!;
                    if (_dataService.TableExists(tbl)) return tbl;
                }
            }
        }
        catch { }

        try
        {
            if (_dataService.TableExists("VMX_TIME_LOG_SUMMARY"))
            {
                string sql = $"SELECT DataTableName FROM VMX_TIME_LOG_SUMMARY WHERE ObjectID='{logId.Replace("'", "''")}' LIMIT 1;";
                object? val = _dataService.GetValue(sql);
                if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                {
                    string tbl = val.ToString()!;
                    if (_dataService.TableExists(tbl)) return tbl;
                }
            }
        }
        catch { }

        return "";
    }

    private string ResolveDepthLogTableName(string logId)
    {
        if (string.IsNullOrWhiteSpace(logId)) return "";
        if (_dataService.TableExists(logId)) return logId;

        try
        {
            if (_dataService.TableExists("VMX_DEPTH_LOG"))
            {
                string sql = $"SELECT DATA_TABLE_NAME FROM VMX_DEPTH_LOG WHERE LOG_ID='{logId.Replace("'", "''")}' LIMIT 1;";
                object? val = _dataService.GetValue(sql);
                if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                {
                    string tbl = val.ToString()!;
                    if (_dataService.TableExists(tbl)) return tbl;
                }
            }
        }
        catch { }

        try
        {
            if (_dataService.TableExists("VMX_DEPTH_LOG_SUMMARY"))
            {
                string sql = $"SELECT DataTableName FROM VMX_DEPTH_LOG_SUMMARY WHERE ObjectID='{logId.Replace("'", "''")}' LIMIT 1;";
                object? val = _dataService.GetValue(sql);
                if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                {
                    string tbl = val.ToString()!;
                    if (_dataService.TableExists(tbl)) return tbl;
                }
            }
        }
        catch { }

        return "";
    }

    private List<string> GetTableColumnNames(string tableName)
    {
        var cols = new List<string>();
        try
        {
            DataTable dt = _dataService.GetTable($"PRAGMA table_info([{tableName}]);");
            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string? name = row["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        cols.Add(name);
                    }
                }
            }
        }
        catch { }
        return cols;
    }
}

/// <summary>
/// Static helper and factory class for convenient ChartData retrieval matching user query.
/// </summary>
public static class ChartData
{
    public static IChartDataService CreateService(ProjectSession session) => new ChartDataService(session);
    public static IChartDataService CreateService(IDataServiceDIntel dataService) => new ChartDataService(dataService);
}
