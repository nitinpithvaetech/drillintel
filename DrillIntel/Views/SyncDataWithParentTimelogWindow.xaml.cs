using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class SyncDataWithParentTimelogWindow : Window
{
    public SyncDataWithParentTimelogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SyncDataWithParentTimelogViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SyncDataWithParentTimelogViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is SyncDataWithParentTimelogViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool success)
    {
        try
        {
            DialogResult = success;
        }
        catch
        {
        }
        Close();
    }
}

