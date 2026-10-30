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
using DrillIntel.Views.Broomstick;



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
    public Func<BroomstickDocumentViewModel, bool?>? OpenBroomstickSetupDialogHandler { get; set; }

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

    [ObservableProperty]
    private bool _isLegendVisible = true;



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



    private bool _isInitializingPlans;

    private readonly SemaphoreSlim _planLoadLock = new(1, 1);



    [ObservableProperty]

    private AdnlHookloadPlan? _selectedPlan;



    partial void OnSelectedPlanChanged(AdnlHookloadPlan? value)

    {

        if (_isInitializingPlans) return;

        if (value != null)

        {

            Document.PlanId = value.PlanID;

            _ = LoadFullPlanDataAsync(value.PlanID);

        }

    }



    public async Task LoadFullPlanDataAsync(string? mainPlanKey)

    {

        await _planLoadLock.WaitAsync();

        try

        {

            if (string.IsNullOrWhiteSpace(mainPlanKey))

            {

                Document.BroomstickPlanData = null;

                Document.AssociatedPlans.Clear();

                UpdateLegendTitles();

                ChartNeedsRepaint?.Invoke();

                return;

            }



            var ds = _session.IsProjectOpen ? _session.GetDataService() : _dataService?.GetDataService();

            if (ds != null)

            {

                try

                {

                    var associated = await Task.Run(() =>

                    {

                        var resultList = new List<AdnlHookloadPlan>();

                        string safeKey = mainPlanKey.Replace("'", "''");



                        // 1. Query child plans under this RUN_NO or main plan prefix

                        string sql = $"SELECT DISTINCT PLAN_ID, PLAN_NAME, RUN_NO FROM VMX_ADNL_HKLD_PLAN WHERE RUN_NO='{safeKey}' OR PLAN_NAME LIKE '{safeKey}%' OR PLAN_ID='{safeKey}'";

                        if (!string.IsNullOrWhiteSpace(WellId))

                        {

                            sql += $" AND WELL_ID='{WellId.Replace("'", "''")}'";

                        }

                        sql += " ORDER BY PLAN_NAME";



                        DataTable dt = ds.GetTable(sql);

                        if ((dt == null || dt.Rows == null || dt.Rows.Count == 0) && !string.IsNullOrWhiteSpace(WellId))

                        {

                            // Fallback without WellId filter

                            sql = $"SELECT DISTINCT PLAN_ID, PLAN_NAME, RUN_NO FROM VMX_ADNL_HKLD_PLAN WHERE RUN_NO='{safeKey}' OR PLAN_NAME LIKE '{safeKey}%' OR PLAN_ID='{safeKey}' ORDER BY PLAN_NAME";

                            dt = ds.GetTable(sql);

                        }



                        if (dt != null && dt.Rows != null && dt.Rows.Count > 0)

                        {

                            foreach (DataRow row in dt.Rows)

                            {

                                string cPlanId = DataService.checkNull(row["PLAN_ID"], "");

                                if (!string.IsNullOrEmpty(cPlanId))

                                {

                                    var loaded = AdnlHookloadPlan.loadObject(ds, WellId, WellboreId, LogId, cPlanId);

                                    if (loaded != null && (loaded.pickup?.Count > 0 || loaded.slackoff?.Count > 0 || loaded.rotate?.Count > 0))

                                    {

                                        resultList.Add(loaded);

                                    }

                                }

                            }

                        }



                        // Fallback 1: in-memory main key match across all plans for this well
                        if (resultList.Count == 0)
                        {
                            string allSql = string.IsNullOrWhiteSpace(WellId)
                                ? "SELECT DISTINCT PLAN_ID, PLAN_NAME, RUN_NO FROM VMX_ADNL_HKLD_PLAN"
                                : $"SELECT DISTINCT PLAN_ID, PLAN_NAME, RUN_NO FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='{WellId.Replace("'", "''")}'";
                            DataTable allDt = ds.GetTable(allSql);
                            if (allDt != null && allDt.Rows != null)
                            {
                                foreach (DataRow r in allDt.Rows)
                                {
                                    string pName = DataService.checkNull(r["PLAN_NAME"], "");
                                    string rNo = DataService.checkNull(r["RUN_NO"], "");
                                    string pId = DataService.checkNull(r["PLAN_ID"], "");
                                    if (string.Equals(AdnlHookloadPlan.ExtractMainPlanKey(pName, rNo), mainPlanKey, StringComparison.OrdinalIgnoreCase))
                                    {
                                        var loaded = AdnlHookloadPlan.loadObject(ds, WellId, WellboreId, LogId, pId);
                                        if (loaded != null && (loaded.pickup?.Count > 0 || loaded.slackoff?.Count > 0 || loaded.rotate?.Count > 0))
                                        {
                                            resultList.Add(loaded);
                                        }
                                    }
                                }
                            }
                        }

                        // Fallback 2: If no associated child plans found, load mainPlanKey directly
                        if (resultList.Count == 0)
                        {
                            var direct = AdnlHookloadPlan.loadObject(ds, WellId, WellboreId, LogId, mainPlanKey);
                            if (direct != null)
                            {
                                resultList.Add(direct);
                            }
                        }

                        return resultList;
                    });

                    Document.AssociatedPlans = associated;
                    Document.BroomstickPlanData = associated.FirstOrDefault(p => (p.RunNo != null && p.RunNo.Contains("0.3")) || (p.Name != null && p.Name.Contains("0.3"))) ?? associated.FirstOrDefault();

                    Document.PlanId = mainPlanKey;

                }

                catch

                {

                }

            }

            UpdateLegendTitles();

            IsDirty = true;

            ChartNeedsRepaint?.Invoke();

        }

        finally

        {

            _planLoadLock.Release();

        }

    }



    // Dynamic Legend Titles

    [ObservableProperty]

    private string _plan02LegendText = "8.5\" Hookload Drilling 0.2 OHFF";

    [ObservableProperty]

    private string _plan025LegendText = "8.5\" Hookload Drilling 0.25 OHFF";

    [ObservableProperty]

    private string _plan03LegendText = "8.5\" Hookload Drilling 0.3 OHFF";

    [ObservableProperty]

    private string _plan035LegendText = "8.5\" Hookload Drilling 0.35 OHFF";

    [ObservableProperty]

    private string _plan04LegendText = "8.5\" Hookload Drilling 0.4 OHFF";

    [ObservableProperty]

    private string _rotTheoryLegendText = "8.5\" Hookload Drilling 0.3 OHFF";

    [ObservableProperty]

    private string _so02LegendText = "8.5\" Hookload Drilling 0.2 OHFF";

    [ObservableProperty]

    private string _so025LegendText = "8.5\" Hookload Drilling 0.25 OHFF";

    [ObservableProperty]

    private string _so03LegendText = "8.5\" Hookload Drilling 0.3 OHFF";

    [ObservableProperty]

    private string _so035LegendText = "8.5\" Hookload Drilling 0.35 OHFF";

    [ObservableProperty]

    private string _so04LegendText = "8.5\" Hookload Drilling 0.4 OHFF";



    public void UpdateLegendTitles()
    {
        string baseName = !string.IsNullOrWhiteSpace(SelectedPlan?.Name) 
            ? SelectedPlan.Name 
            : (!string.IsNullOrWhiteSpace(Document.BroomstickPlanData?.Name) 
                ? Document.BroomstickPlanData.Name 
                : (!string.IsNullOrWhiteSpace(DocumentName) ? DocumentName : "8.5\" Hookload Drilling"));

        baseName = AdnlHookloadPlan.ExtractMainPlanKey(baseName, "");

        if (Document.AssociatedPlans?.Count >= 2)
        {
            var plans = Document.AssociatedPlans
                .OrderBy(p => AdnlHookloadPlan.ExtractFrictionFactor(p))
                .ToList();

            string FormatLabel(AdnlHookloadPlan p, string fallbackFf)
            {
                if (!string.IsNullOrWhiteSpace(p.RunNo) && System.Text.RegularExpressions.Regex.IsMatch(p.RunNo, @"0\.\d+"))
                    return $"{baseName} {p.RunNo.Trim()}";
                if (!string.IsNullOrWhiteSpace(p.Name) && System.Text.RegularExpressions.Regex.IsMatch(p.Name, @"0\.\d+"))
                    return p.Name.Trim();
                return $"{baseName} {fallbackFf}";
            }

            if (plans.Count >= 5)
            {
                Plan02LegendText = FormatLabel(plans[0], "0.2 OHFF");
                Plan025LegendText = FormatLabel(plans[1], "0.25 OHFF");
                Plan03LegendText = FormatLabel(plans[2], "0.3 OHFF");
                Plan035LegendText = FormatLabel(plans[3], "0.35 OHFF");
                Plan04LegendText = FormatLabel(plans[4], "0.4 OHFF");

                RotTheoryLegendText = $"{baseName} 0.3 OHFF";

                So02LegendText = FormatLabel(plans[0], "0.2 OHFF");
                So025LegendText = FormatLabel(plans[1], "0.25 OHFF");
                So03LegendText = FormatLabel(plans[2], "0.3 OHFF");
                So035LegendText = FormatLabel(plans[3], "0.35 OHFF");
                So04LegendText = FormatLabel(plans[4], "0.4 OHFF");
                return;
            }
        }

        Plan02LegendText = $"{baseName} 0.2 OHFF";
        Plan025LegendText = $"{baseName} 0.25 OHFF";
        Plan03LegendText = $"{baseName} 0.3 OHFF";
        Plan035LegendText = $"{baseName} 0.35 OHFF";
        Plan04LegendText = $"{baseName} 0.4 OHFF";

        RotTheoryLegendText = $"{baseName} 0.3 OHFF";

        So02LegendText = $"{baseName} 0.2 OHFF";
        So025LegendText = $"{baseName} 0.25 OHFF";
        So03LegendText = $"{baseName} 0.3 OHFF";
        So035LegendText = $"{baseName} 0.35 OHFF";
        So04LegendText = $"{baseName} 0.4 OHFF";
    }

    // Series Visibility Toggles

    private bool GetSeriesVis(string key, bool defaultVal = true) =>

        Document.PlotSettings.SeriesVisibility.TryGetValue(key, out var v) ? v : defaultVal;



    private void SetSeriesVis(string key, bool value)

    {

        Document.PlotSettings.SeriesVisibility[key] = value;

        IsDirty = true;

        ChartNeedsRepaint?.Invoke();

    }



    public bool IsPlan02Visible { get => GetSeriesVis("plan02"); set { SetSeriesVis("plan02", value); OnPropertyChanged(); } }

    public bool IsPlan025Visible { get => GetSeriesVis("plan025"); set { SetSeriesVis("plan025", value); OnPropertyChanged(); } }

    public bool IsPlan03Visible { get => GetSeriesVis("plan03"); set { SetSeriesVis("plan03", value); OnPropertyChanged(); } }

    public bool IsPlan035Visible { get => GetSeriesVis("plan035"); set { SetSeriesVis("plan035", value); OnPropertyChanged(); } }

    public bool IsPlan04Visible { get => GetSeriesVis("plan04"); set { SetSeriesVis("plan04", value); OnPropertyChanged(); } }

    public bool IsCasingPuVisible { get => GetSeriesVis("casingPu"); set { SetSeriesVis("casingPu", value); OnPropertyChanged(); } }

    public bool IsRotTheoryVisible { get => GetSeriesVis("rotTheory"); set { SetSeriesVis("rotTheory", value); OnPropertyChanged(); } }

    public bool IsCasingSoVisible { get => GetSeriesVis("casingSo"); set { SetSeriesVis("casingSo", value); OnPropertyChanged(); } }

    public bool IsSo02Visible { get => GetSeriesVis("so02"); set { SetSeriesVis("so02", value); OnPropertyChanged(); } }

    public bool IsSo025Visible { get => GetSeriesVis("so025"); set { SetSeriesVis("so025", value); OnPropertyChanged(); } }

    public bool IsSo03Visible { get => GetSeriesVis("so03"); set { SetSeriesVis("so03", value); OnPropertyChanged(); } }

    public bool IsSo035Visible { get => GetSeriesVis("so035"); set { SetSeriesVis("so035", value); OnPropertyChanged(); } }

    public bool IsSo04Visible { get => GetSeriesVis("so04"); set { SetSeriesVis("so04", value); OnPropertyChanged(); } }

    public bool IsActualPuVisible { get => GetSeriesVis("actualPu"); set { SetSeriesVis("actualPu", value); OnPropertyChanged(); } }

    public bool IsActualSoVisible { get => GetSeriesVis("actualSo"); set { SetSeriesVis("actualSo", value); OnPropertyChanged(); } }

    public bool IsActualRotVisible { get => GetSeriesVis("actualRot"); set { SetSeriesVis("actualRot", value); OnPropertyChanged(); } }



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
    private VHTrackConsole? _consoleModel;



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

        if (_session != null)
        {
            _session.DataChanged += OnSessionDataChanged;
        }
    }

    private async void OnSessionDataChanged(object? sender, EventArgs e)
    {
        try
        {
            var ds = _session.IsProjectOpen ? _session.GetDataService() : _dataService?.GetDataService();
            if (ds != null)
            {
                await LoadBroomstickPointsFromDbAsync(ds);
                ApplyRangeFilter();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BroomstickDocumentViewModel] OnSessionDataChanged error: {ex.Message}");
        }
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



            // 1. Load theoretical plans from VMX_ADNL_HKLD_PLAN (Grouped by Main Plan ID)

            if (ds != null)

            {

                var plans = await Task.Run(() =>

                {

                    var pList = AdnlHookloadPlan.GetMainPlanHeaders(ds, WellId, WellboreId, LogId);

                    if ((pList == null || pList.Count == 0) && !string.IsNullOrWhiteSpace(WellId))

                    {

                        pList = AdnlHookloadPlan.GetMainPlanHeaders(ds, WellId, null, null);

                    }

                    if (pList == null || pList.Count == 0)

                    {

                        pList = AdnlHookloadPlan.GetMainPlanHeaders(ds, null, null, null);

                    }

                    return pList;

                });



                _isInitializingPlans = true;

                try

                {

                    AvailablePlans.Clear();

                    foreach (var p in plans)

                    {

                        AvailablePlans.Add(p);

                    }



                    if (!string.IsNullOrEmpty(Document.PlanId))

                    {

                        SelectedPlan = AvailablePlans.FirstOrDefault(p => p.PlanID == Document.PlanId || p.Name == Document.PlanId) ?? AvailablePlans.FirstOrDefault();

                    }

                    else

                    {

                        SelectedPlan = AvailablePlans.FirstOrDefault();

                    }

                }

                finally

                {

                    _isInitializingPlans = false;

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

        IsLegendVisible = Document.PlotSettings.ShowLegend;

    }



    public async Task LoadBroomstickPointsFromDbAsync(IDataServiceDIntel ds)
    {
        if (ds == null) return;

        if (string.IsNullOrWhiteSpace(WellId) && _session?.IsProjectOpen == true)
        {
            try
            {
                var repo = new WellDataRepository(_session);
                var well = await repo.GetProjectWellAsync();
                if (well != null)
                {
                    WellId = well.ObjectID;
                    WellName = well.name;
                    Document.WellId = well.ObjectID;
                }
            }
            catch { }
        }

        try
        {
            var list = await Task.Run(() =>
            {
                string safeWellId = WellId?.Replace("'", "''") ?? "";
                string safeWellName = WellName?.Replace("'", "''") ?? "";

                string sql;
                if (!string.IsNullOrWhiteSpace(safeWellName) && !safeWellName.Equals(safeWellId, StringComparison.OrdinalIgnoreCase))
                {
                    sql = $"SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='{safeWellId}' OR WELL_ID='{safeWellName}' ORDER BY DEPTH ASC";
                }
                else if (!string.IsNullOrWhiteSpace(safeWellId))
                {
                    sql = $"SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='{safeWellId}' ORDER BY DEPTH ASC";
                }
                else
                {
                    sql = "SELECT * FROM VMX_BROOMSTICK_POINTS ORDER BY DEPTH ASC";
                }

                DataTable dt = ds.GetTable(sql);

                // Fallback: If 0 rows returned, load all points from VMX_BROOMSTICK_POINTS
                if ((dt == null || dt.Rows == null || dt.Rows.Count == 0) && (!string.IsNullOrWhiteSpace(safeWellId) || !string.IsNullOrWhiteSpace(safeWellName)))
                {
                    dt = ds.GetTable("SELECT * FROM VMX_BROOMSTICK_POINTS ORDER BY DEPTH ASC");
                }

                var pts = new List<PUSOPoint>();
                if (dt != null && dt.Rows != null)
                {
                    bool hasUserVisible = dt.Columns.Contains("USER_VISIBLE");
                    foreach (DataRow r in dt.Rows)
                    {
                        var pt = PUSOPoint.MapFromRow(r);
                        if (hasUserVisible)
                        {
                            object uv = r["USER_VISIBLE"];
                            if (uv != null && uv != DBNull.Value)
                            {
                                string uvStr = uv.ToString()?.Trim() ?? "";
                                pt.UserVisible = !(uvStr == "0" || string.Equals(uvStr, "false", StringComparison.OrdinalIgnoreCase));
                            }
                            else
                            {
                                pt.UserVisible = true;
                            }
                        }
                        else
                        {
                            pt.UserVisible = true;
                        }
                        pts.Add(pt);
                    }
                }
                return pts;
            });

            Document.BroomstickPointData = list;
            LoadedPointsCount = list.Count;
            System.Diagnostics.Debug.WriteLine($"[BroomstickDocumentViewModel] Loaded {list.Count} broomstick points from database.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BroomstickDocumentViewModel] Error loading points: {ex.Message}");
        }
    }



    /// <summary>

    /// Loads the DataSelector overview trackbar with a DEPTH vs DateTime waveform,

    /// <summary>

    /// Loads background overview waveform for the bottom RangeTrackBarControl dataSelector

    /// matching the RigStateDocument dataSelector mechanism (X Axis: Datetime, Y Axis: Depth).

    /// </summary>

    public async Task LoadTrackBarOverviewAsync()
    {
        string mnemonic = !string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic)
            ? SelectedOverviewChannelMnemonic
            : "DEPTH";

        if (_consoleModel == null)
        {
            _consoleModel = await GetOrCreateConsoleModelAsync();
        }

        // Look for exact mnemonic match first
        var channel = _consoleModel?.Tracks.SelectMany(t => t.Channels)
            .FirstOrDefault(c => string.Equals(c.Mnemonic, mnemonic, StringComparison.OrdinalIgnoreCase));
        if (channel == null)
        {
            channel = _consoleModel?.Tracks.SelectMany(t => t.Channels)
                .FirstOrDefault(c => string.Equals(c.Title, mnemonic, StringComparison.OrdinalIgnoreCase));
        }

        if (channel != null && !string.IsNullOrWhiteSpace(channel.Mnemonic))
        {
            mnemonic = channel.Mnemonic;
        }

        string channelTitle = mnemonic;
        string channelUnit = string.Empty;

        // Derive standard channel title and unit for recognized drilling channels
        if (mnemonic.Contains("DEPTH", StringComparison.OrdinalIgnoreCase) || mnemonic.Contains("BIT", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Depth", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Bit Depth";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "ft";
        }
        else if (mnemonic.Contains("HKLD", StringComparison.OrdinalIgnoreCase) || mnemonic.Contains("HOOK", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Hook", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Hookload";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "klb";
        }
        else if (mnemonic.Contains("TORQ", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Torq", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Drilling Torque";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "kft-lb";
        }
        else if (mnemonic.Contains("ROP", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Penetration", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Rate of Penetration";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "ft/hr";
        }
        else if (mnemonic.Contains("WOB", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Weight", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Weight on Bit";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "klb";
        }
        else if (mnemonic.Contains("RPM", StringComparison.OrdinalIgnoreCase))
        {
            channelTitle = channel?.Title != null && channel.Title.Contains("Speed", StringComparison.OrdinalIgnoreCase) ? channel.Title : "Rotary Speed";
            channelUnit = !string.IsNullOrWhiteSpace(channel?.Unit) ? channel.Unit : "rpm";
        }
        else
        {
            channelTitle = !string.IsNullOrWhiteSpace(channel?.Title) ? channel.Title : mnemonic;
            channelUnit = channel?.Unit ?? string.Empty;
        }

        TrackBarOverviewChannelName = !string.IsNullOrWhiteSpace(channelUnit)
            ? $"{channelTitle} ({channelUnit})"
            : channelTitle;

        TrackBarOverviewBrush = GetChannelBrush(mnemonic);

        // 1. Determine date range and query from TimeLog if available
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
                    if (TrackBarSelectionStart < TrackBarMin || TrackBarSelectionEnd > TrackBarMax || TrackBarSelectionStart >= TrackBarSelectionEnd)
                    {
                        TrackBarSelectionStart = TrackBarMin;
                        TrackBarSelectionEnd = TrackBarMax;
                        FromDate = LogMinDate;
                        ToDate = LogMaxDate;
                        FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
                        ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");
                    }
                    IsTimeLog = true;
                }

                var queryList = new List<string> { mnemonic };
                if (mnemonic.Contains("DEPTH", StringComparison.OrdinalIgnoreCase) || mnemonic.Contains("BIT", StringComparison.OrdinalIgnoreCase))
                {
                    string[] depthCandidates = { "BIT_DEPTH", "HOLE_DEPTH", "DEPTH", "DMEA", "DBIT", "MD", "Bit Depth", "Hole Depth" };
                    foreach (var c in depthCandidates)
                    {
                        if (!queryList.Contains(c, StringComparer.OrdinalIgnoreCase))
                            queryList.Add(c);
                    }
                }

                var overviewData = await _chartDataService.GetTimelogDataAsync(
                    WellId,
                    WellboreId,
                    LogId,
                    queryList,
                    fromDate: null,
                    toDate: null,
                    maxPoints: 800);

                if (overviewData != null && overviewData.IndexValues.Count > 0)
                {
                    List<double>? vals = null;
                    if (overviewData.ChannelValues.TryGetValue(mnemonic, out var exactVals) && exactVals != null && exactVals.Count == overviewData.IndexValues.Count)
                    {
                        vals = exactVals;
                    }
                    else
                    {
                        var kvp = overviewData.ChannelValues.FirstOrDefault(kv =>
                            string.Equals(kv.Key.Trim(), mnemonic.Trim(), StringComparison.OrdinalIgnoreCase) &&
                            kv.Value != null && kv.Value.Count == overviewData.IndexValues.Count);
                        if (kvp.Value != null)
                        {
                            vals = kvp.Value;
                        }
                        else
                        {
                            var first = overviewData.ChannelValues.FirstOrDefault(kv => kv.Value != null && kv.Value.Count == overviewData.IndexValues.Count);
                            vals = first.Value;
                        }
                    }

                    if (vals != null && vals.Count == overviewData.IndexValues.Count)
                    {
                        var pts = new PointCollection(overviewData.IndexValues.Count);
                        for (int i = 0; i < overviewData.IndexValues.Count; i++)
                        {
                            // X Axis: Datetime (OA Date), Y Axis: Channel value
                            pts.Add(new System.Windows.Point(overviewData.IndexValues[i], vals[i]));
                        }
                        if (pts.CanFreeze) pts.Freeze();

                        TrackBarOverviewPoints = pts;
                        return;
                    }
                }
            }
            catch
            {
            }
        }

        // 2. Fallback: Build from Document.BroomstickPointData if TimeLog table wasn't queried
        if (Document.BroomstickPointData.Count > 0)
        {
            var validPoints = Document.BroomstickPointData
                .Where(p => p.StartDate != DateTime.MinValue && p.Depth > 0)
                .OrderBy(p => p.StartDate)
                .ToList();

            if (validPoints.Count > 0)
            {
                var minDt = validPoints.First().StartDate;
                var maxDt = validPoints.Last().StartDate;
                if (maxDt > minDt)
                {
                    if (LogMinDate == DateTime.MinValue || LogMaxDate == DateTime.MinValue)
                    {
                        LogMinDate = minDt;
                        LogMaxDate = maxDt;
                        TrackBarMin = minDt.ToOADate();
                        TrackBarMax = maxDt.ToOADate();
                        if (TrackBarSelectionStart < TrackBarMin || TrackBarSelectionEnd > TrackBarMax || TrackBarSelectionStart >= TrackBarSelectionEnd)
                        {
                            TrackBarSelectionStart = TrackBarMin;
                            TrackBarSelectionEnd = TrackBarMax;
                            FromDate = minDt;
                            ToDate = maxDt;
                            FromDateTimeText = minDt.ToString("dd-MM-yyyy HH:mm");
                            ToDateTimeText = maxDt.ToString("dd-MM-yyyy HH:mm");
                        }
                        IsTimeLog = true;
                    }

                    var pts = new PointCollection();
                    var step = Math.Max(1, validPoints.Count / 300);
                    for (int i = 0; i < validPoints.Count; i += step)
                    {
                        double yVal = validPoints[i].Depth;
                        if (mnemonic.Contains("HKLD", StringComparison.OrdinalIgnoreCase) ||
                            mnemonic.Contains("HOOK", StringComparison.OrdinalIgnoreCase) ||
                            mnemonic.Contains("TORQ", StringComparison.OrdinalIgnoreCase))
                        {
                            yVal = BroomstickChartRenderer.ResolvePointPlotValue(validPoints[i]);
                        }
                        pts.Add(new System.Windows.Point(validPoints[i].StartDate.ToOADate(), yVal));
                    }
                    if (pts.CanFreeze) pts.Freeze();

                    TrackBarOverviewPoints = pts;
                    return;
                }
            }
        }

        // 3. Fallback when only depths exist (or baseline default)
        if (Document.BroomstickPointData.Count > 0)
        {
            TrackBarMin = Document.BroomstickPointData.Min(p => p.Depth);
            TrackBarMax = Document.BroomstickPointData.Max(p => p.Depth);
            if (TrackBarSelectionStart < TrackBarMin || TrackBarSelectionEnd > TrackBarMax || TrackBarSelectionStart >= TrackBarSelectionEnd)
            {
                TrackBarSelectionStart = TrackBarMin;
                TrackBarSelectionEnd = TrackBarMax;
                FromDepth = TrackBarMin;
                ToDepth = TrackBarMax;
            }
            IsTimeLog = false;

            TrackBarOverviewPoints = null;
        }
        else
        {
            // Default baseline so the trackbar graph is never empty
            var now = DateTime.Now;
            LogMinDate = now.AddDays(-7);
            LogMaxDate = now;
            TrackBarMin = LogMinDate.ToOADate();
            TrackBarMax = LogMaxDate.ToOADate();
            if (TrackBarSelectionStart < TrackBarMin || TrackBarSelectionEnd > TrackBarMax || TrackBarSelectionStart >= TrackBarSelectionEnd)
            {
                TrackBarSelectionStart = TrackBarMin;
                TrackBarSelectionEnd = TrackBarMax;
                FromDateTimeText = LogMinDate.ToString("dd-MM-yyyy HH:mm");
                ToDateTimeText = LogMaxDate.ToString("dd-MM-yyyy HH:mm");
            }
            IsTimeLog = true;

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
        }
    }

    private static Brush GetChannelBrush(string mnemonic)
    {
        if (string.IsNullOrWhiteSpace(mnemonic))
        {
            var b = new SolidColorBrush(Color.FromRgb(25, 118, 210));
            if (b.CanFreeze) b.Freeze();
            return b;
        }

        Color color;
        if (mnemonic.Contains("HKLD", StringComparison.OrdinalIgnoreCase) || mnemonic.Contains("HOOK", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromRgb(13, 148, 136); // Teal #0D9488
        }
        else if (mnemonic.Contains("TORQ", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromRgb(217, 119, 6); // Amber #D97706
        }
        else if (mnemonic.Contains("ROP", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromRgb(139, 92, 246); // Purple #8B5CF6
        }
        else if (mnemonic.Contains("WOB", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromRgb(236, 72, 153); // Pink #EC4899
        }
        else if (mnemonic.Contains("RPM", StringComparison.OrdinalIgnoreCase))
        {
            color = Color.FromRgb(6, 182, 212); // Cyan #06B6D4
        }
        else
        {
            color = Color.FromRgb(25, 118, 210); // Blue #1976D2
        }

        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }

        [RelayCommand]
    public void ApplyRangeFilter()
    {
        if (IsTimeLog)
        {
            DateTime? fromDt = null;
            DateTime? toDt = null;

            if (!string.IsNullOrWhiteSpace(FromDateTimeText))
            {
                var parsed = DataService.checkNull(FromDateTimeText, DateTime.MinValue);
                if (parsed > DateTime.MinValue)
                {
                    fromDt = parsed;
                    TrackBarSelectionStart = parsed.ToOADate();
                }
                else if (TrackBarSelectionStart > 1000)
                {
                    fromDt = DateTime.FromOADate(TrackBarSelectionStart);
                }
            }
            else if (TrackBarSelectionStart > 1000)
            {
                fromDt = DateTime.FromOADate(TrackBarSelectionStart);
            }

            if (!string.IsNullOrWhiteSpace(ToDateTimeText))
            {
                var parsed = DataService.checkNull(ToDateTimeText, DateTime.MinValue);
                if (parsed > DateTime.MinValue)
                {
                    toDt = parsed;
                    TrackBarSelectionEnd = parsed.ToOADate();
                }
                else if (TrackBarSelectionEnd > 1000)
                {
                    toDt = DateTime.FromOADate(TrackBarSelectionEnd);
                }
            }
            else if (TrackBarSelectionEnd > 1000)
            {
                toDt = DateTime.FromOADate(TrackBarSelectionEnd);
            }

            bool isEntireLog = SelectedPreset == "Entire Log" ||
                               (LogMinDate > DateTime.MinValue && fromDt.HasValue && fromDt.Value <= LogMinDate.AddMinutes(5) &&
                                toDt.HasValue && toDt.Value >= LogMaxDate.AddMinutes(-5));

            FilteredPoints = isEntireLog
                ? Document.GetFilteredPoints(0, double.MaxValue, null, null)
                : Document.GetFilteredPoints(0, double.MaxValue, fromDt, toDt);
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
        var console = await GetOrCreateConsoleModelAsync();
        var vm = new DataSelectorViewModel(console);
        if (!string.IsNullOrWhiteSpace(SelectedOverviewChannelMnemonic))
        {
            if (!vm.AvailableYCoordinates.Contains(SelectedOverviewChannelMnemonic))
            {
                vm.AvailableYCoordinates.Add(SelectedOverviewChannelMnemonic);
            }

            var match = vm.AvailableSeries.FirstOrDefault(s => s.Contains(SelectedOverviewChannelMnemonic, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                vm.TargetSeries = match;
            }
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
            else if (!string.IsNullOrWhiteSpace(vm.YCoordinate))
            {
                SelectedOverviewChannelMnemonic = vm.YCoordinate;
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

    private async Task<VHTrackConsole> GetOrCreateConsoleModelAsync()
    {
        if (_consoleModel != null && _consoleModel.Tracks.Count > 0 && _consoleModel.Tracks[0].Channels.Count > 0)
        {
            return _consoleModel;
        }

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

        List<LogChannelMetadata>? available = null;
        if (_chartDataService != null && !string.IsNullOrWhiteSpace(LogId))
        {
            try
            {
                available = await _chartDataService.GetAvailableChannelsAsync(LogId, console.DataSource.DatasourceType);
            }
            catch
            {
            }
        }

        if (available != null && available.Count > 0)
        {
            foreach (var ch in available)
            {
                track.Channels.Add(new VHTrackChannel
                {
                    Mnemonic = ch.Mnemonic,
                    Title = !string.IsNullOrWhiteSpace(ch.Name) ? ch.Name : ch.Mnemonic,
                    Unit = ch.Unit
                });
            }
        }
        else
        {
            var fallbackChannels = new List<VHTrackChannel>
            {
                new() { Mnemonic = "DEPTH", Title = "Hole Depth", Unit = "ft" },
                new() { Mnemonic = "BIT_DEPTH", Title = "Bit Depth", Unit = "ft" },
                new() { Mnemonic = "HKLD", Title = "Hookload", Unit = "klb" },
                new() { Mnemonic = "ROPA", Title = "Rate of Penetration", Unit = "ft/hr" },
                new() { Mnemonic = "WOB", Title = "Weight on Bit", Unit = "klb" },
                new() { Mnemonic = "TORQ", Title = "Drilling Torque", Unit = "kft-lb" },
                new() { Mnemonic = "RPM", Title = "Rotary Speed", Unit = "rpm" }
            };
            foreach (var ch in fallbackChannels)
            {
                track.Channels.Add(ch);
            }
        }

        console.Tracks.Add(track);
        _consoleModel = console;
        return _consoleModel;
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
            IsLegendVisible = Document.PlotSettings.ShowLegend;
            ApplyRangeFilter();
        }
    }

    [RelayCommand]
    public async Task OpenBroomstickSetupAsync()
    {
        bool? result;
        if (OpenBroomstickSetupDialogHandler != null)
        {
            result = OpenBroomstickSetupDialogHandler(this);
        }
        else
        {
            var ds = _session?.GetDataService();
            var appDb = App.AppDatabaseService;
            var appDataService = appDb?.GetDataService();
            var contextName = _session != null && _session.IsProjectOpen ? $"Project: {_session.ProjectName}" : "Application Master Template";
            var editorVm = new BroomstickProfileEditorViewModel(ds, contextName, appDataService: appDataService, isProjectContext: _session?.IsProjectOpen == true);
            var dlg = new BroomstickProfileEditorWindow
            {
                DataContext = editorVm,
                Owner = Application.Current?.MainWindow
            };
            result = dlg.ShowDialog();
        }

        if (result == true)
        {
            IsDirty = true;
            await ReloadProfileAsync();
        }
    }

    public async Task ReloadProfileAsync()
    {
        try
        {
            var ds = _session?.GetDataService();
            if (ds != null)
            {
                var profile = BroomstickProfile.LoadSingleProfile(ds, out _, isProjectDatabase: true);
                if (profile != null)
                {
                    Document.Profile = profile;
                    ApplyRangeFilter();
                }
            }
        }
        catch
        {
        }
        await Task.CompletedTask;
    }

    [RelayCommand]

    public async Task RecalculatePointsAsync()

    {

        if (_session == null || string.IsNullOrWhiteSpace(WellId)) return;

        try

        {

            StatusMessage = "Recalculating Broomstick points from well log...";

            var bsDataService = new BroomstickDataService(_session);

            var bsProcessor = new DrillIntel.Services.BroomstickProcessor(bsDataService, _session);

            await bsDataService.ClearBroomstickPointsAsync(WellId);

            var result = await bsProcessor.RecalculatePointsAsync(WellId);



            var ds = _session.GetDataService();

            if (ds != null)

            {

                await LoadBroomstickPointsFromDbAsync(ds);

                ApplyRangeFilter();

            }



            StatusMessage = result.Success

                ? $"Ready. {LoadedPointsCount:N0} points loaded."

                : $"Recalculation warning: {result.ErrorMessage}";

        }

        catch (Exception ex)

        {

            StatusMessage = $"Error recalculating points: {ex.Message}";

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



