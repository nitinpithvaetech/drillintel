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
                double minD = 0;
                double maxD = 0;

                if (ViewModel.FilteredPoints.Count > 0)
                {
                    minD = ViewModel.FilteredPoints.Min(p => p.Depth);
                    maxD = ViewModel.FilteredPoints.Max(p => p.Depth);
                }
                else if (ViewModel.Document.BroomstickPlanData != null)
                {
                    var p = ViewModel.Document.BroomstickPlanData;
                    var depths = new List<double>();
                    if (p.pickup?.Count > 0) depths.AddRange(p.pickup.Values.Select(x => x.Depth));
                    if (p.slackoff?.Count > 0) depths.AddRange(p.slackoff.Values.Select(x => x.Depth));
                    if (p.rotate?.Count > 0) depths.AddRange(p.rotate.Values.Select(x => x.Depth));
                    if (depths.Count > 0)
                    {
                        minD = depths.Min();
                        maxD = depths.Max();
                    }
                }

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

    private void OnDataSelectorClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.OpenDataSelector();
        }
    }

    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}

