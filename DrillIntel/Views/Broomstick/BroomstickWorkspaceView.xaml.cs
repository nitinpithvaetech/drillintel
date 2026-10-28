using System;
using System.Windows;
using System.Windows.Controls;
using DrillIntel.ViewModels.Broomstick;

namespace DrillIntel.Views.Broomstick;

public partial class BroomstickWorkspaceView : UserControl
{
    public BroomstickWorkspaceViewModel? ViewModel => DataContext as BroomstickWorkspaceViewModel;

    public BroomstickWorkspaceView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.InitializeAsync();
        }
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel != null && e.NewValue is BroomstickDocumentTreeNode docNode)
        {
            ViewModel.SelectedTreeNode = docNode;
        }
    }

    private async void OnTreeViewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ViewModel == null) return;

        var targetNode = ViewModel.SelectedTreeNode;
        if (targetNode == null && e.OriginalSource is DependencyObject dep)
        {
            var treeItem = FindVisualParent<TreeViewItem>(dep);
            if (treeItem?.DataContext is BroomstickDocumentTreeNode docNode)
            {
                targetNode = docNode;
            }
        }

        if (targetNode != null)
        {
            await ViewModel.OpenDocumentNodeAsync(targetNode);
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }
}

