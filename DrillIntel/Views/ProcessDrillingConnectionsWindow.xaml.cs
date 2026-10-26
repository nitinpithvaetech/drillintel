using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views
{
    /// <summary>
    /// Interaction logic for ProcessDrillingConnectionsWindow.xaml
    /// </summary>
    public partial class ProcessDrillingConnectionsWindow : Window
    {
        public ProcessDrillingConnectionsWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ProcessDrillingConnectionsViewModel oldVm)
            {
                oldVm.RequestClose -= OnRequestClose;
            }

            if (e.NewValue is ProcessDrillingConnectionsViewModel newVm)
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
            if (DataContext is ProcessDrillingConnectionsViewModel vm && vm.IsRunning)
            {
                vm.CancelCommand.Execute(null);
            }
            base.OnClosing(e);
        }
    }
}

