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
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class EditDepthLogViewModel : ObservableObject
{
    private readonly ProjectSession? _session;
    private readonly IWellDataRepository? _repository;
    private readonly DepthLog? _fallbackDepthLog;

    public event Action<bool?>? RequestClose;

    // Test hook / UI handler for Channel Properties dialog
    public Func<ChannelPropertiesViewModel, bool?>? OpenChannelPropertiesDialogHandler { get; set; }

    [ObservableProperty]
    private string _title = "Edit Depthlog";

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
    private string _comments = string.Empty;

    [ObservableProperty]
    private bool _primaryLog;

    // Depth Range / Index Section
    [ObservableProperty]
    private string _startDepth = "0";

    [ObservableProperty]
    private string _endDepth = "0";

    [ObservableProperty]
    private string _stepIncrement = "0.1";

    [ObservableProperty]
    private string _direction = "Top To Bottom";

    public ObservableCollection<string> AvailableDirections { get; } = new()
    {
        "Top To Bottom",
        "Bottom To Top"
    };

    // --- (2) Channels ---
    public ObservableCollection<LogChannel> Channels { get; } = new();

    [ObservableProperty]
    private LogChannel? _selectedChannel;

    [ObservableProperty]
    private DepthLog? _currentDepthLog;

    // --- (3) Link Depth Log ---
    [ObservableProperty]
    private bool _linkToParent;

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

    public ObservableCollection<DepthLogOption> AvailableDepthLogs { get; } = new();

    [ObservableProperty]
    private DepthLogOption? _selectedDepthLogOption;

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

    public EditDepthLogViewModel()
    {
        // Parameterless constructor for design-time / testing
        _logName = "Depthlog1";
        _runNo = "1";
        _startDepth = "0";
        _endDepth = "3000";
        _stepIncrement = "0.1";
        _dataTableName = "depthLog_demo";
        _title = "Edit Depthlog — Depthlog1";
    }

    public EditDepthLogViewModel(ProjectSession? session, IWellDataRepository? repository, string logId, DepthLog? fallbackDepthLog = null)
    {
        _session = session;
        _repository = repository;
        _logId = logId;
        _fallbackDepthLog = fallbackDepthLog;

        if (fallbackDepthLog != null)
        {
            LogName = fallbackDepthLog.nameLog;
            ServiceCompany = fallbackDepthLog.serviceCompany;
            EdrProvider = fallbackDepthLog.EDRProvider;
            RunNo = fallbackDepthLog.runNumber;
            Description = fallbackDepthLog.description;
            Comments = fallbackDepthLog.comments;
            PrimaryLog = fallbackDepthLog.PrimaryLog;
            StartDepth = fallbackDepthLog.startIndex;
            EndDepth = fallbackDepthLog.endIndex;
            StepIncrement = !string.IsNullOrWhiteSpace(fallbackDepthLog.stepIncrement) ? fallbackDepthLog.stepIncrement : "0.1";
            Direction = !string.IsNullOrWhiteSpace(fallbackDepthLog.direction) ? fallbackDepthLog.direction : "Top To Bottom";
            DataTableName = fallbackDepthLog.__dataTableName;
            WellId = fallbackDepthLog.WellID;
            WellboreId = fallbackDepthLog.WellboreID;
            LinkToParent = fallbackDepthLog.LinkToParent;
            SelectedDuplicateAction = fallbackDepthLog.DuplicateAction switch
            {
                enumDuplicateAction.SkipDuplicates => "Ignore",
                enumDuplicateAction.OverwriteDuplicates => "Replace",
                _ => "Merge Columns"
            };
            Title = string.IsNullOrWhiteSpace(fallbackDepthLog.nameLog) ? "Edit Depthlog" : $"Edit Depthlog — {fallbackDepthLog.nameLog}";
        }
    }

    public async Task InitializeAsync()
    {
        if (_repository == null) return;

        try
        {
            IsLoading = true;

            DepthLog? log = await _repository.GetDepthLogAsync(LogId);
            if (log == null && _session?.GetDataService() is IDataServiceDIntel ds)
            {
                string err = "";
                log = DepthLogService.LoadObject(ds, LogId, ref err);
            }
            if (log == null && _fallbackDepthLog != null)
            {
                log = _fallbackDepthLog;
            }

            if (log != null)
            {
                CurrentDepthLog = log;
                LogName = log.nameLog;
                ServiceCompany = log.serviceCompany;
                EdrProvider = log.EDRProvider;
                RunNo = log.runNumber;
                Description = log.description;
                Comments = log.comments;
                PrimaryLog = log.PrimaryLog;
                if (!string.IsNullOrWhiteSpace(log.startIndex)) StartDepth = log.startIndex;
                if (!string.IsNullOrWhiteSpace(log.endIndex)) EndDepth = log.endIndex;
                if (!string.IsNullOrWhiteSpace(log.stepIncrement)) StepIncrement = log.stepIncrement;
                if (!string.IsNullOrWhiteSpace(log.direction)) Direction = log.direction;
                DataTableName = log.__dataTableName;
                WellId = log.WellID;
                WellboreId = log.WellboreID;
                LinkToParent = log.LinkToParent;
                SelectedDuplicateAction = log.DuplicateAction switch
                {
                    enumDuplicateAction.SkipDuplicates => "Ignore",
                    enumDuplicateAction.OverwriteDuplicates => "Replace",
                    _ => "Merge Columns"
                };

                Title = string.IsNullOrWhiteSpace(log.nameLog) ? "Edit Depthlog" : $"Edit Depthlog — {log.nameLog}";
            }

            // 2. Query all channels and populate Channels tab grid
            List<LogChannel> channels;
            if (CurrentDepthLog != null && CurrentDepthLog.LogCurves.Count > 0)
            {
                channels = CurrentDepthLog.LogCurves.Values.OrderBy(c => c.ColumnOrder).ToList();
            }
            else
            {
                channels = await _repository.GetDepthLogChannelsAsync(LogId, DataTableName);
            }

            Channels.Clear();
            foreach (var ch in channels)
            {
                Channels.Add(ch);
            }

            if (Channels.Count > 0)
            {
                SelectedChannel = Channels[0];
            }

            // 3. Load Link Depth Log choices (Wells, Wellbores, Depthlogs)
            var wells = await _repository.GetWellsForLinkingAsync();
            AvailableWells.Clear();
            foreach (var w in wells)
            {
                AvailableWells.Add(w);
            }

            string linkWell = CurrentDepthLog?.LinkWellID ?? "";
            string linkWb = CurrentDepthLog?.LinkWellboreID ?? "";
            string linkLog = CurrentDepthLog?.LinkLogID ?? "";

            if (!string.IsNullOrWhiteSpace(linkWell))
            {
                SelectedWellOption = AvailableWells.FirstOrDefault(w => w.WellId.Equals(linkWell, StringComparison.OrdinalIgnoreCase));
            }
            else if (AvailableWells.Count > 0)
            {
                SelectedWellOption = AvailableWells[0];
            }

            await RefreshWellboresAsync(linkWb);
            await RefreshDepthLogsAsync(linkLog);

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
        await RefreshDepthLogsAsync();
    }

    async partial void OnSelectedWellboreOptionChanged(WellboreOption? value)
    {
        await RefreshDepthLogsAsync();
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

    private async Task RefreshDepthLogsAsync(string? targetLogId = null)
    {
        if (_repository == null) return;
        var depthlogs = await _repository.GetDepthLogsForLinkingAsync(SelectedWellOption?.WellId, SelectedWellboreOption?.WellboreId, LogId);
        AvailableDepthLogs.Clear();
        foreach (var dl in depthlogs)
        {
            AvailableDepthLogs.Add(dl);
        }

        if (!string.IsNullOrWhiteSpace(targetLogId))
        {
            SelectedDepthLogOption = AvailableDepthLogs.FirstOrDefault(dl => dl.LogId.Equals(targetLogId, StringComparison.OrdinalIgnoreCase));
        }
        if (SelectedDepthLogOption == null && AvailableDepthLogs.Count > 0)
        {
            SelectedDepthLogOption = AvailableDepthLogs[0];
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

        var dataService = _session?.GetDataService() ?? Unit.DefaultDataService;
        var existingMnemonics = Channels.Select(c => c.Mnemonic).ToList();

        var vm = new ChannelPropertiesViewModel(dataService, existingMnemonics, null, Channels.Count + 1, Channels)
        {
            Mnemonic = mnemonic,
            Description = $"Channel {index}",
            ValueType = "Expression",
            ColumnOrder = Channels.Count + 1,
            SensorOffset = "0"
        };

        bool? result;
        if (OpenChannelPropertiesDialogHandler != null)
        {
            result = OpenChannelPropertiesDialogHandler(vm);
        }
        else
        {
            result = ShowChannelPropertiesDialog(vm);
        }

        if (result == true)
        {
            var newChannel = vm.ToLogChannel();
            Channels.Add(newChannel);
            SelectedChannel = newChannel;
            StatusMessage = $"Expression Channel '{newChannel.Mnemonic}' added.";
            IsStatusError = false;
            IsStatusVisible = true;
        }
    }

    [RelayCommand]
    public void EditChannel(LogChannel? channel)
    {
        var target = channel ?? SelectedChannel;
        if (target == null)
        {
            StatusMessage = "Please select a channel in the grid to edit.";
            IsStatusError = true;
            IsStatusVisible = true;
            return;
        }

        var dataService = _session?.GetDataService() ?? Unit.DefaultDataService;
        var existingMnemonics = Channels.Where(c => c != target).Select(c => c.Mnemonic).ToList();

        var vm = new ChannelPropertiesViewModel(dataService, existingMnemonics, target, target.ColumnOrder, Channels);

        bool? result;
        if (OpenChannelPropertiesDialogHandler != null)
        {
            result = OpenChannelPropertiesDialogHandler(vm);
        }
        else
        {
            result = ShowChannelPropertiesDialog(vm);
        }

        if (result == true)
        {
            vm.ApplyTo(target);
            SelectedChannel = target;
            StatusMessage = $"Channel '{target.Mnemonic}' updated.";
            IsStatusError = false;
            IsStatusVisible = true;
        }
    }

    private bool? ShowChannelPropertiesDialog(ChannelPropertiesViewModel vm)
    {
        if (OpenChannelPropertiesDialogHandler != null)
        {
            return OpenChannelPropertiesDialogHandler(vm);
        }

        if (System.Windows.Application.Current == null)
        {
            vm.Ok();
            return !vm.HasError;
        }

        try
        {
            var dialog = new DrillIntel.Views.ChannelPropertiesWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                        ?? System.Windows.Application.Current?.MainWindow
            };
            return dialog.ShowDialog();
        }
        catch
        {
            vm.Ok();
            return !vm.HasError;
        }
    }

    [RelayCommand]
    public void RemoveChannel(LogChannel? channel)
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
        bool isMetric = string.Equals(profile, "Metric", StringComparison.OrdinalIgnoreCase) ||
                        (!string.Equals(profile, "Imperial", StringComparison.OrdinalIgnoreCase) &&
                         Channels.Any(c => c.Unit.Equals("ft", StringComparison.OrdinalIgnoreCase) || c.Unit.Equals("psi", StringComparison.OrdinalIgnoreCase)));

        string targetProfile = isMetric ? "Metric" : "Imperial";

        foreach (var ch in Channels)
        {
            var mnem = ch.Mnemonic.ToUpperInvariant();
            if (mnem.Contains("DEPTH") || mnem.Equals("DEPT") || mnem.Equals("MD") || mnem.Equals("TVD"))
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
            else if (mnem.Contains("GR") || mnem.Contains("GAMMA"))
            {
                ch.Unit = "gAPI";
                ch.VuMaxUnitId = "gAPI";
            }
            else if (mnem.Contains("RES") || mnem.Contains("RESIST"))
            {
                ch.Unit = "ohm.m";
                ch.VuMaxUnitId = "ohm.m";
            }
            else if (mnem.Contains("DEN") || mnem.Contains("RHOB"))
            {
                ch.Unit = isMetric ? "g/cm3" : "lb/gal";
                ch.VuMaxUnitId = isMetric ? "g/cm3" : "lb/gal";
            }
            else if (mnem.Contains("POR") || mnem.Contains("NPHI"))
            {
                ch.Unit = "v/v";
                ch.VuMaxUnitId = "v/v";
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
            StatusMessage = "Log Name is required. Please specify a name for the depth log.";
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

        if (!string.IsNullOrWhiteSpace(StartDepth) && !double.TryParse(StartDepth, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            StatusMessage = "Start Depth must be a valid number.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(EndDepth) && !double.TryParse(EndDepth, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            StatusMessage = "End Depth must be a valid number.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(StepIncrement))
        {
            if (!double.TryParse(StepIncrement, NumberStyles.Any, CultureInfo.InvariantCulture, out double stepVal))
            {
                StatusMessage = "Step Increment must be a valid number.";
                IsStatusError = true;
                IsStatusVisible = true;
                SelectedTabIndex = 0;
                return;
            }
            else if (stepVal <= 0)
            {
                StatusMessage = "Step Increment must be greater than zero.";
                IsStatusError = true;
                IsStatusVisible = true;
                SelectedTabIndex = 0;
                return;
            }
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

            var log = CurrentDepthLog ?? _fallbackDepthLog ?? new DepthLog();
            log.ObjectID = LogId;
            log.WellID = WellId;
            log.WellboreID = WellboreId;
            log.nameLog = LogName.Trim();
            log.serviceCompany = ServiceCompany?.Trim() ?? string.Empty;
            log.EDRProvider = EdrProvider?.Trim() ?? string.Empty;
            log.runNumber = RunNo?.Trim() ?? string.Empty;
            log.description = Description?.Trim() ?? string.Empty;
            log.comments = Comments?.Trim() ?? string.Empty;
            log.PrimaryLog = PrimaryLog;
            log.startIndex = StartDepth.Trim();
            log.endIndex = EndDepth.Trim();
            log.stepIncrement = StepIncrement.Trim();
            log.direction = Direction;
            log.__dataTableName = DataTableName;
            log.LinkToParent = LinkToParent;
            log.LinkWellID = SelectedWellOption?.WellId ?? string.Empty;
            log.LinkWellboreID = SelectedWellboreOption?.WellboreId ?? string.Empty;
            log.LinkLogID = SelectedDepthLogOption?.LogId ?? string.Empty;
            log.DuplicateAction = SelectedDuplicateAction switch
            {
                "Ignore" => enumDuplicateAction.SkipDuplicates,
                "Replace" => enumDuplicateAction.OverwriteDuplicates,
                _ => enumDuplicateAction.MergeColumns
            };

            // Update LogCurves dictionary
            log.LogCurves.Clear();
            int order = 1;
            foreach (var ch in Channels)
            {
                ch.ColumnOrder = order++;
                log.LogCurves[ch.Mnemonic] = ch;
            }

            if (_repository != null)
            {
                await _repository.SaveDepthLogAsync(log, Channels.ToList());
            }
            else if (_session?.GetDataService() is IDataServiceDIntel ds)
            {
                string lastError = "";
                DepthLogService.SaveDepthLog(ds, log, Channels.ToList(), ref lastError);
            }

            if (_fallbackDepthLog != null && !ReferenceEquals(_fallbackDepthLog, log))
            {
                _fallbackDepthLog.nameLog = log.nameLog;
                _fallbackDepthLog.serviceCompany = log.serviceCompany;
                _fallbackDepthLog.EDRProvider = log.EDRProvider;
                _fallbackDepthLog.runNumber = log.runNumber;
                _fallbackDepthLog.description = log.description;
                _fallbackDepthLog.comments = log.comments;
                _fallbackDepthLog.PrimaryLog = log.PrimaryLog;
                _fallbackDepthLog.startIndex = log.startIndex;
                _fallbackDepthLog.endIndex = log.endIndex;
                _fallbackDepthLog.stepIncrement = log.stepIncrement;
                _fallbackDepthLog.direction = log.direction;
                _fallbackDepthLog.LinkToParent = log.LinkToParent;
                _fallbackDepthLog.LinkWellID = log.LinkWellID;
                _fallbackDepthLog.LinkWellboreID = log.LinkWellboreID;
                _fallbackDepthLog.LinkLogID = log.LinkLogID;
                _fallbackDepthLog.DuplicateAction = log.DuplicateAction;
            }

            StatusMessage = "Depthlog updated successfully.";
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

