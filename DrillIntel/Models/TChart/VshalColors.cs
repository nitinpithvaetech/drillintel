using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Color triplet used for VSHAL (volume of shale) lithology representation.
/// </summary>
public class VshalColors
{
    public string Color1 { get; set; } = "#F7F6E5";
    public string Color2 { get; set; } = "#76D2DB";
    public string Color3 { get; set; } = "#DA4848";

    // TypeScript compatibility aliases
    [System.Text.Json.Serialization.JsonIgnore]
    public string color1 { get => Color1; set => Color1 = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string color2 { get => Color2; set => Color2 = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string color3 { get => Color3; set => Color3 = value; }

    public VshalColors()
    {
    }

    public VshalColors(string color1, string color2, string color3)
    {
        Color1 = color1 ?? "#F7F6E5";
        Color2 = color2 ?? "#76D2DB";
        Color3 = color3 ?? "#DA4848";
    }

    public VshalColors GetCopy()
    {
        return new VshalColors
        {
            Color1 = this.Color1,
            Color2 = this.Color2,
            Color3 = this.Color3
        };
    }

    public static VshalColors GetCopy(VshalColors? paramSource)
    {
        if (paramSource == null) return new VshalColors();
        return paramSource.GetCopy();
    }
}

