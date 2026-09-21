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

    public async Task LogVmxTimeLogAsync(VmxTimeLog log)
    {
        var connection = _session.GetConnection();

        // Ensure Master Table exists
        var createMaster = @"
            CREATE TABLE IF NOT EXISTS VMX_TIME_LOG_SUMMARY (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogName TEXT,
                WellName TEXT,
                DataTableName TEXT,
                ImportStatus TEXT,
                QcScore REAL,
                ImportDate TEXT
            );";
        await connection.ExecuteAsync(createMaster);

        // Ensure backward compatibility if table existed previously without LogName
        var existingCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_TIME_LOG_SUMMARY');")).ToList();
        if (!existingCols.Contains("LogName", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_TIME_LOG_SUMMARY ADD COLUMN LogName TEXT;");
        }

        var insertSql = @"
            INSERT INTO VMX_TIME_LOG_SUMMARY (LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate)
            VALUES (@LogName, @WellName, @DataTableName, @ImportStatus, @QcScore, @ImportDate);";
            
        await connection.ExecuteAsync(insertSql, new
        {
            log.LogName,
            log.WellName,
            log.DataTableName,
            log.ImportStatus,
            log.QcScore,
            ImportDate = log.ImportDate.ToString("o")
        });
    }

    public async Task LogVmxDepthLogAsync(VmxDepthLog log)
    {
        var connection = _session.GetConnection();

        var createMaster = @"
            CREATE TABLE IF NOT EXISTS VMX_DEPTH_LOG_SUMMARY (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LogName TEXT,
                WellName TEXT,
                DataTableName TEXT,
                ImportStatus TEXT,
                QcScore REAL,
                ImportDate TEXT
            );";
        await connection.ExecuteAsync(createMaster);

        // Ensure backward compatibility if table existed previously without LogName
        var existingCols = (await connection.QueryAsync<string>("SELECT name FROM pragma_table_info('VMX_DEPTH_LOG_SUMMARY');")).ToList();
        if (!existingCols.Contains("LogName", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync("ALTER TABLE VMX_DEPTH_LOG_SUMMARY ADD COLUMN LogName TEXT;");
        }

        var insertSql = @"
            INSERT INTO VMX_DEPTH_LOG_SUMMARY (LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate)
            VALUES (@LogName, @WellName, @DataTableName, @ImportStatus, @QcScore, @ImportDate);";

        await connection.ExecuteAsync(insertSql, new
        {
            log.LogName,
            log.WellName,
            log.DataTableName,
            log.ImportStatus,
            log.QcScore,
            ImportDate = log.ImportDate.ToString("o")
        });
    }

    public async Task<List<VmxTimeLog>> GetTimeLogsAsync()
    {
        if (!_session.IsProjectOpen) return new List<VmxTimeLog>();
        var connection = _session.GetConnection();

        var hasTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
        if (hasTable == 0) return new List<VmxTimeLog>();

        var rows = await connection.QueryAsync<dynamic>(
            "SELECT Id, LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate FROM VMX_TIME_LOG_SUMMARY ORDER BY Id DESC;");

        var list = new List<VmxTimeLog>();
        foreach (var r in rows)
        {
            DateTime dt = DateTime.Now;
            if (r.ImportDate != null)
                DateTime.TryParse((string)r.ImportDate, out dt);

            list.Add(new VmxTimeLog
            {
                Id = (int)r.Id,
                LogName = (string)(r.LogName ?? string.Empty),
                WellName = (string)(r.WellName ?? string.Empty),
                DataTableName = (string)(r.DataTableName ?? string.Empty),
                ImportStatus = (string)(r.ImportStatus ?? string.Empty),
                QcScore = r.QcScore != null ? Convert.ToDouble(r.QcScore) : 0,
                ImportDate = dt
            });
        }
        return list;
    }

    public async Task<List<VmxDepthLog>> GetDepthLogsAsync()
    {
        if (!_session.IsProjectOpen) return new List<VmxDepthLog>();
        var connection = _session.GetConnection();

        var hasTable = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';");
        if (hasTable == 0) return new List<VmxDepthLog>();

        var rows = await connection.QueryAsync<dynamic>(
            "SELECT Id, LogName, WellName, DataTableName, ImportStatus, QcScore, ImportDate FROM VMX_DEPTH_LOG_SUMMARY ORDER BY Id DESC;");

        var list = new List<VmxDepthLog>();
        foreach (var r in rows)
        {
            DateTime dt = DateTime.Now;
            if (r.ImportDate != null)
                DateTime.TryParse((string)r.ImportDate, out dt);

            list.Add(new VmxDepthLog
            {
                Id = (int)r.Id,
                LogName = (string)(r.LogName ?? string.Empty),
                WellName = (string)(r.WellName ?? string.Empty),
                DataTableName = (string)(r.DataTableName ?? string.Empty),
                ImportStatus = (string)(r.ImportStatus ?? string.Empty),
                QcScore = r.QcScore != null ? Convert.ToDouble(r.QcScore) : 0,
                ImportDate = dt
            });
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
        if (!WellService.AddWell(_session.GetDataService(), well, ref lastError))
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

    public async Task EnsureWellAsync(string wellName, string? fieldName = null)
    {
        var existing = await GetProjectWellAsync();
        if (existing == null)
        {
            await SaveProjectWellAsync(new Well
            {
                ObjectID = Guid.NewGuid().ToString(),
                name = wellName,
                field = fieldName ?? "General Field",
                dTimSpud = DateTime.Now.ToString("o")
            });
        }
    }

    public async Task<List<Well>> GetWellsAsync()
    {
        var projectWell = await GetProjectWellAsync();
        if (projectWell != null)
            return new List<Well> { projectWell };

        return new List<Well>();
    }

    private static string SanitizeIdentifier(string name, int index)
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
    public async Task<StreamImportResult> StreamImportDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_session.IsProjectOpen)
            throw new InvalidOperationException("No project is currently loaded.");

        var connection = _session.GetConnection();
        bool isLas = Path.GetExtension(filePath).Equals(".las", StringComparison.OrdinalIgnoreCase);

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
                IsHookloadColumn = targetChannel.Equals("HKLD", StringComparison.OrdinalIgnoreCase) || targetChannel.Equals("Hookload", StringComparison.OrdinalIgnoreCase)
            });
        }

        // 2. Create Dynamic Table
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

        // 3. Prepare Parameterized Insert Command
        var colNames = string.Join(", ", columnPlans.Select(c => $"[{c.DbColumnName}]"));
        var paramNames = string.Join(", ", Enumerable.Range(0, columnPlans.Count).Select(i => $"@p{i}"));
        var insertSql = $"INSERT INTO [{tableName}] ({colNames}) VALUES ({paramNames});";

        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = insertSql;

        var cmdParams = new DbParameter[columnPlans.Count];
        for (int i = 0; i < columnPlans.Count; i++)
        {
            var p = insertCmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            insertCmd.Parameters.Add(p);
            cmdParams[i] = p;
        }

        int totalRows = 0;
        int validRows = 0;
        const int batchSize = 50000;

        DbTransaction? transaction = connection.BeginTransaction();
        insertCmd.Transaction = transaction;

        try
        {
            if (isLas)
            {
                // Stream LAS file
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
                using var reader = new StreamReader(fileStream, Encoding.UTF8);

                var lasHeaders = new List<string>();
                bool inCurve = false;
                bool inAscii = false;
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    if (trimmed.StartsWith("~"))
                    {
                        if (trimmed.StartsWith("~C", StringComparison.OrdinalIgnoreCase))
                        {
                            inCurve = true;
                            continue;
                        }
                        else if (inCurve && !trimmed.StartsWith("~C", StringComparison.OrdinalIgnoreCase))
                        {
                            inCurve = false;
                        }

                        if (trimmed.StartsWith("~A", StringComparison.OrdinalIgnoreCase))
                        {
                            inAscii = true;

                            // Resolve SourceIndex for each column plan
                            for (int i = 0; i < columnPlans.Count; i++)
                            {
                                columnPlans[i].SourceIndex = lasHeaders.FindIndex(h => h.Equals(columnPlans[i].SourceHeader, StringComparison.OrdinalIgnoreCase));
                            }
                            continue;
                        }
                    }

                    if (inCurve && !trimmed.StartsWith("#"))
                    {
                        var mnem = trimmed.Split(new[] { '.', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (!string.IsNullOrEmpty(mnem)) lasHeaders.Add(mnem);
                        continue;
                    }

                    if (inAscii && !trimmed.StartsWith("#"))
                    {
                        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        totalRows++;
                        bool isRowValid = true;

                        for (int i = 0; i < columnPlans.Count; i++)
                        {
                            int sIdx = columnPlans[i].SourceIndex;
                            if (sIdx >= 0 && sIdx < tokens.Length)
                            {
                                var raw = tokens[sIdx];
                                if (string.IsNullOrWhiteSpace(raw) || raw == "-999.25" || raw == "-9999")
                                {
                                    cmdParams[i].Value = DBNull.Value;
                                    if (columnPlans[i].IsDepthColumn) isRowValid = false;
                                }
                                else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                                {
                                    cmdParams[i].Value = dblVal;
                                    if (columnPlans[i].IsHookloadColumn && dblVal < 0) isRowValid = false;
                                }
                                else
                                {
                                    cmdParams[i].Value = raw;
                                }
                            }
                            else
                            {
                                cmdParams[i].Value = DBNull.Value;
                                if (columnPlans[i].IsDepthColumn) isRowValid = false;
                            }
                        }

                        if (isRowValid) validRows++;
                        insertCmd.ExecuteNonQuery();

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
                }
            }
            else
            {
                // Stream CSV file
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null,
                    BadDataFound = null,
                    BufferSize = 65536
                };

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
                long fileLength = fileStream.Length;
                using var reader = new StreamReader(fileStream, Encoding.UTF8);
                using var csv = new CsvReader(reader, config);

                csv.Read();
                csv.ReadHeader();

                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (csv.HeaderRecord != null)
                {
                    for (int i = 0; i < csv.HeaderRecord.Length; i++)
                    {
                        var h = csv.HeaderRecord[i];
                        if (!headerMap.ContainsKey(h)) headerMap[h] = i;
                    }
                }

                for (int i = 0; i < columnPlans.Count; i++)
                {
                    if (headerMap.TryGetValue(columnPlans[i].SourceHeader, out int idx))
                    {
                        columnPlans[i].SourceIndex = idx;
                    }
                }

                while (csv.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    totalRows++;
                    bool isRowValid = true;

                    for (int i = 0; i < columnPlans.Count; i++)
                    {
                        int sIdx = columnPlans[i].SourceIndex;
                        string? raw = sIdx >= 0 ? csv.GetField(sIdx) : null;

                        if (string.IsNullOrWhiteSpace(raw))
                        {
                            cmdParams[i].Value = DBNull.Value;
                            if (columnPlans[i].IsDepthColumn) isRowValid = false;
                        }
                        else if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                        {
                            cmdParams[i].Value = dblVal;
                            if (columnPlans[i].IsHookloadColumn && dblVal < 0) isRowValid = false;
                        }
                        else
                        {
                            cmdParams[i].Value = raw.Trim();
                        }
                    }

                    if (isRowValid) validRows++;
                    insertCmd.ExecuteNonQuery();

                    if (totalRows % batchSize == 0)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = connection.BeginTransaction();
                        insertCmd.Transaction = transaction;

                        double pct = fileLength > 0 ? ((double)fileStream.Position / fileLength * 100.0) : 0;
                        progress?.Report(new ImportProgressReport
                        {
                            RowsProcessed = totalRows,
                            PercentCompleted = Math.Min(99.0, pct),
                            IsIndeterminate = false,
                            StatusMessage = $"Importing: {totalRows:N0} rows processed ({pct:F0}%)..."
                        });
                    }
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
            }
            throw;
        }

        double finalQcScore = totalRows > 0 ? ((double)validRows / totalRows * 100.0) : 0.0;

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
            QcScore = finalQcScore
        };
    }
}

