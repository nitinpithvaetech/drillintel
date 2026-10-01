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

    private void PickUnknownColor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is RigStateViewModel vm)
        {
            var parentWin = Window.GetWindow(this);
            var (success, hex) = ColorPickerDialog.Show(parentWin, vm.UnknownColorHex);
            if (success)
            {
                vm.UnknownColorHex = hex;
            }
        }
    }

    private void ItemColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is RigStateItemModel item)
        {
            var parentWin = Window.GetWindow(this);
            var (success, hex) = ColorPickerDialog.Show(parentWin, item.ColorHex);
            if (success)
            {
                item.ColorHex = hex;
            }
        }
    }
}

