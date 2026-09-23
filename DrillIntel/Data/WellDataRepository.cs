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

    public WellDataRepository(ProjectSession session)
    {
        _session = session;
    }

    public async Task InitializeDictionaryAsync()
    {
        if (!_session.IsProjectOpen) return;
        var connection = _session.GetConnection();
        
        var createDict = @"
            CREATE TABLE IF NOT EXISTS VMX_CURVE_DICTIONARY (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Mnemonic TEXT UNIQUE,
                StandardChannel TEXT
            );";
        await connection.ExecuteAsync(createDict);

        // Seed default dictionary if empty
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_CURVE_DICTIONARY");
        if (count == 0)
        {
            var seedSql = @"
                INSERT INTO VMX_CURVE_DICTIONARY (Mnemonic, StandardChannel) VALUES 
                ('DEPTH', 'Depth'), ('DMEA', 'Depth'), ('DEPT', 'Depth'),
                ('HKLD', 'Hookload'), ('WOB', 'Hookload'), ('WEIGHT', 'Hookload'),
                ('RPM', 'RPM'), ('SRPM', 'RPM'), ('TRPM', 'RPM'),
                ('SPPA', 'Pump Pressure'), ('PRESS', 'Pump Pressure'), ('PUMP', 'Pump Pressure'),
                ('TQA', 'Torque'), ('TORQ', 'Torque');";
            await connection.ExecuteAsync(seedSql);
        }
    }

    public async Task<List<VmxCurveDictionary>> GetCurveDictionariesAsync()
    {
        if (!_session.IsProjectOpen) return new List<VmxCurveDictionary>();
        var connection = _session.GetConnection();
        var result = await connection.QueryAsync<VmxCurveDictionary>("SELECT * FROM VMX_CURVE_DICTIONARY");
        return result.ToList();
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

    public async Task LogVmxTimeLogAsync(TimeLog log) => await LogTimeLogAsync(log);

    public async Task LogVmxTimeLogAsync(VmxTimeLog log)
    {
        var timeLog = new TimeLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = log.LogName,
            nameWell = log.WellName,
            __WellName = log.WellName,
            __dataTableName = log.DataTableName,
            comments = log.ImportStatus,
            description = $"QC: {log.QcScore:F1}% • {log.ImportDate:dd-MM-yyyy hh:mm tt}",
            creationDate = log.ImportDate.ToString("dd-MMM-yyyy HH:mm:ss")
        };
        await LogTimeLogAsync(timeLog);
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

    public async Task LogVmxDepthLogAsync(DepthLog log) => await LogDepthLogAsync(log);

    public async Task LogVmxDepthLogAsync(VmxDepthLog log)
    {
        var depthLog = new DepthLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = log.LogName,
            nameWell = log.WellName,
            __WellName = log.WellName,
            __dataTableName = log.DataTableName,
            comments = log.ImportStatus,
            description = $"QC: {log.QcScore:F1}% • {log.ImportDate:dd-MM-yyyy hh:mm tt}",
            creationDate = log.ImportDate.ToString("dd-MMM-yyyy HH:mm:ss")
        };
        await LogDepthLogAsync(depthLog);
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
            if (hasSummary > 0)
            {
                var summaries = (await connection.QueryAsync<dynamic>(
                    "SELECT * FROM VMX_TIME_LOG_SUMMARY;")).ToList();

                foreach (var tl in list)
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

                    if (match is IDictionary<string, object> rowDict && string.IsNullOrWhiteSpace(tl.description))
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
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsProjectOpen)
            throw new InvalidOperationException("No project is currently loaded.");

        if (columnHeadingRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnHeadingRow), "Column Heading Row is mandatory and must be a positive integer.");

        if (importFromRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(importFromRow), "Import from Row is mandatory and must be a positive integer.");

        var connection = _session.GetConnection();

        // 1. Determine columns and map to source headers
        var columnPlans = new List<ColumnPlan>();
        var distinctNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int colIdx = 0;
        foreach (var map in mappings)
        {
            var header = map.CsvColumnHeader;
            var targetChannel = map.MappedVumaxChannel == "Dynamic (New Column)" ? header : map.MappedVumaxChannel;
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

        // 2. Check if table already exists (e.g. created by DepthLogService.AddDepthLog)
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
                sb.Append($",\n  [{plan.DbColumnName}] NUMERIC");
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

        bool hasDataIndex = existingTableCols.Contains("DATA_INDEX");

        // 3. Prepare Parameterized Insert Command
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

        int totalRows = 0;
        int validRows = 0;
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

                for (int i = 0; i < columnPlans.Count; i++)
                {
                    int sIdx = columnPlans[i].SourceIndex;
                    string? raw = (sIdx >= 0 && sIdx < tokens.Length) ? tokens[sIdx] : null;

                    if (string.IsNullOrWhiteSpace(raw) || raw == "-999.25" || raw == "-9999" || (fileMetadata?.NullValue != null && raw == fileMetadata.NullValue))
                    {
                        cmdParams[i].Value = DBNull.Value;
                        if (columnPlans[i].IsDepthColumn)
                        {
                            isRowValid = false;
                            depthIsNull = true;
                        }
                    }
                    else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
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

                        if (columnPlans[i].IsDateTimeColumn)
                        {
                            if (minDate == null) minDate = raw;
                            maxDate = raw;
                        }
                    }
                    else
                    {
                        cmdParams[i].Value = raw.Trim();
                        if (columnPlans[i].IsDateTimeColumn)
                        {
                            if (minDate == null) minDate = raw.Trim();
                            maxDate = raw.Trim();
                        }
                        else
                        {
                            isRowValid = false;
                            if (columnPlans[i].IsDepthColumn)
                            {
                                depthIsNull = true;
                            }
                        }
                    }
                }

                if (isRowValid) validRows++;
                if (!depthIsNull)
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
            QcScore = finalQcScore,
            MinDepth = minDepth,
            MaxDepth = maxDepth,
            FirstDepth = firstDepth,
            LastDepth = lastDepth,
            StepIncrement = stepIncrement,
            LastDataIndex = lastDataIndex,
            MinDate = minDate,
            MaxDate = maxDate
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
}
