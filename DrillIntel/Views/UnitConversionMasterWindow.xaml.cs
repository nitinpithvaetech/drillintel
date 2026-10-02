using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitConversionMasterWindow : Window
{
    public UnitConversionMasterWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is UnitConversionMasterViewModel vm)
        {
            vm.RequestClose += _ => Close();
        }
    }
}

