using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.ViewModels;

public partial class ProcessTripConnectionViewModel : ObservableObject
{
    private readonly IProcessTripConnectionLogService _service;
    private readonly TimeLog? _timeLog;
    private CancellationTokenSource? _cts;

    public event Action<bool>? RequestClose;

    [ObservableProperty] private string _wellName = string.Empty;
    [ObservableProperty] private string _wellId = string.Empty;
    [ObservableProperty] private string _logName = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    private bool _isRunning;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressStatus = "Ready to process trip connections.";
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private int _detectedCount;

    public bool CanStart => !IsRunning && !string.IsNullOrWhiteSpace(WellId) && _timeLog != null;
    public bool CanCancel => IsRunning;

    public ProcessTripConnectionViewModel(
        ProjectSession session,
        Well? well = null,
        TimeLog? timeLog = null,
        IProcessTripConnectionLogService? service = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _timeLog = timeLog;
        _service = service ?? new ProcessTripConnectionLogService(session);
        WellId = well?.ObjectID ?? timeLog?.WellID ?? string.Empty;
        WellName = !string.IsNullOrWhiteSpace(well?.name) ? well.name : WellId;
        LogName = timeLog == null ? string.Empty : (!string.IsNullOrWhiteSpace(timeLog.nameLog) ? timeLog.nameLog : timeLog.ObjectID);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (!CanStart) return;
        HasError = false;
        ErrorMessage = string.Empty;
        IsCompleted = false;
        IsRunning = true;
        ProgressPercent = 0;
        DetectedCount = 0;
        ProgressStatus = "Preparing trip connection processing...";
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<CalculationProgress>(value =>
            {
                ProgressPercent = value.Percentage;
                ProgressStatus = value.StatusMessage;
            });
            var result = await _service.ProcessTripConnectionsAsync(new ProcessTripConnectionLogOptions
            {
                WellID = WellId,
                LogID = _timeLog?.ObjectID ?? string.Empty
            }, progress, _cts.Token);

            if (result.Success)
            {
                ProgressPercent = 100;
                DetectedCount = result.ProcessedCount;
                ProgressStatus = $"Trip connection processing complete. {DetectedCount} points detected.";
                IsCompleted = true;
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? string.Empty;
                ProgressStatus = "Trip connection processing failed.";
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Trip connection processing was cancelled.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            ProgressStatus = "Trip connection processing failed.";
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
        if (_cts is { IsCancellationRequested: false })
        {
            ProgressStatus = "Cancelling trip connection processing...";
            _cts.Cancel();
        }
    }

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(IsCompleted);
}
