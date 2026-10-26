using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.Projects;
using DrillIntel.Services;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.ViewModels
{
    public partial class ProcessDrillingConnectionsViewModel : ObservableObject
    {
        private readonly ProjectSession _session;
        private readonly IProcessDrillingConnectionsService _service;
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
        private bool _skipConnectionsMoreThan = true;

        [ObservableProperty]
        private double _skipMinutes = 30.0;

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
        private string _progressStatus = "Ready to process drilling connections.";

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private int _detectedConnectionsCount = 0;

        [ObservableProperty]
        private int _existingConnectionsCount = 0;

        public bool CanStart => !IsRunning && !string.IsNullOrWhiteSpace(WellId);
        public bool CanCancel => IsRunning;

        public ProcessDrillingConnectionsViewModel(
            ProjectSession session,
            Well? well = null,
            TimeLog? timeLog = null,
            IProcessDrillingConnectionsService? service = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _well = well;
            _timeLog = timeLog;
            _service = service ?? new ProcessDrillingConnectionsService(_session);

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
                        // Normalize format: if "06:00:00" -> display "06:00" or as is
                        if (dayTimeSetting.Length >= 5)
                        {
                            DayTimeHoursFrom = dayTimeSetting.Substring(0, 5);
                        }
                        else
                        {
                            DayTimeHoursFrom = dayTimeSetting;
                        }
                    }

                    if (commonSettings.settings.ContainsKey("SkipDrillingConnectionMoreThan"))
                    {
                        SkipConnectionsMoreThan = commonSettings.getBooleanSetting("SkipDrillingConnectionMoreThan");
                    }

                    if (commonSettings.settings.ContainsKey("SkipDrillingConnectionMoreThanValue"))
                    {
                        double val = commonSettings.getNumericSetting("SkipDrillingConnectionMoreThanValue");
                        if (val > 0)
                        {
                            SkipMinutes = val;
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
                    DataTableName = !string.IsNullOrWhiteSpace(_timeLog.__dataTableName)
                        ? _timeLog.__dataTableName
                        : TimeLog.getDataTableName(dataService, _timeLog.WellID, _timeLog.WellboreID, _timeLog.ObjectID);
                    _timeLog.__dataTableName = DataTableName;

                    if (string.IsNullOrWhiteSpace(WellId) && !string.IsNullOrWhiteSpace(_timeLog.WellID))
                    {
                        WellId = _timeLog.WellID;
                        if (string.IsNullOrWhiteSpace(WellName))
                        {
                            WellName = !string.IsNullOrWhiteSpace(_timeLog.nameWell) ? _timeLog.nameWell : _timeLog.__WellName;
                        }
                    }
                }

                // Refresh existing connections count
                RefreshExistingCount();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessDrillingConnectionsViewModel.InitializeMetadata] {ex.Message}");
            }
        }

        public void RefreshExistingCount()
        {
            try
            {
                var dataService = _session.GetDataService();
                if (dataService != null && !string.IsNullOrWhiteSpace(WellId))
                {
                    bool tableExists = dataService.IsRecordExist(
                        "SELECT 1 FROM sqlite_master WHERE type='table' AND name='VMX_AKPI_DRLG_CONNECTIONS';");
                    if (tableExists)
                    {
                        var val = dataService.GetValue($"SELECT COUNT(*) FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{WellId.Replace("'", "''")}';");
                        if (val != null && int.TryParse(val.ToString(), out int count))
                        {
                            ExistingConnectionsCount = count;
                        }
                    }
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
            ProgressStatus = "Preparing drilling connections calculation...";
            DetectedConnectionsCount = 0;

            _cts = new CancellationTokenSource();

            try
            {
                var options = new ProcessDrillingConnectionsOptions
                {
                    WellID = WellId,
                    LogID = _timeLog?.ObjectID ?? string.Empty,
                    SkipConnectionsMoreThan = SkipConnectionsMoreThan,
                    SkipMinutes = SkipMinutes,
                    DayTimeHoursFrom = DayTimeHoursFrom,
                    OverwriteExisting = OverwriteExisting
                };

                var progressReporter = new Progress<CalculationProgress>(p =>
                {
                    ProgressPercent = p.Percentage;
                    ProgressStatus = p.StatusMessage;
                });

                var result = await _service.ProcessConnectionsAsync(options, progressReporter, _cts.Token);

                if (result.Success)
                {
                    ProgressPercent = 100;
                    DetectedConnectionsCount = result.ProcessedCount;
                    ProgressStatus = $"Processing completed successfully! {DetectedConnectionsCount} connections processed.";
                    IsCompleted = true;
                    RefreshExistingCount();
                }
                else
                {
                    HasError = true;
                    ErrorMessage = !string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? result.ErrorMessage
                        : "An error occurred while processing drilling connections.";
                    ProgressStatus = "Processing failed.";
                }
            }
            catch (OperationCanceledException)
            {
                ProgressStatus = "Drilling connection processing was cancelled.";
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
                    "Are you sure you want to clear all existing drilling connections for this well from the database?",
                    "Clear Database Entries");
            }
            else
            {
                var mbr = MessageBox.Show(
                    "Are you sure you want to clear all existing drilling connections for this well from the database?",
                    "Clear Database Entries",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                proceed = (mbr == MessageBoxResult.Yes);
            }

            if (!proceed) return;

            try
            {
                ProgressStatus = "Clearing connections from database...";
                bool success = await _service.ClearConnectionsAsync(WellId);
                if (success)
                {
                    RefreshExistingCount();
                    ProgressStatus = "All drilling connection database entries cleared successfully.";
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
