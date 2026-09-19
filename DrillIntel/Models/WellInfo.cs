using System;

namespace DrillIntel.Models;

public class WellInfo
{
    public int WellID { get; set; }
    public string WellName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public DateTime SpudDate { get; set; }
}
