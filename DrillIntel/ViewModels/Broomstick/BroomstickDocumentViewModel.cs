using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using DrillIntel.Models.Broomstick;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;
using DrillIntel.Services.Charting;
using DrillIntel.Views;

namespace DrillIntel.ViewModels.Broomstick;

/// <summary>
/// Master ViewModel for the Broomstick Document view.
/// Coordinates the BroomstickDocument model, TeeChart rendering, theoretical plan curves,
/// actual calculated points, BroomstickProfile filtering, and the bottom DataSelector (RangeTrackBarControl).
/// By default, plots DEPTH vs datetime in the DataSelector identical to RigStateDocument.
/// </summary>
public partial class BroomstickDocumentViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IBroomstickTemplateRepository _templateRepo;
    private readonly IBroomstickDataService? _dataService;
    private readonly IChartDataService _chartDataService;
    private readonly IWellDataRepository _wellDataRepo;

    public event Action? ChartNeedsRepaint;

    // UI Dialog Handlers
    public Func<BroomstickDocumentViewModel, bool?>? OpenSetupDialogHandler { get; set; }
    public Func<BroomstickDocumentManagerViewModel, bool?>? OpenTemplateManagerDialogHandler { get; set; }
    public Func<DataSelectorViewModel, bool?>? OpenDataSelectorDialogHandler { get; set; }
    public Func<string, string?>? PromptSaveAsHandler { get; set; }

    // Master Domain Model
    public BroomstickDocument Document { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _documentName = "8.5\" Hookload Drilling";

    [ObservableProperty]
    private string _currentTemplateId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    public string WindowTitle => $"Broomstick Document — {DocumentName}{(IsDirty ? " *" : "")}";

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _wellboreId = string.Empty;

    [ObservableProperty]
    private string _logId = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _loadedPointsCount;

    // Available Theoretical Plans for this Well
    public ObservableCollection<AdnlHookloadPlan> AvailablePlans { get; } = new();

    [ObservableProperty]
    private AdnlHookloadPlan? _selectedPlan;

    partial void OnSelectedPlanChanged(AdnlHookloadPlan? value)
    {
        if (value != null)
        {
            Document.PlanId = value.PlanID;
            _ = LoadFullPlanDataAsync(value.PlanID);
        }
    }

    public async Task LoadFullPlanDataAsync(string? planId)
    {
        if (string.IsNullOrWhiteSpace(planId))
        {
            Document.BroomstickPlanData = null;
            ChartNeedsRepaint?.Invoke();
            return;
        }

        var ds = _session.IsProjectOpen ? _session.GetDataService() : _dataService?.GetDataService();
        if (ds != null)
        {
            try
            {
                var fullPlan = await Task.Run(() => AdnlHookloadPlan.loadObject(ds, WellId, WellboreId, LogId, planId));
                Document.BroomstickPlanData = fullPlan;
            }
            catch
            {
                Document.BroomstickPlanData = null;
            }
        }
        IsDirty = true;
        ChartNeedsRepaint?.Invoke();
    }

    // Chart Display Settings
    [ObservableProperty]
    private bool _invertDepthAxis = true;

    partial void OnInvertDepthAxisChanged(bool value)
    {
        Document.PlotSettings.InvertDepthAxis = value;
        IsDirty = true;
        ChartNeedsRepaint?.Invoke();
    }

    [ObservableProperty]
    private bool _highlightFormationTop;

    partial void OnHighlightFormationTopChanged(bool value)
    {
        Document.PlotSettings.HighlightFormationTop = value;
        IsDirty = true;
        ChartNeedsRepaint?.Invoke();
    }

    [ObservableProperty]
    private bool _viewCasing;

    partial void OnViewCasingChanged(bool value)
    {
        Document.PlotSettings.ViewCasing = value;
        IsDirty = true;
        ChartNeedsRepaint?.Invoke();
    }

    [ObservableProperty]
    private bool _section;

    [ObservableProperty]
    private string _unitSystem = "klbs";

    // DataSelector & Range Navigation Properties
    [ObservableProperty]
    private bool _isTimeLog = true;

    [ObservableProperty]
    private DateTime _logMinDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _logMaxDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private string _fromDateTimeText = string.Empty;

    [ObservableProperty]
    private string _toDateTimeText = string.Empty;

    [ObservableProperty]
    private double _fromDepth = 0.0;

    [ObservableProperty]
    private double _toDepth = 20750.0;

    [ObservableProperty]
    private string _selectedPreset = "Entire Log";

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

    partial void OnSelectedPresetChanged(string value)
    {
        if (value != "Custom Range")
        {
            _ = ApplyPresetAsync(value);
        }
    }

    [ObservableProperty]
    private string _selectedOverviewChannelMnemonic = "DEPTH";

    // TrackBar DataSelector Properties
    [ObservableProperty]
    private double _trackBarMin = 0.0;

    [ObservableProperty]
    private double _trackBarMax = 20750.0;

    [ObservableProperty]
    private double _trackBarSelectionStart = 0.0;

    [ObservableProperty]
    private double _trackBarSelectionEnd = 20750.0;

    [ObservableProperty]
    private PointCollection? _trackBarOverviewPoints;

    [ObservableProperty]
    private string _trackBarOverviewChannelName = "DEPTH vs DateTime";

    [ObservableProperty]
    private Brush _trackBarOverviewBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));

    // Hover Tooltip Telemetry
    [ObservableProperty]
    private bool _isHoverVisible;

    [ObservableProperty]
    private string _hoverDepthText = "--";

    [ObservableProperty]
    private string _hoverWeightText = "--";

    [ObservableProperty]
    private string _hoverTypeText = "--";

    [ObservableProperty]
    private string _hoverRigStateText = "--";

    [ObservableProperty]
    private string _hoverDateTimeText = "--";

    [ObservableProperty]
    private string _hoverColorHex = "#E91E63";

    // Active Filtered Points currently plotted
    public List<PUSOPoint> FilteredPoints { get; private set; } = new();

    public BroomstickDocumentViewModel(
        ProjectSession session,
        IBroomstickTemplateRepository templateRepo,
        IBroomstickDataService? dataService = null,
        BroomstickDocument? initialDocument = null,
        IChartDataService? chartDataService = null,
        IWellDataRepository? wellDataRepo = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _templateRepo = templateRepo ?? throw new ArgumentNullException(nameof(templateRepo));
        _dataService = dataService;
        _chartDataService = chartDataService ?? new ChartDataService(_session);
        _wellDataRepo = wellDataRepo ?? new WellDataRepository(_session);

        Document = initialDocument ?? new BroomstickDocument();
        SyncFromDocument();
    }

    public async Task InitializeAsync()
    {
        StatusMessage = "Loading Broomstick data & theoretical plans...";
        try
        {
            var ds = _session.IsProjectOpen ? _session.GetDataService() : _dataService?.GetDataService();

            // Resolve Well & Wellbore if not yet set
            if (string.IsNullOrWhiteSpace(WellId) && _session.IsProjectOpen)
            {
                var well = await _wellDataRepo.GetProjectWellAsync();
                if (well != null)
                {
                    WellId = well.ObjectID;
                    WellName = well.name;
                    Document.WellId = well.ObjectID;
                }
            }

            // Resolve primary TimeLog if not yet set
            if (string.IsNullOrWhiteSpace(LogId) && _session.IsProjectOpen)
            {
                var timeLogs = await _wellDataRepo.GetTimeLogsAsync();
                var targetLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
                if (targetLog != null)
                {
                    LogId = targetLog.ObjectID;
                    Document.LogId = targetLog.ObjectID;
                }
            }

            // 1. Load theoretical plans from VMX_ADNL_HKLD_PLAN
            if (ds != null)
            {
                var plans = await Task.Run(() => AdnlHookloadPlan.getAllPlans(ds, WellId, WellboreId, LogId));
                AvailablePlans.Clear();
                foreach (var p in plans)
                {
                    AvailablePlans.Add(p);
                }

                if (!string.IsNullOrEmpty(Document.PlanId))
                {
                    SelectedPlan = AvailablePlans.FirstOrDefault(p => p.PlanID == Document.PlanId) ?? AvailablePlans.FirstOrDefault();
                }
                else
                {
                    SelectedPlan = AvailablePlans.FirstOrDefault();
                }

                if (SelectedPlan != null)
                {
                    await LoadFullPlanDataAsync(SelectedPlan.PlanID);
                }

                // 2. Load calculated points from VMX_BROOMSTICK_POINTS
                await LoadBroomstickPointsFromDbAsync(ds);
            }

            // 3. Load DataSelector overview waveform (DEPTH vs DateTime by default)
            await LoadTrackBarOverviewAsync();

            // 4. Filter Points for active selection
            ApplyRangeFilter();

            StatusMessage = $"Ready. {LoadedPointsCount:N0} points loaded.";
            ChartNeedsRepaint?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private void SyncFromDocument()
    {
        DocumentName = Document.DocumentName;
        CurrentTemplateId = Document.TemplateId;
        WellId = Document.WellId;
        WellboreId = Document.WellboreId;
        LogId = Document.LogId;
        InvertDepthAxis = Document.PlotSettings.InvertDepthAxis;
        HighlightFormationTop = Document.PlotSettings.HighlightFormationTop;
        ViewCasing = Document.PlotSettings.ViewCasing;
        UnitSystem = Document.PlotSettings.UnitSystem;
    }

    public async Task LoadBroomstickPointsFromDbAsync(IDataServiceDIntel ds)
    {
        if (ds == null || string.IsNullOrWhiteSpace(WellId)) return;

        try
        {
            var list = await Task.Run(() =>
            {
                string sql = $"SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='{WellId.Replace("'", "''")}' ORDER BY DEPTH ASC";
                DataTable dt = ds.GetTable(sql);

                var pts = new List<PUSOPoint>();
                if (dt != null && dt.Rows != null)
                {
                    bool hasRigStates = dt.Columns.Contains("RIG_STATES");
                    bool hasUserVisible = dt.Columns.Contains("USER_VISIBLE");

                    foreach (DataRow r in dt.Rows)
                    {
                        var pt = new PUSOPoint
                        {
                            Depth = Convert.ToDouble(r["DEPTH"] != DBNull.Value ? r["DEPTH"] : 0),
                            Type = Convert.ToInt32(r["TYPE"] != DBNull.Value ? r["TYPE"] : 0),
                            DynamicValue = Convert.ToDouble(r["DYNAMIC_VALUE"] != DBNull.Value ? r["DYNAMIC_VALUE"] : 0),
                            Avg = Convert.ToDouble(r["AVG"] != DBNull.Value ? r["AVG"] : 0),
                            Min = Convert.ToDouble(r["MIN"] != DBNull.Value ? r["MIN"] : 0),
                            Max = Convert.ToDouble(r["MAX"] != DBNull.Value ? r["MAX"] : 0),
                            StartDate = r["START_DATE"] != DBNull.Value && DateTime.TryParse(r["START_DATE"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd) ? sd : DateTime.MinValue,
                            EndDate = r["END_DATE"] != DBNull.Value && DateTime.TryParse(r["END_DATE"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed) ? ed : DateTime.MinValue,
                            RigStates = hasRigStates ? (r["RIG_STATES"]?.ToString() ?? "") : "",
                            UserVisible = !hasUserVisible || Convert.ToInt32(r["USER_VISIBLE"] != DBNull.Value ? r["USER_VISIBLE"] : 1) == 1
                        };
                        pts.Add(pt);
                    }
                }
                return pts;
            });

            Document.BroomstickPointData = list;
            LoadedPointsCount = list.Count;
        }
        catch
        {
        }
    }

    /// <summary>
    /// Loads the DataSelector overview trackbar with a DEPTH vs DateTime waveform,
    /// matching the RigStateDocument dataSelector mechanism.
    /// </summary>
    public async Task LoadTrackBarOverviewAsync()
    {
        // 1. Determine date range from TimeLog if available
        if (_chartDataService != null && !string.IsNullOrWhiteSpace(LogId))
        {
            try
            {
                var dateRange = await _chartDataService.GetTimeLogDateRangeAsync(LogId);
                if (dateRange.HasValue)
                {
                    LogMinDate = dateRange.Value.MinDate;
                    LogMaxDate = dateRange.Value.MaxDate;
                    TrackBarMin = LogMinDate.ToOADate();
                    TrackBarMax = LogMaxDate.ToOADate();
                    TrackBarSelectionStart = TrackBarMin;
                    TrackBarSelectionEnd = TrackBarMax;
                    FromDate = LogMinDate;
                    ToDate = LogMaxDate;
                    FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
                    ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");
                    IsTimeLog = true;
                }

                // Discover Depth mnemonic (DEPTH, HOLE_DEPTH, BIT_DEPTH, DMEA, DBIT, MD)
                var availableChannels = await _chartDataService.GetAvailableChannelsAsync(LogId, enumRTDataSourceType.TimeLog);
                string mnemonic = SelectedOverviewChannelMnemonic;
                if (string.IsNullOrWhiteSpace(mnemonic) || !availableChannels.Any(c => string.Equals(c.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase)))
                {
                    var depthCandidate = availableChannels.FirstOrDefault(c =>
                        string.Equals(c.Mnemonic, "DEPTH", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Mnemonic, "HOLE_DEPTH", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Mnemonic, "BIT_DEPTH", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Mnemonic, "DMEA", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Mnemonic, "DBIT", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Mnemonic, "MD", StringComparison.OrdinalIgnoreCase))
                        ?? availableChannels.FirstOrDefault();

                    if (depthCandidate != null)
                    {
                        mnemonic = depthCandidate.Mnemonic;
                        SelectedOverviewChannelMnemonic = mnemonic;
                    }
                }

                if (!string.IsNullOrWhiteSpace(mnemonic))
                {
                    var overviewData = await _chartDataService.GetTimelogDataAsync(
                        WellId,
                        WellboreId,
                        LogId,
                        new[] { mnemonic },
                        fromDate: null,
                        toDate: null,
                        maxPoints: 800);

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
                            TrackBarOverviewChannelName = $"{mnemonic} vs Time";
                            return;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        // 2. Fallback: Build DEPTH vs DateTime from Document.BroomstickPointData if TimeLog wasn't queried
        if (Document.BroomstickPointData.Count > 0)
        {
            var validPoints = Document.BroomstickPointData
                .Where(p => p.StartDate != DateTime.MinValue)
                .OrderBy(p => p.StartDate)
                .ToList();

            if (validPoints.Count > 0)
            {
                var minDt = validPoints.First().StartDate;
                var maxDt = validPoints.Last().StartDate;
                if (maxDt > minDt)
                {
                    LogMinDate = minDt;
                    LogMaxDate = maxDt;
                    TrackBarMin = minDt.ToOADate();
                    TrackBarMax = maxDt.ToOADate();
                    TrackBarSelectionStart = TrackBarMin;
                    TrackBarSelectionEnd = TrackBarMax;
                    FromDate = minDt;
                    ToDate = maxDt;
                    FromDateTimeText = minDt.ToString("dd-MM-yyyy HH:mm");
                    ToDateTimeText = maxDt.ToString("dd-MM-yyyy HH:mm");
                    IsTimeLog = true;

                    var pts = new PointCollection();
                    var step = Math.Max(1, validPoints.Count / 300);
                    for (int i = 0; i < validPoints.Count; i += step)
                    {
                        pts.Add(new System.Windows.Point(validPoints[i].StartDate.ToOADate(), validPoints[i].Depth));
                    }
                    if (pts.CanFreeze) pts.Freeze();
                    TrackBarOverviewPoints = pts;
                    TrackBarOverviewChannelName = "DEPTH vs DateTime";
                    return;
                }
            }
        }

        // 3. Fallback when only depths exist (or baseline default)
        if (Document.BroomstickPointData.Count > 0)
        {
            TrackBarMin = Document.BroomstickPointData.Min(p => p.Depth);
            TrackBarMax = Document.BroomstickPointData.Max(p => p.Depth);
            TrackBarSelectionStart = TrackBarMin;
            TrackBarSelectionEnd = TrackBarMax;
            FromDepth = TrackBarMin;
            ToDepth = TrackBarMax;
            IsTimeLog = false;

            var pts = new PointCollection();
            var step = Math.Max(1, Document.BroomstickPointData.Count / 300);
            for (int i = 0; i < Document.BroomstickPointData.Count; i += step)
            {
                var pt = Document.BroomstickPointData[i];
                double w = pt.DynamicValue > 0 ? pt.DynamicValue : pt.Avg;
                pts.Add(new System.Windows.Point(pt.Depth, w));
            }
            if (pts.CanFreeze) pts.Freeze();
            TrackBarOverviewPoints = pts;
            TrackBarOverviewChannelName = "Depth Profile";
        }
        else
        {
            // Default baseline so the trackbar graph is never empty
            var now = DateTime.Now;
            LogMinDate = now.AddDays(-7);
            LogMaxDate = now;
            TrackBarMin = LogMinDate.ToOADate();
            TrackBarMax = LogMaxDate.ToOADate();
            TrackBarSelectionStart = TrackBarMin;
            TrackBarSelectionEnd = TrackBarMax;
            IsTimeLog = true;
            FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");

            var pts = new PointCollection();
            for (int i = 0; i <= 20; i++)
            {
                double t = (double)i / 20;
                double oa = TrackBarMin + (t * (TrackBarMax - TrackBarMin));
                double d = t * 15000.0;
                pts.Add(new System.Windows.Point(oa, d));
            }
            if (pts.CanFreeze) pts.Freeze();
            TrackBarOverviewPoints = pts;
            TrackBarOverviewChannelName = "DEPTH vs DateTime";
        }
    }

    [RelayCommand]
    public void ApplyRangeFilter()
    {
        if (IsTimeLog)
        {
            DateTime? fromDt = null;
            DateTime? toDt = null;

            if (DateTime.TryParseExact(FromDateTimeText, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sDt))
            {
                fromDt = sDt;
                TrackBarSelectionStart = sDt.ToOADate();
            }
            else if (TrackBarSelectionStart > 1000)
            {
                fromDt = DateTime.FromOADate(TrackBarSelectionStart);
            }

            if (DateTime.TryParseExact(ToDateTimeText, "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var eDt))
            {
                toDt = eDt;
                TrackBarSelectionEnd = eDt.ToOADate();
            }
            else if (TrackBarSelectionEnd > 1000)
            {
                toDt = DateTime.FromOADate(TrackBarSelectionEnd);
            }

            FilteredPoints = Document.GetFilteredPoints(0, double.MaxValue, fromDt, toDt);
        }
        else
        {
            TrackBarSelectionStart = FromDepth;
            TrackBarSelectionEnd = ToDepth;
            FilteredPoints = Document.GetFilteredPoints(FromDepth, ToDepth);
        }

        LoadedPointsCount = FilteredPoints.Count;
        ChartNeedsRepaint?.Invoke();
    }

    [RelayCommand]
    public void RangeTrackBarChanged()
    {
        if (IsTimeLog && TrackBarSelectionStart > 1000)
        {
            var startDt = DateTime.FromOADate(TrackBarSelectionStart);
            var endDt = DateTime.FromOADate(TrackBarSelectionEnd);

            FromDate = startDt;
            ToDate = endDt;
            FromDateTimeText = startDt.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = endDt.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Custom Range";
        }
        else if (!IsTimeLog)
        {
            FromDepth = TrackBarSelectionStart;
            ToDepth = TrackBarSelectionEnd;
            SelectedPreset = "Custom Range";
        }

        ApplyRangeFilter();
    }

    [RelayCommand]
    public async Task ApplyPresetAsync(string presetName)
    {
        if (!IsTimeLog) return;
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
                break;
        }

        if (LogMinDate != DateTime.MinValue && from < LogMinDate) from = LogMinDate;
        if (to > maxRef) to = maxRef;

        FromDate = from;
        ToDate = to;
        FromDateTimeText = from.ToString("dd-MM-yyyy HH:mm");
        ToDateTimeText = to.ToString("dd-MM-yyyy HH:mm");

        TrackBarSelectionStart = from.ToOADate();
        TrackBarSelectionEnd = to.ToOADate();

        ApplyRangeFilter();
    }

    [RelayCommand]
    public void ResetZoom()
    {
        TrackBarSelectionStart = TrackBarMin;
        TrackBarSelectionEnd = TrackBarMax;
        if (IsTimeLog)
        {
            SelectedPreset = "Entire Log";
            if (LogMinDate != DateTime.MinValue) FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
            if (LogMaxDate != DateTime.MinValue) ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");
        }
        else
        {
            FromDepth = TrackBarMin;
            ToDepth = TrackBarMax;
        }
        ApplyRangeFilter();
    }

    [RelayCommand]
    public async Task OpenDataSelectorAsync()
    {
        var console = GetOrCreateConsoleModel();
        var vm = new DataSelectorViewModel(console);
        if (!string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic))
        {
            var match = vm.AvailableSeries.FirstOrDefault(s => s.Contains(SelectedOverviewChannelMnemonic, StringComparison.OrdinalIgnoreCase));
            if (match != null) vm.TargetSeries = match;
            if (vm.AvailableYCoordinates.Contains(SelectedOverviewChannelMnemonic))
                vm.YCoordinate = SelectedOverviewChannelMnemonic;
        }

        bool? result = OpenDataSelectorDialogHandler != null
            ? OpenDataSelectorDialogHandler(vm)
            : ShowDataSelectorDialog(vm);

        if (result == true)
        {
            if (!string.IsNullOrWhiteSpace(vm.SelectedChannelMnemonic))
            {
                SelectedOverviewChannelMnemonic = vm.SelectedChannelMnemonic;
            }
            await LoadTrackBarOverviewAsync();
            ApplyRangeFilter();
        }
    }

    private static bool? ShowDataSelectorDialog(DataSelectorViewModel vm)
    {
        var dlg = new DataSelectorWindow(vm)
        {
            Owner = Application.Current?.MainWindow
        };
        return dlg.ShowDialog();
    }

    private VHTrackConsole GetOrCreateConsoleModel()
    {
        var console = new VHTrackConsole
        {
            WellID = WellId,
            IndexType = IsTimeLog ? enumIndexType.TimeLog : enumIndexType.Deptlog
        };
        console.DataSource.WellboreID = WellboreId;
        if (IsTimeLog) console.DataSource.TimeLogID = LogId;
        else console.DataSource.DepthLogID = LogId;
        console.DataSource.DatasourceType = IsTimeLog ? enumRTDataSourceType.TimeLog : enumRTDataSourceType.DepthLog;

        var track = new VHTrack { Title = "Data Selector Channels" };
        var channels = new List<VHTrackChannel>
        {
            new() { Mnemonic = "DEPTH", Title = "Hole Depth" },
            new() { Mnemonic = "BIT_DEPTH", Title = "Bit Depth" },
            new() { Mnemonic = "HKLD", Title = "Hookload" },
            new() { Mnemonic = "ROPA", Title = "Rate of Penetration" },
            new() { Mnemonic = "WOB", Title = "Weight on Bit" },
            new() { Mnemonic = "TORQ", Title = "Drilling Torque" },
            new() { Mnemonic = "RPM", Title = "Rotary Speed" }
        };

        foreach (var ch in channels)
        {
            track.Channels.Add(ch);
        }
        console.Tracks.Add(track);
        return console;
    }

    [RelayCommand]
    public async Task SaveDocumentAsync()
    {
        if (string.IsNullOrWhiteSpace(Document.TemplateId))
        {
            Document.TemplateId = Guid.NewGuid().ToString();
        }

        Document.DocumentName = DocumentName;
        Document.WellId = WellId;
        Document.WellboreId = WellboreId;
        Document.LogId = LogId;
        Document.PlanId = SelectedPlan?.PlanID ?? Document.PlanId;

        var item = new BroomstickTemplateItem
        {
            TemplateId = Document.TemplateId,
            WellId = WellId,
            WellboreId = WellboreId,
            PlanId = Document.PlanId,
            DocumentName = DocumentName,
            Description = Document.Description,
            IsDefault = Document.IsDefault,
            TemplateData = Document.SerializeToBlob()
        };

        await _templateRepo.SaveTemplateAsync(item);
        IsDirty = false;
        StatusMessage = $"Saved template '{DocumentName}' successfully.";
    }

    [RelayCommand]
    public async Task SaveAsDocumentAsync()
    {
        string? newName = PromptSaveAsHandler != null
            ? PromptSaveAsHandler(DocumentName)
            : $"{DocumentName} (Copy)";

        if (string.IsNullOrWhiteSpace(newName)) return;

        Document.TemplateId = Guid.NewGuid().ToString();
        Document.DocumentName = newName;
        DocumentName = newName;
        CurrentTemplateId = Document.TemplateId;
        Document.IsDefault = false;

        await SaveDocumentAsync();
    }

    [RelayCommand]
    public void OpenSetup()
    {
        bool? res = OpenSetupDialogHandler?.Invoke(this);
        if (res == true)
        {
            IsDirty = true;
            ApplyRangeFilter();
        }
    }

    [RelayCommand]
    public async Task OpenTemplateManagerAsync()
    {
        var mgrVm = new BroomstickDocumentManagerViewModel(_templateRepo, _session, _wellDataRepo, WellId);
        await mgrVm.InitializeAsync();

        bool? res = OpenTemplateManagerDialogHandler?.Invoke(mgrVm);
        if (res == true && mgrVm.SelectedDocumentNode != null)
        {
            var item = await _templateRepo.GetTemplateByIdAsync(mgrVm.SelectedDocumentNode.TemplateId);
            if (item != null)
            {
                var doc = BroomstickDocument.DeserializeFromBlob(item.TemplateData) ?? new BroomstickDocument();
                doc.TemplateId = item.TemplateId;
                doc.DocumentName = item.DocumentName;
                doc.WellId = item.WellId;
                doc.PlanId = item.PlanId ?? "";
                doc.IsDefault = item.IsDefault;

                Document = doc;
                SyncFromDocument();
                await InitializeAsync();
            }
        }
    }

    public void UpdateHoverTelemetry(BroomstickHoverInfo? info)
    {
        if (info == null)
        {
            IsHoverVisible = false;
            return;
        }

        IsHoverVisible = true;
        HoverDepthText = $"{info.Depth:F2} ft";
        HoverWeightText = $"{info.Weight:F2} {UnitSystem}";
        HoverTypeText = info.PointType;
        HoverRigStateText = !string.IsNullOrWhiteSpace(info.RigStateName) ? info.RigStateName : "Active";
        HoverDateTimeText = info.DateTimeText;
        HoverColorHex = info.ColorHex;
    }
}

