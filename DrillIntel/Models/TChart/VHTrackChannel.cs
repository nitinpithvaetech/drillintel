using System;
using System.Collections.Generic;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Represents a curve / channel plotted on a track within the chart console.
/// </summary>
public class VHTrackChannel
{
    public string ID { get; set; } = string.Empty;
    public string TrackID { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public enumRTSeriesStyle SeriesType { get; set; } = enumRTSeriesStyle.Line;

    // Axis scale
    public string XAxisID { get; set; } = string.Empty;
    public RTAxis objXAxis { get; set; } = new RTAxis();
    public string Unit { get; set; } = string.Empty;
    public string Mnemonic { get; set; } = string.Empty;

    // Grouping & Smoothing (0 - Avg, 1 - Min, 2 - Max)
    public int GroupMethod { get; set; } = 0;
    public int SmoothMethod { get; set; } = 0;
    public bool SmoothData { get; set; } = false;
    public bool IgnoreNegative { get; set; } = true;
    public int SmoothPoints { get; set; } = 10;

    public bool ColorCodeAsRigState { get; set; } = false;
    public bool ShowColorDepthRange { get; set; } = false;

    // Title Font
    public string TitleFontFontName { get; set; } = "Tahoma";
    public double TitleFontFontSize { get; set; } = 8;
    public string TitleFontFontColor { get; set; } = "Black";
    public bool TitleFontFontBold { get; set; } = false;
    public bool TitleFontFontItalic { get; set; } = false;
    public bool TitleFontFontUnderline { get; set; } = false;

    // Line Style
    public double LineWidth { get; set; } = 2;
    public string LineColor { get; set; } = "green";
    public enumRTLineStyle LineStyle { get; set; } = enumRTLineStyle.Solid;
    public bool LineShowPoints { get; set; } = false;
    public enumRTPointStyle LinePointStyle { get; set; } = enumRTPointStyle.Square;
    public double LinePointBorderWidth { get; set; } = 1;
    public string LinePointBorderColor { get; set; } = "green";
    public enumRTLineStyle LinePointBorderStyle { get; set; } = enumRTLineStyle.Solid;
    public double LinePointHeight { get; set; } = 2;
    public double LinePointWidth { get; set; } = 2;
    public string LinePointFillColor { get; set; } = "Blue";
    public double LinePointTransparency { get; set; } = 0;

    public bool Visible { get; set; } = true;

    // Area Style
    public string AreaFillColor { get; set; } = "transparent";
    public double AreaTransparency { get; set; } = 0;
    public double AreaLineWidth { get; set; } = 1;
    public string AreaLineColor { get; set; } = "yellow";
    public enumRTLineStyle AreaLineStyle { get; set; } = enumRTLineStyle.Solid;

    // Point Style
    public enumRTPointStyle PointStyle { get; set; } = enumRTPointStyle.Square;
    public double PointBorderWidth { get; set; } = 1;
    public string PointBorderColor { get; set; } = "red";
    public enumRTLineStyle PointBorderStyle { get; set; } = enumRTLineStyle.Solid;
    public double PointHeight { get; set; } = 2;
    public double PointWidth { get; set; } = 2;
    public string PointFillColor { get; set; } = "Blue";
    public double PointTransparency { get; set; } = 0;

    public enumRTDataSourceType SourceType { get; set; } = enumRTDataSourceType.DepthLog;

    // Data buffers
    public List<double> xData { get; set; } = new List<double>();
    public List<double> yData { get; set; } = new List<double>();

    // Curve Fill
    public bool fillCurve { get; set; } = false;
    public int fillType { get; set; } = 0; // 0 - Curve To Value, 1 - Value to Curve, 2 - Curve to Curve
    public double fillValue { get; set; } = 0;
    public string fillMnemonic { get; set; } = string.Empty;
    public string fillColor { get; set; } = "Yellow";
    public double fillTransparency { get; set; } = 0;
    public int fillDirection { get; set; } = 0; // 0: both, 1: left, 2: right

    // Gap handling
    public bool HideGaps { get; set; } = false;
    public double MaxGap { get; set; } = 10;

    // Roadmap & RMA
    public bool highlightRoadmap { get; set; } = false;
    public bool showRMA { get; set; } = false;
    public double RMAPeriod { get; set; } = 100;
    public string RMAColor { get; set; } = "#FF0000";
    public double RMALineWidth { get; set; } = 1;
    public enumRTLineStyle RMALineStyle { get; set; } = enumRTLineStyle.Dot;

    // Runtime / Layout
    public RTRectangle? headerRect { get; set; } = new RTRectangle();
    public object? __scale { get; set; } = null;
    public object? __chartSeries { get; set; } = null;

    // Multiwell Track Fields
    public string WellId { get; set; } = string.Empty;
    public string WellBoreId { get; set; } = string.Empty;
    public string logId { get; set; } = string.Empty;

    public VHColorDepthRange ColorDepthRange { get; set; } = new VHColorDepthRange();
    public VshalColors vShalColors { get; set; } = new VshalColors();

    public VHTrackChannel()
    {
    }

    public VHTrackChannel GetCopy()
    {
        return new VHTrackChannel
        {
            ID = this.ID,
            TrackID = this.TrackID,
            Name = this.Name,
            Title = this.Title,
            SeriesType = this.SeriesType,
            XAxisID = this.XAxisID,
            objXAxis = this.objXAxis?.GetCopy() ?? new RTAxis(),
            Unit = this.Unit,
            Mnemonic = this.Mnemonic,
            GroupMethod = this.GroupMethod,
            SmoothMethod = this.SmoothMethod,
            SmoothData = this.SmoothData,
            IgnoreNegative = this.IgnoreNegative,
            SmoothPoints = this.SmoothPoints,
            ColorCodeAsRigState = this.ColorCodeAsRigState,
            ShowColorDepthRange = this.ShowColorDepthRange,
            TitleFontFontName = this.TitleFontFontName,
            TitleFontFontSize = this.TitleFontFontSize,
            TitleFontFontColor = this.TitleFontFontColor,
            TitleFontFontBold = this.TitleFontFontBold,
            TitleFontFontItalic = this.TitleFontFontItalic,
            TitleFontFontUnderline = this.TitleFontFontUnderline,
            LineWidth = this.LineWidth,
            LineColor = this.LineColor,
            LineStyle = this.LineStyle,
            LineShowPoints = this.LineShowPoints,
            LinePointStyle = this.LinePointStyle,
            LinePointBorderWidth = this.LinePointBorderWidth,
            LinePointBorderColor = this.LinePointBorderColor,
            LinePointBorderStyle = this.LinePointBorderStyle,
            LinePointHeight = this.LinePointHeight,
            LinePointWidth = this.LinePointWidth,
            LinePointFillColor = this.LinePointFillColor,
            LinePointTransparency = this.LinePointTransparency,
            Visible = this.Visible,
            AreaFillColor = this.AreaFillColor,
            AreaTransparency = this.AreaTransparency,
            AreaLineWidth = this.AreaLineWidth,
            AreaLineColor = this.AreaLineColor,
            AreaLineStyle = this.AreaLineStyle,
            PointStyle = this.PointStyle,
            PointBorderWidth = this.PointBorderWidth,
            PointBorderColor = this.PointBorderColor,
            PointBorderStyle = this.PointBorderStyle,
            PointHeight = this.PointHeight,
            PointWidth = this.PointWidth,
            PointFillColor = this.PointFillColor,
            PointTransparency = this.PointTransparency,
            SourceType = this.SourceType,
            xData = new List<double>(this.xData),
            yData = new List<double>(this.yData),
            fillCurve = this.fillCurve,
            fillType = this.fillType,
            fillValue = this.fillValue,
            fillMnemonic = this.fillMnemonic,
            fillColor = this.fillColor,
            fillTransparency = this.fillTransparency,
            fillDirection = this.fillDirection,
            HideGaps = this.HideGaps,
            MaxGap = this.MaxGap,
            highlightRoadmap = this.highlightRoadmap,
            showRMA = this.showRMA,
            RMAPeriod = this.RMAPeriod,
            RMAColor = this.RMAColor,
            RMALineWidth = this.RMALineWidth,
            RMALineStyle = this.RMALineStyle,
            headerRect = this.headerRect?.GetCopy(),
            __scale = this.__scale,
            __chartSeries = this.__chartSeries,
            WellId = this.WellId,
            WellBoreId = this.WellBoreId,
            logId = this.logId,
            ColorDepthRange = this.ColorDepthRange?.GetCopy() ?? new VHColorDepthRange(),
            vShalColors = this.vShalColors?.GetCopy() ?? new VshalColors()
        };
    }

    public static VHTrackChannel GetCopy(VHTrackChannel? paramSource)
    {
        if (paramSource == null) return new VHTrackChannel();
        return paramSource.GetCopy();
    }
}

