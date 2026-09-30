using System.Windows;
using System.Windows.Controls;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class RigStateView : UserControl
{
    public RigStateView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext == null)
        {
            DataContext = new RigStateViewModel();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RigStateViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is RigStateViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool success)
    {
        var win = Window.GetWindow(this);
        if (win != null && win.IsVisible)
        {
            try
            {
                win.DialogResult = success;
            }
            catch { }
            win.Close();
        }
    }

    private void PresetUnknownColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex && DataContext is RigStateViewModel vm)
        {
            vm.UnknownColorHex = hex;
        }
    }

    private void ItemColorPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex && btn.DataContext is RigStateItemModel item)
        {
            item.ColorHex = hex;
        }
    }
}

