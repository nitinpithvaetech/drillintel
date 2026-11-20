using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;

namespace DrillIntel.Data
{
    public class DQIRepository : IDQIRepository
    {
        private readonly ProjectSession _session;

        public DQIRepository(ProjectSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        private IDataServiceDIntel GetDataService() => _session.GetDataService();

        public async Task EnsureTablesExistAsync()
        {
            await Task.Run(() => SchemaInitializer.EnsureDQITables(GetDataService()));
        }

        public async Task<List<DQIWellLookupItem>> GetLiveWellsAsync()
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                var list = new List<DQIWellLookupItem>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    var dt = ds.GetTable("SELECT WELL_ID, WELL_NAME FROM VMX_WELL ORDER BY WELL_NAME;");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            string id = r["WELL_ID"]?.ToString() ?? "";
                            string name = r["WELL_NAME"]?.ToString() ?? id;
                            if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                            {
                                list.Add(new DQIWellLookupItem(id, !string.IsNullOrWhiteSpace(name) ? name : id));
                            }
                        }
                    }
                }
                catch { }

                return list;
            });
        }

        public async Task<List<DQIChannel>> GetChannelsAsync()
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                var list = new List<DQIChannel>();
                var dt = ds.GetTable("SELECT Mnemonic, Name FROM eVuMaxDQIChannels ORDER BY Name ASC;");
                if (dt != null)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        list.Add(new DQIChannel(r["Mnemonic"]?.ToString() ?? "", r["Name"]?.ToString() ?? ""));
                    }
                }
                return list;
            });
        }

        public async Task<bool> SaveChannelsAsync(List<DQIChannel> channels)
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                ds.BeginTransaction();
                try
                {
                    ds.ExecuteNonQuery("DELETE FROM eVuMaxDQIChannels;");
                    foreach (var ch in channels)
                    {
                        string mnemonic = ch.Mnemonic.Trim().Replace("'", "''");
                        string name = (ch.Name ?? string.Empty).Replace("'", "''");
                        ds.ExecuteNonQuery($"INSERT INTO eVuMaxDQIChannels (Mnemonic, Name) VALUES ('{mnemonic}', '{name}');");
                    }
                    ds.Commit();
                    return true;
                }
                catch
                {
                    ds.RollBack();
                    return false;
                }
            });
        }

        public async Task<List<DQIDataBoundary>> GetBoundariesAsync()
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                var list = new List<DQIDataBoundary>();
                var dt = ds.GetTable("SELECT Mnemonic, Name, Unit, Min, Max FROM eVuMaxDQIDataBoundaries ORDER BY Name ASC;");
                if (dt != null)
                {
                    foreach (DataRow r in dt.Rows)
                    {
                        list.Add(new DQIDataBoundary(
                            r["Mnemonic"]?.ToString() ?? "",
                            r["Name"]?.ToString() ?? "",
                            r["Unit"]?.ToString() ?? "",
                            Convert.ToDouble(r["Min"] ?? 0, CultureInfo.InvariantCulture),
                            Convert.ToDouble(r["Max"] ?? 0, CultureInfo.InvariantCulture)
                        ));
                    }
                }
                return list;
            });
        }

        public async Task<bool> SaveBoundariesAsync(List<DQIDataBoundary> boundaries)
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                ds.BeginTransaction();
                try
                {
                    ds.ExecuteNonQuery("DELETE FROM eVuMaxDQIDataBoundaries;");
                    foreach (var b in boundaries)
                    {
                        string mnemonic = b.Mnemonic.Trim().Replace("'", "''");
                        string name = (b.Name ?? string.Empty).Replace("'", "''");
                        string unit = (b.Unit ?? string.Empty).Replace("'", "''");
                        string minStr = b.Min.ToString("F2", CultureInfo.InvariantCulture);
                        string maxStr = b.Max.ToString("F2", CultureInfo.InvariantCulture);

                        ds.ExecuteNonQuery($@"
INSERT INTO eVuMaxDQIDataBoundaries (Mnemonic, Name, Unit, Min, Max)
VALUES ('{mnemonic}', '{name}', '{unit}', {minStr}, {maxStr});");
                    }
                    ds.Commit();
                    return true;
                }
                catch
                {
                    ds.RollBack();
                    return false;
                }
            });
        }

        public async Task<string> GetNullValuesAsync()
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                object? val = ds.GetValue("SELECT nullValues FROM eVuMaxDQINullValues LIMIT 1;");
                return val?.ToString() ?? "-999.25, -999.0";
            });
        }

        public async Task<bool> SaveNullValuesAsync(string nullValues)
        {
            return await Task.Run(() =>
            {
                var ds = GetDataService();
                string clean = (nullValues ?? "-999.25, -999.0").Replace("'", "''");
                ds.ExecuteNonQuery("DELETE FROM eVuMaxDQINullValues;");
                return ds.ExecuteNonQuery($"INSERT INTO eVuMaxDQINullValues (nullValues) VALUES ('{clean}');");
            });
        }

        public async Task<WellAverageIndices> GetWellAverageIndicesAsync(string wellId)
        {
            return await Task.Run(() =>
            {
                var result = new WellAverageIndices();
                if (string.IsNullOrWhiteSpace(wellId)) return result;

                var ds = GetDataService();
                string sql = $@"
SELECT AVG(ConsIdx) AS ConsAvg,
       AVG(FreqIdx) AS FreqAvg,
       AVG(AccrIdx) AS AccrAvg
FROM eVuMaxWellDQI
WHERE WellId = '{wellId}'
GROUP BY WellId;";

                var dt = ds.GetTable(sql);
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["ConsAvg"] != DBNull.Value)
                {
                    result.ConsistencyAvg = Convert.ToDouble(dt.Rows[0]["ConsAvg"], CultureInfo.InvariantCulture);
                    result.FrequencyAvg = Convert.ToDouble(dt.Rows[0]["FreqAvg"], CultureInfo.InvariantCulture);
                    result.AccuracyAvg = Convert.ToDouble(dt.Rows[0]["AccrAvg"], CultureInfo.InvariantCulture);
                }
                return result;
            });
        }

        public async Task<DQIData> LoadDQIDataAsync(string wellId, string mnemonic, int offsetMinutes = 0)
        {
            return await Task.Run(() =>
            {
                var result = new DQIData
                {
                    WellId = wellId,
                    Mnemonic = mnemonic
                };

                if (string.IsNullOrWhiteSpace(wellId) || string.IsNullOrWhiteSpace(mnemonic))
                {
                    result.ChartDataError = "Well ID and Mnemonic must be provided.";
                    return result;
                }

                var ds = GetDataService();

                // 1. Well-level averages
                var wellAvg = GetWellAverageIndicesAsync(wellId).GetAwaiter().GetResult();
                result.OverallWellConsIdxAvg = wellAvg.ConsistencyAvg;
                result.OverallWellFreqIdxAvg = wellAvg.FrequencyAvg;
                result.OverallWellAccrIdxAvg = wellAvg.AccuracyAvg;

                // 2. Channel-level averages
                string chAvgSql = $@"
SELECT ConsIdx, FreqIdx, AccrIdx, DQI
FROM eVuMaxWellDQI
WHERE WellId = '{wellId}' AND Mnemonic = '{mnemonic}'
LIMIT 1;";

                var dtChAvg = ds.GetTable(chAvgSql);
                if (dtChAvg != null && dtChAvg.Rows.Count > 0)
                {
                    result.ChannelDQI.ConsIdxAvg = Convert.ToDouble(dtChAvg.Rows[0]["ConsIdx"], CultureInfo.InvariantCulture);
                    result.ChannelDQI.FreqIdxAvg = Convert.ToDouble(dtChAvg.Rows[0]["FreqIdx"], CultureInfo.InvariantCulture);
                    result.ChannelDQI.AccrIdxAvg = Convert.ToDouble(dtChAvg.Rows[0]["AccrIdx"], CultureInfo.InvariantCulture);
                    result.ChannelDQI.OverAllChannelDQI = Convert.ToDouble(dtChAvg.Rows[0]["DQI"], CultureInfo.InvariantCulture);
                }

                // 3. Time-series data points from dynamic table
                string safeWellId = wellId.Replace("-", "_").Replace(" ", "_");
                string dqiTableName = $"DQI_{safeWellId}";

                if (!ds.TableExists(dqiTableName))
                {
                    result.ChartDataError = $"No DQI data has been calculated yet for Well '{wellId}'. Click 'Refresh / Process' to compute DQI metrics.";
                    return result;
                }

                string consCol = $"{mnemonic}_CONSIDX";
                string freqCol = $"{mnemonic}_FREQIDX";
                string accrCol = $"{mnemonic}_ACCRIDX";

                // Check if columns exist
                var dtCols = ds.GetTable($"PRAGMA table_info('{dqiTableName}');");
                var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dtCols != null)
                {
                    foreach (DataRow r in dtCols.Rows)
                    {
                        colSet.Add(r["name"]?.ToString() ?? "");
                    }
                }

                if (!colSet.Contains(consCol) || !colSet.Contains(freqCol) || !colSet.Contains(accrCol))
                {
                    result.ChartDataError = $"Mnemonic '{mnemonic}' has not been computed in DQI table. Click 'Refresh / Process' to compute DQI.";
                    return result;
                }

                string seriesSql = $@"
SELECT FromDate, [{consCol}] AS ConsVal, [{freqCol}] AS FreqVal, [{accrCol}] AS AccrVal
FROM [{dqiTableName}]
WHERE [{consCol}] IS NOT NULL
ORDER BY FromDate ASC;";

                var dtSeries = ds.GetTable(seriesSql);
                if (dtSeries != null && dtSeries.Rows.Count > 0)
                {
                    var points = new List<ChannelDataPoint>(dtSeries.Rows.Count);
                    foreach (DataRow r in dtSeries.Rows)
                    {
                        if (DateTime.TryParse(r["FromDate"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ||
                            DateTime.TryParse(r["FromDate"]?.ToString(), out dt))
                        {
                            double cons = r["ConsVal"] != DBNull.Value ? Convert.ToDouble(r["ConsVal"], CultureInfo.InvariantCulture) : 0;
                            double freq = r["FreqVal"] != DBNull.Value ? Convert.ToDouble(r["FreqVal"], CultureInfo.InvariantCulture) : 0;
                            double accr = r["AccrVal"] != DBNull.Value ? Convert.ToDouble(r["AccrVal"], CultureInfo.InvariantCulture) : 0;

                            points.Add(new ChannelDataPoint(dt.AddMinutes(-offsetMinutes), cons, freq, accr));
                        }
                    }
                    result.ChannelDQI.Points = points;
                }
                else
                {
                    result.ChartDataError = $"No data records found for '{mnemonic}' in table '{dqiTableName}'.";
                }

                return result;
            });
        }

        public async Task<List<string>> GetAvailableChannelMnemonicsFromTimeLogAsync(string wellId)
        {
            return await Task.Run(() =>
            {
                var list = new List<string>();
                var ds = GetDataService();
                string lastError = string.Empty;
                var timeLogs = TimeLogService.LoadTimeLogs(ds, wellId, ref lastError);
                var log = timeLogs?.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs?.FirstOrDefault();
                if (log != null)
                {
                    TimeLogService.LoadLogCurves(ds, log);
                    list = log.logCurves.Keys.OrderBy(k => k).ToList();
                }
                return list;
            });
        }
    }
}
