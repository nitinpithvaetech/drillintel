using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class EditDepthLogView : UserControl
{
    public EditDepthLogView()
    {
        InitializeComponent();
    }

    private void OnChannelsDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EditDepthLogViewModel vm && vm.SelectedChannel != null)
        {
            vm.EditChannelCommand.Execute(vm.SelectedChannel);
        }
    }
}

