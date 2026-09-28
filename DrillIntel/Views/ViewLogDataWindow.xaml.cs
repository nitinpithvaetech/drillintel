using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class ViewLogDataWindow : Window
{
    public ViewLogDataWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ViewLogDataViewModel vm)
        {
            vm.RequestClose += () => Close();
        }
    }
}

