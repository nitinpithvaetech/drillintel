using System.Windows;
using System.Windows.Controls;
using DrillIntel.Models;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DashboardViewModel vm && e.NewValue is WellTreeNode node)
        {
            vm.OnTreeNodeSelected(node);
        }
    }
}

