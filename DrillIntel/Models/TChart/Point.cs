using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents a 2D coordinate point (X, Y) for chart positioning.
/// </summary>
public class Point
{
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;

    public Point()
    {
    }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Point GetCopy()
    {
        return new Point
        {
            X = this.X,
            Y = this.Y
        };
    }

    public static Point GetCopy(Point? paramSource)
    {
        if (paramSource == null) return new Point();
        return paramSource.GetCopy();
    }
}

