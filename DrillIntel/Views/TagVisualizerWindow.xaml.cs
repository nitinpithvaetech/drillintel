using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class TagVisualizerWindow : Window
{
    public TagVisualizerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TagVisualizerViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TagVisualizerViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is TagVisualizerViewModel newVm)
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
            // If shown non-modally
        }
        Close();
    }
}

