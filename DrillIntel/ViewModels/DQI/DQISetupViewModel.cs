using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class DQISetupViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;

        public DQIChannelListTabViewModel ChannelListTab { get; }
        public DQIChannelBoundsTabViewModel ChannelBoundsTab { get; }
        public DQINullValuesTabViewModel NullValuesTab { get; }

        public event Action<bool>? RequestClose;

        [ObservableProperty]
        private int _selectedTabIndex;

        [ObservableProperty]
        private bool _isSaving;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public DQISetupViewModel(IDQIService dqiService, IDQISharedStateService sharedState)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));

            ChannelListTab = new DQIChannelListTabViewModel(dqiService, sharedState);
            ChannelBoundsTab = new DQIChannelBoundsTabViewModel(dqiService);
            NullValuesTab = new DQINullValuesTabViewModel(dqiService);
        }

        public async Task InitializeAsync()
        {
            await ChannelListTab.LoadAsync();
            await ChannelBoundsTab.LoadAsync();
            await NullValuesTab.LoadAsync();
            SyncChannelBounds();
        }

        private void SyncChannelBounds()
        {
            // Ensure every channel in ChannelListTab has a corresponding boundary entry in ChannelBoundsTab
            foreach (var ch in ChannelListTab.Channels)
            {
                if (!ChannelBoundsTab.Boundaries.Any(b => b.Mnemonic.Equals(ch.Mnemonic, StringComparison.OrdinalIgnoreCase)))
                {
                    ChannelBoundsTab.Boundaries.Add(new BoundaryDraftItemViewModel
                    {
                        Mnemonic = ch.Mnemonic,
                        Name = ch.Name,
                        Unit = "",
                        Min = 0.0,
                        Max = 1000.0
                    });
                }
            }
        }

        [RelayCommand]
        public async Task SaveChangesAsync()
        {
            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;

                SyncChannelBounds();

                // 1. Save Channels
                var channelModels = ChannelListTab.Channels.Select(c => c.ToModel()).ToList();
                await _dqiService.SaveChannelsAsync(channelModels);

                // 2. Save Boundaries
                var boundaryModels = ChannelBoundsTab.Boundaries.Select(b => b.ToModel()).ToList();
                await _dqiService.SaveBoundariesAsync(boundaryModels);

                // 3. Save Null Values
                await _dqiService.SaveNullValuesAsync(NullValuesTab.NullValues);

                _sharedState.NotifyConfigurationSaved();
                RequestClose?.Invoke(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to save setup: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}

