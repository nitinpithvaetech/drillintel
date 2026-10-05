using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Models.TChart;
using DrillIntel.Views;

namespace DrillIntel.ViewModels;

public partial class TrackPropertiesViewModel : ObservableObject
{
    private readonly VHTrackConsole _console;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Track & Axis Properties";

    [ObservableProperty]
    private string _subtitle = "Configure track columns, titles, widths, fonts, and axis scaling for the chart console.";

    public ObservableCollection<VHTrack> Tracks { get; } = new();

    public ObservableCollection<VHTrackChannel> CurrentTrackChannels { get; } = new();

    [ObservableProperty]
    private VHTrack? _selectedTrack;

    [ObservableProperty]
    private VHTrackChannel? _selectedChannel;

    public ObservableCollection<string> AvailableFonts { get; } = new()
    {
        "Tahoma", "Segoe UI", "Arial", "Calibri", "Consolas"
    };

    public ObservableCollection<enumRTSeriesStyle> AvailableSeriesTypes { get; } = new()
    {
        enumRTSeriesStyle.Line,
        enumRTSeriesStyle.Point,
        enumRTSeriesStyle.Area
    };

    public ObservableCollection<enumRTLineStyle> AvailableLineStyles { get; } = new()
    {
        enumRTLineStyle.Solid,
        enumRTLineStyle.Dash,
        enumRTLineStyle.Dot,
        enumRTLineStyle.DashDot
    };

    public ObservableCollection<enumRTPointStyle> AvailablePointStyles { get; } = new()
    {
        enumRTPointStyle.Circle,
        enumRTPointStyle.Square,
        enumRTPointStyle.Triangle,
        enumRTPointStyle.Diamond
    };

    public TrackPropertiesViewModel(VHTrackConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        RefreshTracks();
    }

    private void RefreshTracks()
    {
        Tracks.Clear();
        foreach (var t in _console.Tracks)
        {
            Tracks.Add(t);
        }
        SelectedTrack = Tracks.FirstOrDefault();
        SyncCurrentTrackChannels();
    }

    partial void OnSelectedTrackChanged(VHTrack? value)
    {
        SyncCurrentTrackChannels();
    }

    private void SyncCurrentTrackChannels()
    {
        CurrentTrackChannels.Clear();
        if (SelectedTrack?.Channels != null)
        {
            foreach (var ch in SelectedTrack.Channels)
            {
                CurrentTrackChannels.Add(ch);
            }
        }
        SelectedChannel = CurrentTrackChannels.FirstOrDefault();
    }

    [RelayCommand]
    private void PickTrackFontColor()
    {
        if (SelectedTrack == null) return;
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, SelectedTrack.FontColor);
        if (ok)
        {
            SelectedTrack.FontColor = hex;
            OnPropertyChanged(nameof(SelectedTrack));
        }
    }

    [RelayCommand]
    private void PickChannelLineColor()
    {
        if (SelectedChannel == null) return;
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, SelectedChannel.LineColor);
        if (ok)
        {
            SelectedChannel.LineColor = hex;
            OnPropertyChanged(nameof(SelectedChannel));
        }
    }

    // Test hook / UI handler
    public Func<string, string, bool>? ConfirmActionHandler { get; set; }

    private bool Confirm(string message, string title)
    {
        if (ConfirmActionHandler != null) return ConfirmActionHandler(message, title);
        if (Application.Current != null)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
        return true;
    }

    [RelayCommand]
    private void DeleteTrack()
    {
        if (SelectedTrack == null) return;
        if (Confirm($"Are you sure you want to delete track '{SelectedTrack.Title}'?", "Confirm Delete"))
        {
            _console.Tracks.Remove(SelectedTrack);
            RefreshTracks();
        }
    }

    [RelayCommand]
    private void PickAxisFontColor()
    {
        if (SelectedChannel?.objXAxis == null) return;
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, SelectedChannel.objXAxis.LabelFontColor);
        if (ok)
        {
            SelectedChannel.objXAxis.LabelFontColor = hex;
            OnPropertyChanged(nameof(SelectedChannel));
        }
    }

    [RelayCommand]
    private void DeleteChannel()
    {
        if (SelectedTrack == null || SelectedChannel == null) return;
        if (Confirm($"Remove curve '{SelectedChannel.Mnemonic}' from this track?", "Confirm Remove"))
        {
            var channelToRemove = SelectedChannel;
            SelectedTrack.Channels.Remove(channelToRemove);
            CurrentTrackChannels.Remove(channelToRemove);
            SelectedChannel = CurrentTrackChannels.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedTrack));
        }
    }

    [RelayCommand]
    private void Save()
    {
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
