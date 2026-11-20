using System;
using System.Windows;
using DrillIntel.Services;
using DrillIntel.ViewModels.DQI;

namespace DrillIntel.Views.DQI
{
    public partial class DQIExplorerWindow : Window
    {
        private readonly DQIExplorerViewModel _viewModel;
        private readonly IDQIService _dqiService;
        private readonly IDQISharedStateService _sharedState;

        public DQIExplorerWindow(
            DQIExplorerViewModel viewModel,
            IDQIService dqiService,
            IDQISharedStateService sharedState)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _dqiService = dqiService;
            _sharedState = sharedState;

            DataContext = _viewModel;

            _viewModel.RequestOpenSetup += OnRequestOpenSetup;
            Loaded += async (_, _) =>
            {
                try
                {
                    await _viewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Error initializing DQI Explorer: {ex.Message}", "DQI Explorer", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
        }

        private async void OnRequestOpenSetup()
        {
            try
            {
                var setupVm = new DQISetupViewModel(_dqiService, _sharedState);
                await setupVm.InitializeAsync();

                var dlg = new DQISetupWindow(setupVm)
                {
                    Owner = this
                };

                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening DQI Setup: {ex.Message}", "DQI Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

