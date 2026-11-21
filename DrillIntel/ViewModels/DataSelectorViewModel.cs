using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Models.TChart;

namespace DrillIntel.ViewModels;

public partial class DataSelectorViewModel : ObservableObject
{
    private readonly VHTrackConsole _console;

    [ObservableProperty]
    private string _targetSeries = string.Empty;

    [ObservableProperty]
    private string _labelsField = "DateTime";

    [ObservableProperty]
    private string _xCoordinate = "DateTime";

    [ObservableProperty]
    private string _yCoordinate = "BIT_DEPTH";

    [ObservableProperty]
    private string _selectedChannelMnemonic = string.Empty;

    [ObservableProperty]
    private bool _colorCodeAsRigState;

    [ObservableProperty]
    private bool _isYAxisInverted = true;

    partial void OnTargetSeriesChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var ch = _console.Tracks.SelectMany(t => t.Channels)
            .FirstOrDefault(c => c.Title.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                                 c.Mnemonic.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (ch != null && !string.IsNullOrWhiteSpace(ch.Mnemonic) && AvailableYCoordinates.Contains(ch.Mnemonic))
        {
            YCoordinate = ch.Mnemonic;
        }
    }

    partial void OnYCoordinateChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var ch = _console.Tracks.SelectMany(t => t.Channels)
            .FirstOrDefault(c => c.Mnemonic.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (ch != null)
        {
            string name = !string.IsNullOrWhiteSpace(ch.Title) ? ch.Title : ch.Mnemonic;
            if (AvailableSeries.Contains(name) && TargetSeries != name)
            {
                TargetSeries = name;
            }
        }
    }

    public ObservableCollection<string> AvailableSeries { get; } = new();
    public ObservableCollection<string> AvailableLabelFields { get; } = new();
    public ObservableCollection<string> AvailableXCoordinates { get; } = new();
    public ObservableCollection<string> AvailableYCoordinates { get; } = new();

    public event Action<bool>? RequestClose;

    public DataSelectorViewModel(VHTrackConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));

        // Populate available series from console tracks/channels
        var channels = _console.Tracks.SelectMany(t => t.Channels).ToList();
        if (channels.Count > 0)
        {
            foreach (var ch in channels)
            {
                string name = !string.IsNullOrWhiteSpace(ch.Title) ? ch.Title : ch.Mnemonic;
                if (!string.IsNullOrWhiteSpace(name) && !AvailableSeries.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    AvailableSeries.Add(name);
                }
            }
        }

        if (AvailableSeries.Count == 0)
        {
            AvailableSeries.Add("LineSeries1");
            AvailableSeries.Add("ROPA (Rate of Penetration)");
            AvailableSeries.Add("WOB (Weight on Bit)");
            AvailableSeries.Add("RPM (Rotary Speed)");
            AvailableSeries.Add("TORQ (Drilling Torque)");
        }

        TargetSeries = AvailableSeries.FirstOrDefault() ?? "LineSeries1";

        // Field mapping options matching drilling analytics & user reference
        AvailableLabelFields.Add("DateTime");
        AvailableLabelFields.Add("Timestamp");
        AvailableLabelFields.Add("DateTime (UTC)");
        AvailableLabelFields.Add("FormattedDate");
        AvailableLabelFields.Add("SampleIndex");
        LabelsField = "DateTime";

        AvailableXCoordinates.Add("DateTime");
        AvailableXCoordinates.Add("DATETIME");
        AvailableXCoordinates.Add("DayIndex");
        AvailableXCoordinates.Add("TimeOADate");
        AvailableXCoordinates.Add("ElapsedHours");
        AvailableXCoordinates.Add("MeasuredDepth");
        XCoordinate = "DateTime";

        // Y Coordinates
        foreach (var ch in channels)
        {
            if (!string.IsNullOrWhiteSpace(ch.Mnemonic) && !AvailableYCoordinates.Contains(ch.Mnemonic))
            {
                AvailableYCoordinates.Add(ch.Mnemonic);
            }
        }
        // Prefer the canonical bit-depth mnemonic, then the app's legacy DEPTH channel.
        // Both options stay in the list when present so existing documents remain selectable.
        if (!AvailableYCoordinates.Contains("BIT_DEPTH", StringComparer.OrdinalIgnoreCase)) AvailableYCoordinates.Insert(0, "BIT_DEPTH");
        if (!AvailableYCoordinates.Contains("DEPTH", StringComparer.OrdinalIgnoreCase)) AvailableYCoordinates.Insert(1, "DEPTH");
        var depthChannel = channels.FirstOrDefault(c =>
            c.Title.Contains("Bit Depth", StringComparison.OrdinalIgnoreCase) ||
            c.Name.Contains("Bit Depth", StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault(c => c.Mnemonic.Equals("BIT_DEPTH", StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault(c => c.Mnemonic.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
        var depthOption = depthChannel?.Mnemonic
            ?? AvailableYCoordinates.FirstOrDefault(y => y.Equals("BIT_DEPTH", StringComparison.OrdinalIgnoreCase))
            ?? AvailableYCoordinates.FirstOrDefault(y => y.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
            ?? AvailableYCoordinates.FirstOrDefault(y => y.Contains("DEPTH", StringComparison.OrdinalIgnoreCase));

        YCoordinate = depthOption ?? AvailableYCoordinates.FirstOrDefault() ?? "DEPTH";
        SelectedChannelMnemonic = YCoordinate;
        ColorCodeAsRigState = true;
        IsYAxisInverted = true;
    }

    [RelayCommand]
    private void ApplyChanges()
    {
        var exactChannel = _console.Tracks.SelectMany(t => t.Channels)
            .FirstOrDefault(c => c.Mnemonic.Equals(YCoordinate, StringComparison.OrdinalIgnoreCase));

        if (exactChannel != null)
        {
            SelectedChannelMnemonic = exactChannel.Mnemonic;
            exactChannel.ColorCodeAsRigState = ColorCodeAsRigState;
        }
        else
        {
            var channel = _console.Tracks.SelectMany(t => t.Channels)
                .FirstOrDefault(c => c.Title.Equals(TargetSeries, StringComparison.OrdinalIgnoreCase) ||
                                     c.Mnemonic.Equals(TargetSeries, StringComparison.OrdinalIgnoreCase));

            if (channel != null && !string.IsNullOrWhiteSpace(YCoordinate))
            {
                channel.Mnemonic = YCoordinate;
                channel.Title = TargetSeries;
            }

            if (channel != null)
                channel.ColorCodeAsRigState = ColorCodeAsRigState;

            SelectedChannelMnemonic = !string.IsNullOrWhiteSpace(YCoordinate)
                ? YCoordinate
                : (channel?.Mnemonic ?? TargetSeries);
        }

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}

