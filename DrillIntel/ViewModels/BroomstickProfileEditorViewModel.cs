using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;

namespace DrillIntel.ViewModels;

public partial class BroomstickRigStateItemModel : ObservableObject
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Color { get; set; }
    public string ColorHex { get; set; } = "#808080";

    [ObservableProperty]
    private bool _isSelected;
}

public partial class BroomstickProfileEditorViewModel : ObservableObject
{
    private readonly IDataServiceDIntel? _dataService;
    private readonly IDataServiceDIntel? _appDataService;
    private readonly string _userName;
    private BroomstickProfile _currentProfile = new();

    public event Action<bool>? RequestClose;

    // Header & Context
    [ObservableProperty]
    private string _title = "Broomstick Profile Configuration";

    [ObservableProperty]
    private string _subtitle = "Configure Broomstick calculation thresholds, filtering, and rig state rules.";

    [ObservableProperty]
    private string _contextName = "Application Master Template";

    [ObservableProperty]
    private bool _isProjectContext;

    [ObservableProperty]
    private bool _canCopyFromAppMaster;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    // General / Identity properties
    [ObservableProperty]
    private string _profileId = string.Empty;

    [ObservableProperty]
    private string _profileName = "Default Profile";

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private int _profileType = 0; // 0 = Broomstick Plot, 1 = Hookload/Torque Plot

    // Downsampling & Interval Filtering
    [ObservableProperty]
    private int _downSampleOn = 0; // 0 = By Depth, 1 = By Time

    [ObservableProperty]
    private int _dataPoints = 6;

    [ObservableProperty]
    private int _timePeriod = 0;

    [ObservableProperty]
    private double _depthInterval = 100.0;

    [ObservableProperty]
    private double _intervalWindow = 10.0;

    [ObservableProperty]
    private bool _groupFuncMin = true;

    [ObservableProperty]
    private bool _groupFuncMax = true;

    [ObservableProperty]
    private bool _groupFuncAvg = true;

    // Filter Rules & Range Limits
    [ObservableProperty]
    private bool _filterByRange;

    [ObservableProperty]
    private double _minHookload = 0.0;

    [ObservableProperty]
    private double _maxHookload = 0.0;

    [ObservableProperty]
    private bool _filterByInterval;

    [ObservableProperty]
    private int _pointsToPlot = 0; // 0 = Dynamic, 1 = Static, 2 = Both

    // Display & Rules
    [ObservableProperty]
    private bool _showDepthTrack;

    [ObservableProperty]
    private int _trackWidth = 100;

    [ObservableProperty]
    private bool _showMultiple;

    [ObservableProperty]
    private bool _enforceRule;

    [ObservableProperty]
    private bool _plotOnBottomTorque;

    // Pick-Up (PU)
    [ObservableProperty]
    private string _pkupPumpChannel = "SPPA";

    [ObservableProperty]
    private double _pkupPumpCutOff = 99.0;

    [ObservableProperty]
    private double _pkupRPMCutOff = 12.0;

    [ObservableProperty]
    private double _pkupMinMovement = 5.0;

    [ObservableProperty]
    private double _pkupMaxMovement = 70.0;

    [ObservableProperty]
    private int _pkupPlotPoints = 0; // 0 = All, 1 = Pump On, 2 = Pump Off

    [ObservableProperty]
    private int _pkupStaticMethod = 0; // 0 = Min, 1 = Max, 2 = Avg

    [ObservableProperty]
    private int _pkupDynamicMethod = 0; // 0 = Break Over, 1 = Avg, 2 = Avg after Break Over

    [ObservableProperty]
    private bool _pkupLocalMax;

    [ObservableProperty]
    private int _pkupMultiMethod = 0;

    // Slack-Off (SO)
    [ObservableProperty]
    private string _slkPumpChannel = "SPPA";

    [ObservableProperty]
    private double _slkPumpCutOff = 99.0;

    [ObservableProperty]
    private double _slkRPMCutOff = 12.0;

    [ObservableProperty]
    private double _slkMinMovement = 5.0;

    [ObservableProperty]
    private double _slkMaxMovement = 70.0;

    [ObservableProperty]
    private int _slkPlotPoints = 0;

    [ObservableProperty]
    private int _slkStaticMethod = 0;

    [ObservableProperty]
    private int _slkDynamicMethod = 0;

    [ObservableProperty]
    private bool _slkLocalMax;

    [ObservableProperty]
    private int _slkMultiMethod = 0;

    // Rotary (ROT)
    [ObservableProperty]
    private string _rotPumpChannel = "SPPA";

    [ObservableProperty]
    private double _rotPumpCutOff = 99.0;

    [ObservableProperty]
    private double _rotMinRPM = 12.0;

    [ObservableProperty]
    private double _rotMaxRPM = 30.0;

    [ObservableProperty]
    private int _rotPlotPoints = 0;

    [ObservableProperty]
    private double _rotChange = 1.0;

    [ObservableProperty]
    private double _rotPoints = 1.0;

    [ObservableProperty]
    private bool _rotCheckPUSO;

    [ObservableProperty]
    private int _timeThreshold = 1;

    [ObservableProperty]
    private bool _enforcePUSO;

    [ObservableProperty]
    private int _rotMultiMethod = 0;

    // Casing Operations
    [ObservableProperty]
    private double _casingPkupMinMovement = 0.0;

    [ObservableProperty]
    private double _casingPkupMaxMovement = 0.0;

    [ObservableProperty]
    private double _casingSlkMinMovement = 0.0;

    [ObservableProperty]
    private double _casingSlkMaxMovement = 0.0;

    // Rig State Lists
    [ObservableProperty]
    private ObservableCollection<BroomstickRigStateItemModel> _pkupRigStates = new();

    [ObservableProperty]
    private ObservableCollection<BroomstickRigStateItemModel> _slkRigStates = new();

    [ObservableProperty]
    private ObservableCollection<BroomstickRigStateItemModel> _rotRigStates = new();

    [ObservableProperty]
    private ObservableCollection<BroomstickRigStateItemModel> _casingPkupRigStates = new();

    [ObservableProperty]
    private ObservableCollection<BroomstickRigStateItemModel> _casingSlkRigStates = new();

    // Master items cache
    private readonly List<(int Number, string Name, int Color, string ColorHex)> _allRigStateItems = new();

    // Units
    public string DepthUnit => "ft";
    public string PressureUnit => "psi";
    public string HookloadUnit => "klb";
    public string RpmUnit => "rpm";
    public string FlowUnit => "gpm";

    public BroomstickProfileEditorViewModel(
        IDataServiceDIntel? dataService,
        string contextName = "Application Master Template",
        string? userName = null,
        IDataServiceDIntel? appDataService = null,
        bool isProjectContext = false)
    {
        _dataService = dataService;
        _appDataService = appDataService;
        _isProjectContext = isProjectContext;
        ContextName = contextName;
        _userName = !string.IsNullOrWhiteSpace(userName) ? userName : Environment.UserName;

        CanCopyFromAppMaster = _isProjectContext && _appDataService != null;
        Subtitle = _isProjectContext
            ? $"Project Configuration: {contextName} (Changes apply to this project)"
            : "Application Master Template (Default settings automatically copied to new projects)";

        LoadMasterRigStates();
        LoadProfile();
    }

    private void LoadMasterRigStates()
    {
        _allRigStateItems.Clear();

        rigState? setup = null;
        if (_dataService != null)
        {
            try
            {
                setup = rigState.LoadCommonRigStateSetup(_dataService);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load rig state setup from dataService: {ex.Message}");
            }
        }

        if ((setup == null || setup.rigStates == null || setup.rigStates.Count == 0) && _appDataService != null)
        {
            try
            {
                setup = rigState.LoadCommonRigStateSetup(_appDataService);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load rig state setup from appDataService: {ex.Message}");
            }
        }

        if (setup != null && setup.rigStates != null && setup.rigStates.Count > 0)
        {
            foreach (var item in setup.rigStates.Values.OrderBy(s => s.Number))
            {
                string hex = !string.IsNullOrWhiteSpace(item.ColorHex)
                    ? item.ColorHex
                    : RigStateService.ConvertColorToHex((int)item.Color);

                _allRigStateItems.Add((item.Number, item.Name ?? $"State {item.Number}", (int)item.Color, hex));
            }
        }
        else
        {
            // All standard default rig state items from RigStateService (all 28 states)
            var defaultItems = RigStateService.GetDefaultRigStateItems();
            foreach (var item in defaultItems.Values.OrderBy(s => s.Number))
            {
                string hex = !string.IsNullOrWhiteSpace(item.ColorHex)
                    ? item.ColorHex
                    : RigStateService.ConvertColorToHex((int)item.Color);

                _allRigStateItems.Add((item.Number, item.Name ?? $"State {item.Number}", (int)item.Color, hex));
            }
        }
    }

    private void LoadProfile()
    {
        if (_dataService == null)
        {
            ApplyModelToProperties(BroomstickProfile.CreateDefault());
            return;
        }

        try
        {
            BroomstickProfile.EnsureTableExists(_dataService);

            // If in project context and project has no profile yet, copy from master app template if available
            if (IsProjectContext && _appDataService != null)
            {
                try
                {
                    var dt = _dataService.GetTable("SELECT COUNT(*) FROM APP_BS_GLOBAL_PROFILE;");
                    if (dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 0)
                    {
                        BroomstickProfile.CopyMasterProfilesToProject(_appDataService, _dataService, out _);
                    }
                }
                catch
                {
                    // Ignore non-fatal count check
                }
            }

            var p = BroomstickProfile.LoadSingleProfile(_dataService, out string err);
            _currentProfile = p ?? BroomstickProfile.CreateDefault();
            ApplyModelToProperties(_currentProfile);
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading profile: {ex.Message}", true);
            ApplyModelToProperties(BroomstickProfile.CreateDefault());
        }
    }

    private void ApplyModelToProperties(BroomstickProfile p)
    {
        ProfileId = p.ID;
        ProfileName = string.IsNullOrWhiteSpace(p.Name) ? "Default Profile" : p.Name;
        Notes = p.Notes ?? string.Empty;
        ProfileType = p.Type;

        // Downsampling & interval
        DownSampleOn = p.DownSampleOn;
        DataPoints = p.DataPoints > 0 ? p.DataPoints : 6;
        TimePeriod = p.TimePeriod;
        DepthInterval = p.DepthInterval > 0 ? p.DepthInterval : 100.0;
        IntervalWindow = p.IntervalWindow > 0 ? p.IntervalWindow : 10.0;

        // Parse GroupFunctions string: e.g. "MIN,MAX,AVG"
        var gf = (p.GroupFunctions ?? string.Empty).ToUpperInvariant();
        GroupFuncMin = string.IsNullOrEmpty(gf) || gf.Contains("MIN");
        GroupFuncMax = string.IsNullOrEmpty(gf) || gf.Contains("MAX");
        GroupFuncAvg = string.IsNullOrEmpty(gf) || gf.Contains("AVG");

        // Filtering
        FilterByRange = p.FilterByRange;
        MinHookload = p.MinHooklaod;
        MaxHookload = p.MaxHookload;
        FilterByInterval = p.FilterByInterval;
        PointsToPlot = p.PointsToPlot;

        // Display
        ShowDepthTrack = p.ShowDepthTrack;
        TrackWidth = p.TrackWidth > 0 ? p.TrackWidth : 100;
        ShowMultiple = p.ShowMultiple;
        EnforceRule = p.EnforceRule;
        PlotOnBottomTorque = p.PlotOnBottomTorque;

        // Pick-up
        PkupPumpChannel = string.IsNullOrWhiteSpace(p.PkupPumpChannel) ? "SPPA" : p.PkupPumpChannel;
        PkupPumpCutOff = p.PkupPumpCutOff;
        PkupRPMCutOff = p.PkupRPMCutOff;
        PkupMinMovement = p.PkupMinMovement;
        PkupMaxMovement = p.PkupMaxMovment;
        PkupPlotPoints = p.PkupPlotPoints;
        PkupStaticMethod = p.PkupStaticMethod;
        PkupDynamicMethod = p.PkupDynamicMethod;
        PkupLocalMax = p.PkupLocalMax;
        PkupMultiMethod = p.PkupMultiMethod;

        // Slack-off
        SlkPumpChannel = string.IsNullOrWhiteSpace(p.SlkPumpChannel) ? "SPPA" : p.SlkPumpChannel;
        SlkPumpCutOff = p.SlkPumpCutOff;
        SlkRPMCutOff = p.SlkRPMCutOff;
        SlkMinMovement = p.SlkMinMovement;
        SlkMaxMovement = p.SlkMaxMovment;
        SlkPlotPoints = p.SlkPlotPoints;
        SlkStaticMethod = p.SlkStaticMethod;
        SlkDynamicMethod = p.SlkDynamicMethod;
        SlkLocalMax = p.SlkLocalMax;
        SlkMultiMethod = p.SlkMultiMethod;

        // Rotary
        RotPumpChannel = string.IsNullOrWhiteSpace(p.RotPumpChannel) ? "SPPA" : p.RotPumpChannel;
        RotPumpCutOff = p.RotPumpCutOff;
        RotMinRPM = p.RotMinRPM;
        RotMaxRPM = p.RotMaxRPM;
        RotPlotPoints = p.RotPlotPoints;
        RotChange = p.RotChange;
        RotPoints = p.RotPoints;
        RotCheckPUSO = p.RotCheckPUSO;
        TimeThreshold = p.TimeThreshold;
        EnforcePUSO = p.EnforcePUSO;
        RotMultiMethod = p.RotMultiMethod;

        // Casing
        CasingPkupMinMovement = p.CasingPkupMinMovement;
        CasingPkupMaxMovement = p.CasingPkupMaxMovement;
        CasingSlkMinMovement = p.CasingSlkMinMovement;
        CasingSlkMaxMovement = p.CasingSlkMaxMovement;

        // Build rig states list for each section
        PkupRigStates = BuildRigStateList(p.PkupRigStates);
        SlkRigStates = BuildRigStateList(p.SlkRigStates);
        RotRigStates = BuildRigStateList(p.RotRigStates);
        CasingPkupRigStates = BuildRigStateList(p.CasingPkupRigStates);
        CasingSlkRigStates = BuildRigStateList(p.CasingSlkRigStates);
    }

    private ObservableCollection<BroomstickRigStateItemModel> BuildRigStateList(string? commaSeparatedCodes)
    {
        var set = new HashSet<int>();
        if (!string.IsNullOrWhiteSpace(commaSeparatedCodes))
        {
            var parts = commaSeparatedCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int code))
                    set.Add(code);
            }
        }

        var col = new ObservableCollection<BroomstickRigStateItemModel>();
        foreach (var item in _allRigStateItems)
        {
            col.Add(new BroomstickRigStateItemModel
            {
                Number = item.Number,
                Name = item.Name,
                Color = item.Color,
                ColorHex = item.ColorHex,
                IsSelected = set.Contains(item.Number)
            });
        }
        return col;
    }

    private string SerializeRigStates(IEnumerable<BroomstickRigStateItemModel> items)
    {
        var selectedNumbers = items.Where(i => i.IsSelected).Select(i => i.Number).OrderBy(n => n);
        return string.Join(",", selectedNumbers);
    }

    private string BuildGroupFunctionsString()
    {
        var list = new List<string>();
        if (GroupFuncMin) list.Add("MIN");
        if (GroupFuncMax) list.Add("MAX");
        if (GroupFuncAvg) list.Add("AVG");
        return string.Join(",", list);
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all parameters to system defaults?",
            "Reset Defaults",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var def = BroomstickProfile.CreateDefault();
            def.ID = ProfileId; // Maintain identity
            ApplyModelToProperties(def);
            SetStatus("Parameters reset to factory defaults. Click 'Save Profile' to persist.", false);
        }
    }

    [RelayCommand]
    private void CopyFromAppMaster()
    {
        if (_appDataService == null)
        {
            SetStatus("Application Master database is not accessible.", true);
            return;
        }

        var result = MessageBox.Show(
            "This will overwrite the current project's Broomstick parameters with the Application Master Template values.\n\nDo you want to continue?",
            "Copy from Application Master",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var master = BroomstickProfile.LoadSingleProfile(_appDataService, out string err);
            if (master != null)
            {
                master.ID = ProfileId; // Preserve project row ID
                ApplyModelToProperties(master);
                SetStatus("Loaded settings from Application Master. Click 'Save Profile' to commit changes.", false);
            }
            else
            {
                SetStatus($"Could not load Master profile: {err}", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error reading master template: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private void SelectAllRigStates(string? section)
    {
        SetRigStateSelection(section, true);
    }

    [RelayCommand]
    private void ClearAllRigStates(string? section)
    {
        SetRigStateSelection(section, false);
    }

    private void SetRigStateSelection(string? section, bool selected)
    {
        var targetList = section?.ToUpperInvariant() switch
        {
            "PU" => PkupRigStates,
            "SO" => SlkRigStates,
            "ROT" => RotRigStates,
            "CASINGPU" => CasingPkupRigStates,
            "CASINGSO" => CasingSlkRigStates,
            _ => null
        };

        if (targetList != null)
        {
            foreach (var item in targetList)
            {
                item.IsSelected = selected;
            }
        }
    }

    private void WritePropertiesToModel(BroomstickProfile p)
    {
        if (string.IsNullOrWhiteSpace(p.ID))
        {
            p.ID = string.IsNullOrWhiteSpace(ProfileId) ? Guid.NewGuid().ToString() : ProfileId;
        }
        p.Name = !string.IsNullOrWhiteSpace(ProfileName) ? ProfileName.Trim() : "Default Profile";
        p.Notes = Notes ?? string.Empty;
        p.Type = ProfileType;
        p.IsDefault = true;

        p.DownSampleOn = DownSampleOn;
        p.DataPoints = DataPoints;
        p.TimePeriod = TimePeriod;
        p.DepthInterval = DepthInterval;
        p.IntervalWindow = IntervalWindow;
        p.GroupFunctions = BuildGroupFunctionsString();

        p.FilterByRange = FilterByRange;
        p.MinHooklaod = MinHookload;
        p.MaxHookload = MaxHookload;
        p.FilterByInterval = FilterByInterval;
        p.PointsToPlot = PointsToPlot;

        p.ShowDepthTrack = ShowDepthTrack;
        p.TrackWidth = TrackWidth;
        p.ShowMultiple = ShowMultiple;
        p.EnforceRule = EnforceRule;
        p.PlotOnBottomTorque = PlotOnBottomTorque;

        // Pick-up
        p.PkupPumpChannel = PkupPumpChannel;
        p.PkupPumpCutOff = PkupPumpCutOff;
        p.PkupRPMCutOff = PkupRPMCutOff;
        p.PkupMinMovement = PkupMinMovement;
        p.PkupMaxMovment = PkupMaxMovement;
        p.PkupPlotPoints = PkupPlotPoints;
        p.PkupStaticMethod = PkupStaticMethod;
        p.PkupDynamicMethod = PkupDynamicMethod;
        p.PkupLocalMax = PkupLocalMax;
        p.PkupMultiMethod = PkupMultiMethod;
        p.PkupRigStates = SerializeRigStates(PkupRigStates);

        // Slack-off
        p.SlkPumpChannel = SlkPumpChannel;
        p.SlkPumpCutOff = SlkPumpCutOff;
        p.SlkRPMCutOff = SlkRPMCutOff;
        p.SlkMinMovement = SlkMinMovement;
        p.SlkMaxMovment = SlkMaxMovement;
        p.SlkPlotPoints = SlkPlotPoints;
        p.SlkStaticMethod = SlkStaticMethod;
        p.SlkDynamicMethod = SlkDynamicMethod;
        p.SlkLocalMax = SlkLocalMax;
        p.SlkMultiMethod = SlkMultiMethod;
        p.SlkRigStates = SerializeRigStates(SlkRigStates);

        // Rotary
        p.RotPumpChannel = RotPumpChannel;
        p.RotPumpCutOff = RotPumpCutOff;
        p.RotMinRPM = RotMinRPM;
        p.RotMaxRPM = RotMaxRPM;
        p.RotPlotPoints = RotPlotPoints;
        p.RotChange = RotChange;
        p.RotPoints = RotPoints;
        p.RotCheckPUSO = RotCheckPUSO;
        p.TimeThreshold = TimeThreshold;
        p.EnforcePUSO = EnforcePUSO;
        p.RotMultiMethod = RotMultiMethod;
        p.RotRigStates = SerializeRigStates(RotRigStates);

        // Casing
        p.CasingPkupMinMovement = CasingPkupMinMovement;
        p.CasingPkupMaxMovement = CasingPkupMaxMovement;
        p.CasingSlkMinMovement = CasingSlkMinMovement;
        p.CasingSlkMaxMovement = CasingSlkMaxMovement;
        p.CasingPkupRigStates = SerializeRigStates(CasingPkupRigStates);
        p.CasingSlkRigStates = SerializeRigStates(CasingSlkRigStates);

        p.ModifiedBy = _userName;
        p.ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            SetStatus("Profile Name cannot be empty.", true);
            return;
        }

        if (_dataService == null)
        {
            SetStatus("Database service unavailable.", true);
            return;
        }

        try
        {
            BroomstickProfile.EnsureTableExists(_dataService);
            WritePropertiesToModel(_currentProfile);

            bool ok = BroomstickProfile.SaveProfile(_dataService, _currentProfile, _userName, out string err);
            if (ok)
            {
                SetStatus($"Broomstick profile saved successfully at {DateTime.Now:HH:mm:ss}.", false);
            }
            else
            {
                SetStatus($"Failed to save profile: {err}", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error saving profile: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke(true);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = !string.IsNullOrEmpty(message);
    }
}

