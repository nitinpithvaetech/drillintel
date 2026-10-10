using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views.Broomstick;

public partial class ImportPlanDialog : Window
{
    public ImportPlanDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImportPlanViewModel vm)
        {
            vm.RequestClose += success =>
            {
                DialogResult = success;
                Close();
            };
        }
    }
}

