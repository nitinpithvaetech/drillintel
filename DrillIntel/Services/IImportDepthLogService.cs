using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Services.Readers;

namespace DrillIntel.Services;

public interface IImportDepthLogService
{
    IDepthLogFormatReader GetReader(string filePath);

    List<string> GetHeaders(
        string filePath,
        int columnHeadingRow = 1,
        string? worksheetName = null,
        string delimiter = ",");

    List<List<string>> GetPreviewRows(
        string filePath,
        int importFromRow = 2,
        int maxRows = 50,
        string? worksheetName = null,
        string delimiter = ",");

    List<string> GetWorksheets(string filePath);

    DepthLogMappingResult LoadMappingFile(string mappingFilePath);

    List<DepthLog> ImportFromFile(
        string filePath,
        DepthLogImportOptions options);

    Task<List<DepthLog>> ImportFromFileAsync(
        string filePath,
        DepthLogImportOptions options,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}

