using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services.AutoTagDetection;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for the Process Tag Creation dialog.
/// Executes automated phase and activity tag detection (Rig Up, Drilling, Hole Cleaning/CUC,
/// Trip Out, Trip In, and Casing runs) against well telemetry in SQLite.
/// </summary>
public partial class ProcessTagCreationViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private Well? _well;
    private TimeLog? _timeLog;
    private CancellationTokenSource? _cts;
    private AutoTagCreationProcess? _currentProcess;

    public event Action<bool>? RequestClose;

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _dataTableName = string.Empty;

    [ObservableProperty]
    private int _existingTagsCount = 0;

    [ObservableProperty]
    private bool _clearExistingTags = true;

    [ObservableProperty]
    private double _standHeight = 120.0;

    [ObservableProperty]
    private int _drillingTimeLimit = 900;

    [ObservableProperty]
    private double _depthThreshold = 300.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelButtonText))]
    private bool _isCompleted;

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressStatus = "Ready to start tag detection.";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private int _detectedTagsCount = 0;

    public bool CanStart => !IsRunning && !string.IsNullOrWhiteSpace(WellId);

    public string CancelButtonText => IsRunning ? "Cancel Detection" : (IsCompleted ? "Close" : "Cancel");

    public ProcessTagCreationViewModel(ProjectSession session, Well? well = null, TimeLog? timeLog = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _well = well;
        _timeLog = timeLog;

        InitializeMetadata();
    }

    private void InitializeMetadata()
    {
        try
        {
            var dataService = _session.GetDataService();
            if (dataService == null) return;

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
            if (_timeLog == null && !string.IsNullOrWhiteSpace(WellId))
            {
                _timeLog = AutoTagCreationProcess.GetPrimaryTimeLog(dataService, WellId);
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

            // Load existing tags count from VMX_PHASE_LIST
            if (!string.IsNullOrWhiteSpace(WellId) && dataService.TableExists("VMX_PHASE_LIST"))
            {
                var val = dataService.GetValue($"SELECT COUNT(*) FROM VMX_PHASE_LIST WHERE WELL_ID='{WellId.Replace("'", "''")}';");
                if (val != null && int.TryParse(val.ToString(), out int count))
                {
                    ExistingTagsCount = count;
                }
            }

            // Load detection parameters from eVuMaxTagDetectionSettings if present
            if (dataService.TableExists("eVuMaxTagDetectionSettings"))
            {
                DataTable? dt = dataService.GetTable("SELECT * FROM eVuMaxTagDetectionSettings LIMIT 1;");
                if (dt != null && dt.Rows.Count > 0)
                {
                    StandHeight = DataService.checkNull(dt.Rows[0]["StandHeight"], 120.0);
                    DrillingTimeLimit = DataService.checkNull(dt.Rows[0]["DrillingTimeLimit"], 900);
                    DepthThreshold = DataService.checkNull(dt.Rows[0]["DepthThreshold"], 300.0);
                }
                else
                {
                    // Table exists but has no data - insert default values
                    dataService.ExecuteNonQuery(@"
                        INSERT INTO eVuMaxTagDetectionSettings (StandHeight, DrillingTimeLimit, DepthThreshold)
                        VALUES (120, 900, 300);");
                    StandHeight = 120.0;
                    DrillingTimeLimit = 900;
                    DepthThreshold = 300.0;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessTagCreationViewModel.InitializeMetadata] {ex.Message}");
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
        ProgressStatus = "Preparing auto-tag detection...";
        DetectedTagsCount = 0;

        _cts = new CancellationTokenSource();

        try
        {
            var dataService = _session.GetDataService();
            if (dataService == null)
            {
                HasError = true;
                ErrorMessage = "Database service is not initialized.";
                ProgressStatus = "Detection failed.";
                return;
            }

            if (string.IsNullOrWhiteSpace(WellId))
            {
                HasError = true;
                ErrorMessage = "Target well ID is missing or could not be determined.";
                ProgressStatus = "Detection failed.";
                return;
            }

            // Persist current settings into eVuMaxTagDetectionSettings
            if (dataService.TableExists("eVuMaxTagDetectionSettings"))
            {
                var val = dataService.GetValue("SELECT COUNT(*) FROM eVuMaxTagDetectionSettings;");
                if (val != null && Convert.ToInt64(val) > 0)
                {
                    dataService.ExecuteNonQuery(
                        $"UPDATE eVuMaxTagDetectionSettings SET StandHeight={StandHeight}, DrillingTimeLimit={DrillingTimeLimit}, DepthThreshold={DepthThreshold};");
                }
                else
                {
                    dataService.ExecuteNonQuery(
                        $"INSERT INTO eVuMaxTagDetectionSettings (StandHeight, DrillingTimeLimit, DepthThreshold) VALUES ({StandHeight}, {DrillingTimeLimit}, {DepthThreshold});");
                }
            }

            _currentProcess = new AutoTagCreationProcess
            {
                objDataService = dataService,
                WellID = WellId,
                CustomTimeLog = _timeLog,
                ClearExistingTags = ClearExistingTags
            };

            var progressPercent = new Progress<double>(pct =>
            {
                ProgressPercent = pct;
            });

            var progressStatus = new Progress<string>(status =>
            {
                ProgressStatus = status;
            });

            bool success = await _currentProcess.ExecuteAsync(
                progressPercent,
                progressStatus,
                _cts.Token);

            DetectedTagsCount = _currentProcess.DetectedTagsCount;

            if (success)
            {
                ProgressPercent = 100;
                ProgressStatus = $"Tag detection completed! {DetectedTagsCount} phase tags created.";
                IsCompleted = true;

                // Refresh existing count
                if (dataService.TableExists("VMX_PHASE_LIST"))
                {
                    var val = dataService.GetValue($"SELECT COUNT(*) FROM VMX_PHASE_LIST WHERE WELL_ID='{WellId.Replace("'", "''")}';");
                    if (val != null && int.TryParse(val.ToString(), out int count))
                    {
                        ExistingTagsCount = count;
                    }
                }

                _session.NotifyDataChanged();
            }
            else
            {
                HasError = true;
                ErrorMessage = !string.IsNullOrWhiteSpace(_currentProcess.LastError)
                    ? _currentProcess.LastError
                    : "An unknown error occurred during tag detection.";
                ProgressStatus = "Tag detection failed.";
            }
        }
        catch (OperationCanceledException)
        {
            ProgressStatus = "Tag detection was cancelled.";
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
            _currentProcess = null;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsRunning)
        {
            _cts?.Cancel();
            _currentProcess?.stopProcess();
            ProgressStatus = "Cancelling tag detection...";
        }
        else
        {
            RequestClose?.Invoke(IsCompleted);
        }
    }

    public Func<TagVisualizerViewModel, bool?>? OpenTagVisualizerDialogHandler { get; set; }

    [RelayCommand]
    private void OpenTagVisualizer()
    {
        var vm = new TagVisualizerViewModel(_session, WellId);
        if (OpenTagVisualizerDialogHandler != null)
        {
            OpenTagVisualizerDialogHandler(vm);
        }
        else if (System.Windows.Application.Current != null)
        {
            var win = new Views.TagVisualizerWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            win.ShowDialog();
        }
    }
}

