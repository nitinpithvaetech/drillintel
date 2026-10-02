using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Configuration for the shared vertical (Y) index axis across tracks.
/// </summary>
public class RTYAxis
{
    public string ID { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double Increment { get; set; } = 1;
    public bool AutoScale { get; set; } = true;
    public double Min { get; set; } = 0;
    public double Max { get; set; } = 0;
    public bool ShowMajorGrids { get; set; } = true;
    public int MajorTickCount { get; set; } = 4;
    public bool ShowMinorGrids { get; set; } = false;
    public int MinorTickCount { get; set; } = 4;
    public bool Visible { get; set; } = true;

    // Font properties
    public string LabelFontName { get; set; } = "Tahoma";
    public double LabelFontSize { get; set; } = 8;
    public string LabelFontColor { get; set; } = "yellow";
    public bool LabelFontBold { get; set; } = false;
    public bool LabelFontItalic { get; set; } = false;
    public bool LabelFontUnderline { get; set; } = false;

    // Major grid style
    public double MajorLineWidth { get; set; } = 1;
    public string MajorLineColor { get; set; } = "LightGray";
    public enumRTLineStyle MajorLineStyle { get; set; } = enumRTLineStyle.Solid;

    // Minor grid style
    public double MinorLineWidth { get; set; } = 1;
    public string MinorLineColor { get; set; } = "LightGray";
    public enumRTLineStyle MinorLineStyle { get; set; } = enumRTLineStyle.Solid;

    public RTYAxis()
    {
    }

    public RTYAxis GetCopy()
    {
        return new RTYAxis
        {
            ID = this.ID,
            Title = this.Title,
            Unit = this.Unit,
            Increment = this.Increment,
            AutoScale = this.AutoScale,
            Min = this.Min,
            Max = this.Max,
            ShowMajorGrids = this.ShowMajorGrids,
            MajorTickCount = this.MajorTickCount,
            ShowMinorGrids = this.ShowMinorGrids,
            MinorTickCount = this.MinorTickCount,
            Visible = this.Visible,
            LabelFontName = this.LabelFontName,
            LabelFontSize = this.LabelFontSize,
            LabelFontColor = this.LabelFontColor,
            LabelFontBold = this.LabelFontBold,
            LabelFontItalic = this.LabelFontItalic,
            LabelFontUnderline = this.LabelFontUnderline,
            MajorLineWidth = this.MajorLineWidth,
            MajorLineColor = this.MajorLineColor,
            MajorLineStyle = this.MajorLineStyle,
            MinorLineWidth = this.MinorLineWidth,
            MinorLineColor = this.MinorLineColor,
            MinorLineStyle = this.MinorLineStyle
        };
    }

    public static RTYAxis GetCopy(RTYAxis? paramSource)
    {
        if (paramSource == null) return new RTYAxis();
        return paramSource.GetCopy();
    }
}

