using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

/// <summary>
/// Interaction logic for RigStateWorkspaceView.xaml.
/// Coordinates TreeView selection, double-click tab opening, and tab header actions.
/// </summary>
public partial class RigStateWorkspaceView : UserControl
{
    public RigStateWorkspaceView()
    {
        InitializeComponent();
    }

    private void ClearSearchFilter_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RigStateWorkspaceViewModel vm)
        {
            vm.SearchFilter = string.Empty;
        }
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is RigStateWorkspaceViewModel vm)
        {
            if (e.NewValue is RigStateDocumentTreeNode docNode)
            {
                vm.SelectedTreeNode = docNode;
            }
            else
            {
                vm.SelectedTreeNode = null;
            }
        }
    }

    private void OnTreeViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is RigStateWorkspaceViewModel vm)
        {
            if (DocumentsTreeView.SelectedItem is RigStateDocumentTreeNode docNode)
            {
                if (vm.OpenDocumentNodeCommand.CanExecute(docNode))
                {
                    vm.OpenDocumentNodeCommand.Execute(docNode);
                    e.Handled = true;
                }
            }
        }
    }
}

