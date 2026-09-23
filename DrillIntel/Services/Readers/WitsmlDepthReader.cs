using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;

namespace DrillIntel.Services.Readers;

public class WitsmlDepthReader : IDepthLogFormatReader
{
    public bool CanHandle(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (ext.Equals(".witsml", StringComparison.OrdinalIgnoreCase)) return true;
        if (ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            // Inspect first few lines for witsml or log
            try
            {
                using var sr = new StreamReader(filePath);
                for (int i = 0; i < 20; i++)
                {
                    var line = sr.ReadLine();
                    if (line == null) break;
                    if (line.Contains("<log ", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("<logCurveInfo", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("<logSet", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
        }
        return false;
    }

    public List<string> GetWorksheets(string filePath) => new();

    public DepthLogMetadata? ExtractMetadata(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var metadata = new DepthLogMetadata();
        try
        {
            var settings = new XmlReaderSettings { CheckCharacters = false, DtdProcessing = DtdProcessing.Ignore };
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var xmlReader = XmlReader.Create(fileStream, settings);

            string currentElement = "";
            string currentMnemonic = "";
            string currentUnit = "";
            string currentDesc = "";
            string currentType = "";
            bool inCurveInfo = false;

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    currentElement = xmlReader.LocalName;
                    if (currentElement.Equals("logCurveInfo", StringComparison.OrdinalIgnoreCase))
                    {
                        inCurveInfo = true;
                        currentMnemonic = "";
                        currentUnit = "";
                        currentDesc = "";
                        currentType = "";
                    }
                }
                else if (xmlReader.NodeType == XmlNodeType.Text)
                {
                    string text = xmlReader.Value.Trim();
                    if (inCurveInfo)
                    {
                        if (currentElement.Equals("mnemonic", StringComparison.OrdinalIgnoreCase)) currentMnemonic = text;
                        else if (currentElement.Equals("unit", StringComparison.OrdinalIgnoreCase)) currentUnit = text;
                        else if (currentElement.Equals("curveDescription", StringComparison.OrdinalIgnoreCase)) currentDesc = text;
                        else if (currentElement.Equals("typeLogData", StringComparison.OrdinalIgnoreCase)) currentType = text;
                    }
                    else
                    {
                        if (currentElement.Equals("startIndex", StringComparison.OrdinalIgnoreCase)) metadata.StartIndex = text;
                        else if (currentElement.Equals("endIndex", StringComparison.OrdinalIgnoreCase)) metadata.EndIndex = text;
                        else if (currentElement.Equals("stepIncrement", StringComparison.OrdinalIgnoreCase)) metadata.StepIncrement = text;
                        else if (currentElement.Equals("indexCurve", StringComparison.OrdinalIgnoreCase)) metadata.IndexCurve = text;
                        else if (currentElement.Equals("nullValue", StringComparison.OrdinalIgnoreCase)) metadata.NullValue = text;
                        else if (currentElement.Equals("nameWell", StringComparison.OrdinalIgnoreCase)) metadata.WellName = text;
                    }
                }
                else if (xmlReader.NodeType == XmlNodeType.EndElement)
                {
                    if (xmlReader.LocalName.Equals("logCurveInfo", StringComparison.OrdinalIgnoreCase))
                    {
                        inCurveInfo = false;
                        if (!string.IsNullOrEmpty(currentMnemonic))
                        {
                            metadata.CurveUnits[currentMnemonic] = currentUnit;
                            metadata.CurveDescriptions[currentMnemonic] = currentDesc;
                            metadata.CurveDataTypes[currentMnemonic] = currentType;
                        }
                    }
                }
            }
        }
        catch { }

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

        try
        {
            var settings = new XmlReaderSettings { CheckCharacters = false, DtdProcessing = DtdProcessing.Ignore };
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var xmlReader = XmlReader.Create(fileStream, settings);

            bool inCurveInfo = false;
            string currentElement = "";

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    currentElement = xmlReader.LocalName;
                    if (currentElement.Equals("logCurveInfo", StringComparison.OrdinalIgnoreCase))
                    {
                        inCurveInfo = true;
                    }
                }
                else if (xmlReader.NodeType == XmlNodeType.Text && inCurveInfo)
                {
                    if (currentElement.Equals("mnemonic", StringComparison.OrdinalIgnoreCase))
                    {
                        var mnem = xmlReader.Value.Trim();
                        if (!string.IsNullOrEmpty(mnem) && !headers.Contains(mnem, StringComparer.OrdinalIgnoreCase))
                        {
                            headers.Add(mnem);
                        }
                    }
                }
                else if (xmlReader.NodeType == XmlNodeType.EndElement && xmlReader.LocalName.Equals("logCurveInfo", StringComparison.OrdinalIgnoreCase))
                {
                    inCurveInfo = false;
                }
            }
        }
        catch { }

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

        try
        {
            var settings = new XmlReaderSettings { CheckCharacters = false, DtdProcessing = DtdProcessing.Ignore };
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var xmlReader = XmlReader.Create(fileStream, settings);

            bool inData = false;
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.LocalName.Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    inData = true;
                }
                else if (xmlReader.NodeType == XmlNodeType.Text && inData)
                {
                    var lines = xmlReader.Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var l in lines)
                    {
                        var trimmed = l.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;

                        char delim = trimmed.Contains(',') ? ',' : (trimmed.Contains('\t') ? '\t' : ' ');
                        var tokens = delim == ' '
                            ? trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
                            : trimmed.Split(delim).Select(s => s.Trim()).ToList();
                        result.Add(tokens);
                        if (result.Count >= maxRows) return result;
                    }
                }
                else if (xmlReader.NodeType == XmlNodeType.EndElement && xmlReader.LocalName.Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    inData = false;
                }
            }
        }
        catch { }

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

        var settings = new XmlReaderSettings { CheckCharacters = false, DtdProcessing = DtdProcessing.Ignore };
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var xmlReader = XmlReader.Create(fileStream, settings);

        bool inData = false;
        while (xmlReader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.LocalName.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                inData = true;
            }
            else if (xmlReader.NodeType == XmlNodeType.Text && inData)
            {
                var lines = xmlReader.Value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in lines)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var trimmed = l.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    char delim = trimmed.Contains(',') ? ',' : (trimmed.Contains('\t') ? '\t' : ' ');
                    var tokens = delim == ' '
                        ? trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        : trimmed.Split(delim).Select(s => s.Trim()).ToArray();
                    yield return tokens;
                }
            }
            else if (xmlReader.NodeType == XmlNodeType.EndElement && xmlReader.LocalName.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                inData = false;
            }
        }
    }
}
