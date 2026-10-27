using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DrillIntel.Services.Charting;
using DrillIntel.ViewModels.Broomstick;

namespace DrillIntel.Views.Broomstick;

public partial class BroomstickDocumentView : UserControl
{
    private BroomstickChartRenderer? _renderer;

    public BroomstickDocumentViewModel? ViewModel => DataContext as BroomstickDocumentViewModel;

    public BroomstickDocumentView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;

        ChartControl.MouseMove += OnChartMouseMove;
        ChartControl.MouseLeave += OnChartMouseLeave;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeRenderer();
        TriggerRender();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        InitializeRenderer();
        TriggerRender();
    }

    private void InitializeRenderer()
    {
        if (ChartControl != null && _renderer == null)
        {
            _renderer = new BroomstickChartRenderer(ChartControl);
        }

        if (ViewModel != null)
        {
            ViewModel.ChartNeedsRepaint -= TriggerRender;
            ViewModel.ChartNeedsRepaint += TriggerRender;
        }
    }

    private void TriggerRender()
    {
        if (ViewModel != null && _renderer != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                double minD = ViewModel.IsTimeLog
                    ? (ViewModel.FilteredPoints.Count > 0 ? ViewModel.FilteredPoints.Min(p => p.Depth) : 0)
                    : ViewModel.TrackBarSelectionStart;
                double maxD = ViewModel.IsTimeLog
                    ? (ViewModel.FilteredPoints.Count > 0 ? ViewModel.FilteredPoints.Max(p => p.Depth) : 0)
                    : ViewModel.TrackBarSelectionEnd;

                _renderer.RenderDocument(
                    ViewModel.Document,
                    ViewModel.FilteredPoints,
                    minD,
                    maxD);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnRangeTrackBarChanged(object sender, RoutedEventArgs e)
    {
        ViewModel?.RangeTrackBarChangedCommand.Execute(null);
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_renderer == null || ViewModel == null) return;

        var pos = e.GetPosition(ChartControl);
        var info = _renderer.GetHoverInfo(pos, ViewModel.FilteredPoints);
        ViewModel.UpdateHoverTelemetry(info);

        if (info != null)
        {
            Canvas.SetLeft(SeriesTooltipCard, Math.Min(pos.X + 15, ActualWidth - 200));
            Canvas.SetTop(SeriesTooltipCard, Math.Max(10, pos.Y - 40));
        }
    }

    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        ViewModel?.UpdateHoverTelemetry(null);
    }
}

