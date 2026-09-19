using System;

namespace DrillIntel.Models;

public class Timelog
{
    public int Id { get; set; }
    public int WellID { get; set; }
    public DateTime Timestamp { get; set; }
    public double Depth { get; set; }
    public double Hookload { get; set; }
    public double RPM { get; set; }
    public double PumpPressure { get; set; }
    public double Torque { get; set; }
}
