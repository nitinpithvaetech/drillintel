using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class TrackPropertiesDialog : Window
{
    public TrackPropertiesDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrackPropertiesViewModel vm)
        {
            vm.RequestClose += (dialogResult) =>
            {
                DialogResult = dialogResult;
                Close();
            };
        }
    }
}

