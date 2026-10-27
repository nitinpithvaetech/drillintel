using System;
using System.Windows;
using DrillIntel.ViewModels.Broomstick;

namespace DrillIntel.Views.Broomstick;

public partial class BroomstickSetupDialog : Window
{
    public BroomstickSetupViewModel ViewModel => (BroomstickSetupViewModel)DataContext;

    public BroomstickSetupDialog(BroomstickSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose = () =>
        {
            this.DialogResult = viewModel.DialogResult;
            this.Close();
        };
    }
}

