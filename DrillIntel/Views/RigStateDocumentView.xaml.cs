using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DrillIntel.Services.Charting;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class RigStateDocumentView : UserControl
{
    private TChartTrackConsoleRenderer? _renderer;

    public RigStateDocumentView()
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

        if (DataContext is RigStateDocumentViewModel vm && _renderer != null && vm.CurrentDataResult != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _renderer.RenderConsole(vm.ConsoleModel, vm.CurrentDataResult, vm.CurrentRigStateIntervals);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        InitializeRenderer();
    }

    private void InitializeRenderer()
    {
        if (ChartControl != null && _renderer == null)
        {
            _renderer = new TChartTrackConsoleRenderer(ChartControl);
        }

        if (DataContext is RigStateDocumentViewModel vm && _renderer != null)
        {
            vm.ChartNeedsRepaint -= OnChartNeedsRepaint;
            vm.ChartNeedsRepaint += OnChartNeedsRepaint;

            // Trigger initial render if data already loaded
            if (vm.CurrentDataResult != null)
            {
                _renderer.RenderConsole(vm.ConsoleModel, vm.CurrentDataResult, vm.CurrentRigStateIntervals);
            }
        }
    }

    private void OnChartNeedsRepaint()
    {
        if (DataContext is RigStateDocumentViewModel vm && _renderer != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _renderer.RenderConsole(vm.ConsoleModel, vm.CurrentDataResult, vm.CurrentRigStateIntervals);
            });
        }
    }

    private void OnRangeTrackBarChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is RigStateDocumentViewModel vm)
        {
            _ = vm.RangeTrackBarChangedCommand.ExecuteAsync(null);
        }
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_renderer == null) return;

        var mousePos = e.GetPosition(ChartControl);
        var vm = DataContext as RigStateDocumentViewModel;
        string? wellName = vm?.WellName;
        if (string.IsNullOrWhiteSpace(wellName)) wellName = vm?.LogName;

        var allChannelsInfo = _renderer.GetAllChannelsHoverInfo(mousePos, wellName);

        if (allChannelsInfo != null && allChannelsInfo.Channels.Count > 0)
        {
            if (vm != null)
            {
                vm.UpdateCursorTelemetry(allChannelsInfo.FormattedIndex, allChannelsInfo.RigStateName, allChannelsInfo.RigStateColorHex);
            }

            // Populate multi-channel tooltip matching reference image
            TooltipWellName.Text = allChannelsInfo.WellName;
            TooltipIndexText.Text = allChannelsInfo.IndexDateTime.HasValue
                ? $"Date Time: {allChannelsInfo.FormattedIndex}"
                : $"Depth: {allChannelsInfo.FormattedIndex}";

            if (!string.IsNullOrWhiteSpace(allChannelsInfo.RigStateName) && allChannelsInfo.RigStateName != "None")
            {
                TooltipRigStateText.Text = allChannelsInfo.RigStateName;
                try
                {
                    var rsBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(allChannelsInfo.RigStateColorHex ?? "#4CAF50")!;
                    TooltipRigStateBullet.Fill = rsBrush;
                    TooltipRigStateText.Foreground = rsBrush;
                }
                catch
                {
                    TooltipRigStateBullet.Fill = System.Windows.Media.Brushes.ForestGreen;
                    TooltipRigStateText.Foreground = System.Windows.Media.Brushes.ForestGreen;
                }
                TooltipRigStateRow.Visibility = Visibility.Visible;
            }
            else
            {
                TooltipRigStateRow.Visibility = Visibility.Collapsed;
            }

            TooltipChannelsList.ItemsSource = allChannelsInfo.Channels;

            // Position Tooltip dynamically near mouse cursor or curve anchor
            double containerWidth = ChartContainer.ActualWidth;
            double containerHeight = ChartContainer.ActualHeight;

            SeriesTooltipCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tipWidth = SeriesTooltipCard.DesiredSize.Width > 0 ? SeriesTooltipCard.DesiredSize.Width : 190;
            double tipHeight = SeriesTooltipCard.DesiredSize.Height > 0 ? SeriesTooltipCard.DesiredSize.Height : 120;

            double anchorX = mousePos.X;
            double anchorY = mousePos.Y;

            double left = anchorX + 16;
            double top = anchorY - (tipHeight / 2);

            // Flip horizontally if extending beyond right edge
            if (left + tipWidth > containerWidth - 12)
            {
                left = anchorX - tipWidth - 16;
            }

            // Flip vertically if extending beyond bottom edge
            if (top + tipHeight > containerHeight - 12)
            {
                top = containerHeight - tipHeight - 12;
            }

            left = Math.Max(8, left);
            top = Math.Max(8, top);

            Canvas.SetLeft(SeriesTooltipCard, left);
            Canvas.SetTop(SeriesTooltipCard, top);
            SeriesTooltipCard.Visibility = Visibility.Visible;
        }
        else
        {
            SeriesTooltipCard.Visibility = Visibility.Collapsed;
            if (vm != null)
            {
                var telemetry = _renderer.GetCursorTelemetry(mousePos);
                if (telemetry.HasValue)
                {
                    vm.UpdateCursorTelemetry(telemetry.Value.FormattedIndex, telemetry.Value.RigStateName, telemetry.Value.RigStateColorHex);
                }
            }
        }
    }

    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        SeriesTooltipCard.Visibility = Visibility.Collapsed;
        if (DataContext is RigStateDocumentViewModel vm)
        {
            vm.ResetCursorTelemetry();
        }
    }
}

