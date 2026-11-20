using System.Windows;
using System.Windows.Input;
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

            MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    DragMove();
                }
            };
        }
    }
}

