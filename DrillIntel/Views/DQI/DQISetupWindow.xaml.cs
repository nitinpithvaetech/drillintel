using System.Windows;
using DrillIntel.ViewModels.DQI;

namespace DrillIntel.Views.DQI
{
    public partial class DQISetupWindow : Window
    {
        public DQISetupWindow(DQISetupViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
