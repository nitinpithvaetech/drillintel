using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitMasterWindow : Window
{
    public UnitMasterWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is UnitMasterViewModel vm)
        {
            vm.RequestClose += _ => Close();
        }
    }
}

