using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents Roadmap entry parameters and metadata.
/// </summary>
public class RMEx
{
    public string ID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public double Value { get; set; } = 0;
    public bool Visible { get; set; } = true;

    public RMEx()
    {
    }

    public RMEx GetCopy()
    {
        return new RMEx
        {
            ID = this.ID,
            Name = this.Name,
            Description = this.Description,
            Color = this.Color,
            Value = this.Value,
            Visible = this.Visible
        };
    }

    public static RMEx GetCopy(RMEx? paramSource)
    {
        if (paramSource == null) return new RMEx();
        return paramSource.GetCopy();
    }
}

