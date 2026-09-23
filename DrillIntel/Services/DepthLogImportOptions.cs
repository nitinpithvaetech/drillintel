using System.Collections.Generic;
using DrillIntel.Models;

namespace DrillIntel.Services;

public class DepthLogImportOptions
{
    public OperationType OperationType { get; set; } = OperationType.NewData;
    public string? TargetTableName { get; set; }
    public string? LogName { get; set; }
    public string? WellName { get; set; }
    public string? WellID { get; set; }
    public string? WellboreID { get; set; }
    public int ColumnHeadingRow { get; set; } = 1;
    public int ImportFromRow { get; set; } = 2;
    public string? WorksheetName { get; set; }
    public string? ManualDepthColumnName { get; set; }
    public Dictionary<string, string>? ColumnMappings { get; set; }
    public string? MappingFilePath { get; set; }
    public string Delimiter { get; set; } = ",";
}

