using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models.TChart;
using Steema.TeeChart.Styles;
using Steema.TeeChart.WPF;
using SteemaAxis = Steema.TeeChart.Axis;

namespace DrillIntel.Services.Charting;

/// <summary>
/// Renders and synchronizes a VHTrackConsole domain model onto a Steema TeeChart WPF canvas.
/// Configures track columns, custom horizontal/vertical axes, series types, fonts, and rig-state color coding.
/// </summary>
public class TChartTrackConsoleRenderer
{
    private readonly TChart _tChart;
    private VHTrackConsole? _currentConsole;
    private ChartDataSeriesResult? _currentData;
    private List<RigStateInterval>? _currentRigStateIntervals;

    public VHTrackConsole? CurrentConsole => _currentConsole;
    public ChartDataSeriesResult? CurrentData => _currentData;
    public List<RigStateInterval>? CurrentRigStateIntervals => _currentRigStateIntervals;

    public double LeftAxisOffset
    {
        get
        {
            if (_currentConsole?.TrackOrientation == enumTrackOrientation.Horizontal)
                return 0;
            int left = _tChart.Chart.ChartRect.Left;
            return left > 0 ? left : 70;
        }
    }

    public double RightAxisOffset
    {
        get
        {
            if (_currentConsole?.TrackOrientation == enumTrackOrientation.Horizontal)
                return 0;
            int rightMargin = (int)_tChart.ActualWidth - _tChart.Chart.ChartRect.Right;
            return rightMargin > 0 ? rightMargin : 12;
        }
    }

    public TChartTrackConsoleRenderer(TChart tChart)
    {
        _tChart = tChart ?? throw new ArgumentNullException(nameof(tChart));
        InitializeChartSettings();
    }

    private void InitializeChartSettings()
    {
        _tChart.Aspect.View3D = false;
        _tChart.Legend.Visible = false;
        _tChart.Header.Visible = false;
        _tChart.Panel.Brush.Color = Color.White;
        _tChart.Panel.MarginUnits = Steema.TeeChart.PanelMarginUnits.Pixels;
        _tChart.Panel.MarginTop = 18;
        _tChart.Panel.MarginBottom = 22;
        _tChart.Panel.MarginLeft = 16;
        _tChart.Panel.MarginRight = 12;
        _tChart.Zoom.Direction = Steema.TeeChart.ZoomDirections.Vertical;

        _tChart.UndoneZoom += (s, e) =>
        {
            ResetInternalZoomState();
        };
    }

    private void ResetInternalZoomState()
    {
        try
        {
            var field = typeof(Steema.TeeChart.Chart).GetField("_restoredAxisScales", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(_tChart.Chart, true);
        }
        catch { }
    }

    private void SyncInternalZoomState()
    {
        try
        {
            _tChart.Chart.SaveScales();
            var field = typeof(Steema.TeeChart.Chart).GetField("_restoredAxisScales", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(_tChart.Chart, true);
        }
        catch { }
    }

    /// <summary>
    /// Builds and renders the complete track console, custom axes, series, and data points onto the TeeChart.
    /// Supports both Vertical and Horizontal track orientations.
    /// </summary>
    public void RenderConsole(VHTrackConsole console, ChartDataSeriesResult? data, List<RigStateInterval>? rigStateIntervals = null)
    {
        if (console == null) return;

        _currentConsole = console;
        _currentData = data;
        _currentRigStateIntervals = rigStateIntervals;

        _tChart.AutoRepaint = false;
        try
        {
            ResetInternalZoomState();

            _tChart.Series.Clear();
            _tChart.Axes.Custom.Clear();

            var visibleTracks = console.Tracks.Where(t => t.Visible).OrderBy(t => t.DisplayOrder).ToList();
            if (visibleTracks.Count == 0)
            {
                _tChart.AutoRepaint = true;
                _tChart.Invalidate();
                return;
            }

            bool isHorizontal = console.TrackOrientation == enumTrackOrientation.Horizontal;
            _tChart.Zoom.Direction = isHorizontal ? Steema.TeeChart.ZoomDirections.Horizontal : Steema.TeeChart.ZoomDirections.Vertical;

            // Calculate max staggered axes per side to ensure sufficient margins
            int maxTopAxes = 0;
            int maxBottomAxes = 0;
            int maxRightAxes = 0;
            int maxLeftAxes = 0;

            foreach (var t in visibleTracks)
            {
                int topCount = 0, bottomCount = 0, rightCount = 0, leftCount = 0;
                var chList = t.Channels.Where(c => c.Visible).ToList();
                for (int cIdx = 0; cIdx < chList.Count; cIdx++)
                {
                    var ch = chList[cIdx];
                    bool otherSide;
                    if (ch.objXAxis.Location == enumRTAxisLocation.Top || ch.objXAxis.Location == enumRTAxisLocation.Right)
                        otherSide = true;
                    else if (ch.objXAxis.Location == enumRTAxisLocation.Bottom || ch.objXAxis.Location == enumRTAxisLocation.Left)
                        otherSide = false;
                    else
                        otherSide = (cIdx % 2 != 0);

                    if (isHorizontal)
                    {
                        if (otherSide) rightCount++; else leftCount++;
                    }
                    else
                    {
                        if (otherSide) topCount++; else bottomCount++;
                    }
                }
                maxTopAxes = Math.Max(maxTopAxes, topCount);
                maxBottomAxes = Math.Max(maxBottomAxes, bottomCount);
                maxRightAxes = Math.Max(maxRightAxes, rightCount);
                maxLeftAxes = Math.Max(maxLeftAxes, leftCount);
            }

            _tChart.Panel.MarginUnits = Steema.TeeChart.PanelMarginUnits.Pixels;

            if (isHorizontal)
            {
                _tChart.Panel.MarginBottom = 48; // Multi-line DateTime labels + Axis Title
                _tChart.Panel.MarginTop = 18;
                _tChart.Panel.MarginLeft = Math.Max(26, maxLeftAxes * 32 + 20);
                _tChart.Panel.MarginRight = Math.Max(22, maxRightAxes * 34 + 18);
            }
            else
            {
                _tChart.Panel.MarginBottom = Math.Max(26, maxBottomAxes * 32 + 20);
                _tChart.Panel.MarginTop = Math.Max(32, maxTopAxes * 34 + 22);
                _tChart.Panel.MarginLeft = 24;
                _tChart.Panel.MarginRight = 14;
            }

            // Configure Legend
            ConfigureLegend(console);

            double totalWeight = visibleTracks.Sum(t => Math.Max(0.1, t.Width));

            // Configure Shared Index Axis (Bottom for horizontal orientation, Left for vertical orientation)
            ConfigureIndexAxis(console, data, isHorizontal);

            double accumulatedWeight = 0.0;

            for (int i = 0; i < visibleTracks.Count; i++)
            {
                var track = visibleTracks[i];
                double trackWeight = Math.Max(0.1, track.Width);
                double startPercent = (accumulatedWeight / totalWeight) * 100.0;
                double endPercent = ((accumulatedWeight + trackWeight) / totalWeight) * 100.0;
                accumulatedWeight += trackWeight;

                // Configure Channels and Custom Axes within this Track
                RenderTrack(console, track, startPercent, endPercent, data, isHorizontal);
            }

            // Sync scales with TeeChart zoom engine to prevent RestoreAxisScales null exceptions on unzoom
            SyncInternalZoomState();

            // Draw Rig State color bands if intervals exist
            if (rigStateIntervals != null && rigStateIntervals.Count > 0)
            {
                ApplyRigStateBands(console, visibleTracks, totalWeight, rigStateIntervals);
            }
        }
        finally
        {
            _tChart.AutoRepaint = true;
            _tChart.Invalidate();
            _tChart.InvalidateVisual();
        }
    }

    private void ConfigureLegend(VHTrackConsole console)
    {
        _tChart.Legend.Visible = console.ShowLegend;
        _tChart.Legend.Alignment = Steema.TeeChart.LegendAlignments.Right;
        _tChart.Legend.LegendStyle = Steema.TeeChart.LegendStyles.Series;

        string title = !string.IsNullOrWhiteSpace(console.Name) ? console.Name : "Channels";
        _tChart.Legend.Title.Visible = true;
        _tChart.Legend.Title.Text = title;
        _tChart.Legend.Title.Font.Name = "Segoe UI";
        _tChart.Legend.Title.Font.Size = 9;
        _tChart.Legend.Title.Font.Bold = true;
        _tChart.Legend.Title.Font.Color = Color.Black;

        _tChart.Legend.Font.Name = "Segoe UI";
        _tChart.Legend.Font.Size = 8;
        _tChart.Legend.Font.Color = Color.FromArgb(51, 65, 85);

        _tChart.Legend.Pen.Visible = false;
        _tChart.Legend.Transparent = false;
        _tChart.Legend.Brush.Color = Color.White;
        _tChart.Legend.Shadow.Visible = false;

        _tChart.Legend.Symbol.Width = 14;
        _tChart.Legend.ResizeChart = true;
    }

    private void ConfigureIndexAxis(VHTrackConsole console, ChartDataSeriesResult? data, bool isHorizontal)
    {
        var activeAxis = isHorizontal ? _tChart.Axes.Bottom : _tChart.Axes.Left;
        var inactiveAxis = isHorizontal ? _tChart.Axes.Left : _tChart.Axes.Bottom;

        inactiveAxis.Visible = false;
        activeAxis.Visible = true;
        activeAxis.Labels.Font.Name = "Segoe UI";
        activeAxis.Labels.Font.Size = 8;
        activeAxis.Labels.Font.Color = Color.DarkSlateGray;
        activeAxis.Labels.Separation = 40;

        // Axis Title on Index Axis
        activeAxis.Title.Visible = console.ShowAxisTitles;
        activeAxis.Title.Font.Name = "Segoe UI";
        activeAxis.Title.Font.Size = 8;
        activeAxis.Title.Font.Bold = true;
        activeAxis.Title.Font.Color = Color.DarkSlateGray;

        if (console.IndexType == enumIndexType.TimeLog)
        {
            activeAxis.Inverted = !isHorizontal; // Downwards for vertical log, left-to-right for horizontal

            if (isHorizontal)
            {
                activeAxis.Labels.MultiLine = true;
                activeAxis.Labels.DateTimeFormat = "HH:mm:ss\ndd-MMM-yy";
                activeAxis.Title.Text = "DateTime (DateTime)";
                activeAxis.Title.Angle = 0;
            }
            else
            {
                activeAxis.Labels.DateTimeFormat = "dd-MMM HH:mm";
                activeAxis.Title.Text = "Time";
                activeAxis.Title.Angle = 90;
            }

            double minOa = 0;
            double maxOa = 0;

            if (data != null && data.IndexValues.Count > 0)
            {
                minOa = data.IndexValues.Min();
                maxOa = data.IndexValues.Max();
            }
            else if (console.currentMinDate != DateTime.UnixEpoch &&
                     console.currentMaxDate != DateTime.UnixEpoch &&
                     console.currentMaxDate > console.currentMinDate)
            {
                minOa = console.currentMinDate.ToOADate();
                maxOa = console.currentMaxDate.ToOADate();
            }

            if (maxOa > minOa)
            {
                activeAxis.Automatic = false;
                activeAxis.SetMinMax(minOa, maxOa);
            }
            else
            {
                activeAxis.Automatic = true;
            }
        }
        else // DepthLog
        {
            activeAxis.Labels.ValueFormat = "0.##";
            activeAxis.Inverted = !isHorizontal; // Downwards for vertical log, left-to-right for horizontal

            string depthUnit = !string.IsNullOrWhiteSpace(console.DepthUnit) ? console.DepthUnit : "m";
            activeAxis.Title.Text = $"Depth ({depthUnit})";
            activeAxis.Title.Angle = isHorizontal ? 0 : 90;

            double minD = 0;
            double maxD = 0;

            if (data != null && data.IndexValues.Count > 0)
            {
                minD = data.IndexValues.Min();
                maxD = data.IndexValues.Max();
            }
            else if (console.currentMinDepth >= 0 && console.currentMaxDepth > console.currentMinDepth)
            {
                minD = console.currentMinDepth;
                maxD = console.currentMaxDepth;
            }

            if (maxD > minD)
            {
                activeAxis.Automatic = false;
                activeAxis.SetMinMax(minD, maxD);
            }
            else
            {
                activeAxis.Automatic = true;
            }
        }
    }

    private void RenderTrack(
        VHTrackConsole console,
        VHTrack track,
        double startPercent,
        double endPercent,
        ChartDataSeriesResult? data,
        bool isHorizontal)
    {
        var visibleChannels = track.Channels.Where(c => c.Visible).ToList();

        for (int chIdx = 0; chIdx < visibleChannels.Count; chIdx++)
        {
            var channel = visibleChannels[chIdx];

            // Look up channel values with mnemonic alias support
            List<double>? channelValues = null;
            if (data != null && data.IndexValues.Count > 0)
            {
                if (!data.ChannelValues.TryGetValue(channel.Mnemonic, out channelValues))
                {
                    var matchKey = data.ChannelValues.Keys.FirstOrDefault(k =>
                        string.Equals(k.Trim(), channel.Mnemonic.Trim(), StringComparison.OrdinalIgnoreCase) ||
                        ChartDataService.FindMatchingColumn(channel.Mnemonic, new[] { k }).Length > 0);
                    if (matchKey != null)
                    {
                        channelValues = data.ChannelValues[matchKey];
                    }
                }
            }

            // 1. Create and configure Custom Axis for this channel
            // In vertical orientation: Custom Horizontal axis spanning startPercent..endPercent of X
            // In horizontal orientation: Custom Vertical axis spanning startPercent..endPercent of Y
            bool placeOnOtherSide;
            if (isHorizontal)
            {
                if (channel.objXAxis.Location == enumRTAxisLocation.Right)
                    placeOnOtherSide = true;
                else if (channel.objXAxis.Location == enumRTAxisLocation.Left)
                    placeOnOtherSide = false;
                else
                    placeOnOtherSide = (chIdx % 2 != 0);
            }
            else
            {
                if (channel.objXAxis.Location == enumRTAxisLocation.Top)
                    placeOnOtherSide = true;
                else if (channel.objXAxis.Location == enumRTAxisLocation.Bottom)
                    placeOnOtherSide = false;
                else
                    placeOnOtherSide = (chIdx % 2 != 0);
            }

            var customAxis = new SteemaAxis(_tChart.Chart)
            {
                Horizontal = !isHorizontal,
                StartPosition = startPercent,
                EndPosition = endPercent,
                Inverted = channel.objXAxis.Inverted,
                Logarithmic = channel.objXAxis.Logarithmic,
                Visible = channel.objXAxis.Visible,
                OtherSide = placeOnOtherSide
            };

            // Count previous visible channels placed on the same side in this track to stagger them cleanly
            int sideIndex = 0;
            for (int prev = 0; prev < chIdx; prev++)
            {
                bool prevSide;
                var prevLoc = visibleChannels[prev].objXAxis.Location;
                if (isHorizontal)
                {
                    prevSide = prevLoc == enumRTAxisLocation.Right ? true :
                               prevLoc == enumRTAxisLocation.Left ? false :
                               (prev % 2 != 0);
                }
                else
                {
                    prevSide = prevLoc == enumRTAxisLocation.Top ? true :
                               prevLoc == enumRTAxisLocation.Bottom ? false :
                               (prev % 2 != 0);
                }

                if (prevSide == placeOnOtherSide)
                    sideIndex++;
            }

            if (sideIndex > 0)
            {
                customAxis.RelativePosition = sideIndex * 34;
            }

            // Match axis line and labels to the channel color
            Color axisColor = !string.IsNullOrWhiteSpace(channel.LineColor)
                ? ParseColor(channel.LineColor, Color.DarkSlateGray)
                : ParseColor(channel.objXAxis.LabelFontColor, Color.DarkSlateGray);

            customAxis.AxisPen.Color = axisColor;
            customAxis.Ticks.Color = axisColor;
            customAxis.MinorTicks.Color = Color.FromArgb(120, axisColor);

            // Font & Labels
            customAxis.Labels.Font.Name = !string.IsNullOrWhiteSpace(channel.objXAxis.LabelFontName) ? channel.objXAxis.LabelFontName : "Segoe UI";
            customAxis.Labels.Font.Size = (int)Math.Max(7, channel.objXAxis.LabelFontSize);
            customAxis.Labels.Font.Color = axisColor;
            customAxis.Labels.Font.Bold = channel.objXAxis.LabelFontBold;
            customAxis.Labels.Font.Italic = channel.objXAxis.LabelFontItalic;
            customAxis.Labels.Angle = (int)channel.objXAxis.LabelAngle;

            // Axis Title: Channel Title (Unit) matching channel color
            string axisTitle = !string.IsNullOrWhiteSpace(channel.Title) ? channel.Title : channel.Mnemonic;
            if (!string.IsNullOrWhiteSpace(channel.Unit))
            {
                axisTitle += $" ({channel.Unit})";
            }

            customAxis.Title.Text = axisTitle;
            customAxis.Title.Visible = console.ShowAxisTitles;
            customAxis.Title.Font.Name = !string.IsNullOrWhiteSpace(channel.objXAxis.LabelFontName) ? channel.objXAxis.LabelFontName : "Segoe UI";
            customAxis.Title.Font.Size = (int)Math.Max(7, channel.objXAxis.LabelFontSize);
            customAxis.Title.Font.Bold = true;
            customAxis.Title.Font.Color = axisColor;

            if (isHorizontal)
            {
                // Vertical axis along the left or right edge: rotate title vertically
                customAxis.Title.Angle = placeOnOtherSide ? 270 : 90;
            }
            else
            {
                // Horizontal axis along the top or bottom: title displayed horizontally
                customAxis.Title.Angle = 0;
            }

            // Enforce label separation and number formatting to prevent X-axis label overlap
            customAxis.Labels.Separation = 40;
            customAxis.Labels.ValueFormat = "#,##0.##";

            if (channel.Mnemonic.Equals("RIG_STATE", StringComparison.OrdinalIgnoreCase) ||
                (channel.ColorCodeAsRigState && channel.objXAxis.Max <= 30))
            {
                customAxis.Labels.Separation = 60;
            }

            // Grid Lines
            customAxis.Grid.Visible = channel.objXAxis.ShowMajorGrids;
            customAxis.Grid.Color = ParseColor(channel.objXAxis.MajorLineColor, Color.Gainsboro);
            customAxis.Grid.Width = (int)Math.Max(1, channel.objXAxis.MajorLineWidth);
            customAxis.Grid.Style = ConvertLineStyle(channel.objXAxis.MajorLineStyle);

            customAxis.MinorGrid.Visible = channel.objXAxis.ShowMinorGrids;
            customAxis.MinorGrid.Color = ParseColor(channel.objXAxis.MinorLineColor, Color.WhiteSmoke);

            // Min / Max Limits
            if (!channel.objXAxis.AutoScale && channel.objXAxis.Max > channel.objXAxis.Min)
            {
                customAxis.Automatic = false;
                customAxis.SetMinMax(channel.objXAxis.Min, channel.objXAxis.Max);
                channel.EffectiveMin = channel.objXAxis.Min;
                channel.EffectiveMax = channel.objXAxis.Max;
            }
            else if (channelValues != null && channelValues.Count > 0)
            {
                double cMin = channelValues.Min();
                double cMax = channelValues.Max();
                if (Math.Abs(cMax - cMin) < 1e-6)
                {
                    cMin -= 1.0;
                    cMax += 1.0;
                }
                customAxis.Automatic = false;
                customAxis.SetMinMax(cMin, cMax);
                channel.EffectiveMin = cMin;
                channel.EffectiveMax = cMax;
            }
            else
            {
                customAxis.Automatic = true;
                channel.EffectiveMin = channel.objXAxis.Min;
                channel.EffectiveMax = channel.objXAxis.Max;
            }

            _tChart.Axes.Custom.Add(customAxis);
            channel.__scale = customAxis;

            // 2. Create Series based on channel.SeriesType
            Series series = CreateSeriesForChannel(channel, console.IndexType == enumIndexType.TimeLog, isHorizontal);
            if (isHorizontal)
            {
                series.CustomHorizAxis = _tChart.Axes.Bottom;
                series.CustomVertAxis = customAxis;
            }
            else
            {
                series.CustomHorizAxis = customAxis;
                series.CustomVertAxis = _tChart.Axes.Left;
            }
            series.Title = !string.IsNullOrWhiteSpace(channel.Title) ? channel.Title : channel.Mnemonic;
            series.Color = axisColor;

            // 3. Populate Points from data
            if (channelValues != null && data != null && data.IndexValues.Count > 0)
            {
                int count = Math.Min(data.IndexValues.Count, channelValues.Count);
                bool colorByRigState = channel.ColorCodeAsRigState;

                for (int p = 0; p < count; p++)
                {
                    double curveVal = channelValues[p];
                    double indexVal = data.IndexValues[p];

                    Color ptColor = Color.Empty;
                    if (colorByRigState)
                    {
                        int argb = p < data.RigStateColors.Count ? data.RigStateColors[p] : 0;
                        ptColor = argb != 0 ? Color.FromArgb(argb) : ParseColor(channel.LineColor, Color.SteelBlue);
                    }

                    if (isHorizontal)
                    {
                        // X is index (time/depth), Y is curve value
                        if (colorByRigState && ptColor != Color.Empty)
                        {
                            series.Add(indexVal, curveVal, ptColor);
                        }
                        else
                        {
                            series.Add(indexVal, curveVal);
                        }
                    }
                    else
                    {
                        // X is curve value, Y is index (time/depth)
                        if (colorByRigState && ptColor != Color.Empty)
                        {
                            series.Add(curveVal, indexVal, ptColor);
                        }
                        else
                        {
                            series.Add(curveVal, indexVal);
                        }
                    }
                }
            }

            _tChart.Series.Add(series);
            channel.__chartSeries = series;
        }
    }

    private Series CreateSeriesForChannel(VHTrackChannel channel, bool isTimeLog, bool isHorizontal)
    {
        Series series;
        switch (channel.SeriesType)
        {
            case enumRTSeriesStyle.Point:
                var ptSeries = new Points(_tChart.Chart);
                ptSeries.Pointer.Style = ConvertPointStyle(channel.PointStyle);
                ptSeries.Pointer.Brush.Color = ParseColor(channel.PointFillColor, Color.DodgerBlue);
                ptSeries.Pointer.Pen.Color = ParseColor(channel.PointBorderColor, Color.DarkBlue);
                ptSeries.Pointer.Pen.Width = (int)Math.Max(1, channel.PointBorderWidth);
                ptSeries.Pointer.HorizSize = (int)Math.Max(2, channel.PointWidth);
                ptSeries.Pointer.VertSize = (int)Math.Max(2, channel.PointHeight);
                series = ptSeries;
                break;

            case enumRTSeriesStyle.Area:
                var areaSeries = new Area(_tChart.Chart);
                areaSeries.AreaBrush.Color = ParseColor(channel.AreaFillColor, Color.FromArgb(120, Color.Gold));
                areaSeries.LinePen.Color = ParseColor(channel.AreaLineColor, Color.DarkOrange);
                areaSeries.LinePen.Width = (int)Math.Max(1, channel.AreaLineWidth);
                areaSeries.LinePen.Style = ConvertLineStyle(channel.AreaLineStyle);
                series = areaSeries;
                break;

            case enumRTSeriesStyle.Line:
            case enumRTSeriesStyle.ColorFill:
            default:
                if (channel.ColorCodeAsRigState)
                {
                    // Steema.TeeChart.Styles.Line natively supports ColorEachLine for multi-colored line segments
                    var line = new Steema.TeeChart.Styles.Line(_tChart.Chart);
                    line.ColorEach = true;
                    line.ColorEachLine = true;
                    line.Pointer.Visible = false;
                    line.LinePen.Width = (int)Math.Max(1, channel.LineWidth);
                    line.LinePen.Style = ConvertLineStyle(channel.LineStyle);
                    series = line;
                }
                else
                {
                    var fastLine = new FastLine(_tChart.Chart);
                    fastLine.LinePen.Color = ParseColor(channel.LineColor, Color.SteelBlue);
                    fastLine.LinePen.Width = (int)Math.Max(1, channel.LineWidth);
                    fastLine.LinePen.Style = ConvertLineStyle(channel.LineStyle);
                    fastLine.DrawAllPoints = true;
                    fastLine.AutoRepaint = false;
                    series = fastLine;
                }
                break;
        }

        series.XValues.Order = ValueListOrder.None;
        series.YValues.Order = ValueListOrder.None;

        if (isTimeLog)
        {
            if (isHorizontal)
            {
                series.XValues.DateTime = true;
            }
            else
            {
                series.YValues.DateTime = true;
            }
        }

        return series;
    }

    private void ApplyRigStateBands(
        VHTrackConsole console,
        List<VHTrack> visibleTracks,
        double totalWidth,
        List<RigStateInterval> intervals)
    {
        // Custom background shading for rig-state intervals can be hooked into AfterDraw
    }

    public static Color ParseColor(string? colorStr, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(colorStr)) return fallback;
        try
        {
            if (colorStr.StartsWith("#", StringComparison.Ordinal))
            {
                return ColorTranslator.FromHtml(colorStr);
            }
            var named = Color.FromName(colorStr);
            if (named.IsKnownColor) return named;
            return ColorTranslator.FromHtml(colorStr);
        }
        catch
        {
            return fallback;
        }
    }

    public static Steema.TeeChart.Drawing.DashStyle ConvertLineStyle(enumRTLineStyle style)
    {
        return style switch
        {
            enumRTLineStyle.Dash => Steema.TeeChart.Drawing.DashStyle.Dash,
            enumRTLineStyle.DashDot => Steema.TeeChart.Drawing.DashStyle.DashDot,
            enumRTLineStyle.Dot => Steema.TeeChart.Drawing.DashStyle.Dot,
            _ => Steema.TeeChart.Drawing.DashStyle.Solid
        };
    }

    public static PointerStyles ConvertPointStyle(enumRTPointStyle style)
    {
        return style switch
        {
            enumRTPointStyle.Circle => PointerStyles.Circle,
            enumRTPointStyle.Triangle => PointerStyles.Triangle,
            enumRTPointStyle.LeftTriangle => PointerStyles.LeftTriangle,
            enumRTPointStyle.RightTriangle => PointerStyles.RightTriangle,
            enumRTPointStyle.DownTriangle => PointerStyles.DownTriangle,
            enumRTPointStyle.Diamond => PointerStyles.Diamond,
            enumRTPointStyle.Star => PointerStyles.Star,
            _ => PointerStyles.Rectangle
        };
    }

    /// <summary>
    /// Performs hit-testing on the chart series given a mouse pixel coordinate.
    /// Finds the closest visible series within the specified tolerance.
    /// </summary>
    public SeriesHoverInfo? HitTestSeries(System.Windows.Point mousePixel, double tolerancePixels = 18.0)
    {
        if (_currentConsole == null || _currentData == null || _currentData.IndexValues.Count == 0)
        {
            return null;
        }

        bool isHorizontal = _currentConsole.TrackOrientation == enumTrackOrientation.Horizontal;
        var indexAxis = isHorizontal ? _tChart.Axes.Bottom : _tChart.Axes.Left;
        if (indexAxis == null || !indexAxis.Visible) return null;

        var chartRect = _tChart.Chart.ChartRect;
        if (mousePixel.X < chartRect.Left - 10 || mousePixel.X > chartRect.Right + 10 ||
            mousePixel.Y < chartRect.Top - 10 || mousePixel.Y > chartRect.Bottom + 10)
        {
            return null;
        }

        double mouseIndexVal;
        try
        {
            mouseIndexVal = indexAxis.CalcPosPoint((int)(isHorizontal ? mousePixel.X : mousePixel.Y));
        }
        catch
        {
            return null;
        }

        int sampleIdx = FindClosestIndex(_currentData.IndexValues, mouseIndexVal);
        if (sampleIdx < 0 || sampleIdx >= _currentData.IndexValues.Count) return null;

        double sampleIndexVal = _currentData.IndexValues[sampleIdx];

        VHTrack? bestTrack = null;
        VHTrackChannel? bestChannel = null;
        Series? bestSeries = null;
        double minDistance = double.MaxValue;
        int bestPx = 0, bestPy = 0;
        int bestHitPt = sampleIdx;

        var visibleTracks = _currentConsole.Tracks.Where(t => t.Visible).OrderBy(t => t.DisplayOrder).ToList();
        foreach (var track in visibleTracks)
        {
            foreach (var ch in track.Channels.Where(c => c.Visible))
            {
                var series = ch.__chartSeries as Series;
                if (series == null || series.Count == 0) continue;

                // 1. Direct TeeChart clicked hit test
                int clickedIndex = -1;
                try
                {
                    clickedIndex = series.Clicked((int)mousePixel.X, (int)mousePixel.Y);
                }
                catch { }

                if (clickedIndex >= 0 && clickedIndex < series.Count)
                {
                    bestTrack = track;
                    bestChannel = ch;
                    bestSeries = series;
                    bestHitPt = clickedIndex;
                    minDistance = 0;
                    bestPx = series.CalcXPos(clickedIndex);
                    bestPy = series.CalcYPos(clickedIndex);
                    break;
                }

                // 2. Proximity check at sampleIdx
                if (sampleIdx < series.Count)
                {
                    int px = series.CalcXPos(sampleIdx);
                    int py = series.CalcYPos(sampleIdx);

                    double dist;
                    if (isHorizontal)
                    {
                        double dY = Math.Abs(mousePixel.Y - py);
                        double dX = Math.Abs(mousePixel.X - px);
                        dist = dY + (dX * 0.2);
                    }
                    else
                    {
                        double dX = Math.Abs(mousePixel.X - px);
                        double dY = Math.Abs(mousePixel.Y - py);
                        dist = dX + (dY * 0.2);
                    }

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestTrack = track;
                        bestChannel = ch;
                        bestSeries = series;
                        bestHitPt = sampleIdx;
                        bestPx = px;
                        bestPy = py;
                    }
                }
            }

            if (minDistance == 0) break;
        }

        if (bestChannel == null || bestTrack == null || minDistance > tolerancePixels)
        {
            return null;
        }

        // Retrieve channel value
        double val = 0.0;
        if (_currentData.ChannelValues.TryGetValue(bestChannel.Mnemonic, out var cVals) && bestHitPt < cVals.Count)
        {
            val = cVals[bestHitPt];
        }
        else if (bestSeries != null && bestHitPt < bestSeries.Count)
        {
            val = isHorizontal ? bestSeries.YValues[bestHitPt] : bestSeries.XValues[bestHitPt];
        }

        // Resolve Rig State at this sample
        string? rsName = null;
        int? rsNum = null;
        string? rsColorHex = null;

        if (bestHitPt < _currentData.RigStateNumbers.Count)
        {
            rsNum = _currentData.RigStateNumbers[bestHitPt];
            int clr = bestHitPt < _currentData.RigStateColors.Count ? _currentData.RigStateColors[bestHitPt] : 0;
            if (clr != 0)
            {
                rsColorHex = RigStateService.ConvertColorToHex(clr);
            }

            if (_currentRigStateIntervals != null && _currentRigStateIntervals.Count > 0)
            {
                var interval = _currentRigStateIntervals.FirstOrDefault(i =>
                    (sampleIndexVal >= Math.Min(i.StartIndex, i.EndIndex) - 1e-5 && sampleIndexVal <= Math.Max(i.StartIndex, i.EndIndex) + 1e-5) ||
                    i.StateNumber == rsNum);
                if (interval != null)
                {
                    rsName = interval.StateName;
                    if (string.IsNullOrEmpty(rsColorHex)) rsColorHex = interval.ColorHex;
                }
            }

            if (string.IsNullOrEmpty(rsName))
            {
                var defItem = RigStateService.DefaultRigStateItems.FirstOrDefault(i => i.Number == rsNum);
                if (defItem != default)
                {
                    rsName = defItem.Name;
                    if (string.IsNullOrEmpty(rsColorHex)) rsColorHex = RigStateService.ConvertColorToHex(defItem.Color);
                }
                else
                {
                    rsName = $"State {rsNum}";
                }
            }
        }

        DateTime? indexDt = null;
        string formattedIndex;
        if (_currentConsole.IndexType == enumIndexType.TimeLog)
        {
            try
            {
                indexDt = DateTime.FromOADate(sampleIndexVal);
                formattedIndex = indexDt.Value.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                formattedIndex = sampleIndexVal.ToString("F1", CultureInfo.InvariantCulture);
            }
        }
        else
        {
            formattedIndex = $"{sampleIndexVal:N2} m";
        }

        return new SeriesHoverInfo
        {
            TrackTitle = bestTrack.Title,
            ChannelMnemonic = bestChannel.Mnemonic,
            ChannelTitle = !string.IsNullOrWhiteSpace(bestChannel.Title) ? bestChannel.Title : bestChannel.Mnemonic,
            Unit = bestChannel.Unit,
            LineColorHex = !string.IsNullOrWhiteSpace(bestChannel.LineColor) ? bestChannel.LineColor : "#1976D2",
            Value = val,
            FormattedValue = val.ToString("N2", CultureInfo.InvariantCulture),
            IndexValue = sampleIndexVal,
            IndexDateTime = indexDt,
            FormattedIndex = formattedIndex,
            PointIndex = bestHitPt,
            RigStateName = rsName,
            RigStateNumber = rsNum,
            RigStateColorHex = !string.IsNullOrWhiteSpace(rsColorHex) ? rsColorHex : "#9E9E9E",
            SeriesPixelX = bestPx,
            SeriesPixelY = bestPy,
            DistanceToSeries = minDistance
        };
    }

    /// <summary>
    /// Gets cursor telemetry (index value and rig state classification) for the mouse position.
    /// </summary>
    public (string FormattedIndex, string RigStateName, string RigStateColorHex)? GetCursorTelemetry(System.Windows.Point mousePixel)
    {
        if (_currentConsole == null || _currentData == null || _currentData.IndexValues.Count == 0) return null;

        bool isHorizontal = _currentConsole.TrackOrientation == enumTrackOrientation.Horizontal;
        var indexAxis = isHorizontal ? _tChart.Axes.Bottom : _tChart.Axes.Left;
        if (indexAxis == null || !indexAxis.Visible) return null;

        double mouseIndexVal;
        try
        {
            mouseIndexVal = indexAxis.CalcPosPoint((int)(isHorizontal ? mousePixel.X : mousePixel.Y));
        }
        catch
        {
            return null;
        }

        int sampleIdx = FindClosestIndex(_currentData.IndexValues, mouseIndexVal);
        if (sampleIdx < 0 || sampleIdx >= _currentData.IndexValues.Count) return null;

        double sampleIndexVal = _currentData.IndexValues[sampleIdx];
        string formattedIndex;
        if (_currentConsole.IndexType == enumIndexType.TimeLog)
        {
            try
            {
                formattedIndex = DateTime.FromOADate(sampleIndexVal).ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch
            {
                formattedIndex = sampleIndexVal.ToString("F1", CultureInfo.InvariantCulture);
            }
        }
        else
        {
            formattedIndex = $"{sampleIndexVal:N2} m";
        }

        string rsName = "None";
        string rsColorHex = "#9E9E9E";
        if (sampleIdx < _currentData.RigStateNumbers.Count)
        {
            int rsNum = _currentData.RigStateNumbers[sampleIdx];
            var defItem = RigStateService.DefaultRigStateItems.FirstOrDefault(i => i.Number == rsNum);
            rsName = defItem != default ? defItem.Name : $"State {rsNum}";
            int clr = sampleIdx < _currentData.RigStateColors.Count ? _currentData.RigStateColors[sampleIdx] : 0;
            rsColorHex = clr != 0 ? RigStateService.ConvertColorToHex(clr) : (defItem != default ? RigStateService.ConvertColorToHex(defItem.Color) : "#9E9E9E");
        }

        return (formattedIndex, rsName, rsColorHex);
    }

    /// <summary>
    /// Binary search helper to find the closest index in a sorted or reverse-sorted list of doubles.
    /// </summary>
    public static int FindClosestIndex(List<double> list, double target)
    {
        if (list == null || list.Count == 0) return -1;
        if (list.Count == 1) return 0;

        bool isAscending = list[^1] >= list[0];
        int low = 0;
        int high = list.Count - 1;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            double midVal = list[mid];

            if (Math.Abs(midVal - target) < 1e-9) return mid;

            if (isAscending)
            {
                if (midVal < target) low = mid + 1;
                else high = mid - 1;
            }
            else
            {
                if (midVal > target) low = mid + 1;
                else high = mid - 1;
            }
        }

        int c1 = Math.Clamp(low, 0, list.Count - 1);
        int c2 = Math.Clamp(high, 0, list.Count - 1);
        return Math.Abs(list[c1] - target) < Math.Abs(list[c2] - target) ? c1 : c2;
    }
}

/// <summary>
/// Data payload returned when a chart series is hit-tested during mouse hover.
/// </summary>
public class SeriesHoverInfo
{
    public string TrackTitle { get; set; } = string.Empty;
    public string ChannelMnemonic { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string LineColorHex { get; set; } = "#1976D2";
    public double Value { get; set; }
    public string FormattedValue { get; set; } = string.Empty;
    public double IndexValue { get; set; }
    public DateTime? IndexDateTime { get; set; }
    public string FormattedIndex { get; set; } = string.Empty;
    public int PointIndex { get; set; }
    public string? RigStateName { get; set; }
    public int? RigStateNumber { get; set; }
    public string? RigStateColorHex { get; set; }
    public int SeriesPixelX { get; set; }
    public int SeriesPixelY { get; set; }
    public double DistanceToSeries { get; set; }
}
