using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DrillIntel.Services.Readers;

public class LasDepthReader : IDepthLogFormatReader
{
    public bool CanHandle(string filePath)
    {
        return Path.GetExtension(filePath).Equals(".las", StringComparison.OrdinalIgnoreCase);
    }

    public List<string> GetWorksheets(string filePath) => new();

    public DepthLogMetadata? ExtractMetadata(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var metadata = new DepthLogMetadata();
        bool inWell = false;
        bool inCurve = false;

        using var reader = new StreamReader(filePath, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~W", StringComparison.OrdinalIgnoreCase))
                {
                    inWell = true;
                    inCurve = false;
                    continue;
                }
                else if (trimmed.StartsWith("~C", StringComparison.OrdinalIgnoreCase))
                {
                    inWell = false;
                    inCurve = true;
                    continue;
                }
                else if (trimmed.StartsWith("~A", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                else
                {
                    inWell = false;
                    inCurve = false;
                    continue;
                }
            }

            if (trimmed.StartsWith("#")) continue;

            if (inWell)
            {
                // Format: MNEM.UNIT VALUE: DESCRIPTION
                var colonIdx = trimmed.IndexOf(':');
                var headerPart = colonIdx >= 0 ? trimmed.Substring(0, colonIdx).Trim() : trimmed;
                var dotIdx = headerPart.IndexOf('.');
                var mnem = dotIdx >= 0 ? headerPart.Substring(0, dotIdx).Trim().ToUpperInvariant() : headerPart.ToUpperInvariant();
                
                string val = "";
                if (dotIdx >= 0)
                {
                    var remainder = headerPart.Substring(dotIdx + 1).Trim();
                    // Remainder may have UNIT followed by spaces and VALUE
                    var parts = remainder.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1)
                    {
                        val = parts[parts.Length - 1];
                    }
                    else if (parts.Length == 1)
                    {
                        val = parts[0];
                    }
                }

                if (mnem == "STRT") metadata.StartIndex = val;
                else if (mnem == "STOP") metadata.EndIndex = val;
                else if (mnem == "STEP") metadata.StepIncrement = val;
                else if (mnem == "NULL") metadata.NullValue = val;
                else if (mnem == "WELL")
                {
                    if (colonIdx >= 0 && colonIdx + 1 < trimmed.Length)
                    {
                        var desc = trimmed.Substring(colonIdx + 1).Trim();
                        metadata.WellName = string.IsNullOrEmpty(desc) ? val : desc;
                    }
                }
            }

            if (inCurve)
            {
                // Format: MNEM.UNIT : DESCRIPTION
                var colonIdx = trimmed.IndexOf(':');
                var desc = colonIdx >= 0 ? trimmed.Substring(colonIdx + 1).Trim() : "";
                var headerPart = colonIdx >= 0 ? trimmed.Substring(0, colonIdx).Trim() : trimmed;
                var dotIdx = headerPart.IndexOf('.');
                var mnem = dotIdx >= 0 ? headerPart.Substring(0, dotIdx).Trim() : headerPart;
                var unit = "";
                if (dotIdx >= 0)
                {
                    unit = headerPart.Substring(dotIdx + 1).Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                }

                if (!string.IsNullOrEmpty(mnem))
                {
                    if (string.IsNullOrEmpty(metadata.IndexCurve)) metadata.IndexCurve = mnem;
                    metadata.CurveUnits[mnem] = unit;
                    metadata.CurveDescriptions[mnem] = desc;
                }
            }
        }

        return metadata;
    }

    public List<string> GetHeaders(
        string filePath,
        int columnHeadingRow = 1,
        string? worksheetName = null,
        string delimiter = ",")
    {
        var headers = new List<string>();
        if (!File.Exists(filePath)) return headers;

        bool inCurve = false;
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~C", StringComparison.OrdinalIgnoreCase))
                {
                    inCurve = true;
                    continue;
                }
                else if (inCurve)
                {
                    break;
                }
            }

            if (inCurve && !trimmed.StartsWith("#"))
            {
                var dotIdx = trimmed.IndexOf('.');
                string mnem = "";
                if (dotIdx >= 0)
                {
                    mnem = trimmed.Substring(0, dotIdx).Trim();
                }
                else
                {
                    mnem = trimmed.Split(new[] { ' ', '\t', ':' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                }

                if (!string.IsNullOrEmpty(mnem) && !headers.Contains(mnem, StringComparer.OrdinalIgnoreCase))
                {
                    headers.Add(mnem);
                }
            }
        }

        return headers;
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

        bool inAscii = false;
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        string? line;
        int rowCount = 0;

        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~A", StringComparison.OrdinalIgnoreCase))
                {
                    inAscii = true;
                }
                continue;
            }

            if (inAscii && !trimmed.StartsWith("#"))
            {
                rowCount++;
                var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                result.Add(tokens);

                if (result.Count >= maxRows) break;
            }
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

        bool inAscii = false;
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("~"))
            {
                if (trimmed.StartsWith("~A", StringComparison.OrdinalIgnoreCase))
                {
                    inAscii = true;
                }
                continue;
            }

            if (inAscii && !trimmed.StartsWith("#"))
            {
                var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                yield return tokens;
            }
        }
    }
}
