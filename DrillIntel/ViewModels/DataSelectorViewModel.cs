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
    private string _labelsField = "Timestamp";

    [ObservableProperty]
    private string _xCoordinate = "DayIndex";

    [ObservableProperty]
    private string _yCoordinate = "TotalSales";

    [ObservableProperty]
    private string _selectedChannelMnemonic = string.Empty;

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
                if (!string.IsNullOrWhiteSpace(name))
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
        AvailableLabelFields.Add("Timestamp");
        AvailableLabelFields.Add("DateTime (UTC)");
        AvailableLabelFields.Add("FormattedDate");
        AvailableLabelFields.Add("SampleIndex");
        LabelsField = "Timestamp";

        AvailableXCoordinates.Add("DayIndex");
        AvailableXCoordinates.Add("TimeOADate");
        AvailableXCoordinates.Add("ElapsedHours");
        AvailableXCoordinates.Add("MeasuredDepth");
        XCoordinate = _console.IndexType == enumIndexType.TimeLog ? "DayIndex" : "MeasuredDepth";

        // Y Coordinates
        foreach (var ch in channels)
        {
            if (!string.IsNullOrWhiteSpace(ch.Mnemonic) && !AvailableYCoordinates.Contains(ch.Mnemonic))
            {
                AvailableYCoordinates.Add(ch.Mnemonic);
            }
        }
        if (!AvailableYCoordinates.Contains("TotalSales")) AvailableYCoordinates.Add("TotalSales");
        if (!AvailableYCoordinates.Contains("ROPA")) AvailableYCoordinates.Add("ROPA");
        if (!AvailableYCoordinates.Contains("WOB")) AvailableYCoordinates.Add("WOB");
        if (!AvailableYCoordinates.Contains("TORQ")) AvailableYCoordinates.Add("TORQ");

        YCoordinate = AvailableYCoordinates.FirstOrDefault() ?? "TotalSales";
        SelectedChannelMnemonic = YCoordinate;
    }

    [RelayCommand]
    private void ApplyChanges()
    {
        // Apply field mappings to matching channel if found
        var channel = _console.Tracks.SelectMany(t => t.Channels)
            .FirstOrDefault(c => c.Title.Equals(TargetSeries, StringComparison.OrdinalIgnoreCase) ||
                                 c.Mnemonic.Equals(TargetSeries, StringComparison.OrdinalIgnoreCase));

        if (channel != null && !string.IsNullOrWhiteSpace(YCoordinate))
        {
            channel.Mnemonic = YCoordinate;
            channel.Title = TargetSeries;
        }

        SelectedChannelMnemonic = !string.IsNullOrWhiteSpace(YCoordinate)
            ? YCoordinate
            : (channel?.Mnemonic ?? TargetSeries);

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}

