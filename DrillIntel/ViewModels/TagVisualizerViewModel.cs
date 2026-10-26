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
using DrillIntel.Data.Objects.DataObjects.Services.AutoTagDetection;
using DrillIntel.Models.TagVisualizer;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for the interactive Tag Visualizer screen.
/// Renders Depth vs DateTime telemetry with overlapping phase tag rectangles,
/// supporting real-time edge dragging, nudge adjustments, phase metadata editing,
/// and bidirectional synchronization with SQLite VMX_PHASE_LIST.
/// </summary>
public partial class TagVisualizerViewModel : ObservableObject
{
    private readonly ProjectSession? _session;
    private readonly IDataServiceDIntel? _dataService;

    public event Action? RequestRepaint;
    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    private string _wellName = "Mock Well 01";

    [ObservableProperty]
    private string _logName = "Primary Telemetry";

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TagVisualizerItem> _tags = new();

    [ObservableProperty]
    private TagVisualizerItem? _selectedTag;

    [ObservableProperty]
    private string _selectedTagStartDateText = string.Empty;

    [ObservableProperty]
    private string _selectedTagEndDateText = string.Empty;

    partial void OnSelectedTagChanged(TagVisualizerItem? value)
    {
        UpdateSelectedTagDateTexts();
    }

    public void UpdateSelectedTagDateTexts()
    {
        if (SelectedTag != null)
        {
            SelectedTagStartDateText = SelectedTag.StartDate.ToString("dd-MMM-yyyy HH:mm:ss");
            SelectedTagEndDateText = SelectedTag.EndDate.ToString("dd-MMM-yyyy HH:mm:ss");
        }
        else
        {
            SelectedTagStartDateText = string.Empty;
            SelectedTagEndDateText = string.Empty;
        }
    }

    [ObservableProperty]
    private List<DepthDataPoint> _depthPoints = new();

    [ObservableProperty]
    private DateTime _dataMinDate = DateTime.Now.AddHours(-12);

    [ObservableProperty]
    private DateTime _dataMaxDate = DateTime.Now;

    [ObservableProperty]
    private DateTime _viewMinDate = DateTime.Now.AddHours(-12);

    [ObservableProperty]
    private DateTime _viewMaxDate = DateTime.Now;

    [ObservableProperty]
    private double _dataMinDepth = 0;

    [ObservableProperty]
    private double _dataMaxDepth = 3000;

    [ObservableProperty]
    private double _viewMinDepth = 0;

    [ObservableProperty]
    private double _viewMaxDepth = 3000;

    [ObservableProperty]
    private bool _invertDepth = true; // Petroleum standard: 0 ft at top, increasing downwards

    [ObservableProperty]
    private bool _showGridLines = true;

    [ObservableProperty]
    private bool _showTagBadges = true;

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    // TrackBar DataSelector Properties
    [ObservableProperty]
    private double _trackBarMin = 0.0;

    [ObservableProperty]
    private double _trackBarMax = 100.0;

    [ObservableProperty]
    private double _trackBarSelectionStart = 0.0;

    [ObservableProperty]
    private double _trackBarSelectionEnd = 100.0;

    [ObservableProperty]
    private PointCollection? _trackBarOverviewPoints;

    [ObservableProperty]
    private string _trackBarOverviewChannelName = "Bit Depth";

    [ObservableProperty]
    private Brush _trackBarOverviewBrush = new SolidColorBrush(Color.FromRgb(25, 118, 210));

    [ObservableProperty]
    private ObservableCollection<string> _rangePresets = new()
    {
        "Full Well", "1 Month", "1 Week", "1 Day", "12 Hours", "4 Hours", "2 Hours", "1 Hour", "30 Mins", "Custom Range"
    };

    [ObservableProperty]
    private string _selectedPreset = "Full Well";

    [ObservableProperty]
    private string _fromDateTimeText = string.Empty;

    [ObservableProperty]
    private string _toDateTimeText = string.Empty;

    private bool _isInternalRangeUpdate = false;

    partial void OnViewMinDateChanged(DateTime value)
    {
        if (!_isInternalRangeUpdate && value != DateTime.MinValue)
        {
            _isInternalRangeUpdate = true;
            try
            {
                TrackBarSelectionStart = value.ToOADate();
                FromDateTimeText = value.ToString("dd-MM-yyyy HH:mm");
            }
            finally
            {
                _isInternalRangeUpdate = false;
            }
        }
    }

    partial void OnViewMaxDateChanged(DateTime value)
    {
        if (!_isInternalRangeUpdate && value != DateTime.MinValue)
        {
            _isInternalRangeUpdate = true;
            try
            {
                TrackBarSelectionEnd = value.ToOADate();
                ToDateTimeText = value.ToString("dd-MM-yyyy HH:mm");
            }
            finally
            {
                _isInternalRangeUpdate = false;
            }
        }
    }

    partial void OnSelectedPresetChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "Custom Range") return;

        DateTime center = ViewMinDate.AddSeconds((ViewMaxDate - ViewMinDate).TotalSeconds / 2.0);

        TimeSpan span = value switch
        {
            "30 Mins" => TimeSpan.FromMinutes(30),
            "1 Hour" => TimeSpan.FromHours(1),
            "2 Hours" => TimeSpan.FromHours(2),
            "4 Hours" => TimeSpan.FromHours(4),
            "12 Hours" => TimeSpan.FromHours(12),
            "1 Day" => TimeSpan.FromDays(1),
            "1 Week" => TimeSpan.FromDays(7),
            "1 Month" => TimeSpan.FromDays(30),
            "Full Well" => DataMaxDate - DataMinDate,
            _ => TimeSpan.FromHours(2)
        };

        if (value == "Full Well")
        {
            ViewMinDate = DataMinDate;
            ViewMaxDate = DataMaxDate;
        }
        else
        {
            DateTime newStart = center.Subtract(TimeSpan.FromSeconds(span.TotalSeconds / 2.0));
            DateTime newEnd = center.Add(TimeSpan.FromSeconds(span.TotalSeconds / 2.0));

            if (newStart < DataMinDate)
            {
                newStart = DataMinDate;
                newEnd = newStart.Add(span);
            }
            if (newEnd > DataMaxDate)
            {
                newEnd = DataMaxDate;
                newStart = newEnd.Subtract(span);
                if (newStart < DataMinDate) newStart = DataMinDate;
            }

            ViewMinDate = newStart;
            ViewMaxDate = newEnd;
        }

        RequestRepaint?.Invoke();
    }

    // Available presets for rapid editing
    public ObservableCollection<string> AvailablePhases { get; } = new()
    {
        "DRILLING", "RIG_UP", "HOLE_CLEANING", "TRIP_OUT", "TRIP_IN", "CASING", "REAMING", "WIRELINE_LOGGING"
    };

    public ObservableCollection<string> AvailableSteps { get; } = new()
    {
        "DRILLING", "CONNECTION", "CIRCULATING", "POOH", "RIH", "NIPPLE_UP", "CASING_RUN", "WIPER_TRIP", "SLIDING", "ROTATING"
    };

    public ObservableCollection<string> AvailableEmphs { get; } = new()
    {
        "MAIN", "BOP", "REAMING", "HOLE_CLEANING", "FORMATION_EVAL", "CEMENTING", "GENERAL"
    };

    private readonly List<long> _deletedPhaseIndices = new();

    public TagVisualizerViewModel(ProjectSession? session = null, string? wellId = null)
    {
        _session = session;
        _dataService = session?.GetDataService();

        if (!string.IsNullOrWhiteSpace(wellId))
        {
            WellId = wellId;
        }

        Initialize();
    }

    public TagVisualizerViewModel(IDataServiceDIntel dataService, string? wellId = null)
    {
        _dataService = dataService;
        if (!string.IsNullOrWhiteSpace(wellId))
        {
            WellId = wellId;
        }

        Initialize();
    }

    private void Initialize()
    {
        bool loadedFromDb = false;

        if (_dataService != null && _dataService.IsConnectionOpen())
        {
            loadedFromDb = TryLoadFromDatabase();
        }

        if (!loadedFromDb)
        {
            // Only loaded if completely decoupled without a database (e.g. standalone unit testing)
            LoadFallbackSampleData();
        }
        else
        {
            if (Tags.Count > 0)
            {
                StatusMessage = $"Loaded {Tags.Count} tags and {DepthPoints.Count} telemetry points from project.";
            }
        }

        if (Tags.Count > 0)
        {
            SelectedTag = Tags.First();
            SelectedTag.IsSelected = true;
        }

        ResetZoom();
    }

    private bool TryLoadFromDatabase()
    {
        try
        {
            if (_dataService == null) return false;

            // Resolve Well ID if empty
            if (string.IsNullOrWhiteSpace(WellId))
            {
                var dtWell = _dataService.GetTable("SELECT WELL_ID, WELL_NAME FROM VMX_WELL LIMIT 1;");
                if (dtWell != null && dtWell.Rows.Count > 0)
                {
                    WellId = dtWell.Rows[0]["WELL_ID"]?.ToString() ?? "";
                    WellName = dtWell.Rows[0]["WELL_NAME"]?.ToString() ?? WellId;
                }
            }
            else
            {
                var dtWell = _dataService.GetTable($"SELECT WELL_NAME FROM VMX_WELL WHERE WELL_ID='{WellId.Replace("'", "''")}' LIMIT 1;");
                if (dtWell != null && dtWell.Rows.Count > 0)
                {
                    WellName = dtWell.Rows[0]["WELL_NAME"]?.ToString() ?? WellId;
                }
            }

            // Find primary timelog
            string sqlTimeLog = !string.IsNullOrWhiteSpace(WellId)
                ? $"SELECT LOG_ID, LOG_NAME, DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE WELL_ID='{WellId.Replace("'", "''")}' ORDER BY PRIMARY_LOG DESC LIMIT 1;"
                : "SELECT LOG_ID, LOG_NAME, DATA_TABLE_NAME FROM VMX_TIME_LOG ORDER BY PRIMARY_LOG DESC LIMIT 1;";

            var dtTimeLog = _dataService.GetTable(sqlTimeLog);
            string dataTable = "";
            if (dtTimeLog != null && dtTimeLog.Rows.Count > 0)
            {
                LogName = dtTimeLog.Rows[0]["LOG_NAME"]?.ToString() ?? "Primary Log";
                dataTable = dtTimeLog.Rows[0]["DATA_TABLE_NAME"]?.ToString() ?? "";
                TableName = dataTable;
            }

            // Load Depth Points
            var points = new List<DepthDataPoint>();
            if (!string.IsNullOrWhiteSpace(dataTable) && _dataService.TableExists(dataTable))
            {
                var cols = _dataService.GetTable($"PRAGMA table_info([{dataTable}]);");
                string depthCol = "DEPTH";
                string timeCol = "DATETIME";
                if (cols != null)
                {
                    var colNames = cols.AsEnumerable().Select(r => r["name"]?.ToString() ?? "").ToList();
                    if (colNames.Any(c => c.Equals("DEPTH", StringComparison.OrdinalIgnoreCase)))
                        depthCol = "DEPTH";
                    else if (colNames.Any(c => c.Equals("BIT_DEPTH", StringComparison.OrdinalIgnoreCase)))
                        depthCol = "BIT_DEPTH";
                    else if (colNames.Any(c => c.Equals("HDTH", StringComparison.OrdinalIgnoreCase)))
                        depthCol = "HDTH";
                    else if (colNames.Any(c => c.Equals("HOLE_DEPTH", StringComparison.OrdinalIgnoreCase)))
                        depthCol = "HOLE_DEPTH";

                    if (colNames.Any(c => c.Equals("DTIM", StringComparison.OrdinalIgnoreCase)))
                        timeCol = "DTIM";
                    else if (colNames.Any(c => c.Equals("DATETIME", StringComparison.OrdinalIgnoreCase)))
                        timeCol = "DATETIME";
                    else if (colNames.Any(c => c.Equals("TIMEINDEX", StringComparison.OrdinalIgnoreCase)))
                        timeCol = "TIMEINDEX";
                }

                // Query in physical sequential insertion order (rowid ASC)
                string query = $"SELECT [{timeCol}], [{depthCol}] FROM [{dataTable}] WHERE [{depthCol}] IS NOT NULL ORDER BY rowid ASC;";
                var dtRaw = _dataService.GetTable(query);
                if (dtRaw != null && dtRaw.Rows.Count > 0)
                {
                    var rawPoints = new List<DepthDataPoint>(dtRaw.Rows.Count);
                    foreach (DataRow row in dtRaw.Rows)
                    {
                        DateTime dt = ParseDate(row[timeCol]);
                        double d = DataService.checkNull(row[depthCol], 0.0);
                        if (dt != DateTime.MinValue)
                        {
                            rawPoints.Add(new DepthDataPoint(dt, d));
                        }
                    }

                    // Explicitly sort raw points chronologically
                    rawPoints.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));

                    // Uniform downsampling across sorted chronological points if > 3000
                    if (rawPoints.Count > 3000)
                    {
                        int step = Math.Max(1, rawPoints.Count / 3000);
                        points = new List<DepthDataPoint>(3100);
                        for (int i = 0; i < rawPoints.Count; i += step)
                        {
                            points.Add(rawPoints[i]);
                        }
                        if (rawPoints.Count > 0 && points[^1].DateTime != rawPoints[^1].DateTime)
                        {
                            points.Add(rawPoints[^1]);
                        }
                    }
                    else
                    {
                        points = rawPoints;
                    }
                }
            }

            DepthPoints = points;

            // Load Tags from VMX_PHASE_LIST
            var loadedTags = new ObservableCollection<TagVisualizerItem>();
            if (_dataService.TableExists("VMX_PHASE_LIST"))
            {
                try
                {
                    ProjectSession.EnsurePhaseListTable(_dataService);
                }
                catch { }

                string tagSql = !string.IsNullOrWhiteSpace(WellId)
                    ? $"SELECT * FROM VMX_PHASE_LIST WHERE WELL_ID='{WellId.Replace("'", "''")}';"
                    : "SELECT * FROM VMX_PHASE_LIST;";

                var dtTags = _dataService.GetTable(tagSql);
                if (dtTags != null)
                {
                    object? SafeGet(DataRow row, string col) => row.Table.Columns.Contains(col) ? row[col] : null;

                    var tagItems = new List<TagVisualizerItem>();
                    foreach (DataRow r in dtTags.Rows)
                    {
                        DateTime sDate = ParseDate(SafeGet(r, "START_DATE"));
                        DateTime eDate = ParseDate(SafeGet(r, "END_DATE"));

                        if (sDate != DateTime.MinValue && eDate != DateTime.MinValue)
                        {
                            // Self-healing: if start > end, swap them to fix backwards dates
                            if (sDate > eDate)
                            {
                                (sDate, eDate) = (eDate, sDate);
                            }
                            else if (sDate == eDate)
                            {
                                eDate = sDate.AddMinutes(15);
                            }

                            var item = new TagVisualizerItem
                            {
                                PhaseIndex = Convert.ToInt64(DataService.checkNull(SafeGet(r, "PHASE_INDEX"), 0.0)),
                                WellId = DataService.checkNull(SafeGet(r, "WELL_ID"), WellId),
                                StartDate = sDate,
                                EndDate = eDate,
                                PhaseId = DataService.checkNull(SafeGet(r, "PHASE_ID"), "PHASE"),
                                StepId = DataService.checkNull(SafeGet(r, "STEP_ID"), ""),
                                EmphId = DataService.checkNull(SafeGet(r, "EMPH_ID"), ""),
                                Notes = DataService.checkNull(SafeGet(r, "NOTES"), ""),
                                CreatedBy = DataService.checkNull(SafeGet(r, "CREATED_BY"), ""),
                                CreatedDate = ParseDate(SafeGet(r, "CREATED_DATE")),
                                ModifiedBy = DataService.checkNull(SafeGet(r, "MODIFIED_BY"), ""),
                                ModifiedDate = ParseDate(SafeGet(r, "MODIFIED_DATE")),
                                IsOpen = Convert.ToInt32(DataService.checkNull(SafeGet(r, "IS_OPEN"), 0))
                            };

                            item.StartDepth = InterpolateDepth(item.StartDate);
                            item.EndDepth = InterpolateDepth(item.EndDate);
                            tagItems.Add(item);
                        }
                    }

                    // Sort chronologically by StartDate, and for overlapping spans, wider span first so narrower/foreground span is drawn on top
                    tagItems.Sort((a, b) =>
                    {
                        int cmp = a.StartDate.CompareTo(b.StartDate);
                        if (cmp != 0) return cmp;
                        return (b.EndDate - b.StartDate).CompareTo(a.EndDate - a.StartDate);
                    });
                    foreach (var item in tagItems)
                    {
                        loadedTags.Add(item);
                    }
                }
            }

            if (loadedTags.Count > 0)
            {
                if (points.Count == 0)
                {
                    DateTime start = loadedTags.Min(t => t.StartDate);
                    DateTime end = loadedTags.Max(t => t.EndDate);
                    if (end <= start) end = start.AddHours(24);
                    double d1 = 1000;
                    double d2 = 2500;
                    int steps = 100;
                    double secStep = (end - start).TotalSeconds / steps;
                    for (int i = 0; i <= steps; i++)
                    {
                        var ptTime = start.AddSeconds(i * secStep);
                        double ptDepth = d1 + (d2 - d1) * (i / (double)steps);
                        points.Add(new DepthDataPoint(ptTime, ptDepth));
                    }
                }

                foreach (var tag in loadedTags)
                {
                    if (tag.StartDepth == 0 && tag.EndDepth == 0)
                    {
                        tag.StartDepth = InterpolateFromList(points, tag.StartDate);
                        tag.EndDepth = InterpolateFromList(points, tag.EndDate);
                    }
                }

                DepthPoints = points;
                Tags = loadedTags;
                HookTagPropertyEvents(Tags);
                HasUnsavedChanges = false;
                UpdateTrackBarOverview();
                return true;
            }

            if (points.Count > 0)
            {
                DepthPoints = points;
                Tags = new ObservableCollection<TagVisualizerItem>();
                HasUnsavedChanges = false;
                UpdateTrackBarOverview();
                StatusMessage = $"Loaded {points.Count} telemetry points from project. No tags found in VMX_PHASE_LIST for well {WellName}.";
                return true;
            }

            DepthPoints = points;
            Tags = new ObservableCollection<TagVisualizerItem>();
            HasUnsavedChanges = false;
            StatusMessage = $"No telemetry data or phase tags found in database for well {WellName}.";
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TagVisualizerViewModel.TryLoadFromDatabase] {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Populates standalone test scenario used when disconnected without an active database.
    /// </summary>
    internal void LoadFallbackSampleData()
    {
        WellName = "Offshore Deepwater Well #4";
        LogName = "High-Res 1-Sec Telemetry";
        DateTime baseTime = new DateTime(2026, 3, 15, 8, 0, 0);

        string effectiveWellId = !string.IsNullOrWhiteSpace(WellId) ? WellId : "MOCK_WELL_01";

        var points = new List<DepthDataPoint>();
        var tags = new ObservableCollection<TagVisualizerItem>();

        // Construct realistic operational scenario (24 hours)
        DateTime t = baseTime;
        double currentDepth = 1000.0;

        // Phase 1: Rig Up (08:00 - 10:00, Depth ~ 1000 ft static)
        DateTime p1Start = t;
        for (int i = 0; i < 120; i++)
        {
            points.Add(new DepthDataPoint(t, currentDepth + Math.Sin(i * 0.1) * 0.5));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1001,
            WellId = effectiveWellId,
            StartDate = p1Start,
            EndDate = t,
            PhaseId = "RIG_UP",
            StepId = "NIPPLE_UP",
            EmphId = "BOP",
            Notes = "Rig up BOP stack and test surface lines."
        });

        // Phase 2: Drilling (10:00 - 14:30, Depth 1000 -> 1550 ft)
        DateTime p2Start = t;
        for (int i = 0; i < 270; i++)
        {
            currentDepth += 2.04;
            points.Add(new DepthDataPoint(t, currentDepth));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1002,
            WellId = effectiveWellId,
            StartDate = p2Start,
            EndDate = t,
            PhaseId = "DRILLING",
            StepId = "DRILLING",
            EmphId = "MAIN",
            Notes = "Rotating on bottom with PDC bit, steady ROP 120 ft/hr."
        });

        // Phase 3: Hole Cleaning / CUC (14:30 - 15:45, Depth ~ 1550 ft static circulation)
        DateTime p3Start = t;
        for (int i = 0; i < 75; i++)
        {
            points.Add(new DepthDataPoint(t, currentDepth + Math.Sin(i * 0.2) * 0.3));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1003,
            WellId = effectiveWellId,
            StartDate = p3Start,
            EndDate = t,
            PhaseId = "HOLE_CLEANING",
            StepId = "CIRCULATING",
            EmphId = "CUC",
            Notes = "Circulating high-vis pill off bottom prior to pulling out."
        });

        // Phase 4: Trip Out (15:45 - 18:30, Depth 1550 -> 100 ft)
        DateTime p4Start = t;
        for (int i = 0; i < 165; i++)
        {
            currentDepth -= 8.78;
            if (currentDepth < 100) currentDepth = 100;
            points.Add(new DepthDataPoint(t, currentDepth));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1004,
            WellId = effectiveWellId,
            StartDate = p4Start,
            EndDate = t,
            PhaseId = "TRIP_OUT",
            StepId = "POOH",
            EmphId = "REAMING",
            Notes = "Pulling out of hole to surface, back-reaming tight spots."
        });

        // Phase 5: Casing Run (18:30 - 21:00, Depth 0 -> 1550 ft)
        DateTime p5Start = t;
        currentDepth = 0;
        for (int i = 0; i < 150; i++)
        {
            currentDepth += 10.33;
            points.Add(new DepthDataPoint(t, currentDepth));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1005,
            WellId = effectiveWellId,
            StartDate = p5Start,
            EndDate = t,
            PhaseId = "CASING",
            StepId = "CASING_RUN",
            EmphId = "CEMENTING",
            Notes = "Running 9-5/8 inch intermediate casing to target shoe depth."
        });

        // Phase 6: Trip In (21:00 - 23:30, Depth 0 -> 1550 ft)
        DateTime p6Start = t;
        currentDepth = 0;
        for (int i = 0; i < 150; i++)
        {
            currentDepth += 10.33;
            points.Add(new DepthDataPoint(t, currentDepth));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1006,
            WellId = effectiveWellId,
            StartDate = p6Start,
            EndDate = t,
            PhaseId = "TRIP_IN",
            StepId = "RIH",
            EmphId = "MAIN",
            Notes = "Running in hole with 8-1/2 inch drilling assembly."
        });

        // Phase 7: Drilling section 2 (23:30 - 04:00 next day, Depth 1550 -> 2100 ft)
        DateTime p7Start = t;
        for (int i = 0; i < 270; i++)
        {
            currentDepth += 2.03;
            points.Add(new DepthDataPoint(t, currentDepth));
            t = t.AddMinutes(1);
        }
        tags.Add(new TagVisualizerItem
        {
            PhaseIndex = 1007,
            WellId = effectiveWellId,
            StartDate = p7Start,
            EndDate = t,
            PhaseId = "DRILLING",
            StepId = "DRILLING",
            EmphId = "MAIN",
            Notes = "Drilling ahead in 8-1/2 inch reservoir section."
        });

        foreach (var tag in tags)
        {
            tag.StartDepth = InterpolateFromList(points, tag.StartDate);
            tag.EndDepth = InterpolateFromList(points, tag.EndDate);
        }

        DepthPoints = points;
        Tags = tags;
        HookTagPropertyEvents(Tags);
        HasUnsavedChanges = false;

        if (Tags.Count > 0)
        {
            SelectedTag = Tags[1]; // Select Drilling tag by default
            SelectedTag.IsSelected = true;
        }

        ResetZoom();
        StatusMessage = "Loaded realistic operational sequence mock data (7 phases).";
    }

    private void HookTagPropertyEvents(IEnumerable<TagVisualizerItem> items)
    {
        foreach (var item in items)
        {
            item.PropertyChanged -= OnTagPropertyChanged;
            item.PropertyChanged += OnTagPropertyChanged;
        }
    }

    private bool _isAdjustingTagBoundary = false;

    private void OnTagPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagVisualizerItem.IsSelected)) return;
        if (e.PropertyName == nameof(TagVisualizerItem.IsModified)) return;
        if (e.PropertyName == nameof(TagVisualizerItem.Duration)) return;
        if (e.PropertyName == nameof(TagVisualizerItem.DurationFormatted)) return;

        if (sender is TagVisualizerItem tag)
        {
            tag.IsModified = true;

            if (!_isAdjustingTagBoundary)
            {
                if (e.PropertyName == nameof(TagVisualizerItem.StartDate))
                {
                    AdjustTagStartDate(tag, tag.StartDate);
                }
                else if (e.PropertyName == nameof(TagVisualizerItem.EndDate))
                {
                    AdjustTagEndDate(tag, tag.EndDate);
                }
            }
        }
        HasUnsavedChanges = true;
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void SelectTag(TagVisualizerItem? tag)
    {
        if (tag == null) return;

        foreach (var t in Tags)
        {
            t.IsSelected = (t == tag);
        }

        SelectedTag = tag;
        UpdateSelectedTagDateTexts();
        RequestRepaint?.Invoke();
    }

    /// <summary>
    /// Adjusts the StartDate of a tag and maintains contiguous synchronization with the preceding adjacent tag.
    /// When stretching or shrinking a tag's start date, the preceding tag's EndDate and EndDepth are updated to match.
    /// </summary>
    public void AdjustTagStartDate(TagVisualizerItem tag, DateTime newStart)
    {
        if (tag == null || _isAdjustingTagBoundary) return;

        try
        {
            _isAdjustingTagBoundary = true;

            var sortedList = Tags.OrderBy(t => t.StartDate).ToList();
            int idx = sortedList.IndexOf(tag);
            if (idx < 0) return;

            TagVisualizerItem? prevTag = idx > 0 ? sortedList[idx - 1] : null;

            // Self-heal corrupted or inverted tag dates
            if (tag.StartDate >= tag.EndDate)
            {
                tag.EndDate = tag.StartDate.AddMinutes(15);
            }

            // Boundary constraints:
            // 1. Cannot exceed or equal current tag's EndDate
            DateTime maxAllowedStart = tag.EndDate.AddSeconds(-30);
            if (newStart > maxAllowedStart)
            {
                newStart = maxAllowedStart;
            }

            // 2. Cannot shrink previous tag to zero or negative duration
            if (prevTag != null)
            {
                DateTime minAllowedStart = prevTag.StartDate.AddSeconds(30);
                if (newStart < minAllowedStart)
                {
                    newStart = minAllowedStart;
                }
            }
            else if (DataMinDate != DateTime.MinValue && newStart < DataMinDate)
            {
                newStart = DataMinDate;
            }

            // Update current tag
            tag.StartDate = newStart;
            tag.StartDepth = InterpolateDepth(newStart);
            tag.IsModified = true;

            // Synchronize preceding tag: previous EndDate must equal current StartDate
            if (prevTag != null)
            {
                prevTag.EndDate = newStart;
                prevTag.EndDepth = tag.StartDepth;
                prevTag.IsModified = true;
            }

            HasUnsavedChanges = true;
            UpdateSelectedTagDateTexts();
            StatusMessage = $"Adjusted Start Date for tag {tag.TagDisplayName}: {newStart:dd-MMM-yyyy HH:mm:ss}";
            RequestRepaint?.Invoke();
        }
        finally
        {
            _isAdjustingTagBoundary = false;
        }
    }

    /// <summary>
    /// Adjusts the EndDate of a tag and maintains contiguous synchronization with the succeeding adjacent tag.
    /// When stretching or shrinking a tag's end date, the succeeding tag's StartDate and StartDepth are updated to match.
    /// </summary>
    public void AdjustTagEndDate(TagVisualizerItem tag, DateTime newEnd)
    {
        if (tag == null || _isAdjustingTagBoundary) return;

        try
        {
            _isAdjustingTagBoundary = true;

            var sortedList = Tags.OrderBy(t => t.StartDate).ToList();
            int idx = sortedList.IndexOf(tag);
            if (idx < 0) return;

            TagVisualizerItem? nextTag = idx < sortedList.Count - 1 ? sortedList[idx + 1] : null;

            // Self-heal corrupted or inverted tag dates
            if (tag.EndDate <= tag.StartDate)
            {
                tag.StartDate = tag.EndDate.AddMinutes(-15);
            }

            // Boundary constraints:
            // 1. Cannot be earlier than or equal to current tag's StartDate
            DateTime minAllowedEnd = tag.StartDate.AddSeconds(30);
            if (newEnd < minAllowedEnd)
            {
                newEnd = minAllowedEnd;
            }

            // 2. Cannot shrink next tag to zero or negative duration
            if (nextTag != null)
            {
                DateTime maxAllowedEnd = nextTag.EndDate.AddSeconds(-30);
                if (newEnd > maxAllowedEnd)
                {
                    newEnd = maxAllowedEnd;
                }
            }
            else if (DataMaxDate != DateTime.MinValue && newEnd > DataMaxDate)
            {
                newEnd = DataMaxDate;
            }

            // Update current tag
            tag.EndDate = newEnd;
            tag.EndDepth = InterpolateDepth(newEnd);
            tag.IsModified = true;

            // Synchronize succeeding tag: next StartDate must equal current EndDate
            if (nextTag != null)
            {
                nextTag.StartDate = newEnd;
                nextTag.StartDepth = tag.EndDepth;
                nextTag.IsModified = true;
            }

            HasUnsavedChanges = true;
            UpdateSelectedTagDateTexts();
            StatusMessage = $"Adjusted End Date for tag {tag.TagDisplayName}: {newEnd:dd-MMM-yyyy HH:mm:ss}";
            RequestRepaint?.Invoke();
        }
        finally
        {
            _isAdjustingTagBoundary = false;
        }
    }

    [RelayCommand]
    public void NudgeStartTime(object? parameter)
    {
        if (SelectedTag == null) return;
        int minutes = ParseMinutes(parameter);
        if (minutes == 0) return;

        DateTime newStart = SelectedTag.StartDate.AddMinutes(minutes);
        AdjustTagStartDate(SelectedTag, newStart);
    }

    [RelayCommand]
    public void NudgeEndTime(object? parameter)
    {
        if (SelectedTag == null) return;
        int minutes = ParseMinutes(parameter);
        if (minutes == 0) return;

        DateTime newEnd = SelectedTag.EndDate.AddMinutes(minutes);
        AdjustTagEndDate(SelectedTag, newEnd);
    }

    [RelayCommand]
    public void ApplySelectedTagStartTime()
    {
        if (SelectedTag == null) return;
        DateTime dt = ParseDate(SelectedTagStartDateText);
        if (dt != DateTime.MinValue)
        {
            AdjustTagStartDate(SelectedTag, dt);
        }
        else
        {
            StatusMessage = "Invalid Start Date format. Please use format like 26-Mar-2013 15:57:00";
        }
    }

    [RelayCommand]
    public void ApplySelectedTagEndTime()
    {
        if (SelectedTag == null) return;
        DateTime dt = ParseDate(SelectedTagEndDateText);
        if (dt != DateTime.MinValue)
        {
            AdjustTagEndDate(SelectedTag, dt);
        }
        else
        {
            StatusMessage = "Invalid End Date format. Please use format like 26-Mar-2013 15:57:00";
        }
    }

    private static int ParseMinutes(object? parameter)
    {
        if (parameter is int i) return i;
        if (parameter is double d) return (int)d;
        if (parameter is long l) return (int)l;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }
        return 0;
    }

    [RelayCommand]
    public void AddTag()
    {
        DateTime start = ViewMinDate.AddSeconds((ViewMaxDate - ViewMinDate).TotalSeconds * 0.4);
        DateTime end = start.AddHours(2);

        long newIndex = Tags.Count > 0 ? Tags.Max(t => t.PhaseIndex) + 1 : 1000;

        var newTag = new TagVisualizerItem
        {
            PhaseIndex = newIndex,
            WellId = WellId,
            StartDate = start,
            EndDate = end,
            PhaseId = "DRILLING",
            StepId = "DRILLING",
            EmphId = "MAIN",
            Notes = "User created tag",
            IsModified = true,
            StartDepth = InterpolateDepth(start),
            EndDepth = InterpolateDepth(end)
        };

        newTag.PropertyChanged += OnTagPropertyChanged;
        Tags.Add(newTag);
        SelectTag(newTag);
        HasUnsavedChanges = true;
        StatusMessage = $"Added new tag {newTag.TagDisplayName}.";
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void SplitSelectedTag()
    {
        if (SelectedTag == null) return;

        var midTime = SelectedTag.StartDate.AddSeconds(SelectedTag.Duration.TotalSeconds / 2.0);
        DateTime oldEnd = SelectedTag.EndDate;

        _isAdjustingTagBoundary = true;
        try
        {
            // Shrink current tag
            SelectedTag.EndDate = midTime;
            SelectedTag.EndDepth = InterpolateDepth(midTime);
            SelectedTag.IsModified = true;

            // Create new second half tag
            long newIndex = Tags.Max(t => t.PhaseIndex) + 1;
            var secondHalf = new TagVisualizerItem
            {
                PhaseIndex = newIndex,
                WellId = SelectedTag.WellId,
                StartDate = midTime,
                EndDate = oldEnd,
                PhaseId = SelectedTag.PhaseId,
                StepId = SelectedTag.StepId,
                EmphId = SelectedTag.EmphId,
                Notes = SelectedTag.Notes,
                IsModified = true,
                StartDepth = InterpolateDepth(midTime),
                EndDepth = InterpolateDepth(oldEnd)
            };

            secondHalf.PropertyChanged += OnTagPropertyChanged;
            int idx = Tags.IndexOf(SelectedTag);
            Tags.Insert(idx + 1, secondHalf);
            UpdateSelectedTagDateTexts();
        }
        finally
        {
            _isAdjustingTagBoundary = false;
        }

        HasUnsavedChanges = true;
        StatusMessage = $"Split tag into two intervals at {midTime:dd-MMM-yyyy HH:mm:ss}.";
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void MergeWithNextTag()
    {
        if (SelectedTag == null) return;

        int idx = Tags.IndexOf(SelectedTag);
        if (idx < 0 || idx >= Tags.Count - 1)
        {
            StatusMessage = "No next tag to merge with.";
            return;
        }

        var nextTag = Tags[idx + 1];
        DateTime targetEnd = nextTag.EndDate;
        double targetDepth = nextTag.EndDepth;
        _deletedPhaseIndices.Add(nextTag.PhaseIndex);

        _isAdjustingTagBoundary = true;
        try
        {
            Tags.RemoveAt(idx + 1);
            SelectedTag.EndDate = targetEnd;
            SelectedTag.EndDepth = targetDepth;
            SelectedTag.IsModified = true;
            UpdateSelectedTagDateTexts();
        }
        finally
        {
            _isAdjustingTagBoundary = false;
        }

        HasUnsavedChanges = true;
        StatusMessage = $"Merged with next tag. New end date: {SelectedTag.EndDate:dd-MMM-yyyy HH:mm:ss}.";
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void DeleteSelectedTag()
    {
        if (SelectedTag == null) return;

        _deletedPhaseIndices.Add(SelectedTag.PhaseIndex);
        int idx = Tags.IndexOf(SelectedTag);
        Tags.Remove(SelectedTag);

        if (Tags.Count > 0)
        {
            int nextIdx = Math.Min(idx, Tags.Count - 1);
            SelectTag(Tags[nextIdx]);
        }
        else
        {
            SelectedTag = null;
        }

        HasUnsavedChanges = true;
        StatusMessage = "Deleted selected tag.";
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public async Task SaveToDatabaseAsync()
    {
        if (_dataService == null || !_dataService.IsConnectionOpen())
        {
            StatusMessage = "Changes saved locally in memory (No active database connection).";
            HasUnsavedChanges = false;
            return;
        }

        try
        {
            // Ensure table schema is up to date
            ProjectSession.EnsurePhaseListTable(_dataService);

            // 1. Delete removed tags
            foreach (var delIdx in _deletedPhaseIndices)
            {
                _dataService.ExecuteNonQuery($"DELETE FROM VMX_PHASE_LIST WHERE WELL_ID='{WellId.Replace("'", "''")}' AND PHASE_INDEX={delIdx};");
            }
            _deletedPhaseIndices.Clear();

            // 2. Insert or update tags
            int savedCount = 0;
            string currentUser = !string.IsNullOrWhiteSpace(_dataService.UserName) ? _dataService.UserName : "SYSTEM";
            string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

            foreach (var tag in Tags)
            {
                string checkSql = $"SELECT COUNT(*) FROM VMX_PHASE_LIST WHERE WELL_ID='{WellId.Replace("'", "''")}' AND PHASE_INDEX={tag.PhaseIndex};";
                var existsVal = _dataService.GetValue(checkSql);
                bool exists = existsVal != null && Convert.ToInt32(existsVal) > 0;

                if (exists)
                {
                    string updateSql = $"UPDATE VMX_PHASE_LIST SET " +
                        $"START_DATE='{tag.StartDate:dd-MMM-yyyy HH:mm:ss}', " +
                        $"END_DATE='{tag.EndDate:dd-MMM-yyyy HH:mm:ss}', " +
                        $"PHASE_ID='{tag.PhaseId.Replace("'", "''")}', " +
                        $"STEP_ID='{tag.StepId.Replace("'", "''")}', " +
                        $"EMPH_ID='{tag.EmphId.Replace("'", "''")}', " +
                        $"NOTES='{(tag.Notes ?? "").Replace("'", "''")}', " +
                        $"MODIFIED_BY='{currentUser}', " +
                        $"MODIFIED_DATE='{now}' " +
                        $"WHERE WELL_ID='{WellId.Replace("'", "''")}' AND PHASE_INDEX={tag.PhaseIndex};";

                    _dataService.ExecuteNonQuery(updateSql);
                }
                else
                {
                    string insertSql = $"INSERT INTO VMX_PHASE_LIST " +
                        $"(PHASE_INDEX, WELL_ID, START_DATE, END_DATE, PHASE_ID, STEP_ID, EMPH_ID, NOTES, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, IS_OPEN) VALUES (" +
                        $"{tag.PhaseIndex}, '{WellId.Replace("'", "''")}', '{tag.StartDate:dd-MMM-yyyy HH:mm:ss}', '{tag.EndDate:dd-MMM-yyyy HH:mm:ss}', " +
                        $"'{tag.PhaseId.Replace("'", "''")}', '{tag.StepId.Replace("'", "''")}', '{tag.EmphId.Replace("'", "''")}', " +
                        $"'{(tag.Notes ?? "").Replace("'", "''")}', '{currentUser}', '{now}', '{currentUser}', '{now}', {tag.IsOpen});";

                    _dataService.ExecuteNonQuery(insertSql);
                }

                tag.IsModified = false;
                savedCount++;
            }

            HasUnsavedChanges = false;
            StatusMessage = $"Successfully saved {savedCount} phase tags to database table VMX_PHASE_LIST!";
            _session?.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save tags: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ResetZoom()
    {
        if (DepthPoints.Count > 0)
        {
            DataMinDate = DepthPoints.Min(p => p.DateTime);
            DataMaxDate = DepthPoints.Max(p => p.DateTime);
            DataMinDepth = DepthPoints.Min(p => p.Depth);
            DataMaxDepth = DepthPoints.Max(p => p.Depth);
        }
        else if (Tags.Count > 0)
        {
            DataMinDate = Tags.Min(t => t.StartDate);
            DataMaxDate = Tags.Max(t => t.EndDate);
            DataMinDepth = 0;
            DataMaxDepth = 3000;
        }

        // Add 5% padding to depth
        double depthSpan = Math.Max(100, DataMaxDepth - DataMinDepth);
        ViewMinDepth = Math.Max(0, DataMinDepth - depthSpan * 0.05);
        ViewMaxDepth = DataMaxDepth + depthSpan * 0.05;

        // View time matches full extents
        _isInternalRangeUpdate = true;
        try
        {
            ViewMinDate = DataMinDate;
            ViewMaxDate = DataMaxDate;
            TrackBarMin = DataMinDate.ToOADate();
            TrackBarMax = DataMaxDate.ToOADate();
            TrackBarSelectionStart = DataMinDate.ToOADate();
            TrackBarSelectionEnd = DataMaxDate.ToOADate();
            FromDateTimeText = DataMinDate.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = DataMaxDate.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Full Well";
        }
        finally
        {
            _isInternalRangeUpdate = false;
        }

        UpdateTrackBarOverview();
        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        double currentSpanSeconds = (ViewMaxDate - ViewMinDate).TotalSeconds;
        if (currentSpanSeconds < 30) return; // Min 30 seconds

        var center = ViewMinDate.AddSeconds(currentSpanSeconds / 2.0);
        double newSpan = currentSpanSeconds * 0.7;

        _isInternalRangeUpdate = true;
        try
        {
            ViewMinDate = center.AddSeconds(-newSpan / 2.0);
            ViewMaxDate = center.AddSeconds(newSpan / 2.0);
            TrackBarSelectionStart = ViewMinDate.ToOADate();
            TrackBarSelectionEnd = ViewMaxDate.ToOADate();
            FromDateTimeText = ViewMinDate.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = ViewMaxDate.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Custom Range";
        }
        finally
        {
            _isInternalRangeUpdate = false;
        }

        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void ZoomOut()
    {
        double currentSpanSeconds = (ViewMaxDate - ViewMinDate).TotalSeconds;
        var center = ViewMinDate.AddSeconds(currentSpanSeconds / 2.0);
        double newSpan = currentSpanSeconds * 1.4;

        DateTime newMin = center.AddSeconds(-newSpan / 2.0);
        DateTime newMax = center.AddSeconds(newSpan / 2.0);

        if (newMin < DataMinDate.AddDays(-2)) newMin = DataMinDate;
        if (newMax > DataMaxDate.AddDays(2)) newMax = DataMaxDate;

        _isInternalRangeUpdate = true;
        try
        {
            ViewMinDate = newMin;
            ViewMaxDate = newMax;
            TrackBarSelectionStart = newMin.ToOADate();
            TrackBarSelectionEnd = newMax.ToOADate();
            FromDateTimeText = newMin.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = newMax.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Custom Range";
        }
        finally
        {
            _isInternalRangeUpdate = false;
        }

        RequestRepaint?.Invoke();
    }

    [RelayCommand]
    public void RangeTrackBarChanged()
    {
        if (TrackBarSelectionStart > 1000 && TrackBarSelectionEnd > TrackBarSelectionStart)
        {
            _isInternalRangeUpdate = true;
            try
            {
                ViewMinDate = DateTime.FromOADate(TrackBarSelectionStart);
                ViewMaxDate = DateTime.FromOADate(TrackBarSelectionEnd);
                FromDateTimeText = ViewMinDate.ToString("dd-MM-yyyy HH:mm");
                ToDateTimeText = ViewMaxDate.ToString("dd-MM-yyyy HH:mm");
                SelectedPreset = "Custom Range";
            }
            finally
            {
                _isInternalRangeUpdate = false;
            }
            RequestRepaint?.Invoke();
        }
    }

    [RelayCommand]
    public void ApplyDateTimeRange()
    {
        DateTime start = ParseDate(FromDateTimeText);
        DateTime end = ParseDate(ToDateTimeText);

        if (start != DateTime.MinValue && end != DateTime.MinValue && end > start)
        {
            _isInternalRangeUpdate = true;
            try
            {
                ViewMinDate = start;
                ViewMaxDate = end;
                TrackBarSelectionStart = start.ToOADate();
                TrackBarSelectionEnd = end.ToOADate();
                SelectedPreset = "Custom Range";
            }
            finally
            {
                _isInternalRangeUpdate = false;
            }
            RequestRepaint?.Invoke();
        }
    }

    [RelayCommand]
    public void ZoomToSelectedTag()
    {
        if (SelectedTag == null) return;

        var span = SelectedTag.Duration;
        var padSeconds = Math.Max(300, span.TotalSeconds * 0.25); // 25% margin
        var newStart = SelectedTag.StartDate.AddSeconds(-padSeconds);
        var newEnd = SelectedTag.EndDate.AddSeconds(padSeconds);

        if (newStart < DataMinDate) newStart = DataMinDate;
        if (newEnd > DataMaxDate) newEnd = DataMaxDate;

        _isInternalRangeUpdate = true;
        try
        {
            ViewMinDate = newStart;
            ViewMaxDate = newEnd;
            TrackBarSelectionStart = newStart.ToOADate();
            TrackBarSelectionEnd = newEnd.ToOADate();
            FromDateTimeText = newStart.ToString("dd-MM-yyyy HH:mm");
            ToDateTimeText = newEnd.ToString("dd-MM-yyyy HH:mm");
            SelectedPreset = "Custom Range";
        }
        finally
        {
            _isInternalRangeUpdate = false;
        }

        RequestRepaint?.Invoke();
    }

    public void UpdateTrackBarOverview()
    {
        if (DepthPoints == null || DepthPoints.Count < 2)
        {
            TrackBarOverviewPoints = null;
            return;
        }

        int sampleStep = Math.Max(1, DepthPoints.Count / 500);
        var pts = new PointCollection(DepthPoints.Count / sampleStep + 2);
        for (int i = 0; i < DepthPoints.Count; i += sampleStep)
        {
            pts.Add(new Point(DepthPoints[i].DateTime.ToOADate(), DepthPoints[i].Depth));
        }
        if (pts.Count > 0 && DepthPoints.Count > 0 && pts[^1].X != DepthPoints[^1].DateTime.ToOADate())
        {
            pts.Add(new Point(DepthPoints[^1].DateTime.ToOADate(), DepthPoints[^1].Depth));
        }

        TrackBarOverviewPoints = pts;
        TrackBarMin = DataMinDate.ToOADate();
        TrackBarMax = DataMaxDate.ToOADate();
        TrackBarSelectionStart = ViewMinDate.ToOADate();
        TrackBarSelectionEnd = ViewMaxDate.ToOADate();
    }

    public void NotifyTagDragged(TagVisualizerItem tag)
    {
        tag.StartDepth = InterpolateDepth(tag.StartDate);
        tag.EndDepth = InterpolateDepth(tag.EndDate);
        tag.IsModified = true;
        HasUnsavedChanges = true;
        StatusMessage = $"Adjusted {tag.TagDisplayName}: {tag.StartDate:HH:mm} -> {tag.EndDate:HH:mm} ({tag.DurationFormatted})";
    }

    private double InterpolateDepth(DateTime dt)
    {
        return InterpolateFromList(DepthPoints, dt);
    }

    private static double InterpolateFromList(List<DepthDataPoint> points, DateTime dt)
    {
        if (points == null || points.Count == 0) return 0;
        if (dt <= points[0].DateTime) return points[0].Depth;
        if (dt >= points[^1].DateTime) return points[^1].Depth;

        // Binary search for surrounding points
        int low = 0, high = points.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) / 2;
            if (points[mid].DateTime == dt) return points[mid].Depth;
            if (points[mid].DateTime < dt) low = mid + 1;
            else high = mid - 1;
        }

        int idx1 = Math.Max(0, high);
        int idx2 = Math.Min(points.Count - 1, low);

        if (idx1 == idx2) return points[idx1].Depth;

        double totalSecs = (points[idx2].DateTime - points[idx1].DateTime).TotalSeconds;
        if (totalSecs <= 0) return points[idx1].Depth;

        double frac = (dt - points[idx1].DateTime).TotalSeconds / totalSecs;
        return points[idx1].Depth + frac * (points[idx2].Depth - points[idx1].Depth);
    }

    public static DateTime ParseDate(object? obj)
    {
        if (obj == null || obj == DBNull.Value) return DateTime.MinValue;
        if (obj is DateTime d) return d;

        string str = obj.ToString()?.Trim() ?? "";
        if (string.IsNullOrEmpty(str)) return DateTime.MinValue;

        if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double oa))
        {
            if (oa > 30000 && oa < 70000)
            {
                try { return DateTime.FromOADate(oa); } catch { }
            }
        }

        string[] formats = new[]
        {
            "dd-MMM-yyyy HH:mm:ss",
            "dd-MMM-yyyy HH:mm",
            "dd-MMM-yyyy",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "dd-MM-yyyy HH:mm:ss"
        };

        if (DateTime.TryParseExact(str, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        if (DateTime.TryParse(str, out dt))
            return dt;

        return DateTime.MinValue;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(!HasUnsavedChanges);
    }
}
