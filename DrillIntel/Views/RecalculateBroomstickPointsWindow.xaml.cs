using System.ComponentModel;
using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

/// <summary>
/// Interaction logic for RecalculateBroomstickPointsWindow.xaml
/// </summary>
public partial class RecalculateBroomstickPointsWindow : Window
{
    public RecalculateBroomstickPointsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RecalculateBroomstickPointsViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }
        if (e.NewValue is RecalculateBroomstickPointsViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool isCompleted)
    {
        DialogResult = isCompleted;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is RecalculateBroomstickPointsViewModel vm && vm.IsRunning)
        {
            vm.StopCommand.Execute(null);
        }
        base.OnClosing(e);
    }
}

