using System.Drawing;
using System.Globalization;
using System.Windows;
using DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;
using DrillIntel.ViewModels;
using Steema.TeeChart.Styles;

namespace DrillIntel.Views;

public partial class DrillingStandChartWindow : Window
{
    public DrillingStandChartWindow(DrillingStandChartViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.ChartNeedsRepaint += RenderChart;
        Loaded += async (_, _) => { await vm.InitializeAsync(); RenderChart(); };
    }

    private void RenderChart()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RenderChart);
            return;
        }
        if (DataContext is not DrillingStandChartViewModel vm) return;
        ChartControl.Series.Clear();
        ChartControl.Aspect.View3D = false;
        ChartControl.Panel.Brush.Color = Color.White;
        ChartControl.Walls.Back.Brush.Color = Color.White;
        ChartControl.Axes.Bottom.Labels.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Left.Labels.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Bottom.Title.Text = "Depth (ft)";
        ChartControl.Axes.Bottom.Title.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Left.Title.Text = "Time (m)";
        ChartControl.Axes.Left.Title.Font.Color = Color.FromArgb(30, 41, 59);
        ChartControl.Axes.Bottom.Grid.Color = Color.FromArgb(225, 232, 240);
        ChartControl.Axes.Left.Grid.Color = Color.FromArgb(225, 232, 240);
        ChartControl.Legend.Visible = true;
        ChartControl.Legend.Font.Color = Color.FromArgb(30, 41, 59);

        if (vm.RigStateView)
        {
            var stateNames = new Dictionary<double, (string Name, Color Color)>();
            foreach (System.Data.DataRow r in vm.Data.rigStates.Rows)
            {
                if (double.TryParse(Convert.ToString(r["RIG_STATE"], CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out double st))
                {
                    string name = Convert.ToString(r["RIG_STATE_NAME"]) ?? $"State {st}";
                    Color color = ParseColor(Convert.ToString(r["RIG_STATE_COLOR"]));
                    stateNames[st] = (name, color);
                }
            }

            var statesByDepth = new Dictionary<double, Dictionary<double, double>>();
            var defaultStates = DrillIntel.Data.Objects.DataObjects.Services.RigStateService.DefaultRigStateItems
                .ToDictionary(s => (double)s.Number, s => (Name: s.Name, Color: Color.FromArgb(s.Color)));

            foreach (System.Data.DataRow row in vm.Data.rigStateData.Rows)
            {
                double depth = Math.Round(Convert.ToDouble(row["DEPTH"], CultureInfo.InvariantCulture), 2);
                var values = new Dictionary<double, double>();
                string encoded = Convert.ToString(row["RIG_STATES"]) ?? "";
                foreach (string pair in encoded.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = pair.Split('#');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double state) &&
                        double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double mins))
                    {
                        values[state] = mins;
                        if (!stateNames.ContainsKey(state))
                        {
                            if (defaultStates.TryGetValue(state, out var fallback))
                            {
                                Color c = fallback.Color.A == 0 ? Color.FromArgb(255, fallback.Color.R, fallback.Color.G, fallback.Color.B) : fallback.Color;
                                stateNames[state] = (fallback.Name, c);
                            }
                            else
                            {
                                stateNames[state] = ($"Rig State {state:G}", Color.FromArgb(128, 96, 125, 139));
                            }
                        }
                    }
                }
                statesByDepth[depth] = values;
            }

            var visibleDepths = vm.Data.standData.Rows.Cast<System.Data.DataRow>()
                .Select(r => Math.Round(Convert.ToDouble(r["Depth"], CultureInfo.InvariantCulture), 2))
                .Where(d => IsSelected(vm, d))
                .ToList();

            var activeStates = new List<KeyValuePair<double, (string Name, Color Color)>>();
            foreach (var state in stateNames)
            {
                double totalStateMins = 0;
                foreach (var d in visibleDepths)
                {
                    if (statesByDepth.TryGetValue(d, out var parts) && parts.TryGetValue(state.Key, out double m))
                        totalStateMins += m;
                }
                if (totalStateMins > 0)
                {
                    activeStates.Add(state);
                }
            }

            if (activeStates.Count == 0 && stateNames.Count > 0)
            {
                activeStates.AddRange(stateNames);
            }

            foreach (var state in activeStates)
            {
                var bar = new Bar { Title = state.Value.Name, Color = state.Value.Color, MultiBar = MultiBars.Stacked };
                bar.Marks.Visible = false;
                foreach (var depth in visibleDepths)
                {
                    statesByDepth.TryGetValue(depth, out var parts);
                    double val = parts != null && parts.TryGetValue(state.Key, out double m) ? m : 0;
                    bar.Add(depth, val);
                }
                ChartControl.Series.Add(bar);
            }
        }
        else
        {
            var main = new Bar { Title = "Main", Color = Color.FromArgb(255, 165, 0), MultiBar = MultiBars.Side };
            var offset = new Bar { Title = "Offset", Color = Color.FromArgb(96, 125, 139), MultiBar = MultiBars.Side };
            main.ColorEach = true;
            main.Marks.Visible = false;
            offset.Marks.Visible = false;
            foreach (System.Data.DataRow row in vm.Data.standData.Rows)
            {
                double depth = Convert.ToDouble(row["Depth"]); if (!IsSelected(vm, depth)) continue;
                var shiftColor = Convert.ToString(row["DayNight"]) == "N"
                    ? Color.FromArgb(96, 125, 139)
                    : Color.FromArgb(255, 165, 0);
                main.Add(depth, Convert.ToDouble(row["Time"]), shiftColor);
                offset.Add(depth, Convert.ToDouble(row["OffsetTime"]));
            }
            ChartControl.Series.Add(main); ChartControl.Series.Add(offset);
        }
        var avg = new Line { Title = "Avg. Drilling Stand Time", Color = Color.FromArgb(255, 87, 34), LinePen = { Width = 2 } };
        avg.Add(vm.Depths.Count == 0 ? 0 : vm.Depths.First(), vm.AverageTime);
        avg.Add(vm.Depths.Count == 0 ? 1 : vm.Depths.Last(), vm.AverageTime);
        avg.Marks.Visible = false;
        ChartControl.Series.Add(avg);
    }

    private static bool IsSelected(DrillingStandChartViewModel vm, double depth)
    {
        return true;
    }

    private static Color ParseColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return Color.Gray;
        try
        {
            var c = ColorTranslator.FromHtml(color);
            if (c.A == 0) return Color.FromArgb(255, c.R, c.G, c.B);
            return c;
        }
        catch
        {
            return Color.Gray;
        }
    }

    private void OnDataSelectorSettingsClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DrillingStandChartViewModel vm) return;
        var selectorVm = new DataSelectorViewModel(vm.CreateDataSelectorConsole());
        var dialog = new DataSelectorWindow(selectorVm) { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(selectorVm.SelectedChannelMnemonic))
            vm.SetOverviewChannel(selectorVm.SelectedChannelMnemonic);
    }

    private async void OnPresetPeriodChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is DrillingStandChartViewModel vm && sender is System.Windows.Controls.ComboBox combo && combo.SelectedItem is string preset)
        {
            await vm.ApplyPresetAsync(preset);
            RenderChart();
        }
    }

    private async void OnSelectorRangeChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is DrillingStandChartViewModel vm)
        {
            await vm.RangeTrackBarChangedAsync();
            RenderChart();
        }
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (DataContext is not DrillingStandChartViewModel vm) return;
        var mouse = e.GetPosition(ChartControl);
        foreach (var bar in ChartControl.Series.OfType<Bar>())
        {
            int pointIndex;
            try { pointIndex = bar.Clicked((int)mouse.X, (int)mouse.Y); }
            catch { pointIndex = -1; }
            if (pointIndex < 0 || pointIndex >= bar.Count) continue;

            double depth = bar.XValues[pointIndex];
            var row = vm.Data.standData.Rows.Cast<System.Data.DataRow>()
                .FirstOrDefault(r => Math.Abs(Convert.ToDouble(r["Depth"]) - depth) < 0.01);
            if (row == null) continue;
            double minutes = bar.YValues[pointIndex];
            string detail = $"{bar.Title}\nDepth: {depth:F2}\nTime (m): {minutes:F2}";
            if (!vm.RigStateView && bar.Title == "Main")
                detail += $"\nShift: {(Convert.ToString(row["DayNight"]) == "D" ? "Day" : "Night")}";

            if (row.Table.Columns.Contains("FromDate") && row["FromDate"] is DateTime fromDt &&
                row.Table.Columns.Contains("ToDate") && row["ToDate"] is DateTime toDt &&
                fromDt > DateTime.MinValue && toDt > DateTime.MinValue)
            {
                detail += $"\nFrom: {fromDt:dd-MM-yyyy HH:mm}\nTo: {toDt:dd-MM-yyyy HH:mm}";
            }

            BarTooltipText.Text = detail;
            BarTooltip.Margin = new Thickness(mouse.X + 14, mouse.Y + 14, 0, 0);
            BarTooltip.Visibility = Visibility.Visible;
            return;
        }
        BarTooltip.Visibility = Visibility.Collapsed;
    }

    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => BarTooltip.Visibility = Visibility.Collapsed;
}
