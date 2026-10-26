using System.Windows;
using System.Windows.Controls;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class TagVisualizerView : UserControl
{
    public TagVisualizerView()
    {
        InitializeComponent();
    }

    private void OnRangeTrackBarChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TagVisualizerViewModel vm)
        {
            vm.RangeTrackBarChangedCommand.Execute(null);
        }
    }
}

