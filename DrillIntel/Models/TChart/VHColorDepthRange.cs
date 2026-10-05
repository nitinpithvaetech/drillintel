using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Defines a depth range with gradient/3-color shading configuration.
/// </summary>
public class VHColorDepthRange
{
    public string Id { get; set; } = string.Empty;
    public double FromDepth { get; set; } = 0;
    public double ToDepth { get; set; } = 0;
    public string Color1 { get; set; } = "cyan";
    public string Color2 { get; set; } = "yellow";
    public string Color3 { get; set; } = "red";

    // TypeScript compatibility aliases
    [System.Text.Json.Serialization.JsonIgnore]
    public string id { get => Id; set => Id = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double fromDepth { get => FromDepth; set => FromDepth = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double toDepth { get => ToDepth; set => ToDepth = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string color1 { get => Color1; set => Color1 = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string color2 { get => Color2; set => Color2 = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string color3 { get => Color3; set => Color3 = value; }

    public VHColorDepthRange()
    {
    }

    public VHColorDepthRange GetCopy()
    {
        return new VHColorDepthRange
        {
            Id = this.Id,
            FromDepth = this.FromDepth,
            ToDepth = this.ToDepth,
            Color1 = this.Color1,
            Color2 = this.Color2,
            Color3 = this.Color3
        };
    }

    public static VHColorDepthRange GetCopy(VHColorDepthRange? paramSource)
    {
        if (paramSource == null) return new VHColorDepthRange();
        return paramSource.GetCopy();
    }
}

