using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ExcelDataReader;

namespace DrillIntel.Services.Readers;

public class ExcelDepthReader : IDepthLogFormatReader
{
    static ExcelDepthReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".xls", StringComparison.OrdinalIgnoreCase);
    }

    public DepthLogMetadata? ExtractMetadata(string filePath) => null;

    public static List<string> GetWorksheetNames(string filePath) => new ExcelDepthReader().GetWorksheets(filePath);

    public List<string> GetWorksheets(string filePath)
    {
        var list = new List<string>();
        if (!File.Exists(filePath)) return list;

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        do
        {
            list.Add(reader.Name);
        } while (reader.NextResult());

        return list;
    }

    private IExcelDataReader? OpenToWorksheet(Stream stream, string? worksheetName)
    {
        var reader = ExcelReaderFactory.CreateReader(stream);
        if (string.IsNullOrEmpty(worksheetName))
        {
            return reader;
        }

        do
        {
            if (reader.Name.Equals(worksheetName, StringComparison.OrdinalIgnoreCase))
            {
                return reader;
            }
        } while (reader.NextResult());

        // Fallback: reopen from start if not found
        stream.Position = 0;
        return ExcelReaderFactory.CreateReader(stream);
    }

    public List<string> GetHeaders(
        string filePath,
        int columnHeadingRow = 1,
        string? worksheetName = null,
        string delimiter = ",")
    {
        if (!File.Exists(filePath)) return new List<string>();

        int targetRow = Math.Max(1, columnHeadingRow);

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = OpenToWorksheet(stream, worksheetName);
        if (reader == null) return new List<string>();

        int currentRow = 0;
        while (reader.Read())
        {
            currentRow++;
            if (currentRow == targetRow)
            {
                var headers = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string raw = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                    string name = string.IsNullOrWhiteSpace(raw) ? $"Column {i + 1}" : raw;

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

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = OpenToWorksheet(stream, worksheetName);
        if (reader == null) return result;

        int currentRow = 0;
        while (reader.Read())
        {
            currentRow++;
            if (currentRow < targetRow) continue;

            var row = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty);
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

        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = OpenToWorksheet(stream, worksheetName);
        if (reader == null) yield break;

        int currentRow = 0;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentRow++;
            if (currentRow < targetRow) continue;

            var row = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
            }
            yield return row;
        }
    }
}
