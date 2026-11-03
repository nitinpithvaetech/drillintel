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
    private bool _changingRange;
    private string _overviewMnemonic = "TIME";

    [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime _toDate = DateTime.Now;
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
    [ObservableProperty] private string _selectedPresetPeriod = "Entire Log";

    public ObservableCollection<string> PresetPeriods { get; } = new() { "15 Mins", "30 Mins", "1 Hour", "2 Hours", "4 Hours", "8 Hours", "12 Hours", "24 Hours", "Entire Log" };

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
        if (range.HasValue) { FromDate = range.Value.MinDate; ToDate = range.Value.MaxDate; }
        string tableName = !string.IsNullOrWhiteSpace(log.__dataTableName)
            ? log.__dataTableName
            : log.getDataTableName(_session.GetDataService());
        await LoadAsync(log.WellID, tableName);
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
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

    private async Task LoadAsync(string wellId, string table)
    {
        try
        {
            Status = "Loading drilling stands…";
            _data = await Task.Run(() => new DrillingStandPlot(_session.GetDataService()).GetData(wellId, table, FromDate, ToDate));
            AverageTime = _data.AvgTime; AverageDayTime = _data.AvgDTime; AverageNightTime = _data.AvgNTime;
            Depths = new ObservableCollection<double>(_data.standData.Rows.Cast<DataRow>().Select(r => Convert.ToDouble(r["Depth"], CultureInfo.InvariantCulture)));
            _changingRange = true;
            TrackBarMin = 0; TrackBarMax = Math.Max(1, Depths.Count - 1); TrackBarSelectionStart = 0; TrackBarSelectionEnd = TrackBarMax;
            _changingRange = false;
            UpdateOverview();
            Status = $"{Depths.Count} drilling stands";
            OnPropertyChanged(nameof(StandData)); OnPropertyChanged(nameof(RigStateData)); OnPropertyChanged(nameof(Data));
            ChartNeedsRepaint?.Invoke();
        }
        catch (Exception ex) { Status = $"Unable to load drilling stands: {ex.Message}"; }
    }

    partial void OnRigStateViewChanged(bool value) => ChartNeedsRepaint?.Invoke();
    partial void OnTrackBarSelectionStartChanged(double value) => ApplySelectorRange();
    partial void OnTrackBarSelectionEndChanged(double value) => ApplySelectorRange();
    private void ApplySelectorRange()
    {
        if (_changingRange) return;
        ChartNeedsRepaint?.Invoke();
    }

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

    partial void OnSelectedPresetPeriodChanged(string value)
    {
        // Period presets are applied to the selector by the view, matching Rig State chart navigation.
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
            points.Add(new System.Windows.Point(i, value));
        }
        TrackBarOverviewPoints = points;
        TrackBarOverviewMin = values.Count == 0 ? 0 : values.Min();
        TrackBarOverviewMax = values.Count == 0 || values.Max() <= TrackBarOverviewMin ? TrackBarOverviewMin + 1 : values.Max();
    }
}
