using System.Collections.Generic;

namespace DrillIntel.Services;

public class DepthLogMappingResult
{
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? MappingFilePath { get; set; }

    /// <summary>
    /// Target channel mnemonic (e.g. "DEPTH", "HKLD") -> Source column name or index
    /// </summary>
    public Dictionary<string, string> TargetToSourceColumn { get; set; } = new();

    /// <summary>
    /// Target channel mnemonic -> 0-based column index (from .vmf)
    /// </summary>
    public Dictionary<string, int> TargetToColumnIndex { get; set; } = new();

    public int? ColumnHeadingRow { get; set; }
    public int? ImportFromRow { get; set; }
    public string? DateTimeSeparator { get; set; }
    public string? DatetimeSeparator { get => DateTimeSeparator; set => DateTimeSeparator = value; }
    public bool? DateTimeInSeparateCol { get; set; }
    public int? DateColNo { get; set; }
    public int? TimeColNo { get; set; }
    public string? DateFormat { get; set; }
}
