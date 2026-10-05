using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents a bounding rectangle for chart headers and content areas.
/// </summary>
public class RTRectangle
{
    public double Height { get; set; } = 0;
    public double Width { get; set; } = 0;
    public double Left { get; set; } = 0;
    public double Top { get; set; } = 0;
    public double Right { get; set; } = 0;
    public double Bottom { get; set; } = 0;

    // TypeScript compatibility aliases
    [System.Text.Json.Serialization.JsonIgnore]
    public double height { get => Height; set => Height = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double width { get => Width; set => Width = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double left { get => Left; set => Left = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double top { get => Top; set => Top = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double right { get => Right; set => Right = value; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double bottom { get => Bottom; set => Bottom = value; }

    public RTRectangle()
    {
    }

    public RTRectangle(double left, double top, double width, double height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Right = left + width;
        Bottom = top + height;
    }

    public RTRectangle GetCopy()
    {
        return new RTRectangle
        {
            Height = this.Height,
            Width = this.Width,
            Left = this.Left,
            Top = this.Top,
            Right = this.Right,
            Bottom = this.Bottom
        };
    }

    public static RTRectangle GetCopy(RTRectangle? paramSource)
    {
        if (paramSource == null) return new RTRectangle();
        return paramSource.GetCopy();
    }
}

