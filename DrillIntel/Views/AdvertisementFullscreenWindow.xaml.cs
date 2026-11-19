using System;
using System.Windows;
using System.Windows.Input;
using DrillIntel.Models;
using DrillIntel.ViewModels;

namespace DrillIntel.Views;

/// <summary>
/// Interaction logic for AdvertisementFullscreenWindow.xaml
/// </summary>
public partial class AdvertisementFullscreenWindow : Window
{
    public AdvertisementFullscreenWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        KeyDown += OnWindowKeyDown;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AdvertisementFullscreenViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }
        if (e.NewValue is AdvertisementFullscreenViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose()
    {
        Close();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not AdvertisementFullscreenViewModel vm) return;

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Left:
                vm.PreviousAdSlide();
                e.Handled = true;
                break;
            case Key.Right:
                vm.NextAdSlide();
                e.Handled = true;
                break;
            case Key.Space:
                vm.ToggleAutoScroll();
                e.Handled = true;
                break;
        }
    }

    private void OnSlideDotClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is MarketingAdSlide slide && DataContext is AdvertisementFullscreenViewModel vm)
        {
            vm.SelectAdSlide(slide.Index);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is AdvertisementFullscreenViewModel vm)
        {
            vm.StopTimer();
        }
        base.OnClosed(e);
    }
}

