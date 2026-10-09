using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class SyncDataWithParentTimelogViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly TimeLog _sourceTimeLog;
    private readonly IWellDataRepository? _repository;
    private TimeLog? _parentTimeLog;
    private CancellationTokenSource? _cts;
    private readonly Stopwatch _stopwatch = new();

    public event Action<bool>? RequestClose;

    // Source Information
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTimelogNodeName))]
    [NotifyPropertyChangedFor(nameof(ConfirmationMessage))]
    private string _sourceTimeLogName = string.Empty;

    public string SelectedTimelogNodeName => SourceTimeLogName;

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
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTimelog))]
    [NotifyPropertyChangedFor(nameof(ConfirmationMessage))]
    private string _targetTimeLogName = "TimeLog";

    public ObservableCollection<TimeLogOption> AvailableParentTimeLogs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTimelog))]
    [NotifyPropertyChangedFor(nameof(ConfirmationMessage))]
    private TimeLogOption? _selectedParentTimeLog;

    public Task? CurrentTargetUpdateTask { get; private set; }

    // (3) Confirmation Message
    public string ConfirmationMessage =>
        $"Data from selected Timelog '{SourceTimeLogName}' will be synchronized into Parent Timelog '{TargetTimeLogName}' for the date range {FromDateTimeText} to {ToDateTimeText}.";

    partial void OnSelectedParentTimeLogChanged(TimeLogOption? value)
    {
        if (value != null)
        {
            TargetTimeLogName = value.LogName;
            CurrentTargetUpdateTask = UpdateTargetDetailsAsync(value);
        }
        else
        {
            TargetTimeLogName = string.Empty;
            _parentTimeLog = null;
        }
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(HasSelectedTimelog));
        OnPropertyChanged(nameof(ConfirmationMessage));
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    partial void OnTargetTimeLogNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(HasSelectedTimelog));
        OnPropertyChanged(nameof(ConfirmationMessage));
        StartSyncCommand.NotifyCanExecuteChanged();
    }

    public async Task SelectParentTimeLogAsync(TimeLogOption? option)
    {
        SelectedParentTimeLog = option;
        if (CurrentTargetUpdateTask != null)
        {
            await CurrentTargetUpdateTask;
        }
    }

    private async Task UpdateTargetDetailsAsync(TimeLogOption option)
    {
        if (!string.IsNullOrWhiteSpace(option.WellId))
        {
            var well = await ResolveWellNameAsync(option.WellId);
            if (!string.IsNullOrWhiteSpace(well))
            {
                TargetWellName = well;
            }
        }
        if (!string.IsNullOrWhiteSpace(option.WellboreId))
        {
            var wb = await ResolveWellboreNameAsync(option.WellId, option.WellboreId);
            if (!string.IsNullOrWhiteSpace(wb))
            {
                TargetWellboreName = wb;
            }
        }
        if (_repository != null && !string.IsNullOrWhiteSpace(option.LogId))
        {
            _parentTimeLog = await _repository.GetTimeLogAsync(option.LogId);
        }
    }

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
            var dt = date.Add(time);
            if (FromDate.Kind == DateTimeKind.Utc || FromTime?.Kind == DateTimeKind.Utc || DataStartDateTime?.Kind == DateTimeKind.Utc)
            {
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return dt;
        }
        set
        {
            if (FromDateTime != value)
            {
                FromDate = DateTime.SpecifyKind(value.Date, value.Kind);
                FromTime = value;
                if (DataStartDateTime.HasValue)
                {
                    DataStartDateTime = DateTime.SpecifyKind(DataStartDateTime.Value, value.Kind);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(FromDateTimeText));
                OnPropertyChanged(nameof(ConfirmationMessage));
            }
        }
    }

    public DateTime ToDateTime
    {
        get
        {
            var date = ToDate.Date;
            var time = ToTime?.TimeOfDay ?? new TimeSpan(23, 59, 59);
            var dt = date.Add(time);
            if (ToDate.Kind == DateTimeKind.Utc || ToTime?.Kind == DateTimeKind.Utc || DataEndDateTime?.Kind == DateTimeKind.Utc)
            {
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return dt;
        }
        set
        {
            if (ToDateTime != value)
            {
                ToDate = DateTime.SpecifyKind(value.Date, value.Kind);
                ToTime = value;
                if (DataEndDateTime.HasValue)
                {
                    DataEndDateTime = DateTime.SpecifyKind(DataEndDateTime.Value, value.Kind);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToDateTimeText));
                OnPropertyChanged(nameof(ConfirmationMessage));
            }
        }
    }

    public string FromDateTimeText
    {
        get => FromDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseDateTime(value, out var dt))
            {
                FromDateTime = dt;
            }
        }
    }

    public string ToDateTimeText
    {
        get => ToDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        set
        {
            if (TryParseDateTime(value, out var dt))
            {
                ToDateTime = dt;
            }
        }
    }

    partial void OnFromDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FromDateTime));
        OnPropertyChanged(nameof(FromDateTimeText));
    }

    partial void OnFromTimeChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(FromDateTime));
        OnPropertyChanged(nameof(FromDateTimeText));
    }

    partial void OnToDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(ToDateTime));
        OnPropertyChanged(nameof(ToDateTimeText));
    }

    partial void OnToTimeChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(ToDateTime));
        OnPropertyChanged(nameof(ToDateTimeText));
    }

    public static bool TryParseDateTime(string? text, out DateTime result) =>
        DrillIntel.Controls.DateTimePicker.TryParseDateTime(text, out result);

    [ObservableProperty]
    private DateTime _minAvailableDate = DateTime.MinValue;

    [ObservableProperty]
    private DateTime _maxAvailableDate = DateTime.MaxValue;

    [ObservableProperty]
    private DateTime? _dataStartDateTime;

    [ObservableProperty]
    private DateTime? _dataEndDateTime;

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
    private string _elapsedTimeText = string.Empty;

    [ObservableProperty]
    private bool _hasSuccess;

    [ObservableProperty]
    private string _successMessage = "Data synced successfully to Parent Timelog.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool HasSelectedTimelog =>
        SelectedParentTimeLog != null ||
        _parentTimeLog != null ||
        (!string.IsNullOrWhiteSpace(TargetTimeLogName) &&
         !TargetTimeLogName.Equals("(None)", StringComparison.OrdinalIgnoreCase) &&
         !TargetTimeLogName.Equals("(Select Timelog)", StringComparison.OrdinalIgnoreCase));

    public bool CanStart => !IsRunning && HasSelectedTimelog;

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
                string tableName = _sourceTimeLog.__dataTableName;
                if (!string.IsNullOrWhiteSpace(tableName) && dataService.TableExists(tableName))
                {
                    var dtMin = RigStateService.GetMinDateFromTable(dataService, tableName);
                    var dtMax = RigStateService.GetMaxDateFromTable(dataService, tableName);
                    if (dtMin != DateTime.MinValue && dtMax != DateTime.MinValue)
                    {
                        string wellDateFormat = _sourceTimeLog.__wellDateFormat;
                        if (string.IsNullOrWhiteSpace(wellDateFormat) && !string.IsNullOrWhiteSpace(_sourceTimeLog.WellID))
                        {
                            wellDateFormat = Well.getWellDateFormat(dataService, _sourceTimeLog.WellID);
                        }
                        if (string.Equals(wellDateFormat, Well.wDateFormatUTC, StringComparison.OrdinalIgnoreCase))
                        {
                            if (dtMin.Kind != DateTimeKind.Utc) dtMin = DateTime.SpecifyKind(dtMin, DateTimeKind.Utc);
                            if (dtMax.Kind != DateTimeKind.Utc) dtMax = DateTime.SpecifyKind(dtMax, DateTimeKind.Utc);
                        }

                        DataStartDateTime = dtMin;
                        DataEndDateTime = dtMax;
                        MinAvailableDate = dtMin.Date;
                        MaxAvailableDate = dtMax.Date;

                        FromDate = DateTime.SpecifyKind(dtMin.Date, dtMin.Kind);
                        FromTime = dtMin;

                        ToDate = DateTime.SpecifyKind(dtMax.Date, dtMax.Kind);
                        ToTime = dtMax;
                        return;
                    }
                }

                double firstOa = _sourceTimeLog.getFirstIndexOptimized(dataService);
                double lastOa = _sourceTimeLog.getLastIndexOptimized(dataService);

                DateTime minDt = firstOa > 0 ? DateTime.FromOADate(firstOa) : DateTime.MinValue;
                DateTime maxDt = lastOa > 0 ? DateTime.FromOADate(lastOa) : DateTime.MinValue;

                if (minDt != DateTime.MinValue && maxDt != DateTime.MinValue)
                {
                    string wellDateFormat = _sourceTimeLog.__wellDateFormat;
                    if (string.IsNullOrWhiteSpace(wellDateFormat) && !string.IsNullOrWhiteSpace(_sourceTimeLog.WellID))
                    {
                        wellDateFormat = Well.getWellDateFormat(dataService, _sourceTimeLog.WellID);
                    }
                    if (string.Equals(wellDateFormat, Well.wDateFormatUTC, StringComparison.OrdinalIgnoreCase))
                    {
                        if (minDt.Kind != DateTimeKind.Utc) minDt = DateTime.SpecifyKind(minDt, DateTimeKind.Utc);
                        if (maxDt.Kind != DateTimeKind.Utc) maxDt = DateTime.SpecifyKind(maxDt, DateTimeKind.Utc);
                    }

                    DataStartDateTime = minDt;
                    DataEndDateTime = maxDt;
                    MinAvailableDate = minDt.Date;
                    MaxAvailableDate = maxDt.Date;

                    FromDate = DateTime.SpecifyKind(minDt.Date, minDt.Kind);
                    FromTime = minDt;

                    ToDate = DateTime.SpecifyKind(maxDt.Date, maxDt.Kind);
                    ToTime = maxDt;
                    return;
                }
            }
        }
        catch { }

        if (DateTime.TryParse(_sourceTimeLog.startIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
        {
            DataStartDateTime = s;
            FromDate = s.Date;
            FromTime = s;
            MinAvailableDate = s.Date;
        }
        if (DateTime.TryParse(_sourceTimeLog.endIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
        {
            DataEndDateTime = e;
            ToDate = e.Date;
            ToTime = e;
            MaxAvailableDate = e.Date;
        }
    }

    public async Task SetDateBoundsFromDataAsync()
    {
        await SetDateBoundsForTimelogAsync(
            !string.IsNullOrWhiteSpace(_sourceTimeLog.ObjectID) ? _sourceTimeLog.ObjectID : _sourceTimeLog.nameLog,
            _sourceTimeLog.nameLog);
    }

    public async Task SetDateBoundsForTimelogAsync(string? logId, string? logName = null)
    {
        try
        {
            DateTime minDt = DateTime.MinValue;
            DateTime maxDt = DateTime.MinValue;

            var dataService = _session.GetDataService();
            var connection = _session.IsProjectOpen ? _session.GetConnection() : null;

            string targetId = !string.IsNullOrWhiteSpace(logId) ? logId : (logName ?? string.Empty);
            string targetName = !string.IsNullOrWhiteSpace(logName) ? logName : targetId;
            string safeLogId = targetId.Replace("'", "''");
            string safeLogName = targetName.Replace("'", "''");

            // Locate in-memory TimeLog if available
            TimeLog? targetLog = null;
            if (_parentTimeLog != null && 
                (_parentTimeLog.ObjectID.Equals(targetId, StringComparison.OrdinalIgnoreCase) || 
                 _parentTimeLog.nameLog.Equals(targetName, StringComparison.OrdinalIgnoreCase)))
            {
                targetLog = _parentTimeLog;
            }
            else if (_sourceTimeLog != null && 
                (_sourceTimeLog.ObjectID.Equals(targetId, StringComparison.OrdinalIgnoreCase) || 
                 _sourceTimeLog.nameLog.Equals(targetName, StringComparison.OrdinalIgnoreCase)))
            {
                targetLog = _sourceTimeLog;
            }
            else if (_repository != null && !string.IsNullOrWhiteSpace(targetId))
            {
                try
                {
                    targetLog = await _repository.GetTimeLogAsync(targetId);
                    if (targetLog == null && !string.IsNullOrWhiteSpace(targetName))
                    {
                        var allLogs = await _repository.GetTimeLogsAsync();
                        targetLog = allLogs.FirstOrDefault(l => 
                            l.ObjectID.Equals(targetId, StringComparison.OrdinalIgnoreCase) || 
                            l.nameLog.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                    }
                }
                catch { }
            }

            // 1. Identify Physical Data Table
            string tableName = targetLog?.__dataTableName ?? string.Empty;
            string resolvedWellId = targetLog?.WellID ?? string.Empty;
            string resolvedWellboreId = targetLog?.WellboreID ?? string.Empty;

            if (string.IsNullOrWhiteSpace(tableName) && connection != null)
            {
                try
                {
                    var hasVmxTimeLog = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");
                    if (hasVmxTimeLog > 0)
                    {
                        var resolvedRow = await connection.QueryFirstOrDefaultAsync<VmxTimeLogMeta>(
                            $"SELECT WELL_ID, WELLBORE_ID, DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE (LOG_ID = '{safeLogId}' OR LOG_NAME = '{safeLogName}' OR LOG_NAME = '{safeLogId}' OR LOG_ID = '{safeLogName}') LIMIT 1;");
                        if (resolvedRow != null)
                        {
                            string resolvedTable = resolvedRow.DATA_TABLE_NAME ?? "";
                            if (!string.IsNullOrWhiteSpace(resolvedTable))
                            {
                                tableName = resolvedTable;
                                if (targetLog != null) targetLog.__dataTableName = resolvedTable;
                            }
                            if (string.IsNullOrWhiteSpace(resolvedWellId) && !string.IsNullOrWhiteSpace(resolvedRow.WELL_ID))
                            {
                                resolvedWellId = resolvedRow.WELL_ID;
                            }
                            if (string.IsNullOrWhiteSpace(resolvedWellboreId) && !string.IsNullOrWhiteSpace(resolvedRow.WELLBORE_ID))
                            {
                                resolvedWellboreId = resolvedRow.WELLBORE_ID;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        var hasSummary = await connection.ExecuteScalarAsync<int>(
                            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
                        if (hasSummary > 0)
                        {
                            var resolvedSummaryTable = await connection.QueryFirstOrDefaultAsync<string>(
                                $"SELECT TableName FROM VMX_TIME_LOG_SUMMARY WHERE (LogId = '{safeLogId}' OR LogName = '{safeLogName}' OR LogName = '{safeLogId}' OR LogId = '{safeLogName}') AND TableName IS NOT NULL AND TableName != '' LIMIT 1;");
                            if (!string.IsNullOrWhiteSpace(resolvedSummaryTable))
                            {
                                tableName = resolvedSummaryTable;
                                if (targetLog != null) targetLog.__dataTableName = resolvedSummaryTable;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        var directTable = await connection.QueryFirstOrDefaultAsync<string>(
                            $"SELECT name FROM sqlite_master WHERE type='table' AND (name='{safeLogId}' OR name='{safeLogName}' OR name='TL_{safeLogId}' OR name='TL_{safeLogName}' OR name='TL_{safeLogId}_DATA' OR name='TL_{safeLogName}_DATA') LIMIT 1;");
                        if (!string.IsNullOrWhiteSpace(directTable))
                        {
                            tableName = directTable;
                            if (targetLog != null) targetLog.__dataTableName = directTable;
                        }
                    }
                }
                catch { }
            }

            // 2. Query physical SQLite data table rows if available
            if (!string.IsNullOrWhiteSpace(tableName) && connection != null)
            {
                try
                {
                    var tableExists = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;",
                        new { name = tableName });

                    if (tableExists > 0)
                    {
                        var colNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            var cols = await connection.QueryAsync<string>($"SELECT name FROM pragma_table_info('{tableName.Replace("'", "''")}');");
                            foreach (var c in cols) if (!string.IsNullOrWhiteSpace(c)) colNames.Add(c);
                        }
                        catch { }

                        if (colNames.Count == 0)
                        {
                            try
                            {
                                var colRows = await connection.QueryAsync<dynamic>($"PRAGMA table_info([{tableName.Replace("]", "]]")}]);");
                                foreach (var r in colRows)
                                {
                                    if (r is IDictionary<string, object> dict && dict.TryGetValue("name", out var n) && n != null)
                                    {
                                        colNames.Add(n.ToString()!);
                                    }
                                }
                            }
                            catch { }
                        }

                        if (colNames.Contains("DATETIME"))
                        {
                            string orderCol = colNames.Contains("INDEX_DOUBLE") ? "INDEX_DOUBLE" : "rowid";
                            var firstVal = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT DATETIME FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '' ORDER BY [{orderCol}] ASC LIMIT 1;");
                            var lastVal = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT DATETIME FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '' ORDER BY [{orderCol}] DESC LIMIT 1;");

                            if (firstVal != null) minDt = ParseRowDateTime(firstVal);
                            if (lastVal != null) maxDt = ParseRowDateTime(lastVal);

                            if (minDt == DateTime.MinValue || maxDt == DateTime.MinValue)
                            {
                                var minAgg = await connection.QueryFirstOrDefaultAsync<object>(
                                    $"SELECT MIN(DATETIME) FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '';");
                                var maxAgg = await connection.QueryFirstOrDefaultAsync<object>(
                                    $"SELECT MAX(DATETIME) FROM [{tableName}] WHERE DATETIME IS NOT NULL AND DATETIME != '';");
                                if (minDt == DateTime.MinValue && minAgg != null) minDt = ParseRowDateTime(minAgg);
                                if (maxDt == DateTime.MinValue && maxAgg != null) maxDt = ParseRowDateTime(maxAgg);
                            }
                        }
                        else if (colNames.Contains("DATE") && colNames.Contains("TIME"))
                        {
                            var firstVal = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT (DATE || ' ' || TIME) FROM [{tableName}] WHERE DATE IS NOT NULL AND DATE != '' ORDER BY rowid ASC LIMIT 1;");
                            var lastVal = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT (DATE || ' ' || TIME) FROM [{tableName}] WHERE DATE IS NOT NULL AND DATE != '' ORDER BY rowid DESC LIMIT 1;");

                            if (firstVal != null) minDt = ParseRowDateTime(firstVal);
                            if (lastVal != null) maxDt = ParseRowDateTime(lastVal);
                        }
                        else if (colNames.Contains("INDEX_DOUBLE"))
                        {
                            var minIdx = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT MIN(INDEX_DOUBLE) FROM [{tableName}] WHERE INDEX_DOUBLE IS NOT NULL AND INDEX_DOUBLE > 0;");
                            var maxIdx = await connection.QueryFirstOrDefaultAsync<object>(
                                $"SELECT MAX(INDEX_DOUBLE) FROM [{tableName}] WHERE INDEX_DOUBLE IS NOT NULL AND INDEX_DOUBLE > 0;");
                            if (minIdx != null && double.TryParse(minIdx.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double minOa) && minOa > 0)
                                minDt = DateTime.FromOADate(minOa);
                            if (maxIdx != null && double.TryParse(maxIdx.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double maxOa) && maxOa > 0)
                                maxDt = DateTime.FromOADate(maxOa);
                        }
                    }
                }
                catch { }
            }

            // 3. Fallback to VMX_TIME_LOG metadata (MIN_DATE / MAX_DATE)
            if ((minDt == DateTime.MinValue || maxDt == DateTime.MinValue) && connection != null)
            {
                try
                {
                    var hasVmxTimeLog = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG';");
                    if (hasVmxTimeLog > 0)
                    {
                        var meta = await connection.QueryFirstOrDefaultAsync<VmxTimeLogMeta>(
                            $"SELECT MIN_DATE, MAX_DATE FROM VMX_TIME_LOG WHERE (LOG_ID = '{safeLogId}' OR LOG_NAME = '{safeLogName}' OR LOG_NAME = '{safeLogId}' OR LOG_ID = '{safeLogName}') LIMIT 1;");
                        if (meta != null)
                        {
                            if (minDt == DateTime.MinValue) minDt = ParseRowDateTime(meta.MIN_DATE);
                            if (maxDt == DateTime.MinValue) maxDt = ParseRowDateTime(meta.MAX_DATE);
                        }
                    }
                }
                catch { }
            }

            // 4. Fallback to getFirstIndexOptimized / getLastIndexOptimized
            if ((minDt == DateTime.MinValue || maxDt == DateTime.MinValue) && dataService != null)
            {
                try
                {
                    string effectiveWellId = !string.IsNullOrWhiteSpace(resolvedWellId) ? resolvedWellId : SourceWellName;
                    string effectiveWbId = !string.IsNullOrWhiteSpace(resolvedWellboreId) ? resolvedWellboreId : SourceWellboreName;

                    double firstOa = TimeLogService.getFirstIndexOptimized(dataService, effectiveWellId, effectiveWbId, targetId);
                    double lastOa = TimeLogService.getLastIndexOptimized(dataService, effectiveWellId, effectiveWbId, targetId);

                    if (firstOa > 0 && minDt == DateTime.MinValue) minDt = DateTime.FromOADate(firstOa);
                    if (lastOa > 0 && maxDt == DateTime.MinValue) maxDt = DateTime.FromOADate(lastOa);
                }
                catch { }
            }

            // 5. Fallback to VMX_TIME_LOG_SUMMARY (StartIndex / EndIndex)
            if ((minDt == DateTime.MinValue || maxDt == DateTime.MinValue) && connection != null)
            {
                try
                {
                    var hasSummary = await connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
                    if (hasSummary > 0)
                    {
                        var summary = await connection.QueryFirstOrDefaultAsync<VmxTimeLogSummaryMeta>(
                            $"SELECT StartIndex, EndIndex FROM VMX_TIME_LOG_SUMMARY WHERE (LogId = '{safeLogId}' OR LogName = '{safeLogName}' OR LogName = '{safeLogId}' OR LogId = '{safeLogName}') LIMIT 1;");
                        if (summary != null)
                        {
                            if (minDt == DateTime.MinValue) minDt = ParseRowDateTime(summary.StartIndex);
                            if (maxDt == DateTime.MinValue) maxDt = ParseRowDateTime(summary.EndIndex);
                        }
                    }
                }
                catch { }
            }

            // 6. Fallback to targetLog.startIndex / endIndex strings
            if (minDt == DateTime.MinValue && targetLog != null && !string.IsNullOrWhiteSpace(targetLog.startIndex))
            {
                minDt = ParseRowDateTime(targetLog.startIndex);
            }
            if (maxDt == DateTime.MinValue && targetLog != null && !string.IsNullOrWhiteSpace(targetLog.endIndex))
            {
                maxDt = ParseRowDateTime(targetLog.endIndex);
            }

            // Fallback to source timelog if target timelog has no rows
            if ((minDt == DateTime.MinValue || maxDt == DateTime.MinValue) && targetLog != _sourceTimeLog)
            {
                if (DataStartDateTime.HasValue && DataEndDateTime.HasValue)
                {
                    minDt = DataStartDateTime.Value;
                    maxDt = DataEndDateTime.Value;
                }
            }

            // 7. Time zone handling (if Well is UTC, set Kind = Utc)
            if (minDt != DateTime.MinValue || maxDt != DateTime.MinValue)
            {
                string wellDateFormat = targetLog?.__wellDateFormat ?? string.Empty;
                string wellId = !string.IsNullOrWhiteSpace(resolvedWellId) ? resolvedWellId : SourceWellName;

                if (string.IsNullOrWhiteSpace(wellDateFormat) && dataService != null && !string.IsNullOrWhiteSpace(wellId))
                {
                    try
                    {
                        wellDateFormat = Well.getWellDateFormat(dataService, wellId);
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(wellDateFormat) && _repository != null)
                {
                    try
                    {
                        var wells = await _repository.GetWellsAsync();
                        var well = wells?.FirstOrDefault(w =>
                            w.ObjectID.Equals(wellId, StringComparison.OrdinalIgnoreCase) ||
                            w.name.Equals(wellId, StringComparison.OrdinalIgnoreCase));
                        if (well == null) well = await _repository.GetProjectWellAsync();
                        if (well != null) wellDateFormat = well.wellDateFormat;
                    }
                    catch { }
                }

                if (string.Equals(wellDateFormat, Well.wDateFormatUTC, StringComparison.OrdinalIgnoreCase))
                {
                    if (minDt != DateTime.MinValue && minDt.Kind != DateTimeKind.Utc)
                        minDt = DateTime.SpecifyKind(minDt, DateTimeKind.Utc);
                    if (maxDt != DateTime.MinValue && maxDt.Kind != DateTimeKind.Utc)
                        maxDt = DateTime.SpecifyKind(maxDt, DateTimeKind.Utc);
                }
            }

            // 8. Order check and single bound adjustment
            if (minDt != DateTime.MinValue && maxDt != DateTime.MinValue && minDt > maxDt)
            {
                var temp = minDt;
                minDt = maxDt;
                maxDt = temp;
            }
            else if (minDt != DateTime.MinValue && maxDt == DateTime.MinValue)
            {
                maxDt = minDt;
            }
            else if (maxDt != DateTime.MinValue && minDt == DateTime.MinValue)
            {
                minDt = maxDt.AddDays(-7);
            }

            // 9. Assign properties
            if (minDt != DateTime.MinValue && maxDt != DateTime.MinValue)
            {
                DataStartDateTime = minDt;
                DataEndDateTime = maxDt;

                MinAvailableDate = minDt.Date;
                MaxAvailableDate = maxDt.Date;

                FromDate = DateTime.SpecifyKind(minDt.Date, minDt.Kind);
                FromTime = minDt;

                ToDate = DateTime.SpecifyKind(maxDt.Date, maxDt.Kind);
                ToTime = maxDt;

                SelectedDateRangePreset = "All Available Data";
                OnPropertyChanged(nameof(SelectedDateRangePreset));

                OnPropertyChanged(nameof(FromDateTime));
                OnPropertyChanged(nameof(FromDateTimeText));
                OnPropertyChanged(nameof(ToDateTime));
                OnPropertyChanged(nameof(ToDateTimeText));
                OnPropertyChanged(nameof(ConfirmationMessage));
            }
        }
        catch { }
    }

    public static DateTime ParseRowDateTime(object? val)
    {
        if (val == null || val == DBNull.Value) return DateTime.MinValue;
        if (val is DateTime dt) return dt;
        if (val is double d && d > 1.0 && d < 100000.0)
        {
            try { return DateTime.FromOADate(d); } catch { }
        }
        string str = Convert.ToString(val)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(str)) return DateTime.MinValue;

        string[] formats =
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "d/M/yyyy HH:mm:ss",
            "d/M/yyyy HH:mm",
            "dd-MM-yyyy HH:mm:ss",
            "dd-MM-yyyy HH:mm",
            "dd-MMM-yyyy HH:mm:ss",
            "d-MMM-yyyy HH:mm:ss",
            "dd-MMM-yyyy HH:mm",
            "d-MMM-yyyy HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "dd-MMM-yyyy"
        };

        if (DateTime.TryParseExact(str, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resExact))
            return resExact;

        if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resInv))
            return resInv;
        if (DateTime.TryParse(str, CultureInfo.CurrentCulture, DateTimeStyles.None, out var resCurr))
            return resCurr;

        if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedD) && parsedD > 1.0 && parsedD < 100000.0)
        {
            try { return DateTime.FromOADate(parsedD); } catch { }
        }

        return DateTime.MinValue;
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

        // 2. Automatically populate From & To date bounds from actual source data
        await SetDateBoundsFromDataAsync();

        // 3. Load available Parent TimeLogs for linking dropdown
        AvailableParentTimeLogs.Clear();
        if (_repository != null)
        {
            var options = await _repository.GetTimeLogsForLinkingAsync(excludeLogId: _sourceTimeLog.ObjectID);
            foreach (var opt in options)
            {
                AvailableParentTimeLogs.Add(opt);
            }
        }

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

            SelectOrAddMatchingOption(targetLogId, TargetTimeLogName, targetWellId, targetWellboreId);
            if (CurrentTargetUpdateTask != null) await CurrentTargetUpdateTask;
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
                SelectOrAddMatchingOption(primaryCandidate.ObjectID, primaryCandidate.nameLog, primaryCandidate.WellID, primaryCandidate.WellboreID);
                if (CurrentTargetUpdateTask != null) await CurrentTargetUpdateTask;
                return;
            }
        }

        // 4. Default: when this timelog is not linked to another parent timelog,
        // Target Well, Wellbore, and TimeLog match the selected timelog itself!
        TargetWellName = SourceWellName;
        TargetWellboreName = SourceWellboreName;
        TargetTimeLogName = SourceTimeLogName;
        SelectOrAddMatchingOption(_sourceTimeLog.ObjectID, SourceTimeLogName, _sourceTimeLog.WellID, _sourceTimeLog.WellboreID);
        if (CurrentTargetUpdateTask != null) await CurrentTargetUpdateTask;
    }

    private void SelectOrAddMatchingOption(string logId, string logName, string wellId, string wellboreId)
    {
        var match = AvailableParentTimeLogs.FirstOrDefault(o =>
            (!string.IsNullOrWhiteSpace(logId) && o.LogId.Equals(logId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(logName) && o.LogName.Equals(logName, StringComparison.OrdinalIgnoreCase)));

        string effectiveWellId = !string.IsNullOrWhiteSpace(wellId) ? wellId : (!string.IsNullOrWhiteSpace(_sourceTimeLog.nameWell) ? _sourceTimeLog.nameWell : SourceWellName);
        string effectiveWbId = !string.IsNullOrWhiteSpace(wellboreId) ? wellboreId : (!string.IsNullOrWhiteSpace(_sourceTimeLog.nameWellbore) ? _sourceTimeLog.nameWellbore : SourceWellboreName);

        if (match == null && !string.IsNullOrWhiteSpace(logName))
        {
            match = new TimeLogOption
            {
                LogId = logId,
                LogName = logName,
                WellId = effectiveWellId,
                WellboreId = effectiveWbId
            };
            AvailableParentTimeLogs.Insert(0, match);
        }

        if (match != null)
        {
            SelectedParentTimeLog = match;
            OnPropertyChanged(nameof(CanStart));
            OnPropertyChanged(nameof(HasSelectedTimelog));
            StartSyncCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task<string> ResolveWellNameAsync(string? wellIdOrName)
    {
        if (string.IsNullOrWhiteSpace(wellIdOrName))
        {
            if (!string.IsNullOrWhiteSpace(SourceWellName))
                return SourceWellName;
            if (_repository != null)
            {
                var projectWell = await _repository.GetProjectWellAsync();
                if (!string.IsNullOrWhiteSpace(projectWell?.WellName))
                    return projectWell.WellName;
            }
            return !string.IsNullOrWhiteSpace(_session.ProjectName) ? _session.ProjectName : "Camel1_2016";
        }

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

        if (!string.IsNullOrWhiteSpace(_sourceTimeLog.nameLog) &&
            !IsRawId(_sourceTimeLog.nameLog) &&
            (logIdOrName.Equals(_sourceTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase) ||
             logIdOrName.Equals(_sourceTimeLog.nameLog, StringComparison.OrdinalIgnoreCase)))
        {
            return _sourceTimeLog.nameLog;
        }

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
            if (DataStartDateTime.HasValue && DataEndDateTime.HasValue)
            {
                FromDate = DataStartDateTime.Value.Date;
                FromTime = DataStartDateTime.Value;
                ToDate = DataEndDateTime.Value.Date;
                ToTime = DataEndDateTime.Value;
            }
            else if (MinAvailableDate != DateTime.MinValue && MaxAvailableDate != DateTime.MaxValue)
            {
                FromDate = MinAvailableDate.Date;
                FromTime = MinAvailableDate;
                ToDate = MaxAvailableDate.Date;
                ToTime = MaxAvailableDate;
            }
            OnPropertyChanged(nameof(FromDateTime));
            OnPropertyChanged(nameof(FromDateTimeText));
            OnPropertyChanged(nameof(ToDateTime));
            OnPropertyChanged(nameof(ToDateTimeText));
        }
        else if (value == "Last 24 Hours")
        {
            var end = DataEndDateTime ?? ((MaxAvailableDate != DateTime.MaxValue && MaxAvailableDate != DateTime.MinValue) ? MaxAvailableDate : DateTime.Now);
            ToDate = end.Date;
            ToTime = end;
            var start = end.AddHours(-24);
            FromDate = start.Date;
            FromTime = start;
            OnPropertyChanged(nameof(FromDateTime));
            OnPropertyChanged(nameof(FromDateTimeText));
            OnPropertyChanged(nameof(ToDateTime));
            OnPropertyChanged(nameof(ToDateTimeText));
        }
        else if (value == "Last 7 Days")
        {
            var end = DataEndDateTime ?? ((MaxAvailableDate != DateTime.MaxValue && MaxAvailableDate != DateTime.MinValue) ? MaxAvailableDate : DateTime.Now);
            ToDate = end.Date;
            ToTime = end;
            var start = end.AddDays(-7);
            FromDate = start.Date;
            FromTime = start;
            OnPropertyChanged(nameof(FromDateTime));
            OnPropertyChanged(nameof(FromDateTimeText));
            OnPropertyChanged(nameof(ToDateTime));
            OnPropertyChanged(nameof(ToDateTimeText));
        }

        OnPropertyChanged(nameof(ConfirmationMessage));
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    public async Task StartSyncAsync()
    {
        if (IsRunning) return;

        HasError = false;
        HasSuccess = false;
        ErrorMessage = string.Empty;

        // 1. Validation before execution (Requirement 3: Validate Timelog selection)
        if (!HasSelectedTimelog)
        {
            HasError = true;
            ErrorMessage = "Timelog selection is required. Please select a Target Parent Timelog in the dialog.";
            ProgressStatus = "Validation failed: No timelog selected.";
            return;
        }

        if (_sourceTimeLog == null)
        {
            HasError = true;
            ErrorMessage = "Source Timelog from WellTree is missing or invalid.";
            ProgressStatus = "Validation failed: Missing source timelog.";
            return;
        }

        if (FromDateTime > ToDateTime)
        {
            HasError = true;
            ErrorMessage = "Invalid Date Range: 'From Date & Time' cannot be later than 'To Date & Time'.";
            ProgressStatus = "Validation failed: Invalid date range.";
            return;
        }

        var dataService = _session?.GetDataService();
        if (dataService == null || !_session.IsProjectOpen)
        {
            HasError = true;
            ErrorMessage = "Project database session is not available.";
            ProgressStatus = "Validation failed: Database offline.";
            return;
        }

        // 2. Resolve Target Parent Timelog
        TimeLog? parentLog = _parentTimeLog;
        if (parentLog == null && SelectedParentTimeLog != null)
        {
            if (_repository != null)
            {
                parentLog = await _repository.GetTimeLogAsync(SelectedParentTimeLog.LogId);
            }
            if (parentLog == null)
            {
                string err = "";
                parentLog = TimeLogService.LoadObject(dataService, SelectedParentTimeLog.LogId, ref err);
            }
        }
        if (parentLog == null && !string.IsNullOrWhiteSpace(TargetTimeLogName))
        {
            if (_repository != null)
            {
                parentLog = await _repository.GetTimeLogAsync(TargetTimeLogName);
            }
            if (parentLog == null)
            {
                string err = "";
                parentLog = TimeLogService.LoadObject(dataService, TargetTimeLogName, ref err);
            }
        }

        if (parentLog == null)
        {
            // If target name equals source name, fallback to source log (self-refresh/sync)
            if (TargetTimeLogName.Equals(SourceTimeLogName, StringComparison.OrdinalIgnoreCase) ||
                TargetTimeLogName.Equals(_sourceTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase))
            {
                parentLog = _sourceTimeLog;
            }
            else
            {
                HasError = true;
                ErrorMessage = $"Target Parent Timelog '{TargetTimeLogName}' could not be found or loaded.";
                ProgressStatus = "Sync failed: Target timelog not found.";
                return;
            }
        }

        _parentTimeLog = parentLog;

        // Check if user attempts to merge two distinct logs that conflict or if source/parent tables conflict
        if (!ReferenceEquals(_sourceTimeLog, _parentTimeLog) &&
            !string.IsNullOrWhiteSpace(_sourceTimeLog.ObjectID) &&
            !string.IsNullOrWhiteSpace(_parentTimeLog.ObjectID) &&
            _sourceTimeLog.ObjectID.Equals(_parentTimeLog.ObjectID, StringComparison.OrdinalIgnoreCase) &&
            !TargetTimeLogName.Equals(SourceTimeLogName, StringComparison.OrdinalIgnoreCase))
        {
            HasError = true;
            ErrorMessage = "Cannot synchronize a timelog into itself. Please select a distinct Parent Timelog.";
            ProgressStatus = "Sync failed: Merge conflict.";
            return;
        }

        // Ensure physical table names and curves are loaded
        if (string.IsNullOrWhiteSpace(_sourceTimeLog.__dataTableName))
        {
            var dtNameObj = dataService.GetValue($"SELECT DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE LOG_ID='{_sourceTimeLog.ObjectID.Replace("'", "''")}' OR LOG_NAME='{_sourceTimeLog.nameLog.Replace("'", "''")}' LIMIT 1;");
            if (dtNameObj != null) _sourceTimeLog.__dataTableName = Convert.ToString(dtNameObj) ?? "";
        }
        if (_sourceTimeLog.logCurves.Count == 0)
        {
            TimeLogService.LoadLogCurves(dataService, _sourceTimeLog);
        }

        if (string.IsNullOrWhiteSpace(_parentTimeLog.__dataTableName))
        {
            var dtNameObj = dataService.GetValue($"SELECT DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE LOG_ID='{_parentTimeLog.ObjectID.Replace("'", "''")}' OR LOG_NAME='{_parentTimeLog.nameLog.Replace("'", "''")}' LIMIT 1;");
            if (dtNameObj != null) _parentTimeLog.__dataTableName = Convert.ToString(dtNameObj) ?? "";
        }
        if (_parentTimeLog.logCurves.Count == 0)
        {
            TimeLogService.LoadLogCurves(dataService, _parentTimeLog);
        }

        IsRunning = true;
        IsCompleted = false;
        ProgressPercent = 0;
        ProgressStatus = "Preparing data synchronization...";
        _stopwatch.Restart();
        ElapsedTimeText = "Time Elapsed [00:00:00]";

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            DateTime minDate = FromDateTime;
            DateTime maxDate = ToDateTime;

            // Time zone handling matching frmSyncTimeData.vb
            if (_repository != null)
            {
                try
                {
                    var wells = await _repository.GetWellsAsync();
                    var well = wells?.FirstOrDefault(w => w.ObjectID.Equals(_sourceTimeLog.WellID, StringComparison.OrdinalIgnoreCase) || w.name.Equals(_sourceTimeLog.WellID, StringComparison.OrdinalIgnoreCase));
                    if (well == null)
                    {
                        well = await _repository.GetProjectWellAsync();
                    }
                    if (well != null && string.Equals(well.wellDateFormat, Well.wDateFormatUTC, StringComparison.OrdinalIgnoreCase))
                    {
                        if (minDate.Kind != DateTimeKind.Utc) minDate = DateTime.SpecifyKind(minDate, DateTimeKind.Utc);
                        if (maxDate.Kind != DateTimeKind.Utc) maxDate = DateTime.SpecifyKind(maxDate, DateTimeKind.Utc);
                    }
                }
                catch { }
            }

            ProgressPercent = 10;
            ProgressStatus = $"Verifying target Parent Timelog '{TargetTimeLogName}'...";
            await Task.Delay(80, ct);

            ProgressPercent = 25;
            ProgressStatus = "Synchronizing columns and verifying schema...";
            await Task.Delay(80, ct);

            var progress = new Progress<(double percent, string message)>(report =>
            {
                ProgressPercent = Math.Min(100, Math.Max(0, report.percent));
                ProgressStatus = report.message;
                ElapsedTimeText = $"Time Elapsed [{_stopwatch.Elapsed:hh\\:mm\\:ss}]";
            });

            var calculator = new HoleDepthCalculator();
            await calculator.SyncTimeLogDataAsync(
                dataService,
                _parentTimeLog,
                _sourceTimeLog,
                minDate,
                maxDate,
                progress,
                ct);

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
            _stopwatch.Stop();
            ElapsedTimeText = $"Time Elapsed [{_stopwatch.Elapsed:hh\\:mm\\:ss}]";
            IsRunning = false;
            OnPropertyChanged(nameof(CanStart));
            StartSyncCommand.NotifyCanExecuteChanged();
        }
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

    private sealed class VmxTimeLogMeta
    {
        public string? WELL_ID { get; set; }
        public string? WELLBORE_ID { get; set; }
        public string? DATA_TABLE_NAME { get; set; }
        public string? MIN_DATE { get; set; }
        public string? MAX_DATE { get; set; }
    }

    private sealed class VmxTimeLogSummaryMeta
    {
        public string? TableName { get; set; }
        public string? StartIndex { get; set; }
        public string? EndIndex { get; set; }
    }
}
