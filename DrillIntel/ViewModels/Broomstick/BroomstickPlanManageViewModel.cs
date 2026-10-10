using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Services;
using Microsoft.Win32;

namespace DrillIntel.ViewModels;

#region Row ViewModel
public partial class HookloadPlanDataRowViewModel : ObservableObject
{
    [ObservableProperty]
    private int _srNo;

    [ObservableProperty]
    private double _depth;

    [ObservableProperty]
    private double _weight;

    [ObservableProperty]
    private double _maxTension;

    [ObservableProperty]
    private double _minTension;

    [ObservableProperty]
    private double _maxCompress;

    [ObservableProperty]
    private double _minCompress;

    public HookloadPlanData ToDataObject()
    {
        return new HookloadPlanData
        {
            Depth = Depth,
            Weight = Weight,
            MaxTension = MaxTension,
            MinTension = MinTension,
            MaxCompress = MaxCompress,
            MinCompress = MinCompress
        };
    }

    public static HookloadPlanDataRowViewModel FromDataObject(int index, HookloadPlanData data)
    {
        return new HookloadPlanDataRowViewModel
        {
            SrNo = index,
            Depth = data.Depth,
            Weight = data.Weight,
            MaxTension = data.MaxTension,
            MinTension = data.MinTension,
            MaxCompress = data.MaxCompress,
            MinCompress = data.MinCompress
        };
    }
}
#endregion

#region Tree Models
public partial class PlanTreeItemViewModel : ObservableObject
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public string RunNo { get; set; } = string.Empty;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RunNo) && !RunNo.Equals(PlanName, StringComparison.OrdinalIgnoreCase))
            {
                return RunNo;
            }
            if (!string.IsNullOrWhiteSpace(GroupName) && PlanName.StartsWith(GroupName, StringComparison.OrdinalIgnoreCase) && PlanName.Length > GroupName.Length)
            {
                string sub = PlanName.Substring(GroupName.Length).Trim();
                if (!string.IsNullOrWhiteSpace(sub))
                    return sub;
            }
            return PlanName;
        }
    }

    public string BreadcrumbTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(RunNo) && !RunNo.Equals(PlanName, StringComparison.OrdinalIgnoreCase))
            {
                return $"{PlanName}  ›  Run: {RunNo}";
            }
            return PlanName;
        }
    }

    public bool HasRunNo => !string.IsNullOrWhiteSpace(RunNo) && !RunNo.Equals(PlanName, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isActive;

    public AdnlHookloadPlan? RawPlan { get; set; }

    public int TotalDataPoints
    {
        get
        {
            if (RawPlan == null) return 0;
            return (RawPlan.pickup?.Count ?? 0)
                 + (RawPlan.slackoff?.Count ?? 0)
                 + (RawPlan.rotate?.Count ?? 0)
                 + (RawPlan.torque?.Count ?? 0)
                 + (RawPlan.onTorque?.Count ?? 0)
                 + (RawPlan.mkTorque?.Count ?? 0)
                 + (RawPlan.tqLimit?.Count ?? 0)
                 + (RawPlan.sinRot?.Count ?? 0);
        }
    }
}

public partial class PlanTreeGroupViewModel : ObservableObject
{
    public string GroupName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    public ObservableCollection<PlanTreeItemViewModel> Plans { get; } = new();

    partial void OnIsSelectedChanged(bool value)
    {
        foreach (var p in Plans)
        {
            p.IsSelected = value;
        }
    }
}
#endregion

#region Timelog Context Model
public class TimelogContextItem
{
    public string WellId { get; set; } = string.Empty;
    public string WellName { get; set; } = string.Empty;
    public string WellboreId { get; set; } = string.Empty;
    public string WellboreName { get; set; } = string.Empty;
    public string LogId { get; set; } = string.Empty;
    public string LogName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsAllPlansOption { get; set; }
    public int PlanCount { get; set; }

    public string DisplayText
    {
        get
        {
            if (IsAllPlansOption)
            {
                return PlanCount > 0
                    ? $"★ All Plans (All Wells / Timelogs) ({PlanCount})"
                    : "★ All Plans (All Wells / Timelogs)";
            }

            string primaryTag = IsPrimary ? " [Primary]" : "";
            string countTag = PlanCount > 0 ? $" ({PlanCount} plans)" : "";
            return $"{WellName}  ›  {WellboreName}  ›  {LogName}{primaryTag}{countTag}";
        }
    }

    public override string ToString() => DisplayText;
}
#endregion

#region Main ViewModel
public partial class BroomstickPlanManageViewModel : ObservableObject
{
    private readonly IDataServiceDIntel? _dataService;
    private readonly PlanImportService _importService = new();

    public string WellID { get; set; } = string.Empty;
    public string WellboreID { get; set; } = string.Empty;
    public string LogID { get; set; } = string.Empty;

    // Timelog & Well Context
    public ObservableCollection<TimelogContextItem> AvailableTimelogContexts { get; } = new();

    [ObservableProperty]
    private TimelogContextItem? _selectedTimelogContext;

    [ObservableProperty]
    private string _currentWellName = string.Empty;

    [ObservableProperty]
    private string _currentWellboreName = string.Empty;

    [ObservableProperty]
    private string _currentLogName = string.Empty;

    [ObservableProperty]
    private bool _isPrimaryTimelog;

    [ObservableProperty]
    private bool _hasNoPrimaryTimelog;

    [ObservableProperty]
    private bool _hasMultipleTimelogs;

    [ObservableProperty]
    private string _windowTitle = "BroomStick Manage Plan";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private bool _isStatusError;

    // Left Tree
    public ObservableCollection<PlanTreeGroupViewModel> PlanGroups { get; } = new();
    private readonly List<AdnlHookloadPlan> _allPlans = new();

    [ObservableProperty]
    private PlanTreeItemViewModel? _activePlanItem;

    [ObservableProperty]
    private AdnlHookloadPlan? _currentPlan;

    // Tabs: 0=Pickup, 1=SlackOff, 2=Rotate, 3=Off Bottom Torque, 4=On Bottom Torque, 5=Make-Up Torque, 6=Torque Limit, 7=Sinusoidal
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private string _selectedTabTitle = "Pickup";

    // Current Table Data
    public ObservableCollection<HookloadPlanDataRowViewModel> CurrentTableRows { get; } = new();

    [ObservableProperty]
    private string _totalRowsText = "Total Rows: 0";

    [ObservableProperty]
    private string _currentDataSummaryText = string.Empty;

    /// <summary>
    /// Optional hook to bypass UI MessageBox during unit testing.
    /// Returns true if deletion is confirmed.
    /// </summary>
    public Func<int, bool>? ConfirmDeleteHandler { get; set; }

    public BroomstickPlanManageViewModel(
        IDataServiceDIntel? dataService,
        string wellId = "",
        string wellboreId = "",
        string logId = "")
    {
        _dataService = dataService;
        WellID = wellId ?? "";
        WellboreID = wellboreId ?? "";
        LogID = logId ?? "";

        InitializeTimelogContexts();
    }

    partial void OnSelectedTimelogContextChanged(TimelogContextItem? value)
    {
        if (value == null) return;

        if (value.IsAllPlansOption)
        {
            WellID = "";
            WellboreID = "";
            LogID = "";
            CurrentWellName = "All Wells";
            CurrentWellboreName = "All Wellbores";
            CurrentLogName = "All Timelogs";
            IsPrimaryTimelog = false;
            WindowTitle = "BroomStick Manage Plan — All Wells & Timelogs";
        }
        else
        {
            WellID = value.WellId;
            WellboreID = value.WellboreId;
            LogID = value.LogId;
            CurrentWellName = !string.IsNullOrWhiteSpace(value.WellName) ? value.WellName : value.WellId;
            CurrentWellboreName = !string.IsNullOrWhiteSpace(value.WellboreName) ? value.WellboreName : value.WellboreId;
            CurrentLogName = !string.IsNullOrWhiteSpace(value.LogName) ? value.LogName : value.LogId;
            IsPrimaryTimelog = value.IsPrimary;
            WindowTitle = $"BroomStick Manage Plan — Well: {CurrentWellName} | Wellbore: {CurrentWellboreName} | Timelog: {CurrentLogName}";
        }

        ReloadPlans();
    }

    public void InitializeTimelogContexts()
    {
        AvailableTimelogContexts.Clear();

        var planCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (_dataService != null)
        {
            try
            {
                var dtCounts = _dataService.GetTable("SELECT LOG_ID, COUNT(*) AS CNT FROM VMX_ADNL_HKLD_PLAN GROUP BY LOG_ID;");
                if (dtCounts != null && dtCounts.Rows != null)
                {
                    foreach (DataRow r in dtCounts.Rows)
                    {
                        string lid = Convert.ToString(r["LOG_ID"]) ?? "";
                        int cnt = Convert.ToInt32(r["CNT"]);
                        planCounts[lid] = cnt;
                    }
                }
            }
            catch { }
        }

        var items = new List<TimelogContextItem>();

        if (_dataService != null)
        {
            bool hasTimeLogTable = false;
            try
            {
                var obj = _dataService.GetValue("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");
                hasTimeLogTable = Convert.ToInt32(obj) > 0;
            }
            catch { }

            if (hasTimeLogTable)
            {
                try
                {
                    string sql = @"
SELECT t.WELL_ID, t.WELLBORE_ID, t.LOG_ID, t.LOG_NAME, t.PRIMARY_LOG,
       w.WELL_NAME, wb.WELLBORE_NAME
FROM VMX_TIME_LOG t
LEFT JOIN VMX_WELL w ON t.WELL_ID = w.WELL_ID
LEFT JOIN VMX_WELLBORE wb ON t.WELLBORE_ID = wb.WELLBORE_ID
ORDER BY t.WELL_ID, t.WELLBORE_ID, t.PRIMARY_LOG DESC, t.LOG_NAME;";

                    DataTable dt = _dataService.GetTable(sql);
                    if (dt != null && dt.Rows != null)
                    {
                        foreach (DataRow r in dt.Rows)
                        {
                            string wId = Convert.ToString(r["WELL_ID"]) ?? "";
                            string wbId = Convert.ToString(r["WELLBORE_ID"]) ?? "";
                            string lId = Convert.ToString(r["LOG_ID"]) ?? "";
                            string lName = Convert.ToString(r["LOG_NAME"]) ?? "";
                            string wName = Convert.ToString(r["WELL_NAME"]) ?? "";
                            string wbName = Convert.ToString(r["WELLBORE_NAME"]) ?? "";
                            bool isPrimary = false;
                            var primVal = r["PRIMARY_LOG"];
                            if (primVal != null && primVal != DBNull.Value)
                            {
                                isPrimary = Convert.ToInt32(primVal) == 1;
                            }

                            if (string.IsNullOrWhiteSpace(wName)) wName = !string.IsNullOrWhiteSpace(wId) ? wId : "Default Well";
                            if (string.IsNullOrWhiteSpace(wbName)) wbName = !string.IsNullOrWhiteSpace(wbId) ? wbId : "Default Wellbore";
                            if (string.IsNullOrWhiteSpace(lName)) lName = !string.IsNullOrWhiteSpace(lId) ? lId : "Default Timelog";

                            int pCount = planCounts.TryGetValue(lId, out int c) ? c : 0;

                            items.Add(new TimelogContextItem
                            {
                                WellId = wId,
                                WellName = wName,
                                WellboreId = wbId,
                                WellboreName = wbName,
                                LogId = lId,
                                LogName = lName,
                                IsPrimary = isPrimary,
                                PlanCount = pCount
                            });
                        }
                    }
                }
                catch
                {
                    try
                    {
                        DataTable dt = _dataService.GetTable("SELECT WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, PRIMARY_LOG FROM VMX_TIME_LOG;");
                        if (dt != null && dt.Rows != null)
                        {
                            foreach (DataRow r in dt.Rows)
                            {
                                string wId = Convert.ToString(r["WELL_ID"]) ?? "";
                                string wbId = Convert.ToString(r["WELLBORE_ID"]) ?? "";
                                string lId = Convert.ToString(r["LOG_ID"]) ?? "";
                                string lName = Convert.ToString(r["LOG_NAME"]) ?? "";
                                bool isPrimary = Convert.ToInt32(r["PRIMARY_LOG"]) == 1;
                                int pCount = planCounts.TryGetValue(lId, out int c) ? c : 0;

                                items.Add(new TimelogContextItem
                                {
                                    WellId = wId,
                                    WellName = !string.IsNullOrWhiteSpace(wId) ? wId : "Default Well",
                                    WellboreId = wbId,
                                    WellboreName = !string.IsNullOrWhiteSpace(wbId) ? wbId : "Default Wellbore",
                                    LogId = lId,
                                    LogName = !string.IsNullOrWhiteSpace(lName) ? lName : lId,
                                    IsPrimary = isPrimary,
                                    PlanCount = pCount
                                });
                            }
                        }
                    }
                    catch { }
                }
            }

            // Also check distinct contexts from existing plans
            try
            {
                DataTable dtPlans = _dataService.GetTable("SELECT DISTINCT WELL_ID, WELLBORE_ID, LOG_ID FROM VMX_ADNL_HKLD_PLAN;");
                if (dtPlans != null && dtPlans.Rows != null)
                {
                    foreach (DataRow r in dtPlans.Rows)
                    {
                        string lId = Convert.ToString(r["LOG_ID"]) ?? "";
                        if (!string.IsNullOrWhiteSpace(lId) && !items.Any(x => x.LogId.Equals(lId, StringComparison.OrdinalIgnoreCase)))
                        {
                            string wId = Convert.ToString(r["WELL_ID"]) ?? "";
                            string wbId = Convert.ToString(r["WELLBORE_ID"]) ?? "";
                            int pCount = planCounts.TryGetValue(lId, out int c) ? c : 0;
                            items.Add(new TimelogContextItem
                            {
                                WellId = wId,
                                WellName = !string.IsNullOrWhiteSpace(wId) ? wId : "Well",
                                WellboreId = wbId,
                                WellboreName = !string.IsNullOrWhiteSpace(wbId) ? wbId : "Wellbore",
                                LogId = lId,
                                LogName = lId,
                                IsPrimary = false,
                                PlanCount = pCount
                            });
                        }
                    }
                }
            }
            catch { }
        }

        // Fallback placeholder if no database or no records
        if (items.Count == 0)
        {
            string wId = !string.IsNullOrWhiteSpace(WellID) ? WellID : "WELL-01";
            string wbId = !string.IsNullOrWhiteSpace(WellboreID) ? WellboreID : "WB-01";
            string lId = !string.IsNullOrWhiteSpace(LogID) ? LogID : "LOG-01";
            items.Add(new TimelogContextItem
            {
                WellId = wId,
                WellName = !string.IsNullOrWhiteSpace(wId) ? wId : "Default Well",
                WellboreId = wbId,
                WellboreName = !string.IsNullOrWhiteSpace(wbId) ? wbId : "Default Wellbore",
                LogId = lId,
                LogName = !string.IsNullOrWhiteSpace(lId) ? lId : "Default Timelog",
                IsPrimary = true,
                PlanCount = 0
            });
        }

        // Determine multiple / no primary status across timelogs
        HasMultipleTimelogs = items.Count > 1;
        HasNoPrimaryTimelog = HasMultipleTimelogs && items.All(c => !c.IsPrimary);

        // Add "★ All Plans (All Wells / Timelogs)" option as the first option
        var allPlansOption = new TimelogContextItem
        {
            IsAllPlansOption = true,
            PlanCount = planCounts.Values.Sum()
        };
        AvailableTimelogContexts.Add(allPlansOption);

        foreach (var it in items)
        {
            AvailableTimelogContexts.Add(it);
        }

        // Priority Auto-Selection:
        TimelogContextItem? target = null;

        // 1. If explicit LogID was provided by caller, match it
        if (!string.IsNullOrEmpty(LogID))
        {
            target = items.FirstOrDefault(i => i.LogId.Equals(LogID, StringComparison.OrdinalIgnoreCase));
        }

        // 2. If wellId is provided, search within well
        if (target == null && !string.IsNullOrEmpty(WellID))
        {
            var wellItems = items.Where(i => i.WellId.Equals(WellID, StringComparison.OrdinalIgnoreCase)).ToList();
            if (wellItems.Count > 0)
            {
                // 2a. Primary in this well
                target = wellItems.FirstOrDefault(i => i.IsPrimary);
                // 2b. Timelog in this well that has plans
                target ??= wellItems.OrderByDescending(i => i.PlanCount).FirstOrDefault(i => i.PlanCount > 0);
                // 2c. First timelog in this well
                target ??= wellItems.FirstOrDefault();
            }
        }

        // 3. Overall primary log
        target ??= items.FirstOrDefault(i => i.IsPrimary);

        // 4. Timelog that already has plans saved
        target ??= items.OrderByDescending(i => i.PlanCount).FirstOrDefault(i => i.PlanCount > 0);

        // 5. First timelog item
        target ??= items.FirstOrDefault();

        // 6. Fallback to AllPlans
        target ??= allPlansOption;

        SelectedTimelogContext = target;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterPlanGroups();
    }

    partial void OnSelectedTabIndexChanged(int oldValue, int newValue)
    {
        SyncRowsToPlanTab(oldValue);
        UpdateTabTitle();
        LoadCurrentTabRows();
    }

    partial void OnActivePlanItemChanged(PlanTreeItemViewModel? oldValue, PlanTreeItemViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsActive = false;
            SyncRowsToCurrentPlan();
        }
        if (newValue != null)
        {
            newValue.IsActive = true;
            ReloadActivePlanData(newValue);
        }
    }

    private void UpdateTabTitle()
    {
        SelectedTabTitle = SelectedTabIndex switch
        {
            0 => "Pickup",
            1 => "SlackOff",
            2 => "Rotate",
            3 => "Off Bottom Torque",
            4 => "On Bottom Torque",
            5 => "Make-Up Torque",
            6 => "Torque Limit",
            7 => "Sinusoidal While Rotating",
            _ => "Pickup"
        };
    }

    public void ReloadPlans()
    {
        if (_dataService == null) return;

        try
        {
            _allPlans.Clear();
            var plans = AdnlHookloadPlan.getAllPlans(_dataService, WellID, WellboreID, LogID);
            _allPlans.AddRange(plans);

            BuildPlanGroups(_allPlans);

            // Select active plan: default to RotateOnBottom 0.1 OHFF or 0.2 OHFF or first available
            var targetItem = PlanGroups.SelectMany(g => g.Plans).FirstOrDefault(p => p.PlanName.Contains("0.1 OHFF"))
                          ?? PlanGroups.SelectMany(g => g.Plans).FirstOrDefault();

            if (targetItem != null)
            {
                SelectPlan(targetItem);
            }
            else
            {
                ActivePlanItem = null;
                CurrentPlan = null;
                CurrentTableRows.Clear();
                TotalRowsText = "Total Rows: 0";
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading plans: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    public void LoadSampleDemoPlans()
    {
        if (_dataService == null)
        {
            ShowStatus("Database connection is not open.", isError: true);
            return;
        }

        SeedDistinctDemoPlans();
        ReloadPlans();
        ShowStatus("Sample engineering plans loaded successfully.");
    }

    public void SeedDistinctDemoPlans()
    {
        if (_dataService == null) return;

        // Seed 9 scenario-specific, realistic industrial plans with DISTINCT data
        var planDefs = new List<(string group, string name, string type, Action<AdnlHookloadPlan> populate)>
        {
            ("8.5 In_ON_BTOM_TORQUE", "8.5 In_ON_BTOM_TORQUE", "TORP", plan =>
            {
                // Depths 1000 to 5000 ft (every 500 ft)
                for (double d = 1000; d <= 5000; d += 500)
                {
                    double offTorque = Math.Round(5.2 + (d * 0.0042), 1);
                    double onTorque = Math.Round(12.0 + (d * 0.0075), 1);
                    plan.torque.Add(plan.torque.Count + 1, new HookloadPlanData { Depth = d, Weight = offTorque });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTorque });
                    plan.mkTorque.Add(plan.mkTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = 28.5 });
                    plan.tqLimit.Add(plan.tqLimit.Count + 1, new HookloadPlanData { Depth = d, Weight = 45.0 });
                }
            }),

            ("4-12 x 5-12 Casing - Tension", "4-12 x 5-12 Casing - Tension", "HKLDP", plan =>
            {
                // Heavy string casing tension (Depths 0 to 4500 ft every 500 ft)
                for (double d = 0; d <= 4500; d += 500)
                {
                    double wt = Math.Round(58.0 + (d * 0.038), 1);
                    double maxTens = Math.Round(120.0 + (d * 0.065), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = wt, MaxTension = maxTens });
                }
            }),

            ("4-1/2\" x 5-1/2\" Casing - HL Slack", "4-1/2\" x 5-1/2\" Casing - HL Slack", "HKLDP", plan =>
            {
                // Casing slackoff (Depths 0 to 4500 ft every 500 ft)
                for (double d = 0; d <= 4500; d += 500)
                {
                    double wt = Math.Round(42.0 + (d * 0.024), 1);
                    double maxComp = Math.Round(20.0 + (d * 0.016), 1);
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = wt, MaxCompress = maxComp });
                }
            }),

            ("RotateOnBottom", "RotateOnBottom 0.1 OHFF", "ROT", plan =>
            {
                // Friction factor 0.1 (low friction): Depths 0 to 2000 ft (every 100 ft)
                for (double d = 0; d <= 2000; d += 100)
                {
                    double pkup = Math.Round(21.0 + (d * 0.0125), 1);
                    double slk = Math.Round(18.0 + (d * 0.0085), 1);
                    double rot = Math.Round(19.5 + (d * 0.0105), 1);
                    double onTor = Math.Round(4.2 + (d * 0.0035), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = pkup });
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = slk });
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTor });
                }
            }),

            ("RotateOnBottom", "RotateOnBottom 0.2 OHFF", "ROT", plan =>
            {
                // Friction factor 0.2 (exactly matches mockup screenshot! 23.0, 28.2, 30.1... up to 64.5)
                double[] exactMockupWeights = { 23.0, 28.2, 30.1, 31.9, 33.8, 35.6, 37.5, 39.4, 41.3, 43.2, 45.1, 47.0, 48.9, 50.8, 52.8, 54.7, 56.7, 58.6, 60.6, 62.5, 64.5 };
                for (int i = 0; i < exactMockupWeights.Length; i++)
                {
                    double d = i * 100;
                    double pkup = exactMockupWeights[i];
                    double slk = Math.Round(19.0 + (d * 0.0145), 1);
                    double rot = Math.Round(21.0 + (d * 0.0175), 1);
                    double onTor = Math.Round(6.5 + (d * 0.0055), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = pkup });
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = slk });
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTor });
                }
            }),

            ("RotateOnBottom", "RotateOnBottom 0.3 OHFF", "ROT", plan =>
            {
                // Friction factor 0.3 (moderate friction): Depths 0 to 2000 ft
                for (double d = 0; d <= 2000; d += 100)
                {
                    double pkup = Math.Round(25.5 + (d * 0.0315), 1);
                    double slk = Math.Round(20.0 + (d * 0.0210), 1);
                    double rot = Math.Round(22.8 + (d * 0.0260), 1);
                    double onTor = Math.Round(9.0 + (d * 0.0082), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = pkup });
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = slk });
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTor });
                }
            }),

            ("RotateOnBottom", "RotateOnBottom 0.4 OHFF", "ROT", plan =>
            {
                // Friction factor 0.4 (high friction): Depths 0 to 2000 ft
                for (double d = 0; d <= 2000; d += 100)
                {
                    double pkup = Math.Round(28.0 + (d * 0.0440), 1);
                    double slk = Math.Round(21.5 + (d * 0.0285), 1);
                    double rot = Math.Round(24.5 + (d * 0.0355), 1);
                    double onTor = Math.Round(12.5 + (d * 0.0110), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = pkup });
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = slk });
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTor });
                }
            }),

            ("RotateOnBottom", "RotateOnBottom 0.5 OHFF", "ROT", plan =>
            {
                // Friction factor 0.5 (severe friction): Depths 0 to 2000 ft
                for (double d = 0; d <= 2000; d += 100)
                {
                    double pkup = Math.Round(32.0 + (d * 0.0580), 1);
                    double slk = Math.Round(23.0 + (d * 0.0360), 1);
                    double rot = Math.Round(27.0 + (d * 0.0465), 1);
                    double onTor = Math.Round(16.0 + (d * 0.0145), 1);
                    plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData { Depth = d, Weight = pkup });
                    plan.slackoff.Add(plan.slackoff.Count + 1, new HookloadPlanData { Depth = d, Weight = slk });
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.onTorque.Add(plan.onTorque.Count + 1, new HookloadPlanData { Depth = d, Weight = onTor });
                }
            }),

            ("RotateOffBottom", "RotateOffBottom", "ROT", plan =>
            {
                // Off bottom rotating: Depths 0 to 2500 ft (every 250 ft)
                for (double d = 0; d <= 2500; d += 250)
                {
                    double rot = Math.Round(18.0 + (d * 0.0125), 1);
                    double offTor = Math.Round(3.8 + (d * 0.0032), 1);
                    plan.rotate.Add(plan.rotate.Count + 1, new HookloadPlanData { Depth = d, Weight = rot });
                    plan.torque.Add(plan.torque.Count + 1, new HookloadPlanData { Depth = d, Weight = offTor });
                }
            })
        };

        string targetWellId = !string.IsNullOrEmpty(WellID) ? WellID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellId ?? "WELL-01");
        string targetWellboreId = !string.IsNullOrEmpty(WellboreID) ? WellboreID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellboreId ?? "WB-01");
        string targetLogId = !string.IsNullOrEmpty(LogID) ? LogID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.LogId ?? "LOG-01");

        foreach (var def in planDefs)
        {
            var p = new AdnlHookloadPlan
            {
                WellID = targetWellId,
                WellboreID = targetWellboreId,
                LogID = targetLogId,
                PlanID = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                Name = def.name,
                PlanType = def.type,
                RunNo = "1",
                Type = 0
            };

            def.populate(p);

            string err = string.Empty;
            AdnlHookloadPlan.savePlanEx(_dataService, p, ref err, false, 0, 0);
        }
    }

    private void BuildPlanGroups(IEnumerable<AdnlHookloadPlan> plans)
    {
        PlanGroups.Clear();

        var grouped = plans.GroupBy(p => InferGroupName(p.Name));

        foreach (var group in grouped)
        {
            var grpVm = new PlanTreeGroupViewModel
            {
                GroupName = group.Key,
                IsExpanded = true
            };

            foreach (var plan in group)
            {
                grpVm.Plans.Add(new PlanTreeItemViewModel
                {
                    PlanId = plan.PlanID,
                    PlanName = plan.Name,
                    GroupName = group.Key,
                    PlanType = plan.PlanType,
                    RunNo = plan.RunNo,
                    RawPlan = plan
                });
            }

            PlanGroups.Add(grpVm);
        }
    }

    private static string InferGroupName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "General Plans";
        string n = name.Trim();
        if (n.StartsWith("RotateOnBottom", StringComparison.OrdinalIgnoreCase)) return "RotateOnBottom";
        if (n.StartsWith("RotateOffBottom", StringComparison.OrdinalIgnoreCase)) return "RotateOffBottom";
        if (n.Contains("Casing", StringComparison.OrdinalIgnoreCase)) return n;
        if (n.Contains("TORQUE", StringComparison.OrdinalIgnoreCase)) return n;
        return n;
    }

    private void FilterPlanGroups()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            BuildPlanGroups(_allPlans);
            return;
        }

        string q = SearchText.Trim().ToLowerInvariant();
        var filtered = _allPlans.Where(p =>
            p.Name.ToLowerInvariant().Contains(q) ||
            p.RunNo.ToLowerInvariant().Contains(q) ||
            p.PlanType.ToLowerInvariant().Contains(q) ||
            InferGroupName(p.Name).ToLowerInvariant().Contains(q)).ToList();

        BuildPlanGroups(filtered);
    }

    public void LoadCurrentTabRows()
    {
        CurrentTableRows.Clear();

        if (CurrentPlan == null)
        {
            TotalRowsText = "Total Rows: 0";
            CurrentDataSummaryText = "No Plan Selected";
            return;
        }

        var dict = GetActiveDictionary();
        if (dict != null && dict.Count > 0)
        {
            int idx = 1;
            foreach (var pt in dict.Values.OrderBy(x => x.Depth))
            {
                CurrentTableRows.Add(HookloadPlanDataRowViewModel.FromDataObject(idx++, pt));
            }
        }

        if (CurrentTableRows.Count > 0)
        {
            double minD = CurrentTableRows.Min(r => r.Depth);
            double maxD = CurrentTableRows.Max(r => r.Depth);
            double minW = CurrentTableRows.Min(r => r.Weight);
            double maxW = CurrentTableRows.Max(r => r.Weight);
            string runLabel = !string.IsNullOrWhiteSpace(CurrentPlan.RunNo) ? $"Run: {CurrentPlan.RunNo} • " : "";
            CurrentDataSummaryText = $"{runLabel}{CurrentTableRows.Count} pts (Depth {minD:0.#} - {maxD:0.#} ft | Wt {minW:0.#} - {maxW:0.#} klbf)";
            TotalRowsText = $"Total Rows: {CurrentTableRows.Count}  (Wt Range: {minW:0.#} - {maxW:0.#} klbf)";
        }
        else
        {
            string runLabel = !string.IsNullOrWhiteSpace(CurrentPlan.RunNo) ? $"Run: {CurrentPlan.RunNo} • " : "";
            CurrentDataSummaryText = $"{runLabel}0 pts";
            TotalRowsText = "Total Rows: 0";
        }
    }

    private Dictionary<int, HookloadPlanData>? GetDictionaryForTab(int tabIndex)
    {
        if (CurrentPlan == null) return null;
        return tabIndex switch
        {
            0 => CurrentPlan.pickup,
            1 => CurrentPlan.slackoff,
            2 => CurrentPlan.rotate,
            3 => CurrentPlan.torque,
            4 => CurrentPlan.onTorque,
            5 => CurrentPlan.mkTorque,
            6 => CurrentPlan.tqLimit,
            7 => CurrentPlan.sinRot,
            _ => CurrentPlan.pickup
        };
    }

    private Dictionary<int, HookloadPlanData>? GetActiveDictionary()
    {
        return GetDictionaryForTab(SelectedTabIndex);
    }

    private void SyncRowsToPlanTab(int tabIndex)
    {
        if (CurrentPlan == null) return;
        var dict = GetDictionaryForTab(tabIndex);
        if (dict == null) return;

        dict.Clear();
        int idx = 1;
        foreach (var row in CurrentTableRows.OrderBy(r => r.Depth))
        {
            dict.Add(idx++, row.ToDataObject());
        }
    }

    private void SyncRowsToCurrentPlan()
    {
        SyncRowsToPlanTab(SelectedTabIndex);
    }

    private void ReloadActivePlanData(PlanTreeItemViewModel item)
    {
        if (_dataService != null && !string.IsNullOrEmpty(item.PlanId))
        {
            var fresh = AdnlHookloadPlan.loadObject(
                _dataService,
                item.RawPlan?.WellID ?? WellID,
                item.RawPlan?.WellboreID ?? WellboreID,
                item.RawPlan?.LogID ?? LogID,
                item.PlanId);

            CurrentPlan = fresh ?? item.RawPlan;
            if (fresh != null)
            {
                item.RawPlan = fresh;
            }
        }
        else
        {
            CurrentPlan = item.RawPlan;
        }

        LoadCurrentTabRows();
    }

    #region Commands
    [RelayCommand]
    public void SelectPlan(PlanTreeItemViewModel? item)
    {
        if (item == null) return;

        foreach (var grp in PlanGroups)
        {
            foreach (var p in grp.Plans)
            {
                p.IsActive = (p.PlanId == item.PlanId);
            }
        }

        ActivePlanItem = item;
        ReloadActivePlanData(item);
        string runInfo = !string.IsNullOrWhiteSpace(item.RunNo) && !item.RunNo.Equals(item.PlanName, StringComparison.OrdinalIgnoreCase)
            ? $" (Run: {item.RunNo})"
            : "";
        ShowStatus($"Selected plan: {item.PlanName}{runInfo} — {CurrentDataSummaryText}");
    }

    [RelayCommand]
    public void SelectGroup(PlanTreeGroupViewModel? group)
    {
        if (group == null || group.Plans.Count == 0) return;
        var planToSelect = group.Plans.FirstOrDefault(p => p.IsActive) ?? group.Plans.FirstOrDefault();
        if (planToSelect != null)
        {
            SelectPlan(planToSelect);
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        bool targetState = !PlanGroups.SelectMany(g => g.Plans).All(p => p.IsSelected);
        foreach (var group in PlanGroups)
        {
            group.IsSelected = targetState;
            foreach (var plan in group.Plans)
            {
                plan.IsSelected = targetState;
            }
        }
    }

    [RelayCommand]
    private void DeleteSelectedPlans()
    {
        var selected = PlanGroups.SelectMany(g => g.Plans).Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowStatus("No plans selected for deletion. Please check one or more plans.", isError: true);
            return;
        }

        bool confirmed = ConfirmDeleteHandler != null
            ? ConfirmDeleteHandler(selected.Count)
            : MessageBox.Show(
                $"Are you sure you want to delete {selected.Count} selected plan(s)? This action cannot be undone.",
                "Confirm Delete Plans",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (!confirmed) return;

        int deleted = 0;
        if (_dataService != null)
        {
            foreach (var p in selected)
            {
                string wId = p.RawPlan?.WellID ?? WellID;
                string wbId = p.RawPlan?.WellboreID ?? WellboreID;
                string lId = p.RawPlan?.LogID ?? LogID;
                if (AdnlHookloadPlan.removePlan(_dataService, wId, wbId, lId, p.PlanId))
                {
                    _allPlans.RemoveAll(x => x.PlanID == p.PlanId);
                    deleted++;
                }
            }
        }

        BuildPlanGroups(_allPlans);
        var nextItem = PlanGroups.SelectMany(g => g.Plans).FirstOrDefault();
        if (nextItem != null)
        {
            SelectPlan(nextItem);
        }
        else
        {
            ActivePlanItem = null;
            CurrentPlan = null;
            CurrentTableRows.Clear();
            TotalRowsText = "Total Rows: 0";
        }

        if (SelectedTimelogContext != null)
        {
            SelectedTimelogContext.PlanCount = Math.Max(0, SelectedTimelogContext.PlanCount - deleted);
        }

        ShowStatus($"Successfully deleted {deleted} plan(s) from database.");
    }

    [RelayCommand]
    private void AddRow()
    {
        if (CurrentPlan == null)
        {
            ShowStatus("Please select or create a plan first.", isError: true);
            return;
        }

        double lastDepth = CurrentTableRows.Count > 0 ? CurrentTableRows.Max(r => r.Depth) + 100 : 0;
        double lastWeight = CurrentTableRows.Count > 0 ? CurrentTableRows.Last().Weight : 25.0;

        var newRow = new HookloadPlanDataRowViewModel
        {
            SrNo = CurrentTableRows.Count + 1,
            Depth = lastDepth,
            Weight = lastWeight,
            MaxTension = 0,
            MinTension = 0,
            MaxCompress = 0,
            MinCompress = 0
        };

        CurrentTableRows.Add(newRow);
        TotalRowsText = $"Total Rows: {CurrentTableRows.Count}";
        SyncRowsToCurrentPlan();
        ShowStatus($"Added new point at depth {lastDepth:F1}. Click 'Save Plan' to persist changes.");
    }

    [RelayCommand]
    private void DeleteRow(HookloadPlanDataRowViewModel? row)
    {
        if (row == null) return;
        CurrentTableRows.Remove(row);

        // Re-index
        for (int i = 0; i < CurrentTableRows.Count; i++)
        {
            CurrentTableRows[i].SrNo = i + 1;
        }

        TotalRowsText = $"Total Rows: {CurrentTableRows.Count}";
        SyncRowsToCurrentPlan();
    }

    [RelayCommand]
    private void SavePlan()
    {
        if (_dataService == null)
        {
            ShowStatus("Database connection is not open.", isError: true);
            return;
        }

        if (CurrentPlan == null)
        {
            ShowStatus("No active plan selected to save. Plan deletions have already been committed to the database.");
            return;
        }

        SyncRowsToCurrentPlan();

        string lastError = string.Empty;
        bool ok = AdnlHookloadPlan.savePlanEx(_dataService, CurrentPlan, ref lastError, false, 0, 0);

        if (ok)
        {
            ShowStatus($"Plan '{CurrentPlan.Name}' saved successfully to SQLite database ({TotalRowsText}).");
        }
        else
        {
            ShowStatus($"Failed to save plan: {lastError}", isError: true);
        }
    }

    [RelayCommand]
    private void CreateNewPlan()
    {
        string newName = $"Plan_{DateTime.Now:yyyyMMdd_HHmm}";
        string targetWellId = !string.IsNullOrEmpty(WellID) ? WellID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellId ?? "WELL-01");
        string targetWellboreId = !string.IsNullOrEmpty(WellboreID) ? WellboreID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellboreId ?? "WB-01");
        string targetLogId = !string.IsNullOrEmpty(LogID) ? LogID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.LogId ?? "LOG-01");

        var newPlan = new AdnlHookloadPlan
        {
            WellID = targetWellId,
            WellboreID = targetWellboreId,
            LogID = targetLogId,
            PlanID = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Name = newName,
            PlanType = "HKLDP",
            RunNo = "1",
            Type = 0
        };

        if (_dataService != null)
        {
            string lastError = string.Empty;
            AdnlHookloadPlan.savePlanEx(_dataService, newPlan, ref lastError, false, 0, 0);
        }

        _allPlans.Add(newPlan);
        BuildPlanGroups(_allPlans);
        var target = PlanGroups.SelectMany(g => g.Plans).FirstOrDefault(p => p.PlanId == newPlan.PlanID);
        if (target != null)
        {
            SelectPlan(target);
        }
        ShowStatus($"Created new plan '{newName}'.");
    }

    [RelayCommand]
    private void Refresh()
    {
        ReloadPlans();
        ShowStatus("Refreshed plans from database.");
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (CurrentTableRows.Count == 0)
        {
            ShowStatus("No data rows to export.", isError: true);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = $"{CurrentPlan?.Name ?? "Plan"}_{SelectedTabTitle}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                using var sw = new StreamWriter(sfd.FileName);
                sw.WriteLine("Depth,Weight,Max Tension,Min Tension,Max Compress,Min Compress");
                foreach (var row in CurrentTableRows)
                {
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}",
                        row.Depth, row.Weight, row.MaxTension, row.MinTension, row.MaxCompress, row.MinCompress));
                }
                ShowStatus($"Exported {CurrentTableRows.Count} rows to {Path.GetFileName(sfd.FileName)}.");
            }
            catch (Exception ex)
            {
                ShowStatus($"Export failed: {ex.Message}", isError: true);
            }
        }
    }

    /// <summary>
    /// Opens the unified Import Plan Dialog for CSV &amp; Excel files.
    /// </summary>
    [RelayCommand]
    public void OpenImportDialog()
    {
        if (_dataService == null)
        {
            ShowStatus("Database connection is not open.", isError: true);
            return;
        }

        string targetWellId = !string.IsNullOrEmpty(WellID) ? WellID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellId ?? "WELL-01");
        string targetWellboreId = !string.IsNullOrEmpty(WellboreID) ? WellboreID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.WellboreId ?? "WB-01");
        string targetLogId = !string.IsNullOrEmpty(LogID) ? LogID : (AvailableTimelogContexts.FirstOrDefault(c => !c.IsAllPlansOption)?.LogId ?? "LOG-01");

        var importVm = new ImportPlanViewModel(
            _dataService,
            wellId: targetWellId,
            wellboreId: targetWellboreId,
            logId: targetLogId,
            defaultPlanName: CurrentPlan?.Name ?? "Imported Plan",
            activeTabName: SelectedTabTitle,
            wellName: CurrentWellName,
            wellboreName: CurrentWellboreName,
            logName: CurrentLogName,
            isPrimaryLog: IsPrimaryTimelog);

        var dialog = new DrillIntel.Views.Broomstick.ImportPlanDialog
        {
            DataContext = importVm,
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && (importVm.ParsedPlans.Count > 0 || importVm.ImportedPlan != null))
        {
            InitializeTimelogContexts();
            ReloadPlans();

            var plansToAdd = importVm.ParsedPlans.Count > 0
                ? importVm.ParsedPlans
                : new List<AdnlHookloadPlan> { importVm.ImportedPlan! };

            // Ensure all imported runs are in _allPlans in case SQLite query in ReloadPlans didn't find them (e.g. test environments)
            bool needsRebuild = false;
            foreach (var p in plansToAdd)
            {
                if (!_allPlans.Any(x => x.PlanID == p.PlanID))
                {
                    _allPlans.Add(p);
                    needsRebuild = true;
                }
            }

            if (needsRebuild)
            {
                BuildPlanGroups(_allPlans);
            }

            // Select the first run of the imported plan
            var firstRun = plansToAdd.FirstOrDefault();
            var item = PlanGroups.SelectMany(g => g.Plans)
                .FirstOrDefault(p => p.PlanId == firstRun?.PlanID)
                ?? PlanGroups.SelectMany(g => g.Plans)
                .FirstOrDefault(p => p.PlanName.Equals(importVm.PlanName, StringComparison.OrdinalIgnoreCase));

            if (item != null)
            {
                SelectPlan(item);
            }

            string runNames = string.Join(", ", plansToAdd.Select(p => !string.IsNullOrWhiteSpace(p.RunNo) ? p.RunNo : p.Name));
            ShowStatus($"Plan '{importVm.PlanName}' ({plansToAdd.Count} run(s): {runNames}) successfully imported and saved via SavePlanEx.");
        }
    }
    #endregion

    private void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
    }
}
#endregion

