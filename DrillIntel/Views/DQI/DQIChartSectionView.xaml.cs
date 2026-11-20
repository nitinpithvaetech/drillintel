using System.Windows;
using System.Windows.Controls;
using DrillIntel.Services;
using DrillIntel.ViewModels.DQI;

namespace DrillIntel.Views.DQI
{
    public partial class DQIChartSectionView : UserControl
    {
        public DQIChartSectionView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ChartContentGrid != null && ChartContentGrid.IsVisible)
            {
                ConsistencyChart?.Invalidate();
                FrequencyChart?.Invalidate();
                AccuracyChart?.Invalidate();
            }
        }

        private void ChartContentGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ChartContentGrid != null && ChartContentGrid.IsVisible)
            {
                RenderTracks();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DQITeeChartRenderer.SetupTrackChart(ConsistencyChart);
            DQITeeChartRenderer.SetupTrackChart(FrequencyChart);
            DQITeeChartRenderer.SetupTrackChart(AccuracyChart);

            DQITeeChartRenderer.SynchronizeAxes(ConsistencyChart, FrequencyChart, AccuracyChart);
            DQITeeChartRenderer.SynchronizeAxes(FrequencyChart, ConsistencyChart, AccuracyChart);
            DQITeeChartRenderer.SynchronizeAxes(AccuracyChart, ConsistencyChart, FrequencyChart);

            RenderTracks();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DQIChartSectionViewModel oldVm)
            {
                oldVm.ChartDataLoaded -= RenderTracks;
            }

            if (e.NewValue is DQIChartSectionViewModel newVm)
            {
                newVm.ChartDataLoaded += RenderTracks;
                RenderTracks();
            }
        }

        private void RenderTracks()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RenderTracks);
                return;
            }

            if (DataContext is not DQIChartSectionViewModel vm) return;
            if (ConsistencyChart == null || FrequencyChart == null || AccuracyChart == null) return;

            var chartType = vm.IsAreaChart ? DQIChartType.Area : DQIChartType.Line;

            // Track 1: Consistency
            DQITeeChartRenderer.RenderSeries(
                ConsistencyChart,
                vm.CurrentPoints,
                pt => pt.ConsIdx,
                DQITeeChartRenderer.ConsistencyColor,
                chartType);

            // Track 2: Frequency
            DQITeeChartRenderer.RenderSeries(
                FrequencyChart,
                vm.CurrentPoints,
                pt => pt.FreqIdx,
                DQITeeChartRenderer.FrequencyColor,
                chartType);

            // Track 3: Accuracy
            DQITeeChartRenderer.RenderSeries(
                AccuracyChart,
                vm.CurrentPoints,
                pt => pt.AccrIdx,
                DQITeeChartRenderer.AccuracyColor,
                chartType);
        }
    }
}

