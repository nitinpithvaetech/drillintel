using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents an anchor point with position and size.
/// </summary>
public class AnchorPoint
{
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;
    public double Size { get; set; } = 0;

    // TypeScript compatibility aliases
    public double x { get => X; set => X = value; }
    public double y { get => Y; set => Y = value; }
    public double size { get => Size; set => Size = value; }

    public AnchorPoint()
    {
    }

    public AnchorPoint(double x, double y, double size = 0)
    {
        X = x;
        Y = y;
        Size = size;
    }

    public AnchorPoint GetCopy()
    {
        return new AnchorPoint
        {
            X = this.X,
            Y = this.Y,
            Size = this.Size
        };
    }

    public static AnchorPoint GetCopy(AnchorPoint? paramSource)
    {
        if (paramSource == null) return new AnchorPoint();
        return paramSource.GetCopy();
    }
}

/// <summary>
/// Alias for AnchorPoint matching original TypeScript camelCase naming.
/// </summary>
public class anchorPoint : AnchorPoint
{
}

