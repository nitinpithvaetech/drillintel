using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents a saved template definition of a track console.
/// </summary>
public class VHTrackTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TrackConsoleObject { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;

    public VHTrackTemplate()
    {
    }

    public VHTrackTemplate GetCopy()
    {
        return new VHTrackTemplate
        {
            Id = this.Id,
            Name = this.Name,
            TrackConsoleObject = this.TrackConsoleObject,
            Type = this.Type
        };
    }

    public static VHTrackTemplate GetCopy(VHTrackTemplate? paramSource)
    {
        if (paramSource == null) return new VHTrackTemplate();
        return paramSource.GetCopy();
    }
}

