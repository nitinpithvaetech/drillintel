using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class ProcessTagCreationWindow : Window
{
    public ProcessTagCreationWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProcessTagCreationViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProcessTagCreationViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is ProcessTagCreationViewModel newVm)
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

