using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DrillIntel.Services.Readers;

public class MappingFileReader
{
    public DepthLogMappingResult LoadMappingFile(string mappingFilePath)
    {
        var result = new DepthLogMappingResult
        {
            MappingFilePath = mappingFilePath
        };

        if (!File.Exists(mappingFilePath))
        {
            result.Success = false;
            result.ErrorMessage = $"Mapping file not found: {mappingFilePath}";
            return result;
        }

        try
        {
            var ext = Path.GetExtension(mappingFilePath).ToLowerInvariant();
            if (ext == ".vmf" || IsVmfContent(mappingFilePath))
            {
                ParseVmf(mappingFilePath, result);
            }
            else if (ext == ".json")
            {
                ParseJson(mappingFilePath, result);
            }
            else
            {
                // Fallback to CSV / delimited
                ParseCsv(mappingFilePath, result);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error loading mapping file: {ex.Message}";
        }

        return result;
    }

    private bool IsVmfContent(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            var firstLine = reader.ReadLine();
            return firstLine != null && firstLine.Contains("[VuMaxDR Mapping Info.]", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ParseVmf(string filePath, DepthLogMappingResult result)
    {
        using var reader = new StreamReader(filePath);
        string? line;
        bool inInfo = false;
        bool inMappings = false;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.Equals("[VuMaxDR Mapping Info.]", StringComparison.OrdinalIgnoreCase))
            {
                inInfo = true;
                inMappings = false;
                continue;
            }

            if (trimmed.Equals("[ColumnMappings]", StringComparison.OrdinalIgnoreCase))
            {
                inInfo = false;
                inMappings = true;
                continue;
            }

            if (inInfo)
            {
                var parts = trimmed.Split('~');
                if (parts.Length >= 2)
                {
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();

                    if (key.Equals("ImportFromRow", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int ifr))
                    {
                        result.ImportFromRow = ifr;
                    }
                    else if (key.Equals("HeadingFromRow", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int hfr))
                    {
                        result.ColumnHeadingRow = hfr;
                    }
                    else if (key.Equals("DateTimeSeparator", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DateTimeSeparator = val;
                    }
                    else if (key.Equals("DateTimeInSeparateCol", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out bool disc))
                    {
                        result.DateTimeInSeparateCol = disc;
                    }
                    else if (key.Equals("DateColNo", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int dcn))
                    {
                        result.DateColNo = dcn;
                    }
                    else if (key.Equals("TimeColNo", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int tcn))
                    {
                        result.TimeColNo = tcn;
                    }
                    else if (key.Equals("DateFormat", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DateFormat = val;
                    }
                }
            }
            else if (inMappings)
            {
                var parts = trimmed.Split('~');
                if (parts.Length >= 2)
                {
                    var targetMnemonic = parts[0].Trim();
                    if (int.TryParse(parts[1].Trim(), out int colIndex))
                    {
                        result.TargetToColumnIndex[targetMnemonic] = colIndex;
                        result.TargetToSourceColumn[targetMnemonic] = $"#{colIndex}";
                    }
                    else
                    {
                        result.TargetToSourceColumn[targetMnemonic] = parts[1].Trim();
                    }
                }
            }
        }
    }

    private void ParseJson(string filePath, DepthLogMappingResult result)
    {
        string json = File.ReadAllText(filePath);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result.TargetToSourceColumn[prop.Name.Trim()] = prop.Value.GetString()?.Trim() ?? string.Empty;
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    string target = "";
                    string source = "";

                    foreach (var prop in elem.EnumerateObject())
                    {
                        if (prop.Name.Equals("Target", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("Channel", StringComparison.OrdinalIgnoreCase) ||
                            prop.Name.Equals("Mnemonic", StringComparison.OrdinalIgnoreCase))
                        {
                            target = prop.Value.GetString()?.Trim() ?? "";
                        }
                        else if (prop.Name.Equals("Source", StringComparison.OrdinalIgnoreCase) ||
                                 prop.Name.Equals("SourceColumn", StringComparison.OrdinalIgnoreCase) ||
                                 prop.Name.Equals("Column", StringComparison.OrdinalIgnoreCase))
                        {
                            source = prop.Value.GetString()?.Trim() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(source))
                    {
                        result.TargetToSourceColumn[target] = source;
                    }
                }
            }
        }
    }

    private void ParseCsv(string filePath, DepthLogMappingResult result)
    {
        using var reader = new StreamReader(filePath);
        string? line;
        bool firstLine = true;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var seps = new[] { ',', ';', '\t' };
            var parts = trimmed.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var target = parts[0].Trim();
                var source = parts[1].Trim();

                if (firstLine && (target.Equals("Target", StringComparison.OrdinalIgnoreCase) || target.Equals("VuMax", StringComparison.OrdinalIgnoreCase)))
                {
                    firstLine = false;
                    continue;
                }

                firstLine = false;
                result.TargetToSourceColumn[target] = source;
            }
        }
    }
}

