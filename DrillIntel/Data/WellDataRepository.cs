using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using System.Data.Common;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Services;
using DrillIntel.Services.Readers;

namespace DrillIntel.Data;

public class WellDataRepository : IWellDataRepository
{
    private readonly ProjectSession _session;
    private readonly IAppDatabaseService? _appDatabaseService;

    public WellDataRepository(ProjectSession session, IAppDatabaseService? appDatabaseService = null)
    {
        _session = session;
        _appDatabaseService = appDatabaseService;
    }

    private IAppDatabaseService? ResolveAppDatabaseService()
    {
        return _appDatabaseService ?? (System.Windows.Application.Current != null ? App.AppDatabaseService : null);
    }

    public async Task InitializeDictionaryAsync()
    {
        var appDb = ResolveAppDatabaseService();
        if (appDb != null && !appDb.IsInitialized)
        {
            appDb.Initialize();
        }
        await Task.CompletedTask;
    }

    public async Task<List<AppChannelMapping>> GetChannelMappingsAsync()
    {
        var appDb = ResolveAppDatabaseService();
        if (appDb != null)
        {
            try
            {
                var mappings = appDb.GetChannelMappings();
                if (mappings != null && mappings.Count > 0)
                {
                    return mappings;
                }
            }
            catch { }
        }

        // Fallback: check project database for APP_CHANNEL_MAPPING or legacy VMX_CURVE_DICTIONARY
        if (_session.IsProjectOpen)
        {
            try
            {
                var connection = _session.GetConnection();
                var hasAppTable = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table','view') AND name='APP_CHANNEL_MAPPING';");
                if (hasAppTable > 0)
                {
                    var result = await connection.QueryAsync<AppChannelMapping>(
                        "SELECT ID as Id, MNEMONIC as Mnemonic, STANDARD_CHANNEL as StandardChannel, DESCRIPTION as Description, DEFAULT_UNIT as DefaultUnit, SOURCE_VENDOR as SourceVendor FROM APP_CHANNEL_MAPPING;");
                    return result.ToList();
                }

                var hasLegacyTable = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table','view') AND name='VMX_CURVE_DICTIONARY';");
                if (hasLegacyTable > 0)
                {
                    var result = await connection.QueryAsync<AppChannelMapping>(
                        "SELECT ID as Id, MNEMONIC as Mnemonic, STANDARD_CHANNEL as StandardChannel FROM VMX_CURVE_DICTIONARY;");
                    return result.ToList();
                }
            }
            catch { }
        }

        return new List<AppChannelMapping>();
    }

    public async Task<List<VmxCurveDictionary>> GetCurveDictionariesAsync()
    {
        var mappings = await GetChannelMappingsAsync();
        return mappings.Select(m => new VmxCurveDictionary
        {
            Id = m.Id,
            Mnemonic = m.Mnemonic,
            StandardChannel = m.StandardChannel,
            Description = m.Description,
            DefaultUnit = m.DefaultUnit,
            SourceVendor = m.SourceVendor
        }).ToList();
    }

    public async Task LogTimeLogAsync(TimeLog log)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));

        if (string.IsNullOrWhiteSpace(log.ObjectID))
        {
            log.ObjectID = Guid.NewGuid().ToString();
        }

        var dataService = _session.GetDataService();
        var connection = _session.GetConnection();

        // Ensure WellID is set
        if (string.IsNullOrWhiteSpace(log.WellID))
        {
            var wellIdObj = dataService.GetValue("SELECT WELL_ID FROM VMX_WELL LIMIT 1;");
            if (wellIdObj != null)
            {
                log.WellID = Convert.ToString(wellIdObj) ?? string.Empty;
            }
        }

        // Ensure WellboreID is set
        if (string.IsNullOrWhiteSpace(log.WellboreID) && !string.IsNullOrWhiteSpace(log.WellID))
        {
            var wbIdObj = dataService.GetValue("SELECT WELLBORE_ID FROM VMX_WELLBORE WHERE WELL_ID='" 
                + log.WellID.Replace("'", "''") + "' LIMIT 1;");
            if (wbIdObj != null)
            {
                log.WellboreID = Convert.ToString(wbIdObj) ?? string.Empty;
            }
        }

        // 1. Persist to official VMX_TIME_LOG table via TimeLogService
        string lastError = string.Empty;
        bool addSuccess = TimeLogService.addTimeLog(dataService, log, ref lastError);
        if (!addSuccess)
        {
            throw new InvalidOperationException($"TimeLogService.addTimeLog failed: {lastError}");
        }

        if (!string.IsNullOrWhiteSpace(log.startIndex) || !string.IsNullOrWhiteSpace(log.endIndex))
        {
            try
            {
                await connection.ExecuteAsync(
                    "UPDATE VMX_TIME_LOG SET MIN_DATE = @minDate, MAX_DATE = @maxDate WHERE LOG_ID = @logId;",
                    new { minDate = log.startIndex, maxDate = log.endIndex, logId = log.ObjectID });
            }
            catch { }
        }

        // 2. Also record in separate summary table VMX_TIME_LOG_SUMMARY for metadata and fast access
        var createMaster = @"
            CREATE TABLE IF NOT EXISTS VMX_TIME_LOG_SUMMARY (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogId TEXT,
                LogName TEXT,
                WellName TEXT,
                DataTableName TEXT,
                ImportStatus TEXT,
                QcScore REAL,
                ImportDate TEXT
            );";
        await connection.ExecuteAsync(createMaster);

        // Ensure backward compatibility if table existed previously without LogId or LogName
        var existingCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_TIME_LOG_SUMMARY');")).ToList();
        if (!existingCols.Contains("LogId", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_TIME_LOG_SUMMARY ADD COLUMN LogId TEXT;");
        }
        if (!existingCols.Contains("LogName", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_TIME_LOG_SUMMARY ADD COLUMN LogName TEXT;");
        }

        string wellName = !string.IsNullOrWhiteSpace(log.nameWell) ? log.nameWell : log.__WellName;
        double qcScore = 0;
        if (!string.IsNullOrWhiteSpace(log.description) && log.description.Contains("QC:"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(log.description, @"QC:\s*([0-9.]+)\s*%");
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedQc))
            {
                qcScore = parsedQc;
            }
        }

        var insertSql = @"
            INSERT INTO VMX_TIME_LOG_SUMMARY (LogId, LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate)
            VALUES (@LogId, @LogName, @WellName, @DataTableName, @ImportStatus, @QcScore, @ImportDate);";

        await connection.ExecuteAsync(insertSql, new
        {
            LogId = log.ObjectID,
            LogName = log.nameLog,
            WellName = wellName,
            DataTableName = log.__dataTableName,
            ImportStatus = !string.IsNullOrWhiteSpace(log.comments) ? log.comments : "Success",
            QcScore = qcScore,
            ImportDate = DateTime.Now.ToString("o")
        });

        var vmxTimeCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_TIME_LOG');")).ToList();
        if (!vmxTimeCols.Contains("QC_SCORE", StringComparer.OrdinalIgnoreCase))
        {
            try { await connection.ExecuteAsync("ALTER TABLE VMX_TIME_LOG ADD COLUMN QC_SCORE REAL;"); } catch { }
        }
        try
        {
            await connection.ExecuteAsync(
                "UPDATE VMX_TIME_LOG SET QC_SCORE = @QcScore WHERE LOG_ID = @LogId;",
                new { QcScore = qcScore, LogId = log.ObjectID });
        }
        catch { }

        // Ensure no cross-pollution in VMX_DEPTH_LOG / VMX_DEPTH_LOG_SUMMARY for this TimeLog
        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM VMX_DEPTH_LOG WHERE LOG_ID = @LogId OR DATA_TABLE_NAME = @DataTableName;",
                new { LogId = log.ObjectID, DataTableName = log.__dataTableName });
        }
        catch { }
        try
        {
            var hasDepthSummary = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';");
            if (hasDepthSummary > 0)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM VMX_DEPTH_LOG_SUMMARY WHERE LogId = @LogId OR DataTableName = @DataTableName;",
                    new { LogId = log.ObjectID, DataTableName = log.__dataTableName });
            }
        }
        catch { }

        // Flush SQLite WAL to disk
        try { await connection.ExecuteAsync("PRAGMA wal_checkpoint(FULL);"); } catch { }

        // Notify session that well data has changed
        _session.NotifyDataChanged();
    }

    public async Task LogDepthLogAsync(DepthLog log)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));

        if (string.IsNullOrWhiteSpace(log.ObjectID))
        {
            log.ObjectID = Guid.NewGuid().ToString();
        }

        var dataService = _session.GetDataService();
        var connection = _session.GetConnection();

        // Ensure WellID is set
        if (string.IsNullOrWhiteSpace(log.WellID))
        {
            var wellIdObj = dataService.GetValue("SELECT WELL_ID FROM VMX_WELL LIMIT 1;");
            if (wellIdObj != null)
            {
                log.WellID = Convert.ToString(wellIdObj) ?? string.Empty;
            }
        }

        // Ensure WellboreID is set
        if (string.IsNullOrWhiteSpace(log.WellboreID) && !string.IsNullOrWhiteSpace(log.WellID))
        {
            var wbIdObj = dataService.GetValue("SELECT WELLBORE_ID FROM VMX_WELLBORE WHERE WELL_ID='" 
                + log.WellID.Replace("'", "''") + "' LIMIT 1;");
            if (wbIdObj != null)
            {
                log.WellboreID = Convert.ToString(wbIdObj) ?? string.Empty;
            }
        }

        // 1. Persist to official VMX_DEPTH_LOG table via DepthLogService
        string lastError = string.Empty;
        bool addSuccess = DepthLogService.AddDepthLog(dataService, log, ref lastError);
        if (!addSuccess)
        {
            throw new InvalidOperationException($"DepthLogService.AddDepthLog failed: {lastError}");
        }

        if (double.TryParse(log.startIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double minD) &&
            double.TryParse(log.endIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxD))
        {
            try
            {
                await connection.ExecuteAsync(
                    "UPDATE VMX_DEPTH_LOG SET MIN_DEPTH = @minD, MAX_DEPTH = @maxD WHERE LOG_ID = @logId;",
                    new { minD, maxD, logId = log.ObjectID });
            }
            catch { }
        }

        // 2. Also record in separate summary table VMX_DEPTH_LOG_SUMMARY for metadata and fast access
        var createMaster = @"
            CREATE TABLE IF NOT EXISTS VMX_DEPTH_LOG_SUMMARY (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogId TEXT,
                LogName TEXT,
                WellName TEXT,
                DataTableName TEXT,
                ImportStatus TEXT,
                QcScore REAL,
                ImportDate TEXT
            );";
        await connection.ExecuteAsync(createMaster);

        // Ensure backward compatibility if table existed previously without LogId or LogName
        var existingCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_DEPTH_LOG_SUMMARY');")).ToList();
        if (!existingCols.Contains("LogId", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_DEPTH_LOG_SUMMARY ADD COLUMN LogId TEXT;");
        }
        if (!existingCols.Contains("LogName", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_DEPTH_LOG_SUMMARY ADD COLUMN LogName TEXT;");
        }

        string wellName = !string.IsNullOrWhiteSpace(log.nameWell) ? log.nameWell : log.__WellName;
        double qcScore = 0;
        if (!string.IsNullOrWhiteSpace(log.description) && log.description.Contains("QC:"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(log.description, @"QC:\s*([0-9.]+)\s*%");
            if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedQc))
            {
                qcScore = parsedQc;
            }
        }

        var insertSql = @"
            INSERT INTO VMX_DEPTH_LOG_SUMMARY (LogId, LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate)
            VALUES (@LogId, @LogName, @WellName, @DataTableName, @ImportStatus, @QcScore, @ImportDate);";

        await connection.ExecuteAsync(insertSql, new
        {
            LogId = log.ObjectID,
            LogName = log.nameLog,
            WellName = wellName,
            DataTableName = log.__dataTableName,
            ImportStatus = !string.IsNullOrWhiteSpace(log.comments) ? log.comments : "Success",
            QcScore = qcScore,
            ImportDate = DateTime.Now.ToString("o")
        });

        // 3. Update QC score in VMX_DEPTH_LOG if column exists / add column
        try
        {
            var vmxDepthCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_DEPTH_LOG');")).ToList();
            if (!vmxDepthCols.Contains("QC_SCORE", StringComparer.OrdinalIgnoreCase))
            {
                try { await connection.ExecuteAsync("ALTER TABLE VMX_DEPTH_LOG ADD COLUMN QC_SCORE REAL;"); } catch { }
            }
            await connection.ExecuteAsync(
                "UPDATE VMX_DEPTH_LOG SET QC_SCORE = @QcScore WHERE LOG_ID = @LogId;",
                new { QcScore = qcScore, LogId = log.ObjectID });
        }
        catch { }

        // 4. Ensure no cross-pollution in VMX_TIME_LOG / VMX_TIME_LOG_SUMMARY for this DepthLog
        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM VMX_TIME_LOG WHERE LOG_ID = @LogId OR DATA_TABLE_NAME = @DataTableName;",
                new { LogId = log.ObjectID, DataTableName = log.__dataTableName });
        }
        catch { }
        try
        {
            var hasTimeSummary = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
            if (hasTimeSummary > 0)
            {
                await connection.ExecuteAsync(
                    "DELETE FROM VMX_TIME_LOG_SUMMARY WHERE LogId = @LogId OR DataTableName = @DataTableName;",
                    new { LogId = log.ObjectID, DataTableName = log.__dataTableName });
            }
        }
        catch { }

        // Flush SQLite WAL to disk
        try { await connection.ExecuteAsync("PRAGMA wal_checkpoint(FULL);"); } catch { }

        // Notify session that well data has changed
        _session.NotifyDataChanged();
    }

    public async Task<List<TimeLog>> GetTimeLogsAsync()
    {
        if (!_session.IsProjectOpen) return new List<TimeLog>();
        var connection = _session.GetConnection();
        var dataService = _session.GetDataService();

        // 1. Gather known DepthLog IDs and tables to enforce strict separation
        var knownDepthLogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownDepthTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hasDepthTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG';");
        if (hasDepthTable > 0)
        {
            var depthRows = await connection.QueryAsync<dynamic>("SELECT LOG_ID, DATA_TABLE_NAME FROM VMX_DEPTH_LOG;");
            foreach (var r in depthRows)
            {
                if (r.LOG_ID != null) knownDepthLogIds.Add(Convert.ToString(r.LOG_ID));
                if (r.DATA_TABLE_NAME != null) knownDepthTables.Add(Convert.ToString(r.DATA_TABLE_NAME));
            }
        }

        var hasDepthSummary = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';");
        if (hasDepthSummary > 0)
        {
            var depthSummaryRows = await connection.QueryAsync<dynamic>("SELECT LogId, DataTableName FROM VMX_DEPTH_LOG_SUMMARY;");
            foreach (var r in depthSummaryRows)
            {
                if (r.LogId != null) knownDepthLogIds.Add(Convert.ToString(r.LogId));
                if (r.DataTableName != null) knownDepthTables.Add(Convert.ToString(r.DataTableName));
            }
        }

        bool IsDepthLogEntry(string? id, string? tbl)
        {
            if (!string.IsNullOrWhiteSpace(id) && knownDepthLogIds.Contains(id)) return true;
            if (!string.IsNullOrWhiteSpace(tbl))
            {
                if (knownDepthTables.Contains(tbl)) return true;
                if (tbl.StartsWith("depthLog", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 2. Clean up any stale cross-listed depth logs from VMX_TIME_LOG and VMX_TIME_LOG_SUMMARY
        try
        {
            await connection.ExecuteAsync("DELETE FROM VMX_TIME_LOG WHERE DATA_TABLE_NAME LIKE 'depthLog%';");
            if (knownDepthLogIds.Count > 0)
            {
                await connection.ExecuteAsync("DELETE FROM VMX_TIME_LOG WHERE LOG_ID IN @ids;", new { ids = knownDepthLogIds.ToArray() });
            }
        }
        catch { }

        var hasSummary = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
        if (hasSummary > 0)
        {
            try
            {
                await connection.ExecuteAsync("DELETE FROM VMX_TIME_LOG_SUMMARY WHERE DataTableName LIKE 'depthLog%';");
                if (knownDepthLogIds.Count > 0)
                {
                    await connection.ExecuteAsync("DELETE FROM VMX_TIME_LOG_SUMMARY WHERE LogId IN @ids;", new { ids = knownDepthLogIds.ToArray() });
                }
            }
            catch { }
        }

        string lastError = string.Empty;
        var list = TimeLogService.LoadTimeLogs(dataService, "", ref lastError);

        // Filter out any depth log entries
        list = list.Where(tl => !IsDepthLogEntry(tl.ObjectID, tl.__dataTableName)).ToList();

        if (list.Count > 0)
        {
            var wellMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasWellTable = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_WELL';");
            if (hasWellTable > 0)
            {
                var wellRows = await connection.QueryAsync<dynamic>("SELECT WELL_ID, WELL_NAME FROM VMX_WELL;");
                foreach (var w in wellRows)
                {
                    var wDict = w as IDictionary<string, object>;
                    if (wDict != null &&
                        wDict.TryGetValue("WELL_ID", out var wId) && wId != null &&
                        wDict.TryGetValue("WELL_NAME", out var wName) && wName != null)
                    {
                        wellMap[Convert.ToString(wId)!] = Convert.ToString(wName)!;
                    }
                }
            }

            var summaries = hasSummary > 0
                ? (await connection.QueryAsync<dynamic>("SELECT * FROM VMX_TIME_LOG_SUMMARY;")).ToList()
                : new List<dynamic>();

            foreach (var tl in list)
            {
                if (string.IsNullOrWhiteSpace(tl.nameWell) && !string.IsNullOrWhiteSpace(tl.WellID) && wellMap.TryGetValue(tl.WellID, out var mappedWellName))
                {
                    tl.nameWell = mappedWellName;
                    tl.__WellName = mappedWellName;
                }

                if (summaries.Count > 0)
                {
                    var match = summaries.FirstOrDefault(s =>
                    {
                        var sDict = s as IDictionary<string, object>;
                        if (sDict == null) return false;
                        string? sId = sDict.TryGetValue("LogId", out var idO) ? Convert.ToString(idO) : null;
                        string? sTbl = sDict.TryGetValue("DataTableName", out var tblO) ? Convert.ToString(tblO) : null;
                        string? sName = sDict.TryGetValue("LogName", out var nameO) ? Convert.ToString(nameO) : null;
                        return (!string.IsNullOrWhiteSpace(sId) && sId == tl.ObjectID) ||
                               (!string.IsNullOrWhiteSpace(sTbl) && sTbl == tl.__dataTableName) ||
                               (!string.IsNullOrWhiteSpace(sName) && sName == tl.nameLog);
                    });

                    if (match is IDictionary<string, object> rowDict)
                    {
                        if (string.IsNullOrWhiteSpace(tl.nameWell) && rowDict.TryGetValue("WellName", out var wellObj) && wellObj != null && wellObj != DBNull.Value)
                        {
                            var wName = Convert.ToString(wellObj)!;
                            tl.nameWell = wName;
                            tl.__WellName = wName;
                        }

                        if (string.IsNullOrWhiteSpace(tl.description))
                        {
                            if (rowDict.TryGetValue("QcScore", out var qcObj) && qcObj != null && qcObj != DBNull.Value)
                            {
                                DateTime dt = DateTime.Now;
                                if (rowDict.TryGetValue("ImportDate", out var dateObj) && dateObj != null && dateObj != DBNull.Value)
                                {
                                    DateTime.TryParse(Convert.ToString(dateObj), out dt);
                                }
                                tl.description = $"QC: {Convert.ToDouble(qcObj):F1}% • {dt:dd-MM-yyyy hh:mm tt}";
                            }
                        }
                    }
                }
            }
            return list;
        }

        // Fallback: If VMX_TIME_LOG is empty but VMX_TIME_LOG_SUMMARY has records from previous imports
        if (hasSummary > 0)
        {
            var rows = await connection.QueryAsync<dynamic>(
                "SELECT * FROM VMX_TIME_LOG_SUMMARY ORDER BY Id DESC;");

            foreach (var r in rows)
            {
                var dict = (IDictionary<string, object>)r;
                string rLogId = dict.TryGetValue("LogId", out var idVal) ? Convert.ToString(idVal) ?? string.Empty : string.Empty;
                string rTbl = dict.TryGetValue("DataTableName", out var tblVal) ? Convert.ToString(tblVal) ?? string.Empty : string.Empty;
                if (IsDepthLogEntry(rLogId, rTbl)) continue;

                string logName = dict.TryGetValue("LogName", out var nameVal) ? Convert.ToString(nameVal) ?? string.Empty : string.Empty;
                string wellName = dict.TryGetValue("WellName", out var wellVal) ? Convert.ToString(wellVal) ?? string.Empty : string.Empty;
                string status = dict.TryGetValue("ImportStatus", out var statusVal) ? Convert.ToString(statusVal) ?? string.Empty : string.Empty;

                DateTime dt = DateTime.Now;
                if (dict.TryGetValue("ImportDate", out var dtVal) && dtVal != null && dtVal != DBNull.Value)
                    DateTime.TryParse(Convert.ToString(dtVal), out dt);

                double qc = 0;
                if (dict.TryGetValue("QcScore", out var qcVal) && qcVal != null && qcVal != DBNull.Value)
                    double.TryParse(Convert.ToString(qcVal), NumberStyles.Any, CultureInfo.InvariantCulture, out qc);

                list.Add(new TimeLog
                {
                    ObjectID = !string.IsNullOrWhiteSpace(rLogId) ? rLogId : Guid.NewGuid().ToString(),
                    nameLog = logName,
                    nameWell = wellName,
                    __WellName = wellName,
                    __dataTableName = rTbl,
                    comments = status,
                    description = $"QC: {qc:F1}% • {dt:dd-MM-yyyy hh:mm tt}",
                    creationDate = dt.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
        }

        return list;
    }

    public async Task<List<DepthLog>> GetDepthLogsAsync()
    {
        if (!_session.IsProjectOpen) return new List<DepthLog>();
        var connection = _session.GetConnection();
        var dataService = _session.GetDataService();

        // 1. Gather known TimeLog IDs and tables to enforce strict separation
        var knownTimeLogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownTimeTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hasTimeTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");
        if (hasTimeTable > 0)
        {
            var timeRows = await connection.QueryAsync<dynamic>("SELECT LOG_ID, DATA_TABLE_NAME FROM VMX_TIME_LOG;");
            foreach (var r in timeRows)
            {
                if (r.LOG_ID != null) knownTimeLogIds.Add(Convert.ToString(r.LOG_ID));
                if (r.DATA_TABLE_NAME != null) knownTimeTables.Add(Convert.ToString(r.DATA_TABLE_NAME));
            }
        }

        var hasTimeSummary = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
        if (hasTimeSummary > 0)
        {
            var timeSummaryRows = await connection.QueryAsync<dynamic>("SELECT * FROM VMX_TIME_LOG_SUMMARY;");
            foreach (var r in timeSummaryRows)
            {
                var sDict = r as IDictionary<string, object>;
                if (sDict != null)
                {
                    if (sDict.TryGetValue("LogId", out var idVal) && idVal != null) knownTimeLogIds.Add(Convert.ToString(idVal)!);
                    if (sDict.TryGetValue("DataTableName", out var tblVal) && tblVal != null) knownTimeTables.Add(Convert.ToString(tblVal)!);
                }
            }
        }

        bool IsTimeLogEntry(string? id, string? tbl)
        {
            if (!string.IsNullOrWhiteSpace(id) && knownTimeLogIds.Contains(id)) return true;
            if (!string.IsNullOrWhiteSpace(tbl))
            {
                if (knownTimeTables.Contains(tbl)) return true;
                if (tbl.StartsWith("timeLog", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 2. Clean up any stale cross-listed timelogs from VMX_DEPTH_LOG and VMX_DEPTH_LOG_SUMMARY
        try
        {
            await connection.ExecuteAsync("DELETE FROM VMX_DEPTH_LOG WHERE DATA_TABLE_NAME LIKE 'timeLog%';");
            if (knownTimeLogIds.Count > 0)
            {
                await connection.ExecuteAsync("DELETE FROM VMX_DEPTH_LOG WHERE LOG_ID IN @ids;", new { ids = knownTimeLogIds.ToArray() });
            }
        }
        catch { }

        var hasDepthSummary = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';");
        if (hasDepthSummary > 0)
        {
            try
            {
                await connection.ExecuteAsync("DELETE FROM VMX_DEPTH_LOG_SUMMARY WHERE DataTableName LIKE 'timeLog%';");
                if (knownTimeLogIds.Count > 0)
                {
                    await connection.ExecuteAsync("DELETE FROM VMX_DEPTH_LOG_SUMMARY WHERE LogId IN @ids;", new { ids = knownTimeLogIds.ToArray() });
                }
            }
            catch { }
        }

        string lastError = string.Empty;
        var list = DepthLogService.LoadDepthLogs(dataService, "", ref lastError);

        // Filter out any timelog entries
        list = list.Where(dl => !IsTimeLogEntry(dl.ObjectID, dl.__dataTableName)).ToList();

        if (list.Count > 0)
        {
            if (hasDepthSummary > 0)
            {
                var summaries = (await connection.QueryAsync<dynamic>(
                    "SELECT * FROM VMX_DEPTH_LOG_SUMMARY;")).ToList();

                foreach (var dl in list)
                {
                    var match = summaries.FirstOrDefault(s =>
                    {
                        var sDict = s as IDictionary<string, object>;
                        if (sDict == null) return false;
                        string? sId = sDict.TryGetValue("LogId", out var idO) ? Convert.ToString(idO) : null;
                        string? sTbl = sDict.TryGetValue("DataTableName", out var tblO) ? Convert.ToString(tblO) : null;
                        string? sName = sDict.TryGetValue("LogName", out var nameO) ? Convert.ToString(nameO) : null;
                        return (!string.IsNullOrWhiteSpace(sId) && sId == dl.ObjectID) ||
                               (!string.IsNullOrWhiteSpace(sTbl) && sTbl == dl.__dataTableName) ||
                               (!string.IsNullOrWhiteSpace(sName) && sName == dl.nameLog);
                    });

                    if (match is IDictionary<string, object> rowDict && string.IsNullOrWhiteSpace(dl.description))
                    {
                        if (rowDict.TryGetValue("QcScore", out var qcObj) && qcObj != null && qcObj != DBNull.Value)
                        {
                            DateTime dt = DateTime.Now;
                            if (rowDict.TryGetValue("ImportDate", out var dateObj) && dateObj != null && dateObj != DBNull.Value)
                            {
                                DateTime.TryParse(Convert.ToString(dateObj), out dt);
                            }
                            dl.description = $"QC: {Convert.ToDouble(qcObj):F1}% • {dt:dd-MM-yyyy hh:mm tt}";
                        }
                    }
                }
            }
            return list;
        }

        // Fallback: If VMX_DEPTH_LOG is empty but VMX_DEPTH_LOG_SUMMARY has records from previous imports
        if (hasDepthSummary > 0)
        {
            var rows = await connection.QueryAsync<dynamic>(
                "SELECT * FROM VMX_DEPTH_LOG_SUMMARY ORDER BY Id DESC;");

            foreach (var r in rows)
            {
                var dict = (IDictionary<string, object>)r;
                string rLogId = dict.TryGetValue("LogId", out var idVal) ? Convert.ToString(idVal) ?? string.Empty : string.Empty;
                string rTbl = dict.TryGetValue("DataTableName", out var tblVal) ? Convert.ToString(tblVal) ?? string.Empty : string.Empty;
                if (IsTimeLogEntry(rLogId, rTbl)) continue;

                string logName = dict.TryGetValue("LogName", out var nameVal) ? Convert.ToString(nameVal) ?? string.Empty : string.Empty;
                string wellName = dict.TryGetValue("WellName", out var wellVal) ? Convert.ToString(wellVal) ?? string.Empty : string.Empty;
                string status = dict.TryGetValue("ImportStatus", out var statusVal) ? Convert.ToString(statusVal) ?? string.Empty : string.Empty;

                DateTime dt = DateTime.Now;
                if (dict.TryGetValue("ImportDate", out var dtVal) && dtVal != null && dtVal != DBNull.Value)
                    DateTime.TryParse(Convert.ToString(dtVal), out dt);

                double qc = 0;
                if (dict.TryGetValue("QcScore", out var qcVal) && qcVal != null && qcVal != DBNull.Value)
                    double.TryParse(Convert.ToString(qcVal), NumberStyles.Any, CultureInfo.InvariantCulture, out qc);

                list.Add(new DepthLog
                {
                    ObjectID = !string.IsNullOrWhiteSpace(rLogId) ? rLogId : Guid.NewGuid().ToString(),
                    nameLog = logName,
                    nameWell = wellName,
                    __WellName = wellName,
                    __dataTableName = rTbl,
                    comments = status,
                    description = $"QC: {qc:F1}% • {dt:dd-MM-yyyy hh:mm tt}",
                    creationDate = dt.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
        }

        return list;
    }

    public async Task<Well?> GetProjectWellAsync()
    {
        if (!_session.IsProjectOpen) return null;

        var dbWell = await _session.GetConnection().QueryFirstOrDefaultAsync<dynamic>(
            "SELECT WELL_ID, WELL_NAME, FIELD FROM VMX_WELL LIMIT 1;");

        if (dbWell != null)
        {
            string? wellId = dbWell.WELL_ID?.ToString();
            if (!string.IsNullOrWhiteSpace(wellId))
            {
                string lastError = string.Empty;
                var loadedWell = WellService.LoadObject(_session.GetDataService(), wellId, ref lastError);
                if (loadedWell != null)
                {
                    return loadedWell;
                }
            }

            string? wellName = dbWell.WELL_NAME?.ToString();
            if (!string.IsNullOrWhiteSpace(wellName))
            {
                return new Well
                {
                    ObjectID = wellId ?? Guid.NewGuid().ToString(),
                    name = wellName,
                    field = dbWell.FIELD?.ToString() ?? "General Field"
                };
            }
        }

        return null;
    }

    public async Task SaveProjectWellAsync(Well well)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(well.name)) return;
        var connection = _session.GetConnection();

        if (string.IsNullOrWhiteSpace(well.ObjectID))
        {
            well.ObjectID = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(well.field))
        {
            well.field = "General Field";
        }

        string lastError = string.Empty;
        bool isExisting = Well.IsWellExist(_session.GetDataService(), well.ObjectID);
        bool success = isExisting
            ? WellService.UpdateWell(_session.GetDataService(), well, ref lastError)
            : WellService.AddWell(_session.GetDataService(), well, ref lastError);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to save well record: {lastError}");
        }

        // Normalize any existing logs in this project to this single well name
        var hasTimeTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
        if (hasTimeTable > 0)
        {
            await connection.ExecuteAsync("UPDATE VMX_TIME_LOG_SUMMARY SET WellName = @WellName;", new { WellName = well.name.Trim() });
        }

        var hasDepthTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';");
        if (hasDepthTable > 0)
        {
            await connection.ExecuteAsync("UPDATE VMX_DEPTH_LOG_SUMMARY SET WellName = @WellName;", new { WellName = well.name.Trim() });
        }
    }

    public async Task<List<Wellbore>> GetWellboresAsync(string wellId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(wellId)) return new List<Wellbore>();
        string lastError = string.Empty;
        return await Task.Run(() => WellboreService.LoadWellbores(_session.GetDataService(), wellId, ref lastError));
    }

    public async Task EnsureWellAsync(string wellName, string? fieldName = null)
    {
        var existing = await GetProjectWellAsync();
        if (existing == null)
        {
            var wellId = Guid.NewGuid().ToString();
            var wellboreId = Guid.NewGuid().ToString();
            var well = new Well
            {
                ObjectID = wellId,
                name = wellName,
                field = fieldName ?? "General Field",
                dTimSpud = DateTime.Now.ToString("o")
            };
            var wellbore = new Wellbore
            {
                ObjectID = wellboreId,
                WellID = wellId,
                nameWell = wellName,
                name = wellName
            };
            well.wellbores[wellboreId] = wellbore;
            well.__timeLogWellboreID = wellboreId;

            await SaveProjectWellAsync(well);
        }
    }

    public async Task<List<Well>> GetWellsAsync()
    {
        var projectWell = await GetProjectWellAsync();
        if (projectWell != null)
            return new List<Well> { projectWell };

        return new List<Well>();
    }

    public static string SanitizeIdentifier(string name, int index)
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"col_{index + 1}";

        var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
        var clean = new string(chars).Trim('_');

        while (clean.Contains("__"))
            clean = clean.Replace("__", "_");

        if (string.IsNullOrEmpty(clean))
            return $"col_{index + 1}";

        if (char.IsDigit(clean[0]))
            clean = "col_" + clean;

        return clean;
    }

    private class ColumnPlan
    {
        public string SourceHeader { get; set; } = string.Empty;
        public string DbColumnName { get; set; } = string.Empty;
        public int SourceIndex { get; set; } = -1;
        public bool IsDepthColumn { get; set; }
        public bool IsHookloadColumn { get; set; }
        public bool IsDateTimeColumn { get; set; }
    }

    public async Task CreateDynamicTimelogTableAsync(string tableName, List<ChannelMapping> mappings)
    {
        var connection = _session.GetConnection();

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS [{tableName}] (");
        sb.Append("  [Id] INTEGER PRIMARY KEY AUTOINCREMENT");
        
        var distinctColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int idx = 0;
        foreach (var map in mappings)
        {
            var colName = map.MappedVumaxChannel == "Dynamic (New Column)" ? map.CsvColumnHeader : map.MappedVumaxChannel;
            var safeColName = SanitizeIdentifier(colName, idx++);
            
            var finalColName = safeColName;
            int suffix = 1;
            while (distinctColumns.Contains(finalColName))
            {
                finalColName = $"{safeColName}_{suffix++}";
            }

            distinctColumns.Add(finalColName);
            sb.Append($",\n  [{finalColName}] NUMERIC");
        }
        sb.AppendLine("\n);");

        await connection.ExecuteAsync(sb.ToString());
    }

    public async Task BulkInsertTimelogAsync(string tableName, DataTable data)
    {
        var connection = _session.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;

        var colNames = new List<string>();
        var paramPlaceholders = new List<string>();
        var parameters = new List<DbParameter>();

        for (int i = 0; i < data.Columns.Count; i++)
        {
            var col = data.Columns[i];
            var cleanName = SanitizeIdentifier(col.ColumnName, i);
            colNames.Add($"[{cleanName}]");
            paramPlaceholders.Add($"@p{i}");

            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            cmd.Parameters.Add(p);
            parameters.Add(p);
        }

        cmd.CommandText = $"INSERT INTO [{tableName}] ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramPlaceholders)});";

        foreach (DataRow row in data.Rows)
        {
            for (int i = 0; i < data.Columns.Count; i++)
            {
                var val = row[i];
                parameters[i].Value = (val == null || val == DBNull.Value) ? DBNull.Value : val;
            }
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        await Task.CompletedTask;
    }

    /// <summary>
    /// High-throughput streaming importer directly into SQLite.
    /// Eliminates memory spikes, eliminates exception overhead, and imports 1,000,000+ rows in seconds.
    /// </summary>
    public Task<StreamImportResult> StreamImportDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return StreamImportDataAsync(tableName, filePath, mappings, 1, 2, ",", null, progress, cancellationToken);
    }

    public async Task<StreamImportResult> StreamImportDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        int columnHeadingRow,
        int importFromRow,
        string delimiter = ",",
        string? worksheetName = null,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default,
        TimeLogDateTimeOptions? dateTimeOptions = null)
    {
        if (!_session.IsProjectOpen)
            throw new InvalidOperationException("No project is currently loaded.");

        if (columnHeadingRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnHeadingRow), "Column Heading Row is mandatory and must be a positive integer.");

        if (importFromRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(importFromRow), "Import from Row is mandatory and must be a positive integer.");

        var connection = _session.GetConnection();

        // 1. Resolve Reader and Headers
        var formatReaders = new IDepthLogFormatReader[]
        {
            new LasDepthReader(),
            new WitsmlDepthReader(),
            new ExcelDepthReader(),
            new CsvDepthReader()
        };
        var reader = formatReaders.FirstOrDefault(r => r.CanHandle(filePath)) ?? new CsvDepthReader();
        var fileMetadata = reader.ExtractMetadata(filePath);
        var sourceHeaders = reader.GetHeaders(filePath, columnHeadingRow, worksheetName, delimiter);

        bool isTimeLog = tableName.StartsWith("timeLog", StringComparison.OrdinalIgnoreCase) || dateTimeOptions != null;
        string? separateDateColHeader = null;
        string? separateTimeColHeader = null;

        if (isTimeLog)
        {
            dateTimeOptions ??= new TimeLogDateTimeOptions();

            if (!dateTimeOptions.IsDatetimeInSeperatorColumn && dateTimeOptions.DateColNo.HasValue && dateTimeOptions.TimeColNo.HasValue)
            {
                dateTimeOptions.IsDatetimeInSeperatorColumn = true;
            }
            // Auto-detect separate Date & Time columns if not explicitly set
            else if (!dateTimeOptions.IsDatetimeInSeperatorColumn && (!dateTimeOptions.DateColNo.HasValue || !dateTimeOptions.TimeColNo.HasValue))
            {
                if (TimeLogDateTimeParser.TryDetectSeparateDateTimeColumns(sourceHeaders, out int dIdx, out int tIdx))
                {
                    dateTimeOptions.IsDatetimeInSeperatorColumn = true;
                    dateTimeOptions.DateColNo = dIdx;
                    dateTimeOptions.TimeColNo = tIdx;
                }
            }

            if (dateTimeOptions.IsDatetimeInSeperatorColumn)
            {
                var sampleRows = reader.GetPreviewRows(filePath, importFromRow, 20, worksheetName, delimiter);
                if (TimeLogDateTimeParser.TryResolveDateAndTimeHeaders(
                    sourceHeaders, dateTimeOptions.DateColNo, dateTimeOptions.TimeColNo,
                    out var resolvedDate, out var resolvedTime,
                    out int resDIdx, out int resTIdx,
                    sampleRows, dateTimeOptions.DateFormat))
                {
                    separateDateColHeader = resolvedDate;
                    separateTimeColHeader = resolvedTime;
                    dateTimeOptions.ResolvedDateColIdx = resDIdx;
                    dateTimeOptions.ResolvedTimeColIdx = resTIdx;
                }
            }
        }

        bool isSplit = isTimeLog && dateTimeOptions != null && (dateTimeOptions.IsDatetimeInSeperatorColumn || (dateTimeOptions.DateColNo.HasValue && dateTimeOptions.TimeColNo.HasValue));
        var excludedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (isSplit)
        {
            if (!string.IsNullOrEmpty(separateDateColHeader)) excludedHeaders.Add(separateDateColHeader);
            if (!string.IsNullOrEmpty(separateTimeColHeader)) excludedHeaders.Add(separateTimeColHeader);
            if (dateTimeOptions!.ResolvedDateColIdx.HasValue && dateTimeOptions.ResolvedDateColIdx.Value >= 0 && dateTimeOptions.ResolvedDateColIdx.Value < sourceHeaders.Count)
                excludedHeaders.Add(sourceHeaders[dateTimeOptions.ResolvedDateColIdx.Value]);
            if (dateTimeOptions.ResolvedTimeColIdx.HasValue && dateTimeOptions.ResolvedTimeColIdx.Value >= 0 && dateTimeOptions.ResolvedTimeColIdx.Value < sourceHeaders.Count)
                excludedHeaders.Add(sourceHeaders[dateTimeOptions.ResolvedTimeColIdx.Value]);

            excludedHeaders.Add("DATE");
            excludedHeaders.Add("TIME");
            excludedHeaders.Add("DATE_TIME");
            excludedHeaders.Add("LOGDATE");
            excludedHeaders.Add("LOGTIME");
            excludedHeaders.Add("LOG_DATE");
            excludedHeaders.Add("LOG_TIME");
        }

        // 2. Determine columns and map to source headers
        var columnPlans = new List<ColumnPlan>();
        var distinctNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int colIdx = 0;
        foreach (var map in mappings)
        {
            var header = map.CsvColumnHeader;

            // In split datetime mode for TimeLog, skip separate raw Date and Time columns so they don't become numeric columns
            if (isSplit)
            {
                if (!map.MappedVumaxChannel.Equals("DATETIME", StringComparison.OrdinalIgnoreCase))
                {
                    if (excludedHeaders.Contains(header) ||
                        excludedHeaders.Contains(map.MappedVumaxChannel) ||
                        TimeLogDateTimeParser.LooksLikeDateHeader(header) ||
                        TimeLogDateTimeParser.LooksLikeDateHeader(map.MappedVumaxChannel) ||
                        TimeLogDateTimeParser.LooksLikeTimeHeader(header) ||
                        TimeLogDateTimeParser.LooksLikeTimeHeader(map.MappedVumaxChannel))
                    {
                        continue;
                    }
                }
            }

            var targetChannel = map.MappedVumaxChannel == "Dynamic (New Column)" ? header : map.MappedVumaxChannel;

            // In split datetime mode for TimeLog, ensure targetChannel is never DATE or TIME
            if (isSplit)
            {
                if (!targetChannel.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) &&
                    (excludedHeaders.Contains(targetChannel) ||
                     TimeLogDateTimeParser.LooksLikeDateHeader(targetChannel) ||
                     TimeLogDateTimeParser.LooksLikeTimeHeader(targetChannel)))
                {
                    continue;
                }
            }

            var safeName = SanitizeIdentifier(targetChannel, colIdx++);

            var finalName = safeName;
            int suffix = 1;
            while (distinctNames.Contains(finalName))
            {
                finalName = $"{safeName}_{suffix++}";
            }
            distinctNames.Add(finalName);

            columnPlans.Add(new ColumnPlan
            {
                SourceHeader = header,
                DbColumnName = finalName,
                IsDepthColumn = targetChannel.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) || targetChannel.Equals("Depth", StringComparison.OrdinalIgnoreCase),
                IsHookloadColumn = targetChannel.Equals("HKLD", StringComparison.OrdinalIgnoreCase) || targetChannel.Equals("Hookload", StringComparison.OrdinalIgnoreCase),
                IsDateTimeColumn = targetChannel.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
                                   targetChannel.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                                   targetChannel.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                                   targetChannel.Equals("DATE", StringComparison.OrdinalIgnoreCase)
            });
        }

        // Map column plan source indices
        for (int i = 0; i < columnPlans.Count; i++)
        {
            int idx = sourceHeaders.FindIndex(h => h.Equals(columnPlans[i].SourceHeader, StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
            {
                if (columnPlans[i].SourceHeader.StartsWith("#") && int.TryParse(columnPlans[i].SourceHeader.Substring(1), out int numIdx))
                {
                    idx = numIdx;
                }
            }
            columnPlans[i].SourceIndex = idx;
        }

        // Final safety filter for TimeLog split datetime mode: exclude split date & time columns from columnPlans
        if (isSplit)
        {
            columnPlans = columnPlans.Where(p =>
            {
                if (p.DbColumnName.Equals("DATETIME", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (excludedHeaders.Contains(p.SourceHeader) ||
                    excludedHeaders.Contains(p.DbColumnName))
                {
                    return false;
                }
                if (TimeLogDateTimeParser.LooksLikeDateHeader(p.SourceHeader) ||
                    TimeLogDateTimeParser.LooksLikeDateHeader(p.DbColumnName) ||
                    TimeLogDateTimeParser.LooksLikeTimeHeader(p.SourceHeader) ||
                    TimeLogDateTimeParser.LooksLikeTimeHeader(p.DbColumnName))
                {
                    return false;
                }
                if (dateTimeOptions!.ResolvedDateColIdx.HasValue && p.SourceIndex == dateTimeOptions.ResolvedDateColIdx.Value)
                    return false;
                if (dateTimeOptions.ResolvedTimeColIdx.HasValue && p.SourceIndex == dateTimeOptions.ResolvedTimeColIdx.Value)
                    return false;
                return true;
            }).ToList();
        }

        // Ensure primary DATETIME column plan is present for TimeLog tables
        if (isTimeLog && !columnPlans.Any(p => p.DbColumnName.Equals("DATETIME", StringComparison.OrdinalIgnoreCase)))
        {
            columnPlans.Insert(0, new ColumnPlan
            {
                SourceHeader = separateDateColHeader ?? "DATETIME",
                DbColumnName = "DATETIME",
                IsDateTimeColumn = true,
                SourceIndex = -1
            });
            distinctNames.Add("DATETIME");
        }

        // 3. Check if table already exists (e.g. created by DepthLogService.AddDepthLog / TimeLogService.addTimeLog)
        bool tableExists = false;
        var existingTableCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tName;";
            var pName = checkCmd.CreateParameter();
            pName.ParameterName = "@tName";
            pName.Value = tableName;
            checkCmd.Parameters.Add(pName);
            tableExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
        }

        if (!tableExists)
        {
            // Create Dynamic Table
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE IF NOT EXISTS [{tableName}] (");
            sb.Append("  [Id] INTEGER PRIMARY KEY AUTOINCREMENT");
            foreach (var plan in columnPlans)
            {
                sb.Append($",\n  [{plan.DbColumnName}] {(plan.IsDateTimeColumn ? "DATETIME" : "NUMERIC")}");
            }
            sb.AppendLine("\n);");

            using (var createCmd = connection.CreateCommand())
            {
                createCmd.CommandText = sb.ToString();
                createCmd.ExecuteNonQuery();
            }
        }
        else
        {
            using (var infoCmd = connection.CreateCommand())
            {
                infoCmd.CommandText = $"PRAGMA table_info([{tableName}]);";
                using var infoReader = infoCmd.ExecuteReader();
                while (infoReader.Read())
                {
                    var cName = infoReader["name"]?.ToString();
                    if (!string.IsNullOrEmpty(cName))
                        existingTableCols.Add(cName);
                }
            }

            // Dynamically create any missing columns in target table
            foreach (var plan in columnPlans)
            {
                if (isSplit)
                {
                    if (excludedHeaders.Contains(plan.DbColumnName))
                    {
                        continue;
                    }
                }

                if (!existingTableCols.Contains(plan.DbColumnName))
                {
                    using (var alterCmd = connection.CreateCommand())
                    {
                        alterCmd.CommandText = $"ALTER TABLE [{tableName}] ADD COLUMN [{plan.DbColumnName}] NUMERIC;";
                        alterCmd.ExecuteNonQuery();
                    }
                    existingTableCols.Add(plan.DbColumnName);
                }
            }
        }

        // Ensure primary DATETIME column plan is present for TimeLog tables (or tables containing DATETIME column)
        if ((isTimeLog || existingTableCols.Contains("DATETIME")) &&
            !columnPlans.Any(p => p.DbColumnName.Equals("DATETIME", StringComparison.OrdinalIgnoreCase)))
        {
            columnPlans.Insert(0, new ColumnPlan
            {
                SourceHeader = separateDateColHeader ?? "DATETIME",
                DbColumnName = "DATETIME",
                IsDateTimeColumn = true,
                SourceIndex = -1
            });
            existingTableCols.Add("DATETIME");
        }

        bool hasDataIndex = existingTableCols.Contains("DATA_INDEX");

        // 4. Prepare Parameterized Insert Command
        var insertCols = new List<string>();
        var insertParams = new List<string>();

        if (hasDataIndex)
        {
            insertCols.Add("[DATA_INDEX]");
            insertParams.Add("@pDataIndex");
        }

        for (int i = 0; i < columnPlans.Count; i++)
        {
            insertCols.Add($"[{columnPlans[i].DbColumnName}]");
            insertParams.Add($"@p{i}");
        }

        var colNames = string.Join(", ", insertCols);
        var paramNames = string.Join(", ", insertParams);
        var insertSql = tableExists
            ? $"INSERT OR REPLACE INTO [{tableName}] ({colNames}) VALUES ({paramNames});"
            : $"INSERT INTO [{tableName}] ({colNames}) VALUES ({paramNames});";

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = insertSql;

        DbParameter? pDataIndex = null;
        if (hasDataIndex)
        {
            pDataIndex = insertCmd.CreateParameter();
            pDataIndex.ParameterName = "@pDataIndex";
            insertCmd.Parameters.Add(pDataIndex);
        }

        var cmdParams = new DbParameter[columnPlans.Count];
        for (int i = 0; i < columnPlans.Count; i++)
        {
            var p = insertCmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            insertCmd.Parameters.Add(p);
            cmdParams[i] = p;
        }



        if (isTimeLog && dateTimeOptions != null && dateTimeOptions.SingleDateTimeColIdx < 0)
        {
            var dtPlan = columnPlans.FirstOrDefault(p => p.IsDateTimeColumn && p.SourceIndex >= 0);
            if (dtPlan != null)
            {
                dateTimeOptions.SingleDateTimeColIdx = dtPlan.SourceIndex;
            }
        }

        int totalRows = 0;
        int validRows = 0;
        int nonSequentialCount = 0;
        var validationMessages = new List<string>();
        DateTime? prevDateTime = null;
        string? prevFormattedDate = null;
        double? minDepth = null;
        double? maxDepth = null;
        double? firstDepth = null;
        double? lastDepth = null;
        double? prevDepth = null;
        string? calculatedStep = null;
        string? minDate = null;
        string? maxDate = null;
        const int batchSize = 50000;

        DbTransaction? transaction = connection.BeginTransaction();
        insertCmd.Transaction = transaction;

        try
        {
            foreach (var tokens in reader.StreamDataRows(filePath, importFromRow, worksheetName, delimiter, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalRows++;
                if (hasDataIndex && pDataIndex != null)
                {
                    pDataIndex.Value = totalRows;
                }
                bool isRowValid = true;
                bool depthIsNull = false;

                // --- [NEW LOGIC (TimeLog DateTime parsing using TimeLogDateTimeParser and full QC validation)] ---
                string? rowFormattedDate = null;
                bool dateTimeParsedOk = false;

                if (isTimeLog && dateTimeOptions != null)
                {
                    dateTimeParsedOk = TimeLogDateTimeParser.TryParse(tokens, dateTimeOptions, out var parsedDt, out rowFormattedDate);
                    if (dateTimeParsedOk && !string.IsNullOrEmpty(rowFormattedDate))
                    {
                        if (minDate == null) minDate = rowFormattedDate;
                        maxDate = rowFormattedDate;

                        if (prevDateTime.HasValue)
                        {
                            if (parsedDt < prevDateTime.Value)
                            {
                                nonSequentialCount++;
                                if (validationMessages.Count < 20)
                                {
                                    validationMessages.Add($"Row {totalRows}: Timestamp {rowFormattedDate} is earlier than previous timestamp {prevFormattedDate}.");
                                }
                            }
                        }
                        prevDateTime = parsedDt;
                        prevFormattedDate = rowFormattedDate;
                    }
                    else
                    {
                        isRowValid = false;
                    }
                }

                for (int i = 0; i < columnPlans.Count; i++)
                {
                    if (columnPlans[i].IsDateTimeColumn)
                    {
                        if (columnPlans[i].DbColumnName.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) && rowFormattedDate != null)
                        {
                            cmdParams[i].Value = rowFormattedDate;
                        }
                        else if (rowFormattedDate != null && columnPlans[i].SourceIndex < 0)
                        {
                            cmdParams[i].Value = rowFormattedDate;
                        }
                        else
                        {
                            int sIdx = columnPlans[i].SourceIndex;
                            string? raw = (sIdx >= 0 && sIdx < tokens.Length) ? tokens[sIdx] : null;
                            if (string.IsNullOrWhiteSpace(raw))
                            {
                                cmdParams[i].Value = DBNull.Value;
                                if (isTimeLog && columnPlans[i].DbColumnName.Equals("DATETIME", StringComparison.OrdinalIgnoreCase)) isRowValid = false;
                            }
                            else
                            {
                                cmdParams[i].Value = raw.Trim();
                                if (minDate == null) minDate = raw.Trim();
                                maxDate = raw.Trim();
                            }
                        }
                        continue;
                    }

                    int srcIdx = columnPlans[i].SourceIndex;
                    string? rawVal = (srcIdx >= 0 && srcIdx < tokens.Length) ? tokens[srcIdx] : null;

                    if (string.IsNullOrWhiteSpace(rawVal) || rawVal == "-999.25" || rawVal == "-9999" || (fileMetadata?.NullValue != null && rawVal == fileMetadata.NullValue))
                    {
                        cmdParams[i].Value = DBNull.Value;
                        if (columnPlans[i].IsDepthColumn)
                        {
                            isRowValid = false;
                            depthIsNull = true;
                        }
                    }
                    else if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    {
                        cmdParams[i].Value = dblVal;
                        if (double.IsNaN(dblVal) || double.IsInfinity(dblVal)) isRowValid = false;

                        if (columnPlans[i].IsHookloadColumn && dblVal < 0) isRowValid = false;
                        if (columnPlans[i].IsDepthColumn)
                        {
                            if (dblVal < 0) isRowValid = false;
                            if (!firstDepth.HasValue) firstDepth = dblVal;
                            if (prevDepth.HasValue && calculatedStep == null)
                            {
                                double diff = Math.Abs(dblVal - prevDepth.Value);
                                if (diff > 0.00001)
                                {
                                    calculatedStep = diff.ToString("0.####", CultureInfo.InvariantCulture);
                                }
                            }
                            prevDepth = dblVal;
                            lastDepth = dblVal;

                            if (!minDepth.HasValue || dblVal < minDepth.Value) minDepth = dblVal;
                            if (!maxDepth.HasValue || dblVal > maxDepth.Value) maxDepth = dblVal;
                        }

                        var colUpper = columnPlans[i].DbColumnName.ToUpperInvariant();
                        if ((colUpper.Contains("RPM") || colUpper.Contains("SPPA") || colUpper.Contains("PRESS") ||
                             colUpper.Contains("PUMP") || colUpper.Contains("TORQ") || colUpper.Contains("TQA") ||
                             colUpper.Contains("WOB") || colUpper.Contains("GAMMA") || colUpper == "GR") && dblVal < 0)
                        {
                            isRowValid = false;
                        }
                    }
                    else
                    {
                        cmdParams[i].Value = rawVal.Trim();
                        isRowValid = false;
                        if (columnPlans[i].IsDepthColumn)
                        {
                            depthIsNull = true;
                        }
                    }
                }

                if (isRowValid) validRows++;

                if (isTimeLog)
                {
                    // For TimeLog: insert row if DATETIME was successfully parsed (or if no explicit options configured)
                    if (dateTimeOptions == null || dateTimeParsedOk)
                    {
                        insertCmd.ExecuteNonQuery();
                    }
                }
                else if (!depthIsNull)
                {
                    insertCmd.ExecuteNonQuery();
                }

                if (totalRows % batchSize == 0)
                {
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = connection.BeginTransaction();
                    insertCmd.Transaction = transaction;

                    progress?.Report(new ImportProgressReport
                    {
                        RowsProcessed = totalRows,
                        StatusMessage = $"Imported {totalRows:N0} records...",
                        IsIndeterminate = true
                    });
                }
            }

            transaction.Commit();
            transaction.Dispose();
            transaction = null;
        }
        catch
        {
            if (transaction != null)
            {
                try { transaction.Rollback(); } catch { }
                transaction.Dispose();
                transaction = null;
            }
            throw;
        }

        double finalQcScore = totalRows > 0 ? ((double)validRows / totalRows * 100.0) : 0.0;
        string? stepIncrement = !string.IsNullOrEmpty(fileMetadata?.StepIncrement) ? fileMetadata.StepIncrement : calculatedStep;
        string? lastDataIndex = lastDepth.HasValue ? lastDepth.Value.ToString(CultureInfo.InvariantCulture) : null;

        progress?.Report(new ImportProgressReport
        {
            RowsProcessed = totalRows,
            PercentCompleted = 100,
            IsIndeterminate = false,
            StatusMessage = $"Completed! {totalRows:N0} rows imported."
        });

        return new StreamImportResult
        {
            TableName = tableName,
            TotalRows = totalRows,
            ValidRows = validRows,
            QcScore = finalQcScore,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
            FirstDepth = firstDepth,
            LastDepth = lastDepth,
            StepIncrement = stepIncrement,
            LastDataIndex = lastDataIndex,
            MinDate = minDate,
            MaxDate = maxDate,
            IsSequential = nonSequentialCount == 0,
            NonSequentialCount = nonSequentialCount,
            ValidationMessages = validationMessages
        };
    }

    public async Task<List<string>> GetTableColumnsAsync(string tableName)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(tableName))
            return new List<string>();

        var connection = _session.GetConnection();
        var columns = new List<string>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info([{tableName}]);";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(colName))
            {
                columns.Add(colName);
            }
        }

        return columns;
    }

    /// <summary>
    /// Updates an existing DepthLog target table with data from a source file.
    /// Strictly adheres to:
    /// 1. Only updates mapped VuMax columns (unmapped columns retain original DB values).
    /// 2. Does NOT create any new columns.
    /// 3. Does NOT alter existing column names in the target table.
    /// </summary>
    public async Task<StreamImportResult> StreamUpdateDepthDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        int columnHeadingRow,
        int importFromRow,
        string delimiter = ",",
        string? worksheetName = null,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsProjectOpen)
            throw new InvalidOperationException("No project is currently loaded.");

        if (columnHeadingRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnHeadingRow), "Column Heading Row is mandatory and must be a positive integer.");

        if (importFromRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(importFromRow), "Import from Row is mandatory and must be a positive integer.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Import file not found: {filePath}", filePath);

        var connection = _session.GetConnection();

        // 1. Validate DEPTH mapping (mandatory)
        var depthMapping = mappings.FirstOrDefault(m =>
            m.MappedVumaxChannel.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
            m.MappedVumaxChannel.Equals("Depth", StringComparison.OrdinalIgnoreCase));

        if (depthMapping == null || string.IsNullOrWhiteSpace(depthMapping.CsvColumnHeader))
        {
            throw new InvalidOperationException("You must map and select DEPTH channel. Please map and select the depth channel to continue");
        }

        // 2. Verify target table exists and retrieve existing columns
        bool tableExists;
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tName;";
            var pName = checkCmd.CreateParameter();
            pName.ParameterName = "@tName";
            pName.Value = tableName;
            checkCmd.Parameters.Add(pName);
            tableExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        if (!tableExists)
            throw new InvalidOperationException($"Target table '{tableName}' does not exist in the database.");

        var existingTableCols = new HashSet<string>(await GetTableColumnsAsync(tableName), StringComparer.OrdinalIgnoreCase);

        // 3. Filter mappings: ONLY mapped columns that exist in the target table (excluding DEPTH)
        // Strictly prevents creation of any new columns and prevents alteration of existing column names
        var validCurveMappings = mappings
            .Where(m => !m.MappedVumaxChannel.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) &&
                        !m.MappedVumaxChannel.Equals("Dynamic (New Column)", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(m.CsvColumnHeader) &&
                        existingTableCols.Contains(m.MappedVumaxChannel))
            .ToList();

        // 4. Resolve Reader and Headers
        var formatReaders = new IDepthLogFormatReader[]
        {
            new LasDepthReader(),
            new WitsmlDepthReader(),
            new ExcelDepthReader(),
            new CsvDepthReader()
        };
        var reader = formatReaders.FirstOrDefault(r => r.CanHandle(filePath)) ?? new CsvDepthReader();
        var fileMetadata = reader.ExtractMetadata(filePath);
        var sourceHeaders = reader.GetHeaders(filePath, columnHeadingRow, worksheetName, delimiter);

        // Map depth source column index
        int depthSourceIndex = sourceHeaders.FindIndex(h => h.Equals(depthMapping.CsvColumnHeader, StringComparison.OrdinalIgnoreCase));
        if (depthSourceIndex < 0 && depthMapping.CsvColumnHeader.StartsWith("#") && int.TryParse(depthMapping.CsvColumnHeader.Substring(1), out int numDepthIdx))
        {
            depthSourceIndex = numDepthIdx;
        }

        if (depthSourceIndex < 0)
        {
            throw new InvalidOperationException($"Source column '{depthMapping.CsvColumnHeader}' for DEPTH not found in file headers.");
        }

        // Map curve source column indices
        var curvePlans = new List<ColumnPlan>();
        for (int i = 0; i < validCurveMappings.Count; i++)
        {
            var map = validCurveMappings[i];
            int sIdx = sourceHeaders.FindIndex(h => h.Equals(map.CsvColumnHeader, StringComparison.OrdinalIgnoreCase));
            if (sIdx < 0 && map.CsvColumnHeader.StartsWith("#") && int.TryParse(map.CsvColumnHeader.Substring(1), out int numIdx))
            {
                sIdx = numIdx;
            }

            if (sIdx >= 0)
            {
                // Find exact casing from existing table columns
                var exactDbCol = existingTableCols.First(c => c.Equals(map.MappedVumaxChannel, StringComparison.OrdinalIgnoreCase));
                curvePlans.Add(new ColumnPlan
                {
                    SourceHeader = map.CsvColumnHeader,
                    DbColumnName = exactDbCol,
                    SourceIndex = sIdx,
                    IsDepthColumn = false,
                    IsHookloadColumn = exactDbCol.Equals("HKLD", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        // 5. Build Targeted Parameterized SQL
        // Ensure unique index on DEPTH exists for upsert
        try
        {
            using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS [{tableName}_PK] ON [{tableName}](DEPTH);";
            await idxCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        // Construct UPSERT statement:
        // Only updates the mapped VuMax columns; unmapped columns retain their database values.
        string sql;
        if (curvePlans.Count > 0)
        {
            var insertColNames = new List<string> { "[DEPTH]" };
            var insertParamNames = new List<string> { "@pDepth" };
            var updateSetClauses = new List<string>();

            for (int i = 0; i < curvePlans.Count; i++)
            {
                insertColNames.Add($"[{curvePlans[i].DbColumnName}]");
                insertParamNames.Add($"@p{i}");
                updateSetClauses.Add($"[{curvePlans[i].DbColumnName}] = excluded.[{curvePlans[i].DbColumnName}]");
            }

            sql = $"INSERT INTO [{tableName}] ({string.Join(", ", insertColNames)}) VALUES ({string.Join(", ", insertParamNames)}) " +
                  $"ON CONFLICT([DEPTH]) DO UPDATE SET {string.Join(", ", updateSetClauses)};";
        }
        else
        {
            // Only DEPTH is mapped
            sql = $"INSERT OR IGNORE INTO [{tableName}] ([DEPTH]) VALUES (@pDepth);";
        }

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = sql;

        var pDepth = updateCmd.CreateParameter();
        pDepth.ParameterName = "@pDepth";
        updateCmd.Parameters.Add(pDepth);

        var curveParams = new DbParameter[curvePlans.Count];
        for (int i = 0; i < curvePlans.Count; i++)
        {
            var p = updateCmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            updateCmd.Parameters.Add(p);
            curveParams[i] = p;
        }

        // 6. Stream rows and execute update
        int totalRows = 0;
        int validRows = 0;
        double? minDepth = null;
        double? maxDepth = null;
        double? firstDepth = null;
        double? lastDepth = null;
        double? prevDepth = null;
        string? calculatedStep = null;
        const int batchSize = 50000;

        DbTransaction? transaction = connection.BeginTransaction();
        updateCmd.Transaction = transaction;

        try
        {
            foreach (var tokens in reader.StreamDataRows(filePath, importFromRow, worksheetName, delimiter, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalRows++;

                string? rawDepth = (depthSourceIndex >= 0 && depthSourceIndex < tokens.Length) ? tokens[depthSourceIndex] : null;
                if (string.IsNullOrWhiteSpace(rawDepth) ||
                    rawDepth == "-999.25" ||
                    rawDepth == "-9999" ||
                    (fileMetadata?.NullValue != null && rawDepth == fileMetadata.NullValue) ||
                    !double.TryParse(rawDepth, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblDepth))
                {
                    // Invalid depth: skip row
                    continue;
                }

                pDepth.Value = dblDepth;
                bool isRowValid = true;

                if (!firstDepth.HasValue) firstDepth = dblDepth;
                if (prevDepth.HasValue && calculatedStep == null)
                {
                    double diff = Math.Abs(dblDepth - prevDepth.Value);
                    if (diff > 0.00001)
                    {
                        calculatedStep = diff.ToString("0.####", CultureInfo.InvariantCulture);
                    }
                }
                prevDepth = dblDepth;
                lastDepth = dblDepth;

                if (!minDepth.HasValue || dblDepth < minDepth.Value) minDepth = dblDepth;
                if (!maxDepth.HasValue || dblDepth > maxDepth.Value) maxDepth = dblDepth;

                // Bind curve parameters
                for (int i = 0; i < curvePlans.Count; i++)
                {
                    int sIdx = curvePlans[i].SourceIndex;
                    string? raw = (sIdx >= 0 && sIdx < tokens.Length) ? tokens[sIdx] : null;

                    if (string.IsNullOrWhiteSpace(raw) ||
                        raw == "-999.25" ||
                        raw == "-9999" ||
                        (fileMetadata?.NullValue != null && raw == fileMetadata.NullValue))
                    {
                        curveParams[i].Value = DBNull.Value;
                    }
                    else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    {
                        curveParams[i].Value = dblVal;
                        if (curvePlans[i].IsHookloadColumn && dblVal < 0) isRowValid = false;
                    }
                    else
                    {
                        curveParams[i].Value = raw.Trim();
                    }
                }

                if (isRowValid) validRows++;
                updateCmd.ExecuteNonQuery();

                if (totalRows % batchSize == 0)
                {
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = connection.BeginTransaction();
                    updateCmd.Transaction = transaction;

                    progress?.Report(new ImportProgressReport
                    {
                        RowsProcessed = totalRows,
                        StatusMessage = $"Updated {totalRows:N0} records...",
                        IsIndeterminate = true
                    });
                }
            }

            transaction.Commit();
            transaction.Dispose();
            transaction = null;
        }
        catch
        {
            if (transaction != null)
            {
                try { transaction.Rollback(); } catch { }
                transaction.Dispose();
                transaction = null;
            }
            throw;
        }

        double finalQcScore = totalRows > 0 ? ((double)validRows / totalRows * 100.0) : 0.0;
        string? stepIncrement = !string.IsNullOrEmpty(fileMetadata?.StepIncrement) ? fileMetadata.StepIncrement : calculatedStep;
        string? lastDataIndex = lastDepth.HasValue ? lastDepth.Value.ToString(CultureInfo.InvariantCulture) : null;

        progress?.Report(new ImportProgressReport
        {
            RowsProcessed = totalRows,
            PercentCompleted = 100,
            IsIndeterminate = false,
            StatusMessage = $"Update completed! {totalRows:N0} rows processed."
        });

        return new StreamImportResult
        {
            TableName = tableName,
            TotalRows = totalRows,
            QcScore = finalQcScore,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
            FirstDepth = firstDepth,
            LastDepth = lastDepth,
            StepIncrement = stepIncrement,
            LastDataIndex = lastDataIndex
        };
    }

    /// <summary>
    /// High-throughput streaming update for TimeLog tables.
    /// Follows legacy VuMax Main (frmMain.vb / ASCIILoader.vb) logic:
    /// - Mandatory DATETIME mapping (or configured via DateTime options)
    /// - Updates existing columns matching file channels via SQL UPSERT
    /// - Unmapped columns retain their database values
    /// - No new columns created during update
    /// </summary>
    public async Task<StreamImportResult> StreamUpdateTimeDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        int columnHeadingRow,
        int importFromRow,
        string delimiter = ",",
        string? worksheetName = null,
        TimeLogDateTimeOptions? dateTimeOptions = null,
        UpdateMethodType updateMethod = UpdateMethodType.DateTimeComaparision,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsProjectOpen)
            throw new InvalidOperationException("No project is currently loaded.");

        if (columnHeadingRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnHeadingRow), "Column Heading Row is mandatory and must be a positive integer.");

        if (importFromRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(importFromRow), "Import from Row is mandatory and must be a positive integer.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Import file not found: {filePath}", filePath);

        var connection = _session.GetConnection();

        // 1. Verify target table exists and retrieve existing columns
        bool tableExists;
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tName;";
            var pName = checkCmd.CreateParameter();
            pName.ParameterName = "@tName";
            pName.Value = tableName;
            checkCmd.Parameters.Add(pName);
            tableExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        if (!tableExists)
            throw new InvalidOperationException($"Target table '{tableName}' does not exist in the database.");

        var existingTableCols = new HashSet<string>(await GetTableColumnsAsync(tableName), StringComparer.OrdinalIgnoreCase);

        bool isDateTimeComparison = updateMethod == UpdateMethodType.DateTimeComaparision;
        string keyColName = isDateTimeComparison ? "DATETIME" : "DEPTH";

        // 2. Resolve Reader and Headers upfront
        var formatReaders = new IDepthLogFormatReader[]
        {
            new LasDepthReader(),
            new WitsmlDepthReader(),
            new ExcelDepthReader(),
            new CsvDepthReader()
        };
        var reader = formatReaders.FirstOrDefault(r => r.CanHandle(filePath)) ?? new CsvDepthReader();
        var fileMetadata = reader.ExtractMetadata(filePath);
        var sourceHeaders = reader.GetHeaders(filePath, columnHeadingRow, worksheetName, delimiter);

        // Auto-detect separate Date & Time columns if not explicitly configured
        string? separateDateColHeader = null;
        string? separateTimeColHeader = null;
        if (isDateTimeComparison)
        {
            if (dateTimeOptions != null && !dateTimeOptions.IsDatetimeInSeperatorColumn && dateTimeOptions.DateColNo.HasValue && dateTimeOptions.TimeColNo.HasValue)
            {
                dateTimeOptions.IsDatetimeInSeperatorColumn = true;
            }
            else if (dateTimeOptions == null || (!dateTimeOptions.IsDatetimeInSeperatorColumn && (!dateTimeOptions.DateColNo.HasValue || !dateTimeOptions.TimeColNo.HasValue)))
            {
                if (TimeLogDateTimeParser.TryDetectSeparateDateTimeColumns(sourceHeaders, out int dIdx, out int tIdx))
                {
                    dateTimeOptions ??= new TimeLogDateTimeOptions();
                    dateTimeOptions.IsDatetimeInSeperatorColumn = true;
                    dateTimeOptions.DateColNo = dIdx;
                    dateTimeOptions.TimeColNo = tIdx;
                }
            }

            if (dateTimeOptions != null && dateTimeOptions.IsDatetimeInSeperatorColumn)
            {
                var sampleRows = reader.GetPreviewRows(filePath, importFromRow, 20, worksheetName, delimiter);
                if (TimeLogDateTimeParser.TryResolveDateAndTimeHeaders(sourceHeaders, dateTimeOptions.DateColNo, dateTimeOptions.TimeColNo, out var resolvedDate, out var resolvedTime, out int resDIdx, out int resTIdx, sampleRows, dateTimeOptions.DateFormat))
                {
                    separateDateColHeader = resolvedDate;
                    separateTimeColHeader = resolvedTime;
                    dateTimeOptions.ResolvedDateColIdx = resDIdx;
                    dateTimeOptions.ResolvedTimeColIdx = resTIdx;
                }
            }
        }

        // 3. Validate mandatory Key Channel mapping based on comparison method
        ChannelMapping? keyMapping = null;
        if (isDateTimeComparison)
        {
            keyMapping = mappings.FirstOrDefault(m =>
                m.MappedVumaxChannel.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
                m.MappedVumaxChannel.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                m.MappedVumaxChannel.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                m.MappedVumaxChannel.Equals("DATE", StringComparison.OrdinalIgnoreCase));

            bool hasValidSplitOptions = dateTimeOptions != null &&
                ((dateTimeOptions.IsDatetimeInSeperatorColumn && dateTimeOptions.DateColNo.HasValue && dateTimeOptions.TimeColNo.HasValue && dateTimeOptions.DateColNo.Value != dateTimeOptions.TimeColNo.Value) ||
                 !string.IsNullOrWhiteSpace(dateTimeOptions.DatetimeSeparator));

            if (keyMapping == null && !hasValidSplitOptions)
            {
                throw new InvalidOperationException("You must map and select DATE/TIME channel. Please map and select these channels to continue");
            }
        }
        else
        {
            keyMapping = mappings.FirstOrDefault(m =>
                m.MappedVumaxChannel.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
                m.MappedVumaxChannel.Equals("Depth", StringComparison.OrdinalIgnoreCase));

            if (keyMapping == null || string.IsNullOrWhiteSpace(keyMapping.CsvColumnHeader))
            {
                throw new InvalidOperationException("You must map and select DEPTH channel. Please map and select the depth channel to continue");
            }
        }

        // 4. Filter mappings: ONLY mapped columns that exist in the target table (excluding key column, DATA_INDEX, and Dynamic)
        var validCurveMappings = mappings
            .Where(m => !m.MappedVumaxChannel.Equals(keyColName, StringComparison.OrdinalIgnoreCase) &&
                        !m.MappedVumaxChannel.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase) &&
                        !m.MappedVumaxChannel.Equals("Dynamic (New Column)", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(m.CsvColumnHeader) &&
                        existingTableCols.Contains(m.MappedVumaxChannel))
            .ToList();

        if (isDateTimeComparison && dateTimeOptions != null && (dateTimeOptions.IsDatetimeInSeperatorColumn || (dateTimeOptions.DateColNo.HasValue && dateTimeOptions.TimeColNo.HasValue)))
        {
            validCurveMappings = validCurveMappings.Where(m =>
            {
                if (separateDateColHeader != null && m.CsvColumnHeader.Equals(separateDateColHeader, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (separateTimeColHeader != null && m.CsvColumnHeader.Equals(separateTimeColHeader, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (dateTimeOptions.ResolvedDateColIdx.HasValue && dateTimeOptions.ResolvedDateColIdx.Value >= 0 && dateTimeOptions.ResolvedDateColIdx.Value < sourceHeaders.Count &&
                    m.CsvColumnHeader.Equals(sourceHeaders[dateTimeOptions.ResolvedDateColIdx.Value], StringComparison.OrdinalIgnoreCase))
                    return false;
                if (dateTimeOptions.ResolvedTimeColIdx.HasValue && dateTimeOptions.ResolvedTimeColIdx.Value >= 0 && dateTimeOptions.ResolvedTimeColIdx.Value < sourceHeaders.Count &&
                    m.CsvColumnHeader.Equals(sourceHeaders[dateTimeOptions.ResolvedTimeColIdx.Value], StringComparison.OrdinalIgnoreCase))
                    return false;
                if (m.MappedVumaxChannel.Equals("DATE", StringComparison.OrdinalIgnoreCase) ||
                    m.MappedVumaxChannel.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                    m.MappedVumaxChannel.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                    TimeLogDateTimeParser.LooksLikeDateHeader(m.CsvColumnHeader) ||
                    TimeLogDateTimeParser.LooksLikeDateHeader(m.MappedVumaxChannel) ||
                    TimeLogDateTimeParser.LooksLikeTimeHeader(m.CsvColumnHeader) ||
                    TimeLogDateTimeParser.LooksLikeTimeHeader(m.MappedVumaxChannel))
                    return false;
                return true;
            }).ToList();
        }

        int keySourceIndex = -1;
        if (keyMapping != null && !string.IsNullOrWhiteSpace(keyMapping.CsvColumnHeader))
        {
            keySourceIndex = sourceHeaders.FindIndex(h => h.Equals(keyMapping.CsvColumnHeader, StringComparison.OrdinalIgnoreCase));
            if (keySourceIndex < 0 && keyMapping.CsvColumnHeader.StartsWith("#") && int.TryParse(keyMapping.CsvColumnHeader.Substring(1), out int numIdx))
            {
                keySourceIndex = numIdx;
            }
        }

        if (dateTimeOptions != null && keySourceIndex >= 0 && dateTimeOptions.SingleDateTimeColIdx < 0)
        {
            dateTimeOptions.SingleDateTimeColIdx = keySourceIndex;
        }

        // Map curve source column indices
        var curvePlans = new List<ColumnPlan>();
        for (int i = 0; i < validCurveMappings.Count; i++)
        {
            var map = validCurveMappings[i];
            int sIdx = sourceHeaders.FindIndex(h => h.Equals(map.CsvColumnHeader, StringComparison.OrdinalIgnoreCase));
            if (sIdx < 0 && map.CsvColumnHeader.StartsWith("#") && int.TryParse(map.CsvColumnHeader.Substring(1), out int numIdx))
            {
                sIdx = numIdx;
            }

            if (sIdx >= 0)
            {
                var exactDbCol = existingTableCols.First(c => c.Equals(map.MappedVumaxChannel, StringComparison.OrdinalIgnoreCase));
                curvePlans.Add(new ColumnPlan
                {
                    SourceHeader = map.CsvColumnHeader,
                    DbColumnName = exactDbCol,
                    SourceIndex = sIdx,
                    IsDepthColumn = exactDbCol.Equals("DEPTH", StringComparison.OrdinalIgnoreCase),
                    IsHookloadColumn = exactDbCol.Equals("HKLD", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        // 5. Ensure unique index on key column for UPSERT
        try
        {
            using var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS [{tableName}_{keyColName}_PK] ON [{tableName}]([{keyColName}]);";
            await idxCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch { }

        // Construct UPSERT statement
        string sql;
        if (curvePlans.Count > 0)
        {
            var insertColNames = new List<string> { $"[{keyColName}]" };
            var insertParamNames = new List<string> { "@pKey" };
            var updateSetClauses = new List<string>();

            for (int i = 0; i < curvePlans.Count; i++)
            {
                insertColNames.Add($"[{curvePlans[i].DbColumnName}]");
                insertParamNames.Add($"@p{i}");
                updateSetClauses.Add($"[{curvePlans[i].DbColumnName}] = excluded.[{curvePlans[i].DbColumnName}]");
            }

            sql = $"INSERT INTO [{tableName}] ({string.Join(", ", insertColNames)}) " +
                  $"VALUES ({string.Join(", ", insertParamNames)}) " +
                  $"ON CONFLICT([{keyColName}]) DO UPDATE SET {string.Join(", ", updateSetClauses)};";
        }
        else
        {
            sql = $"INSERT OR IGNORE INTO [{tableName}] ([{keyColName}]) VALUES (@pKey);";
        }

        using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = sql;

        var pKey = updateCmd.CreateParameter();
        pKey.ParameterName = "@pKey";
        updateCmd.Parameters.Add(pKey);

        var curveParams = new DbParameter[curvePlans.Count];
        for (int i = 0; i < curvePlans.Count; i++)
        {
            var p = updateCmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            updateCmd.Parameters.Add(p);
            curveParams[i] = p;
        }

        // 6. Stream rows and execute update
        int totalRows = 0;
        int validRows = 0;
        int nonSequentialCount = 0;
        var validationMessages = new List<string>();
        DateTime? prevDateTime = null;
        string? prevFormattedDate = null;
        string? minDate = null;
        string? maxDate = null;
        double? minDepth = null;
        double? maxDepth = null;
        const int batchSize = 50000;

        DbTransaction? transaction = connection.BeginTransaction();
        updateCmd.Transaction = transaction;

        try
        {
            foreach (var tokens in reader.StreamDataRows(filePath, importFromRow, worksheetName, delimiter, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalRows++;

                bool isRowValid = true;

                if (isDateTimeComparison)
                {
                    string formattedDt;
                    DateTime parsedDt = default;
                    if (dateTimeOptions != null)
                    {
                        if (!TimeLogDateTimeParser.TryParse(tokens, dateTimeOptions, out parsedDt, out formattedDt))
                        {
                            continue; // Skip row if DATETIME cannot be parsed
                        }
                    }
                    else
                    {
                        string? rawDt = (keySourceIndex >= 0 && keySourceIndex < tokens.Length) ? tokens[keySourceIndex] : null;
                        if (string.IsNullOrWhiteSpace(rawDt)) continue;
                        formattedDt = rawDt.Trim();
                    }

                    pKey.Value = formattedDt;
                    if (minDate == null) minDate = formattedDt;
                    maxDate = formattedDt;

                    if (prevDateTime.HasValue && parsedDt != default)
                    {
                        if (parsedDt < prevDateTime.Value)
                        {
                            nonSequentialCount++;
                            if (validationMessages.Count < 20)
                            {
                                validationMessages.Add($"Row {totalRows}: Timestamp {formattedDt} is earlier than previous timestamp {prevFormattedDate}.");
                            }
                        }
                    }
                    if (parsedDt != default) prevDateTime = parsedDt;
                    prevFormattedDate = formattedDt;
                }
                else
                {
                    string? rawDepth = (keySourceIndex >= 0 && keySourceIndex < tokens.Length) ? tokens[keySourceIndex] : null;
                    if (string.IsNullOrWhiteSpace(rawDepth) || !double.TryParse(rawDepth, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblDepth))
                    {
                        continue;
                    }
                    pKey.Value = dblDepth;
                    if (!minDepth.HasValue || dblDepth < minDepth.Value) minDepth = dblDepth;
                    if (!maxDepth.HasValue || dblDepth > maxDepth.Value) maxDepth = dblDepth;
                }

                // Bind curve parameters
                for (int i = 0; i < curvePlans.Count; i++)
                {
                    int sIdx = curvePlans[i].SourceIndex;
                    string? raw = (sIdx >= 0 && sIdx < tokens.Length) ? tokens[sIdx] : null;

                    if (string.IsNullOrWhiteSpace(raw) ||
                        raw == "-999.25" ||
                        raw == "-9999" ||
                        (fileMetadata?.NullValue != null && raw == fileMetadata.NullValue))
                    {
                        curveParams[i].Value = DBNull.Value;
                    }
                    else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    {
                        curveParams[i].Value = dblVal;
                        if (double.IsNaN(dblVal) || double.IsInfinity(dblVal)) isRowValid = false;
                        if (curvePlans[i].IsHookloadColumn && dblVal < 0) isRowValid = false;
                        if (curvePlans[i].IsDepthColumn && dblVal < 0) isRowValid = false;
                    }
                    else
                    {
                        curveParams[i].Value = raw.Trim();
                    }
                }

                if (isRowValid) validRows++;
                updateCmd.ExecuteNonQuery();

                if (totalRows % batchSize == 0)
                {
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = connection.BeginTransaction();
                    updateCmd.Transaction = transaction;

                    progress?.Report(new ImportProgressReport
                    {
                        RowsProcessed = totalRows,
                        StatusMessage = $"Updated {totalRows:N0} records...",
                        IsIndeterminate = true
                    });
                }
            }

            transaction.Commit();
            transaction.Dispose();
            transaction = null;
        }
        catch
        {
            if (transaction != null)
            {
                try { transaction.Rollback(); } catch { }
                transaction.Dispose();
                transaction = null;
            }
            throw;
        }

        double finalQcScore = totalRows > 0 ? ((double)validRows / totalRows * 100.0) : 0.0;

        progress?.Report(new ImportProgressReport
        {
            RowsProcessed = totalRows,
            PercentCompleted = 100,
            IsIndeterminate = false,
            StatusMessage = $"Update completed! {totalRows:N0} rows processed."
        });

        return new StreamImportResult
        {
            TableName = tableName,
            TotalRows = totalRows,
            ValidRows = validRows,
            QcScore = finalQcScore,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
            MinDate = minDate,
            MaxDate = maxDate,
            IsSequential = nonSequentialCount == 0,
            NonSequentialCount = nonSequentialCount,
            ValidationMessages = validationMessages
        };
    }

    public async Task<DataTable> GetLogDataTableAsync(string tableName, int limitRows = 1000)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(tableName))
            return new DataTable();

        return await Task.Run(() =>
        {
            try
            {
                var ds = _session.GetDataService();
                string cleanTable = tableName.Trim().Trim('[', ']');
                string sql = limitRows > 0
                    ? $"SELECT * FROM [{cleanTable}] LIMIT {limitRows};"
                    : $"SELECT * FROM [{cleanTable}];";
                return ds.GetTable(sql) ?? new DataTable();
            }
            catch
            {
                return new DataTable();
            }
        });
    }

    // --- [NEW LOGIC (TimeLog and LogChannel operations using TimeLogService and SQLite)] ---
    public async Task<TimeLog?> GetTimeLogAsync(string logId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(logId)) return null;
        var dataService = _session.GetDataService();
        if (dataService == null) return null;

        return await Task.Run(() =>
        {
            string lastError = "";
            var log = TimeLogService.LoadObject(dataService, logId, ref lastError);
            if (log != null) return log;

            // Fallback by LOG_NAME or DATA_TABLE_NAME if logId was passed as a name
            var dt = dataService.GetTable("SELECT WELL_ID, WELLBORE_ID, LOG_ID FROM VMX_TIME_LOG WHERE LOG_NAME = '" 
                + logId.Replace("'", "''") + "' OR DATA_TABLE_NAME = '" 
                + logId.Replace("'", "''") + "' LIMIT 1;");
            if (dt != null && dt.Rows.Count > 0)
            {
                string foundLogId = DataService.checkNull(dt.Rows[0]["LOG_ID"], "");
                string wId = DataService.checkNull(dt.Rows[0]["WELL_ID"], "");
                string wbId = DataService.checkNull(dt.Rows[0]["WELLBORE_ID"], "");
                dt.Dispose();
                return TimeLogService.LoadObject(dataService, wId, wbId, foundLogId, ref lastError);
            }

            return null;
        });
    }

    public async Task<List<LogChannel>> GetLogChannelsAsync(string logId, string? dataTableName)
    {
        var log = await GetTimeLogAsync(logId);
        if (log != null && log.logCurves.Count > 0)
        {
            return log.logCurves.Values.OrderBy(c => c.ColumnOrder).ToList();
        }

        var dataService = _session.GetDataService();
        if (dataService != null)
        {
            if (log == null)
            {
                log = new TimeLog { ObjectID = logId, __dataTableName = dataTableName ?? "" };
            }
            TimeLogService.LoadLogCurves(dataService, log);
            return log.logCurves.Values.OrderBy(c => c.ColumnOrder).ToList();
        }

        return new List<LogChannel>();
    }

    public async Task SaveTimeLogAsync(TimeLog timeLog, List<LogChannel> channels)
    {
        if (!_session.IsProjectOpen || timeLog == null || string.IsNullOrWhiteSpace(timeLog.ObjectID)) return;
        var dataService = _session.GetDataService();
        if (dataService == null) return;

        await Task.Run(() =>
        {
            string lastError = "";
            TimeLogService.SaveTimeLog(dataService, timeLog, channels, ref lastError);
        });
    }

    // --- [OLD LOGIC (Legacy TimeLogEditMetadata methods maintained for backwards compatibility)] ---
    public async Task<TimeLogEditMetadata?> GetTimeLogEditMetadataAsync(string logId)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(logId)) return null;
        var connection = _session.GetConnection();

        // 1. Query vmx_time_log for the selected Timelog ID
        var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM VMX_TIME_LOG WHERE LOG_ID = @LogId LIMIT 1;",
            new { LogId = logId });

        if (row == null)
        {
            // Fallback by LOG_NAME or DATA_TABLE_NAME if logId was passed as a name
            row = await connection.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT * FROM VMX_TIME_LOG WHERE LOG_NAME = @LogId OR DATA_TABLE_NAME = @LogId LIMIT 1;",
                new { LogId = logId });
        }

        if (row == null)
        {
            // Fallback to VMX_TIME_LOG_SUMMARY
            var hasSummary = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
            if (hasSummary > 0)
            {
                var sumRow = await connection.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT * FROM VMX_TIME_LOG_SUMMARY WHERE LogId = @LogId OR LogName = @LogId LIMIT 1;",
                    new { LogId = logId });
                if (sumRow != null)
                {
                    var sDict = (IDictionary<string, object>)sumRow;
                    return new TimeLogEditMetadata
                    {
                        LogId = logId,
                        LogName = sDict.TryGetValue("LogName", out var nm) && nm != null ? Convert.ToString(nm)! : "",
                        DataTableName = sDict.TryGetValue("DataTableName", out var dt) && dt != null ? Convert.ToString(dt)! : "",
                        Description = sDict.TryGetValue("ImportStatus", out var st) && st != null ? Convert.ToString(st)! : "",
                        NoAutoCalc = true
                    };
                }
            }
            return null;
        }

        var dict = (IDictionary<string, object>)row;

        string GetStr(string col1, string? col2 = null)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Key.Equals(col1, StringComparison.OrdinalIgnoreCase) && kvp.Value != null && kvp.Value != DBNull.Value)
                    return Convert.ToString(kvp.Value)!;
                if (col2 != null && kvp.Key.Equals(col2, StringComparison.OrdinalIgnoreCase) && kvp.Value != null && kvp.Value != DBNull.Value)
                    return Convert.ToString(kvp.Value)!;
            }
            return string.Empty;
        }

        bool GetBool(string col1, string? col2 = null)
        {
            foreach (var kvp in dict)
            {
                if ((kvp.Key.Equals(col1, StringComparison.OrdinalIgnoreCase) || (col2 != null && kvp.Key.Equals(col2, StringComparison.OrdinalIgnoreCase))) && kvp.Value != null && kvp.Value != DBNull.Value)
                {
                    if (kvp.Value is bool b) return b;
                    if (long.TryParse(Convert.ToString(kvp.Value), out var l)) return l != 0;
                    return true;
                }
            }
            return false;
        }

        double GetDouble(string col1, string? col2 = null)
        {
            foreach (var kvp in dict)
            {
                if ((kvp.Key.Equals(col1, StringComparison.OrdinalIgnoreCase) || (col2 != null && kvp.Key.Equals(col2, StringComparison.OrdinalIgnoreCase))) && kvp.Value != null && kvp.Value != DBNull.Value)
                {
                    if (double.TryParse(Convert.ToString(kvp.Value), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        return d;
                }
            }
            return 0;
        }

        int dupCode = 2; // Default to Merge Columns
        foreach (var kvp in dict)
        {
            if (kvp.Key.Equals("DUPLICATE_ACTION", StringComparison.OrdinalIgnoreCase) && kvp.Value != null && kvp.Value != DBNull.Value)
            {
                int.TryParse(Convert.ToString(kvp.Value), out dupCode);
                break;
            }
        }

        string dupActionStr = dupCode switch
        {
            1 => "Ignore",
            0 => "Replace",
            2 => "Merge Columns",
            _ => "Merge Columns"
        };

        return new TimeLogEditMetadata
        {
            LogId = GetStr("LOG_ID", "log_id"),
            WellId = GetStr("WELL_ID", "well_id"),
            WellboreId = GetStr("WELLBORE_ID", "wellbore_id"),
            LogName = GetStr("LOG_NAME", "log_name"),
            ServiceCompany = GetStr("SERVICE_COMPANY", "service_company"),
            EdrProvider = GetStr("EDR_PROVIDER", "edr_provider"),
            RunNo = GetStr("RUN_NO", "run_no"),
            Description = GetStr("DESCRIPTION", "description"),
            PrimaryLog = GetBool("PRIMARY_LOG", "primary_log"),
            RemarksLog = GetBool("REMARKS_LOG", "remarks_log"),
            NoAutoCalc = GetBool("DONT_CALC_HDTH", "no_auto_calc"),
            StartingHoleDepth = GetDouble("STARTING_HDTH", "starting_hole_depth"),
            DataTableName = GetStr("DATA_TABLE_NAME", "data_table_name"),
            LinkToParent = GetBool("LINK_TO_PARENT", "link_to_parent"),
            LinkWellId = GetStr("LINK_WELL_ID", "link_well_id"),
            LinkWellboreId = GetStr("LINK_WELLBORE_ID", "link_wellbore_id"),
            LinkLogId = GetStr("LINK_LOG_ID", "link_log_id"),
            DontMoveAhead = GetBool("DONT_MOVE_AHEAD", "dont_move_ahead"),
            DuplicateAction = dupActionStr
        };
    }

    public async Task<List<TimelogChannelItem>> GetTimeLogChannelsAsync(string logId, string? dataTableName)
    {
        var channels = new List<TimelogChannelItem>();
        if (!_session.IsProjectOpen) return channels;
        var connection = _session.GetConnection();

        bool dataTableIsTimeSeries = false;

        // 1. Query all rows from DATA_TABLE_NAME if specified
        if (!string.IsNullOrWhiteSpace(dataTableName))
        {
            var hasTable = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @TableName;",
                new { TableName = dataTableName });

            if (hasTable > 0)
            {
                try
                {
                    var tableCols = (await connection.QueryAsync<string>(
                        $"SELECT name FROM pragma_table_info('{dataTableName}');")).ToList();

                    // Check if DATA_TABLE_NAME rows represent channel definitions
                    var mnemCol = tableCols.FirstOrDefault(c => c.Equals("MNEMONIC", StringComparison.OrdinalIgnoreCase) || c.Equals("Mnemonic", StringComparison.OrdinalIgnoreCase));

                    if (mnemCol != null)
                    {
                        var rows = (await connection.QueryAsync<dynamic>($"SELECT * FROM [{dataTableName}];")).ToList();
                        foreach (var r in rows)
                        {
                            var rDict = (IDictionary<string, object>)r;
                            var item = new TimelogChannelItem();
                            foreach (var kvp in rDict)
                            {
                                if (kvp.Value == null || kvp.Value == DBNull.Value) continue;
                                var k = kvp.Key;
                                var v = kvp.Value;

                                if (k.Equals("Upload", StringComparison.OrdinalIgnoreCase) || k.Equals("WRITE_BACK", StringComparison.OrdinalIgnoreCase))
                                    item.Upload = Convert.ToInt64(v) != 0 || (v is bool b && b);
                                else if (k.Equals("Mnemonic", StringComparison.OrdinalIgnoreCase) || k.Equals("MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                    item.Mnemonic = Convert.ToString(v) ?? "";
                                else if (k.Equals("Unit", StringComparison.OrdinalIgnoreCase) || k.Equals("UNIT", StringComparison.OrdinalIgnoreCase))
                                    item.Unit = Convert.ToString(v) ?? "";
                                else if (k.Equals("VuMax Unit ID", StringComparison.OrdinalIgnoreCase) || k.Equals("VUMAX_UNIT_ID", StringComparison.OrdinalIgnoreCase) || k.Equals("UnitID", StringComparison.OrdinalIgnoreCase))
                                    item.VuMaxUnitId = Convert.ToString(v) ?? "";
                                else if (k.Equals("Description", StringComparison.OrdinalIgnoreCase) || k.Equals("CHANNEL_NAME", StringComparison.OrdinalIgnoreCase) || k.Equals("curveDescription", StringComparison.OrdinalIgnoreCase))
                                    item.Description = Convert.ToString(v) ?? "";
                                else if (k.Equals("Upload Mnemonic", StringComparison.OrdinalIgnoreCase) || k.Equals("WITSML_MNEMONIC", StringComparison.OrdinalIgnoreCase) || k.Equals("PI_MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                    item.UploadMnemonic = Convert.ToString(v) ?? "";
                                else if (k.Equals("Value Type", StringComparison.OrdinalIgnoreCase) || k.Equals("VALUE_TYPE", StringComparison.OrdinalIgnoreCase))
                                    item.ValueType = Convert.ToString(v) ?? "0";
                                else if (k.Equals("Expression", StringComparison.OrdinalIgnoreCase) || k.Equals("VALUE_QUERY", StringComparison.OrdinalIgnoreCase))
                                    item.Expression = Convert.ToString(v) ?? "";
                                else if (k.Equals("Do Not Interpol", StringComparison.OrdinalIgnoreCase) || k.Equals("NO_INTERPOLATE", StringComparison.OrdinalIgnoreCase))
                                    item.DoNotInterpol = Convert.ToInt64(v) != 0 || (v is bool b && b);
                            }
                            item.OriginalMnemonic = item.Mnemonic;
                            channels.Add(item);
                        }
                        return channels;
                    }
                    else
                    {
                        dataTableIsTimeSeries = true;
                    }
                }
                catch { }
            }
        }

        // 2. Query VMX_TIME_LOG_COLUMNS
        var hasVmxCols = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_COLUMNS';");

        var loadedMnemonics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasVmxCols > 0)
        {
            var colRows = (await connection.QueryAsync<dynamic>(
                "SELECT * FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID = @LogId ORDER BY COLUMN_ORDER ASC, MNEMONIC ASC;",
                new { LogId = logId })).ToList();

            foreach (var r in colRows)
            {
                var dict = (IDictionary<string, object>)r;
                string mnem = dict.TryGetValue("MNEMONIC", out var mVal) && mVal != null ? Convert.ToString(mVal)! : "";
                if (string.IsNullOrWhiteSpace(mnem)) continue;

                bool upload = true;
                if (dict.TryGetValue("WRITE_BACK", out var wbVal) && wbVal != null && wbVal != DBNull.Value)
                {
                    upload = Convert.ToInt64(wbVal) != 0;
                }

                string unit = dict.TryGetValue("UNIT", out var uVal) && uVal != null ? Convert.ToString(uVal)! : "";
                string vumaxUnitId = dict.TryGetValue("VUMAX_UNIT_ID", out var vVal) && vVal != null ? Convert.ToString(vVal)! : (dict.TryGetValue("UNIT_ID", out var uid) && uid != null ? Convert.ToString(uid)! : "");
                string desc = dict.TryGetValue("CHANNEL_NAME", out var cVal) && cVal != null ? Convert.ToString(cVal)! : "";
                string upMnem = dict.TryGetValue("WITSML_MNEMONIC", out var wVal) && wVal != null ? Convert.ToString(wVal)! : (dict.TryGetValue("PI_MNEMONIC", out var piVal) && piVal != null ? Convert.ToString(piVal)! : mnem);
                string valType = dict.TryGetValue("VALUE_TYPE", out var vtVal) && vtVal != null ? Convert.ToString(vtVal)! : "0";
                string expr = dict.TryGetValue("VALUE_QUERY", out var vqVal) && vqVal != null ? Convert.ToString(vqVal)! : "";
                bool doNotInterpol = dict.TryGetValue("NO_INTERPOLATE", out var niVal) && niVal != null && niVal != DBNull.Value && Convert.ToInt64(niVal) != 0;
                string dataType = dict.TryGetValue("DATA_TYPE", out var dtVal) && dtVal != null ? Convert.ToString(dtVal)! : "Double";
                int order = dict.TryGetValue("COLUMN_ORDER", out var ordVal) && ordVal != null && ordVal != DBNull.Value ? Convert.ToInt32(ordVal) : channels.Count + 1;

                channels.Add(new TimelogChannelItem
                {
                    Upload = upload,
                    Mnemonic = mnem,
                    Unit = unit,
                    VuMaxUnitId = vumaxUnitId,
                    Description = desc,
                    UploadMnemonic = upMnem,
                    ValueType = valType,
                    Expression = expr,
                    DoNotInterpol = doNotInterpol,
                    DataType = dataType,
                    ColumnOrder = order,
                    OriginalMnemonic = mnem
                });
                loadedMnemonics.Add(mnem);
            }
        }

        // 3. If DATA_TABLE_NAME exists and contains columns not yet in channels, include them
        if (dataTableIsTimeSeries && !string.IsNullOrWhiteSpace(dataTableName))
        {
            try
            {
                var cols = await GetTableColumnsAsync(dataTableName);
                int order = channels.Count + 1;
                foreach (var col in cols)
                {
                    if (col.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!loadedMnemonics.Contains(col))
                    {
                        bool isDt = col.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
                                    col.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                                    col.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                                    col.Equals("DATE", StringComparison.OrdinalIgnoreCase);

                        channels.Add(new TimelogChannelItem
                        {
                            Upload = true,
                            Mnemonic = col,
                            Unit = isDt ? "" : (col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                            VuMaxUnitId = isDt ? "" : (col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                            Description = col,
                            UploadMnemonic = col,
                            ValueType = "0",
                            Expression = "",
                            DoNotInterpol = false,
                            DataType = isDt ? "DateTime" : "Double",
                            ColumnOrder = order++,
                            OriginalMnemonic = col
                        });
                        loadedMnemonics.Add(col);
                    }
                }
            }
            catch { }
        }

        return channels;
    }

    public async Task SaveTimeLogEditAsync(TimeLogEditMetadata metadata, List<TimelogChannelItem> channels)
    {
        if (!_session.IsProjectOpen || string.IsNullOrWhiteSpace(metadata.LogId)) return;
        var connection = _session.GetConnection();

        int dupCode = metadata.DuplicateAction switch
        {
            "Ignore" => 1,
            "Replace" => 0,
            "Merge Columns" => 2,
            _ => 2
        };

        var vmxCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_TIME_LOG');")).ToList();

        bool hasNoAutoCalc = vmxCols.Contains("no_auto_calc", StringComparer.OrdinalIgnoreCase);
        bool hasDontCalcHdth = vmxCols.Contains("DONT_CALC_HDTH", StringComparer.OrdinalIgnoreCase);
        bool hasStartingHoleDepth = vmxCols.Contains("starting_hole_depth", StringComparer.OrdinalIgnoreCase);
        bool hasStartingHdth = vmxCols.Contains("STARTING_HDTH", StringComparer.OrdinalIgnoreCase);

        var updateSql = @"
            UPDATE VMX_TIME_LOG SET
                LOG_NAME = @LogName,
                SERVICE_COMPANY = @ServiceCompany,
                EDR_PROVIDER = @EdrProvider,
                RUN_NO = @RunNo,
                DESCRIPTION = @Description,
                PRIMARY_LOG = @PrimaryLog,
                REMARKS_LOG = @RemarksLog,
                LINK_TO_PARENT = @LinkToParent,
                LINK_WELL_ID = @LinkWellId,
                LINK_WELLBORE_ID = @LinkWellboreId,
                LINK_LOG_ID = @LinkLogId,
                DONT_MOVE_AHEAD = @DontMoveAhead,
                DUPLICATE_ACTION = @DuplicateAction,
                MODIFIED_DATE = @ModifiedDate"
            + (hasDontCalcHdth ? ", DONT_CALC_HDTH = @NoAutoCalc" : "")
            + (hasNoAutoCalc ? ", no_auto_calc = @NoAutoCalc" : "")
            + (hasStartingHdth ? ", STARTING_HDTH = @StartingHoleDepth" : "")
            + (hasStartingHoleDepth ? ", starting_hole_depth = @StartingHoleDepth" : "")
            + " WHERE LOG_ID = @LogId;";

        await connection.ExecuteAsync(updateSql, new
        {
            LogName = metadata.LogName,
            ServiceCompany = metadata.ServiceCompany,
            EdrProvider = metadata.EdrProvider,
            RunNo = metadata.RunNo,
            Description = metadata.Description,
            PrimaryLog = metadata.PrimaryLog ? 1 : 0,
            RemarksLog = metadata.RemarksLog ? 1 : 0,
            LinkToParent = metadata.LinkToParent ? 1 : 0,
            LinkWellId = metadata.LinkWellId,
            LinkWellboreId = metadata.LinkWellboreId,
            LinkLogId = metadata.LinkLogId,
            DontMoveAhead = metadata.DontMoveAhead ? 1 : 0,
            DuplicateAction = dupCode,
            ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
            NoAutoCalc = metadata.NoAutoCalc ? 1 : 0,
            StartingHoleDepth = metadata.StartingHoleDepth,
            LogId = metadata.LogId
        });

        // Also update summary table
        var hasSummary = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
        if (hasSummary > 0)
        {
            await connection.ExecuteAsync(
                "UPDATE VMX_TIME_LOG_SUMMARY SET LogName = @LogName WHERE LogId = @LogId;",
                new { LogName = metadata.LogName, LogId = metadata.LogId });
        }

        // Persist channels into VMX_TIME_LOG_COLUMNS
        var hasColsTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_COLUMNS';");
        if (hasColsTable > 0)
        {
            await connection.ExecuteAsync(
                "DELETE FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID = @LogId;",
                new { LogId = metadata.LogId });

            var insertColSql = @"
                INSERT INTO VMX_TIME_LOG_COLUMNS (
                    WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE,
                    UNIT, UNIT_ID, VUMAX_UNIT_ID, VALUE_TYPE, VALUE_QUERY, WITSML_MNEMONIC,
                    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, COLUMN_ORDER,
                    WRITE_BACK, NO_INTERPOLATE
                ) VALUES (
                    @WellId, @WellboreId, @LogId, @Mnemonic, @ChannelName, @DataType,
                    @Unit, @UnitId, @VuMaxUnitId, @ValueType, @ValueQuery, @WitsmlMnemonic,
                    'System', @CreatedDate, 'System', @ModifiedDate, @ColumnOrder,
                    @WriteBack, @NoInterpolate
                );";

            int order = 1;
            foreach (var ch in channels)
            {
                int vt = 0;
                if (int.TryParse(ch.ValueType, out var parsedVt)) vt = parsedVt;
                else if (ch.ValueType.Contains("Query", StringComparison.OrdinalIgnoreCase) || ch.ValueType.Contains("Calc", StringComparison.OrdinalIgnoreCase)) vt = 1;

                await connection.ExecuteAsync(insertColSql, new
                {
                    WellId = metadata.WellId,
                    WellboreId = metadata.WellboreId,
                    LogId = metadata.LogId,
                    Mnemonic = ch.Mnemonic,
                    ChannelName = ch.Description,
                    DataType = string.IsNullOrWhiteSpace(ch.DataType) ? "Double" : ch.DataType,
                    Unit = ch.Unit,
                    UnitId = ch.VuMaxUnitId,
                    VuMaxUnitId = ch.VuMaxUnitId,
                    ValueType = vt,
                    ValueQuery = ch.Expression,
                    WitsmlMnemonic = string.IsNullOrWhiteSpace(ch.UploadMnemonic) ? ch.Mnemonic : ch.UploadMnemonic,
                    CreatedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ColumnOrder = order++,
                    WriteBack = ch.Upload ? 1 : 0,
                    NoInterpolate = ch.DoNotInterpol ? 1 : 0
                });
            }
        }

        // If DATA_TABLE_NAME stores channel rows directly (has MNEMONIC column), update it too
        if (!string.IsNullOrWhiteSpace(metadata.DataTableName))
        {
            var hasDt = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name = @TableName;",
                new { TableName = metadata.DataTableName });
            if (hasDt > 0)
            {
                try
                {
                    var dtCols = (await connection.QueryAsync<string>(
                        $"SELECT name FROM pragma_table_info('{metadata.DataTableName}');")).ToList();
                    if (dtCols.Contains("MNEMONIC", StringComparer.OrdinalIgnoreCase))
                    {
                        await connection.ExecuteAsync($"DELETE FROM [{metadata.DataTableName}];");
                        foreach (var ch in channels)
                        {
                            var rowCols = new List<string>();
                            var rowVals = new List<string>();
                            var p = new DynamicParameters();

                            void AddIfCol(string col, object? val)
                            {
                                if (dtCols.Contains(col, StringComparer.OrdinalIgnoreCase))
                                {
                                    rowCols.Add($"[{col}]");
                                    rowVals.Add($"@{col}");
                                    p.Add(col, val);
                                }
                            }

                            AddIfCol("Upload", ch.Upload ? 1 : 0);
                            AddIfCol("WRITE_BACK", ch.Upload ? 1 : 0);
                            AddIfCol("Mnemonic", ch.Mnemonic);
                            AddIfCol("MNEMONIC", ch.Mnemonic);
                            AddIfCol("Unit", ch.Unit);
                            AddIfCol("UNIT", ch.Unit);
                            AddIfCol("VuMax Unit ID", ch.VuMaxUnitId);
                            AddIfCol("VUMAX_UNIT_ID", ch.VuMaxUnitId);
                            AddIfCol("Description", ch.Description);
                            AddIfCol("CHANNEL_NAME", ch.Description);
                            AddIfCol("Upload Mnemonic", ch.UploadMnemonic);
                            AddIfCol("WITSML_MNEMONIC", ch.UploadMnemonic);
                            AddIfCol("Value Type", ch.ValueType);
                            AddIfCol("VALUE_TYPE", ch.ValueType);
                            AddIfCol("Expression", ch.Expression);
                            AddIfCol("VALUE_QUERY", ch.Expression);
                            AddIfCol("Do Not Interpol", ch.DoNotInterpol ? 1 : 0);
                            AddIfCol("NO_INTERPOLATE", ch.DoNotInterpol ? 1 : 0);

                            if (rowCols.Count > 0)
                            {
                                string insSql = $"INSERT INTO [{metadata.DataTableName}] ({string.Join(", ", rowCols)}) VALUES ({string.Join(", ", rowVals)});";
                                await connection.ExecuteAsync(insSql, p);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        try { await connection.ExecuteAsync("PRAGMA wal_checkpoint(FULL);"); } catch { }
        _session.NotifyDataChanged();
    }

    public async Task<List<WellOption>> GetWellsForLinkingAsync()
    {
        var list = new List<WellOption>();
        if (!_session.IsProjectOpen) return list;
        var connection = _session.GetConnection();

        var hasWellTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_WELL';");
        if (hasWellTable > 0)
        {
            var rows = await connection.QueryAsync<dynamic>("SELECT WELL_ID, WELL_NAME FROM VMX_WELL ORDER BY WELL_NAME;");
            foreach (var r in rows)
            {
                var dict = (IDictionary<string, object>)r;
                string wId = dict.TryGetValue("WELL_ID", out var idVal) && idVal != null ? Convert.ToString(idVal)! : "";
                string wName = dict.TryGetValue("WELL_NAME", out var nmVal) && nmVal != null ? Convert.ToString(nmVal)! : "";
                if (!string.IsNullOrWhiteSpace(wId) || !string.IsNullOrWhiteSpace(wName))
                {
                    list.Add(new WellOption { WellId = wId, WellName = !string.IsNullOrWhiteSpace(wName) ? wName : wId });
                }
            }
        }

        if (list.Count == 0)
        {
            var projectWell = await GetProjectWellAsync();
            if (projectWell != null)
            {
                list.Add(new WellOption
                {
                    WellId = projectWell.ObjectID,
                    WellName = projectWell.WellName
                });
            }
        }

        return list;
    }

    public async Task<List<WellboreOption>> GetWellboresForLinkingAsync(string? wellId = null)
    {
        var list = new List<WellboreOption>();
        if (!_session.IsProjectOpen) return list;
        var connection = _session.GetConnection();

        var hasWbTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_WELLBORE';");
        if (hasWbTable > 0)
        {
            string sql = string.IsNullOrWhiteSpace(wellId)
                ? "SELECT WELLBORE_ID, WELL_ID, WELLBORE_NAME FROM VMX_WELLBORE ORDER BY WELLBORE_NAME;"
                : "SELECT WELLBORE_ID, WELL_ID, WELLBORE_NAME FROM VMX_WELLBORE WHERE WELL_ID = @WellId ORDER BY WELLBORE_NAME;";

            var rows = await connection.QueryAsync<dynamic>(sql, new { WellId = wellId });
            foreach (var r in rows)
            {
                var dict = (IDictionary<string, object>)r;
                string wbId = dict.TryGetValue("WELLBORE_ID", out var idVal) && idVal != null ? Convert.ToString(idVal)! : "";
                string wId = dict.TryGetValue("WELL_ID", out var wVal) && wVal != null ? Convert.ToString(wVal)! : "";
                string wbName = dict.TryGetValue("WELLBORE_NAME", out var nmVal) && nmVal != null ? Convert.ToString(nmVal)! : "";
                if (!string.IsNullOrWhiteSpace(wbId) || !string.IsNullOrWhiteSpace(wbName))
                {
                    list.Add(new WellboreOption
                    {
                        WellboreId = wbId,
                        WellId = wId,
                        WellboreName = !string.IsNullOrWhiteSpace(wbName) ? wbName : wbId
                    });
                }
            }
        }

        if (list.Count == 0)
        {
            // Default fallback wellbore
            list.Add(new WellboreOption
            {
                WellboreId = "WB-01",
                WellId = wellId ?? "",
                WellboreName = "Default Wellbore"
            });
        }

        return list;
    }

    public async Task<List<TimeLogOption>> GetTimeLogsForLinkingAsync(string? wellId = null, string? wellboreId = null, string? excludeLogId = null)
    {
        var list = new List<TimeLogOption>();
        if (!_session.IsProjectOpen) return list;
        var connection = _session.GetConnection();

        var hasTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");

        if (hasTable > 0)
        {
            var rows = await connection.QueryAsync<dynamic>("SELECT LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME FROM VMX_TIME_LOG;");
            foreach (var r in rows)
            {
                var dict = (IDictionary<string, object>)r;
                string id = dict.TryGetValue("LOG_ID", out var idVal) && idVal != null ? Convert.ToString(idVal)! : "";
                string name = dict.TryGetValue("LOG_NAME", out var nmVal) && nmVal != null ? Convert.ToString(nmVal)! : "";
                string wId = dict.TryGetValue("WELL_ID", out var wVal) && wVal != null ? Convert.ToString(wVal)! : "";
                string wbId = dict.TryGetValue("WELLBORE_ID", out var wbVal) && wbVal != null ? Convert.ToString(wbVal)! : "";

                if (!string.IsNullOrWhiteSpace(excludeLogId) && id.Equals(excludeLogId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(wellId) && !wId.Equals(wellId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(wellboreId) && !wbId.Equals(wellboreId, StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(new TimeLogOption
                {
                    LogId = id,
                    LogName = !string.IsNullOrWhiteSpace(name) ? name : id,
                    WellId = wId,
                    WellboreId = wbId
                });
            }
        }

        if (list.Count == 0)
        {
            // Check VMX_TIME_LOG_SUMMARY
            var hasSummary = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
            if (hasSummary > 0)
            {
                var rows = await connection.QueryAsync<dynamic>("SELECT LogId, LogName FROM VMX_TIME_LOG_SUMMARY;");
                foreach (var r in rows)
                {
                    var dict = (IDictionary<string, object>)r;
                    string id = dict.TryGetValue("LogId", out var idVal) && idVal != null ? Convert.ToString(idVal)! : "";
                    string name = dict.TryGetValue("LogName", out var nmVal) && nmVal != null ? Convert.ToString(nmVal)! : "";

                    if (!string.IsNullOrWhiteSpace(excludeLogId) && id.Equals(excludeLogId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    list.Add(new TimeLogOption
                    {
                        LogId = id,
                        LogName = !string.IsNullOrWhiteSpace(name) ? name : id,
                        WellId = wellId ?? "",
                        WellboreId = wellboreId ?? ""
                    });
                }
            }
        }

        return list;
    }
}

