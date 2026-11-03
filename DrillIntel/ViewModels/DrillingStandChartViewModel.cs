using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class DrillingStandChartViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repository;
    private DrillingStandPlotData _data = new();
    private bool _isApplyingPreset;
    private string _overviewMnemonic = "TIME";

    [ObservableProperty] private bool _isTimeLog = true;
    [ObservableProperty] private DateTime _logMinDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _logMaxDate = DateTime.MinValue;
    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;
    [ObservableProperty] private string _fromDateTimeText = string.Empty;
    [ObservableProperty] private string _toDateTimeText = string.Empty;
    [ObservableProperty] private string _selectedPreset = "Entire Log";

    [ObservableProperty] private bool _rigStateView;
    [ObservableProperty] private double _averageTime;
    [ObservableProperty] private double _averageDayTime;
    [ObservableProperty] private double _averageNightTime;

    [ObservableProperty] private double _trackBarMin;
    [ObservableProperty] private double _trackBarMax = 100;
    [ObservableProperty] private double _trackBarSelectionStart;
    [ObservableProperty] private double _trackBarSelectionEnd = 100;

    [ObservableProperty] private string _status = "Loading drilling stands…";
    [ObservableProperty] private ObservableCollection<double> _depths = new();
    [ObservableProperty] private PointCollection _trackBarOverviewPoints = new();
    [ObservableProperty] private string _trackBarOverviewChannelName = "Main Stand Time";
    [ObservableProperty] private Brush _trackBarOverviewBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));
    [ObservableProperty] private double _trackBarOverviewMin;
    [ObservableProperty] private double _trackBarOverviewMax = 1;

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

    public DataTable StandData => _data.standData;
    public DataTable RigStateData => _data.rigStateData;
    public DrillingStandPlotData Data => _data;
    public event Action? ChartNeedsRepaint;

    public DrillingStandChartViewModel(ProjectSession session)
    {
        _session = session;
        _repository = new WellDataRepository(session);
    }

    public async Task InitializeAsync()
    {
        var logs = await _repository.GetTimeLogsAsync();
        var log = logs.FirstOrDefault(x => x.PrimaryLog) ?? logs.FirstOrDefault();
        if (log == null) { Status = "No time log is available for this project."; return; }

        var range = await new ChartDataService(_session).GetTimeLogDateRangeAsync(log.ObjectID);
        if (range.HasValue && range.Value.MinDate > DateTime.MinValue)
        {
            LogMinDate = range.Value.MinDate;
            LogMaxDate = range.Value.MaxDate;
            TrackBarMin = LogMinDate.ToOADate();
            TrackBarMax = LogMaxDate.ToOADate();
            FromDate = LogMinDate;
            ToDate = LogMaxDate;
            FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");
            TrackBarSelectionStart = TrackBarMin;
            TrackBarSelectionEnd = TrackBarMax;
        }
        else
        {
            FromDate = DateTime.Today.AddDays(-7);
            ToDate = DateTime.Now;
            FromDateTimeText = FromDate.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = ToDate.ToString("dd-MM-yyyy HH:mm");
            TrackBarMin = FromDate.ToOADate();
            TrackBarMax = ToDate.ToOADate();
            TrackBarSelectionStart = TrackBarMin;
            TrackBarSelectionEnd = TrackBarMax;
        }

        string tableName = !string.IsNullOrWhiteSpace(log.__dataTableName)
            ? log.__dataTableName
            : log.getDataTableName(_session.GetDataService());
        await LoadAsync(log.WellID, tableName);
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (TryParseDateTime(FromDateTimeText, out var dtFrom))
        {
            FromDate = dtFrom;
            if (LogMinDate != DateTime.MinValue && dtFrom.ToOADate() >= TrackBarMin)
                TrackBarSelectionStart = dtFrom.ToOADate();
        }
        if (TryParseDateTime(ToDateTimeText, out var dtTo))
        {
            ToDate = dtTo;
            if (LogMaxDate != DateTime.MinValue && dtTo.ToOADate() <= TrackBarMax)
                TrackBarSelectionEnd = dtTo.ToOADate();
        }
        SelectedPreset = "Custom Range";

        var logs = await _repository.GetTimeLogsAsync();
        var log = logs.FirstOrDefault(x => x.PrimaryLog) ?? logs.FirstOrDefault();
        if (log != null)
        {
            string tableName = !string.IsNullOrWhiteSpace(log.__dataTableName)
                ? log.__dataTableName
                : log.getDataTableName(_session.GetDataService());
            await LoadAsync(log.WellID, tableName);
        }
    }

    [RelayCommand]
    public async Task ApplyPresetAsync(string presetName)
    {
        if (_isApplyingPreset) return;
        _isApplyingPreset = true;
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
                    from = LogMinDate != DateTime.MinValue ? LogMinDate : to.AddDays(-7);
                    to = maxRef;
                    break;
            }

            if (LogMinDate != DateTime.MinValue && from < LogMinDate) from = LogMinDate;
            if (to > maxRef) to = maxRef;

            FromDate = from;
            ToDate = to;
            FromDateTimeText = from.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = to.ToString("dd-MM-yyyy HH:mm");

            if (LogMinDate != DateTime.MinValue && LogMaxDate != DateTime.MinValue)
            {
                TrackBarSelectionStart = from.ToOADate();
                TrackBarSelectionEnd = to.ToOADate();
            }

            SelectedPreset = presetName;

            var logs = await _repository.GetTimeLogsAsync();
            var log = logs.FirstOrDefault(x => x.PrimaryLog) ?? logs.FirstOrDefault();
            if (log != null)
            {
                string tableName = !string.IsNullOrWhiteSpace(log.__dataTableName)
                    ? log.__dataTableName
                    : log.getDataTableName(_session.GetDataService());
                await LoadAsync(log.WellID, tableName);
            }
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    public async Task RangeTrackBarChangedAsync()
    {
        if (_isApplyingPreset) return;

        if (IsTimeLog && TrackBarSelectionStart > 1000 && TrackBarSelectionEnd > 1000)
        {
            var startDt = DateTime.FromOADate(TrackBarSelectionStart);
            var endDt = DateTime.FromOADate(TrackBarSelectionEnd);

            FromDate = startDt;
            ToDate = endDt;
            FromDateTimeText = startDt.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = endDt.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Custom Range";

            var logs = await _repository.GetTimeLogsAsync();
            var log = logs.FirstOrDefault(x => x.PrimaryLog) ?? logs.FirstOrDefault();
            if (log != null)
            {
                string tableName = !string.IsNullOrWhiteSpace(log.__dataTableName)
                    ? log.__dataTableName
                    : log.getDataTableName(_session.GetDataService());
                await LoadAsync(log.WellID, tableName);
            }
        }
    }

    private async Task LoadAsync(string wellId, string table)
    {
        try
        {
            Status = "Loading drilling stands…";
            _data = await Task.Run(() => new DrillingStandPlot(_session.GetDataService()).GetData(wellId, table, FromDate, ToDate));
            AverageTime = _data.AvgTime; AverageDayTime = _data.AvgDTime; AverageNightTime = _data.AvgNTime;
            Depths = new ObservableCollection<double>(_data.standData.Rows.Cast<DataRow>().Select(r => Convert.ToDouble(r["Depth"], CultureInfo.InvariantCulture)));
            UpdateOverview();
            Status = $"{Depths.Count} drilling stands";
            OnPropertyChanged(nameof(StandData)); OnPropertyChanged(nameof(RigStateData)); OnPropertyChanged(nameof(Data));
            ChartNeedsRepaint?.Invoke();
        }
        catch (Exception ex) { Status = $"Unable to load drilling stands: {ex.Message}"; }
    }

    partial void OnRigStateViewChanged(bool value) => ChartNeedsRepaint?.Invoke();

    public VHTrackConsole CreateDataSelectorConsole()
    {
        var console = VHTrackConsole.CreateConsoleFromIndexType(enumIndexType.Deptlog);
        var track = new VHTrack { ID = "DRLG_STAND_TRACK", Title = "Drilling Stand", DisplayOrder = 0 };
        track.Channels.Add(new VHTrackChannel { ID = "MAIN_TIME", TrackID = track.ID, Name = "Main Stand Time", Title = "Main Stand Time", Mnemonic = "TIME" });
        track.Channels.Add(new VHTrackChannel { ID = "OFFSET_TIME", TrackID = track.ID, Name = "Offset Stand Time", Title = "Offset Stand Time", Mnemonic = "OFFSET_TIME" });
        track.Channels.Add(new VHTrackChannel { ID = "DEPTH", TrackID = track.ID, Name = "Depth", Title = "Depth", Mnemonic = "DEPTH" });
        console.Tracks.Add(track);
        return console;
    }

    public void SetOverviewChannel(string mnemonic)
    {
        _overviewMnemonic = mnemonic;
        TrackBarOverviewChannelName = mnemonic switch { "OFFSET_TIME" => "Offset Stand Time", "DEPTH" => "Stand Depth", _ => "Main Stand Time" };
        UpdateOverview();
    }

    private void UpdateOverview()
    {
        var points = new PointCollection();
        var values = new List<double>();
        for (int i = 0; i < _data.standData.Rows.Count; i++)
        {
            var row = _data.standData.Rows[i];
            double value = _overviewMnemonic switch
            {
                "OFFSET_TIME" => Convert.ToDouble(row["OffsetTime"], CultureInfo.InvariantCulture),
                "DEPTH" => Convert.ToDouble(row["Depth"], CultureInfo.InvariantCulture),
                _ => Convert.ToDouble(row["Time"], CultureInfo.InvariantCulture)
            };
            values.Add(value);

            double xCoord;
            if (row.Table.Columns.Contains("FromDate") && row["FromDate"] is DateTime dt && dt > DateTime.MinValue)
            {
                xCoord = dt.ToOADate();
            }
            else
            {
                xCoord = TrackBarMin + i;
            }
            points.Add(new System.Windows.Point(xCoord, value));
        }
        TrackBarOverviewPoints = points;
        TrackBarOverviewMin = values.Count == 0 ? 0 : values.Min();
        TrackBarOverviewMax = values.Count == 0 || values.Max() <= TrackBarOverviewMin ? TrackBarOverviewMin + 1 : values.Max();
    }

    public static bool TryParseDateTime(string? text, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string[] formats = new[]
        {
            "dd-MM-yyyy HH:mm",
            "dd-MM-yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd-MMM-yyyy HH:mm",
            "dd-MMM-yyyy HH:mm:ss",
            "d-MMM-yyyy HH:mm",
            "d-MMM-yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "dd-MM-yyyy",
            "dd/MM/yyyy",
            "yyyy-MM-dd",
            "M/d/yyyy h:mm tt",
            "M/d/yyyy h:mm:ss tt"
        };

        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            return true;
        if (DateTime.TryParse(text.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            return true;
        return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
