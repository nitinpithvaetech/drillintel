using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitConversionEditDialog : Window
{
    public UnitConversionEditDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UnitConversionEditViewModel vm)
        {
            vm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool? dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }
}

