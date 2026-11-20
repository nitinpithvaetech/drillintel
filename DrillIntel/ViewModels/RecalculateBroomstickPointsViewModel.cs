using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.ViewModels;

public partial class RecalculateBroomstickPointsViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IBroomstickProcessor _processor;
    private readonly IBroomstickDataService _dataService;
    private Well? _well;
    private TimeLog? _timeLog;
    private CancellationTokenSource? _cts;

    public event Action<bool>? RequestClose;
    public Func<string, string, bool>? ConfirmationHandler;

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _dataTableName = string.Empty;

    [ObservableProperty]
    private int _existingPointsCount = 0;

    [ObservableProperty]
    private bool _clearExistingPoints = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelectDates))]
    [NotifyPropertyChangedFor(nameof(IsCustomRange))]
    private bool _isEntireWell = true;

    public bool IsCustomRange
    {
        get => !IsEntireWell;
        set => IsEntireWell = !value;
    }

    public bool CanSelectDates => !IsEntireWell && !IsRunning;

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
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanSelectDates))]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressStatus = "Ready to recalculate Broomstick points.";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool CanStart => !IsRunning && !string.IsNullOrWhiteSpace(WellId);
    public bool CanStop => IsRunning;

    public string CancelButtonText => IsRunning ? "Stop" : (IsCompleted ? "Close" : "Cancel");

    public RecalculateBroomstickPointsViewModel(
        ProjectSession session,
        Well? well = null,
        TimeLog? timeLog = null,
        IBroomstickProcessor? processor = null,
        IBroomstickDataService? dataService = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _well = well;
        _timeLog = timeLog;
        _dataService = dataService ?? new BroomstickDataService(_session);
        _processor = processor ?? new BroomstickProcessor(_dataService, _session);

        InitializeMetadata();
    }

    private void InitializeMetadata()
    {
        try
        {
            WellName = !string.IsNullOrWhiteSpace(_well?.name)
                ? _well.name
                : (!string.IsNullOrWhiteSpace(_timeLog?.nameWell)
                    ? _timeLog.nameWell
                    : (!string.IsNullOrWhiteSpace(_well?.ObjectID) ? _well.ObjectID : (_session.ProjectName ?? "Current Well")));

            WellId = !string.IsNullOrWhiteSpace(_well?.ObjectID)
                ? _well.ObjectID
                : (!string.IsNullOrWhiteSpace(_timeLog?.WellID)
                    ? _timeLog.WellID
                    : (!string.IsNullOrWhiteSpace(_well?.name) ? _well.name : "WELL_1"));

            LogName = !string.IsNullOrWhiteSpace(_timeLog?.nameLog)
                ? _timeLog.nameLog
                : (!string.IsNullOrWhiteSpace(_timeLog?.ObjectID) ? _timeLog.ObjectID : "Primary Timelog");

            DataTableName = _timeLog?.__dataTableName ?? string.Empty;

            _ = RefreshExistingCountAsync();
            InitializeDateBounds();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecalculateBroomstickPointsViewModel.InitializeMetadata] {ex.Message}");
        }
    }

    public async Task RefreshExistingCountAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(WellId))
            {
                ExistingPointsCount = await _dataService.GetBroomstickPointsCountAsync(WellId);
            }
        }
        catch { }
    }

    private void InitializeDateBounds()
    {
        try
        {
            var dataService = _session.GetDataService();
            if (dataService != null && _timeLog != null)
            {
                double firstOa = _timeLog.getFirstIndexOptimized(dataService);
                double lastOa = _timeLog.getLastIndexOptimized(dataService);

                DateTime minDt = firstOa > 0 ? DateTime.FromOADate(firstOa) : DateTime.MinValue;
                DateTime maxDt = lastOa > 0 ? DateTime.FromOADate(lastOa) : DateTime.MinValue;

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

            if (_timeLog != null && DateTime.TryParse(_timeLog.startIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
            {
                FromDate = s.Date;
                FromTime = s;
                MinAvailableDate = s.Date;
            }
            if (_timeLog != null && DateTime.TryParse(_timeLog.endIndex, CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
            {
                ToDate = e.Date;
                ToTime = e;
                MaxAvailableDate = e.Date;
            }
        }
        catch { }
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
            if (ClearExistingPoints)
            {
                ProgressStatus = "Clearing existing Broomstick points...";
                await _dataService.ClearBroomstickPointsAsync(WellId, _cts.Token);
            }

            DateTime? start = IsEntireWell ? null : FromDateTime;
            DateTime? end = IsEntireWell ? null : ToDateTime;

            var progressReporter = new Progress<CalculationProgress>(p =>
            {
                ProgressPercent = p.Percentage;
                ProgressStatus = p.StatusMessage;
            });

            var result = await _processor.RecalculatePointsAsync(WellId, progressReporter, _cts.Token, start, end);

            if (result.Success)
            {
                ProgressPercent = 100;
                ProgressStatus = "Recalculation completed successfully!";
                IsCompleted = true;
                await RefreshExistingCountAsync();
                _session.NotifyDataChanged();
            }
            else
            {
                HasError = true;
                ErrorMessage = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ErrorMessage
                    : "An error occurred during recalculation.";
                ProgressStatus = "Recalculation failed.";
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Recalculation stopped by user.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            ProgressStatus = "Error encountered during recalculation.";
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (IsRunning && _cts != null && !_cts.IsCancellationRequested)
        {
            ProgressStatus = "Stopping recalculation...";
            _cts.Cancel();
        }
    }

    [RelayCommand]
    private async Task ClearDbEntriesAsync()
    {
        if (IsRunning) return;

        bool proceed = true;
        if (ConfirmationHandler != null)
        {
            proceed = ConfirmationHandler(
                "Are you sure you want to clear all existing Broomstick points for this well from the database?",
                "Clear Database Entries");
        }
        else
        {
            var mbr = MessageBox.Show(
                "Are you sure you want to clear all existing Broomstick points for this well from the database?",
                "Clear Database Entries",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            proceed = (mbr == MessageBoxResult.Yes);
        }

        if (!proceed) return;

        try
        {
            await _dataService.ClearBroomstickPointsAsync(WellId);
            await RefreshExistingCountAsync();
            ProgressStatus = "Existing Broomstick points cleared.";
            _session.NotifyDataChanged();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error clearing points: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Close()
    {
        if (IsRunning)
        {
            Stop();
        }
        RequestClose?.Invoke(IsCompleted);
    }
}
