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

    private void TreeViewItem_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var item = FindVisualParent<TreeViewItem>(source);
            if (item != null && item == sender)
            {
                item.Focus();
                item.IsSelected = true;
                if (DataContext is DashboardViewModel vm && item.DataContext is WellTreeNode node)
                {
                    vm.ContextSelectedNode = node.HasContextMenu ? node : null;
                    vm.OnTreeNodeSelected(node);
                }
            }
        }
    }

    private void TreeViewItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            var clickedItem = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (clickedItem == null && item.IsFocused)
            {
                clickedItem = item;
            }

            if (clickedItem == item && item.DataContext is WellTreeNode node && !node.HasContextMenu)
            {
                e.Handled = true;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

