using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitConversionMasterView : UserControl
{
    public UnitConversionMasterView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is UnitConversionMasterViewModel vm && vm.SelectedConversion != null)
        {
            vm.EditConversion(vm.SelectedConversion);
        }
    }
}

