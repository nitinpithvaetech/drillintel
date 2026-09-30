using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class RigStateItemModel : ObservableObject
{
    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private int _color;

    [ObservableProperty]
    private string _colorHex = "#000000";

    partial void OnColorHexChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Color = RigStateService.ConvertHexToColor(value);
        }
    }

    public void SetColor(int argb)
    {
        Color = argb;
        ColorHex = RigStateService.ConvertColorToHex(argb);
    }

    [RelayCommand]
    public void SetColorHex(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            ColorHex = hex;
        }
    }
}

public partial class AutoSlideRowModel : ObservableObject
{
    [ObservableProperty]
    private double _fromDepth;

    [ObservableProperty]
    private double _toDepth;

    [ObservableProperty]
    private double _minTorque;

    [ObservableProperty]
    private double _maxTorque;

    [ObservableProperty]
    private int _calibrationRows;

    [ObservableProperty]
    private double _minTorqueDiff;

    [ObservableProperty]
    private double _minRPM;

    [ObservableProperty]
    private double _maxRPM;
}

public partial class RigStateViewModel : ObservableObject
{
    private readonly IDataServiceDIntel? _dataService;
    private rigState _model = new();

    public event Action<bool>? RequestClose;

    // Header & Feedback
    [ObservableProperty]
    private string _title = "Rig State Master Configuration";

    [ObservableProperty]
    private string _subtitle = "Manage rig state classifications, identification cutoff thresholds, and auto-slide settings.";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    // Selected Tab
    [ObservableProperty]
    private int _selectedTabIndex;

    // Units
    [ObservableProperty]
    private string _hookloadUnit = "klb";

    [ObservableProperty]
    private string _circUnit = "gpm";

    [ObservableProperty]
    private string _movementUnit = "ft";

    [ObservableProperty]
    private string _pumpUnit = "psi";

    [ObservableProperty]
    private string _storUnit = "ft-lbf";

    [ObservableProperty]
    private string _airPressureUnit = "psi";

    [ObservableProperty]
    private string _mistFlowUnit = "gpm";

    [ObservableProperty]
    private string _depthUnit = "ft";

    #region Tab 1: Rig States & Unknown State
    [ObservableProperty]
    private string _unknownName = "Unknown";

    [ObservableProperty]
    private float _unknownNumber = 15;

    [ObservableProperty]
    private int _unknownColor = -2818048;

    [ObservableProperty]
    private string _unknownColorHex = "#D50000";

    partial void OnUnknownColorHexChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            UnknownColor = RigStateService.ConvertHexToColor(value);
        }
    }

    [ObservableProperty]
    private RigStateItemModel? _selectedRigStateItem;

    public ObservableCollection<RigStateItemModel> RigStateItems { get; } = new();
    #endregion

    #region Tab 2: Threshold Values
    [ObservableProperty]
    private double _hookloadCutOff = 90;

    [ObservableProperty]
    private double _rPMCutOff = 1;

    [ObservableProperty]
    private double _cIRCCutOff = 1;

    [ObservableProperty]
    private double _sensitivity = 1;

    [ObservableProperty]
    private double _pumpPressureCutOff = 0;

    [ObservableProperty]
    private double _depthComparisonSens = 0.3;

    // Air Drilling
    [ObservableProperty]
    private bool _detectAirDrilling;

    [ObservableProperty]
    private double _airPressure;

    [ObservableProperty]
    private double _torqueCutOff;

    [ObservableProperty]
    private double _mistFlowCutOff;

    // Pipe Movement
    [ObservableProperty]
    private bool _detectPipeMovement;

    [ObservableProperty]
    private double _pipeMovementThreshold;

    // Sets
    [ObservableProperty]
    private int _selectedSet = 0;

    partial void OnSelectedSetChanged(int value)
    {
        OnPropertyChanged(nameof(IsDefaultSetSelected));
        OnPropertyChanged(nameof(IsSet2Selected));
        OnPropertyChanged(nameof(IsSet3Selected));
    }

    public bool IsDefaultSetSelected
    {
        get => SelectedSet == 0 || SelectedSet == 1;
        set { if (value) SelectedSet = 1; OnPropertyChanged(); OnPropertyChanged(nameof(IsSet2Selected)); OnPropertyChanged(nameof(IsSet3Selected)); }
    }

    public bool IsSet2Selected
    {
        get => SelectedSet == 2;
        set { if (value) SelectedSet = 2; OnPropertyChanged(); OnPropertyChanged(nameof(IsDefaultSetSelected)); OnPropertyChanged(nameof(IsSet3Selected)); }
    }

    public bool IsSet3Selected
    {
        get => SelectedSet == 3;
        set { if (value) SelectedSet = 3; OnPropertyChanged(); OnPropertyChanged(nameof(IsDefaultSetSelected)); OnPropertyChanged(nameof(IsSet2Selected)); }
    }

    // Set 1 (Default)
    [ObservableProperty]
    private double _torqueMin = 1;

    [ObservableProperty]
    private double _torqueMax = 12000;

    [ObservableProperty]
    private int _calibrationRows = 20;

    [ObservableProperty]
    private double _minTorqueDifference = 500;

    [ObservableProperty]
    private double _minRPM = 0;

    [ObservableProperty]
    private double _maxRPM = 50;

    // Set 2
    [ObservableProperty]
    private double _torqueMin2 = 0;

    [ObservableProperty]
    private double _torqueMax2 = 15000;

    [ObservableProperty]
    private int _calibrationRows2 = 50;

    [ObservableProperty]
    private double _minTorqueDifference2 = 1;

    [ObservableProperty]
    private double _minRPM2 = 0;

    [ObservableProperty]
    private double _maxRPM2 = 44;

    // Set 3
    [ObservableProperty]
    private double _torqueMin3 = 0;

    [ObservableProperty]
    private double _torqueMax3 = 0;

    [ObservableProperty]
    private int _calibrationRows3 = 0;

    [ObservableProperty]
    private double _minTorqueDifference3 = 0;

    [ObservableProperty]
    private double _minRPM3 = 0;

    [ObservableProperty]
    private double _maxRPM3 = 0;

    // Detect Auto Slide Drilling
    [ObservableProperty]
    private bool _detectAutoSlideDrilling;

    // Use Well Section Rig State
    [ObservableProperty]
    private bool _useWellSectionRigState;

    [ObservableProperty]
    private bool _showWellSectionOption;
    #endregion

    #region Tab 3: Auto Slide Parameters
    [ObservableProperty]
    private double _autoSlideMaxRPM = 50;

    [ObservableProperty]
    private int _torqueCycles = 0;

    [ObservableProperty]
    private double _percentWindow = 0;

    [ObservableProperty]
    private int _calibrationTime = 0;
    #endregion

    #region Tab 4: Auto Slide Parameters (2) Grid
    [ObservableProperty]
    private AutoSlideRowModel? _selectedAutoSlideRow;

    public ObservableCollection<AutoSlideRowModel> AutoSlideRows { get; } = new();
    #endregion

    public RigStateViewModel() : this(null, null)
    {
    }

    public RigStateViewModel(IDataServiceDIntel? dataService, string? contextName = null)
    {
        _dataService = dataService ?? (App.Session.IsProjectOpen ? App.Session.GetDataService() : null);
        if (!string.IsNullOrWhiteSpace(contextName))
        {
            Subtitle = $"{contextName} • Configure rig state detection cutoffs, auto-slide calibration, and state color mappings.";
        }
        else if (App.Session.IsProjectOpen)
        {
            Subtitle = $"Project: {App.Session.ProjectName} • Configure rig state detection cutoffs, auto-slide calibration, and state color mappings.";
        }
        else
        {
            Subtitle = "Global Master Template • Configure standard rig state detection cutoffs, auto-slide calibration, and state color mappings.";
        }
        LoadData();
    }

    /// <summary>
    /// Loads common rig state setup from database using rigState.LoadCommonRigStateSetup.
    /// </summary>
    [RelayCommand]
    public void LoadData()
    {
        try
        {
            if (_dataService == null)
            {
                // In-memory defaults when no DB is connected
                _model = rigState.CreateDefault();
                PopulateViewModelFromModel(_model);
                ShowStatus("Loaded default template values (no project open).", false);
                return;
            }

            var loaded = rigState.LoadCommonRigStateSetup(_dataService);
            if (loaded != null)
            {
                _model = loaded;
                PopulateViewModelFromModel(_model);
                ShowStatus("Common Rig State setup loaded successfully.", false);
            }
            else
            {
                _model = rigState.CreateDefault();
                PopulateViewModelFromModel(_model);
                ShowStatus("Loaded default setup template.", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error loading rig state setup: {ex.Message}", true);
        }
    }

    private void PopulateViewModelFromModel(rigState model)
    {
        UnknownName = model.UnknownName ?? "Unknown";
        UnknownNumber = model.UnknownNumber;
        UnknownColor = (int)model.UnknownColor;
        UnknownColorHex = !string.IsNullOrWhiteSpace(model.UnknownColorHex)
            ? model.UnknownColorHex
            : RigStateService.ConvertColorToHex((int)model.UnknownColor);

        HookloadCutOff = model.HookloadCutOff;
        RPMCutOff = model.RPMCutOff;
        CIRCCutOff = model.CIRCCutOff;
        Sensitivity = model.Sensitivity;
        PumpPressureCutOff = model.PumpPressureCutOff;
        DepthComparisonSens = model.DepthComparisonSens;

        DetectAirDrilling = model.DetectAirDrilling;
        AirPressure = model.AirPressure;
        TorqueCutOff = model.TorqueCutOff;
        MistFlowCutOff = model.MistFlowCutOff;

        DetectPipeMovement = model.DetectPipeMovement;
        PipeMovementThreshold = model.PipeMovementThreshold;

        SelectedSet = model.SelectedSet;
        OnPropertyChanged(nameof(IsDefaultSetSelected));
        OnPropertyChanged(nameof(IsSet2Selected));
        OnPropertyChanged(nameof(IsSet3Selected));

        // Set 1
        TorqueMin = model.TorqueMin;
        TorqueMax = model.TorqueMax;
        CalibrationRows = model.CalibrationRows;
        MinTorqueDifference = model.MinTorqueDifference;
        MinRPM = model.MinRPM;
        MaxRPM = model.MaxRPM;

        // Set 2
        TorqueMin2 = model.TorqueMin2;
        TorqueMax2 = model.TorqueMax2;
        CalibrationRows2 = model.CalibrationRows2;
        MinTorqueDifference2 = model.MinTorqueDifference2;
        MinRPM2 = model.MinRPM2;
        MaxRPM2 = model.MaxRPM2;

        // Set 3
        TorqueMin3 = model.TorqueMin3;
        TorqueMax3 = model.TorqueMax3;
        CalibrationRows3 = model.CalibrationRows3;
        MinTorqueDifference3 = model.MinTorqueDifference3;
        MinRPM3 = model.MinRPM3;
        MaxRPM3 = model.MaxRPM3;

        DetectAutoSlideDrilling = model.DetectAutoSlideDrilling;
        UseWellSectionRigState = model.UseWellSectionRigState;

        AutoSlideMaxRPM = model.MaxRPM;
        TorqueCycles = model.TorqueCycles;
        PercentWindow = model.PercentWindow;
        CalibrationTime = model.CalibrationTime;

        // Populate Rig States collection
        RigStateItems.Clear();
        if (model.rigStates == null || model.rigStates.Count == 0)
        {
            model.rigStates = RigStateService.GetDefaultRigStateItems();
        }
        var sorted = model.rigStates.Values.OrderBy(s => s.Number);
            foreach (var s in sorted)
            {
                RigStateItems.Add(new RigStateItemModel
                {
                    Number = s.Number,
                    Name = s.Name ?? string.Empty,
                    Color = (int)s.Color,
                    ColorHex = !string.IsNullOrWhiteSpace(s.ColorHex) ? s.ColorHex : RigStateService.ConvertColorToHex((int)s.Color)
                });
            }

        // Populate AutoSlide rows
        AutoSlideRows.Clear();
        if (model.autoSlideSetupList != null)
        {
            foreach (var kvp in model.autoSlideSetupList)
            {
                var row = kvp.Value;
                if (row == null) continue;
                AutoSlideRows.Add(new AutoSlideRowModel
                {
                    FromDepth = row.FromDepth,
                    ToDepth = row.ToDepth,
                    MinTorque = row.MinTorque,
                    MaxTorque = row.MaxTorque,
                    CalibrationRows = row.CalibrationRows,
                    MinTorqueDiff = row.MinTorqueDiff,
                    MinRPM = row.MinRPM,
                    MaxRPM = row.MaxRPM
                });
            }
        }
    }

    private void PopulateModelFromViewModel(rigState model)
    {
        model.UnknownName = UnknownName;
        model.UnknownNumber = UnknownNumber;
        model.UnknownColor = UnknownColor;
        model.UnknownColorHex = UnknownColorHex;

        model.HookloadCutOff = HookloadCutOff;
        model.RPMCutOff = RPMCutOff;
        model.CIRCCutOff = CIRCCutOff;
        model.Sensitivity = Sensitivity;
        model.PumpPressureCutOff = PumpPressureCutOff;
        model.DepthComparisonSens = DepthComparisonSens;

        model.DetectAirDrilling = DetectAirDrilling;
        model.AirPressure = AirPressure;
        model.TorqueCutOff = TorqueCutOff;
        model.MistFlowCutOff = MistFlowCutOff;

        model.DetectPipeMovement = DetectPipeMovement;
        model.PipeMovementThreshold = PipeMovementThreshold;

        model.SelectedSet = SelectedSet;

        model.TorqueMin = TorqueMin;
        model.TorqueMax = TorqueMax;
        model.CalibrationRows = CalibrationRows;
        model.MinTorqueDifference = MinTorqueDifference;
        model.MinRPM = MinRPM;
        model.MaxRPM = MaxRPM;

        model.TorqueMin2 = TorqueMin2;
        model.TorqueMax2 = TorqueMax2;
        model.CalibrationRows2 = CalibrationRows2;
        model.MinTorqueDifference2 = MinTorqueDifference2;
        model.MinRPM2 = MinRPM2;
        model.MaxRPM2 = MaxRPM2;

        model.TorqueMin3 = TorqueMin3;
        model.TorqueMax3 = TorqueMax3;
        model.CalibrationRows3 = CalibrationRows3;
        model.MinTorqueDifference3 = MinTorqueDifference3;
        model.MinRPM3 = MinRPM3;
        model.MaxRPM3 = MaxRPM3;

        model.DetectAutoSlideDrilling = DetectAutoSlideDrilling;
        model.UseWellSectionRigState = UseWellSectionRigState;

        model.TorqueCycles = TorqueCycles;
        model.PercentWindow = PercentWindow;
        model.CalibrationTime = CalibrationTime;

        // Rig states items
        model.rigStates.Clear();
        foreach (var item in RigStateItems)
        {
            model.rigStates[item.Number] = new rigStateItem
            {
                Number = item.Number,
                Name = item.Name,
                Color = item.Color,
                ColorHex = item.ColorHex
            };
        }

        // Auto slide settings
        model.autoSlideSetupList.Clear();
        int counter = 1;
        foreach (var row in AutoSlideRows)
        {
            model.autoSlideSetupList[counter++] = new AutoSlideSettings
            {
                FromDepth = row.FromDepth,
                ToDepth = row.ToDepth,
                MinTorque = row.MinTorque,
                MaxTorque = row.MaxTorque,
                CalibrationRows = row.CalibrationRows,
                MinTorqueDiff = row.MinTorqueDiff,
                MinRPM = row.MinRPM,
                MaxRPM = row.MaxRPM
            };
        }
    }

    /// <summary>
    /// Saves the current rig state setup using rigState.SaveCommonRigStateSetup.
    /// </summary>
    [RelayCommand]
    public void Save()
    {
        try
        {
            if (_dataService == null)
            {
                ShowStatus("Cannot save: No database connection is available.", true);
                return;
            }

            PopulateModelFromViewModel(_model);

            bool saved = rigState.SaveCommonRigStateSetup(_dataService, _model);
            if (saved)
            {
                ShowStatus("Rig State setup saved successfully!", false);
                RequestClose?.Invoke(true);
            }
            else
            {
                string err = !string.IsNullOrEmpty(rigState.LastError) ? rigState.LastError : "Failed to save rig state setup.";
                ShowStatus($"Save error: {err}", true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Save failed: {ex.Message}", true);
        }
    }

    [RelayCommand]
    public void Reset()
    {
        LoadData();
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    public void AddAutoSlideRow()
    {
        double lastTo = AutoSlideRows.LastOrDefault()?.ToDepth ?? 0;
        AutoSlideRows.Add(new AutoSlideRowModel
        {
            FromDepth = lastTo,
            ToDepth = lastTo + 1000,
            MinTorque = TorqueMin,
            MaxTorque = TorqueMax,
            CalibrationRows = CalibrationRows,
            MinTorqueDiff = MinTorqueDifference,
            MinRPM = MinRPM,
            MaxRPM = MaxRPM
        });
    }

    [RelayCommand]
    public void RemoveAutoSlideRow()
    {
        if (SelectedAutoSlideRow != null)
        {
            AutoSlideRows.Remove(SelectedAutoSlideRow);
        }
        else if (AutoSlideRows.Count > 0)
        {
            AutoSlideRows.RemoveAt(AutoSlideRows.Count - 1);
        }
    }

    [RelayCommand]
    public void SetUnknownColorHex(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            UnknownColorHex = hex;
        }
    }

    [RelayCommand]
    public void SetItemColor((RigStateItemModel item, string hex) param)
    {
        if (param.item != null && !string.IsNullOrWhiteSpace(param.hex))
        {
            param.item.ColorHex = param.hex;
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
    }
}
