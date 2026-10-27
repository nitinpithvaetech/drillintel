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
        if (e.NewValue is BroomstickDocumentTreeNode docNode)
        {
            ViewModel.SelectedDocumentNode = docNode;
        }
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

