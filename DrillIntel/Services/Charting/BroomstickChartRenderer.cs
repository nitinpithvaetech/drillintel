using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using DrillIntel.Models.Broomstick;
using Steema.TeeChart;
using Steema.TeeChart.Styles;
using WpfTChart = Steema.TeeChart.WPF.TChart;

namespace DrillIntel.Services.Charting;

public class BroomstickHoverInfo
{
    public double Depth { get; set; }
    public double Weight { get; set; }
    public string PointType { get; set; } = "Point";
    public string RigStateName { get; set; } = string.Empty;
    public string DateTimeText { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#E91E63";
}

/// <summary>
/// Dedicated chart renderer for Combined Broomstick Plots on Steema TeeChart WPF.
/// Plots theoretical Hookload/Torque plan curves across multiple friction factors
/// alongside calculated broomstick scatter points (PU, SO, ROT, Casing).
/// </summary>
public class BroomstickChartRenderer
{
    private readonly WpfTChart _tChart;

    // Series Cache
    private readonly Dictionary<string, Series> _seriesMap = new();

    public BroomstickChartRenderer(WpfTChart tChart)
    {
        _tChart = tChart ?? throw new ArgumentNullException(nameof(tChart));
        InitializeChartSettings();
    }

    private void InitializeChartSettings()
    {
        _tChart.Aspect.View3D = false;
        _tChart.Legend.Visible = false;
        _tChart.Header.Visible = false;

        // Dark theme matching eVumaxPro
        _tChart.Panel.Brush.Color = Color.Black;
        _tChart.Panel.MarginUnits = PanelMarginUnits.Pixels;
        _tChart.Panel.MarginTop = 16;
        _tChart.Panel.MarginBottom = 20;
        _tChart.Panel.MarginLeft = 24;
        _tChart.Panel.MarginRight = 16;

        // Inverted Depth Left Axis
        _tChart.Axes.Left.Inverted = true;
        _tChart.Axes.Left.AxisPen.Color = Color.FromArgb(70, 80, 95);
        _tChart.Axes.Left.Grid.Color = Color.FromArgb(30, 36, 48);
        _tChart.Axes.Left.Labels.Font.Color = Color.FromArgb(180, 190, 205);
        _tChart.Axes.Left.Labels.Font.Size = 9;
        _tChart.Axes.Left.Title.Text = "Depth (ft)";
        _tChart.Axes.Left.Title.Font.Color = Color.FromArgb(220, 225, 235);
        _tChart.Axes.Left.Title.Font.Bold = true;

        // Bottom Weight Axis
        _tChart.Axes.Bottom.AxisPen.Color = Color.FromArgb(70, 80, 95);
        _tChart.Axes.Bottom.Grid.Color = Color.FromArgb(30, 36, 48);
        _tChart.Axes.Bottom.Labels.Font.Color = Color.FromArgb(180, 190, 205);
        _tChart.Axes.Bottom.Labels.Font.Size = 9;
        _tChart.Axes.Bottom.Title.Text = "Weight (klbs)";
        _tChart.Axes.Bottom.Title.Font.Color = Color.FromArgb(220, 225, 235);
        _tChart.Axes.Bottom.Title.Font.Bold = true;

        _tChart.Zoom.Direction = ZoomDirections.Both;
    }

    public void RenderDocument(
        BroomstickDocument document,
        List<PUSOPoint> filteredPoints,
        double minDepth,
        double maxDepth)
    {
        if (document == null) return;

        _tChart.Series.Clear();
        _seriesMap.Clear();

        // 1. Configure Inverted Depth and Unit Axes
        _tChart.Axes.Left.Inverted = document.PlotSettings.InvertDepthAxis;
        _tChart.Axes.Bottom.Title.Text = $"Weight ({document.PlotSettings.UnitSystem})";

        // Set axis depth extents
        if (maxDepth > minDepth && maxDepth > 0)
        {
            _tChart.Axes.Left.SetMinMax(minDepth, maxDepth);
        }
        else
        {
            _tChart.Axes.Left.Automatic = true;
        }

        // 2. Render Theoretical Curves from BroomstickPlanData
        RenderTheoreticalCurves(document);

        // 3. Render Actual Scatter Points
        RenderScatterPoints(document, filteredPoints);

        _tChart.Invalidate();
    }

    private void RenderTheoreticalCurves(BroomstickDocument doc)
    {
        var plan = doc.BroomstickPlanData;
        var vis = doc.PlotSettings.SeriesVisibility;

        if (plan == null) return;

        // Pick-up curves (cyan shades)
        if (plan.pickup != null && plan.pickup.Count > 0)
        {
            if (IsSeriesVisible(vis, "plan03"))
            {
                var s = CreateLineSeries("plan03", "8.5\" Hookload Drilling 0.3 OHFF", Color.FromArgb(0, 229, 255), 2);
                foreach (var pt in plan.pickup.Values.OrderBy(p => p.Depth))
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }
        }

        // Slack-off curves (emerald green shades)
        if (plan.slackoff != null && plan.slackoff.Count > 0)
        {
            if (IsSeriesVisible(vis, "so03"))
            {
                var s = CreateLineSeries("so03", "8.5\" Hookload Drilling 0.3 OHFF (SO)", Color.FromArgb(0, 230, 118), 2);
                foreach (var pt in plan.slackoff.Values.OrderBy(p => p.Depth))
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }
        }

        // Rotate curve (white)
        if (plan.rotate != null && plan.rotate.Count > 0)
        {
            if (IsSeriesVisible(vis, "rotTheory"))
            {
                var s = CreateLineSeries("rotTheory", "8.5\" Hookload Rotate", Color.White, 2);
                foreach (var pt in plan.rotate.Values.OrderBy(p => p.Depth))
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }
        }
    }

    private void RenderScatterPoints(BroomstickDocument doc, List<PUSOPoint> points)
    {
        if (points == null || points.Count == 0) return;

        var vis = doc.PlotSettings.SeriesVisibility;

        // Pick-Up Points (PU)
        if (IsSeriesVisible(vis, "actualPu"))
        {
            var puSeries = CreatePointsSeries("actualPu", "Pick up", Color.FromArgb(233, 30, 99), 6);
            foreach (var pt in points.Where(p => p.Type == 1 || p.Type == 2)) // Pick up types
            {
                double val = pt.DynamicValue > 0 ? pt.DynamicValue : pt.Avg;
                if (val > 0) puSeries.Add(val, pt.Depth);
            }
        }

        // Slack-Off Points (SO)
        if (IsSeriesVisible(vis, "actualSo"))
        {
            var soSeries = CreatePointsSeries("actualSo", "Slack Off", Color.FromArgb(41, 121, 255), 6);
            foreach (var pt in points.Where(p => p.Type == 3 || p.Type == 4)) // Slack off types
            {
                double val = pt.DynamicValue > 0 ? pt.DynamicValue : pt.Avg;
                if (val > 0) soSeries.Add(val, pt.Depth);
            }
        }

        // Rotate Points (ROT)
        if (IsSeriesVisible(vis, "actualRot"))
        {
            var rotSeries = CreatePointsSeries("actualRot", "Rotate", Color.FromArgb(255, 152, 0), 5);
            foreach (var pt in points.Where(p => p.Type == 5)) // Rotate types
            {
                double val = pt.DynamicValue > 0 ? pt.DynamicValue : pt.Avg;
                if (val > 0) rotSeries.Add(val, pt.Depth);
            }
        }
    }

    private FastLine CreateLineSeries(string id, string title, Color color, int width)
    {
        var s = new FastLine(_tChart.Chart)
        {
            Title = title,
            LinePen = { Color = color, Width = width },
            DrawAllPoints = true
        };
        _seriesMap[id] = s;
        return s;
    }

    private Points CreatePointsSeries(string id, string title, Color color, int size)
    {
        var s = new Points(_tChart.Chart)
        {
            Title = title,
            Color = color
        };
        s.Pointer.Style = PointerStyles.Rectangle;
        s.Pointer.HorizSize = size;
        s.Pointer.VertSize = size;
        s.Pointer.Pen.Visible = false;
        s.Pointer.Brush.Color = color;
        _seriesMap[id] = s;
        return s;
    }

    private static bool IsSeriesVisible(Dictionary<string, bool> visibilityMap, string key)
    {
        if (visibilityMap == null || !visibilityMap.ContainsKey(key)) return true;
        return visibilityMap[key];
    }

    public BroomstickHoverInfo? GetHoverInfo(System.Windows.Point mousePos, List<PUSOPoint> points)
    {
        if (points == null || points.Count == 0) return null;

        try
        {
            int px = (int)mousePos.X;
            int py = (int)mousePos.Y;

            double depthAtPos = _tChart.Axes.Left.CalcPosPoint(py);
            double weightAtPos = _tChart.Axes.Bottom.CalcPosPoint(px);

            // Find closest scatter point within tolerance
            var closest = points
                .Select(p => new
                {
                    Point = p,
                    Dist = Math.Abs(p.Depth - depthAtPos) + Math.Abs((p.DynamicValue > 0 ? p.DynamicValue : p.Avg) - weightAtPos)
                })
                .OrderBy(x => x.Dist)
                .FirstOrDefault();

            if (closest != null && closest.Dist < 400.0) // Within screen proximity
            {
                var pt = closest.Point;
                string typeName = pt.Type == 1 || pt.Type == 2 ? "Pick up" :
                                  pt.Type == 3 || pt.Type == 4 ? "Slack off" : "Rotate";
                string colorHex = pt.Type == 1 || pt.Type == 2 ? "#E91E63" :
                                  pt.Type == 3 || pt.Type == 4 ? "#2979FF" : "#FF9800";

                return new BroomstickHoverInfo
                {
                    Depth = pt.Depth,
                    Weight = pt.DynamicValue > 0 ? pt.DynamicValue : pt.Avg,
                    PointType = typeName,
                    RigStateName = !string.IsNullOrWhiteSpace(pt.RigStates) ? pt.RigStates : "Rotary Drill",
                    DateTimeText = pt.StartDate != DateTime.MinValue ? pt.StartDate.ToString("dd-MMM-yyyy HH:mm:ss") : "",
                    ColorHex = colorHex
                };
            }
        }
        catch
        {
        }

        return null;
    }
}
