using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class DQINullValuesTabViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;

        [ObservableProperty]
        private string _nullValues = "-999.25, -999.0";

        public DQINullValuesTabViewModel(IDQIService dqiService)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
        }

        public async Task LoadAsync()
        {
            NullValues = await _dqiService.GetNullValuesAsync();
        }
    }
}

