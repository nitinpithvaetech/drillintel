using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Holds widget customization payload and user/well metadata for track persistence.
/// </summary>
public class VHTracksInfo
{
    public string WellId { get; set; } = string.Empty;
    public string WidgetId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CompId { get; set; } = string.Empty;
    public string ThemeId { get; set; } = string.Empty;
    public string CustomizationData { get; set; } = string.Empty;

    // TypeScript compatibility aliases
    [System.Text.Json.Serialization.JsonIgnore]
    public string themeId { get => ThemeId; set => ThemeId = value; }

    public VHTracksInfo()
    {
    }

    public VHTracksInfo GetCopy()
    {
        return new VHTracksInfo
        {
            WellId = this.WellId,
            WidgetId = this.WidgetId,
            UserId = this.UserId,
            CompId = this.CompId,
            ThemeId = this.ThemeId,
            CustomizationData = this.CustomizationData
        };
    }

    public static VHTracksInfo GetCopy(VHTracksInfo? paramSource)
    {
        if (paramSource == null) return new VHTracksInfo();
        return paramSource.GetCopy();
    }
}

