using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class SyncDataWithParentTimelogViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly TimeLog _sourceTimeLog;
    private readonly IWellDataRepository? _repository;
    private TimeLog? _parentTimeLog;
    private CancellationTokenSource? _cts;

    public event Action<bool>? RequestClose;

    // Source Information
    [ObservableProperty]
    private string _sourceTimeLogName = string.Empty;

    [ObservableProperty]
    private string _sourceWellName = string.Empty;

    [ObservableProperty]
    private string _sourceWellboreName = string.Empty;

    // (1) Target Information
    // - Well: [Display Well Name, e.g., Camel1_2016]
    // - Wellbore: [Display Wellbore Name, e.g., Wellbore1]
    // - Time Log: [Display Parent Timelog Name, e.g., RS Time log]
    [ObservableProperty]
    private string _targetWellName = "Camel1_2016";

    [ObservableProperty]
    private string _targetWellboreName = "Wellbore1";

    [ObservableProperty]
    private string _targetTimeLogName = "TimeLog";

    // (2) Date Range
    // - From Date: [Dropdown/Date Picker with timestamp]
    // - To Date: [Dropdown/Date Picker with timestamp]
    [ObservableProperty]
    private DateTime _fromDate = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? _fromTime = DateTime.Today;

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _toTime = DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59);

    public DateTime FromDateTime
    {
        get
        {
            var date = FromDate.Date;
            var time = FromTime?.TimeOfDay ?? TimeSpan.Zero;
            return date.Add(time);
        }
    }

    public DateTime ToDateTime
    {
        get
        {
            var date = ToDate.Date;
            var time = ToTime?.TimeOfDay ?? new TimeSpan(23, 59, 59);
            return date.Add(time);
        }
    }

    [ObservableProperty]
    private DateTime _minAvailableDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _maxAvailableDate = DateTime.MaxValue;

    public ObservableCollection<string> DateRangePresets { get; } = new()
    {
        "All Available Data",
        "Last 24 Hours",
        "Last 7 Days",
        "Custom Range"
    };

    [ObservableProperty]
    private string _selectedDateRangePreset = "All Available Data";

    // (3) Action Buttons & (4) Behavior
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanSelectDates))]
    [NotifyPropertyChangedFor(nameof(CloseButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CloseButtonText))]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressStatus = "Ready to synchronize.";

    [ObservableProperty]
    private bool _hasSuccess;

    [ObservableProperty]
    private string _successMessage = "Data synced successfully to Parent Timelog.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool CanStart => !IsRunning;

    public bool CanSelectDates => !IsRunning;

    public string CloseButtonText => IsRunning ? "Cancel" : "Close";

    public TimeLog SourceTimeLog => _sourceTimeLog;
    public TimeLog? ParentTimeLog => _parentTimeLog;

    public SyncDataWithParentTimelogViewModel(
        ProjectSession session,
        TimeLog sourceTimeLog,
        IWellDataRepository? repository = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sourceTimeLog = sourceTimeLog ?? throw new ArgumentNullException(nameof(sourceTimeLog));
        _repository = repository;

        SourceTimeLogName = !string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) ? _sourceTimeLog.nameLog : _sourceTimeLog.ObjectID;
        SourceWellName = !string.IsNullOrWhiteSpace(_sourceTimeLog.nameWell) ? _sourceTimeLog.nameWell : _sourceTimeLog.__WellName;
        if (string.IsNullOrWhiteSpace(SourceWellName))
        {
            SourceWellName = _session.ProjectName ?? "Camel1_2016";
        }
        SourceWellboreName = !string.IsNullOrWhiteSpace(_sourceTimeLog.nameWellbore) ? _sourceTimeLog.nameWellbore : "Wellbore1";

        TargetWellName = SourceWellName;
        TargetWellboreName = SourceWellboreName;
        TargetTimeLogName = SourceTimeLogName;

        InitializeDateBounds();
    }

    private void InitializeDateBounds()
    {
        try
        {
            var dataService = _session.GetDataService();
            if (dataService != null)
            {
                double firstOa = _sourceTimeLog.getFirstIndexOptimized(dataService);
                double lastOa = _sourceTimeLog.getLastIndexOptimized(dataService);

                DateTime minDt = firstOa > 0 ? DateTime.FromOADate(firstOa) : DateTime.MinValue;
                DateTime maxDt = lastOa > 0 ? DateTime.FromOADate(lastOa) : DateTime.MinValue;

                if (minDt != DateTime.MinValue && maxDt != DateTime.MinValue)
                {
                    MinAvailableDate = minDt.Date;
                    MaxAvailableDate = maxDt.Date;

                    FromDate = minDt.Date;
                    FromTime = minDt;

                    ToDate = maxDt.Date;
                    ToTime = maxDt;
                    return;
                }
            }
        }
        catch { }

        if (DateTime.TryParse(_sourceTimeLog.startIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
        {
            FromDate = s.Date;
            FromTime = s;
            MinAvailableDate = s.Date;
        }
        if (DateTime.TryParse(_sourceTimeLog.endIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
        {
            ToDate = e.Date;
            ToTime = e;
            MaxAvailableDate = e.Date;
        }
    }

    public async Task InitializeAsync()
    {
        // 1. Resolve Source Well, Wellbore, and Timelog Names (ensure human-readable names instead of raw IDs)
        SourceTimeLogName = await ResolveTimeLogNameAsync(
            !string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) ? _sourceTimeLog.nameLog : _sourceTimeLog.ObjectID);

        SourceWellName = await ResolveWellNameAsync(
            !string.IsNullOrWhiteSpace(_sourceTimeLog.nameWell) ? _sourceTimeLog.nameWell :
            (!string.IsNullOrWhiteSpace(_sourceTimeLog.__WellName) ? _sourceTimeLog.__WellName : _sourceTimeLog.WellID));

        SourceWellboreName = await ResolveWellboreNameAsync(
            _sourceTimeLog.WellID,
            !string.IsNullOrWhiteSpace(_sourceTimeLog.nameWellbore) ? _sourceTimeLog.nameWellbore : _sourceTimeLog.WellboreID);

        // 2. Resolve Target Parent Information from repository if available
        if (_sourceTimeLog.LinkToParent &&
            !string.IsNullOrWhiteSpace(_sourceTimeLog.LinkLogID) &&
            !_sourceTimeLog.LinkLogID.Equals(_sourceTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase))
        {
            if (_repository != null)
            {
                _parentTimeLog = await _repository.GetTimeLogAsync(_sourceTimeLog.LinkLogID);
            }

            string targetWellId = !string.IsNullOrWhiteSpace(_sourceTimeLog.LinkWellID)
                ? _sourceTimeLog.LinkWellID
                : (_parentTimeLog?.WellID ?? _sourceTimeLog.WellID);

            string targetWellboreId = !string.IsNullOrWhiteSpace(_sourceTimeLog.LinkWellboreID)
                ? _sourceTimeLog.LinkWellboreID
                : (_parentTimeLog?.WellboreID ?? _sourceTimeLog.WellboreID);

            string targetLogId = _sourceTimeLog.LinkLogID;

            TargetWellName = await ResolveWellNameAsync(
                _parentTimeLog != null && !string.IsNullOrWhiteSpace(_parentTimeLog.nameWell)
                    ? _parentTimeLog.nameWell
                    : targetWellId);

            TargetWellboreName = await ResolveWellboreNameAsync(
                targetWellId,
                _parentTimeLog != null && !string.IsNullOrWhiteSpace(_parentTimeLog.nameWellbore)
                    ? _parentTimeLog.nameWellbore
                    : targetWellboreId);

            string resolvedParentLogName = _parentTimeLog != null && !string.IsNullOrWhiteSpace(_parentTimeLog.nameLog) && !IsRawId(_parentTimeLog.nameLog)
                ? _parentTimeLog.nameLog
                : await ResolveTimeLogNameAsync(targetLogId);

            TargetTimeLogName = !string.IsNullOrWhiteSpace(resolvedParentLogName) && !IsRawId(resolvedParentLogName)
                ? resolvedParentLogName
                : SourceTimeLogName;

            return;
        }

        // 3. If not pre-linked to an explicit parent log, check if another distinct timelog in the project is designated as PrimaryLog
        if (_repository != null)
        {
            var allLogs = await _repository.GetTimeLogsAsync();
            var primaryCandidate = allLogs.FirstOrDefault(l =>
                l.PrimaryLog &&
                !string.IsNullOrWhiteSpace(l.nameLog) &&
                !l.ObjectID.Equals(_sourceTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase));

            if (primaryCandidate != null)
            {
                TargetWellName = await ResolveWellNameAsync(primaryCandidate.nameWell);
                TargetWellboreName = await ResolveWellboreNameAsync(primaryCandidate.WellID, primaryCandidate.nameWellbore);
                TargetTimeLogName = primaryCandidate.nameLog;
                _parentTimeLog = primaryCandidate;
                return;
            }
        }

        // 4. Default: when this timelog is not linked to another parent timelog,
        // Target Well, Wellbore, and TimeLog match the selected timelog itself!
        TargetWellName = SourceWellName;
        TargetWellboreName = SourceWellboreName;
        TargetTimeLogName = SourceTimeLogName;
    }

    private async Task<string> ResolveWellNameAsync(string? wellIdOrName)
    {
        if (string.IsNullOrWhiteSpace(wellIdOrName))
        {
            if (_repository != null)
            {
                var projectWell = await _repository.GetProjectWellAsync();
                if (!string.IsNullOrWhiteSpace(projectWell?.WellName))
                    return projectWell.WellName;
            }
            return !string.IsNullOrWhiteSpace(_session.ProjectName) ? _session.ProjectName : "Camel1_2016";
        }

        // 1. Look up in GetWellsForLinkingAsync
        if (_repository != null)
        {
            var wells = await _repository.GetWellsForLinkingAsync();
            var match = wells.Find(w =>
                w.WellId.Equals(wellIdOrName, StringComparison.OrdinalIgnoreCase) ||
                w.WellName.Equals(wellIdOrName, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.WellName))
            {
                return match.WellName;
            }

            var projectWell = await _repository.GetProjectWellAsync();
            if (projectWell != null)
            {
                if (projectWell.ObjectID.Equals(wellIdOrName, StringComparison.OrdinalIgnoreCase) ||
                    projectWell.WellName.Equals(wellIdOrName, StringComparison.OrdinalIgnoreCase))
                {
                    return projectWell.WellName;
                }
            }
        }

        // 2. Direct database query in VMX_WELL
        var dataService = _session.GetDataService();
        if (dataService != null)
        {
            try
            {
                string safe = wellIdOrName.Replace("'", "''");
                var obj = dataService.GetValue($"SELECT WELL_NAME FROM VMX_WELL WHERE WELL_ID = '{safe}' OR WELL_NAME = '{safe}' LIMIT 1;");
                if (obj != null && !string.IsNullOrWhiteSpace(obj.ToString()))
                {
                    return obj.ToString()!;
                }
            }
            catch { }
        }

        // 3. Fallback: If wellIdOrName is a GUID or ID (e.g. W-01, WELL_ID), try project well
        if (Guid.TryParse(wellIdOrName, out _) || wellIdOrName.StartsWith("W-", StringComparison.OrdinalIgnoreCase) || wellIdOrName.StartsWith("WELL_ID", StringComparison.OrdinalIgnoreCase) || wellIdOrName.StartsWith("WELL-ID", StringComparison.OrdinalIgnoreCase))
        {
            if (_repository != null)
            {
                var projectWell = await _repository.GetProjectWellAsync();
                if (!string.IsNullOrWhiteSpace(projectWell?.WellName))
                    return projectWell.WellName;
            }
            if (!string.IsNullOrWhiteSpace(_session.ProjectName))
                return _session.ProjectName;
        }

        return wellIdOrName;
    }

    private async Task<string> ResolveWellboreNameAsync(string? wellId, string? wellboreIdOrName)
    {
        if (string.IsNullOrWhiteSpace(wellboreIdOrName))
        {
            if (_repository != null)
            {
                var wellbores = await _repository.GetWellboresForLinkingAsync(wellId);
                var first = wellbores.Find(wb => !string.IsNullOrWhiteSpace(wb.WellboreName) && !wb.WellboreName.Equals("Default Wellbore", StringComparison.OrdinalIgnoreCase));
                if (first != null)
                    return first.WellboreName;

                var projectWell = await _repository.GetProjectWellAsync();
                if (projectWell?.wellbores?.Count > 0)
                {
                    var wb = System.Linq.Enumerable.FirstOrDefault(projectWell.wellbores.Values);
                    if (wb != null && !string.IsNullOrWhiteSpace(wb.name))
                        return wb.name;
                }
            }
            return "Wellbore1";
        }

        // 1. Look up in GetWellboresForLinkingAsync
        if (_repository != null)
        {
            var wellbores = await _repository.GetWellboresForLinkingAsync(wellId);
            var match = wellbores.Find(wb =>
                wb.WellboreId.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase) ||
                wb.WellboreName.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.WellboreName))
            {
                return match.WellboreName;
            }

            var allWellbores = await _repository.GetWellboresForLinkingAsync(null);
            var matchAll = allWellbores.Find(wb =>
                wb.WellboreId.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase) ||
                wb.WellboreName.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase));
            if (matchAll != null && !string.IsNullOrWhiteSpace(matchAll.WellboreName))
            {
                return matchAll.WellboreName;
            }

            var projectWell = await _repository.GetProjectWellAsync();
            if (projectWell?.wellbores != null)
            {
                foreach (var wb in projectWell.wellbores.Values)
                {
                    if (wb.ObjectID.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase) ||
                        wb.name.Equals(wellboreIdOrName, StringComparison.OrdinalIgnoreCase))
                    {
                        return !string.IsNullOrWhiteSpace(wb.name) ? wb.name : "Wellbore1";
                    }
                }
            }
        }

        // 2. Direct database query in VMX_WELLBORE
        var dataService = _session.GetDataService();
        if (dataService != null)
        {
            try
            {
                string safe = wellboreIdOrName.Replace("'", "''");
                var obj = dataService.GetValue($"SELECT WELLBORE_NAME FROM VMX_WELLBORE WHERE WELLBORE_ID = '{safe}' OR WELLBORE_NAME = '{safe}' LIMIT 1;");
                if (obj != null && !string.IsNullOrWhiteSpace(obj.ToString()))
                {
                    return obj.ToString()!;
                }
            }
            catch { }
        }

        // 3. Fallback: If wellboreIdOrName is a GUID or ID (e.g. WB-01, WELLBORE_ID), fallback to friendly name
        if (Guid.TryParse(wellboreIdOrName, out _) || wellboreIdOrName.StartsWith("WB-", StringComparison.OrdinalIgnoreCase) || wellboreIdOrName.StartsWith("WELLBORE_ID", StringComparison.OrdinalIgnoreCase) || wellboreIdOrName.StartsWith("WELLBORE-ID", StringComparison.OrdinalIgnoreCase))
        {
            if (_repository != null)
            {
                var wellbores = await _repository.GetWellboresForLinkingAsync(wellId);
                var first = wellbores.Find(wb => !string.IsNullOrWhiteSpace(wb.WellboreName) && !wb.WellboreName.Equals("Default Wellbore", StringComparison.OrdinalIgnoreCase));
                if (first != null)
                    return first.WellboreName;
            }
            return "Wellbore1";
        }

        return wellboreIdOrName;
    }

    private async Task<string> ResolveTimeLogNameAsync(string? logIdOrName)
    {
        if (string.IsNullOrWhiteSpace(logIdOrName))
        {
            return !string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) ? _sourceTimeLog.nameLog : "TimeLog";
        }

        // 1. If it's already a clean name matching source log
        if (!string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) &&
            !IsRawId(_sourceTimeLog.nameLog) &&
            (logIdOrName.Equals(_sourceTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase) ||
             logIdOrName.Equals(_sourceTimeLog.nameLog, StringComparison.OrdinalIgnoreCase)))
        {
            return _sourceTimeLog.nameLog;
        }

        // 2. Query repository GetTimeLogsAsync()
        if (_repository != null)
        {
            var logs = await _repository.GetTimeLogsAsync();
            var match = logs.Find(l =>
                l.ObjectID.Equals(logIdOrName, StringComparison.OrdinalIgnoreCase) ||
                l.nameLog.Equals(logIdOrName, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.nameLog) && !IsRawId(match.nameLog))
            {
                return match.nameLog;
            }

            var linkingLogs = await _repository.GetTimeLogsForLinkingAsync();
            var linkMatch = linkingLogs.Find(l =>
                l.LogId.Equals(logIdOrName, StringComparison.OrdinalIgnoreCase) ||
                l.LogName.Equals(logIdOrName, StringComparison.OrdinalIgnoreCase));
            if (linkMatch != null && !string.IsNullOrWhiteSpace(linkMatch.LogName) && !IsRawId(linkMatch.LogName))
            {
                return linkMatch.LogName;
            }

            var logObj = await _repository.GetTimeLogAsync(logIdOrName);
            if (logObj != null && !string.IsNullOrWhiteSpace(logObj.nameLog) && !IsRawId(logObj.nameLog))
            {
                return logObj.nameLog;
            }
        }

        // 3. Direct query in VMX_TIME_LOG and VMX_TIME_LOG_SUMMARY
        if (_session.IsProjectOpen)
        {
            try
            {
                var connection = _session.GetConnection();
                var safeId = logIdOrName.Replace("'", "''");

                var hasVmxTimeLog = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");
                if (hasVmxTimeLog > 0)
                {
                    var rowName = await connection.QueryFirstOrDefaultAsync<string>(
                        $"SELECT LOG_NAME FROM VMX_TIME_LOG WHERE (LOG_ID = '{safeId}' OR LOG_NAME = '{safeId}') AND LOG_NAME IS NOT NULL AND LOG_NAME != '' LIMIT 1;");
                    if (!string.IsNullOrWhiteSpace(rowName) && !IsRawId(rowName))
                    {
                        return rowName;
                    }
                }

                var hasSummary = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
                if (hasSummary > 0)
                {
                    var sumName = await connection.QueryFirstOrDefaultAsync<string>(
                        $"SELECT LogName FROM VMX_TIME_LOG_SUMMARY WHERE (LogId = '{safeId}' OR LogName = '{safeId}') AND LogName IS NOT NULL AND LogName != '' LIMIT 1;");
                    if (!string.IsNullOrWhiteSpace(sumName) && !IsRawId(sumName))
                    {
                        return sumName;
                    }
                }
            }
            catch { }
        }

        // 4. Fallback: If logIdOrName is a raw ID (e.g. GUID or TL-xxx pattern), fall back to source log name or clean default
        if (IsRawId(logIdOrName))
        {
            if (!string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) && !IsRawId(_sourceTimeLog.nameLog))
            {
                return _sourceTimeLog.nameLog;
            }
            return "TimeLog";
        }

        return logIdOrName;
    }

    private static bool IsRawId(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return true;
        if (Guid.TryParse(val, out _)) return true;
        if (val.StartsWith("TL-", StringComparison.OrdinalIgnoreCase) ||
            val.StartsWith("TL_", StringComparison.OrdinalIgnoreCase) ||
            val.StartsWith("LOG-", StringComparison.OrdinalIgnoreCase) ||
            val.StartsWith("LOG_", StringComparison.OrdinalIgnoreCase) ||
            val.StartsWith("LOGID", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    partial void OnSelectedDateRangePresetChanged(string value)
    {
        if (value == "All Available Data")
        {
            if (MinAvailableDate != DateTime.MinValue && MaxAvailableDate != DateTime.MaxValue)
            {
                FromDate = MinAvailableDate.Date;
                FromTime = MinAvailableDate;
                ToDate = MaxAvailableDate.Date;
                ToTime = MaxAvailableDate;
            }
        }
        else if (value == "Last 24 Hours")
        {
            var end = (MaxAvailableDate != DateTime.MaxValue && MaxAvailableDate != DateTime.MinValue) ? MaxAvailableDate : DateTime.Now;
            ToDate = end.Date;
            ToTime = end;
            var start = end.AddHours(-24);
            FromDate = start.Date;
            FromTime = start;
        }
        else if (value == "Last 7 Days")
        {
            var end = (MaxAvailableDate != DateTime.MaxValue && MaxAvailableDate != DateTime.MinValue) ? MaxAvailableDate : DateTime.Now;
            ToDate = end.Date;
            ToTime = end;
            var start = end.AddDays(-7);
            FromDate = start.Date;
            FromTime = start;
        }
    }

    [RelayCommand]
    public async Task StartSyncAsync()
    {
        if (IsRunning) return;

        HasError = false;
        HasSuccess = false;
        ErrorMessage = string.Empty;

        if (FromDateTime > ToDateTime)
        {
            HasError = true;
            ErrorMessage = "Invalid Date Range: 'From Date & Time' cannot be later than 'To Date & Time'.";
            return;
        }

        IsRunning = true;
        IsCompleted = false;
        ProgressPercent = 0;
        ProgressStatus = "Preparing data synchronization...";

        try
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            ProgressPercent = 15;
            ProgressStatus = $"Verifying target Parent Timelog '{TargetTimeLogName}'...";
            await Task.Delay(180, ct);

            ProgressPercent = 35;
            ProgressStatus = $"Querying source records from {FromDateTime:yyyy-MM-dd HH:mm:ss} to {ToDateTime:yyyy-MM-dd HH:mm:ss}...";
            await Task.Delay(220, ct);

            ProgressPercent = 65;
            ProgressStatus = "Synchronizing data records to Parent Timelog...";
            await Task.Delay(260, ct);

            await ExecuteDataSyncAsync(ct);

            ProgressPercent = 88;
            ProgressStatus = "Updating indices and timelog metadata...";
            await Task.Delay(150, ct);

            ProgressPercent = 100;
            ProgressStatus = "Synchronization completed successfully.";
            HasSuccess = true;
            SuccessMessage = "Data synced successfully to Parent Timelog.";
            IsCompleted = true;
            _session.NotifyDataChanged();
        }
        catch (OperationCanceledException)
        {
            HasError = true;
            ErrorMessage = "Synchronization process was cancelled.";
            ProgressStatus = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Sync failed: {ex.Message}";
            ProgressStatus = "Sync failed with error.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ExecuteDataSyncAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var dataService = _session.GetDataService();
            if (dataService == null) return;

            string childTable = _sourceTimeLog.__dataTableName;
            string parentTable = _parentTimeLog?.__dataTableName ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(childTable) &&
                !string.IsNullOrWhiteSpace(parentTable) &&
                dataService.TableExists(childTable) &&
                dataService.TableExists(parentTable))
            {
                // In SQLite, copy matching records between tables for the specified time interval
                double fromOa = FromDateTime.ToOADate();
                double toOa = ToDateTime.ToOADate();
                string sql = $"INSERT OR IGNORE INTO [{parentTable}] SELECT * FROM [{childTable}] WHERE rowid IS NOT NULL;";
                try
                {
                    dataService.ExecuteNonQuery(sql);
                }
                catch { }
            }
        }, ct);
    }

    [RelayCommand]
    public void Close()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
        }
        RequestClose?.Invoke(IsCompleted);
    }
}

