using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class DQIChartSectionViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;

        [ObservableProperty]
        private bool _isChartVisible;

        [ObservableProperty]
        private bool _isEmptyStateVisible = true;

        [ObservableProperty]
        private string _emptyStateMessage = "Select a well and channel to view DQI metrics and historical tracks.";

        [ObservableProperty]
        private string _channelName = string.Empty;

        [ObservableProperty]
        private string _mnemonic = string.Empty;

        [ObservableProperty]
        private double _overallChannelDQI;

        [ObservableProperty]
        private double _consAvg;

        [ObservableProperty]
        private double _freqAvg;

        [ObservableProperty]
        private double _accrAvg;

        [ObservableProperty]
        private bool _isLineChart = true;

        [ObservableProperty]
        private bool _isAreaChart = false;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        partial void OnErrorMessageChanged(string value)
        {
            HasError = !string.IsNullOrWhiteSpace(value);
        }

        private readonly HashSet<string> _autoProcessedWells = new(StringComparer.OrdinalIgnoreCase);

        public List<ChannelDataPoint> CurrentPoints { get; private set; } = new();

        public event Action? ChartDataLoaded;

        public DQIChartSectionViewModel(IDQIService dqiService, IDQISharedStateService sharedState)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));

            _sharedState.WellChanged += async (_, _) => await HandleSelectionChangedAsync();
            _sharedState.ChannelChanged += async (_, _) => await HandleSelectionChangedAsync();
            _sharedState.DataRefreshed += async (_, _) => await HandleSelectionChangedAsync();
        }

        private async Task HandleSelectionChangedAsync()
        {
            var wellId = _sharedState.SelectedWellId;
            var channel = _sharedState.SelectedChannel;

            if (string.IsNullOrWhiteSpace(wellId) || channel == null)
            {
                IsChartVisible = false;
                IsEmptyStateVisible = true;
                ChannelName = string.Empty;
                Mnemonic = string.Empty;
                CurrentPoints.Clear();
                ChartDataLoaded?.Invoke();
                return;
            }

            IsChartVisible = true;
            IsEmptyStateVisible = false;
            ChannelName = channel.Name;
            Mnemonic = channel.Mnemonic;

            await LoadChartDataAsync();
        }

        public async Task LoadChartDataAsync()
        {
            var wellId = _sharedState.SelectedWellId;
            var channel = _sharedState.SelectedChannel;
            if (string.IsNullOrWhiteSpace(wellId) || channel == null) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var data = await _dqiService.LoadDQIDataAsync(wellId, channel.Mnemonic);
                _sharedState.CurrentDQIData = data;

                if (!string.IsNullOrEmpty(data.ChartDataError))
                {
                    ErrorMessage = data.ChartDataError;
                }

                OverallChannelDQI = data.ChannelDQI.OverAllChannelDQI;
                ConsAvg = data.ChannelDQI.ConsIdxAvg;
                FreqAvg = data.ChannelDQI.FreqIdxAvg;
                AccrAvg = data.ChannelDQI.AccrIdxAvg;

                CurrentPoints = data.ChannelDQI.Points ?? new List<ChannelDataPoint>();

                if (CurrentPoints.Count == 0 && !_autoProcessedWells.Contains(wellId))
                {
                    _autoProcessedWells.Add(wellId);
                    await ProcessDQIAsync();
                    return;
                }

                ChartDataLoaded?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading DQI chart data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void SetLineChart()
        {
            IsLineChart = true;
            IsAreaChart = false;
            _sharedState.ChartType = DQIChartType.Line;
            ChartDataLoaded?.Invoke();
        }

        [RelayCommand]
        public void SetAreaChart()
        {
            IsLineChart = false;
            IsAreaChart = true;
            _sharedState.ChartType = DQIChartType.Area;
            ChartDataLoaded?.Invoke();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            var wellId = _sharedState.SelectedWellId;
            if (!string.IsNullOrWhiteSpace(wellId))
            {
                _autoProcessedWells.Remove(wellId);
            }
            await LoadChartDataAsync();
            _sharedState.NotifyDataRefreshed();
        }

        [RelayCommand]
        public async Task ProcessDQIAsync()
        {
            var wellId = _sharedState.SelectedWellId;
            if (string.IsNullOrWhiteSpace(wellId)) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Processing DQI metrics...";
                var progress = new Progress<(double Percent, string Message)>(p =>
                {
                    ProgressPercent = p.Percent;
                    StatusMessage = p.Message;
                });

                bool success = await _dqiService.ProcessWellDQIAsync(wellId, progress, CancellationToken.None);
                if (success)
                {
                    await LoadChartDataAsync();
                    _sharedState.NotifyDataRefreshed();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error calculating DQI: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                StatusMessage = string.Empty;
                ProgressPercent = 0;
            }
        }
    }
}

