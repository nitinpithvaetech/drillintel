using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class BoundaryDraftItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _mnemonic = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _unit = string.Empty;

        [ObservableProperty]
        private double _min;

        [ObservableProperty]
        private double _max;

        public BoundaryDraftItemViewModel() { }

        public BoundaryDraftItemViewModel(DQIDataBoundary boundary)
        {
            if (boundary != null)
            {
                _mnemonic = boundary.Mnemonic;
                _name = boundary.Name;
                _unit = boundary.Unit;
                _min = boundary.Min;
                _max = boundary.Max;
            }
        }

        public DQIDataBoundary ToModel() => new DQIDataBoundary(Mnemonic, Name, Unit, Min, Max);
    }

    public partial class DQIChannelBoundsTabViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;

        [ObservableProperty]
        private ObservableCollection<BoundaryDraftItemViewModel> _boundaries = new();

        public DQIChannelBoundsTabViewModel(IDQIService dqiService)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
        }

        public async Task LoadAsync()
        {
            Boundaries.Clear();
            var list = await _dqiService.GetBoundariesAsync();
            foreach (var b in list)
            {
                Boundaries.Add(new BoundaryDraftItemViewModel(b));
            }
        }
    }
}

