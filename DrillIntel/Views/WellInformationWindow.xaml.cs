using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class WellInformationWindow : Window
{
    public WellInformationWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is WellInformationViewModel vm)
        {
            vm.RequestClose += (success) =>
            {
                DialogResult = success;
                Close();
            };
        }
    }
}

