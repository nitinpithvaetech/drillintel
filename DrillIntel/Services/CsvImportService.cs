using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using DrillIntel.Models;

namespace DrillIntel.Services;

public class CsvImportService
{
    public List<string> GetHeaders(string filePath)
    {
        if (Path.GetExtension(filePath).Equals(".las", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLasHeaders(filePath);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        
        csv.Read();
        csv.ReadHeader();
        return csv.HeaderRecord?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Reads only up to maxRows from the file for preview purposes, preventing UI freezes on large files.
    /// </summary>
    public List<List<string>> GetPreviewRows(string filePath, int maxRows = 100)
    {
        if (Path.GetExtension(filePath).Equals(".las", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLasPreviewRows(filePath, maxRows);
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        
        csv.Read();
        csv.ReadHeader();
        
        var rows = new List<List<string>>();
        int colCount = csv.HeaderRecord?.Length ?? 0;
        int count = 0;

        while (csv.Read() && count < maxRows)
        {
            var row = new List<string>();
            for (int i = 0; i < colCount; i++)
            {
                row.Add(csv.GetField(i) ?? string.Empty);
            }
            rows.Add(row);
            count++;
        }
        return rows;
    }

    public List<List<string>> GetAllRowsAsString(string filePath)
    {
        return GetPreviewRows(filePath, 5000);
    }

    public DataTable ParseCsv(string filePath, List<ChannelMapping> mappings)
    {
        if (Path.GetExtension(filePath).Equals(".las", StringComparison.OrdinalIgnoreCase))
        {
            return ParseLasToDataTable(filePath, mappings);
        }

        var dataTable = new DataTable();
        
        // Add columns based on mapping
        foreach (var mapping in mappings)
        {
            var colName = mapping.MappedVumaxChannel == "Dynamic (New Column)" 
                ? mapping.CsvColumnHeader 
                : mapping.MappedVumaxChannel;

            if (!dataTable.Columns.Contains(colName))
            {
                dataTable.Columns.Add(colName, typeof(object));
            }
        }

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);
        
        csv.Read();
        csv.ReadHeader();

        // Cache header indices to avoid repeated dictionary lookups
        var colIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (csv.HeaderRecord != null)
        {
            for (int i = 0; i < csv.HeaderRecord.Length; i++)
            {
                var h = csv.HeaderRecord[i];
                if (!colIndices.ContainsKey(h)) colIndices[h] = i;
            }
        }

        while (csv.Read())
        {
            var row = dataTable.NewRow();
            foreach (var mapping in mappings)
            {
                var colName = mapping.MappedVumaxChannel == "Dynamic (New Column)" 
                    ? mapping.CsvColumnHeader 
                    : mapping.MappedVumaxChannel;

                if (colIndices.TryGetValue(mapping.CsvColumnHeader, out int idx))
                {
                    string? rawVal = csv.GetField(idx);
                    if (string.IsNullOrWhiteSpace(rawVal))
                    {
                        row[colName] = DBNull.Value;
                    }
                    else if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dblVal))
                    {
                        row[colName] = dblVal;
                    }
                    else
                    {
                        row[colName] = rawVal.Trim();
                    }
                }
                else
                {
                    row[colName] = DBNull.Value;
                }
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    private List<string> ParseLasHeaders(string filePath)
    {
        var headers = new List<string>();
        bool inCurveSection = false;
        
        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~C", StringComparison.OrdinalIgnoreCase))
                {
                    inCurveSection = true;
                    continue;
                }
                else if (inCurveSection)
                {
                    break; // End of Curve section
                }
            }

            if (inCurveSection && !trimmed.StartsWith("#"))
            {
                var mnem = trimmed.Split(new[] { '.', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(mnem))
                {
                    headers.Add(mnem);
                }
            }
        }
        
        return headers;
    }

    private List<List<string>> ParseLasPreviewRows(string filePath, int maxRows)
    {
        var rows = new List<List<string>>();
        bool inAsciiSection = false;

        using var reader = new StreamReader(filePath);
        string? line;
        while ((line = reader.ReadLine()) != null && rows.Count < maxRows)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~A", StringComparison.OrdinalIgnoreCase))
                {
                    inAsciiSection = true;
                }
                continue;
            }

            if (inAsciiSection && !trimmed.StartsWith("#"))
            {
                var rowData = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                rows.Add(rowData);
            }
        }

        return rows;
    }

    private List<List<string>> ParseLasRows(string filePath)
    {
        return ParseLasPreviewRows(filePath, 5000);
    }

    private DataTable ParseLasToDataTable(string filePath, List<ChannelMapping> mappings)
    {
        var dataTable = new DataTable();
        
        foreach (var mapping in mappings)
        {
            var colName = mapping.MappedVumaxChannel == "Dynamic (New Column)" 
                ? mapping.CsvColumnHeader 
                : mapping.MappedVumaxChannel;

            if (!dataTable.Columns.Contains(colName))
            {
                dataTable.Columns.Add(colName, typeof(object));
            }
        }

        var headers = ParseLasHeaders(filePath);
        var rows = ParseLasRows(filePath);

        var headerIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++) headerIndices[headers[i]] = i;

        foreach (var rowData in rows)
        {
            var row = dataTable.NewRow();
            foreach (var mapping in mappings)
            {
                var colName = mapping.MappedVumaxChannel == "Dynamic (New Column)" 
                    ? mapping.CsvColumnHeader 
                    : mapping.MappedVumaxChannel;

                if (headerIndices.TryGetValue(mapping.CsvColumnHeader, out int index) && index < rowData.Count)
                {
                    string rawVal = rowData[index];
                    if (double.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        row[colName] = val;
                    }
                    else
                    {
                        row[colName] = rawVal;
                    }
                }
                else
                {
                    row[colName] = DBNull.Value;
                }
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    public double CalculateQcScore(DataTable data)
    {
        if (data.Rows.Count == 0) return 0;
        
        int validRows = 0;
        foreach (DataRow row in data.Rows)
        {
            bool isValid = true;
            
            if (data.Columns.Contains("Depth") && row.IsNull("Depth")) isValid = false;
            
            if (data.Columns.Contains("Hookload") && !row.IsNull("Hookload"))
            {
                if (double.TryParse(row["Hookload"]?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double hkld) && hkld < 0)
                {
                    isValid = false;
                }
            }

            if (isValid) validRows++;
        }

        return (double)validRows / data.Rows.Count * 100.0;
    }
}

