using System.Windows;
using System.Windows.Controls;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

public partial class WellInformationView : UserControl
{
    public WellInformationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext == null)
        {
            DataContext = new WellInformationViewModel();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WellInformationViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is WellInformationViewModel newVm)
        {
            newVm.RequestClose -= OnRequestClose;
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(bool success)
    {
        var win = Window.GetWindow(this);
        if (win != null && win.IsLoaded)
        {
            try
            {
                win.DialogResult = success;
            }
            catch { }
            try
            {
                win.Close();
            }
            catch { }
        }
    }
}
