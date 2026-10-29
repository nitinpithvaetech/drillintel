using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models.Broomstick;

namespace DrillIntel.ViewModels.Broomstick;

public partial class BroomstickSetupRigStateItem : ObservableObject
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#10B981";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;
}

/// <summary>
/// ViewModel for the Broomstick Setup & Filter Dialog (matching eVumaxPro screenshot).
/// Controls Downsampling, Hookload Range Cutoff, Outlier Filters, Point Formatting,
/// Casing Formatting, and Hookload Rig States.
/// </summary>
public partial class BroomstickSetupViewModel : ObservableObject
{
    private readonly BroomstickDocument _document;
    private readonly DrillIntel.Data.IDataServiceDIntel? _projectDataService;
    private readonly DrillIntel.Data.IDataServiceDIntel? _appDataService;

    public Action? RequestClose { get; set; }
    public bool DialogResult { get; private set; }

    [ObservableProperty]
    private string _profileName = "Hookload Test";

    [ObservableProperty]
    private bool _syncWithProfile;

    [ObservableProperty]
    private bool _updateProfile = true;

    // --- Tab 1: Filter Criteria ---
    // Downsampling
    [ObservableProperty]
    private int _downSampleOn = 0; // 0 = By Depth, 1 = By Time

    [ObservableProperty]
    private int _dataPointsPer = 100;

    [ObservableProperty]
    private bool _groupMin = true;

    [ObservableProperty]
    private bool _groupMax = true;

    [ObservableProperty]
    private bool _groupAvg = true;

    // Filter By Hookload Range
    [ObservableProperty]
    private bool _filterByHookloadRange = true;

    [ObservableProperty]
    private double _minHookload = 1.0;

    [ObservableProperty]
    private double _maxHookload = 300.0;

    // Filter By Depth Intervals
    [ObservableProperty]
    private bool _filterByDepthIntervals;

    [ObservableProperty]
    private double _depthInterval = 500.0;

    [ObservableProperty]
    private double _outlierPercentMean = 10.0;

    [ObservableProperty]
    private string _filterMethod = "Moving Avg.";

    // Points Style
    [ObservableProperty]
    private string _pointsStyle = "Rectangle";

    [ObservableProperty]
    private int _pointSize = 5;

    [ObservableProperty]
    private int _pointTransparency = 0;

    [ObservableProperty]
    private int _lineThickness = 3;

    // Plot Time Data
    [ObservableProperty]
    private bool _plotTimeData = true;

    [ObservableProperty]
    private bool _showLegend = true;

    [ObservableProperty]
    private bool _plotMakeupTorque = true;

    [ObservableProperty]
    private bool _plotTorqueLimit = true;

    // BroomStick Points Formatting (Colors)
    [ObservableProperty]
    private string _pickupColor = "#E91E63";

    [ObservableProperty]
    private string _pickupStaticColor = "#00E676";

    [ObservableProperty]
    private string _slackoffColor = "#2979FF";

    [ObservableProperty]
    private string _slackoffStaticColor = "#FFEA00";

    [ObservableProperty]
    private string _robColor = "#FF9800";

    [ObservableProperty]
    private string _onBottomTorqueColor = "#FF80AB";

    [ObservableProperty]
    private string _offBottomTorqueColor = "#80D8FF";

    // Casing Points Formatting
    [ObservableProperty]
    private bool _showCasingPoints = true;

    [ObservableProperty]
    private bool _showCasingMax = true;

    [ObservableProperty]
    private bool _showCasingMin;

    [ObservableProperty]
    private bool _showCasingAvg;

    [ObservableProperty]
    private string _casingPointStyle = "Rectangle";

    [ObservableProperty]
    private int _casingPointSize = 10;

    [ObservableProperty]
    private int _casingPointTransparency = 10;

    [ObservableProperty]
    private string _casingPickupColor = "#B3B8FF";

    [ObservableProperty]
    private string _casingSlackoffColor = "#4DD0E1";

    // Hookload Rig States
    public ObservableCollection<BroomstickSetupRigStateItem> RigStates { get; } = new();

    [ObservableProperty]
    private string _rigStateSearchText = string.Empty;

    partial void OnRigStateSearchTextChanged(string value)
    {
        string q = value?.Trim().ToLowerInvariant() ?? string.Empty;
        foreach (var rs in RigStates)
        {
            rs.IsVisible = string.IsNullOrEmpty(q) || rs.Name.ToLowerInvariant().Contains(q);
        }
    }

    public BroomstickSetupViewModel(
        BroomstickDocument document,
        DrillIntel.Data.IDataServiceDIntel? projectDataService = null,
        DrillIntel.Data.IDataServiceDIntel? appDataService = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _projectDataService = projectDataService;
        _appDataService = appDataService;
        LoadFromDocument();
        LoadRigStates();
    }

    private void LoadFromDocument()
    {
        var p = _document.Profile;
        ProfileName = !string.IsNullOrWhiteSpace(p.Name) ? p.Name : "Hookload Test";
        DownSampleOn = p.DownSampleOn;
        DataPointsPer = p.DataPoints > 0 ? p.DataPoints : 100;
        FilterByHookloadRange = p.FilterByRange;
        MinHookload = p.MinHooklaod > 0 ? p.MinHooklaod : 1.0;
        MaxHookload = p.MaxHookload > 0 ? p.MaxHookload : 300.0;
        FilterByDepthIntervals = p.FilterByInterval;
        DepthInterval = p.DepthInterval > 0 ? p.DepthInterval : 500.0;
    }

    private void LoadRigStates()
    {
        RigStates.Clear();
        var defaults = new List<(int num, string name, string hex, bool sel)>
        {
            (1, "Rotary Drill", "#10B981", true),
            (2, "Slide Drill", "#06B6D4", true),
            (3, "In Slips", "#3B82F6", true),
            (4, "Ream", "#F59E0B", true),
            (5, "Connection", "#A855F7", true),
            (6, "Trip In", "#EC4899", false),
            (7, "Trip Out", "#F43F5E", false),
            (8, "Circulate", "#6B7280", false),
            (9, "Wiper Trip", "#8B5CF6", false)
        };

        foreach (var def in defaults)
        {
            RigStates.Add(new BroomstickSetupRigStateItem
            {
                Number = def.num,
                Name = def.name,
                ColorHex = def.hex,
                IsSelected = def.sel
            });
        }
    }

    [RelayCommand]
    public void SaveAndApply()
    {
        var p = _document.Profile;
        p.DownSampleOn = DownSampleOn;
        p.DataPoints = DataPointsPer;
        p.GroupFunctions = "MIN,MAX,AVG";
        p.FilterByRange = FilterByHookloadRange;
        p.MinHooklaod = MinHookload;
        p.MaxHookload = MaxHookload;
        p.FilterByInterval = FilterByDepthIntervals;
        p.DepthInterval = DepthInterval;
        p.IntervalWindow = OutlierPercentMean;

        // Persist to project and/or global database if requested
        if (UpdateProfile && _projectDataService != null)
        {
            try
            {
                BroomstickProfile.SaveProfile(_projectDataService, p, Environment.UserName, out _, isProjectDatabase: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BroomstickSetupViewModel] SaveProfile project error: {ex.Message}");
            }
        }

        if (SyncWithProfile && _appDataService != null)
        {
            try
            {
                BroomstickProfile.SaveProfile(_appDataService, p, Environment.UserName, out _, isProjectDatabase: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BroomstickSetupViewModel] SaveProfile app error: {ex.Message}");
            }
        }

        DialogResult = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}

