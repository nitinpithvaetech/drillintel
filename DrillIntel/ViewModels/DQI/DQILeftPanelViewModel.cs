using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class DQIChannelItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _mnemonic = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private bool _isSelected;

        public DQIChannel Channel { get; }

        public DQIChannelItemViewModel(DQIChannel channel)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _mnemonic = channel.Mnemonic;
            _name = channel.Name;
        }
    }

    public partial class DQILeftPanelViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;
        private readonly ProjectSession _session;

        [ObservableProperty]
        private ObservableCollection<DQIWellLookupItem> _wells = new();

        [ObservableProperty]
        private DQIWellLookupItem? _selectedWell;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<DQIChannelItemViewModel> _channels = new();

        private ICollectionView _channelsView;

        [ObservableProperty]
        private DQIChannelItemViewModel? _selectedChannelItem;

        [ObservableProperty]
        private string _channelCountText = "0 channels in setup";

        [ObservableProperty]
        private double _consistencyScore;

        [ObservableProperty]
        private double _frequencyScore;

        [ObservableProperty]
        private double _accuracyScore;

        [ObservableProperty]
        private double _overallWellAvg;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public event Action? RequestOpenSettings;

        public DQILeftPanelViewModel(
            IDQIService dqiService,
            IDQISharedStateService sharedState,
            ProjectSession session)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            _channelsView = CollectionViewSource.GetDefaultView(Channels);
            _channelsView.Filter = FilterChannels;

            _sharedState.ConfigurationSaved += async (_, _) => await LoadChannelsAsync();
            _sharedState.DataRefreshed += async (_, _) => await RefreshWellAveragesAsync();
        }

        public async Task InitializeAsync()
        {
            await LoadWellsAsync();
            await LoadChannelsAsync();
        }

        private async Task LoadWellsAsync()
        {
            try
            {
                IsLoading = true;
                Wells.Clear();

                var liveWells = await _dqiService.GetLiveWellsAsync();
                foreach (var w in liveWells)
                {
                    Wells.Add(w);
                }

                SelectedWell = Wells.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load wells: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadChannelsAsync()
        {
            try
            {
                IsLoading = true;
                Channels.Clear();

                var channels = await _dqiService.GetChannelsAsync();
                foreach (var ch in channels)
                {
                    Channels.Add(new DQIChannelItemViewModel(ch));
                }

                ChannelCountText = $"{Channels.Count} channels in setup";
                _channelsView.Refresh();

                // Restore or select first channel
                var currentMnemonic = _sharedState.SelectedChannel?.Mnemonic;
                var match = Channels.FirstOrDefault(c => c.Mnemonic.Equals(currentMnemonic, StringComparison.OrdinalIgnoreCase))
                            ?? Channels.FirstOrDefault();

                if (match != null)
                {
                    SelectChannel(match);
                }
                else
                {
                    _sharedState.SelectedChannel = null;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load channels: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSelectedWellChanged(DQIWellLookupItem? value)
        {
            _sharedState.SelectedWellId = value?.WellId;
            _sharedState.SelectedWellName = value?.WellName;
            _ = RefreshWellAveragesAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _channelsView.Refresh();
        }

        private bool FilterChannels(object obj)
        {
            if (obj is not DQIChannelItemViewModel item) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            var q = SearchText.Trim();
            return item.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                   item.Mnemonic.Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        partial void OnSelectedChannelItemChanged(DQIChannelItemViewModel? value)
        {
            if (value == null)
            {
                _sharedState.SelectedChannel = null;
                return;
            }

            foreach (var ch in Channels)
            {
                ch.IsSelected = (ch == value);
            }

            _sharedState.SelectedChannel = value.Channel;
        }

        [RelayCommand]
        public void SelectChannel(DQIChannelItemViewModel? channelItem)
        {
            if (channelItem == null) return;
            SelectedChannelItem = channelItem;
        }

        [RelayCommand]
        public void OpenSettings()
        {
            RequestOpenSettings?.Invoke();
        }

        public async Task RefreshWellAveragesAsync()
        {
            if (string.IsNullOrWhiteSpace(_sharedState.SelectedWellId))
            {
                ConsistencyScore = 0;
                FrequencyScore = 0;
                AccuracyScore = 0;
                OverallWellAvg = 0;
                return;
            }

            try
            {
                var averages = await _dqiService.GetWellAverageIndicesAsync(_sharedState.SelectedWellId);
                _sharedState.WellAvgIndices = averages;

                ConsistencyScore = averages.ConsistencyAvg;
                FrequencyScore = averages.FrequencyAvg;
                AccuracyScore = averages.AccuracyAvg;
                OverallWellAvg = averages.OverallAvg;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load well averages: {ex.Message}";
            }
        }
    }
}
