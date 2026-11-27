using System.Drawing;
using System.Globalization;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.ViewModels;
using Steema.TeeChart.Styles;
using System.Windows;
using System.Windows.Input;

namespace DrillIntel.Views;

public partial class TripConnectionChartWindow : Window
{
    public TripConnectionChartWindow(TripConnectionChartViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.ChartNeedsRepaint += RenderChart;
        Loaded += async (_, _) => { await vm.InitializeAsync(); RenderChart(); };
    }

    private void RenderChart()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(RenderChart); return; }
        if (DataContext is not TripConnectionChartViewModel vm) return;

        ChartControl.Series.Clear();
        ChartControl.Aspect.View3D = false;
        ChartControl.Panel.Brush.Color = Color.White;
        ChartControl.Walls.Back.Brush.Color = Color.White;
        ChartControl.Axes.Bottom.Labels.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Left.Labels.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Bottom.Title.Text = "Depth (ft)";
        ChartControl.Axes.Bottom.Title.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Left.Title.Text = "Time (min)";
        ChartControl.Axes.Left.Title.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Bottom.Grid.Color = Color.FromArgb(225, 232, 240);
        ChartControl.Axes.Left.Grid.Color = Color.FromArgb(225, 232, 240);
        ChartControl.Legend.Visible = true;
        ChartControl.Legend.Font.Color = Color.FromArgb(30, 41, 59);

        if (vm.RigStateView)
        {
            var states = new Dictionary<int, (string Name, Color Color)>();
            foreach (var item in vm.RigStates.Values)
                states[item.Number] = (item.Name, ParseRigStateColor(item.ColorHex, item.Color));
            foreach (var stateNo in vm.Points.SelectMany(point => point.SlipsToSlipsRigStates.Keys).Distinct())
            {
                if (states.ContainsKey(stateNo)) continue;
                var fallback = DrillIntel.Data.Objects.DataObjects.Services.RigStateService.DefaultRigStateItems
                    .FirstOrDefault(item => item.Number == stateNo);
                states[stateNo] = fallback.Number == stateNo
                    ? (fallback.Name, Color.FromArgb(fallback.Color))
                    : ($"Rig State {stateNo}", Color.FromArgb(255, 96, 125, 139));
            }

            var activeStates = states.Where(state => vm.Points.Any(point =>
                    point.SlipsToSlipsRigStates.TryGetValue(state.Key, out double duration) && duration > 0))
                .OrderBy(state => state.Key).ToList();
            if (activeStates.Count > 0)
            {
                foreach (var state in activeStates)
                {
                    var stateBars = new Bar
                    {
                        Title = state.Value.Name,
                        Color = state.Value.Color,
                        MultiBar = MultiBars.Stacked
                    };
                    stateBars.Marks.Visible = false;
                    foreach (var point in vm.Points)
                    {
                        double minutes = point.SlipsToSlipsRigStates.TryGetValue(state.Key, out double duration)
                            ? duration / 60d
                            : 0d;
                        stateBars.Add(point.Depth, minutes);
                    }
                    ChartControl.Series.Add(stateBars);
                }
            }
            else
            {
                AddTripTimeBars(vm, useShiftColors: false);
            }
        }
        else
        {
            AddTripTimeBars(vm, useShiftColors: true);
        }

        if (vm.Points.Count > 0)
        {
            var average = new Line
            {
                Title = "Avg. Trip-In Time",
                Color = Color.FromArgb(255, 87, 34),
                LinePen = { Width = 2 }
            };
            average.Add(vm.Points.Min(point => point.Depth), vm.AverageTime);
            average.Add(vm.Points.Max(point => point.Depth), vm.AverageTime);
            average.Marks.Visible = false;
            ChartControl.Series.Add(average);
        }
    }

    private void AddTripTimeBars(TripConnectionChartViewModel vm, bool useShiftColors)
    {
        var main = new Bar { Title = "Trip-In Time", Color = Color.FromArgb(96, 125, 139), MultiBar = MultiBars.Side, ColorEach = useShiftColors };
        main.Marks.Visible = false;
        foreach (var point in vm.Points)
        {
            Color color = useShiftColors && point.DayNightStatus == ConnectionLogPoint.cnDayNightTime.DayTime
                ? Color.FromArgb(255, 165, 0)
                : Color.FromArgb(96, 125, 139);
            main.Add(point.Depth, point.SlipsToSlipsSeconds / 60d, color);
        }
        ChartControl.Series.Add(main);
    }

    private static Color ParseRigStateColor(string? colorHex, double color)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(colorHex))
                return ColorTranslator.FromHtml(colorHex);
            Color parsed = Color.FromArgb(unchecked((int)color));
            return parsed.A == 0 ? Color.FromArgb(255, parsed.R, parsed.G, parsed.B) : parsed;
        }
        catch
        {
            return Color.FromArgb(255, 96, 125, 139);
        }
    }

    private async void OnPresetChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is TripConnectionChartViewModel vm && sender is System.Windows.Controls.ComboBox { SelectedItem: string preset })
        {
            await vm.ApplyPresetAsync(preset);
            RenderChart();
        }
    }

    private async void OnRangeChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TripConnectionChartViewModel vm)
        {
            await vm.RangeTrackBarChangedAsync();
            RenderChart();
        }
    }

    private async void OnTripChartSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TripConnectionChartViewModel vm) return;
        var settings = vm.CreateSettingsDraft();
        var dialog = new TripConnectionChartSettingsWindow
        {
            Owner = this,
            DataContext = settings
        };
        if (dialog.ShowDialog() != true) return;

        if (!vm.SaveSettings(settings, out string error))
        {
            MessageBox.Show(this, error, "Trip-In Chart Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await vm.ReloadAsync();
        RenderChart();
    }

    private async void OnDataSelectorSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TripConnectionChartViewModel vm) return;
        var selectorConsole = await vm.CreateDataSelectorConsoleAsync();
        var selectorVm = new DataSelectorViewModel(selectorConsole);
        if (selectorVm.AvailableYCoordinates.Contains(vm.OverviewMnemonic))
            selectorVm.YCoordinate = vm.OverviewMnemonic;
        selectorVm.ColorCodeAsRigState = vm.ColorCodeOverviewAsRigState;
        selectorVm.IsYAxisInverted = vm.IsOverviewYAxisInverted;

        var dialog = new DataSelectorWindow(selectorVm) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            vm.SetOverviewChannel(selectorVm.SelectedChannelMnemonic);
            vm.ColorCodeOverviewAsRigState = selectorVm.ColorCodeAsRigState;
            vm.IsOverviewYAxisInverted = selectorVm.IsYAxisInverted;
            await vm.ReloadOverviewAsync();
        }
    }

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not TripConnectionChartViewModel vm) return;
        var mouse = e.GetPosition(ChartControl);
        foreach (var bar in ChartControl.Series.OfType<Bar>())
        {
            int index;
            try { index = bar.Clicked((int)mouse.X, (int)mouse.Y); }
            catch { index = -1; }
            if (index < 0 || index >= bar.Count) continue;
            double depth = bar.XValues[index];
            var point = vm.Points.FirstOrDefault(item => Math.Abs(item.Depth - depth) < 0.01);
            if (point == null) continue;
            string timeDetails = vm.RigStateView
                ? $"{bar.Title} Time (min): {bar.YValues[index].ToString("F2", CultureInfo.InvariantCulture)}"
                : $"Trip-In Time (min): {point.SlipsToSlipsSeconds / 60d:F2}\nShift: {(point.DayNightStatus == ConnectionLogPoint.cnDayNightTime.DayTime ? "Day" : "Night")}";
            BarTooltipText.Text = $"Depth: {depth:F2}\n{timeDetails}" +
                                  $"\nFrom: {point.FromDate:dd-MM-yyyy HH:mm}\nTo: {point.ToDate:dd-MM-yyyy HH:mm}";
            BarTooltip.Margin = new Thickness(mouse.X + 14, mouse.Y + 14, 0, 0);
            BarTooltip.Visibility = Visibility.Visible;
            return;
        }
        BarTooltip.Visibility = Visibility.Collapsed;
    }

    private void OnChartMouseLeave(object sender, MouseEventArgs e) => BarTooltip.Visibility = Visibility.Collapsed;
}
