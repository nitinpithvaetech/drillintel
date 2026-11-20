using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.ViewModels.DQI
{
    public partial class DQIExplorerViewModel : ObservableObject
    {
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;
        private readonly ProjectSession _session;

        public DQILeftPanelViewModel LeftPanel { get; }
        public DQIChartSectionViewModel ChartSection { get; }

        public event Action? RequestOpenSetup;

        public DQIExplorerViewModel(
            IDQIService dqiService,
            IDQISharedStateService sharedState,
            ProjectSession session)
        {
            _dqiService = dqiService ?? throw new ArgumentNullException(nameof(dqiService));
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _session = session ?? throw new ArgumentNullException(nameof(session));

            LeftPanel = new DQILeftPanelViewModel(dqiService, sharedState, session);
            ChartSection = new DQIChartSectionViewModel(dqiService, sharedState);

            LeftPanel.RequestOpenSettings += () => RequestOpenSetup?.Invoke();
        }

        public async Task InitializeAsync()
        {
            await LeftPanel.InitializeAsync();
        }
    }
}

