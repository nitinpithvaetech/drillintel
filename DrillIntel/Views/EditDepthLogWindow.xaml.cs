using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class EditDepthLogWindow : Window
{
    public EditDepthLogWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is EditDepthLogViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EditDepthLogViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is EditDepthLogViewModel newVm)
        {
            newVm.RequestClose -= OnRequestClose;
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool? success)
    {
        try
        {
            DialogResult = success;
        }
        catch
        {
            // Fallback if shown non-modal
        }

        try
        {
            Close();
        }
        catch
        {
        }
    }
}

