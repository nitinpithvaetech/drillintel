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
        if (ViewModel != null && ViewModel.SelectedTreeNode != null)
        {
            await ViewModel.OpenDocumentNodeCommand.ExecuteAsync(ViewModel.SelectedTreeNode);
        }
    }
}

