using System;

namespace DrillIntel.Models;

public class VmxTimeLog
{
    public int Id { get; set; }
    public string LogName { get; set; } = string.Empty;
    public string WellName { get; set; } = string.Empty;
    public string DataTableName { get; set; } = string.Empty;
    public string ImportStatus { get; set; } = string.Empty;
    public double QcScore { get; set; }
    public DateTime ImportDate { get; set; }
}
