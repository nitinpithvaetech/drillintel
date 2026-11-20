using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class DQIProcessor
    {
        private readonly IDataServiceDIntel _dataService;
        private HashSet<double>? _cachedNullValues;

        public DQIProcessor(IDataServiceDIntel dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        public async Task<bool> ProcessWellDQIAsync(
            string wellId,
            IProgress<(double ProgressPercent, string StatusMessage)>? progress = null,
            CancellationToken ct = default)
        {
            return await Task.Run(() => ProcessWellDQI(wellId, progress, ct), ct);
        }

        public bool ProcessWellDQI(
            string wellId,
            IProgress<(double ProgressPercent, string StatusMessage)>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(wellId))
                throw new ArgumentException("Well ID cannot be null or empty.", nameof(wellId));

            progress?.Report((5, "Initializing DQI processing..."));

            // 1. Load active channels
            var channels = LoadDQIChannels();
            if (channels.Count == 0)
            {
                progress?.Report((100, "No active DQI channels configured. Please configure channels in DQI Setup."));
                return false;
            }

            // 2. Load primary time log for this well
            string lastError = string.Empty;
            var timeLogs = TimeLogService.LoadTimeLogs(_dataService, wellId, ref lastError);
            var primaryLog = timeLogs?.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs?.FirstOrDefault();
            if (primaryLog == null)
            {
                progress?.Report((100, $"No TimeLog found for Well '{wellId}'."));
                return false;
            }

            // Load log curves
            TimeLogService.LoadLogCurves(_dataService, primaryLog);

            string dataTableName = primaryLog.getDataTableName(_dataService);
            if (string.IsNullOrWhiteSpace(dataTableName) || !_dataService.TableExists(dataTableName))
            {
                progress?.Report((100, $"Data table for TimeLog '{primaryLog.ObjectID}' not found."));
                return false;
            }

            // 3. Resolve table name for DQI storage
            string safeWellId = wellId.Replace("-", "_").Replace(" ", "_");
            string dqiTableName = $"DQI_{safeWellId}";

            EnsureDQITableAndColumns(dqiTableName, channels);

            // 4. Determine time frequency
            const int DATA_FREQUENCY_SECONDS = 300;
            int wellFrequency = 1;

            object? freqObj = _dataService.GetValue($"SELECT FREQUENCY FROM VMX_WELL WHERE WELL_ID = '{wellId}'");
            if (freqObj != null && int.TryParse(freqObj.ToString(), out int f) && f > 0)
            {
                wellFrequency = f;
            }

            int expectedRows = DATA_FREQUENCY_SECONDS / wellFrequency;
            if (expectedRows <= 0) expectedRows = 300;

            // 5. Determine start and end date
            bool hasExistingRows = false;
            try
            {
                object? countObj = _dataService.GetValue($"SELECT COUNT(*) FROM [{dqiTableName}];");
                hasExistingRows = countObj != null && Convert.ToInt64(countObj) > 0;
            }
            catch { }

            DateTime fromDate = hasExistingRows ? GetLastTrackedDate(wellId) : DateTime.MinValue;
            double firstOa = TimeLogService.getFirstIndexOptimized(_dataService, wellId, primaryLog.WellboreID, primaryLog.ObjectID);
            double lastOa = TimeLogService.getLastIndexOptimized(_dataService, wellId, primaryLog.WellboreID, primaryLog.ObjectID);

            if (lastOa <= 0)
            {
                progress?.Report((100, "TimeLog contains no time indexes. Nothing to process."));
                return true;
            }

            DateTime timeLogEnd = DateTime.FromOADate(lastOa);
            if (fromDate == DateTime.MinValue || fromDate >= timeLogEnd)
            {
                fromDate = firstOa > 0 ? DateTime.FromOADate(firstOa) : timeLogEnd.AddDays(-7);
            }

            if (hasExistingRows && (timeLogEnd - fromDate).TotalSeconds < 1)
            {
                progress?.Report((100, "DQI is already up to date."));
                ComputeAndUpsertAggregates(wellId, dqiTableName, channels);
                return true;
            }

            // Load boundaries and null values
            var boundaries = LoadDQIBoundaries();
            var nullValues = GetDQINullValues();

            // Load optional mnemonic mappings if table exists
            var mnemonicMap = LoadMnemonicMappings();

            // 6. Build column query and fetch sequential records
            var queryCols = new List<string> { "DATETIME" };
            var mappedChannelCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ch in channels)
            {
                string targetCol = ch.Mnemonic;
                if (!primaryLog.logCurves.ContainsKey(targetCol) && mnemonicMap.TryGetValue(targetCol, out var mapped))
                {
                    targetCol = mapped;
                }

                if (primaryLog.logCurves.ContainsKey(targetCol))
                {
                    mappedChannelCols[ch.Mnemonic] = targetCol;
                    queryCols.Add(targetCol);
                }
            }

            string selectSql = $"SELECT {string.Join(", ", queryCols.Distinct().Select(c => $"[{c}]"))} FROM [{dataTableName}] ORDER BY rowid ASC;";
            DataTable dtAll = _dataService.GetTable(selectSql);
            if (dtAll == null || dtAll.Rows.Count == 0)
            {
                progress?.Report((100, "TimeLog table has no rows to process."));
                return true;
            }

            int totalRowsCount = dtAll.Rows.Count;
            int processedRows = 0;

            _dataService.BeginTransaction();
            try
            {
                DateTime? windowStart = null;
                DateTime? windowEnd = null;
                var windowRows = new List<DataRow>();

                void FlushWindow(DateTime wStart, DateTime wEnd, List<DataRow> rowsInWindow)
                {
                    if (rowsInWindow.Count == 0) return;
                    int totalRows = rowsInWindow.Count;
                    var dqiRow = new Dictionary<string, (double ConsIdx, double FreqIdx, double AccrIdx)>();

                    foreach (var ch in channels)
                    {
                        if (!mappedChannelCols.TryGetValue(ch.Mnemonic, out string? colName) || !dtAll.Columns.Contains(colName))
                        {
                            continue;
                        }

                        int nullCount = 0;
                        var nonNullVals = new List<double>(totalRows);

                        foreach (DataRow row in rowsInWindow)
                        {
                            object cell = row[colName];
                            if (cell == null || cell == DBNull.Value)
                            {
                                nullCount++;
                                continue;
                            }

                            if (double.TryParse(cell.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                            {
                                if (nullValues.Contains(val))
                                {
                                    nullCount++;
                                }
                                else
                                {
                                    nonNullVals.Add(val);
                                }
                            }
                            else
                            {
                                nullCount++;
                            }
                        }

                        // 1. Consistency Index = 100 - (nulls / total * 100)
                        double consIdx = totalRows > 0 ? 100.0 - ((nullCount * 100.0) / totalRows) : 0.0;
                        consIdx = Math.Clamp(consIdx, 0.0, 100.0);

                        // 2. Frequency Index = (actual / expected * 100)
                        double freqIdx = expectedRows > 0 ? (totalRows * 100.0) / expectedRows : 0.0;
                        freqIdx = Math.Clamp(freqIdx, 0.0, 100.0);

                        // 3. Accuracy Index = Outlier percentage
                        int outlierCount = 0;
                        if (boundaries.TryGetValue(ch.Mnemonic, out var bounds) && nonNullVals.Count > 0)
                        {
                            foreach (double v in nonNullVals)
                            {
                                if (v < bounds.Min || v > bounds.Max)
                                    outlierCount++;
                            }
                        }

                        double accrIdx = totalRows > 0 ? 100.0 - ((outlierCount * 100.0) / totalRows) : 100.0;
                        accrIdx = Math.Clamp(accrIdx, 0.0, 100.0);

                        dqiRow[ch.Mnemonic] = (consIdx, freqIdx, accrIdx);
                    }

                    InsertOrUpdateDQIWindowRow(dqiTableName, wStart, wEnd, dqiRow);
                    UpdateDQITracking(wellId, wEnd);
                }

                foreach (DataRow row in dtAll.Rows)
                {
                    ct.ThrowIfCancellationRequested();
                    processedRows++;

                    if (processedRows % 1000 == 0)
                    {
                        double percent = Math.Min(95.0, 10.0 + ((double)processedRows / totalRowsCount) * 80.0);
                        progress?.Report((percent, $"Processing records ({processedRows}/{totalRowsCount})..."));
                    }

                    DateTime rowDate = ParseDateTime(row["DATETIME"]);
                    if (rowDate == DateTime.MinValue) continue;

                    if (hasExistingRows && fromDate != DateTime.MinValue && rowDate < fromDate)
                        continue;

                    if (windowStart == null)
                    {
                        windowStart = rowDate;
                        windowEnd = rowDate.AddSeconds(DATA_FREQUENCY_SECONDS);
                    }

                    while (rowDate >= windowEnd)
                    {
                        FlushWindow(windowStart.Value, windowEnd.Value, windowRows);
                        windowRows.Clear();
                        windowStart = windowEnd;
                        windowEnd = windowStart.Value.AddSeconds(DATA_FREQUENCY_SECONDS);
                    }

                    windowRows.Add(row);
                }

                if (windowRows.Count > 0 && windowStart.HasValue && windowEnd.HasValue)
                {
                    FlushWindow(windowStart.Value, windowEnd.Value, windowRows);
                    windowRows.Clear();
                }

                _dataService.Commit();
            }
            catch
            {
                _dataService.RollBack();
                throw;
            }

            // 7. Compute overall well and channel aggregates
            progress?.Report((95, "Computing aggregate DQI averages..."));
            ComputeAndUpsertAggregates(wellId, dqiTableName, channels);

            progress?.Report((100, "DQI processing complete."));
            return true;
        }

        private void EnsureDQITableAndColumns(string dqiTableName, List<DQIChannel> channels)
        {
            string createSql = $@"
CREATE TABLE IF NOT EXISTS [{dqiTableName}] (
    FromDate TEXT NOT NULL,
    ToDate TEXT NOT NULL,
    PRIMARY KEY (FromDate, ToDate)
);";
            _dataService.ExecuteNonQuery(createSql);

            // Check existing columns
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dtCols = _dataService.GetTable($"PRAGMA table_info('{dqiTableName}');");
            if (dtCols != null && dtCols.Rows.Count > 0)
            {
                foreach (DataRow r in dtCols.Rows)
                {
                    existingCols.Add(r["name"]?.ToString() ?? string.Empty);
                }
            }

            foreach (var ch in channels)
            {
                string consCol = $"{ch.Mnemonic}_CONSIDX";
                string freqCol = $"{ch.Mnemonic}_FREQIDX";
                string accrCol = $"{ch.Mnemonic}_ACCRIDX";

                if (!existingCols.Contains(consCol))
                    _dataService.ExecuteNonQuery($"ALTER TABLE [{dqiTableName}] ADD COLUMN [{consCol}] REAL NULL;");
                if (!existingCols.Contains(freqCol))
                    _dataService.ExecuteNonQuery($"ALTER TABLE [{dqiTableName}] ADD COLUMN [{freqCol}] REAL NULL;");
                if (!existingCols.Contains(accrCol))
                    _dataService.ExecuteNonQuery($"ALTER TABLE [{dqiTableName}] ADD COLUMN [{accrCol}] REAL NULL;");
            }
        }

        private void InsertOrUpdateDQIWindowRow(
            string dqiTableName,
            DateTime fromDate,
            DateTime toDate,
            Dictionary<string, (double ConsIdx, double FreqIdx, double AccrIdx)> dqiRow)
        {
            string fromStr = fromDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string toStr = toDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

            var cols = new List<string> { "FromDate", "ToDate" };
            var vals = new List<string> { $"'{fromStr}'", $"'{toStr}'" };

            foreach (var kvp in dqiRow)
            {
                cols.Add($"[{kvp.Key}_CONSIDX]");
                cols.Add($"[{kvp.Key}_FREQIDX]");
                cols.Add($"[{kvp.Key}_ACCRIDX]");

                vals.Add(kvp.Value.ConsIdx.ToString("F2", CultureInfo.InvariantCulture));
                vals.Add(kvp.Value.FreqIdx.ToString("F2", CultureInfo.InvariantCulture));
                vals.Add(kvp.Value.AccrIdx.ToString("F2", CultureInfo.InvariantCulture));
            }

            string sql = $@"
INSERT OR REPLACE INTO [{dqiTableName}] ({string.Join(", ", cols)})
VALUES ({string.Join(", ", vals)});";

            _dataService.ExecuteNonQuery(sql);
        }

        private void UpdateDQITracking(string wellId, DateTime toDate)
        {
            string dateStr = toDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string sql = $@"
INSERT INTO eVuMaxWellDQITracking (WellId, LastIndex)
VALUES ('{wellId}', '{dateStr}')
ON CONFLICT(WellId) DO UPDATE SET LastIndex = '{dateStr}';";

            _dataService.ExecuteNonQuery(sql);
        }

        private DateTime GetLastTrackedDate(string wellId)
        {
            try
            {
                object? val = _dataService.GetValue($"SELECT LastIndex FROM eVuMaxWellDQITracking WHERE WellId = '{wellId}'");
                if (val != null && DateTime.TryParse(val.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    return dt;
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        private void ComputeAndUpsertAggregates(string wellId, string dqiTableName, List<DQIChannel> channels)
        {
            foreach (var ch in channels)
            {
                string sql = $@"
SELECT AVG([{ch.Mnemonic}_CONSIDX]) AS ConsAvg,
       AVG([{ch.Mnemonic}_FREQIDX]) AS FreqAvg,
       AVG([{ch.Mnemonic}_ACCRIDX]) AS AccrAvg
FROM [{dqiTableName}]
WHERE [{ch.Mnemonic}_CONSIDX] IS NOT NULL;";

                DataTable dt = _dataService.GetTable(sql);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["ConsAvg"] != DBNull.Value)
                {
                    double cons = Convert.ToDouble(dt.Rows[0]["ConsAvg"], CultureInfo.InvariantCulture);
                    double freq = Convert.ToDouble(dt.Rows[0]["FreqAvg"], CultureInfo.InvariantCulture);
                    double accr = Convert.ToDouble(dt.Rows[0]["AccrAvg"], CultureInfo.InvariantCulture);
                    double overall = (cons + freq + accr) / 3.0;

                    string upsert = $@"
INSERT INTO eVuMaxWellDQI (WellId, Mnemonic, ConsIdx, FreqIdx, AccrIdx, DQI)
VALUES ('{wellId}', '{ch.Mnemonic}', {cons.ToString("F2", CultureInfo.InvariantCulture)}, {freq.ToString("F2", CultureInfo.InvariantCulture)}, {accr.ToString("F2", CultureInfo.InvariantCulture)}, {overall.ToString("F2", CultureInfo.InvariantCulture)})
ON CONFLICT(WellId, Mnemonic) DO UPDATE SET
    ConsIdx = excluded.ConsIdx,
    FreqIdx = excluded.FreqIdx,
    AccrIdx = excluded.AccrIdx,
    DQI = excluded.DQI;";

                    _dataService.ExecuteNonQuery(upsert);
                }
            }
        }

        private List<DQIChannel> LoadDQIChannels()
        {
            var list = new List<DQIChannel>();
            if (!_dataService.TableExists("eVuMaxDQIChannels")) return list;

            var dt = _dataService.GetTable("SELECT Mnemonic, Name FROM eVuMaxDQIChannels ORDER BY Name ASC;");
            if (dt != null)
            {
                foreach (DataRow r in dt.Rows)
                {
                    list.Add(new DQIChannel(r["Mnemonic"]?.ToString() ?? "", r["Name"]?.ToString() ?? ""));
                }
            }
            return list;
        }

        private Dictionary<string, DQIDataBoundary> LoadDQIBoundaries()
        {
            var dict = new Dictionary<string, DQIDataBoundary>(StringComparer.OrdinalIgnoreCase);
            if (!_dataService.TableExists("eVuMaxDQIDataBoundaries")) return dict;

            var dt = _dataService.GetTable("SELECT Mnemonic, Name, Unit, Min, Max FROM eVuMaxDQIDataBoundaries;");
            if (dt != null)
            {
                foreach (DataRow r in dt.Rows)
                {
                    string m = r["Mnemonic"]?.ToString() ?? "";
                    string n = r["Name"]?.ToString() ?? "";
                    string u = r["Unit"]?.ToString() ?? "";
                    double min = Convert.ToDouble(r["Min"] ?? 0, CultureInfo.InvariantCulture);
                    double max = Convert.ToDouble(r["Max"] ?? 0, CultureInfo.InvariantCulture);
                    dict[m] = new DQIDataBoundary(m, n, u, min, max);
                }
            }
            return dict;
        }

        private HashSet<double> GetDQINullValues()
        {
            if (_cachedNullValues != null) return _cachedNullValues;

            var set = new HashSet<double> { -999.25, -999.0 };
            if (_dataService.TableExists("eVuMaxDQINullValues"))
            {
                object? raw = _dataService.GetValue("SELECT nullValues FROM eVuMaxDQINullValues LIMIT 1;");
                if (raw != null && !string.IsNullOrWhiteSpace(raw.ToString()))
                {
                    var parser = new DQINullValue(raw.ToString()!);
                    var parsed = parser.ParseValues();
                    if (parsed.Count > 0)
                        set = parsed;
                }
            }

            _cachedNullValues = set;
            return set;
        }

        private Dictionary<string, string> LoadMnemonicMappings()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!_dataService.TableExists("VMX_MNEMONIC_MAPPING")) return dict;

            try
            {
                var dt = _dataService.GetTable("SELECT MNEMONIC, SOURCE_MNEMONIC FROM VMX_MNEMONIC_MAPPING;");
                if (dt != null)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        string target = r["MNEMONIC"]?.ToString() ?? "";
                        string source = r["SOURCE_MNEMONIC"]?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(source))
                        {
                            dict[target] = source;
                        }
                    }
                }
            }
            catch { }
            return dict;
        }

        private static DateTime ParseDateTime(object? value)
        {
            if (value == null || value == DBNull.Value) return DateTime.MinValue;
            string s = value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(s)) return DateTime.MinValue;

            string[] formats = {
                "dd-MMM-yyyy HH:mm:ss",
                "dd-MMM-yyyy HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff",
                "dd/MM/yyyy HH:mm:ss",
                "MM/dd/yyyy HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss"
            };

            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                return dt;
            if (DateTime.TryParse(s, out dt))
                return dt;

            return DateTime.MinValue;
        }
    }
}

