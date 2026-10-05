using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class AddTrackChannelDialog : Window
{
    public AddTrackChannelDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AddTrackChannelViewModel vm)
        {
            vm.RequestClose += (dialogResult) =>
            {
                DialogResult = dialogResult;
                Close();
            };
        }
    }
}

