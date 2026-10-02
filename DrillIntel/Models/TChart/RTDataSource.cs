using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Specifies the well, wellbore, and log identifiers that serve as data sources for a chart or track.
/// </summary>
public class RTDataSource
{
    public string WellID { get; set; } = string.Empty;
    public string WellboreID { get; set; } = string.Empty;
    public string DepthLogID { get; set; } = string.Empty;
    public string TimeLogID { get; set; } = string.Empty;
    public string TrajID { get; set; } = string.Empty;
    public enumRTDataSourceType DatasourceType { get; set; } = enumRTDataSourceType.TimeLog;

    public RTDataSource()
    {
    }

    public RTDataSource GetCopy()
    {
        return new RTDataSource
        {
            WellID = this.WellID,
            WellboreID = this.WellboreID,
            DepthLogID = this.DepthLogID,
            TimeLogID = this.TimeLogID,
            TrajID = this.TrajID,
            DatasourceType = this.DatasourceType
        };
    }

    public static RTDataSource GetCopy(RTDataSource? paramSource)
    {
        if (paramSource == null) return new RTDataSource();
        return paramSource.GetCopy();
    }
}

