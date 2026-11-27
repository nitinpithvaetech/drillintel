using System.ComponentModel;
using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class ProcessTripConnectionWindow : Window
{
    public ProcessTripConnectionWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProcessTripConnectionViewModel oldVm)
            oldVm.RequestClose -= OnRequestClose;
        if (e.NewValue is ProcessTripConnectionViewModel newVm)
            newVm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is ProcessTripConnectionViewModel vm && vm.IsRunning)
        {
            e.Cancel = true;
            vm.CancelCommand.Execute(null);
        }
        base.OnClosing(e);
    }
}
