using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents multi-well configuration information for multi-well tracks.
/// </summary>
public class MultiWellInfoEx
{
    public string WellID { get; set; } = string.Empty;
    public string WellName { get; set; } = string.Empty;
    public string WellboreID { get; set; } = string.Empty;
    public string WellboreName { get; set; } = string.Empty;
    public string TimeLogID { get; set; } = string.Empty;
    public string DepthLogID { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;

    public MultiWellInfoEx()
    {
    }

    public MultiWellInfoEx GetCopy()
    {
        return new MultiWellInfoEx
        {
            WellID = this.WellID,
            WellName = this.WellName,
            WellboreID = this.WellboreID,
            WellboreName = this.WellboreName,
            TimeLogID = this.TimeLogID,
            DepthLogID = this.DepthLogID,
            Color = this.Color,
            Visible = this.Visible
        };
    }

    public static MultiWellInfoEx GetCopy(MultiWellInfoEx? paramSource)
    {
        if (paramSource == null) return new MultiWellInfoEx();
        return paramSource.GetCopy();
    }
}

