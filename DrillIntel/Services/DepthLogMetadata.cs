using System.Collections.Generic;

namespace DrillIntel.Services;

public class DepthLogMetadata
{
    public string? StartIndex { get; set; }
    public string? EndIndex { get; set; }
    public string? LastDataIndex { get; set; }
    public string? StepIncrement { get; set; }
    public string? IndexCurve { get; set; }
    public string? NullValue { get; set; } = "-999.25";
    public string? WellName { get; set; }
    public string? ServiceCompany { get; set; }
    public Dictionary<string, string> CurveUnits { get; set; } = new();
    public Dictionary<string, string> CurveDescriptions { get; set; } = new();
    public Dictionary<string, string> CurveDataTypes { get; set; } = new();
}
