using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models.TChart;
using DrillIntel.Views;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for adding and styling a channel onto a VHTrackConsole track.
/// Allows configuration of series type, line/point marker styles, fonts, and axis scaling.
/// </summary>
public partial class AddTrackChannelViewModel : ObservableObject
{
    private readonly VHTrackConsole _console;
    private readonly List<LogChannelMetadata> _allMetadata = new();

    public event Action<bool?>? RequestClose;

    // Header & Meta
    [ObservableProperty]
    private string _title = "Add Channel to Chart";

    [ObservableProperty]
    private string _subtitle = "Select log curve, assign to a track, and configure visual styles, fonts, and axis limits.";

    [ObservableProperty]
    private string _filterText = string.Empty;

    // Selected Channel
    [ObservableProperty]
    private LogChannelMetadata? _selectedChannel;

    [ObservableProperty]
    private string _mnemonic = string.Empty;

    [ObservableProperty]
    private string _channelTitle = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    // Target Track
    public ObservableCollection<string> AvailableTracks { get; } = new();

    [ObservableProperty]
    private string _selectedTrack = string.Empty;

    // Series Type
    public ObservableCollection<enumRTSeriesStyle> AvailableSeriesTypes { get; } = new()
    {
        enumRTSeriesStyle.Line,
        enumRTSeriesStyle.Point,
        enumRTSeriesStyle.Area
    };

    [ObservableProperty]
    private enumRTSeriesStyle _selectedSeriesType = enumRTSeriesStyle.Line;

    // Line Styles
    [ObservableProperty]
    private double _lineWidth = 2;

    [ObservableProperty]
    private string _lineColorHex = "#1E88E5";

    public ObservableCollection<enumRTLineStyle> AvailableLineStyles { get; } = new()
    {
        enumRTLineStyle.Solid,
        enumRTLineStyle.Dash,
        enumRTLineStyle.DashDot,
        enumRTLineStyle.Dot
    };

    [ObservableProperty]
    private enumRTLineStyle _selectedLineStyle = enumRTLineStyle.Solid;

    // Point Marker Styles
    public ObservableCollection<enumRTPointStyle> AvailablePointStyles { get; } = new()
    {
        enumRTPointStyle.Circle,
        enumRTPointStyle.Square,
        enumRTPointStyle.Triangle,
        enumRTPointStyle.Diamond,
        enumRTPointStyle.Star
    };

    [ObservableProperty]
    private enumRTPointStyle _selectedPointStyle = enumRTPointStyle.Circle;

    [ObservableProperty]
    private double _pointSize = 4;

    [ObservableProperty]
    private string _pointFillColorHex = "#2196F3";

    [ObservableProperty]
    private string _pointBorderColorHex = "#0D47A1";

    // Rig State Shading
    [ObservableProperty]
    private bool _colorCodeAsRigState = false;

    // Font Configuration
    public ObservableCollection<string> AvailableFonts { get; } = new()
    {
        "Tahoma",
        "Segoe UI",
        "Arial",
        "Calibri",
        "Consolas"
    };

    [ObservableProperty]
    private string _selectedFont = "Tahoma";

    [ObservableProperty]
    private double _fontSize = 8;

    [ObservableProperty]
    private string _fontColorHex = "#2E7D32";

    [ObservableProperty]
    private bool _fontBold = true;

    [ObservableProperty]
    private bool _fontItalic = false;

    // Axis Limits & Behavior
    [ObservableProperty]
    private bool _autoScale = true;

    [ObservableProperty]
    private double _axisMin = 0;

    [ObservableProperty]
    private double _axisMax = 100;

    [ObservableProperty]
    private bool _inverted = false;

    [ObservableProperty]
    private bool _logarithmic = false;

    [ObservableProperty]
    private bool _showMajorGrids = true;

    [ObservableProperty]
    private string _majorGridColorHex = "#E0E0E0";

    [ObservableProperty]
    private bool _showMinorGrids = false;

    // Filtered collection
    public ObservableCollection<LogChannelMetadata> FilteredChannels { get; } = new();

    public VHTrackChannel? CreatedChannel { get; private set; }
    public VHTrack? TargetTrackInstance { get; private set; }

    public AddTrackChannelViewModel(VHTrackConsole console, IEnumerable<LogChannelMetadata> availableChannels)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        if (availableChannels != null)
        {
            _allMetadata.AddRange(availableChannels);
        }

        PopulateTracks();
        ApplyFilter();
    }

    private void PopulateTracks()
    {
        AvailableTracks.Clear();
        foreach (var t in _console.Tracks)
        {
            string trackName = !string.IsNullOrWhiteSpace(t.Title) ? t.Title : $"Track {_console.Tracks.IndexOf(t) + 1}";
            AvailableTracks.Add(trackName);
        }
        AvailableTracks.Add("+ Add to New Track");

        SelectedTrack = AvailableTracks.FirstOrDefault() ?? "+ Add to New Track";
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredChannels.Clear();
        var q = string.IsNullOrWhiteSpace(FilterText)
            ? _allMetadata
            : _allMetadata.Where(c => c.Mnemonic.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                      c.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));

        foreach (var c in q)
        {
            FilteredChannels.Add(c);
        }

        if (SelectedChannel == null && FilteredChannels.Count > 0)
        {
            SelectedChannel = FilteredChannels.First();
        }
    }

    partial void OnSelectedChannelChanged(LogChannelMetadata? value)
    {
        if (value == null) return;
        Mnemonic = value.Mnemonic;
        ChannelTitle = !string.IsNullOrWhiteSpace(value.Name) ? value.Name : value.Mnemonic;
        Unit = value.Unit;

        if (value.MaxValue > value.MinValue)
        {
            AxisMin = Math.Floor(value.MinValue);
            AxisMax = Math.Ceiling(value.MaxValue);
        }

        if (value.Mnemonic.Contains("RIG_STATE", StringComparison.OrdinalIgnoreCase))
        {
            ColorCodeAsRigState = true;
        }
    }

    [RelayCommand]
    private void PickLineColor()
    {
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, LineColorHex);
        if (ok) LineColorHex = hex;
    }

    [RelayCommand]
    private void PickPointFillColor()
    {
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, PointFillColorHex);
        if (ok) PointFillColorHex = hex;
    }

    [RelayCommand]
    private void PickPointBorderColor()
    {
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, PointBorderColorHex);
        if (ok) PointBorderColorHex = hex;
    }

    [RelayCommand]
    private void PickFontColor()
    {
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, FontColorHex);
        if (ok) FontColorHex = hex;
    }

    [RelayCommand]
    private void PickMajorGridColor()
    {
        var (ok, hex) = ColorPickerDialog.Show(Application.Current?.MainWindow, MajorGridColorHex);
        if (ok) MajorGridColorHex = hex;
    }

    // Test hook / UI handler
    public Action<string, string>? ShowWarningHandler { get; set; }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Mnemonic))
        {
            if (ShowWarningHandler != null) ShowWarningHandler("Please select or enter a valid channel mnemonic.", "Validation");
            else if (Application.Current != null) MessageBox.Show("Please select or enter a valid channel mnemonic.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Determine or create target track
        VHTrack targetTrack;
        if (SelectedTrack == "+ Add to New Track" || string.IsNullOrWhiteSpace(SelectedTrack))
        {
            targetTrack = new VHTrack
            {
                ID = Guid.NewGuid().ToString(),
                Title = !string.IsNullOrWhiteSpace(ChannelTitle) ? ChannelTitle : Mnemonic,
                TrackType = enumRTTrackType.Regular,
                Visible = true,
                Width = 2,
                DisplayOrder = _console.Tracks.Count + 1
            };
            _console.Tracks.Add(targetTrack);
        }
        else
        {
            targetTrack = _console.Tracks.FirstOrDefault(t => t.Title == SelectedTrack || $"Track {_console.Tracks.IndexOf(t) + 1}" == SelectedTrack)
                       ?? _console.Tracks.FirstOrDefault()
                       ?? new VHTrack();
        }

        TargetTrackInstance = targetTrack;

        var channel = new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = targetTrack.ID,
            Mnemonic = Mnemonic.Trim(),
            Name = ChannelTitle.Trim(),
            Title = ChannelTitle.Trim(),
            Unit = Unit.Trim(),
            SeriesType = SelectedSeriesType,
            LineWidth = LineWidth,
            LineColor = LineColorHex,
            LineStyle = SelectedLineStyle,
            PointStyle = SelectedPointStyle,
            PointWidth = PointSize,
            PointHeight = PointSize,
            PointFillColor = PointFillColorHex,
            PointBorderColor = PointBorderColorHex,
            ColorCodeAsRigState = ColorCodeAsRigState,
            TitleFontFontName = SelectedFont,
            TitleFontFontSize = FontSize,
            TitleFontFontColor = FontColorHex,
            TitleFontFontBold = FontBold,
            TitleFontFontItalic = FontItalic,
            Visible = true,
            objXAxis = new RTAxis
            {
                ID = Guid.NewGuid().ToString(),
                TrackID = targetTrack.ID,
                Mnemonic = Mnemonic.Trim(),
                Title = ChannelTitle.Trim(),
                Unit = Unit.Trim(),
                AutoScale = AutoScale,
                Min = AxisMin,
                Max = AxisMax,
                Inverted = Inverted,
                Logarithmic = Logarithmic,
                ShowMajorGrids = ShowMajorGrids,
                MajorLineColor = MajorGridColorHex,
                ShowMinorGrids = ShowMinorGrids,
                LabelFontName = SelectedFont,
                LabelFontSize = FontSize,
                LabelFontColor = FontColorHex,
                LabelFontBold = FontBold,
                LabelFontItalic = FontItalic,
                Visible = true
            }
        };

        targetTrack.Channels.Add(channel);
        CreatedChannel = channel;

        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
