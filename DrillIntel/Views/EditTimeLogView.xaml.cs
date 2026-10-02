using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class EditTimeLogView : UserControl
{
    public EditTimeLogView()
    {
        InitializeComponent();
    }

    private void OnChannelsDataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EditTimeLogViewModel vm && vm.SelectedChannel != null)
        {
            vm.EditChannelCommand.Execute(vm.SelectedChannel);
        }
    }
}

