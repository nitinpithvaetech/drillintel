using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
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

public partial class ImportPlanViewModel : ObservableObject
{
    private readonly IDataServiceDIntel _dataService;
    private readonly PlanImportService _importService = new();

    public event Action<bool>? RequestClose;

    public string WellID { get; }
    public string WellboreID { get; }
    public string LogID { get; }

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _planName = "New Imported Plan";

    [ObservableProperty]
    private string _planType = "HKLDP";

    [ObservableProperty]
    private string _runNo = "1";

    [ObservableProperty]
    private string _selectedTargetCurve = "All";

    public ObservableCollection<string> TargetCurveOptions { get; } = new()
    {
        "All",
        "Pickup",
        "SlackOff",
        "Rotate",
        "Off Bottom Torque",
        "On Bottom Torque",
        "Make-Up Torque",
        "Torque Limit",
        "Sinusoidal While Rotating"
    };

    // Worksheet selection for Excel workbooks
    public ObservableCollection<string> AvailableSheets { get; } = new();

    [ObservableProperty]
    private string _selectedSheet = string.Empty;

    [ObservableProperty]
    private bool _hasMultipleSheets;

    // Range import options (directly supported by savePlanEx)
    [ObservableProperty]
    private bool _importByRange;

    [ObservableProperty]
    private double _fromDepth = 0;

    [ObservableProperty]
    private double _toDepth = 10000;

    // Preview
    [ObservableProperty]
    private string _detectedCurvesSummary = "No file parsed yet";

    [ObservableProperty]
    private int _totalPointsParsed;

    [ObservableProperty]
    private bool _isFileParsed;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    public DataTable PreviewTable { get; private set; } = new();

    public AdnlHookloadPlan? ImportedPlan { get; private set; }
    public List<AdnlHookloadPlan> ParsedPlans { get; } = new();

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _wellboreName = string.Empty;

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private bool _isPrimaryLog;

    public ImportPlanViewModel(
        IDataServiceDIntel dataService,
        string wellId,
        string wellboreId,
        string logId,
        string defaultPlanName = "Imported Plan",
        string activeTabName = "Pickup",
        string wellName = "",
        string wellboreName = "",
        string logName = "",
        bool isPrimaryLog = false)
    {
        _dataService = dataService;
        WellID = wellId ?? "";
        WellboreID = wellboreId ?? "";
        LogID = logId ?? "";
        WellName = !string.IsNullOrWhiteSpace(wellName) ? wellName : (!string.IsNullOrWhiteSpace(WellID) ? WellID : "Current Well");
        WellboreName = !string.IsNullOrWhiteSpace(wellboreName) ? wellboreName : (!string.IsNullOrWhiteSpace(WellboreID) ? WellboreID : "Current Wellbore");
        LogName = !string.IsNullOrWhiteSpace(logName) ? logName : (!string.IsNullOrWhiteSpace(LogID) ? LogID : "Current Timelog");
        IsPrimaryLog = isPrimaryLog;
        PlanName = string.IsNullOrWhiteSpace(defaultPlanName) ? "Imported Plan" : defaultPlanName;

        if (TargetCurveOptions.Contains(activeTabName))
        {
            SelectedTargetCurve = activeTabName;
        }
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select Hookload / Torque Plan File (CSV or Excel)",
            Filter = "All Supported Files (*.xlsx;*.xls;*.csv;*.txt)|*.xlsx;*.xls;*.csv;*.txt|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|CSV Files (*.csv;*.txt)|*.csv;*.txt|All Files (*.*)|*.*",
            Multiselect = false
        };

        if (ofd.ShowDialog() == true)
        {
            LoadSheetsAndParse(ofd.FileName);
        }
    }

    public const string ALL_EXCEPT_HOW_TO = "All except How to";

    public void LoadSheetsAndParse(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            FilePath = filePath;
            PlanName = Path.GetFileNameWithoutExtension(filePath);
        }

        ParseFile();
    }

    private static bool IsDocumentationSheetName(string name)
    {
        return PlanImportService.IsDocumentationSheet(name);
    }

    partial void OnPlanTypeChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath))
        {
            ParseFile();
        }
    }

    public void ParseFile()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            ShowStatus("Please select a valid CSV or Excel file.", isError: true);
            return;
        }

        var result = _importService.ParsePlanFile(
            filePath: FilePath,
            planName: PlanName,
            wellId: WellID,
            wellboreId: WellboreID,
            logId: LogID,
            runNo: "",
            planType: PlanType,
            targetCurve: "All",
            sheetName: "");

        if (!result.Success)
        {
            IsFileParsed = false;
            ImportedPlan = null;
            ParsedPlans.Clear();
            TotalPointsParsed = 0;
            DetectedCurvesSummary = "Parsing failed";
            ShowStatus(result.Message, isError: true);
            return;
        }

        ImportedPlan = result.Plan;
        ParsedPlans.Clear();
        ParsedPlans.AddRange(result.ParsedPlans);
        TotalPointsParsed = result.TotalPoints;
        IsFileParsed = true;

        if (result.ParsedPlans.Count > 1)
        {
            DetectedCurvesSummary = $"{result.ParsedPlans.Count} Runs ({string.Join(", ", result.ParsedPlans.Select(p => p.RunNo))}) • {TotalPointsParsed} total points";
        }
        else
        {
            DetectedCurvesSummary = result.DetectedCurves.Count > 0
                ? $"{string.Join(" • ", result.DetectedCurves)} • {TotalPointsParsed} points"
                : $"{TotalPointsParsed} points parsed";
        }

        // Populate preview table
        var newTable = new DataTable("PreviewTable");

        if (result.PreviewHeaders.Count > 0)
        {
            foreach (var h in result.PreviewHeaders)
            {
                newTable.Columns.Add(h);
            }

            foreach (var r in result.PreviewRows)
            {
                var row = newTable.NewRow();
                for (int i = 0; i < Math.Min(r.Count, newTable.Columns.Count); i++)
                {
                    row[i] = r[i];
                }
                newTable.Rows.Add(row);
            }
        }

        PreviewTable = newTable;
        OnPropertyChanged(nameof(PreviewTable));

        ShowStatus(result.Message, isError: false);
    }

    [RelayCommand]
    private void ExecuteImport()
    {
        if (ParsedPlans.Count == 0 || !IsFileParsed)
        {
            ParseFile();
            if (ParsedPlans.Count == 0)
            {
                ShowStatus("Cannot import: no valid plan points were parsed from the file.", isError: true);
                return;
            }
        }

        string trimmedPlanName = PlanName?.Trim() ?? "Imported Plan";
        string trimmedPlanType = PlanType?.Trim() ?? "HKLDP";

        foreach (var p in ParsedPlans)
        {
            p.Name = trimmedPlanName;
            p.PlanType = trimmedPlanType;
        }

        bool success = _importService.SavePlans(
            _dataService,
            ParsedPlans,
            importByRange: ImportByRange,
            fromDepth: FromDepth,
            toDepth: ToDepth,
            errorMessage: out string errorMsg);

        if (success)
        {
            ShowStatus($"Plan '{trimmedPlanName}' with {ParsedPlans.Count} run(s) successfully saved to database!");
            RequestClose?.Invoke(true);
        }
        else
        {
            ShowStatus($"Failed to save plan: {errorMsg}", isError: true);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
    }
}
