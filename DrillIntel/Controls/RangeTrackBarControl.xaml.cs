using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DrillIntel.Controls;

public partial class RangeTrackBarControl : UserControl
{
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnBoundsChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnBoundsChanged));

    public static readonly DependencyProperty SelectionStartProperty =
        DependencyProperty.Register(nameof(SelectionStart), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));

    public static readonly DependencyProperty SelectionEndProperty =
        DependencyProperty.Register(nameof(SelectionEnd), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(25.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionChanged));

    public static readonly DependencyProperty IsTimeLogProperty =
        DependencyProperty.Register(nameof(IsTimeLog), typeof(bool), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(true, OnFormattingChanged));

    public static readonly DependencyProperty SelectedPeriodProperty =
        DependencyProperty.Register(nameof(SelectedPeriod), typeof(string), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata("2 Hours", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedPeriodChanged));

    public static readonly DependencyProperty OverviewPointsProperty =
        DependencyProperty.Register(nameof(OverviewPoints), typeof(PointCollection), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnOverviewChanged));

    public static readonly DependencyProperty OverviewStrokeProperty =
        DependencyProperty.Register(nameof(OverviewStroke), typeof(Brush), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(null, OnOverviewAppearanceChanged));

    public static readonly DependencyProperty OverviewFillProperty =
        DependencyProperty.Register(nameof(OverviewFill), typeof(Brush), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(null, OnOverviewAppearanceChanged));

    public static readonly DependencyProperty ShowOverviewFillProperty =
        DependencyProperty.Register(nameof(ShowOverviewFill), typeof(bool), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(true, OnOverviewAppearanceChanged));

    public static readonly DependencyProperty OverviewChannelNameProperty =
        DependencyProperty.Register(nameof(OverviewChannelName), typeof(string), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(string.Empty, OnOverviewAppearanceChanged));

    public static readonly DependencyProperty OverviewYMinProperty =
        DependencyProperty.Register(nameof(OverviewYMin), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(double.NaN, OnOverviewChanged));

    public static readonly DependencyProperty OverviewYMaxProperty =
        DependencyProperty.Register(nameof(OverviewYMax), typeof(double), typeof(RangeTrackBarControl),
            new FrameworkPropertyMetadata(double.NaN, OnOverviewChanged));

    public static readonly DependencyProperty RangeChangedCommandProperty =
        DependencyProperty.Register(nameof(RangeChangedCommand), typeof(ICommand), typeof(RangeTrackBarControl),
            new PropertyMetadata(null));

    public ICommand? RangeChangedCommand
    {
        get => (ICommand?)GetValue(RangeChangedCommandProperty);
        set => SetValue(RangeChangedCommandProperty, value);
    }

    public static readonly RoutedEvent RangeChangedEvent =
        EventManager.RegisterRoutedEvent(nameof(RangeChanged), RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(RangeTrackBarControl));

    public event RoutedEventHandler RangeChanged
    {
        add => AddHandler(RangeChangedEvent, value);
        remove => RemoveHandler(RangeChangedEvent, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double SelectionStart
    {
        get => (double)GetValue(SelectionStartProperty);
        set => SetValue(SelectionStartProperty, value);
    }

    public double SelectionEnd
    {
        get => (double)GetValue(SelectionEndProperty);
        set => SetValue(SelectionEndProperty, value);
    }

    public bool IsTimeLog
    {
        get => (bool)GetValue(IsTimeLogProperty);
        set => SetValue(IsTimeLogProperty, value);
    }

    public string SelectedPeriod
    {
        get => (string)GetValue(SelectedPeriodProperty);
        set => SetValue(SelectedPeriodProperty, value);
    }

    public PointCollection? OverviewPoints
    {
        get => (PointCollection?)GetValue(OverviewPointsProperty);
        set => SetValue(OverviewPointsProperty, value);
    }

    public Brush? OverviewStroke
    {
        get => (Brush?)GetValue(OverviewStrokeProperty);
        set => SetValue(OverviewStrokeProperty, value);
    }

    public Brush? OverviewFill
    {
        get => (Brush?)GetValue(OverviewFillProperty);
        set => SetValue(OverviewFillProperty, value);
    }

    public bool ShowOverviewFill
    {
        get => (bool)GetValue(ShowOverviewFillProperty);
        set => SetValue(ShowOverviewFillProperty, value);
    }

    public string OverviewChannelName
    {
        get => (string)GetValue(OverviewChannelNameProperty);
        set => SetValue(OverviewChannelNameProperty, value);
    }

    public double OverviewYMin
    {
        get => (double)GetValue(OverviewYMinProperty);
        set => SetValue(OverviewYMinProperty, value);
    }

    public double OverviewYMax
    {
        get => (double)GetValue(OverviewYMaxProperty);
        set => SetValue(OverviewYMaxProperty, value);
    }

    private enum DragMode { None, MoveWindow, ResizeLeft, ResizeRight }
    private DragMode _currentDragMode = DragMode.None;
    private Point _dragStartPoint;
    private double _initialSelectionStart;
    private double _initialSelectionEnd;
    private bool _isInternalUpdate;

    public RangeTrackBarControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += (s, e) =>
        {
            UpdateVisualLayout();
            UpdateOverviewVisual();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyOverviewAppearance();
        UpdateVisualLayout();
        UpdateOverviewVisual();
        SyncPeriodComboBox();
    }

    private static void OnBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl)
        {
            ctrl.UpdateVisualLayout();
            ctrl.UpdateOverviewVisual();
        }
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl && !ctrl._isInternalUpdate)
        {
            ctrl.UpdateVisualLayout();
        }
    }

    private static void OnFormattingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl)
        {
            ctrl.UpdateVisualLayout();
        }
    }

    private static void OnSelectedPeriodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl)
        {
            ctrl.SyncPeriodComboBox();
        }
    }

    private static void OnOverviewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl)
        {
            ctrl.UpdateOverviewVisual();
        }
    }

    private static void OnOverviewAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RangeTrackBarControl ctrl)
        {
            ctrl.ApplyOverviewAppearance();
            ctrl.UpdateOverviewVisual();
        }
    }

    public void ApplyOverviewAppearance()
    {
        if (OverviewPolyline != null)
        {
            if (OverviewStroke != null)
            {
                OverviewPolyline.Stroke = OverviewStroke;
            }
            else
            {
                OverviewPolyline.Stroke = new SolidColorBrush(Color.FromRgb(25, 118, 210));
            }
        }

        if (OverviewPolygon != null)
        {
            if (OverviewFill != null)
            {
                OverviewPolygon.Fill = OverviewFill;
            }
            else if (OverviewStroke is SolidColorBrush scb)
            {
                var c = scb.Color;
                OverviewPolygon.Fill = new SolidColorBrush(Color.FromArgb(35, c.R, c.G, c.B));
            }
            else
            {
                OverviewPolygon.Fill = new SolidColorBrush(Color.FromArgb(32, 25, 118, 210));
            }
        }
    }

    public void UpdateOverviewVisual()
    {
        if (OverviewCanvas == null || OverviewPolyline == null || TrackBarContainer == null) return;

        double trackWidth = TrackBarContainer.ActualWidth;
        double trackHeight = TrackBarContainer.ActualHeight;
        if (trackWidth <= 0) trackWidth = 300;
        if (trackHeight <= 0) trackHeight = 34;

        var pts = OverviewPoints;
        if (pts == null || pts.Count < 2)
        {
            OverviewPolyline.Points = new PointCollection();
            if (OverviewPolygon != null)
            {
                OverviewPolygon.Points = new PointCollection();
                OverviewPolygon.Visibility = Visibility.Collapsed;
            }
            if (TxtOverviewChannel != null) TxtOverviewChannel.Visibility = Visibility.Collapsed;
            return;
        }

        double totalRange = Maximum - Minimum;
        if (totalRange <= 0.0001) totalRange = 1.0;

        double yMin = OverviewYMin;
        double yMax = OverviewYMax;
        if (double.IsNaN(yMin) || double.IsNaN(yMax) || yMax <= yMin)
        {
            yMin = double.MaxValue;
            yMax = double.MinValue;
            foreach (var p in pts)
            {
                if (p.Y < yMin) yMin = p.Y;
                if (p.Y > yMax) yMax = p.Y;
            }
            if (yMax <= yMin)
            {
                yMin -= 1.0;
                yMax += 1.0;
            }
        }

        double yRange = yMax - yMin;
        if (yRange <= 0.0001) yRange = 1.0;

        double topPadding = 4.0;
        double bottomPadding = 4.0;
        double usableHeight = Math.Max(4.0, trackHeight - topPadding - bottomPadding);

        var screenPoints = new PointCollection(pts.Count);
        foreach (var p in pts)
        {
            double xPx = ((p.X - Minimum) / totalRange) * trackWidth;
            if (xPx < 0) xPx = 0;
            if (xPx > trackWidth) xPx = trackWidth;

            double normY = (p.Y - yMin) / yRange;
            normY = Math.Max(0.0, Math.Min(1.0, normY));
            double yPx = (trackHeight - bottomPadding) - (normY * usableHeight);

            screenPoints.Add(new Point(xPx, yPx));
        }

        OverviewPolyline.Points = screenPoints;

        if (OverviewPolygon != null && ShowOverviewFill && screenPoints.Count > 0)
        {
            var polyPoints = new PointCollection(screenPoints.Count + 2);
            polyPoints.Add(new Point(screenPoints[0].X, trackHeight));
            foreach (var pt in screenPoints)
            {
                polyPoints.Add(pt);
            }
            polyPoints.Add(new Point(screenPoints[screenPoints.Count - 1].X, trackHeight));
            OverviewPolygon.Points = polyPoints;
            OverviewPolygon.Visibility = Visibility.Visible;
        }
        else if (OverviewPolygon != null)
        {
            OverviewPolygon.Visibility = Visibility.Collapsed;
        }

        if (TxtOverviewChannel != null)
        {
            if (!string.IsNullOrWhiteSpace(OverviewChannelName))
            {
                TxtOverviewChannel.Text = $"{OverviewChannelName} [{yMin:0.#} - {yMax:0.#}]";
                TxtOverviewChannel.Visibility = Visibility.Visible;
            }
            else
            {
                TxtOverviewChannel.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void SyncPeriodComboBox()
    {
        // Period dropdown removed from TrackBar in favor of unified Preset Range
    }

    public void UpdateVisualLayout()
    {
        if (TrackBarContainer == null || SelectionWindow == null) return;

        double totalRange = Maximum - Minimum;
        if (totalRange <= 0.0001) totalRange = 1.0;

        double trackWidth = TrackBarContainer.ActualWidth;
        if (trackWidth <= 0) trackWidth = 300;

        double start = Math.Max(Minimum, Math.Min(Maximum, SelectionStart));
        double end = Math.Max(start, Math.Min(Maximum, SelectionEnd));

        double leftRatio = (start - Minimum) / totalRange;
        double widthRatio = (end - start) / totalRange;

        double leftPx = leftRatio * trackWidth;
        double widthPx = Math.Max(16, widthRatio * trackWidth);

        Canvas.SetLeft(SelectionWindow, leftPx);
        SelectionWindow.Width = widthPx;

        // Update Text Readouts
        UpdateLabels(start, end);
    }

    private void UpdateLabels(double start, double end)
    {
        if (TxtFrameSummary == null || TxtSpanDuration == null) return;

        double span = end - start;

        if (IsTimeLog && start > 1000)
        {
            try
            {
                var dtStart = DateTime.FromOADate(start);
                var dtEnd = DateTime.FromOADate(end);
                var ts = dtEnd - dtStart;

                TxtFrameSummary.Text = $"{dtStart:MMM dd HH:mm} - {dtEnd:MMM dd HH:mm}";

                if (ts.TotalDays >= 1)
                    TxtSpanDuration.Text = $"Window: {ts.TotalDays:0.#} Days";
                else if (ts.TotalHours >= 1)
                    TxtSpanDuration.Text = $"Window: {ts.TotalHours:0.#} Hours";
                else
                    TxtSpanDuration.Text = $"Window: {ts.TotalMinutes:0} Mins";
                return;
            }
            catch { }
        }

        TxtFrameSummary.Text = $"Index {start:0.0} - {end:0.0}";
        TxtSpanDuration.Text = $"Span: {span:0.0} units";
    }

    #region Mouse Drag Handling

    private void OnSelectionWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _currentDragMode == DragMode.None)
        {
            _currentDragMode = DragMode.MoveWindow;
            BeginDrag(e);
        }
    }

    private void OnLeftGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
            _currentDragMode = DragMode.ResizeLeft;
            BeginDrag(e);
        }
    }

    private void OnRightGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            e.Handled = true;
            _currentDragMode = DragMode.ResizeRight;
            BeginDrag(e);
        }
    }

    private void BeginDrag(MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(TrackBarContainer);
        _initialSelectionStart = SelectionStart;
        _initialSelectionEnd = SelectionEnd;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_currentDragMode == DragMode.None || !IsMouseCaptured) return;

        Point currentPoint = e.GetPosition(TrackBarContainer);
        double deltaPx = currentPoint.X - _dragStartPoint.X;
        double trackWidth = TrackBarContainer.ActualWidth;
        if (trackWidth <= 0) return;

        double totalRange = Maximum - Minimum;
        double deltaUnits = (deltaPx / trackWidth) * totalRange;
        double windowSpan = _initialSelectionEnd - _initialSelectionStart;

        _isInternalUpdate = true;
        try
        {
            switch (_currentDragMode)
            {
                case DragMode.MoveWindow:
                    double newStart = _initialSelectionStart + deltaUnits;
                    double newEnd = _initialSelectionEnd + deltaUnits;

                    if (newStart < Minimum)
                    {
                        newStart = Minimum;
                        newEnd = Minimum + windowSpan;
                    }
                    if (newEnd > Maximum)
                    {
                        newEnd = Maximum;
                        newStart = Maximum - windowSpan;
                    }

                    SelectionStart = newStart;
                    SelectionEnd = newEnd;
                    break;

                case DragMode.ResizeLeft:
                    double resStart = Math.Max(Minimum, Math.Min(_initialSelectionEnd - (totalRange * 0.005), _initialSelectionStart + deltaUnits));
                    SelectionStart = resStart;
                    break;

                case DragMode.ResizeRight:
                    double resEnd = Math.Min(Maximum, Math.Max(_initialSelectionStart + (totalRange * 0.005), _initialSelectionEnd + deltaUnits));
                    SelectionEnd = resEnd;
                    break;
            }
        }
        finally
        {
            _isInternalUpdate = false;
        }

        UpdateVisualLayout();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            _currentDragMode = DragMode.None;
            NotifyRangeChanged();
        }
    }

    private void NotifyRangeChanged()
    {
        RaiseEvent(new RoutedEventArgs(RangeChangedEvent));
        if (RangeChangedCommand?.CanExecute(null) == true)
        {
            RangeChangedCommand.Execute(null);
        }
    }

    private void OnTrackBarContainerMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        Point pt = e.GetPosition(TrackBarContainer);
        double trackWidth = TrackBarContainer.ActualWidth;
        if (trackWidth <= 0) return;

        double totalRange = Maximum - Minimum;
        double targetUnit = Minimum + ((pt.X / trackWidth) * totalRange);
        double windowSpan = SelectionEnd - SelectionStart;

        double newStart = targetUnit - (windowSpan / 2.0);
        double newEnd = newStart + windowSpan;

        if (newStart < Minimum) { newStart = Minimum; newEnd = Minimum + windowSpan; }
        if (newEnd > Maximum) { newEnd = Maximum; newStart = Maximum - windowSpan; }

        _isInternalUpdate = true;
        try
        {
            SelectionStart = newStart;
            SelectionEnd = newEnd;
        }
        finally
        {
            _isInternalUpdate = false;
        }

        UpdateVisualLayout();
        NotifyRangeChanged();
    }

    #endregion

    #region Steppers & Period

    private void OnStepLeftClick(object sender, RoutedEventArgs e)
    {
        double windowSpan = SelectionEnd - SelectionStart;
        double step = windowSpan * 0.25; // Nudge by 25% of active window

        double newStart = SelectionStart - step;
        double newEnd = SelectionEnd - step;

        if (newStart < Minimum)
        {
            newStart = Minimum;
            newEnd = Minimum + windowSpan;
        }

        _isInternalUpdate = true;
        try
        {
            SelectionStart = newStart;
            SelectionEnd = newEnd;
        }
        finally
        {
            _isInternalUpdate = false;
        }

        UpdateVisualLayout();
        NotifyRangeChanged();
    }

    private void OnStepRightClick(object sender, RoutedEventArgs e)
    {
        double windowSpan = SelectionEnd - SelectionStart;
        double step = windowSpan * 0.25;

        double newStart = SelectionStart + step;
        double newEnd = SelectionEnd + step;

        if (newEnd > Maximum)
        {
            newEnd = Maximum;
            newStart = Maximum - windowSpan;
        }

        _isInternalUpdate = true;
        try
        {
            SelectionStart = newStart;
            SelectionEnd = newEnd;
        }
        finally
        {
            _isInternalUpdate = false;
        }

        UpdateVisualLayout();
        NotifyRangeChanged();
    }

    private void OnPeriodSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    public void ApplyPeriodPreset(string preset)
    {
        double totalRange = Maximum - Minimum;
        double targetSpan;

        if (IsTimeLog && SelectionStart > 1000)
        {
            // OADate span (1 day = 1.0)
            targetSpan = preset switch
            {
                "15 Mins" => 15.0 / (24.0 * 60.0),
                "30 Mins" => 30.0 / (24.0 * 60.0),
                "1 Hour" => 1.0 / 24.0,
                "2 Hours" => 2.0 / 24.0,
                "4 Hours" => 4.0 / 24.0,
                "8 Hours" => 8.0 / 24.0,
                "12 Hours" => 12.0 / 24.0,
                "24 Hours" => 1.0,
                "Entire Log" => totalRange,
                _ => 2.0 / 24.0
            };
        }
        else
        {
            // Depth / index span units
            targetSpan = preset switch
            {
                "15 Mins" => totalRange * 0.05,
                "30 Mins" => totalRange * 0.10,
                "1 Hour" => totalRange * 0.15,
                "2 Hours" => totalRange * 0.25,
                "4 Hours" => totalRange * 0.40,
                "8 Hours" => totalRange * 0.60,
                "12 Hours" => totalRange * 0.80,
                "24 Hours" => totalRange,
                "Entire Log" => totalRange,
                _ => totalRange * 0.25
            };
        }

        if (targetSpan > totalRange) targetSpan = totalRange;

        double newEnd = SelectionStart + targetSpan;
        double newStart = SelectionStart;

        if (newEnd > Maximum)
        {
            newEnd = Maximum;
            newStart = Math.Max(Minimum, Maximum - targetSpan);
        }

        _isInternalUpdate = true;
        try
        {
            SelectionStart = newStart;
            SelectionEnd = newEnd;
        }
        finally
        {
            _isInternalUpdate = false;
        }

        UpdateVisualLayout();
        NotifyRangeChanged();
    }

    #endregion
}

