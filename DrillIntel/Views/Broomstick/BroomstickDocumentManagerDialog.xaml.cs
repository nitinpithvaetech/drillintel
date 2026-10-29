using System;
using System.Windows;
using DrillIntel.ViewModels.Broomstick;

namespace DrillIntel.Views.Broomstick;

public partial class BroomstickDocumentManagerDialog : Window
{
    public BroomstickDocumentManagerViewModel ViewModel => (BroomstickDocumentManagerViewModel)DataContext;

    public BroomstickDocumentManagerDialog(BroomstickDocumentManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose = () =>
        {
            this.DialogResult = viewModel.DialogResult;
            this.Close();
        };

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        ViewModel.SelectedDocumentNode = e.NewValue as BroomstickDocumentTreeNode;
    }

    private void OnTreeViewPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            var treeItem = FindVisualParent<System.Windows.Controls.TreeViewItem>(dep);
            if (treeItem != null)
            {
                treeItem.Focus();
                treeItem.IsSelected = true;
            }
        }
    }

    private async void OnTreeViewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete && ViewModel != null && ViewModel.CanDeleteSelection)
        {
            await ViewModel.DeleteTemplateCommand.ExecuteAsync(null);
            e.Handled = true;
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

    private void OnTreeViewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ViewModel.SelectedDocumentNode != null)
        {
            ViewModel.OpenSelectedCommand.Execute(null);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

