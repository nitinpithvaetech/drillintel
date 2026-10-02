using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitMasterView : UserControl
{
    public UnitMasterView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UnitMasterViewModel vm && vm.SelectedUnit != null)
        {
            vm.EditUnit(vm.SelectedUnit);
        }
    }
}

