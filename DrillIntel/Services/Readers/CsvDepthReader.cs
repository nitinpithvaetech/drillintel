using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;

namespace DrillIntel.Services.Readers;

public class CsvDepthReader : IDepthLogFormatReader
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dat", StringComparison.OrdinalIgnoreCase);
    }

    public List<string> GetWorksheets(string filePath) => new();

    public DepthLogMetadata? ExtractMetadata(string filePath) => null;

    private CsvConfiguration CreateCsvConfig(string delimiter)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = string.IsNullOrEmpty(delimiter) ? "," : delimiter,
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
            Mode = CsvMode.RFC4180,
            BufferSize = 65536
        };
    }

    public List<string> GetHeaders(
        string filePath,
        int columnHeadingRow = 1,
        string? worksheetName = null,
        string delimiter = ",")
    {
        if (!File.Exists(filePath)) return new List<string>();

        int targetRow = Math.Max(1, columnHeadingRow);
        var config = CreateCsvConfig(delimiter);

        using var reader = new StreamReader(filePath, Encoding.UTF8, true);
        using var csv = new CsvReader(reader, config);

        int currentRow = 0;
        while (csv.Read())
        {
            currentRow++;
            if (currentRow == targetRow)
            {
                var headers = new List<string>();
                int colCount = csv.Parser.Count;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < colCount; i++)
                {
                    string raw = csv.GetField(i)?.Trim() ?? string.Empty;
                    string name = string.IsNullOrWhiteSpace(raw) ? $"Column {i + 1}" : raw;
                    
                    // Disallow characters that break identifiers if needed, but preserve channel names
                    string uniqueName = name;
                    int suffix = 1;
                    while (seen.Contains(uniqueName))
                    {
                        uniqueName = $"{name}_{suffix++}";
                    }
                    seen.Add(uniqueName);
                    headers.Add(uniqueName);
                }
                return headers;
            }
        }

        return new List<string>();
    }

    public List<List<string>> GetPreviewRows(
        string filePath,
        int importFromRow = 2,
        int maxRows = 50,
        string? worksheetName = null,
        string delimiter = ",")
    {
        var result = new List<List<string>>();
        if (!File.Exists(filePath)) return result;

        int targetRow = Math.Max(1, importFromRow);
        var config = CreateCsvConfig(delimiter);

        using var reader = new StreamReader(filePath, Encoding.UTF8, true);
        using var csv = new CsvReader(reader, config);

        int currentRow = 0;
        while (csv.Read())
        {
            currentRow++;
            if (currentRow < targetRow) continue;

            var row = new List<string>();
            int colCount = csv.Parser.Count;
            for (int i = 0; i < colCount; i++)
            {
                row.Add(csv.GetField(i)?.Trim() ?? string.Empty);
            }
            result.Add(row);

            if (result.Count >= maxRows) break;
        }

        return result;
    }

    public IEnumerable<string[]> StreamDataRows(
        string filePath,
        int importFromRow = 2,
        string? worksheetName = null,
        string delimiter = ",",
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) yield break;

        int targetRow = Math.Max(1, importFromRow);
        var config = CreateCsvConfig(delimiter);

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
        using var reader = new StreamReader(fileStream, Encoding.UTF8, true);
        using var csv = new CsvReader(reader, config);

        int currentRow = 0;
        while (csv.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentRow++;
            if (currentRow < targetRow) continue;

            int colCount = csv.Parser.Count;
            var row = new string[colCount];
            for (int i = 0; i < colCount; i++)
            {
                row[i] = csv.GetField(i)?.Trim() ?? string.Empty;
            }
            yield return row;
        }
    }
}

