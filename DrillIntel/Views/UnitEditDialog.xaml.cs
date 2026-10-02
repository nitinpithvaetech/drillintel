using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class UnitEditDialog : Window
{
    public UnitEditDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is UnitEditViewModel vm)
        {
            vm.RequestClose += result =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}

