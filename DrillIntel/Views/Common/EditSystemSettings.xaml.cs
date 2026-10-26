using System;
using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views.Common
{
    /// <summary>
    /// Interaction logic for EditSystemSettings.xaml
    /// </summary>
    public partial class EditSystemSettings : Window
    {
        public EditSystemSettings()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        public EditSystemSettings(EditSystemSettingsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is EditSystemSettingsViewModel oldVm)
            {
                oldVm.RequestClose -= OnRequestClose;
            }

            if (e.NewValue is EditSystemSettingsViewModel newVm)
            {
                newVm.RequestClose += OnRequestClose;
            }
        }

        private void OnRequestClose(bool? result)
        {
            if (result.HasValue)
            {
                try
                {
                    DialogResult = result.Value;
                }
                catch (InvalidOperationException)
                {
                    // DialogResult can only be set when shown as modal via ShowDialog()
                }
            }
            Close();
        }
    }
}

