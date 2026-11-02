using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views
{
    /// <summary>
    /// Interaction logic for ProcessDrillingStandsWindow.xaml
    /// </summary>
    public partial class ProcessDrillingStandsWindow : Window
    {
        public ProcessDrillingStandsWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ProcessDrillingStandsViewModel oldVm)
            {
                oldVm.RequestClose -= OnRequestClose;
            }

            if (e.NewValue is ProcessDrillingStandsViewModel newVm)
            {
                newVm.RequestClose += OnRequestClose;
            }
        }

        private void OnRequestClose(bool dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is ProcessDrillingStandsViewModel vm && vm.IsRunning)
            {
                vm.CancelCommand.Execute(null);
            }
            base.OnClosing(e);
        }
    }
}

