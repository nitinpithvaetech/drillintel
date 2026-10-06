using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;
using DrillIntel.Views;

namespace DrillIntel.ViewModels;

/// <summary>
/// Master ViewModel for the RigStateDocument view.
/// Coordinates the VHTrackConsole domain model, ChartDataService data fetching,
/// date/depth index navigation, real-time scrolling, and dialog interactions.
/// </summary>
public partial class RigStateDocumentViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IChartDataService _chartDataService;
    private readonly IWellDataRepository _repository;
    private readonly IDocTemplateRepository _templateRepo;

    public event Action? ChartNeedsRepaint;

    // Test hooks / UI Handlers
    public Func<AddTrackChannelViewModel, bool?>? OpenAddChannelDialogHandler { get; set; }
    public Func<TrackPropertiesViewModel, bool?>? OpenTrackPropertiesDialogHandler { get; set; }
    public Func<SaveDocumentAsViewModel, bool?>? OpenSaveAsDialogHandler { get; set; }
    public Func<RigStateDocumentManagerViewModel, bool?>? OpenDocumentManagerDialogHandler { get; set; }
    public Func<DataSelectorViewModel, bool?>? OpenDataSelectorDialogHandler { get; set; }

    [ObservableProperty]
    private string _currentTemplateId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _documentName = "Default Rig State Overview";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public string WindowTitle => $"Rig State Document — {DocumentName}{(IsDirty ? " *" : "")}";

    public ObservableCollection<DocTemplateItem> SavedDocuments { get; } = new();

    [ObservableProperty]
    private DocTemplateItem? _selectedSavedDocument;

    partial void OnSelectedSavedDocumentChanged(DocTemplateItem? value)
    {
        if (value != null && value.TemplateId != CurrentTemplateId)
        {
            _ = SwitchDocumentAsync(value);
        }
    }

    [ObservableProperty]
    private string _documentTitle = "Rig State Analysis Document";

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _wellboreName = string.Empty;

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _logId = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _loadedPointsCount;

    // VHTrackConsole master model
    public VHTrackConsole ConsoleModel { get; }

    public ObservableCollection<VHTrack> DisplayTracks { get; } = new();

    public void SyncDisplayTracks()
    {
        DisplayTracks.Clear();
        foreach (var t in ConsoleModel.Tracks)
        {
            DisplayTracks.Add(t.GetCopy());
        }
        OnPropertyChanged(nameof(DisplayTracks));
    }

    // Navigation & Range Presets
    public ObservableCollection<string> RangePresets { get; } = new()
    {
        "Last 1 Hour",
        "Last 4 Hours",
        "Last 12 Hours",
        "Last 24 Hours",
        "Last 48 Hours",
        "Last 7 Days",
        "Entire Log",
        "Custom Range"
    };

    [ObservableProperty]
    private string _selectedPreset = "Last 24 Hours";

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _fromTime;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private DateTime? _toTime;

    [ObservableProperty]
    private double _fromDepth = 0;

    [ObservableProperty]
    private double _toDepth = 5000;

    [ObservableProperty]
    private bool _isTimeLog = true;

    [ObservableProperty]
    private bool _isRealTime = true;

    [ObservableProperty]
    private enumTrackOrientation _trackOrientation = enumTrackOrientation.Vertical;

    [ObservableProperty]
    private int _maxPoints = 10000;

    [ObservableProperty]
    private bool _showLegend = true;

    public string OrientationText => TrackOrientation == enumTrackOrientation.Vertical ? "Vertical" : "Horizontal";
    public string OrientationIcon => TrackOrientation == enumTrackOrientation.Vertical ? "ViewColumnOutline" : "ViewAgendaOutline";

    [ObservableProperty]
    private DateTime _logMinDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _logMaxDate = DateTime.MinValue;

    // TrackBar DataSelector Properties
    [ObservableProperty]
    private double _trackBarMin = 0.0;

    [ObservableProperty]
    private double _trackBarMax = 100.0;

    [ObservableProperty]
    private double _trackBarSelectionStart = 0.0;

    [ObservableProperty]
    private double _trackBarSelectionEnd = 25.0;

    [ObservableProperty]
    private string _selectedTrackBarPeriod = "2 Hours";

    // TrackBar Background Overview Plot Properties
    [ObservableProperty]
    private string _selectedOverviewChannelMnemonic = string.Empty;

    [ObservableProperty]
    private PointCollection? _trackBarOverviewPoints;

    [ObservableProperty]
    private string _trackBarOverviewChannelName = string.Empty;

    [ObservableProperty]
    private Brush _trackBarOverviewBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));

    // Telemetry & Cursor Info
    [ObservableProperty]
    private string _cursorIndexText = "--";

    [ObservableProperty]
    private string _cursorRigStateText = "None";

    [ObservableProperty]
    private string _cursorRigStateColorHex = "#9E9E9E";

    public void UpdateCursorTelemetry(string indexText, string rigStateText, string rigStateColorHex)
    {
        CursorIndexText = indexText;
        CursorRigStateText = rigStateText;
        CursorRigStateColorHex = rigStateColorHex;
    }

    public void ResetCursorTelemetry()
    {
        CursorIndexText = "--";
        CursorRigStateText = "None";
        CursorRigStateColorHex = "#9E9E9E";
    }

    // Cached Result Data
    public ChartDataSeriesResult? CurrentDataResult { get; private set; }
    public List<RigStateInterval> CurrentRigStateIntervals { get; private set; } = new();

    public RigStateDocumentViewModel(
        ProjectSession session,
        IChartDataService? chartDataService = null,
        IWellDataRepository? repository = null,
        VHTrackConsole? console = null,
        IDocTemplateRepository? templateRepo = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _chartDataService = chartDataService ?? new ChartDataService(_session);
        _repository = repository ?? new WellDataRepository(_session);
        _templateRepo = templateRepo ?? new DocTemplateRepository(_session);

        ConsoleModel = console ?? CreateDefaultConsole();
        TrackOrientation = ConsoleModel.TrackOrientation;
        SyncDisplayTracks();
        IsTimeLog = ConsoleModel.IndexType == enumIndexType.TimeLog;
        IsRealTime = ConsoleModel.realTime;
        ShowLegend = ConsoleModel.ShowLegend;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading log metadata...";
        try
        {
            await ReloadSavedDocumentsAsync();

            var well = await _repository.GetProjectWellAsync();
            if (well != null)
            {
                WellName = well.name;
                ConsoleModel.WellID = well.ObjectID;
            }

            var timeLogs = await _repository.GetTimeLogsAsync();
            var targetLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();

            if (targetLog != null)
            {
                LogId = targetLog.ObjectID;
                LogName = !string.IsNullOrWhiteSpace(targetLog.nameLog) ? targetLog.nameLog : "TimeLog";
                ConsoleModel.DataSource.TimeLogID = targetLog.ObjectID;
                ConsoleModel.DataSource.DatasourceType = enumRTDataSourceType.TimeLog;

                // Query log date range
                var dateRange = await _chartDataService.GetTimeLogDateRangeAsync(LogId);
                if (dateRange.HasValue)
                {
                    LogMinDate = dateRange.Value.MinDate;
                    LogMaxDate = dateRange.Value.MaxDate;
                }

                // If no template is loaded yet, load default template from repository
                if (string.IsNullOrEmpty(CurrentTemplateId))
                {
                    var defaultTemplate = await _templateRepo.EnsureDefaultRigStateTemplateAsync();
                    if (defaultTemplate != null)
                    {
                        await LoadFromTemplateAsync(defaultTemplate, refreshData: false);
                    }
                }

                // Load initial window anchored to log dates
                await ApplyPresetAsync(SelectedPreset);
            }
            else
            {
                StatusMessage = "No logs found in this project. Please import a timelog or depthlog.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private VHTrackConsole CreateDefaultConsole()
    {
        var c = new VHTrackConsole
        {
            ID = Guid.NewGuid().ToString(),
            Name = "Rig State Analysis Console",
            IndexType = enumIndexType.TimeLog,
            TrackOrientation = enumTrackOrientation.Vertical,
            realTime = true,
            crossHair = true
        };

        // Track 1: Depth Track (Bit Depth & Hole Depth)
        var depthTrack = new VHTrack
        {
            ID = Guid.NewGuid().ToString(),
            Title = "Depth",
            TrackType = enumRTTrackType.Regular,
            Width = 1.5,
            DisplayOrder = 1,
            FontName = "Segoe UI",
            FontSize = 9,
            FontColor = "#1565C0",
            Visible = true
        };

        depthTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = depthTrack.ID,
            Mnemonic = "BIT_DEPTH",
            Title = "Bit Depth",
            Unit = "m",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#1976D2",
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "BIT_DEPTH", Title = "Bit Depth", Unit = "m", AutoScale = true, Visible = true }
        });

        depthTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = depthTrack.ID,
            Mnemonic = "HOLE_DEPTH",
            Title = "Hole Depth",
            Unit = "m",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1,
            LineColor = "#90CAF9",
            LineStyle = enumRTLineStyle.Dash,
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "HOLE_DEPTH", Title = "Hole Depth", Unit = "m", AutoScale = true, Visible = true }
        });

        // Track 2: Rig State Track
        var rsTrack = new VHTrack
        {
            ID = Guid.NewGuid().ToString(),
            Title = "Rig State",
            TrackType = enumRTTrackType.Regular,
            Width = 1.2,
            DisplayOrder = 2,
            FontName = "Segoe UI",
            FontSize = 9,
            FontColor = "#6A1B9A",
            Visible = true
        };

        rsTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = rsTrack.ID,
            Mnemonic = "RIG_STATE",
            Title = "Rig State",
            Unit = "code",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#8E24AA",
            ColorCodeAsRigState = true,
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "RIG_STATE", Title = "Rig State", Unit = "code", Min = 0, Max = 30, AutoScale = false, Visible = true }
        });

        // Track 3: Drilling Mechanics (Hookload & Torque)
        var mechTrack = new VHTrack
        {
            ID = Guid.NewGuid().ToString(),
            Title = "Drilling Mechanics",
            TrackType = enumRTTrackType.Regular,
            Width = 2.2,
            DisplayOrder = 3,
            FontName = "Segoe UI",
            FontSize = 9,
            FontColor = "#2E7D32",
            Visible = true
        };

        mechTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = mechTrack.ID,
            Mnemonic = "HOOK_LOAD",
            Title = "Hook Load",
            Unit = "klbf",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 2,
            LineColor = "#388E3C",
            ColorCodeAsRigState = true,
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "HOOK_LOAD", Title = "Hook Load", Unit = "klbf", AutoScale = true, Visible = true }
        });

        mechTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = mechTrack.ID,
            Mnemonic = "TORQUE",
            Title = "Surface Torque",
            Unit = "ft-lbf",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#F57C00",
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "TORQUE", Title = "Torque", Unit = "ft-lbf", AutoScale = true, Visible = true }
        });

        // Track 4: Hydraulics (RPM & SPP)
        var hydTrack = new VHTrack
        {
            ID = Guid.NewGuid().ToString(),
            Title = "RPM & Pressure",
            TrackType = enumRTTrackType.Regular,
            Width = 2.0,
            DisplayOrder = 4,
            FontName = "Segoe UI",
            FontSize = 9,
            FontColor = "#D84315",
            Visible = true
        };

        hydTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = hydTrack.ID,
            Mnemonic = "RPM",
            Title = "Rotary RPM",
            Unit = "rpm",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#00897B",
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "RPM", Title = "RPM", Unit = "rpm", AutoScale = true, Visible = true }
        });

        hydTrack.Channels.Add(new VHTrackChannel
        {
            ID = Guid.NewGuid().ToString(),
            TrackID = hydTrack.ID,
            Mnemonic = "SPP",
            Title = "Pump Pressure",
            Unit = "psi",
            SeriesType = enumRTSeriesStyle.Line,
            LineWidth = 1.5,
            LineColor = "#E53935",
            Visible = true,
            objXAxis = new RTAxis { Mnemonic = "SPP", Title = "SPP", Unit = "psi", AutoScale = true, Visible = true }
        });

        c.Tracks.Add(depthTrack);
        c.Tracks.Add(rsTrack);
        c.Tracks.Add(mechTrack);
        c.Tracks.Add(hydTrack);

        return c;
    }

    partial void OnSelectedPresetChanged(string value)
    {
        if (value != "Custom Range")
        {
            _ = ApplyPresetAsync(value);
        }
    }

    [RelayCommand]
    public async Task ApplyPresetAsync(string presetName)
    {
        IsLoading = true;
        StatusMessage = $"Calculating {presetName} range...";
        try
        {
            DateTime maxRef = LogMaxDate != DateTime.MinValue ? LogMaxDate : DateTime.Now;
            DateTime to = maxRef;
            DateTime from = to.AddHours(-24);

            switch (presetName)
            {
                case "Last 1 Hour":
                    from = to.AddHours(-1);
                    break;
                case "Last 4 Hours":
                    from = to.AddHours(-4);
                    break;
                case "Last 12 Hours":
                    from = to.AddHours(-12);
                    break;
                case "Last 24 Hours":
                    from = to.AddHours(-24);
                    break;
                case "Last 48 Hours":
                    from = to.AddHours(-48);
                    break;
                case "Last 7 Days":
                    from = to.AddDays(-7);
                    break;
                case "Entire Log":
                    from = LogMinDate != DateTime.MinValue ? LogMinDate : DateTime.MinValue;
                    to = LogMaxDate != DateTime.MinValue ? LogMaxDate : DateTime.MaxValue;
                    break;
            }

            if (LogMinDate != DateTime.MinValue && from < LogMinDate && presetName != "Entire Log")
            {
                from = LogMinDate;
            }

            FromDate = from != DateTime.MinValue ? from.Date : null;
            FromTime = from != DateTime.MinValue ? from : null;
            ToDate = to != DateTime.MaxValue ? to.Date : null;
            ToTime = to != DateTime.MaxValue ? to : null;

            if (from != DateTime.MinValue) ConsoleModel.currentMinDate = from;
            if (to != DateTime.MaxValue) ConsoleModel.currentMaxDate = to;

            await RefreshDataAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        if (string.IsNullOrWhiteSpace(LogId)) return;

        IsLoading = true;
        StatusMessage = "Querying data points from database...";
        try
        {
            // Collect all unique mnemonics across all tracks
            var mnemonics = ConsoleModel.Tracks
                .SelectMany(t => t.Channels)
                .Select(c => c.Mnemonic)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            DateTime? fullFrom = null;
            if (FromDate.HasValue)
            {
                var time = FromTime ?? DateTime.MinValue;
                fullFrom = FromDate.Value.Date.Add(time.TimeOfDay);
                ConsoleModel.currentMinDate = fullFrom.Value;
            }
            else if (LogMinDate != DateTime.MinValue)
            {
                ConsoleModel.currentMinDate = LogMinDate;
            }

            DateTime? fullTo = null;
            if (ToDate.HasValue)
            {
                var time = ToTime ?? DateTime.MaxValue;
                fullTo = ToDate.Value.Date.Add(time.TimeOfDay);
                if (fullTo.Value.Second == 0 && fullTo.Value.Millisecond == 0)
                {
                    fullTo = fullTo.Value.AddSeconds(59).AddMilliseconds(999);
                }
                ConsoleModel.currentMaxDate = fullTo.Value;
            }
            else if (LogMaxDate != DateTime.MinValue)
            {
                ConsoleModel.currentMaxDate = LogMaxDate;
            }

            if (SelectedPreset == "Entire Log")
            {
                fullFrom = null;
                fullTo = null;
            }

            if (IsTimeLog)
            {
                CurrentDataResult = await _chartDataService.GetTimelogDataAsync(
                    ConsoleModel.WellID,
                    ConsoleModel.DataSource.WellboreID,
                    LogId,
                    mnemonics,
                    fullFrom,
                    fullTo,
                    maxPoints: MaxPoints);

                CurrentRigStateIntervals = await _chartDataService.GetRigStateIntervalsAsync(
                    LogId,
                    null,
                    fullFrom,
                    fullTo);
            }
            else
            {
                ConsoleModel.currentMinDepth = FromDepth;
                ConsoleModel.currentMaxDepth = ToDepth;

                CurrentDataResult = await _chartDataService.GetDepthLogDataAsync(
                    ConsoleModel.WellID,
                    ConsoleModel.DataSource.WellboreID,
                    LogId,
                    mnemonics,
                    FromDepth,
                    ToDepth,
                    maxPoints: MaxPoints);
            }

            LoadedPointsCount = CurrentDataResult?.TotalPointCount ?? 0;

            if (CurrentDataResult != null && CurrentDataResult.IndexValues.Count > 0)
            {
                double minOa = CurrentDataResult.IndexValues.Min();
                double maxOa = CurrentDataResult.IndexValues.Max();

                if (IsTimeLog)
                {
                    var startDt = DateTime.FromOADate(minOa);
                    var endDt = DateTime.FromOADate(maxOa);

                    if (LogMinDate == DateTime.MinValue || LogMinDate > startDt) LogMinDate = startDt;
                    if (LogMaxDate == DateTime.MinValue || LogMaxDate < endDt) LogMaxDate = endDt;

                    ConsoleModel.currentMinDate = startDt;
                    ConsoleModel.currentMaxDate = endDt;

                    if (!FromDate.HasValue)
                    {
                        FromDate = startDt.Date;
                        FromTime = startDt;
                    }
                    if (!ToDate.HasValue)
                    {
                        ToDate = endDt.Date;
                        ToTime = endDt;
                    }
                }
                else
                {
                    ConsoleModel.currentMinDepth = minOa;
                    ConsoleModel.currentMaxDepth = maxOa;
                    FromDepth = minOa;
                    ToDepth = maxOa;
                }
            }

            if (IsTimeLog)
            {
                if (LogMinDate != DateTime.MinValue) TrackBarMin = LogMinDate.ToOADate();
                if (LogMaxDate != DateTime.MinValue) TrackBarMax = LogMaxDate.ToOADate();

                if (fullFrom.HasValue) TrackBarSelectionStart = fullFrom.Value.ToOADate();
                else if (LogMinDate != DateTime.MinValue) TrackBarSelectionStart = LogMinDate.ToOADate();

                if (fullTo.HasValue) TrackBarSelectionEnd = fullTo.Value.ToOADate();
                else if (LogMaxDate != DateTime.MinValue) TrackBarSelectionEnd = LogMaxDate.ToOADate();
            }
            else
            {
                TrackBarMin = ConsoleModel.currentMinDepth;
                TrackBarMax = ConsoleModel.currentMaxDepth;
                TrackBarSelectionStart = FromDepth;
                TrackBarSelectionEnd = ToDepth;
            }

            // Update channel scale min/max for track legends
            foreach (var track in ConsoleModel.Tracks)
            {
                foreach (var channel in track.Channels)
                {
                    if (!channel.objXAxis.AutoScale && channel.objXAxis.Max > channel.objXAxis.Min)
                    {
                        channel.EffectiveMin = channel.objXAxis.Min;
                        channel.EffectiveMax = channel.objXAxis.Max;
                    }
                    else if (CurrentDataResult != null &&
                             (CurrentDataResult.ChannelValues.TryGetValue(channel.Mnemonic, out var cVals) ||
                              CurrentDataResult.ChannelValues.TryGetValue(channel.Mnemonic.Trim(), out cVals)) &&
                             cVals != null && cVals.Count > 0)
                    {
                        double cMin = cVals.Min();
                        double cMax = cVals.Max();
                        if (Math.Abs(cMax - cMin) < 1e-6)
                        {
                            cMin -= 1.0;
                            cMax += 1.0;
                        }
                        channel.EffectiveMin = cMin;
                        channel.EffectiveMax = cMax;
                    }
                    else
                    {
                        channel.EffectiveMin = channel.objXAxis.Min;
                        channel.EffectiveMax = channel.objXAxis.Max;
                    }
                }
            }
            SyncDisplayTracks();
            await UpdateTrackBarOverviewAsync();

            StatusMessage = $"Loaded {LoadedPointsCount:N0} samples across {ConsoleModel.Tracks.Count} tracks.";
            ChartNeedsRepaint?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Query error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task UpdateTrackBarOverviewAsync()
    {
        var allChannels = ConsoleModel.Tracks.SelectMany(t => t.Channels).ToList();
        if (allChannels.Count == 0 && string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic))
        {
            TrackBarOverviewPoints = null;
            TrackBarOverviewChannelName = string.Empty;
            return;
        }

        VHTrackChannel? targetChannel = null;
        if (!string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic))
        {
            targetChannel = allChannels.FirstOrDefault(c =>
                string.Equals(c.Mnemonic, SelectedOverviewChannelMnemonic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Title, SelectedOverviewChannelMnemonic, StringComparison.OrdinalIgnoreCase));
        }

        if (targetChannel == null)
        {
            targetChannel = allChannels.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Mnemonic));
            if (targetChannel != null)
            {
                SelectedOverviewChannelMnemonic = targetChannel.Mnemonic;
            }
        }

        string mnemonic = targetChannel?.Mnemonic ?? SelectedOverviewChannelMnemonic;
        if (string.IsNullOrWhiteSpace(mnemonic))
        {
            TrackBarOverviewPoints = null;
            TrackBarOverviewChannelName = string.Empty;
            return;
        }

        string channelTitle = !string.IsNullOrWhiteSpace(targetChannel?.Title) ? targetChannel.Title : mnemonic;
        string channelUnit = targetChannel?.Unit ?? string.Empty;
        TrackBarOverviewChannelName = !string.IsNullOrWhiteSpace(channelUnit)
            ? $"{channelTitle} ({channelUnit})"
            : channelTitle;

        TrackBarOverviewBrush = ParseColorBrush(targetChannel?.LineColor);

        ChartDataSeriesResult? overviewData = null;
        if (_chartDataService != null && !string.IsNullOrWhiteSpace(LogId))
        {
            try
            {
                if (IsTimeLog)
                {
                    overviewData = await _chartDataService.GetTimelogDataAsync(
                        ConsoleModel.WellID,
                        ConsoleModel.DataSource.WellboreID,
                        LogId,
                        new[] { mnemonic },
                        fromDate: null,
                        toDate: null,
                        maxPoints: 800);
                }
                else
                {
                    overviewData = await _chartDataService.GetDepthLogDataAsync(
                        ConsoleModel.WellID,
                        ConsoleModel.DataSource.WellboreID,
                        LogId,
                        new[] { mnemonic },
                        fromDepth: null,
                        toDepth: null,
                        maxPoints: 800);
                }
            }
            catch
            {
            }
        }

        if ((overviewData == null || overviewData.IndexValues.Count == 0) &&
            CurrentDataResult != null && CurrentDataResult.IndexValues.Count > 0)
        {
            overviewData = CurrentDataResult;
        }

        if (overviewData != null && overviewData.IndexValues.Count > 0)
        {
            List<double>? vals = null;
            if (overviewData.ChannelValues.TryGetValue(mnemonic, out var exactVals))
            {
                vals = exactVals;
            }
            else
            {
                var kvp = overviewData.ChannelValues.FirstOrDefault(kv => string.Equals(kv.Key.Trim(), mnemonic.Trim(), StringComparison.OrdinalIgnoreCase));
                vals = kvp.Value;
            }

            if (vals != null && vals.Count == overviewData.IndexValues.Count)
            {
                var pts = new PointCollection(overviewData.IndexValues.Count);
                for (int i = 0; i < overviewData.IndexValues.Count; i++)
                {
                    pts.Add(new System.Windows.Point(overviewData.IndexValues[i], vals[i]));
                }
                if (pts.CanFreeze) pts.Freeze();
                TrackBarOverviewPoints = pts;
                return;
            }
        }

        TrackBarOverviewPoints = null;
    }

    private static Brush ParseColorBrush(string? colorStr)
    {
        if (string.IsNullOrWhiteSpace(colorStr))
        {
            var defBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));
            if (defBrush.CanFreeze) defBrush.Freeze();
            return defBrush;
        }

        try
        {
            var converted = new BrushConverter().ConvertFromString(colorStr);
            if (converted is Brush b)
            {
                if (b.CanFreeze) b.Freeze();
                return b;
            }
        }
        catch
        {
        }

        var fallbackBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));
        if (fallbackBrush.CanFreeze) fallbackBrush.Freeze();
        return fallbackBrush;
    }

    [RelayCommand]
    public async Task OpenDataSelectorAsync()
    {
        var vm = new DataSelectorViewModel(ConsoleModel);
        if (!string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic))
        {
            var match = vm.AvailableSeries.FirstOrDefault(s => s.Contains(SelectedOverviewChannelMnemonic, StringComparison.OrdinalIgnoreCase));
            if (match != null) vm.TargetSeries = match;
            if (vm.AvailableYCoordinates.Contains(SelectedOverviewChannelMnemonic))
                vm.YCoordinate = SelectedOverviewChannelMnemonic;
        }

        bool? result;
        if (OpenDataSelectorDialogHandler != null)
        {
            result = OpenDataSelectorDialogHandler(vm);
        }
        else
        {
            var win = new Views.DataSelectorWindow(vm)
            {
                Owner = Application.Current?.MainWindow
            };
            result = win.ShowDialog();
        }

        if (result == true)
        {
            IsDirty = true;
            if (!string.IsNullOrWhiteSpace(vm.SelectedChannelMnemonic))
            {
                SelectedOverviewChannelMnemonic = vm.SelectedChannelMnemonic;
            }
            await RefreshDataAsync();
        }
    }

    public void OpenDataSelector() => _ = OpenDataSelectorAsync();

    [RelayCommand]
    public async Task RangeTrackBarChangedAsync()
    {
        if (IsTimeLog && TrackBarSelectionStart > 1000)
        {
            var startDt = DateTime.FromOADate(TrackBarSelectionStart);
            var endDt = DateTime.FromOADate(TrackBarSelectionEnd);

            FromDate = startDt.Date;
            FromTime = startDt;
            ToDate = endDt.Date;
            ToTime = endDt;
        }
        else if (!IsTimeLog)
        {
            FromDepth = TrackBarSelectionStart;
            ToDepth = TrackBarSelectionEnd;
        }

        await RefreshDataAsync();
    }

    [RelayCommand]
    public async Task AddChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(LogId))
        {
            MessageBox.Show("Please load a log first before adding channels.", "Add Channel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsLoading = true;
        List<LogChannelMetadata> available;
        try
        {
            available = await _chartDataService.GetAvailableChannelsAsync(LogId, ConsoleModel.DataSource.DatasourceType);
        }
        finally
        {
            IsLoading = false;
        }

        var vm = new AddTrackChannelViewModel(ConsoleModel, available);
        bool? result;

        if (OpenAddChannelDialogHandler != null)
        {
            result = OpenAddChannelDialogHandler(vm);
        }
        else
        {
            var dialog = new AddTrackChannelDialog
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            result = dialog.ShowDialog();
        }

        if (result == true)
        {
            IsDirty = true;
            SyncDisplayTracks();
            await RefreshDataAsync();
        }
    }

    [RelayCommand]
    public async Task OpenTrackPropertiesAsync()
    {
        var vm = new TrackPropertiesViewModel(ConsoleModel);
        bool? result;

        if (OpenTrackPropertiesDialogHandler != null)
        {
            result = OpenTrackPropertiesDialogHandler(vm);
        }
        else
        {
            var dialog = new TrackPropertiesDialog
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            result = dialog.ShowDialog();
        }

        if (result == true)
        {
            IsDirty = true;
            SyncDisplayTracks();
            await RefreshDataAsync();
        }
    }

    [RelayCommand]
    public void ToggleRealTime()
    {
        IsRealTime = !IsRealTime;
        ConsoleModel.realTime = IsRealTime;
        StatusMessage = IsRealTime ? "Real-time scrolling enabled." : "Real-time scrolling paused.";
    }

    [RelayCommand]
    public void ToggleOrientation()
    {
        TrackOrientation = TrackOrientation == enumTrackOrientation.Vertical
            ? enumTrackOrientation.Horizontal
            : enumTrackOrientation.Vertical;
        ConsoleModel.TrackOrientation = TrackOrientation;
        IsDirty = true;
        OnPropertyChanged(nameof(OrientationText));
        OnPropertyChanged(nameof(OrientationIcon));
        ChartNeedsRepaint?.Invoke();
    }

    [RelayCommand]
    public void ToggleLegend()
    {
        ShowLegend = !ShowLegend;
        ConsoleModel.ShowLegend = ShowLegend;
        IsDirty = true;
        StatusMessage = ShowLegend ? "Chart legend shown." : "Chart legend hidden.";
        ChartNeedsRepaint?.Invoke();
    }

    [RelayCommand]
    public async Task ResetZoomAsync()
    {
        if (SelectedPreset != "Custom Range")
        {
            await ApplyPresetAsync(SelectedPreset);
        }
        else
        {
            await RefreshDataAsync();
        }
    }

    // =========================================================================
    // Document Template Operations (VMX_DOC_TEMPLATES)
    // =========================================================================

    public async Task ReloadSavedDocumentsAsync()
    {
        var list = await _templateRepo.GetTemplatesAsync(DocTemplateItem.TypeRigState);
        SavedDocuments.Clear();
        foreach (var item in list)
        {
            SavedDocuments.Add(item);
        }
        SelectedSavedDocument = SavedDocuments.FirstOrDefault(d => d.TemplateId == CurrentTemplateId);
        OnPropertyChanged(nameof(SavedDocuments));
    }

    public async Task LoadFromTemplateAsync(DocTemplateItem template, bool refreshData = true)
    {
        if (template == null) return;

        CurrentTemplateId = template.TemplateId;
        DocumentName = template.DocumentName;
        DocumentTitle = template.DocumentName;

        var docData = RigStateDocumentData.FromBytes(template.TemplateData);
        if (docData?.ConsoleModel != null)
        {
            ConsoleModel.Tracks.Clear();
            foreach (var t in docData.ConsoleModel.Tracks)
            {
                ConsoleModel.Tracks.Add(t);
            }
            ConsoleModel.TrackOrientation = docData.ConsoleModel.TrackOrientation;
            TrackOrientation = ConsoleModel.TrackOrientation;
            ConsoleModel.IndexType = docData.ConsoleModel.IndexType;
            ConsoleModel.displayResolution = docData.ConsoleModel.displayResolution;
            ConsoleModel.realTime = docData.ConsoleModel.realTime;
            ConsoleModel.crossHair = docData.ConsoleModel.crossHair;

            if (!string.IsNullOrWhiteSpace(docData.SelectedPreset))
                SelectedPreset = docData.SelectedPreset;
            if (docData.MaxPoints > 0)
                MaxPoints = docData.MaxPoints;
            if (docData.FromDate.HasValue) FromDate = docData.FromDate;
            if (docData.FromTime.HasValue) FromTime = docData.FromTime;
            if (docData.ToDate.HasValue) ToDate = docData.ToDate;
            if (docData.ToTime.HasValue) ToTime = docData.ToTime;

            SyncDisplayTracks();
            IsDirty = false;
            OnPropertyChanged(nameof(OrientationText));
            OnPropertyChanged(nameof(OrientationIcon));
            OnPropertyChanged(nameof(WindowTitle));
        }

        SelectedSavedDocument = SavedDocuments.FirstOrDefault(d => d.TemplateId == CurrentTemplateId);

        if (refreshData)
        {
            await RefreshDataAsync();
        }
    }

    public RigStateDocumentData CaptureDocumentData()
    {
        return new RigStateDocumentData
        {
            DocumentName = DocumentName,
            Description = string.Empty,
            ConsoleModel = ConsoleModel,
            SelectedPreset = SelectedPreset,
            FromDate = FromDate,
            FromTime = FromTime,
            ToDate = ToDate,
            ToTime = ToTime,
            MaxPoints = MaxPoints,
            SelectedWellId = ConsoleModel.WellID,
            SelectedLogId = LogId
        };
    }

    [RelayCommand]
    public async Task SaveDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentTemplateId))
        {
            await SaveAsDocumentAsync();
            return;
        }

        var item = await _templateRepo.GetTemplateByIdAsync(DocTemplateItem.TypeRigState, CurrentTemplateId);
        if (item == null)
        {
            item = new DocTemplateItem
            {
                TemplateType = DocTemplateItem.TypeRigState,
                TemplateId = CurrentTemplateId,
                CreatedBy = Environment.UserName,
                CreatedDate = DateTime.Now
            };
        }

        item.DocumentName = DocumentName;
        item.TemplateData = CaptureDocumentData().ToBytes();
        item.ModifiedBy = Environment.UserName;
        item.ModifiedDate = DateTime.Now;

        await _templateRepo.SaveTemplateAsync(item);
        IsDirty = false;
        StatusMessage = $"Saved '{DocumentName}' successfully.";
        await ReloadSavedDocumentsAsync();
    }

    [RelayCommand]
    public async Task SaveAsDocumentAsync()
    {
        var vm = new SaveDocumentAsViewModel(_templateRepo, DocumentName, string.Empty, false);
        bool? res = OpenSaveAsDialogHandler != null ? OpenSaveAsDialogHandler(vm) : ShowSaveAsDialog(vm);

        if (res == true && vm.DialogResult)
        {
            var docData = CaptureDocumentData();
            docData.DocumentName = vm.DocumentName.Trim();
            docData.Description = vm.Description?.Trim() ?? string.Empty;

            var newItem = new DocTemplateItem
            {
                TemplateType = DocTemplateItem.TypeRigState,
                TemplateId = Guid.NewGuid().ToString(),
                DocumentName = vm.DocumentName.Trim(),
                Description = vm.Description?.Trim(),
                TemplateData = docData.ToBytes(),
                IsDefault = vm.IsDefault,
                CreatedBy = Environment.UserName,
                CreatedDate = DateTime.Now,
                ModifiedBy = Environment.UserName,
                ModifiedDate = DateTime.Now
            };

            await _templateRepo.SaveTemplateAsync(newItem);
            CurrentTemplateId = newItem.TemplateId;
            DocumentName = newItem.DocumentName;
            DocumentTitle = newItem.DocumentName;
            IsDirty = false;
            StatusMessage = $"Saved new document '{DocumentName}'.";
            await ReloadSavedDocumentsAsync();
        }
    }

    [RelayCommand]
    public async Task OpenDocumentManagerAsync()
    {
        var vm = new RigStateDocumentManagerViewModel(_templateRepo);
        await vm.InitializeAsync();

        bool? res = OpenDocumentManagerDialogHandler != null
            ? OpenDocumentManagerDialogHandler(vm)
            : ShowDocumentManagerDialog(vm);

        if (res == true && vm.DialogResult && vm.SelectedTemplateForOpen != null)
        {
            await LoadFromTemplateAsync(vm.SelectedTemplateForOpen, refreshData: true);
        }
        await ReloadSavedDocumentsAsync();
    }

    [RelayCommand]
    public async Task NewDocumentAsync()
    {
        var vm = new SaveDocumentAsViewModel(_templateRepo, "New Rig State View", "Custom user track configuration.", false);
        bool? res = OpenSaveAsDialogHandler != null ? OpenSaveAsDialogHandler(vm) : ShowSaveAsDialog(vm);

        if (res == true && vm.DialogResult)
        {
            var defTemplate = await _templateRepo.EnsureDefaultRigStateTemplateAsync();
            var docData = RigStateDocumentData.FromBytes(defTemplate.TemplateData) ?? new RigStateDocumentData();
            docData.DocumentName = vm.DocumentName.Trim();
            docData.Description = vm.Description?.Trim() ?? string.Empty;

            var newItem = new DocTemplateItem
            {
                TemplateType = DocTemplateItem.TypeRigState,
                TemplateId = Guid.NewGuid().ToString(),
                DocumentName = vm.DocumentName.Trim(),
                Description = vm.Description?.Trim(),
                TemplateData = docData.ToBytes(),
                IsDefault = vm.IsDefault,
                CreatedBy = Environment.UserName,
                CreatedDate = DateTime.Now
            };

            await _templateRepo.SaveTemplateAsync(newItem);
            await ReloadSavedDocumentsAsync();
            await LoadFromTemplateAsync(newItem, refreshData: true);
            StatusMessage = $"Created and opened '{DocumentName}'.";
        }
    }

    [RelayCommand]
    public async Task SwitchDocumentAsync(DocTemplateItem? template)
    {
        if (template == null || template.TemplateId == CurrentTemplateId) return;

        if (IsDirty)
        {
            var ans = MessageBox.Show($"Save changes to '{DocumentName}' before switching?", "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (ans == MessageBoxResult.Yes)
            {
                await SaveDocumentAsync();
            }
            else if (ans == MessageBoxResult.Cancel)
            {
                SelectedSavedDocument = SavedDocuments.FirstOrDefault(d => d.TemplateId == CurrentTemplateId);
                return;
            }
        }

        await LoadFromTemplateAsync(template, refreshData: true);
    }

    private bool? ShowSaveAsDialog(SaveDocumentAsViewModel vm)
    {
        var win = new Views.SaveDocumentAsDialog
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow
        };
        vm.RequestClose = () => win.DialogResult = vm.DialogResult;
        return win.ShowDialog();
    }

    private bool? ShowDocumentManagerDialog(RigStateDocumentManagerViewModel vm)
    {
        var win = new Views.RigStateDocumentManagerDialog
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow
        };
        vm.RequestClose = () => win.DialogResult = vm.DialogResult;
        return win.ShowDialog();
    }
}

