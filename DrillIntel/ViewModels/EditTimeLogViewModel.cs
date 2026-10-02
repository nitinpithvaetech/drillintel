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

public partial class EditTimeLogViewModel : ObservableObject
{
    private readonly ProjectSession? _session;
    private readonly IWellDataRepository? _repository;
    private readonly TimeLog? _fallbackTimeLog;

    public event Action<bool?>? RequestClose;

    // Test hook / UI handler for Channel Properties dialog
    public Func<ChannelPropertiesViewModel, bool?>? OpenChannelPropertiesDialogHandler { get; set; }

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
    // --- [OLD LOGIC (TimelogChannelItem: replaced by LogChannel)] ---
    // public ObservableCollection<TimelogChannelItem> Channels { get; } = new();
    // [ObservableProperty]
    // private TimelogChannelItem? _selectedChannel;

    // --- [NEW LOGIC (LogChannel and TimeLog from DrillIntel.Data.Objects)] ---
    public ObservableCollection<LogChannel> Channels { get; } = new();

    [ObservableProperty]
    private LogChannel? _selectedChannel;

    [ObservableProperty]
    private TimeLog? _currentTimeLog;

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

            // --- [OLD LOGIC (Loaded TimeLogEditMetadata from repository)] ---
            // var meta = await _repository.GetTimeLogEditMetadataAsync(LogId);
            // if (meta != null) { ... }
            // var channels = await _repository.GetTimeLogChannelsAsync(LogId, DataTableName);

            // --- [NEW LOGIC (Load TimeLog and LogChannel directly using TimeLog and TimeLogService)] ---
            TimeLog? log = await _repository.GetTimeLogAsync(LogId);
            if (log == null && _session?.GetDataService() is IDataServiceDIntel ds)
            {
                string err = "";
                log = TimeLogService.LoadObject(ds, LogId, ref err);
            }
            if (log == null && _fallbackTimeLog != null)
            {
                log = _fallbackTimeLog;
            }

            if (log != null)
            {
                CurrentTimeLog = log;
                LogName = log.nameLog;
                ServiceCompany = log.serviceCompany;
                EdrProvider = log.EDRProvider;
                RunNo = log.runNumber;
                Description = log.description;
                PrimaryLog = log.PrimaryLog;
                RemarksLog = log.RemarksLog;
                NoAutoCalc = log.DontCalcHoleDepth;
                StartingHoleDepth = log.StartingHoleDepth.ToString(CultureInfo.InvariantCulture);
                DataTableName = log.__dataTableName;
                WellId = log.WellID;
                WellboreId = log.WellboreID;
                LinkToParent = log.LinkToParent;
                DontMoveAhead = log.DontMoveAhead;
                SelectedDuplicateAction = log.DuplicateAction switch
                {
                    enumDuplicateAction.SkipDuplicates => "Ignore",
                    enumDuplicateAction.OverwriteDuplicates => "Replace",
                    _ => "Merge Columns"
                };

                Title = string.IsNullOrWhiteSpace(log.nameLog) ? "Edit Timelog" : $"Edit Timelog — {log.nameLog}";
            }
            else
            {
                // Fallback to legacy metadata if necessary
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
            }

            // 2. Query all channels and populate Channels tab grid
            List<LogChannel> channels;
            if (CurrentTimeLog != null && CurrentTimeLog.logCurves.Count > 0)
            {
                channels = CurrentTimeLog.logCurves.Values.OrderBy(c => c.ColumnOrder).ToList();
            }
            else
            {
                channels = await _repository.GetLogChannelsAsync(LogId, DataTableName);
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

            // 3. Load Link Time Log choices (Wells, Wellbores, Timelogs)
            var wells = await _repository.GetWellsForLinkingAsync();
            AvailableWells.Clear();
            foreach (var w in wells)
            {
                AvailableWells.Add(w);
            }

            string linkWell = CurrentTimeLog?.LinkWellID ?? "";
            string linkWb = CurrentTimeLog?.LinkWellboreID ?? "";
            string linkLog = CurrentTimeLog?.LinkLogID ?? "";

            if (!string.IsNullOrWhiteSpace(linkWell))
            {
                SelectedWellOption = AvailableWells.FirstOrDefault(w => w.WellId.Equals(linkWell, StringComparison.OrdinalIgnoreCase));
            }
            else if (AvailableWells.Count > 0)
            {
                SelectedWellOption = AvailableWells[0];
            }

            await RefreshWellboresAsync(linkWb);
            await RefreshTimeLogsAsync(linkLog);

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

        var dataService = _session?.GetDataService() ?? Unit.DefaultDataService;
        var existingMnemonics = Channels.Select(c => c.Mnemonic).ToList();

        // --- [OLD LOGIC (Immediately appended placeholder channel without dialog)] ---
        // var newChannel = new LogChannel
        // {
        //     Upload = true,
        //     Mnemonic = mnemonic,
        //     Unit = "",
        //     VuMaxUnitId = "",
        //     Description = $"Channel {index}",
        //     UploadMnemonic = mnemonic,
        //     ValueType = "0",
        //     Expression = "",
        //     DoNotInterpol = false,
        //     DataType = "Double",
        //     ColumnOrder = Channels.Count + 1,
        //     OriginalMnemonic = mnemonic
        // };
        // Channels.Add(newChannel);
        // SelectedChannel = newChannel;

        // --- [NEW LOGIC (Open Channel Properties dialog with Expression valueType and Project Unit Master units)] ---
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

        // --- [NEW LOGIC (Open Channel Properties dialog to edit channel details)] ---
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

        // If running in headless / unit test environment without active WPF application
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

            // --- [OLD LOGIC (TimeLogEditMetadata)] ---
            // var metadata = new TimeLogEditMetadata
            // {
            //     LogId = LogId,
            //     WellId = WellId,
            //     WellboreId = WellboreId,
            //     LogName = LogName.Trim(),
            //     ServiceCompany = ServiceCompany?.Trim() ?? string.Empty,
            //     EdrProvider = EdrProvider?.Trim() ?? string.Empty,
            //     RunNo = RunNo?.Trim() ?? string.Empty,
            //     Description = Description?.Trim() ?? string.Empty,
            //     PrimaryLog = PrimaryLog,
            //     RemarksLog = RemarksLog,
            //     NoAutoCalc = NoAutoCalc,
            //     StartingHoleDepth = startHoleDepth,
            //     DataTableName = DataTableName,
            //     LinkToParent = LinkToParent,
            //     LinkWellId = SelectedWellOption?.WellId ?? string.Empty,
            //     LinkWellboreId = SelectedWellboreOption?.WellboreId ?? string.Empty,
            //     LinkLogId = SelectedTimeLogOption?.LogId ?? string.Empty,
            //     DontMoveAhead = DontMoveAhead,
            //     DuplicateAction = SelectedDuplicateAction
            // };
            // if (_repository != null)
            // {
            //     await _repository.SaveTimeLogEditAsync(metadata, Channels.ToList());
            // }

            // --- [NEW LOGIC (TimeLog domain object persisted directly via repository / TimeLogService)] ---
            var log = CurrentTimeLog ?? _fallbackTimeLog ?? new TimeLog();
            log.ObjectID = LogId;
            log.WellID = WellId;
            log.WellboreID = WellboreId;
            log.nameLog = LogName.Trim();
            log.serviceCompany = ServiceCompany?.Trim() ?? string.Empty;
            log.EDRProvider = EdrProvider?.Trim() ?? string.Empty;
            log.runNumber = RunNo?.Trim() ?? string.Empty;
            log.description = Description?.Trim() ?? string.Empty;
            log.PrimaryLog = PrimaryLog;
            log.RemarksLog = RemarksLog;
            log.DontCalcHoleDepth = NoAutoCalc;
            log.StartingHoleDepth = startHoleDepth;
            log.__dataTableName = DataTableName;
            log.LinkToParent = LinkToParent;
            log.LinkWellID = SelectedWellOption?.WellId ?? string.Empty;
            log.LinkWellboreID = SelectedWellboreOption?.WellboreId ?? string.Empty;
            log.LinkLogID = SelectedTimeLogOption?.LogId ?? string.Empty;
            log.DontMoveAhead = DontMoveAhead;
            log.DuplicateAction = SelectedDuplicateAction switch
            {
                "Ignore" => enumDuplicateAction.SkipDuplicates,
                "Replace" => enumDuplicateAction.OverwriteDuplicates,
                _ => enumDuplicateAction.MergeColumns
            };

            // Update logCurves dictionary
            log.logCurves.Clear();
            int order = 1;
            foreach (var ch in Channels)
            {
                ch.ColumnOrder = order++;
                log.logCurves[ch.Mnemonic] = ch;
            }

            if (_repository != null)
            {
                await _repository.SaveTimeLogAsync(log, Channels.ToList());
            }
            else if (_session?.GetDataService() is IDataServiceDIntel ds)
            {
                string lastError = "";
                TimeLogService.SaveTimeLog(ds, log, Channels.ToList(), ref lastError);
            }

            if (_fallbackTimeLog != null && !ReferenceEquals(_fallbackTimeLog, log))
            {
                _fallbackTimeLog.nameLog = log.nameLog;
                _fallbackTimeLog.serviceCompany = log.serviceCompany;
                _fallbackTimeLog.EDRProvider = log.EDRProvider;
                _fallbackTimeLog.runNumber = log.runNumber;
                _fallbackTimeLog.description = log.description;
                _fallbackTimeLog.PrimaryLog = log.PrimaryLog;
                _fallbackTimeLog.RemarksLog = log.RemarksLog;
                _fallbackTimeLog.DontCalcHoleDepth = log.DontCalcHoleDepth;
                _fallbackTimeLog.StartingHoleDepth = log.StartingHoleDepth;
                _fallbackTimeLog.LinkToParent = log.LinkToParent;
                _fallbackTimeLog.LinkWellID = log.LinkWellID;
                _fallbackTimeLog.LinkWellboreID = log.LinkWellboreID;
                _fallbackTimeLog.LinkLogID = log.LinkLogID;
                _fallbackTimeLog.DontMoveAhead = log.DontMoveAhead;
                _fallbackTimeLog.DuplicateAction = log.DuplicateAction;
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
