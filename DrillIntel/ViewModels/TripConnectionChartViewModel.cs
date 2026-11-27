using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.Data.Objects.DataObjects.TripConnectionProcessor;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.ViewModels;

public partial class TripConnectionChartViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repository;
    private TimeLog? _timeLog;
    private Well? _well;
    private CancellationTokenSource? _rangeCts;
    private string _overviewMnemonic = "BIT_DEPTH";

    [ObservableProperty] private DateTime _logMinDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _logMaxDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;
    [ObservableProperty] private string _fromDateTimeText = string.Empty;
    [ObservableProperty] private string _toDateTimeText = string.Empty;
    [ObservableProperty] private string _selectedPreset = "Entire Log";
    [ObservableProperty] private bool _rigStateView;
    [ObservableProperty] private bool _durationFilterEnabled;
    [ObservableProperty] private double _minimumDurationMinutes = 1;
    [ObservableProperty] private double _maximumDurationMinutes = 5;
    [ObservableProperty] private double _averageTime;
    [ObservableProperty] private double _averageDayTime;
    [ObservableProperty] private double _averageNightTime;
    [ObservableProperty] private double _trackBarMin;
    [ObservableProperty] private double _trackBarMax = 100;
    [ObservableProperty] private double _trackBarSelectionStart;
    [ObservableProperty] private double _trackBarSelectionEnd = 100;
    [ObservableProperty] private string _status = "Loading trip connections…";
    [ObservableProperty] private ObservableCollection<TripConnectionPoint> _points = new();
    [ObservableProperty] private PointCollection _trackBarOverviewPoints = new();
    [ObservableProperty] private List<int>? _trackBarOverviewColors;
    [ObservableProperty] private string _trackBarOverviewChannelName = "Bit Depth";
    [ObservableProperty] private Brush _trackBarOverviewBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));
    [ObservableProperty] private double _trackBarOverviewMin;
    [ObservableProperty] private double _trackBarOverviewMax = 1;
    [ObservableProperty] private bool _isOverviewYAxisInverted = true;
    [ObservableProperty] private bool _colorCodeOverviewAsRigState;

    public ObservableCollection<string> RangePresets { get; } = new()
    {
        "Last 1 Hour", "Last 4 Hours", "Last 12 Hours", "Last 24 Hours",
        "Last 48 Hours", "Last 7 Days", "Entire Log", "Custom Range"
    };

    public Dictionary<int, rigStateItem> RigStates { get; private set; } = new();

    public event Action? ChartNeedsRepaint;

    public string OverviewMnemonic => _overviewMnemonic;

    partial void OnRigStateViewChanged(bool value) => ChartNeedsRepaint?.Invoke();

    public TripConnectionChartViewModel(ProjectSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _repository = new WellDataRepository(session);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var logs = await _repository.GetTimeLogsAsync();
            _timeLog = logs.FirstOrDefault(log => log.PrimaryLog) ?? logs.FirstOrDefault();
            if (_timeLog == null)
            {
                Status = "No time log is available for this project.";
                ChartNeedsRepaint?.Invoke();
                return;
            }

            _well = await _repository.GetProjectWellAsync();
            if (_well == null)
                _well = new Well { ObjectID = _timeLog.WellID, wellDateFormat = Well.wDateFormatLocal };

            var range = await new ChartDataService(_session).GetTimeLogDateRangeAsync(_timeLog.ObjectID);
            if (range.HasValue && range.Value.MinDate > DateTime.MinValue)
            {
                LogMinDate = range.Value.MinDate;
                LogMaxDate = range.Value.MaxDate;
            }
            else
            {
                LogMinDate = DateTime.Today.AddDays(-7);
                LogMaxDate = DateTime.Now;
            }

            TrackBarMin = LogMinDate.ToOADate();
            TrackBarMax = LogMaxDate.ToOADate();
            FromDate = LogMinDate;
            ToDate = LogMaxDate;
            TrackBarSelectionStart = TrackBarMin;
            TrackBarSelectionEnd = TrackBarMax;
            FromDateTimeText = FromDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
            ToDateTimeText = ToDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

            LoadDurationSettings();

            await LoadAsync();
            await LoadOverviewAsync();
        }
        catch (Exception ex)
        {
            Status = $"Unable to initialize trip-in summary: {ex.Message}";
            ChartNeedsRepaint?.Invoke();
        }
    }

    public async Task ApplyPresetAsync(string preset)
    {
        DateTime max = LogMaxDate > DateTime.MinValue ? LogMaxDate : DateTime.Now;
        DateTime to = max;
        DateTime from = preset switch
        {
            "Last 1 Hour" => to.AddHours(-1),
            "Last 4 Hours" => to.AddHours(-4),
            "Last 12 Hours" => to.AddHours(-12),
            "Last 24 Hours" => to.AddHours(-24),
            "Last 48 Hours" => to.AddHours(-48),
            "Last 7 Days" => to.AddDays(-7),
            "Entire Log" => LogMinDate > DateTime.MinValue ? LogMinDate : to.AddDays(-7),
            _ => FromDate
        };
        if (LogMinDate > DateTime.MinValue && from < LogMinDate) from = LogMinDate;
        FromDate = from;
        ToDate = to;
        FromDateTimeText = from.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        ToDateTimeText = to.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        if (LogMinDate > DateTime.MinValue)
        {
            TrackBarSelectionStart = from.ToOADate();
            TrackBarSelectionEnd = to.ToOADate();
        }
        SelectedPreset = preset;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (!DrillingStandChartViewModel.TryParseDateTime(FromDateTimeText, out var from) ||
            !DrillingStandChartViewModel.TryParseDateTime(ToDateTimeText, out var to) || from > to)
        {
            Status = "Enter a valid date range with From earlier than or equal to To.";
            return;
        }
        FromDate = from;
        ToDate = to;
        SelectedPreset = "Custom Range";
        TrackBarSelectionStart = from.ToOADate();
        TrackBarSelectionEnd = to.ToOADate();
        await LoadAsync();
    }

    public async Task RangeTrackBarChangedAsync()
    {
        if (TrackBarSelectionStart < 1000 || TrackBarSelectionEnd < 1000) return;
        FromDate = DateTime.FromOADate(TrackBarSelectionStart);
        ToDate = DateTime.FromOADate(TrackBarSelectionEnd);
        FromDateTimeText = FromDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        ToDateTimeText = ToDate.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        SelectedPreset = "Custom Range";

        _rangeCts?.Cancel();
        var cts = new CancellationTokenSource();
        _rangeCts = cts;
        try
        {
            await Task.Delay(60, cts.Token);
            await LoadAsync(cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    public TripConnectionChartSettings CreateSettingsDraft() => new()
    {
        DurationFilterEnabled = DurationFilterEnabled,
        MinimumDurationMinutes = MinimumDurationMinutes,
        MaximumDurationMinutes = MaximumDurationMinutes
    };

    public bool SaveSettings(TripConnectionChartSettings settings, out string error)
    {
        error = string.Empty;
        if (settings == null)
        {
            error = "Settings were not provided.";
            return false;
        }
        if (double.IsNaN(settings.MinimumDurationMinutes) || double.IsInfinity(settings.MinimumDurationMinutes) || settings.MinimumDurationMinutes < 0 ||
            double.IsNaN(settings.MaximumDurationMinutes) || double.IsInfinity(settings.MaximumDurationMinutes) || settings.MaximumDurationMinutes <= settings.MinimumDurationMinutes)
        {
            error = "Enter a non-negative minimum duration and a maximum duration greater than the minimum.";
            return false;
        }

        var dataService = _session.GetDataService();
        if (dataService == null || !dataService.IsConnectionOpen())
        {
            error = "The project database is unavailable; settings were not saved.";
            return false;
        }

        const string enabledKey = "TripConnectionChart.DurationFilterEnabled";
        const string minKey = "TripConnectionChart.MinimumDurationMinutes";
        const string maxKey = "TripConnectionChart.MaximumDurationMinutes";
        SystemSettings.saveASetting(dataService, enabledKey, settings.DurationFilterEnabled.ToString());
        SystemSettings.saveASetting(dataService, minKey, settings.MinimumDurationMinutes.ToString("R", CultureInfo.InvariantCulture));
        SystemSettings.saveASetting(dataService, maxKey, settings.MaximumDurationMinutes.ToString("R", CultureInfo.InvariantCulture));

        if (!string.Equals(SystemSettings.getASetting(dataService, enabledKey), settings.DurationFilterEnabled.ToString(), StringComparison.OrdinalIgnoreCase) ||
            !double.TryParse(SystemSettings.getASetting(dataService, minKey), NumberStyles.Float, CultureInfo.InvariantCulture, out double savedMin) ||
            !double.TryParse(SystemSettings.getASetting(dataService, maxKey), NumberStyles.Float, CultureInfo.InvariantCulture, out double savedMax) ||
            Math.Abs(savedMin - settings.MinimumDurationMinutes) > 0.000001 ||
            Math.Abs(savedMax - settings.MaximumDurationMinutes) > 0.000001)
        {
            error = "Could not verify saved trip chart settings.";
            return false;
        }

        DurationFilterEnabled = settings.DurationFilterEnabled;
        MinimumDurationMinutes = settings.MinimumDurationMinutes;
        MaximumDurationMinutes = settings.MaximumDurationMinutes;
        return true;
    }

    public Task ReloadAsync() => LoadAsync();

    public void SetOverviewChannel(string mnemonic)
    {
        if (!string.IsNullOrWhiteSpace(mnemonic))
            _overviewMnemonic = mnemonic;
    }

    public Task ReloadOverviewAsync() => LoadOverviewAsync();

    public VHTrackConsole CreateDataSelectorConsole()
    {
        var console = VHTrackConsole.CreateConsoleFromIndexType(enumIndexType.TimeLog);
        var track = new VHTrack { ID = "TRIP_CONNECTION_TRACK", Title = "Trip-In Summary", DisplayOrder = 0 };
        track.Channels.Add(new VHTrackChannel { ID = "BIT_DEPTH", TrackID = track.ID, Name = "Bit Depth", Title = "Bit Depth", Mnemonic = "BIT_DEPTH", Unit = "ft" });
        track.Channels.Add(new VHTrackChannel { ID = "DEPTH", TrackID = track.ID, Name = "Depth", Title = "Depth", Mnemonic = "DEPTH", Unit = "ft" });
        console.Tracks.Add(track);
        return console;
    }

    public async Task<VHTrackConsole> CreateDataSelectorConsoleAsync()
    {
        var console = CreateDataSelectorConsole();
        if (_timeLog == null) return console;
        try
        {
            string table = !string.IsNullOrWhiteSpace(_timeLog.__dataTableName)
                ? _timeLog.__dataTableName
                : _timeLog.getDataTableName(_session.GetDataService());
            if (string.IsNullOrWhiteSpace(table)) return console;
            var names = await Task.Run(() =>
            {
                string escapedTable = table.Replace("]", "]]", StringComparison.Ordinal);
                DataTable columns = _session.GetDataService().GetTable($"PRAGMA table_info([{escapedTable}]);");
                return columns.Rows.Cast<DataRow>()
                    .Select(row => Convert.ToString(row["name"], CultureInfo.InvariantCulture) ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name) && !new[]
                    {
                        "ID", "DATETIME", "DTIM", "TIMEINDEX", "RIG_STATE", "RIGSTATE", "RIG_STATE_COLOR"
                    }.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
            var track = console.Tracks.FirstOrDefault();
            if (track != null)
            {
                foreach (string name in names)
                {
                    if (track.Channels.Any(channel => channel.Mnemonic.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
                    track.Channels.Add(new VHTrackChannel { ID = name, TrackID = track.ID, Name = name, Title = name, Mnemonic = name });
                }
            }
        }
        catch { }
        return console;
    }

    private void LoadDurationSettings()
    {
        try
        {
            var dataService = _session.GetDataService();
            if (dataService == null || !dataService.IsConnectionOpen()) return;
            bool.TryParse(SystemSettings.getASetting(dataService, "TripConnectionChart.DurationFilterEnabled"), out bool enabled);
            DurationFilterEnabled = enabled;
            if (double.TryParse(SystemSettings.getASetting(dataService, "TripConnectionChart.MinimumDurationMinutes"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double min) && min >= 0)
                MinimumDurationMinutes = min;
            if (double.TryParse(SystemSettings.getASetting(dataService, "TripConnectionChart.MaximumDurationMinutes"), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double max) && max > MinimumDurationMinutes)
                MaximumDurationMinutes = max;
        }
        catch { }
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_timeLog == null || _well == null) return;
        try
        {
            Status = "Loading trip connections…";
            var ds = _session.GetDataService();
            if (ds == null || !ds.IsConnectionOpen())
            {
                Points = new ObservableCollection<TripConnectionPoint>();
                Status = "Project database service is unavailable.";
                ChartNeedsRepaint?.Invoke();
                return;
            }

            DateTime dayFrom = new(2000, 1, 1, 6, 0, 0);
            DateTime dayTo = new(2000, 1, 1, 18, 0, 0);
            try
            {
                var settings = new SystemSettings();
                var baseDb = BaseDatabaseProvider.GetBaseDataService();
                settings.LoadSettings(baseDb != null && baseDb.IsConnectionOpen() ? baseDb : ds);
                if (DateTime.TryParse(settings.getSetting(SystemSettings.DayTimeFrom), out var parsedFrom))
                    dayFrom = new DateTime(2000, 1, 1, parsedFrom.Hour, parsedFrom.Minute, parsedFrom.Second);
                if (DateTime.TryParse(settings.getSetting(SystemSettings.DayTimeTo), out var parsedTo))
                    dayTo = new DateTime(2000, 1, 1, parsedTo.Hour, parsedTo.Minute, parsedTo.Second);
            }
            catch { }

            var result = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var loadedRigState = rigState.loadWellRigStateSetup(ds, _timeLog.WellID) ?? new rigState();
                var processor = new TripConnectionLogProcessor
                {
                    objLocalConn = ds,
                    objRigState = loadedRigState,
                    objWell = _well,
                    DayTimeHoursFrom = dayFrom,
                    DayTimeHoursTo = dayTo,
                    ApplyDurationLimits = DurationFilterEnabled,
                    MIN_TIME = MinimumDurationMinutes * 60d,
                    MAX_TIME = MaximumDurationMinutes * 60d
                };
                processor.ProcessPoints(_timeLog, FromDate, ToDate);
                return (Points: processor.connectionPoints.Values.ToList(), processor.LastError, RigStates: loadedRigState.rigStates);
            }, cancellationToken);

            RigStates = result.RigStates;
            if (!string.IsNullOrWhiteSpace(result.LastError))
            {
                Points = new ObservableCollection<TripConnectionPoint>();
                Status = $"Unable to load trip connections: {result.LastError}";
            }
            else
            {
                Points = new ObservableCollection<TripConnectionPoint>(result.Points.OrderBy(point => point.Depth));
                var day = Points.Where(point => point.DayNightStatus == ConnectionLogPoint.cnDayNightTime.DayTime).ToList();
                var night = Points.Where(point => point.DayNightStatus == ConnectionLogPoint.cnDayNightTime.NightTime).ToList();
                AverageTime = Points.Count == 0 ? 0 : Points.Average(point => point.SlipsToSlipsSeconds / 60d);
                AverageDayTime = day.Count == 0 ? 0 : day.Average(point => point.SlipsToSlipsSeconds / 60d);
                AverageNightTime = night.Count == 0 ? 0 : night.Average(point => point.SlipsToSlipsSeconds / 60d);
                Status = Points.Count == 0
                    ? "No trip-in records in this range. Widen the date range or check the timelog."
                    : $"{Points.Count} trip connections";
            }
            ChartNeedsRepaint?.Invoke();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Points = new ObservableCollection<TripConnectionPoint>();
            Status = $"Unable to load trip connections: {ex.Message}";
            ChartNeedsRepaint?.Invoke();
        }
    }

    private async Task LoadOverviewAsync()
    {
        if (_timeLog == null) return;
        try
        {
            var data = await new ChartDataService(_session).GetTimelogDataAsync(
                _timeLog.WellID, _timeLog.WellboreID, _timeLog.ObjectID,
                new[] { _overviewMnemonic, "BIT_DEPTH", "DEPTH" }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), maxPoints: 800);
            string channel = data.ChannelValues.TryGetValue(_overviewMnemonic, out var selected) && selected.Count == data.IndexValues.Count
                ? _overviewMnemonic
                : data.ChannelValues.TryGetValue("BIT_DEPTH", out var bitDepth) && bitDepth.Count == data.IndexValues.Count
                    ? "BIT_DEPTH" : "DEPTH";
            if (!data.ChannelValues.TryGetValue(channel, out var values) || values.Count != data.IndexValues.Count) return;
            var points = new PointCollection(data.IndexValues.Count);
            for (int i = 0; i < data.IndexValues.Count; i++)
                points.Add(new System.Windows.Point(data.IndexValues[i], values[i]));
            if (points.CanFreeze) points.Freeze();
            TrackBarOverviewPoints = points;
            TrackBarOverviewColors = ColorCodeOverviewAsRigState && data.RigStateColors.Count == points.Count
                ? data.RigStateColors
                : null;
            TrackBarOverviewMin = values.Count == 0 ? 0 : values.Min();
            TrackBarOverviewMax = values.Count == 0 || values.Max() <= TrackBarOverviewMin ? TrackBarOverviewMin + 1 : values.Max();
            string title = channel == "BIT_DEPTH" ? "Bit Depth" : channel;
            TrackBarOverviewChannelName = ColorCodeOverviewAsRigState ? $"{title} (Rig State)" : title;
        }
        catch
        {
            TrackBarOverviewPoints = new PointCollection();
            TrackBarOverviewColors = null;
        }
    }
}

public sealed class TripConnectionChartSettings
{
    public bool DurationFilterEnabled { get; set; }
    public double MinimumDurationMinutes { get; set; } = 1;
    public double MaximumDurationMinutes { get; set; } = 5;
}
