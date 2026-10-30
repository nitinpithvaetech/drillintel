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

        // Clean white theme matching application dialogs
        _tChart.Panel.Brush.Color = Color.White;
        _tChart.Panel.MarginUnits = PanelMarginUnits.Pixels;
        _tChart.Panel.MarginTop = 16;
        _tChart.Panel.MarginBottom = 20;
        _tChart.Panel.MarginLeft = 24;
        _tChart.Panel.MarginRight = 16;

        // Inverted Depth Left Axis
        _tChart.Axes.Left.Inverted = true;
        _tChart.Axes.Left.AxisPen.Color = Color.FromArgb(180, 190, 203);
        _tChart.Axes.Left.Grid.Color = Color.FromArgb(229, 231, 235);
        _tChart.Axes.Left.Labels.Font.Color = Color.FromArgb(70, 80, 95);
        _tChart.Axes.Left.Labels.Font.Size = 9;
        _tChart.Axes.Left.Title.Text = "Depth (ft)";
        _tChart.Axes.Left.Title.Font.Color = Color.FromArgb(30, 41, 59);
        _tChart.Axes.Left.Title.Font.Bold = true;

        // Bottom Weight Axis
        _tChart.Axes.Bottom.AxisPen.Color = Color.FromArgb(180, 190, 203);
        _tChart.Axes.Bottom.Grid.Color = Color.FromArgb(229, 231, 235);
        _tChart.Axes.Bottom.Labels.Font.Color = Color.FromArgb(70, 80, 95);
        _tChart.Axes.Bottom.Labels.Font.Size = 9;
        _tChart.Axes.Bottom.Title.Text = "Weight (klbs)";
        _tChart.Axes.Bottom.Title.Font.Color = Color.FromArgb(30, 41, 59);
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

        // 3. Render Actual Scatter Points (Hookload only)
        RenderScatterPoints(document, filteredPoints);

        // 4. Configure optimal Bottom Axis (Hookload Weight klbs)
        ConfigureBottomWeightAxis(document, filteredPoints);

        _tChart.Invalidate();
    }

    private void RenderTheoreticalCurves(BroomstickDocument doc)
    {
        var vis = doc.PlotSettings.SeriesVisibility;
        string planName = !string.IsNullOrWhiteSpace(doc.BroomstickPlanData?.Name) ? doc.BroomstickPlanData.Name : "8.5\" Hookload Drilling";
        if (!string.IsNullOrWhiteSpace(doc.PlanId))
        {
            planName = doc.PlanId;
        }
        planName = AdnlHookloadPlan.ExtractMainPlanKey(planName, "");

        // 1. If multiple associated child plans exist for this Main Plan, render their authentic curves
        if (doc.AssociatedPlans != null && doc.AssociatedPlans.Count >= 2)
        {
            RenderAssociatedChildPlans(doc, doc.AssociatedPlans, vis, planName);
            return;
        }

        // 2. Single plan or fallback: Render synthesized curves smoothly
        RenderSynthesizedCurves(doc, vis, planName);
    }

    private void RenderAssociatedChildPlans(
        BroomstickDocument doc,
        List<AdnlHookloadPlan> childPlans,
        Dictionary<string, bool> vis,
        string basePlanName)
    {
        // Sort child plans strictly by friction factor
        var sortedPlans = childPlans.OrderBy(p => AdnlHookloadPlan.ExtractFrictionFactor(p)).ToList();

        // High-contrast palette for light theme matching engineering standards:
        // Pick-up: Vivid Cyan to Deep Navy (#0097A7, #0288D1, #1976D2, #1565C0, #0D47A1)
        var puColors = new[]
        {
            Color.FromArgb(0, 151, 167),    // 0.2 OHFF: #0097A7
            Color.FromArgb(2, 136, 209),    // 0.25 OHFF: #0288D1
            Color.FromArgb(25, 118, 210),   // 0.3 OHFF: #1976D2
            Color.FromArgb(21, 101, 192),   // 0.35 OHFF: #1565C0
            Color.FromArgb(13, 71, 161)     // 0.4 OHFF: #0D47A1
        };

        // Slack-off: Crisp Emerald and Forest Green shades (#4CAF50, #43A047, #388E3C, #2E7D32, #1B5E20)
        var soColors = new[]
        {
            Color.FromArgb(76, 175, 80),    // 0.2 OHFF: #4CAF50
            Color.FromArgb(67, 160, 71),    // 0.25 OHFF: #43A047
            Color.FromArgb(56, 142, 60),    // 0.3 OHFF: #388E3C
            Color.FromArgb(46, 125, 50),    // 0.35 OHFF: #2E7D32
            Color.FromArgb(27, 94, 32)      // 0.4 OHFF: #1B5E20
        };

        var visKeys = new[] { "plan02", "plan025", "plan03", "plan035", "plan04" };
        var soVisKeys = new[] { "so02", "so025", "so03", "so035", "so04" };

        bool rotRendered = false;
        bool casingPuRendered = false;
        bool casingSoRendered = false;

        for (int i = 0; i < sortedPlans.Count; i++)
        {
            var plan = sortedPlans[i];
            string pKey = i < visKeys.Length ? visKeys[i] : $"plan_extra_{i}";
            string soKey = i < soVisKeys.Length ? soVisKeys[i] : $"so_extra_{i}";
            var puColor = i < puColors.Length ? puColors[i] : Color.FromArgb(0, 229, 255);
            var soColor = i < soColors.Length ? soColors[i] : Color.FromArgb(0, 230, 118);
            int width = (i == 2 || i == sortedPlans.Count - 1) ? 2 : 1;

            string ffLabel = !string.IsNullOrWhiteSpace(plan.RunNo) && System.Text.RegularExpressions.Regex.IsMatch(plan.RunNo, @"0\.\d+")
                ? plan.RunNo.Trim()
                : (!string.IsNullOrWhiteSpace(plan.Name) && System.Text.RegularExpressions.Regex.IsMatch(plan.Name, @"0\.\d+") ? plan.Name.Trim() : $"{(0.2 + i * 0.05):0.00} OHFF");
            string curveTitle = $"{basePlanName} {ffLabel}".Trim();

            // 1. Pick-up Curve (Strictly ordered by Depth, distinct per child plan)
            if (plan.pickup?.Count > 0 && IsSeriesVisible(vis, pKey))
            {
                var s = CreateLineSeries(pKey, curveTitle, puColor, width);
                var orderedPu = plan.pickup.Values
                    .Where(p => p.Depth > 0 && p.Weight > 0)
                    .GroupBy(p => Math.Round(p.Depth, 2))
                    .Select(g => g.First())
                    .OrderBy(p => p.Depth);
                foreach (var pt in orderedPu)
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }

            // 2. Casing Pick-up (Lavender: #B3B8FF)
            if (!casingPuRendered && IsSeriesVisible(vis, "casingPu"))
            {
                var casingPuPoints = plan.pickup?.Values
                    .Where(p => p.Depth > 0 && p.MaxTension > 0)
                    .GroupBy(p => Math.Round(p.Depth, 2))
                    .Select(g => g.First())
                    .OrderBy(p => p.Depth)
                    .ToList();
                if (casingPuPoints != null && casingPuPoints.Count > 0)
                {
                    var s = CreateLineSeries("casingPu", "Casing - Pick up", Color.FromArgb(123, 31, 162), 2);
                    foreach (var pt in casingPuPoints)
                    {
                        s.Add(pt.MaxTension, pt.Depth);
                    }
                    casingPuRendered = true;
                }
            }

            // 3. Rotate Curve (Solid White: #FFFFFF)
            if (!rotRendered && IsSeriesVisible(vis, "rotTheory") && plan.rotate?.Count > 0)
            {
                var rotPoints = plan.rotate.Values
                    .Where(p => p.Depth > 0 && p.Weight > 0)
                    .GroupBy(p => Math.Round(p.Depth, 2))
                    .Select(g => g.First())
                    .OrderBy(p => p.Depth)
                    .ToList();
                if (rotPoints.Count > 0)
                {
                    var s = CreateLineSeries("rotTheory", $"{basePlanName} 0.3 OHFF", Color.FromArgb(41, 37, 36), 2);
                    foreach (var pt in rotPoints)
                    {
                        s.Add(pt.Weight, pt.Depth);
                    }
                    rotRendered = true;
                }
            }

            // 4. Casing Slack-off (Cyan / Turquoise: #4DD0E1)
            if (!casingSoRendered && IsSeriesVisible(vis, "casingSo"))
            {
                var casingSoPoints = plan.slackoff?.Values
                    .Where(p => p.Depth > 0 && p.MaxCompress > 0)
                    .GroupBy(p => Math.Round(p.Depth, 2))
                    .Select(g => g.First())
                    .OrderBy(p => p.Depth)
                    .ToList();
                if (casingSoPoints == null || casingSoPoints.Count == 0)
                {
                    casingSoPoints = plan.pickup?.Values
                        .Where(p => p.Depth > 0 && p.MaxCompress > 0)
                        .GroupBy(p => Math.Round(p.Depth, 2))
                        .Select(g => g.First())
                        .OrderBy(p => p.Depth)
                        .ToList();
                }
                if (casingSoPoints != null && casingSoPoints.Count > 0)
                {
                    var s = CreateLineSeries("casingSo", "Casing - Slack off", Color.FromArgb(216, 67, 21), 2);
                    foreach (var pt in casingSoPoints)
                    {
                        s.Add(pt.MaxCompress, pt.Depth);
                    }
                    casingSoRendered = true;
                }
            }

            // 5. Slack-off Curve (Strictly ordered by Depth, distinct per child plan, perfectly smooth)
            if (plan.slackoff?.Count > 0 && IsSeriesVisible(vis, soKey))
            {
                var s = CreateLineSeries(soKey, curveTitle, soColor, width);
                var orderedSo = plan.slackoff.Values
                    .Where(p => p.Depth > 0 && p.Weight > 0)
                    .GroupBy(p => Math.Round(p.Depth, 2))
                    .Select(g => g.First())
                    .OrderBy(p => p.Depth);
                foreach (var pt in orderedSo)
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }
        }
    }

    private void RenderSynthesizedCurves(BroomstickDocument doc, Dictionary<string, bool> vis, string planName)
    {
        var plan = doc.BroomstickPlanData;
        if (plan == null) return;

        // Clean, monotonically sorted points for each curve type
        var puPoints = plan.pickup?.Values
            .Where(p => p.Depth > 0 && p.Weight > 0)
            .GroupBy(p => Math.Round(p.Depth, 2))
            .Select(g => g.First())
            .OrderBy(p => p.Depth)
            .ToList() ?? new List<HookloadPlanData>();

        var soPoints = plan.slackoff?.Values
            .Where(p => p.Depth > 0 && p.Weight > 0)
            .GroupBy(p => Math.Round(p.Depth, 2))
            .Select(g => g.First())
            .OrderBy(p => p.Depth)
            .ToList() ?? new List<HookloadPlanData>();

        var rotPoints = plan.rotate?.Values
            .Where(p => p.Depth > 0 && p.Weight > 0)
            .GroupBy(p => Math.Round(p.Depth, 2))
            .Select(g => g.First())
            .OrderBy(p => p.Depth)
            .ToList() ?? new List<HookloadPlanData>();

        // 1. Pick-up theoretical curves: 0.2, 0.25, 0.3, 0.35, 0.4 OHFF
        var factors = new (string key, string label, double factor, Color color, int width)[]
        {
            ("plan02",  $"{planName} 0.2 OHFF",  0.20, Color.FromArgb(0, 151, 167), 1),
            ("plan025", $"{planName} 0.25 OHFF", 0.25, Color.FromArgb(2, 136, 209), 1),
            ("plan03",  $"{planName} 0.3 OHFF",  0.30, Color.FromArgb(25, 118, 210), 2),
            ("plan035", $"{planName} 0.35 OHFF", 0.35, Color.FromArgb(21, 101, 192), 1),
            ("plan04",  $"{planName} 0.4 OHFF",  0.40, Color.FromArgb(13, 71, 161),  1),
        };

        if (puPoints.Count > 0)
        {
            foreach (var (key, label, factor, color, width) in factors)
            {
                if (IsSeriesVisible(vis, key))
                {
                    var s = CreateLineSeries(key, label, color, width);
                    foreach (var pu in puPoints)
                    {
                        double scale = 1.0 + ((factor - 0.30) / 0.30) * 0.20;
                        double calcWeight = factor == 0.30 ? pu.Weight : pu.Weight * scale;
                        s.Add(Math.Round(calcWeight, 2), pu.Depth);
                    }
                }
            }
        }

        // 2. Casing - Pick up (Lavender: #B3B8FF)
        if (IsSeriesVisible(vis, "casingPu"))
        {
            var casingPuPoints = puPoints.Where(p => p.MaxTension > 0).OrderBy(p => p.Depth).ToList();
            if (casingPuPoints.Count > 0)
            {
                var s = CreateLineSeries("casingPu", "Casing - Pick up", Color.FromArgb(123, 31, 162), 2);
                foreach (var pt in casingPuPoints)
                {
                    s.Add(pt.MaxTension, pt.Depth);
                }
            }
        }

        // 3. Theoretical Rotate (Solid White: #FFFFFF)
        if (IsSeriesVisible(vis, "rotTheory"))
        {
            if (rotPoints.Count > 0)
            {
                var s = CreateLineSeries("rotTheory", $"{planName} 0.3 OHFF", Color.FromArgb(41, 37, 36), 2);
                foreach (var pt in rotPoints)
                {
                    s.Add(pt.Weight, pt.Depth);
                }
            }
            else if (puPoints.Count > 0)
            {
                var s = CreateLineSeries("rotTheory", $"{planName} 0.3 OHFF", Color.FromArgb(41, 37, 36), 2);
                foreach (var pu in puPoints)
                {
                    s.Add(Math.Round(pu.Weight * 0.85, 2), pu.Depth);
                }
            }
        }

        // 4. Casing - Slack off (Cyan: #4DD0E1)
        if (IsSeriesVisible(vis, "casingSo"))
        {
            var casingSoPoints = soPoints.Where(p => p.MaxCompress > 0).OrderBy(p => p.Depth).ToList();
            if (casingSoPoints.Count == 0 && puPoints.Count > 0)
            {
                casingSoPoints = puPoints.Where(p => p.MaxCompress > 0).OrderBy(p => p.Depth).ToList();
            }
            if (casingSoPoints.Count > 0)
            {
                var s = CreateLineSeries("casingSo", "Casing - Slack off", Color.FromArgb(216, 67, 21), 2);
                foreach (var pt in casingSoPoints)
                {
                    s.Add(pt.MaxCompress, pt.Depth);
                }
            }
        }

        // 5. Slack off curves: 0.20, 0.25, 0.30, 0.35, 0.40 (Emerald & forest green)
        var soFactors = new (string key, string label, double factor, Color color, int width)[]
        {
            ("so02",  $"{planName} 0.2 OHFF",  0.20, Color.FromArgb(76, 175, 80), 1),
            ("so025", $"{planName} 0.25 OHFF", 0.25, Color.FromArgb(67, 160, 71), 1),
            ("so03",  $"{planName} 0.3 OHFF",  0.30, Color.FromArgb(56, 142, 60), 2),
            ("so035", $"{planName} 0.35 OHFF", 0.35, Color.FromArgb(46, 125, 50), 1),
            ("so04",  $"{planName} 0.4 OHFF",  0.40, Color.FromArgb(27, 94, 32), 1),
        };

        if (soPoints.Count > 0)
        {
            foreach (var (key, label, factor, color, width) in soFactors)
            {
                if (IsSeriesVisible(vis, key))
                {
                    var s = CreateLineSeries(key, label, color, width);
                    foreach (var pt in soPoints)
                    {
                        double scale = 1.0 - ((factor - 0.30) / 0.30) * 0.20;
                        double calcWeight = factor == 0.30 ? pt.Weight : pt.Weight * scale;
                        s.Add(Math.Round(Math.Max(0, calcWeight), 2), pt.Depth);
                    }
                }
            }
        }
        else if (puPoints.Count > 0)
        {
            foreach (var (key, label, factor, color, width) in soFactors)
            {
                if (IsSeriesVisible(vis, key))
                {
                    var s = CreateLineSeries(key, label, color, width);
                    foreach (var pt in puPoints)
                    {
                        double scale = 0.70 - ((factor - 0.30) / 0.30) * 0.15;
                        double calcWeight = pt.Weight * scale;
                        s.Add(Math.Round(Math.Max(0, calcWeight), 2), pt.Depth);
                    }
                }
            }
        }
    }

    public static double ResolvePointPlotValue(PUSOPoint pt)
    {
        if (pt == null) return 0.0;

        // If torque point (Type 3 = ROB Torque, Type 4 = OnBottom Torque), return torque value
        if (pt.Type == 3 || pt.Type == 4)
        {
            if (pt.DynamicTorqueValue > 0) return pt.DynamicTorqueValue;
            if (pt.DynamicValue > 0) return pt.DynamicValue;
            if (pt.Avg > 0) return pt.Avg;
            return 0.0;
        }

        // Strictly use Hookload values for Hookload Broomstick chart
        double val = 0.0;
        if (pt.DynamicValue > 0) val = pt.DynamicValue;
        else if (pt.Avg > 0) val = pt.Avg;
        else if (pt.UserDynamicValue > 0) val = pt.UserDynamicValue;
        else if (pt.UserStaticValue > 0) val = pt.UserStaticValue;
        else if (pt.Avg2 > 0) val = pt.Avg2;
        else if (pt.Max > 0) val = pt.Max;
        else if (pt.Min > 0) val = pt.Min;

        // If raw pounds (e.g. > 2000), convert to klbs
        if (val > 2000.0) val /= 1000.0;
        return val;
    }

    private void RenderScatterPoints(BroomstickDocument doc, List<PUSOPoint> points)
    {
        if (points == null || points.Count == 0) return;

        var vis = doc.PlotSettings.SeriesVisibility;

        bool hasType5 = points.Any(p => p.Type == 5);
        bool hasType0 = points.Any(p => p.Type == 0);

        IEnumerable<PUSOPoint> puPoints;
        IEnumerable<PUSOPoint> soPoints;
        IEnumerable<PUSOPoint> rotPoints;

        if (hasType5 && !hasType0)
        {
            // Legacy 1-based (1,2 = PU, 3,4 = SO, 5 = Rotate)
            puPoints = points.Where(p => p.Type == 1 || p.Type == 2);
            soPoints = points.Where(p => p.Type == 3 || p.Type == 4);
            rotPoints = points.Where(p => p.Type == 5);
        }
        else
        {
            // Standard DrillIntel 0-based:
            // 0 = PU (Hookload klbs)
            // 1 = SO (Hookload klbs)
            // 2 = Rotate (Hookload klbs)
            // Note: Type 3 (Torque ROB) and Type 4 (OnBottom Torque) belong to Torque plots, NEVER Hookload!
            puPoints = points.Where(p => p.Type == 0);
            soPoints = points.Where(p => p.Type == 1);
            rotPoints = points.Where(p => p.Type == 2);
        }

        int ptSize = doc.PlotSettings.PointSize > 0 ? doc.PlotSettings.PointSize : 6;
        var ptStyle = ResolvePointerStyle(doc.PlotSettings.PointsStyle);

        // Pick-Up Points (PU) - Crimson / Rose
        if (IsSeriesVisible(vis, "actualPu"))
        {
            var puSeries = CreatePointsSeries("actualPu", "Pick up", Color.FromArgb(225, 29, 72), ptSize, ptStyle);
            foreach (var pt in puPoints)
            {
                double val = ResolvePointPlotValue(pt);
                if (val > 0) puSeries.Add(val, pt.Depth);
            }
        }

        // Slack-Off Points (SO) - Royal Blue
        if (IsSeriesVisible(vis, "actualSo"))
        {
            var soSeries = CreatePointsSeries("actualSo", "Slack Off", Color.FromArgb(37, 99, 235), ptSize, ptStyle);
            foreach (var pt in soPoints)
            {
                double val = ResolvePointPlotValue(pt);
                if (val > 0) soSeries.Add(val, pt.Depth);
            }
        }

        // Rotate Points (ROT) - Amber / Orange
        if (IsSeriesVisible(vis, "actualRot"))
        {
            var rotSeries = CreatePointsSeries("actualRot", "Rotate", Color.FromArgb(217, 119, 6), Math.Max(3, ptSize - 1), ptStyle);
            foreach (var pt in rotPoints)
            {
                double val = ResolvePointPlotValue(pt);
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
        s.XValues.Order = ValueListOrder.None;
        s.YValues.Order = ValueListOrder.None;
        _seriesMap[id] = s;
        return s;
    }

    private static PointerStyles ResolvePointerStyle(string? style) => (style?.Trim().ToLowerInvariant()) switch
    {
        "circle" => PointerStyles.Circle,
        "triangle" => PointerStyles.Triangle,
        "diamond" => PointerStyles.Diamond,
        _ => PointerStyles.Rectangle
    };

    private Points CreatePointsSeries(string id, string title, Color color, int size, PointerStyles pointerStyle = PointerStyles.Rectangle)
    {
        var s = new Points(_tChart.Chart)
        {
            Title = title,
            Color = color
        };
        s.Pointer.Visible = true;
        s.Pointer.Style = pointerStyle;
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

    private void ConfigureBottomWeightAxis(BroomstickDocument doc, List<PUSOPoint> points)
    {
        double maxWeight = 0;

        // 1. Scan plan points
        if (doc.AssociatedPlans != null && doc.AssociatedPlans.Count > 0)
        {
            foreach (var plan in doc.AssociatedPlans)
            {
                if (plan.pickup?.Count > 0)
                    maxWeight = Math.Max(maxWeight, plan.pickup.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
                if (plan.slackoff?.Count > 0)
                    maxWeight = Math.Max(maxWeight, plan.slackoff.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
                if (plan.rotate?.Count > 0)
                    maxWeight = Math.Max(maxWeight, plan.rotate.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
            }
        }
        else if (doc.BroomstickPlanData != null)
        {
            var plan = doc.BroomstickPlanData;
            if (plan.pickup?.Count > 0)
                maxWeight = Math.Max(maxWeight, plan.pickup.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
            if (plan.slackoff?.Count > 0)
                maxWeight = Math.Max(maxWeight, plan.slackoff.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
            if (plan.rotate?.Count > 0)
                maxWeight = Math.Max(maxWeight, plan.rotate.Values.Where(p => p.Weight > 0).Select(p => p.Weight).DefaultIfEmpty(0).Max());
        }

        // 2. Scan scatter hookload points (Hookload Type 0, 1, 2, or legacy 5)
        if (points != null && points.Count > 0)
        {
            bool hasType0 = points.Any(p => p.Type == 0);
            var hookloadPts = hasType0 
                ? points.Where(p => p.Type == 0 || p.Type == 1 || p.Type == 2)
                : points.Where(p => p.Type == 1 || p.Type == 2 || p.Type == 3 || p.Type == 4 || p.Type == 5);

            foreach (var pt in hookloadPts)
            {
                double val = ResolvePointPlotValue(pt);
                if (val > 0 && val < 2000.0)
                {
                    maxWeight = Math.Max(maxWeight, val);
                }
            }
        }

        // 3. Round to standard 50 klbs increments
        if (maxWeight <= 0)
        {
            maxWeight = 350.0;
        }

        double axisMax = Math.Ceiling((maxWeight * 1.10) / 50.0) * 50.0;
        if (axisMax < 250.0) axisMax = 250.0;
        if (axisMax > 600.0) axisMax = 600.0;

        _tChart.Axes.Bottom.Automatic = false;
        _tChart.Axes.Bottom.SetMinMax(0, axisMax);
        _tChart.Axes.Bottom.Increment = axisMax <= 200 ? 20 : (axisMax <= 400 ? 50 : 100);
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

            // Find closest scatter hookload point within tolerance (exclude torque points)
            bool isZeroBased = points.Any(p => p.Type == 0);
            var hookloadPts = isZeroBased
                ? points.Where(p => p.Type == 0 || p.Type == 1 || p.Type == 2)
                : points.Where(p => p.Type == 1 || p.Type == 2 || p.Type == 3 || p.Type == 4 || p.Type == 5);

            var closest = hookloadPts
                .Select(p => new
                {
                    Point = p,
                    Dist = Math.Abs(p.Depth - depthAtPos) + Math.Abs(ResolvePointPlotValue(p) - weightAtPos)
                })
                .OrderBy(x => x.Dist)
                .FirstOrDefault();

            if (closest != null && closest.Dist < 400.0) // Within screen proximity
            {
                var pt = closest.Point;
                string typeName = isZeroBased
                    ? (pt.Type == 0 ? "Pick up" : pt.Type == 1 ? "Slack off" : "Rotate")
                    : (pt.Type == 1 || pt.Type == 2 ? "Pick up" : pt.Type == 3 || pt.Type == 4 ? "Slack off" : "Rotate");
                string colorHex = isZeroBased
                    ? (pt.Type == 0 ? "#E11D48" : pt.Type == 1 ? "#2563EB" : "#D97706")
                    : (pt.Type == 1 || pt.Type == 2 ? "#E11D48" : pt.Type == 3 || pt.Type == 4 ? "#2563EB" : "#D97706");

                return new BroomstickHoverInfo
                {
                    Depth = pt.Depth,
                    Weight = ResolvePointPlotValue(pt),
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
