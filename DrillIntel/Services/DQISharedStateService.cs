using System;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Data.Objects.DataObjects.DQI;

namespace DrillIntel.Services
{
    public class DQISharedStateService : ObservableObject, IDQISharedStateService
    {
        private string? _selectedWellId;
        private string? _selectedWellName;
        private DQIChannel? _selectedChannel;
        private DQIChartType _chartType = DQIChartType.Line;
        private bool _isConfigOpen;
        private WellAverageIndices? _wellAvgIndices;
        private DQIData? _currentDQIData;

        public event EventHandler? WellChanged;
        public event EventHandler? ChannelChanged;
        public event EventHandler? ChartTypeChanged;
        public event EventHandler? ConfigOpenChanged;
        public event EventHandler? DataRefreshed;
        public event EventHandler? ConfigurationSaved;

        public string? SelectedWellId
        {
            get => _selectedWellId;
            set
            {
                if (SetProperty(ref _selectedWellId, value))
                {
                    WellChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string? SelectedWellName
        {
            get => _selectedWellName;
            set => SetProperty(ref _selectedWellName, value);
        }

        public DQIChannel? SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                if (SetProperty(ref _selectedChannel, value))
                {
                    ChannelChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public DQIChartType ChartType
        {
            get => _chartType;
            set
            {
                if (SetProperty(ref _chartType, value))
                {
                    ChartTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsConfigOpen
        {
            get => _isConfigOpen;
            set
            {
                if (SetProperty(ref _isConfigOpen, value))
                {
                    ConfigOpenChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public WellAverageIndices? WellAvgIndices
        {
            get => _wellAvgIndices;
            set => SetProperty(ref _wellAvgIndices, value);
        }

        public DQIData? CurrentDQIData
        {
            get => _currentDQIData;
            set => SetProperty(ref _currentDQIData, value);
        }

        public void NotifyDataRefreshed() => DataRefreshed?.Invoke(this, EventArgs.Empty);

        public void NotifyConfigurationSaved() => ConfigurationSaved?.Invoke(this, EventArgs.Empty);
    }
}

