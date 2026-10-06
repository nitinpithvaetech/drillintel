using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class DataSelectorWindow : Window
{
    public DataSelectorWindow(DataSelectorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (result) =>
        {
            DialogResult = result;
            Close();
        };
    }
}

