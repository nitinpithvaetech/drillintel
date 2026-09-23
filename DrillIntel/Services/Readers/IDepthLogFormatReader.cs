using System.Collections.Generic;
using System.Threading;

namespace DrillIntel.Services.Readers;

public interface IDepthLogFormatReader
{
    bool CanHandle(string filePath);

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

    IEnumerable<string[]> StreamDataRows(
        string filePath,
        int importFromRow = 2,
        string? worksheetName = null,
        string delimiter = ",",
        CancellationToken cancellationToken = default);

    DepthLogMetadata? ExtractMetadata(string filePath);

    List<string> GetWorksheets(string filePath);
}

