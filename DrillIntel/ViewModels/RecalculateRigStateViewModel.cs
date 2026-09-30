using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class RecalculateRigStateViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly TimeLog _timeLog;
    private CancellationTokenSource? _cts;

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _dataTableName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectDates))]
    [NotifyPropertyChangedFor(nameof(IsCustomRange))]
    private bool _isEntireLog = true;

    public bool IsCustomRange
    {
        get => !IsEntireLog;
        set => IsEntireLog = !value;
    }

    public bool CanSelectDates => !IsEntireLog && !IsRunning;

    [ObservableProperty]
    private DateTime _fromDate = DateTime.Now.AddDays(-7);

    [ObservableProperty]
    private DateTime? _fromTime = DateTime.Today;

    [ObservableProperty]
    private DateTime _toDate = DateTime.Now;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanSelectDates))]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressStatus = "Ready to recalculate.";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool CanStart => !IsRunning;

    public string CancelButtonText => IsRunning ? "Cancel Recalculation" : (IsCompleted ? "Close" : "Cancel");

    public RecalculateRigStateViewModel(ProjectSession session, TimeLog timeLog)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _timeLog = timeLog ?? throw new ArgumentNullException(nameof(timeLog));

        LogName = !string.IsNullOrWhiteSpace(_timeLog.nameLog) ? _timeLog.nameLog : _timeLog.ObjectID;
        WellName = !string.IsNullOrWhiteSpace(_timeLog.nameWell) ? _timeLog.nameWell : _timeLog.__WellName;
        DataTableName = _timeLog.__dataTableName;

        InitializeDateBounds();
    }

    private void InitializeDateBounds()
    {
        try
        {
            var dataService = _session.GetDataService();
            if (dataService != null)
            {
                // Fast path: use optimized index from VMX_TIME_LOG (getFirstIndexOptimized / getLastIndexOptimized)
                double firstOa = _timeLog.getFirstIndexOptimized(dataService);
                double lastOa = _timeLog.getLastIndexOptimized(dataService);

                DateTime minDt = firstOa > 0 ? DateTime.FromOADate(firstOa) : DateTime.MinValue;
                DateTime maxDt = lastOa > 0 ? DateTime.FromOADate(lastOa) : DateTime.MinValue;

                // Fallback to table scan if metadata extents are not available
                if (minDt == DateTime.MinValue && !string.IsNullOrWhiteSpace(_timeLog.__dataTableName))
                {
                    minDt = RigStateService.GetMinDateFromTable(dataService, _timeLog.__dataTableName);
                }
                if (maxDt == DateTime.MinValue && !string.IsNullOrWhiteSpace(_timeLog.__dataTableName))
                {
                    maxDt = RigStateService.GetMaxDateFromTable(dataService, _timeLog.__dataTableName);
                }

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

        // Fallback to timeLog startIndex/endIndex strings if present
        if (DateTime.TryParse(_timeLog.startIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
        {
            FromDate = s.Date;
            FromTime = s;
            MinAvailableDate = s.Date;
        }
        if (DateTime.TryParse(_timeLog.endIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
        {
            ToDate = e.Date;
            ToTime = e;
            MaxAvailableDate = e.Date;
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;

        HasError = false;
        ErrorMessage = string.Empty;
        IsCompleted = false;
        IsRunning = true;
        ProgressPercent = 0;
        ProgressStatus = "Preparing recalculation...";

        _cts = new CancellationTokenSource();

        try
        {
            var dataService = _session.GetDataService();
            if (dataService == null)
            {
                HasError = true;
                ErrorMessage = "Database service is not initialized.";
                ProgressStatus = "Recalculation failed.";
                return;
            }

            DateTime? start;
            DateTime? end;

            if (IsEntireLog)
            {
                start = null;
                end = null;
            }
            else
            {
                start = FromDateTime;
                end = ToDateTime;
            }

            var progress = new Progress<double>(percent =>
            {
                ProgressPercent = percent;
                ProgressStatus = $"Processing rig states: {percent:F1}% completed...";
            });

            bool success = await RigStateService.RecalculateRigStateAsync(
                dataService,
                _timeLog,
                start,
                end,
                progress,
                _cts.Token);

            if (success)
            {
                ProgressPercent = 100;
                ProgressStatus = "Recalculation completed successfully!";
                IsCompleted = true;
                _session.NotifyDataChanged();
            }
            else
            {
                HasError = true;
                ErrorMessage = !string.IsNullOrWhiteSpace(RigStateService.LastError)
                    ? RigStateService.LastError
                    : "An unknown error occurred during recalculation.";
                ProgressStatus = "Recalculation failed.";
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Recalculation was cancelled.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            ProgressStatus = "Error encountered.";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
            ProgressStatus = "Cancelling recalculation...";
        }
        else
        {
            RequestClose?.Invoke(IsCompleted);
        }
    }
}
