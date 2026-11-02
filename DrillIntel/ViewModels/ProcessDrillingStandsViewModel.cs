using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.ViewModels
{
    public partial class ProcessDrillingStandsViewModel : ObservableObject
    {
        private readonly ProjectSession _session;
        private readonly IProcessDrillingStandsService _service;
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
        private string _dayTimeHoursFrom = "12:00";

        [ObservableProperty]
        private bool _overwriteExisting = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanStart))]
        [NotifyPropertyChangedFor(nameof(CanCancel))]
        private bool _isRunning;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private double _progressPercent;

        [ObservableProperty]
        private string _progressStatus = "Ready to process drilling stands.";

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private int _detectedStandsCount = 0;

        [ObservableProperty]
        private int _existingStandsCount = 0;

        public bool CanStart => !IsRunning && !string.IsNullOrWhiteSpace(WellId);
        public bool CanCancel => IsRunning;

        public ProcessDrillingStandsViewModel(
            ProjectSession session,
            Well? well = null,
            TimeLog? timeLog = null,
            IProcessDrillingStandsService? service = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _well = well;
            _timeLog = timeLog;
            _service = service ?? new ProcessDrillingStandsService(_session);

            InitializeMetadata();
        }

        private void InitializeMetadata()
        {
            try
            {
                var dataService = _session.GetDataService();
                if (dataService == null) return;

                // Load common settings from DrillIntelApp.sqlite (VMX_SETTINGS)
                var commonSettings = _service.LoadCommonSettings();
                if (commonSettings != null)
                {
                    string dayTimeSetting = commonSettings.getSetting(SystemSettings.DayTimeFrom);
                    if (!string.IsNullOrWhiteSpace(dayTimeSetting))
                    {
                        if (dayTimeSetting.Length >= 5)
                        {
                            DayTimeHoursFrom = dayTimeSetting.Substring(0, 5);
                        }
                        else
                        {
                            DayTimeHoursFrom = dayTimeSetting;
                        }
                    }
                }

                // Resolve Well if not provided
                if (_well == null && _timeLog != null && !string.IsNullOrWhiteSpace(_timeLog.WellID))
                {
                    _well = new Well
                    {
                        ObjectID = _timeLog.WellID,
                        name = !string.IsNullOrWhiteSpace(_timeLog.nameWell) ? _timeLog.nameWell : _timeLog.__WellName
                    };
                }

                if (_well == null)
                {
                    var repo = new WellDataRepository(_session);
                    _well = repo.GetProjectWellAsync().GetAwaiter().GetResult();
                }

                if (_well != null)
                {
                    WellId = _well.ObjectID ?? string.Empty;
                    WellName = !string.IsNullOrWhiteSpace(_well.name) ? _well.name : WellId;
                }

                // Resolve TimeLog if not provided
                if (_timeLog == null)
                {
                    var repo = new WellDataRepository(_session);
                    var timeLogs = repo.GetTimeLogsAsync().GetAwaiter().GetResult();
                    _timeLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
                }

                if (_timeLog != null)
                {
                    LogName = !string.IsNullOrWhiteSpace(_timeLog.nameLog) ? _timeLog.nameLog : _timeLog.ObjectID;
                    if (string.IsNullOrWhiteSpace(WellId) && !string.IsNullOrWhiteSpace(_timeLog.WellID))
                    {
                        WellId = _timeLog.WellID;
                        if (string.IsNullOrWhiteSpace(WellName))
                        {
                            WellName = !string.IsNullOrWhiteSpace(_timeLog.nameWell) ? _timeLog.nameWell : _timeLog.__WellName;
                        }
                    }
                }

                RefreshExistingCount();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessDrillingStandsViewModel.InitializeMetadata] {ex.Message}");
            }
        }

        public void RefreshExistingCount()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(WellId))
                {
                    ExistingStandsCount = _service.GetExistingStandsCount(WellId);
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
            ProgressStatus = "Preparing drilling stands calculation...";
            DetectedStandsCount = 0;

            _cts = new CancellationTokenSource();

            try
            {
                var options = new ProcessDrillingStandsOptions
                {
                    WellID = WellId,
                    LogID = _timeLog?.ObjectID ?? string.Empty,
                    DayTimeHoursFrom = DayTimeHoursFrom,
                    OverwriteExisting = OverwriteExisting
                };

                var progressReporter = new Progress<CalculationProgress>(p =>
                {
                    ProgressPercent = p.Percentage;
                    ProgressStatus = p.StatusMessage;
                });

                var result = await _service.ProcessStandsAsync(options, progressReporter, _cts.Token);

                if (result.Success)
                {
                    ProgressPercent = 100;
                    DetectedStandsCount = result.ProcessedCount;
                    ProgressStatus = $"Processing completed successfully! {DetectedStandsCount} drilling stands processed.";
                    IsCompleted = true;
                    RefreshExistingCount();
                }
                else
                {
                    HasError = true;
                    ErrorMessage = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? result.ErrorMessage
                        : "An error occurred while processing drilling stands.";
                    ProgressStatus = "Processing failed.";
                }
            }
            catch (OperationCanceledException)
            {
                ProgressStatus = "Drilling stand processing was cancelled.";
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                ProgressStatus = "Processing encountered an unhandled error.";
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
            if (IsRunning && _cts != null && !_cts.IsCancellationRequested)
            {
                ProgressStatus = "Cancelling processing...";
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
                    "Are you sure you want to clear all existing drilling stands for this well from the database?",
                    "Clear Database Entries");
            }
            else
            {
                var mbr = MessageBox.Show(
                    "Are you sure you want to clear all existing drilling stands for this well from the database?",
                    "Clear Database Entries",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                proceed = (mbr == MessageBoxResult.Yes);
            }

            if (!proceed) return;

            try
            {
                ProgressStatus = "Clearing stands from database...";
                bool success = await _service.ClearStandsAsync(WellId);
                if (success)
                {
                    RefreshExistingCount();
                    ProgressStatus = "All drilling stand database entries cleared successfully.";
                }
                else
                {
                    HasError = true;
                    ErrorMessage = "Failed to clear database entries.";
                    ProgressStatus = "Clear operation failed.";
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                ProgressStatus = "Clear operation failed.";
            }
        }

        [RelayCommand]
        private void Close()
        {
            if (IsRunning)
            {
                Cancel();
            }
            RequestClose?.Invoke(IsCompleted);
        }
    }
}
