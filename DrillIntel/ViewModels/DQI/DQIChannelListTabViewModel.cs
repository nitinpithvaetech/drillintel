using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class ChannelDraftItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _mnemonic = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        public ChannelDraftItemViewModel() { }

        public ChannelDraftItemViewModel(string mnemonic, string name)
        {
            _mnemonic = mnemonic;
            _name = name;
        }

        public DQIChannel ToModel() => new DQIChannel(Mnemonic, Name);
    }

    public partial class DQIChannelListTabViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;

        [ObservableProperty]
        private ObservableCollection<ChannelDraftItemViewModel> _channels = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableCatalogueMnemonics = new();

        [ObservableProperty]
        private string? _selectedCatalogueMnemonic;

        public DQIChannelListTabViewModel(IDQIService dqiService, IDQISharedStateService sharedState)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
        }

        public async Task LoadAsync()
        {
            Channels.Clear();
            var channels = await _dqiService.GetChannelsAsync();
            foreach (var ch in channels)
            {
                Channels.Add(new ChannelDraftItemViewModel(ch.Mnemonic, ch.Name));
            }

            // Load available catalogue channels from the active well's timelog if possible
            AvailableCatalogueMnemonics.Clear();
            var wellId = _sharedState.SelectedWellId;
            if (!string.IsNullOrWhiteSpace(wellId))
            {
                var mnemonics = await _dqiService.GetAvailableChannelMnemonicsFromTimeLogAsync(wellId);
                foreach (var m in mnemonics)
                {
                    if (!Channels.Any(c => c.Mnemonic.Equals(m, StringComparison.OrdinalIgnoreCase)))
                    {
                        AvailableCatalogueMnemonics.Add(m);
                    }
                }
            }

            // Also add standard presets if not present
            var standardPresets = new[] { "BPOS", "CIRC", "HKLD", "SPPA", "TORQ", "RPM", "ROP", "FLOWIN", "WOB" };
            foreach (var p in standardPresets)
            {
                if (!AvailableCatalogueMnemonics.Contains(p) && !Channels.Any(c => c.Mnemonic.Equals(p, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableCatalogueMnemonics.Add(p);
                }
            }
        }

        [RelayCommand]
        public void AddChannel()
        {
            if (string.IsNullOrWhiteSpace(SelectedCatalogueMnemonic)) return;

            string m = SelectedCatalogueMnemonic.Trim();
            if (!Channels.Any(c => c.Mnemonic.Equals(m, StringComparison.OrdinalIgnoreCase)))
            {
                string friendlyName = GetFriendlyName(m);
                Channels.Add(new ChannelDraftItemViewModel(m, friendlyName));
                AvailableCatalogueMnemonics.Remove(m);
                SelectedCatalogueMnemonic = AvailableCatalogueMnemonics.FirstOrDefault();
            }
        }

        [RelayCommand]
        public void RemoveChannel(ChannelDraftItemViewModel? item)
        {
            if (item == null) return;
            Channels.Remove(item);
            if (!AvailableCatalogueMnemonics.Contains(item.Mnemonic))
            {
                AvailableCatalogueMnemonics.Add(item.Mnemonic);
            }
        }

        private static string GetFriendlyName(string mnemonic) => mnemonic.ToUpperInvariant() switch
        {
            "BPOS" => "Block Position",
            "CIRC" => "Circulation",
            "HKLD" => "Hookload",
            "SPPA" => "Pump Pressure",
            "TORQ" => "Torque",
            "RPM" => "Rotary Speed",
            "ROP" => "Rate of Penetration",
            "FLOWIN" => "Flow In Rate",
            "WOB" => "Weight on Bit",
            _ => mnemonic
        };
    }
}

