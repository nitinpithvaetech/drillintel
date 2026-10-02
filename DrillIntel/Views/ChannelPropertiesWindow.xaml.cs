using System.Windows;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class ChannelPropertiesWindow : Window
{
    public ChannelPropertiesWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ChannelPropertiesViewModel vm)
        {
            vm.RequestClose += result =>
            {
                try
                {
                    DialogResult = result;
                }
                catch
                {
                    // In case window was shown non-modally
                }
                Close();
            };
        }
    }
}

