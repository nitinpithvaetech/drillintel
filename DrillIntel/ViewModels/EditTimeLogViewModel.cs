using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class EditTimeLogViewModel : ObservableObject
{
    private readonly ProjectSession? _session;
    private readonly IWellDataRepository? _repository;
    private readonly TimeLog? _fallbackTimeLog;

    public event Action<bool?>? RequestClose;

    [ObservableProperty]
    private string _title = "Edit Timelog";

    [ObservableProperty]
    private string _subtitle = "Configure log metadata, channel definitions, and log linking parameters.";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private bool _isLoading;

    // --- Identification & Metadata ---
    [ObservableProperty]
    private string _logId = string.Empty;

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    private string _wellboreId = string.Empty;

    [ObservableProperty]
    private string _dataTableName = string.Empty;

    // --- (1) Log Information ---
    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _serviceCompany = string.Empty;

    [ObservableProperty]
    private string _edrProvider = string.Empty;

    [ObservableProperty]
    private string _runNo = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _primaryLog;

    [ObservableProperty]
    private bool _remarksLog;

    // Hole Depth Section
    [ObservableProperty]
    private bool _noAutoCalc = true;

    [ObservableProperty]
    private string _startingHoleDepth = "0";

    // --- (2) Channels ---
    public ObservableCollection<TimelogChannelItem> Channels { get; } = new();

    [ObservableProperty]
    private TimelogChannelItem? _selectedChannel;

    // --- (3) Link Time Log ---
    [ObservableProperty]
    private bool _linkToParent;

    [ObservableProperty]
    private bool _dontMoveAhead;

    [ObservableProperty]
    private string _selectedDuplicateAction = "Merge Columns";

    public ObservableCollection<string> DuplicateActionOptions { get; } = new()
    {
        "Merge Columns",
        "Replace",
        "Ignore"
    };

    public ObservableCollection<WellOption> AvailableWells { get; } = new();

    [ObservableProperty]
    private WellOption? _selectedWellOption;

    public ObservableCollection<WellboreOption> AvailableWellbores { get; } = new();

    [ObservableProperty]
    private WellboreOption? _selectedWellboreOption;

    public ObservableCollection<TimeLogOption> AvailableTimeLogs { get; } = new();

    [ObservableProperty]
    private TimeLogOption? _selectedTimeLogOption;

    // Dropdown choices
    public ObservableCollection<string> AvailableServiceCompanies { get; } = new()
    {
        "Schlumberger (SLB)",
        "Halliburton",
        "Baker Hughes",
        "Weatherford",
        "Nabors",
        "Ensign",
        "Helmerich & Payne",
        "Precision Drilling",
        "Patterson-UTI",
        "Generic Service Co"
    };

    public ObservableCollection<string> AvailableEdrProviders { get; } = new()
    {
        "Pason",
        "NOV",
        "SLB / Schlumberger",
        "Totco",
        "Epoch",
        "PetroLink",
        "Halliburton Insite",
        "Generic EDR"
    };

    public ObservableCollection<string> ValueTypeOptions { get; } = new()
    {
        "0 - Static / Raw",
        "1 - Calculated / Query"
    };

    public EditTimeLogViewModel()
    {
        // Parameterless constructor for design-time / testing
        _logName = "Timelog1";
        _runNo = "1";
        _startingHoleDepth = "0";
        _dataTableName = "timeLog_demo";
        _title = "Edit Timelog — Timelog1";
    }

    public EditTimeLogViewModel(ProjectSession? session, IWellDataRepository? repository, string logId, TimeLog? fallbackTimeLog = null)
    {
        _session = session;
        _repository = repository;
        _logId = logId;
        _fallbackTimeLog = fallbackTimeLog;

        if (fallbackTimeLog != null)
        {
            LogName = fallbackTimeLog.nameLog;
            ServiceCompany = fallbackTimeLog.serviceCompany;
            EdrProvider = fallbackTimeLog.EDRProvider;
            RunNo = fallbackTimeLog.runNumber;
            Description = fallbackTimeLog.description;
            PrimaryLog = fallbackTimeLog.PrimaryLog;
            RemarksLog = fallbackTimeLog.RemarksLog;
            NoAutoCalc = fallbackTimeLog.DontCalcHoleDepth;
            StartingHoleDepth = fallbackTimeLog.StartingHoleDepth.ToString(CultureInfo.InvariantCulture);
            DataTableName = fallbackTimeLog.__dataTableName;
            WellId = fallbackTimeLog.WellID;
            WellboreId = fallbackTimeLog.WellboreID;
            LinkToParent = fallbackTimeLog.LinkToParent;
            DontMoveAhead = fallbackTimeLog.DontMoveAhead;
            SelectedDuplicateAction = fallbackTimeLog.DuplicateAction switch
            {
                enumDuplicateAction.SkipDuplicates => "Ignore",
                enumDuplicateAction.OverwriteDuplicates => "Replace",
                _ => "Merge Columns"
            };
        }
    }

    public async Task InitializeAsync()
    {
        if (_repository == null) return;

        try
        {
            IsLoading = true;

            // 1. Query vmx_time_log for the selected Timelog ID
            var meta = await _repository.GetTimeLogEditMetadataAsync(LogId);
            if (meta != null)
            {
                LogName = meta.LogName;
                ServiceCompany = meta.ServiceCompany;
                EdrProvider = meta.EdrProvider;
                RunNo = meta.RunNo;
                Description = meta.Description;
                PrimaryLog = meta.PrimaryLog;
                RemarksLog = meta.RemarksLog;
                NoAutoCalc = meta.NoAutoCalc;
                StartingHoleDepth = meta.StartingHoleDepth.ToString(CultureInfo.InvariantCulture);
                DataTableName = meta.DataTableName;
                WellId = meta.WellId;
                WellboreId = meta.WellboreId;
                LinkToParent = meta.LinkToParent;
                DontMoveAhead = meta.DontMoveAhead;
                SelectedDuplicateAction = meta.DuplicateAction;

                Title = string.IsNullOrWhiteSpace(meta.LogName) ? "Edit Timelog" : $"Edit Timelog — {meta.LogName}";
            }
            else if (_fallbackTimeLog != null)
            {
                DataTableName = _fallbackTimeLog.__dataTableName;
                Title = $"Edit Timelog — {_fallbackTimeLog.nameLog}";
            }

            // 2. Query all rows from DATA_TABLE_NAME and populate Channels tab grid
            var channels = await _repository.GetTimeLogChannelsAsync(LogId, DataTableName);
            Channels.Clear();
            foreach (var ch in channels)
            {
                Channels.Add(ch);
            }

            if (Channels.Count > 0)
            {
                SelectedChannel = Channels[0];
            }

            // 3. Load Link Time Log choices (Wells, Wellbores, Timelogs)
            var wells = await _repository.GetWellsForLinkingAsync();
            AvailableWells.Clear();
            foreach (var w in wells)
            {
                AvailableWells.Add(w);
            }

            if (!string.IsNullOrWhiteSpace(meta?.LinkWellId))
            {
                SelectedWellOption = AvailableWells.FirstOrDefault(w => w.WellId.Equals(meta.LinkWellId, StringComparison.OrdinalIgnoreCase));
            }
            else if (AvailableWells.Count > 0)
            {
                SelectedWellOption = AvailableWells[0];
            }

            await RefreshWellboresAsync(meta?.LinkWellboreId);
            await RefreshTimeLogsAsync(meta?.LinkLogId);

            // Add any custom service companies or EDR providers if present
            if (!string.IsNullOrWhiteSpace(ServiceCompany) && !AvailableServiceCompanies.Contains(ServiceCompany))
            {
                AvailableServiceCompanies.Insert(0, ServiceCompany);
            }
            if (!string.IsNullOrWhiteSpace(EdrProvider) && !AvailableEdrProviders.Contains(EdrProvider))
            {
                AvailableEdrProviders.Insert(0, EdrProvider);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error initializing dialog: {ex.Message}";
            IsStatusError = true;
            IsStatusVisible = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    async partial void OnSelectedWellOptionChanged(WellOption? value)
    {
        await RefreshWellboresAsync();
        await RefreshTimeLogsAsync();
    }

    async partial void OnSelectedWellboreOptionChanged(WellboreOption? value)
    {
        await RefreshTimeLogsAsync();
    }

    private async Task RefreshWellboresAsync(string? targetWellboreId = null)
    {
        if (_repository == null) return;
        var wellbores = await _repository.GetWellboresForLinkingAsync(SelectedWellOption?.WellId);
        AvailableWellbores.Clear();
        foreach (var wb in wellbores)
        {
            AvailableWellbores.Add(wb);
        }

        if (!string.IsNullOrWhiteSpace(targetWellboreId))
        {
            SelectedWellboreOption = AvailableWellbores.FirstOrDefault(wb => wb.WellboreId.Equals(targetWellboreId, StringComparison.OrdinalIgnoreCase));
        }
        if (SelectedWellboreOption == null && AvailableWellbores.Count > 0)
        {
            SelectedWellboreOption = AvailableWellbores[0];
        }
    }

    private async Task RefreshTimeLogsAsync(string? targetLogId = null)
    {
        if (_repository == null) return;
        var timelogs = await _repository.GetTimeLogsForLinkingAsync(SelectedWellOption?.WellId, SelectedWellboreOption?.WellboreId, LogId);
        AvailableTimeLogs.Clear();
        foreach (var tl in timelogs)
        {
            AvailableTimeLogs.Add(tl);
        }

        if (!string.IsNullOrWhiteSpace(targetLogId))
        {
            SelectedTimeLogOption = AvailableTimeLogs.FirstOrDefault(tl => tl.LogId.Equals(targetLogId, StringComparison.OrdinalIgnoreCase));
        }
        if (SelectedTimeLogOption == null && AvailableTimeLogs.Count > 0)
        {
            SelectedTimeLogOption = AvailableTimeLogs[0];
        }
    }

    // --- Action Buttons for Channels ---

    [RelayCommand]
    public void AddChannel()
    {
        int index = Channels.Count + 1;
        string mnemonic = $"CHAN_{index}";
        while (Channels.Any(c => c.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase)))
        {
            index++;
            mnemonic = $"CHAN_{index}";
        }

        var newChannel = new TimelogChannelItem
        {
            Upload = true,
            Mnemonic = mnemonic,
            Unit = "",
            VuMaxUnitId = "",
            Description = $"Channel {index}",
            UploadMnemonic = mnemonic,
            ValueType = "0",
            Expression = "",
            DoNotInterpol = false,
            DataType = "Double",
            ColumnOrder = Channels.Count + 1
        };

        Channels.Add(newChannel);
        SelectedChannel = newChannel;
        StatusMessage = $"Channel '{mnemonic}' added. You can edit its details directly in the grid.";
        IsStatusError = false;
        IsStatusVisible = true;
    }

    [RelayCommand]
    public void EditChannel(TimelogChannelItem? channel)
    {
        var target = channel ?? SelectedChannel;
        if (target == null)
        {
            StatusMessage = "Please select a channel in the grid to edit.";
            IsStatusError = true;
            IsStatusVisible = true;
            return;
        }

        SelectedChannel = target;
        StatusMessage = $"Editing channel '{target.Mnemonic}'. Edit fields directly in the table.";
        IsStatusError = false;
        IsStatusVisible = true;
    }

    [RelayCommand]
    public void RemoveChannel(TimelogChannelItem? channel)
    {
        var target = channel ?? SelectedChannel;
        if (target == null)
        {
            StatusMessage = "Please select a channel in the grid to remove.";
            IsStatusError = true;
            IsStatusVisible = true;
            return;
        }

        string removedName = target.Mnemonic;
        Channels.Remove(target);
        SelectedChannel = Channels.FirstOrDefault();

        StatusMessage = $"Channel '{removedName}' removed.";
        IsStatusError = false;
        IsStatusVisible = true;
    }

    [RelayCommand]
    public void ApplyUnitConversionProfile(string? profile = null)
    {
        // Determine profile: Metric or Imperial
        bool isMetric = string.Equals(profile, "Metric", StringComparison.OrdinalIgnoreCase) ||
                        (!string.Equals(profile, "Imperial", StringComparison.OrdinalIgnoreCase) &&
                         Channels.Any(c => c.Unit.Equals("ft", StringComparison.OrdinalIgnoreCase) || c.Unit.Equals("psi", StringComparison.OrdinalIgnoreCase)));

        string targetProfile = isMetric ? "Metric" : "Imperial";

        foreach (var ch in Channels)
        {
            var mnem = ch.Mnemonic.ToUpperInvariant();
            if (mnem.Contains("DEPTH"))
            {
                ch.Unit = isMetric ? "m" : "ft";
                ch.VuMaxUnitId = isMetric ? "m" : "ft";
            }
            else if (mnem.Contains("HKLD") || mnem.Contains("HOOK") || mnem.Contains("WOB"))
            {
                ch.Unit = isMetric ? "kN" : "klbf";
                ch.VuMaxUnitId = isMetric ? "kN" : "klbf";
            }
            else if (mnem.Contains("PRESS") || mnem.Contains("SPP"))
            {
                ch.Unit = isMetric ? "kPa" : "psi";
                ch.VuMaxUnitId = isMetric ? "kPa" : "psi";
            }
            else if (mnem.Contains("TORQ"))
            {
                ch.Unit = isMetric ? "kN.m" : "ft-lbf";
                ch.VuMaxUnitId = isMetric ? "kN.m" : "ft-lbf";
            }
            else if (mnem.Contains("FLOW") || mnem.Contains("PUMP"))
            {
                ch.Unit = isMetric ? "L/min" : "gpm";
                ch.VuMaxUnitId = isMetric ? "L/min" : "gpm";
            }
            else if (mnem.Contains("ROP"))
            {
                ch.Unit = isMetric ? "m/h" : "ft/h";
                ch.VuMaxUnitId = isMetric ? "m/h" : "ft/h";
            }
            else if (mnem.Contains("TEMP"))
            {
                ch.Unit = isMetric ? "degC" : "degF";
                ch.VuMaxUnitId = isMetric ? "degC" : "degF";
            }
        }

        StatusMessage = $"Applied standard {targetProfile} unit profile to channels.";
        IsStatusError = false;
        IsStatusVisible = true;
    }

    // --- General Controls (OK / Cancel) ---

    [RelayCommand]
    public async Task SaveAsync()
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(LogName))
        {
            StatusMessage = "Log Name is required. Please specify a name for the timelog.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(RunNo) && !double.TryParse(RunNo, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            StatusMessage = "Run No. must be numeric.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        double startHoleDepth = 0;
        if (!string.IsNullOrWhiteSpace(StartingHoleDepth) && !double.TryParse(StartingHoleDepth, NumberStyles.Any, CultureInfo.InvariantCulture, out startHoleDepth))
        {
            StatusMessage = "Starting Hole Depth must be a valid number.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        // Check for duplicate mnemonics in channels
        var duplicateMnemonic = Channels
            .GroupBy(c => c.Mnemonic.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateMnemonic != null)
        {
            StatusMessage = $"Duplicate channel mnemonic found: '{duplicateMnemonic.Key}'. Each channel mnemonic must be unique.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 1;
            return;
        }

        try
        {
            IsLoading = true;

            var metadata = new TimeLogEditMetadata
            {
                LogId = LogId,
                WellId = WellId,
                WellboreId = WellboreId,
                LogName = LogName.Trim(),
                ServiceCompany = ServiceCompany?.Trim() ?? string.Empty,
                EdrProvider = EdrProvider?.Trim() ?? string.Empty,
                RunNo = RunNo?.Trim() ?? string.Empty,
                Description = Description?.Trim() ?? string.Empty,
                PrimaryLog = PrimaryLog,
                RemarksLog = RemarksLog,
                NoAutoCalc = NoAutoCalc,
                StartingHoleDepth = startHoleDepth,
                DataTableName = DataTableName,
                LinkToParent = LinkToParent,
                LinkWellId = SelectedWellOption?.WellId ?? string.Empty,
                LinkWellboreId = SelectedWellboreOption?.WellboreId ?? string.Empty,
                LinkLogId = SelectedTimeLogOption?.LogId ?? string.Empty,
                DontMoveAhead = DontMoveAhead,
                DuplicateAction = SelectedDuplicateAction
            };

            if (_repository != null)
            {
                await _repository.SaveTimeLogEditAsync(metadata, Channels.ToList());
            }

            if (_fallbackTimeLog != null)
            {
                _fallbackTimeLog.nameLog = metadata.LogName;
                _fallbackTimeLog.serviceCompany = metadata.ServiceCompany;
                _fallbackTimeLog.EDRProvider = metadata.EdrProvider;
                _fallbackTimeLog.runNumber = metadata.RunNo;
                _fallbackTimeLog.description = metadata.Description;
                _fallbackTimeLog.PrimaryLog = metadata.PrimaryLog;
                _fallbackTimeLog.RemarksLog = metadata.RemarksLog;
                _fallbackTimeLog.DontCalcHoleDepth = metadata.NoAutoCalc;
                _fallbackTimeLog.StartingHoleDepth = metadata.StartingHoleDepth;
                _fallbackTimeLog.LinkToParent = metadata.LinkToParent;
                _fallbackTimeLog.LinkWellID = metadata.LinkWellId;
                _fallbackTimeLog.LinkWellboreID = metadata.LinkWellboreId;
                _fallbackTimeLog.LinkLogID = metadata.LinkLogId;
                _fallbackTimeLog.DontMoveAhead = metadata.DontMoveAhead;
                _fallbackTimeLog.DuplicateAction = metadata.DuplicateAction switch
                {
                    "Ignore" => enumDuplicateAction.SkipDuplicates,
                    "Replace" => enumDuplicateAction.OverwriteDuplicates,
                    _ => enumDuplicateAction.MergeColumns
                };
            }

            StatusMessage = "Timelog updated successfully.";
            IsStatusError = false;
            IsStatusVisible = false;
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save changes: {ex.Message}";
            IsStatusError = true;
            IsStatusVisible = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
