using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DrillIntel.Models.TagVisualizer;
using DrillIntel.ViewModels;

namespace DrillIntel.Controls.TagVisualizer;

/// <summary>
/// Custom WPF graph control rendering Depth vs DateTime telemetry with overlapping phase tag rectangles.
/// Supports interactive edge dragging (resizing tags), tag selection, hovering, crosshair, and zoom/pan.
/// </summary>
public class DepthTimeTagGraphControl : FrameworkElement
{
    private const double MarginLeft = 70;
    private const double MarginRight = 30;
    private const double MarginTop = 30;
    private const double MarginBottom = 45;
    private const double HandleWidth = 8;

    private enum DragMode
    {
        None,
        LeftHandle,
        RightHandle,
        TagBody,
        PanPlot,
        BoxZoom
    }

    private DragMode _currentDragMode = DragMode.None;
    private TagVisualizerItem? _draggedTag;
    private Point _dragStartMousePos;
    private Point? _boxZoomCurrentPos;
    private DateTime _dragOriginalStart;
    private DateTime _dragOriginalEnd;
    private Point? _currentMousePos;
    private TagVisualizerItem? _hoveredTag;

    public DepthTimeTagGraphControl()
    {
        ClipToBounds = true;
        Focusable = true;

        MouseMove += OnMouseMove;
        MouseDown += OnMouseDown;
        MouseUp += OnMouseUp;
        MouseLeave += OnMouseLeave;
        MouseWheel += OnMouseWheel;
        DataContextChanged += OnDataContextChanged;
    }

    private TagVisualizerViewModel? ViewModel => DataContext as TagVisualizerViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TagVisualizerViewModel oldVm)
        {
            oldVm.RequestRepaint -= InvalidateVisual;
        }

        if (e.NewValue is TagVisualizerViewModel newVm)
        {
            newVm.RequestRepaint -= InvalidateVisual;
            newVm.RequestRepaint += InvalidateVisual;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;

        if (width < 100 || height < 100) return;

        var vm = ViewModel;
        if (vm == null) return;

        Rect plotRect = new Rect(
            MarginLeft,
            MarginTop,
            Math.Max(10, width - MarginLeft - MarginRight),
            Math.Max(10, height - MarginTop - MarginBottom));

        // Background
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFC)), new Pen(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)), 1.0), plotRect);

        // 1. Grid lines and Axis Ticks
        DrawAxesAndGrid(dc, plotRect, vm);

        // Clip to plot area for data and tags
        dc.PushClip(new RectangleGeometry(plotRect));

        // 2. Tag Rectangles Layer (Overlapping graph)
        DrawTagRectangles(dc, plotRect, vm);

        // 3. Depth vs DateTime Telemetry Curve Layer
        DrawDepthCurve(dc, plotRect, vm);

        dc.Pop(); // End plotRect clip

        // 4. Axis Labels and Titles
        DrawAxisLabels(dc, plotRect, vm);

        // 5. Interactive Crosshair & Hover telemetry
        if (_currentMousePos.HasValue && plotRect.Contains(_currentMousePos.Value))
        {
            DrawCrosshairAndTooltip(dc, plotRect, _currentMousePos.Value, vm);
        }

        // 6. Interactive Box Zoom Rubberband Overlay
        if (_currentDragMode == DragMode.BoxZoom && _boxZoomCurrentPos.HasValue)
        {
            double x1 = Math.Min(_dragStartMousePos.X, _boxZoomCurrentPos.Value.X);
            double x2 = Math.Max(_dragStartMousePos.X, _boxZoomCurrentPos.Value.X);
            x1 = Math.Max(plotRect.Left, Math.Min(plotRect.Right, x1));
            x2 = Math.Max(plotRect.Left, Math.Min(plotRect.Right, x2));

            if (x2 > x1)
            {
                Rect zoomBox = new Rect(x1, plotRect.Top, x2 - x1, plotRect.Height);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(50, 57, 73, 171)),
                    new Pen(new SolidColorBrush(Color.FromRgb(57, 73, 171)), 1.5) { DashStyle = DashStyles.Dash },
                    zoomBox);

                // Draw time duration badge inside zoom box
                DateTime t1 = XToTime(x1, plotRect, vm);
                DateTime t2 = XToTime(x2, plotRect, vm);
                var duration = t2 - t1;
                string spanText = $"Zoom: {duration.TotalHours:0.0}h ({t1:dd-MMM HH:mm} - {t2:dd-MMM HH:mm})";
                var ft = new FormattedText(spanText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    11, Brushes.White, 1.0);
                Point badgePos = new Point(x1 + 6, plotRect.Top + 6);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210, 30, 41, 59)), null,
                    new Rect(badgePos.X - 4, badgePos.Y - 2, ft.Width + 8, ft.Height + 4), 3, 3);
                dc.DrawText(ft, badgePos);
            }
        }
    }

    private void DrawAxesAndGrid(DrawingContext dc, Rect plotRect, TagVisualizerViewModel vm)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x35, 0x94, 0xA3, 0xB8)), 1.0);
        gridPen.DashStyle = DashStyles.Dot;
        gridPen.Freeze();

        // Horizontal Grid (Depth)
        int depthTicks = 6;
        double depthRange = vm.ViewMaxDepth - vm.ViewMinDepth;
        if (depthRange > 0)
        {
            for (int i = 0; i <= depthTicks; i++)
            {
                double depthVal = vm.ViewMinDepth + (depthRange / depthTicks) * i;
                double y = DepthToY(depthVal, plotRect, vm);

                if (vm.ShowGridLines)
                {
                    dc.DrawLine(gridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));
                }

                // Y-Axis tick label
                var ft = new FormattedText(
                    $"{depthVal:F0} ft",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(ft, new Point(plotRect.Left - ft.Width - 8, y - ft.Height / 2));
            }
        }

        // Vertical Grid (DateTime)
        int timeTicks = Math.Max(3, (int)(plotRect.Width / 140));
        double timeSecs = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds;
        if (timeSecs > 0)
        {
            for (int i = 0; i <= timeTicks; i++)
            {
                DateTime dt = vm.ViewMinDate.AddSeconds((timeSecs / timeTicks) * i);
                double x = TimeToX(dt, plotRect, vm);

                if (vm.ShowGridLines)
                {
                    dc.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
                }

                // X-Axis tick label
                string format = timeSecs > 86400 * 2 ? "dd-MMM HH:mm" : "HH:mm:ss";
                var ft = new FormattedText(
                    dt.ToString(format, CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    11,
                    new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(ft, new Point(x - ft.Width / 2, plotRect.Bottom + 6));
            }
        }
    }

    private void DrawTagRectangles(DrawingContext dc, Rect plotRect, TagVisualizerViewModel vm)
    {
        var selectionBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)), 2.5);
        selectionBorderPen.Freeze();

        var handleBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
        handleBrush.Freeze();

        foreach (var tag in vm.Tags)
        {
            double xStart = TimeToX(tag.StartDate, plotRect, vm);
            double xEnd = TimeToX(tag.EndDate, plotRect, vm);

            if (xEnd < plotRect.Left || xStart > plotRect.Right) continue;

            double tagWidth = Math.Max(3, xEnd - xStart);
            Rect tagRect = new Rect(xStart, plotRect.Top, tagWidth, plotRect.Height);

            // Fill translucent rectangle
            dc.DrawRectangle(tag.FillBrush, new Pen(tag.BorderBrush, tag.IsSelected ? 2.0 : 1.0), tagRect);

            // Visible range within plot area
            double visibleLeft = Math.Max(xStart, plotRect.Left);
            double visibleRight = Math.Min(xEnd, plotRect.Right);
            double visibleWidth = visibleRight - visibleLeft;

            // Vertical Tag Name Ribbon & Badges
            if (vm.ShowTagBadges && visibleWidth >= 12)
            {
                // Top accent strip
                Rect topStrip = new Rect(xStart, plotRect.Top, tagWidth, 3.5);
                dc.DrawRectangle(tag.HeaderBrush, null, topStrip);

                Rect ribbonRect = GetTagRibbonRect(tag, plotRect, vm);
                if (!ribbonRect.IsEmpty)
                {
                    double ribbonTop = ribbonRect.Top;
                    double labelCenterX = ribbonRect.Left + ribbonRect.Width / 2.0;

                    // Draw vertical ribbon with rounded corners
                    var ribbonBorderPen = tag.IsSelected
                        ? new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF)), 1.5)
                        : new Pen(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), 0.6);
                    ribbonBorderPen.Freeze();

                    dc.DrawRoundedRectangle(tag.HeaderBrush, ribbonBorderPen, ribbonRect, 4, 4);

                    string label = tag.TagDisplayName;
                    double fontSize = visibleWidth < 18 ? 9.5 : (visibleWidth < 30 ? 10.5 : 11.5);
                    var ft = new FormattedText(
                        label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        fontSize,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    {
                        MaxTextWidth = Math.Max(30, plotRect.Height - 50),
                        MaxTextHeight = 22,
                        Trimming = TextTrimming.CharacterEllipsis
                    };

                    // Draw vertical text rotated 90 degrees clockwise (reads top-to-bottom)
                    var transform = new TransformGroup();
                    transform.Children.Add(new RotateTransform(90));
                    transform.Children.Add(new TranslateTransform(labelCenterX + ft.Height / 2.0, ribbonTop + 7.0));

                    dc.PushTransform(transform);
                    dc.DrawText(ft, new Point(0, 0));
                    dc.Pop();
                }

                // Duration badge at the bottom of rectangle if sufficient width & height
                if (visibleWidth > 50 && plotRect.Height > 100)
                {
                    var ftDur = new FormattedText(
                        tag.DurationFormatted,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                        10,
                        new SolidColorBrush(Color.FromArgb(0xDD, 0x1E, 0x29, 0x3B)),
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(ftDur, new Point(visibleLeft + 6, plotRect.Bottom - 20));
                }
            }

            // Highlight selected tag with handles
            if (tag.IsSelected)
            {
                dc.DrawRectangle(null, selectionBorderPen, tagRect);

                // Left Drag Handle
                Rect leftHandleRect = new Rect(xStart - HandleWidth / 2, plotRect.Top + 24, HandleWidth, plotRect.Height - 24);
                dc.DrawRoundedRectangle(handleBrush, null, leftHandleRect, 2, 2);

                // Right Drag Handle
                Rect rightHandleRect = new Rect(xEnd - HandleWidth / 2, plotRect.Top + 24, HandleWidth, plotRect.Height - 24);
                dc.DrawRoundedRectangle(handleBrush, null, rightHandleRect, 2, 2);
            }
        }
    }

    /// <summary>
    /// Computes the bounding rectangle for a tag's vertical ribbon label inside the plot area.
    /// </summary>
    private Rect GetTagRibbonRect(TagVisualizerItem tag, Rect plotRect, TagVisualizerViewModel vm)
    {
        double xStart = TimeToX(tag.StartDate, plotRect, vm);
        double xEnd = TimeToX(tag.EndDate, plotRect, vm);
        double visibleLeft = Math.Max(xStart, plotRect.Left);
        double visibleRight = Math.Min(xEnd, plotRect.Right);
        double visibleWidth = visibleRight - visibleLeft;

        if (visibleWidth < 12 || visibleRight <= visibleLeft)
            return Rect.Empty;

        string label = tag.TagDisplayName;
        double fontSize = visibleWidth < 18 ? 9.5 : (visibleWidth < 30 ? 10.5 : 11.5);

        var ft = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            fontSize,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(30, plotRect.Height - 50),
            MaxTextHeight = 22,
            Trimming = TextTrimming.CharacterEllipsis
        };

        double ribbonWidth = Math.Min(visibleWidth - 2, 22.0);
        double ribbonHeight = Math.Min(plotRect.Height - 16, ft.Width + 14.0);

        double labelCenterX = (visibleWidth <= 40)
            ? (visibleLeft + visibleWidth / 2.0)
            : (visibleLeft + 15.0);

        double ribbonTop = plotRect.Top + 5.0;
        double ribbonLeft = labelCenterX - ribbonWidth / 2.0;
        return new Rect(ribbonLeft, ribbonTop, ribbonWidth, ribbonHeight);
    }

    /// <summary>
    /// Finds the target tag at the specified coordinate.
    /// Ribbons in front take precedence, followed by narrower/more specific tags among overlapping intervals.
    /// </summary>
    private TagVisualizerItem? FindTagAtPoint(Point pos, Rect plotRect, TagVisualizerViewModel vm)
    {
        if (vm.Tags == null || vm.Tags.Count == 0 || !plotRect.Contains(pos))
            return null;

        // 1. Check ribbons in reverse order (topmost ribbons in painter's order hit first)
        if (vm.ShowTagBadges)
        {
            for (int i = vm.Tags.Count - 1; i >= 0; i--)
            {
                var tag = vm.Tags[i];
                Rect ribbonRect = GetTagRibbonRect(tag, plotRect, vm);
                if (!ribbonRect.IsEmpty)
                {
                    // Generous hit box around the visible ribbon
                    Rect hitRibbon = new Rect(ribbonRect.Left - 3, ribbonRect.Top, ribbonRect.Width + 6, ribbonRect.Height + 2);
                    if (hitRibbon.Contains(pos))
                    {
                        return tag;
                    }
                }
            }
        }

        // 2. Check tag bodies: if multiple tags overlap at pos.X, choose the narrowest (shortest duration) tag
        TagVisualizerItem? bestMatch = null;
        double smallestDurationSec = double.MaxValue;

        for (int i = vm.Tags.Count - 1; i >= 0; i--)
        {
            var tag = vm.Tags[i];
            double xStart = TimeToX(tag.StartDate, plotRect, vm);
            double xEnd = TimeToX(tag.EndDate, plotRect, vm);
            Rect r = new Rect(xStart, plotRect.Top, Math.Max(4, xEnd - xStart), plotRect.Height);

            if (r.Contains(pos))
            {
                double durationSec = (tag.EndDate - tag.StartDate).TotalSeconds;
                if (durationSec < smallestDurationSec)
                {
                    smallestDurationSec = durationSec;
                    bestMatch = tag;
                }
            }
        }

        return bestMatch;
    }

    private void DrawDepthCurve(DrawingContext dc, Rect plotRect, TagVisualizerViewModel vm)
    {
        var points = vm.DepthPoints;
        if (points == null || points.Count < 2) return;

        var curvePen = new Pen(new SolidColorBrush(Color.FromRgb(0x0D, 0x47, 0xA1)), 2.2);
        curvePen.Freeze();

        var streamGeo = new StreamGeometry();
        using (var ctx = streamGeo.Open())
        {
            bool started = false;

            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                if (pt.DateTime < vm.ViewMinDate.AddHours(-1) || pt.DateTime > vm.ViewMaxDate.AddHours(1))
                    continue;

                double x = TimeToX(pt.DateTime, plotRect, vm);
                double y = DepthToY(pt.Depth, plotRect, vm);

                var p = new Point(x, y);

                if (!started)
                {
                    ctx.BeginFigure(p, false, false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(p, true, true);
                }
            }
        }

        streamGeo.Freeze();
        dc.DrawGeometry(null, curvePen, streamGeo);
    }

    private void DrawAxisLabels(DrawingContext dc, Rect plotRect, TagVisualizerViewModel vm)
    {
        // Y-Axis Title: Depth
        var depthTitle = new FormattedText(
            vm.InvertDepth ? "Depth (ft) [0 at surface ↓]" : "Depth (ft)",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // Draw rotated title on Y-axis
        dc.PushTransform(new RotateTransform(-90, 16, plotRect.Top + plotRect.Height / 2));
        dc.DrawText(depthTitle, new Point(16 - depthTitle.Width / 2, plotRect.Top + plotRect.Height / 2 - depthTitle.Height / 2));
        dc.Pop();

        // X-Axis Title: DateTime
        var timeTitle = new FormattedText(
            "Date Time (UTC)",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        dc.DrawText(timeTitle, new Point(plotRect.Left + plotRect.Width / 2 - timeTitle.Width / 2, plotRect.Bottom + 24));
    }

    private void DrawCrosshairAndTooltip(DrawingContext dc, Rect plotRect, Point pos, TagVisualizerViewModel vm)
    {
        var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x47, 0x55, 0x69)), 1.0);
        crossPen.DashStyle = DashStyles.Dash;
        crossPen.Freeze();

        // Vertical crosshair
        dc.DrawLine(crossPen, new Point(pos.X, plotRect.Top), new Point(pos.X, plotRect.Bottom));
        // Horizontal crosshair
        dc.DrawLine(crossPen, new Point(plotRect.Left, pos.Y), new Point(plotRect.Right, pos.Y));

        DateTime dt = XToTime(pos.X, plotRect, vm);
        double depth = YToDepth(pos.Y, plotRect, vm);

        // Crosshair coordinate badge
        string coordText = $"{dt:dd-MMM HH:mm:ss} | {depth:F1} ft";
        var ftCoord = new FormattedText(
            coordText,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double badgeW = ftCoord.Width + 12;
        double badgeH = ftCoord.Height + 6;
        double badgeX = Math.Min(plotRect.Right - badgeW, Math.Max(plotRect.Left, pos.X - badgeW / 2));
        double badgeY = plotRect.Top + 6;

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xEE, 0x1E, 0x29, 0x3B)), null, new Rect(badgeX, badgeY, badgeW, badgeH), 4, 4);
        dc.DrawText(ftCoord, new Point(badgeX + 6, badgeY + 3));

        // If hovering over a tag, draw rich card tooltip
        if (_hoveredTag != null)
        {
            DrawHoverCard(dc, plotRect, pos, _hoveredTag);
        }
    }

    private void DrawHoverCard(DrawingContext dc, Rect plotRect, Point pos, TagVisualizerItem tag)
    {
        string header = tag.TagDisplayName;
        string line1 = $"Time: {tag.StartDate:HH:mm:ss} -> {tag.EndDate:HH:mm:ss} ({tag.DurationFormatted})";
        string line2 = $"Depth: {tag.StartDepth:F1} ft -> {tag.EndDepth:F1} ft";
        string line3 = !string.IsNullOrWhiteSpace(tag.Notes) ? $"Notes: {tag.Notes}" : "";

        var ftHeader = new FormattedText(
            header,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            12,
            tag.BorderBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var ftBody = new FormattedText(
            line3.Length > 0 ? $"{line1}\n{line2}\n{line3}" : $"{line1}\n{line2}",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        double cardW = Math.Max(ftHeader.Width, ftBody.Width) + 20;
        double cardH = ftHeader.Height + ftBody.Height + 16;
        double cardX = Math.Min(plotRect.Right - cardW - 10, pos.X + 15);
        double cardY = Math.Min(plotRect.Bottom - cardH - 10, pos.Y + 15);

        dc.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromArgb(0xF2, 0x0F, 0x17, 0x2A)),
            new Pen(tag.BorderBrush, 1.5),
            new Rect(cardX, cardY, cardW, cardH),
            6, 6);

        dc.DrawText(ftHeader, new Point(cardX + 10, cardY + 8));
        dc.DrawText(ftBody, new Point(cardX + 10, cardY + 8 + ftHeader.Height + 2));
    }

    // =========================================================================
    // Mouse Event Handlers & Interactive Adjustments
    // =========================================================================

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        Point pos = e.GetPosition(this);
        _currentMousePos = pos;

        Rect plotRect = GetPlotRect();

        if (_currentDragMode == DragMode.LeftHandle && _draggedTag != null)
        {
            DateTime newStart = XToTime(pos.X, plotRect, vm);
            vm.AdjustTagStartDate(_draggedTag, newStart);
            InvalidateVisual();
            return;
        }

        if (_currentDragMode == DragMode.RightHandle && _draggedTag != null)
        {
            DateTime newEnd = XToTime(pos.X, plotRect, vm);
            vm.AdjustTagEndDate(_draggedTag, newEnd);
            InvalidateVisual();
            return;
        }

        if (_currentDragMode == DragMode.TagBody && _draggedTag != null)
        {
            double deltaX = pos.X - _dragStartMousePos.X;
            double secondsPerPixel = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds / plotRect.Width;
            double deltaSeconds = deltaX * secondsPerPixel;

            _draggedTag.StartDate = _dragOriginalStart.AddSeconds(deltaSeconds);
            _draggedTag.EndDate = _dragOriginalEnd.AddSeconds(deltaSeconds);
            vm.NotifyTagDragged(_draggedTag);
            InvalidateVisual();
            return;
        }

        if (_currentDragMode == DragMode.BoxZoom)
        {
            _boxZoomCurrentPos = pos;
            InvalidateVisual();
            return;
        }

        if (_currentDragMode == DragMode.PanPlot)
        {
            double deltaX = pos.X - _dragStartMousePos.X;
            double secondsPerPixel = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds / plotRect.Width;
            double deltaSeconds = -deltaX * secondsPerPixel;

            vm.ViewMinDate = _dragOriginalStart.AddSeconds(deltaSeconds);
            vm.ViewMaxDate = _dragOriginalEnd.AddSeconds(deltaSeconds);
            InvalidateVisual();
            return;
        }

        // Update Cursor & Hovered Tag
        UpdateHoverState(pos, plotRect, vm);
        InvalidateVisual();
    }

    private void UpdateHoverState(Point pos, Rect plotRect, TagVisualizerViewModel vm)
    {
        _hoveredTag = null;
        Cursor = Cursors.Cross;

        if (!plotRect.Contains(pos)) return;

        // Check if hovering over selected tag's handles
        if (vm.SelectedTag != null)
        {
            double xStart = TimeToX(vm.SelectedTag.StartDate, plotRect, vm);
            double xEnd = TimeToX(vm.SelectedTag.EndDate, plotRect, vm);

            Rect leftHandle = new Rect(xStart - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);
            Rect rightHandle = new Rect(xEnd - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);

            if (leftHandle.Contains(pos) || rightHandle.Contains(pos))
            {
                Cursor = Cursors.SizeWE;
                return;
            }
        }

        // Check boundary edges of any tag (reverse order so topmost handles are tested first)
        for (int i = vm.Tags.Count - 1; i >= 0; i--)
        {
            var tag = vm.Tags[i];
            double xStart = TimeToX(tag.StartDate, plotRect, vm);
            double xEnd = TimeToX(tag.EndDate, plotRect, vm);

            Rect leftHandle = new Rect(xStart - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);
            Rect rightHandle = new Rect(xEnd - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);

            if (leftHandle.Contains(pos) || rightHandle.Contains(pos))
            {
                Cursor = Cursors.SizeWE;
                return;
            }
        }

        // Check tags using FindTagAtPoint (foreground ribbon takes precedence over background span)
        var hit = FindTagAtPoint(pos, plotRect, vm);
        if (hit != null)
        {
            _hoveredTag = hit;
            Cursor = Cursors.Hand;
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        Point pos = e.GetPosition(this);
        Rect plotRect = GetPlotRect();
        if (!plotRect.Contains(pos)) return;

        Focus();

        // 1. Double Click Handler: Zoom to Tag or Reset Zoom
        if (e.ClickCount == 2 && e.LeftButton == MouseButtonState.Pressed)
        {
            TagVisualizerItem? hitTag = FindTagAtPoint(pos, plotRect, vm);

            if (hitTag != null)
            {
                vm.SelectTag(hitTag);
                vm.ZoomToSelectedTag();
            }
            else
            {
                vm.ResetZoom();
            }
            InvalidateVisual();
            return;
        }

        // 2. Right Button: Interactive Rubberband Box Zoom
        if (e.RightButton == MouseButtonState.Pressed)
        {
            _currentDragMode = DragMode.BoxZoom;
            _dragStartMousePos = pos;
            _boxZoomCurrentPos = pos;
            CaptureMouse();
            InvalidateVisual();
            return;
        }

        // 3. Middle Button: Pan
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _currentDragMode = DragMode.PanPlot;
            _dragStartMousePos = pos;
            _dragOriginalStart = vm.ViewMinDate;
            _dragOriginalEnd = vm.ViewMaxDate;
            CaptureMouse();
            return;
        }

        // 4. Left Button: Resize handles, drag tag, or pan empty area
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            // Check selected tag's handles first
            if (vm.SelectedTag != null)
            {
                double xStart = TimeToX(vm.SelectedTag.StartDate, plotRect, vm);
                double xEnd = TimeToX(vm.SelectedTag.EndDate, plotRect, vm);

                Rect leftHandle = new Rect(xStart - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);
                Rect rightHandle = new Rect(xEnd - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);

                // If handles overlap (very narrow tag on screen), pick closer handle
                if (leftHandle.Contains(pos) && rightHandle.Contains(pos))
                {
                    double midX = (xStart + xEnd) / 2.0;
                    _currentDragMode = (pos.X <= midX) ? DragMode.LeftHandle : DragMode.RightHandle;
                    _draggedTag = vm.SelectedTag;
                    CaptureMouse();
                    return;
                }
                if (leftHandle.Contains(pos))
                {
                    _currentDragMode = DragMode.LeftHandle;
                    _draggedTag = vm.SelectedTag;
                    CaptureMouse();
                    return;
                }
                if (rightHandle.Contains(pos))
                {
                    _currentDragMode = DragMode.RightHandle;
                    _draggedTag = vm.SelectedTag;
                    CaptureMouse();
                    return;
                }
            }

            // Also check boundary edges of any tag (reverse order so foreground handles win)
            for (int i = vm.Tags.Count - 1; i >= 0; i--)
            {
                var tag = vm.Tags[i];
                double xStart = TimeToX(tag.StartDate, plotRect, vm);
                double xEnd = TimeToX(tag.EndDate, plotRect, vm);

                Rect leftHandle = new Rect(xStart - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);
                Rect rightHandle = new Rect(xEnd - HandleWidth, plotRect.Top, HandleWidth * 2, plotRect.Height);

                if (leftHandle.Contains(pos))
                {
                    vm.SelectTag(tag);
                    _currentDragMode = DragMode.LeftHandle;
                    _draggedTag = tag;
                    CaptureMouse();
                    return;
                }
                if (rightHandle.Contains(pos))
                {
                    vm.SelectTag(tag);
                    _currentDragMode = DragMode.RightHandle;
                    _draggedTag = tag;
                    CaptureMouse();
                    return;
                }
            }

            // Check if clicked inside a tag (ribbon / foreground priority)
            TagVisualizerItem? hitTag = FindTagAtPoint(pos, plotRect, vm);

            if (hitTag != null)
            {
                vm.SelectTag(hitTag);
                _draggedTag = hitTag;
                _currentDragMode = DragMode.TagBody;
                _dragStartMousePos = pos;
                _dragOriginalStart = hitTag.StartDate;
                _dragOriginalEnd = hitTag.EndDate;
                CaptureMouse();
                InvalidateVisual();
                return;
            }

            // Clicked empty area -> Pan mode
            _currentDragMode = DragMode.PanPlot;
            _dragStartMousePos = pos;
            _dragOriginalStart = vm.ViewMinDate;
            _dragOriginalEnd = vm.ViewMaxDate;
            CaptureMouse();
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var vm = ViewModel;
        if (_currentDragMode == DragMode.BoxZoom && vm != null)
        {
            Point pos = e.GetPosition(this);
            Rect plotRect = GetPlotRect();
            double dragDistance = Math.Abs(pos.X - _dragStartMousePos.X);

            if (dragDistance > 12)
            {
                DateTime t1 = XToTime(Math.Min(_dragStartMousePos.X, pos.X), plotRect, vm);
                DateTime t2 = XToTime(Math.Max(_dragStartMousePos.X, pos.X), plotRect, vm);

                if (t2 > t1.AddSeconds(10))
                {
                    vm.ViewMinDate = t1;
                    vm.ViewMaxDate = t2;
                    vm.SelectedPreset = "Custom Range";
                }
            }

            _currentDragMode = DragMode.None;
            _boxZoomCurrentPos = null;
            ReleaseMouseCapture();
            InvalidateVisual();
            return;
        }

        if (_currentDragMode != DragMode.None)
        {
            _currentDragMode = DragMode.None;
            _draggedTag = null;
            ReleaseMouseCapture();
            InvalidateVisual();
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        _currentMousePos = null;
        _hoveredTag = null;
        InvalidateVisual();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        Point pos = e.GetPosition(this);
        Rect plotRect = GetPlotRect();
        if (!plotRect.Contains(pos)) return;

        double factor = e.Delta > 0 ? 0.75 : 1.33;
        double currentSpan = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds;
        double newSpan = currentSpan * factor;

        // Dynamic span limit supporting multi-month wells
        double maxSpan = Math.Max(86400.0 * 365.0 * 5.0, (vm.DataMaxDate - vm.DataMinDate).TotalSeconds * 2.0);
        if (newSpan < 10 || newSpan > maxSpan) return;

        double mouseFraction = (pos.X - plotRect.Left) / plotRect.Width;
        DateTime mouseTime = vm.ViewMinDate.AddSeconds(currentSpan * mouseFraction);

        vm.ViewMinDate = mouseTime.AddSeconds(-newSpan * mouseFraction);
        vm.ViewMaxDate = mouseTime.AddSeconds(newSpan * (1.0 - mouseFraction));
        vm.SelectedPreset = "Custom Range";

        InvalidateVisual();
    }

    // =========================================================================
    // Coordinate Conversion Helpers
    // =========================================================================

    private Rect GetPlotRect()
    {
        return new Rect(
            MarginLeft,
            MarginTop,
            Math.Max(10, ActualWidth - MarginLeft - MarginRight),
            Math.Max(10, ActualHeight - MarginTop - MarginBottom));
    }

    private double TimeToX(DateTime dt, Rect plotRect, TagVisualizerViewModel vm)
    {
        double totalSecs = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds;
        if (totalSecs <= 0) return plotRect.Left;

        double fraction = (dt - vm.ViewMinDate).TotalSeconds / totalSecs;
        return plotRect.Left + fraction * plotRect.Width;
    }

    private DateTime XToTime(double x, Rect plotRect, TagVisualizerViewModel vm)
    {
        double fraction = (x - plotRect.Left) / Math.Max(1, plotRect.Width);
        double totalSecs = (vm.ViewMaxDate - vm.ViewMinDate).TotalSeconds;
        return vm.ViewMinDate.AddSeconds(totalSecs * fraction);
    }

    private double DepthToY(double depth, Rect plotRect, TagVisualizerViewModel vm)
    {
        double span = vm.ViewMaxDepth - vm.ViewMinDepth;
        if (span <= 0) return plotRect.Top;

        double fraction = (depth - vm.ViewMinDepth) / span;

        // In drilling, depth 0 is at top, increasing down
        if (vm.InvertDepth)
        {
            return plotRect.Top + fraction * plotRect.Height;
        }
        else
        {
            return plotRect.Bottom - fraction * plotRect.Height;
        }
    }

    private double YToDepth(double y, Rect plotRect, TagVisualizerViewModel vm)
    {
        double fraction = (y - plotRect.Top) / Math.Max(1, plotRect.Height);
        double span = vm.ViewMaxDepth - vm.ViewMinDepth;

        if (vm.InvertDepth)
        {
            return vm.ViewMinDepth + fraction * span;
        }
        else
        {
            return vm.ViewMaxDepth - fraction * span;
        }
    }
}

