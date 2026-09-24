using System;

namespace DrillIntel.Models;

public class ImportProgressReport
{
    public int RowsProcessed { get; set; }
    public double PercentCompleted { get; set; }
    public bool IsIndeterminate { get; set; } = true;
    public string StatusMessage { get; set; } = string.Empty;
}

public class StreamImportResult
{
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public double QcScore { get; set; }
    public string TableName { get; set; } = string.Empty;
    public double? MinDepth { get; set; }
    public double? MaxDepth { get; set; }
    public double? FirstDepth { get; set; }
    public double? LastDepth { get; set; }
    public string? StepIncrement { get; set; }
    public string? LastDataIndex { get; set; }
    public string? MinDate { get; set; }
    public string? MaxDate { get; set; }
    public bool IsSequential { get; set; } = true;
    public int NonSequentialCount { get; set; }
    public System.Collections.Generic.List<string> ValidationMessages { get; set; } = new();
}

