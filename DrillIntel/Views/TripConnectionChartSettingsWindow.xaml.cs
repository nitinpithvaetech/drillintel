using System.Windows;

namespace DrillIntel.Views;

public partial class TripConnectionChartSettingsWindow : Window
{
    public TripConnectionChartSettingsWindow() => InitializeComponent();

    private void OnSaveClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
