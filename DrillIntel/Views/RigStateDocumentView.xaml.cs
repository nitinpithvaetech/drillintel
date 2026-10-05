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
        ChartControl.SizeChanged -= OnChartSizeChanged;
        ChartControl.SizeChanged += OnChartSizeChanged;

        if (DataContext is RigStateDocumentViewModel vm && _renderer != null && vm.CurrentDataResult != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _renderer.RenderConsole(vm.ConsoleModel, vm.CurrentDataResult, vm.CurrentRigStateIntervals);
                UpdateTrackLegends();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else
        {
            UpdateTrackLegends();
        }
    }

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTrackLegends();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        InitializeRenderer();
        UpdateTrackLegends();
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
            UpdateTrackLegends();
        }
    }

    private void OnChartNeedsRepaint()
    {
        if (DataContext is RigStateDocumentViewModel vm && _renderer != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _renderer.RenderConsole(vm.ConsoleModel, vm.CurrentDataResult, vm.CurrentRigStateIntervals);
                UpdateTrackLegends();
            });
        }
    }

    private void UpdateTrackLegends()
    {
        if (DataContext is not RigStateDocumentViewModel vm || TrackLegendGrid == null)
            return;

        TrackLegendGrid.Children.Clear();
        TrackLegendGrid.ColumnDefinitions.Clear();

        var visibleTracks = vm.ConsoleModel?.Tracks
            .Where(t => t.Visible)
            .OrderBy(t => t.DisplayOrder)
            .ToList();

        if (visibleTracks == null || visibleTracks.Count == 0)
            return;

        int currentCol = 0;
        bool isVertical = vm.ConsoleModel?.TrackOrientation == Models.TChart.enumTrackOrientation.Vertical;

        // 1. Left axis spacer (aligned with Time / Depth axis on the left of TeeChart)
        if (isVertical)
        {
            double leftOffset = _renderer != null ? _renderer.LeftAxisOffset : 70;
            TrackLegendGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(Math.Max(50, leftOffset), GridUnitType.Pixel)
            });

            var axisBadge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 5, 4, 5),
                Margin = new Thickness(2, 1, 2, 1),
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var sp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var icon = new MaterialDesignThemes.Wpf.PackIcon
            {
                Kind = vm.IsTimeLog
                    ? MaterialDesignThemes.Wpf.PackIconKind.ClockOutline
                    : MaterialDesignThemes.Wpf.PackIconKind.Ruler,
                Width = 14,
                Height = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var tb = new TextBlock
            {
                Text = vm.IsTimeLog ? "TIME" : "DEPTH",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#475569")),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            sp.Children.Add(icon);
            sp.Children.Add(tb);
            axisBadge.Child = sp;

            TrackLegendGrid.Children.Add(axisBadge);
            Grid.SetColumn(axisBadge, currentCol++);
        }

        // 2. Track columns (proportionally weighted matching TeeChart track widths)
        var template = FindResource("TrackLegendCardTemplate") as DataTemplate;

        foreach (var track in visibleTracks)
        {
            double weight = Math.Max(0.1, track.Width);
            TrackLegendGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(weight, GridUnitType.Star)
            });

            var card = new ContentControl
            {
                Content = track,
                ContentTemplate = template,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            TrackLegendGrid.Children.Add(card);
            Grid.SetColumn(card, currentCol++);
        }

        // 3. Right margin spacer (matching TeeChart panel right margin)
        if (isVertical)
        {
            double rightOffset = _renderer != null ? _renderer.RightAxisOffset : 12;
            if (rightOffset > 0)
            {
                TrackLegendGrid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(rightOffset, GridUnitType.Pixel)
                });
            }
        }
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_renderer == null) return;

        var mousePos = e.GetPosition(ChartControl);
        var hit = _renderer.HitTestSeries(mousePos, tolerancePixels: 18.0);
        var telemetry = _renderer.GetCursorTelemetry(mousePos);

        if (DataContext is RigStateDocumentViewModel vm)
        {
            if (telemetry.HasValue)
            {
                vm.UpdateCursorTelemetry(telemetry.Value.FormattedIndex, telemetry.Value.RigStateName, telemetry.Value.RigStateColorHex);
            }
            else if (hit != null)
            {
                vm.UpdateCursorTelemetry(hit.FormattedIndex, hit.RigStateName ?? "None", hit.RigStateColorHex ?? "#9E9E9E");
            }
        }

        if (hit != null)
        {
            // Populate Tooltip Content
            TooltipSeriesTitle.Text = hit.ChannelTitle;
            TooltipTrackTitle.Text = hit.TrackTitle;
            TooltipValueText.Text = hit.FormattedValue;
            TooltipUnitText.Text = string.IsNullOrWhiteSpace(hit.Unit) ? string.Empty : $" {hit.Unit}";
            TooltipIndexText.Text = hit.FormattedIndex;
            TooltipIndexIcon.Kind = hit.IndexDateTime.HasValue
                ? MaterialDesignThemes.Wpf.PackIconKind.ClockOutline
                : MaterialDesignThemes.Wpf.PackIconKind.Ruler;

            try
            {
                var brush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hit.LineColorHex)!;
                TooltipSeriesBullet.Fill = brush;
            }
            catch
            {
                TooltipSeriesBullet.Fill = System.Windows.Media.Brushes.DodgerBlue;
            }

            if (!string.IsNullOrWhiteSpace(hit.RigStateName) && hit.RigStateName != "None")
            {
                TooltipRigStateText.Text = hit.RigStateName;
                try
                {
                    var rsBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hit.RigStateColorHex ?? "#4CAF50")!;
                    TooltipRigStateBadge.Background = rsBrush;
                }
                catch
                {
                    TooltipRigStateBadge.Background = System.Windows.Media.Brushes.ForestGreen;
                }
                TooltipRigStateBadge.Visibility = Visibility.Visible;
            }
            else
            {
                TooltipRigStateBadge.Visibility = Visibility.Collapsed;
            }

            // Position Tooltip dynamically near mouse cursor
            double containerWidth = ChartContainer.ActualWidth;
            double containerHeight = ChartContainer.ActualHeight;

            SeriesTooltipCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double tipWidth = SeriesTooltipCard.DesiredSize.Width > 0 ? SeriesTooltipCard.DesiredSize.Width : 180;
            double tipHeight = SeriesTooltipCard.DesiredSize.Height > 0 ? SeriesTooltipCard.DesiredSize.Height : 90;

            double left = mousePos.X + 16;
            double top = mousePos.Y + 16;

            // Flip horizontally if extending beyond right edge
            if (left + tipWidth > containerWidth - 12)
            {
                left = mousePos.X - tipWidth - 16;
            }

            // Flip vertically if extending beyond bottom edge
            if (top + tipHeight > containerHeight - 12)
            {
                top = mousePos.Y - tipHeight - 16;
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

