namespace DrillIntel.Models;

/// <summary>
/// Lookup options for linking depth logs.
/// </summary>
public class DepthLogOption
{
    public string LogId { get; set; } = string.Empty;
    public string WellId { get; set; } = string.Empty;
    public string WellboreId { get; set; } = string.Empty;
    public string LogName { get; set; } = string.Empty;
    public override string ToString() => LogName;
}

