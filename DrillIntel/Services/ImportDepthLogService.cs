using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services.Readers;

namespace DrillIntel.Services;

public class ImportDepthLogService : IImportDepthLogService
{
    private readonly List<IDepthLogFormatReader> _readers;
    private readonly MappingFileReader _mappingFileReader;
    private readonly IWellDataRepository? _repository;
    private readonly ProjectSession? _session;

    public ImportDepthLogService(IWellDataRepository? repository = null, ProjectSession? session = null)
    {
        _repository = repository;
        _session = session;
        _readers = new List<IDepthLogFormatReader>
        {
            new LasDepthReader(),
            new WitsmlDepthReader(),
            new ExcelDepthReader(),
            new CsvDepthReader()
        };
        _mappingFileReader = new MappingFileReader();
    }

    public IDepthLogFormatReader GetReader(string filePath)
    {
        var reader = _readers.FirstOrDefault(r => r.CanHandle(filePath));
        if (reader == null)
        {
            throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(filePath)}");
        }
        return reader;
    }

    public List<string> GetHeaders(
        string filePath,
        int columnHeadingRow = 1,
        string? worksheetName = null,
        string delimiter = ",")
    {
        var reader = GetReader(filePath);
        return reader.GetHeaders(filePath, columnHeadingRow, worksheetName, delimiter);
    }

    public List<List<string>> GetPreviewRows(
        string filePath,
        int importFromRow = 2,
        int maxRows = 50,
        string? worksheetName = null,
        string delimiter = ",")
    {
        var reader = GetReader(filePath);
        return reader.GetPreviewRows(filePath, importFromRow, maxRows, worksheetName, delimiter);
    }

    public List<string> GetWorksheets(string filePath)
    {
        var reader = GetReader(filePath);
        return reader.GetWorksheets(filePath);
    }

    public DepthLogMappingResult LoadMappingFile(string mappingFilePath)
    {
        return _mappingFileReader.LoadMappingFile(mappingFilePath);
    }

    public List<DepthLog> ImportFromFile(
        string filePath,
        DepthLogImportOptions options)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Import file not found: {filePath}", filePath);
        }

        if (!options.ColumnHeadingRow.HasValue || options.ColumnHeadingRow.Value <= 0)
        {
            throw new InvalidOperationException("Column Heading Row is mandatory and must be specified to import DepthLog.");
        }

        if (!options.ImportFromRow.HasValue || options.ImportFromRow.Value <= 0)
        {
            throw new InvalidOperationException("Import from Row is mandatory and must be specified to import DepthLog.");
        }

        var reader = GetReader(filePath);
        var metadata = reader.ExtractMetadata(filePath);
        var headers = reader.GetHeaders(filePath, options.ColumnHeadingRow.Value, options.WorksheetName, options.Delimiter);

        if (headers.Count == 0)
        {
            throw new InvalidOperationException($"No headers found in {filePath} at row {options.ColumnHeadingRow.Value}.");
        }

        // 1. Resolve DEPTH mapping
        string? depthSourceColumn = options.ManualDepthColumnName;

        if (string.IsNullOrEmpty(depthSourceColumn) && options.ColumnMappings != null)
        {
            foreach (var kvp in options.ColumnMappings)
            {
                if (kvp.Value.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.Equals("Depth", StringComparison.OrdinalIgnoreCase))
                {
                    depthSourceColumn = kvp.Key;
                    break;
                }
                else if (kvp.Key.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
                         kvp.Key.Equals("Depth", StringComparison.OrdinalIgnoreCase))
                {
                    depthSourceColumn = kvp.Value;
                    break;
                }
            }
        }

        // Auto-detect DEPTH if still null
        if (string.IsNullOrEmpty(depthSourceColumn))
        {
            depthSourceColumn = headers.FirstOrDefault(h =>
                h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Depth", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("DMEA", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("MD", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("MeasuredDepth", StringComparison.OrdinalIgnoreCase) ||
                h.Equals("Measured Depth", StringComparison.OrdinalIgnoreCase));
        }

        // DEPTH mapping is MANDATORY
        if (string.IsNullOrEmpty(depthSourceColumn))
        {
            throw new InvalidOperationException("You must map and select DEPTH channel. Please map and select the depth channel to continue");
        }

        var effectiveWellName = !string.IsNullOrWhiteSpace(options.WellName)
            ? options.WellName.Trim()
            : (!string.IsNullOrWhiteSpace(metadata?.WellName) ? metadata.WellName.Trim() : "Well 1");

        var effectiveLogName = !string.IsNullOrWhiteSpace(options.LogName)
            ? options.LogName.Trim()
            : Path.GetFileNameWithoutExtension(filePath);

        var depthLog = new DepthLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = effectiveLogName,
            nameWell = effectiveWellName,
            __WellName = effectiveWellName,
            WellID = options.WellID ?? "",
            WellboreID = options.WellboreID ?? "",
            indexCurve = "DEPTH",
            indexType = "measured depth",
            indexUnits = metadata?.CurveUnits.GetValueOrDefault("DEPTH", "m") ?? "m",
            nullValue = metadata?.NullValue ?? "-999.25",
            comments = "Success",
            creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
            startIndex = metadata?.StartIndex ?? "",
            endIndex = metadata?.EndIndex ?? "",
            stepIncrement = metadata?.StepIncrement ?? ""
        };

        // 2. Build mapping dictionary: importedSourceColumn -> targetVuMaxChannel
        var sourceToTargetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(depthSourceColumn))
        {
            sourceToTargetMap[depthSourceColumn] = "DEPTH";
        }

        if (options.ColumnMappings != null)
        {
            foreach (var kvp in options.ColumnMappings)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                    continue;

                if (kvp.Key.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Value] = "DEPTH";
                }
                else if (kvp.Value.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Key] = "DEPTH";
                }
                else if (headers.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase) && !headers.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Key] = kvp.Value;
                }
                else if (headers.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase) && !headers.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Value] = kvp.Key;
                }
                else if (headers.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Key] = kvp.Value;
                }
                else if (headers.Contains(kvp.Value, StringComparer.OrdinalIgnoreCase))
                {
                    sourceToTargetMap[kvp.Value] = kvp.Key;
                }
            }
        }

        // 3. Resolve target channels:
        // - Mapped imported columns: Values go into mapped VuMax Channel field; original imported column is NOT created.
        // - Unmapped imported columns: Dynamically create a new column in target table with imported column name.
        var distinctMnemonic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedChannels = new List<(string FinalMnemonic, string SourceHeader)>();

        // Phase A: Process mapped columns first (so target VuMax channels like DEPTH get their exact name)
        foreach (var h in headers)
        {
            if (sourceToTargetMap.TryGetValue(h, out var targetMnemonic) && !string.IsNullOrWhiteSpace(targetMnemonic))
            {
                var safeMnemonic = WellDataRepository.SanitizeIdentifier(targetMnemonic, resolvedChannels.Count);
                var finalMnemonic = safeMnemonic;
                int suffix = 1;
                while (distinctMnemonic.Contains(finalMnemonic))
                {
                    finalMnemonic = $"{safeMnemonic}_{suffix++}";
                }
                distinctMnemonic.Add(finalMnemonic);
                resolvedChannels.Add((finalMnemonic, h));
            }
        }

        // Phase B: Process unmapped imported columns (dynamically create new column with imported column name)
        foreach (var h in headers)
        {
            if (sourceToTargetMap.ContainsKey(h))
                continue;

            // If an unmapped column name conflicts with an already-claimed mapped target channel (e.g. file had DEPTH, but DEPTH was mapped from GAMMARAY),
            // skip it to prevent creating a duplicate column in the table
            if (distinctMnemonic.Contains(h))
                continue;

            var safeMnemonic = WellDataRepository.SanitizeIdentifier(h, resolvedChannels.Count);
            var finalMnemonic = safeMnemonic;
            int suffix = 1;
            while (distinctMnemonic.Contains(finalMnemonic))
            {
                finalMnemonic = $"{safeMnemonic}_{suffix++}";
            }
            distinctMnemonic.Add(finalMnemonic);
            resolvedChannels.Add((finalMnemonic, h));
        }

        // 4. Populate LogCurves
        int order = 1;
        foreach (var (finalMnemonic, sourceHeader) in resolvedChannels)
        {
            string unit = "";
            if (finalMnemonic.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
            {
                unit = metadata?.CurveUnits.GetValueOrDefault(sourceHeader, "m") ?? "m";
            }
            else if (metadata != null && metadata.CurveUnits.TryGetValue(sourceHeader, out var u))
            {
                unit = u;
            }

            string desc = sourceHeader;
            if (metadata != null && metadata.CurveDescriptions.TryGetValue(sourceHeader, out var d) && !string.IsNullOrWhiteSpace(d))
            {
                desc = d;
            }

            var channel = new LogChannel
            {
                mnemonic = finalMnemonic,
                curveDescription = desc,
                typeLogData = "Double",
                unit = unit,
                ColumnOrder = order++,
                witsmlMnemonic = finalMnemonic
            };
            depthLog.LogCurves[finalMnemonic] = channel;
        }

        return new List<DepthLog> { depthLog };
    }

    public async Task<List<DepthLog>> ImportFromFileAsync(
        string filePath,
        DepthLogImportOptions options,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Import file not found: {filePath}", filePath);
        }

        if (!options.ColumnHeadingRow.HasValue || options.ColumnHeadingRow.Value <= 0)
        {
            throw new InvalidOperationException("Column Heading Row is mandatory and must be specified to import DepthLog.");
        }

        if (!options.ImportFromRow.HasValue || options.ImportFromRow.Value <= 0)
        {
            throw new InvalidOperationException("Import from Row is mandatory and must be specified to import DepthLog.");
        }

        if (_repository == null || _session == null)
        {
            throw new InvalidOperationException("Repository and session are required for database persistence.");
        }

        if (options.OperationType == OperationType.UpdateData)
        {
            // === UPDATE LOGIC ===
            var existingLogs = await _repository.GetDepthLogsAsync();
            var existingLog = existingLogs.FirstOrDefault(l =>
                (!string.IsNullOrWhiteSpace(options.TargetTableName) && l.__dataTableName.Equals(options.TargetTableName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(options.LogName) && l.nameLog.Equals(options.LogName, StringComparison.OrdinalIgnoreCase)));

            if (existingLog == null)
            {
                throw new InvalidOperationException($"Target DepthLog '{options.LogName ?? options.TargetTableName}' was not found for update.");
            }

            string targetTableName = existingLog.__dataTableName;
            var existingCols = await _repository.GetTableColumnsAsync(targetTableName);

            var reader = GetReader(filePath);
            var headers = reader.GetHeaders(filePath, options.ColumnHeadingRow.Value, options.WorksheetName, options.Delimiter);

            // Auto-mapping:
            // "If the VuMax column name matches the imported file column name, auto-map and update.
            // If no match is found, keep current logic as it is."
            var activeMappings = new List<ChannelMapping>();
            foreach (var vuCol in existingCols)
            {
                if (vuCol.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase))
                    continue;

                string? matchedSource = null;

                // 1. Explicit user selection or mapping dictionary
                if (options.ColumnMappings != null)
                {
                    foreach (var kvp in options.ColumnMappings)
                    {
                        if (kvp.Value.Equals(vuCol, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedSource = kvp.Key;
                            break;
                        }
                    }
                }

                // 2. Manual depth column override
                if (string.IsNullOrEmpty(matchedSource) && vuCol.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
                {
                    matchedSource = options.ManualDepthColumnName;
                }

                // 3. Auto-mapping: if VuMax column name matches imported file header
                if (string.IsNullOrEmpty(matchedSource))
                {
                    matchedSource = headers.FirstOrDefault(h =>
                        h.Equals(vuCol, StringComparison.OrdinalIgnoreCase) ||
                        (vuCol.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) && (
                            h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("DMEA", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("MD", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("MeasuredDepth", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("Measured Depth", StringComparison.OrdinalIgnoreCase))));
                }

                if (!string.IsNullOrEmpty(matchedSource))
                {
                    activeMappings.Add(new ChannelMapping
                    {
                        CsvColumnHeader = matchedSource,
                        MappedVumaxChannel = vuCol
                    });
                }
            }

            // Restrictions:
            // Do not create any new columns.
            // Do not alter existing column names in the target table.
            // Only update values in mapped VuMax columns.
            var importResult = await _repository.StreamUpdateDepthDataAsync(
                targetTableName,
                filePath,
                activeMappings,
                options.ColumnHeadingRow.Value,
                options.ImportFromRow.Value,
                options.Delimiter,
                options.WorksheetName,
                progress,
                cancellationToken);

            // Update DepthLog metadata
            existingLog.description = $"QC: {importResult.QcScore:F1}% • {DateTime.Now:dd-MM-yyyy hh:mm tt}";
            if (importResult.MinDepth.HasValue)
                existingLog.startIndex = importResult.MinDepth.Value.ToString(CultureInfo.InvariantCulture);
            if (importResult.MaxDepth.HasValue)
                existingLog.endIndex = importResult.MaxDepth.Value.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(importResult.LastDataIndex))
                existingLog.lastDataIndex = importResult.LastDataIndex;
            if (!string.IsNullOrEmpty(importResult.StepIncrement))
                existingLog.stepIncrement = importResult.StepIncrement;

            await _repository.LogDepthLogAsync(existingLog);
            _session?.NotifyDataChanged();

            return new List<DepthLog> { existingLog };
        }

        // === NEW DATA IMPORT LOGIC ===
        // 1. Build and validate DepthLog domain object (throws if DEPTH not mapped)
        var logs = ImportFromFile(filePath, options);
        var depthLog = logs[0];

        // 2. Ensure Well exists
        await _repository.EnsureWellAsync(depthLog.nameWell);

        // 3. Register DepthLog schema in database
        var dataService = _session.GetDataService();
        string lastError = string.Empty;
        bool addSuccess = DepthLogService.AddDepthLog(dataService, depthLog, ref lastError);
        if (!addSuccess)
        {
            throw new InvalidOperationException($"DepthLogService.AddDepthLog failed: {lastError}");
        }

        // 4. Build ChannelMappings
        var newMappings = new List<ChannelMapping>();
        foreach (var curve in depthLog.LogCurves.Values)
        {
            newMappings.Add(new ChannelMapping
            {
                CsvColumnHeader = curve.curveDescription,
                MappedVumaxChannel = curve.mnemonic
            });
        }

        // 5. Execute streaming import into SQLite table
        var result = await _repository.StreamImportDataAsync(
            depthLog.__dataTableName,
            filePath,
            newMappings,
            options.ColumnHeadingRow.Value,
            options.ImportFromRow.Value,
            options.Delimiter,
            options.WorksheetName,
            progress,
            cancellationToken);

        // 6. Update DepthLog metadata
        depthLog.description = $"QC: {result.QcScore:F1}% • {DateTime.Now:dd-MM-yyyy hh:mm tt}";
        if (result.MinDepth.HasValue)
            depthLog.startIndex = result.MinDepth.Value.ToString(CultureInfo.InvariantCulture);
        if (result.MaxDepth.HasValue)
            depthLog.endIndex = result.MaxDepth.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(result.LastDataIndex))
            depthLog.lastDataIndex = result.LastDataIndex;
        if (!string.IsNullOrEmpty(result.StepIncrement))
            depthLog.stepIncrement = result.StepIncrement;

        await _repository.LogDepthLogAsync(depthLog);
        _session?.NotifyDataChanged();

        return logs;
    }
}
