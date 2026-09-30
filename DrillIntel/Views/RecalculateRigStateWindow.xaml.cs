using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class RecalculateRigStateWindow : Window
{
    public RecalculateRigStateWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is RecalculateRigStateViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RecalculateRigStateViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is RecalculateRigStateViewModel newVm)
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

