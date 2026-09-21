using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using DrillIntel.Models;
using DrillIntel.Services;
using DrillIntel.Data;
using System.Data;

using DrillIntel.Projects;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.ViewModels;

public partial class ImportDataViewModel : ObservableObject
{
    private readonly CsvImportService _importService;
    private readonly IWellDataRepository _repository;
    private readonly ProjectSession _session;

    public event EventHandler? RequestClose;

    public ImportDataViewModel(ProjectSession session)
    {
        _session = session;
        _importService = new CsvImportService();
        _repository = new WellDataRepository(_session);

        ExistingWellList = new ObservableCollection<Well>();
        UpdateWellList = new ObservableCollection<Well>();

        InitializeCommands();
        LoadExistingWells();
    }

    [ObservableProperty]
    private string _projectWellName = "Loading Well...";

    private async void LoadExistingWells()
    {
        try
        {
            var projectWell = await _repository.GetProjectWellAsync();
            if (projectWell != null)
            {
                ProjectWellName = projectWell.WellName;
                NewWellName = projectWell.WellName;
                ExistingWellList.Clear();
                ExistingWellList.Add(projectWell);
            }
            else if (!string.IsNullOrWhiteSpace(_session?.ProjectName))
            {
                ProjectWellName = _session.ProjectName;
                NewWellName = _session.ProjectName;
            }
        }
        catch
        {
            // fallback gracefully
        }
    }

    public ImportSettings Settings { get; } = new();

    private WizardTab _currentTab = WizardTab.FileSelection;
    public WizardTab CurrentTab
    {
        get => _currentTab;
        set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsNextVisible));
                OnPropertyChanged(nameof(IsBackVisible));
                OnPropertyChanged(nameof(CanNavigateBack));
                PreviousStepCommand?.NotifyCanExecuteChanged();
                NextStepCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNextVisible => CurrentTab != WizardTab.Finalize;
    public bool IsBackVisible => CurrentTab != WizardTab.FileSelection;

    // ================= DataType tab =================

    public OperationType OperationType
    {
        get => Settings.OperationType;
        set
        {
            if (Settings.OperationType == value) return;
            Settings.OperationType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowDataAssociationTab));
            OnPropertyChanged(nameof(ShowDataAssociationUpdateTab));
            OnPropertyChanged(nameof(ShowDateFormatTab));
            OnPropertyChanged(nameof(ShowDataComparisionTab));
            OnPropertyChanged(nameof(ShowFinishTab));
        }
    }

    public ImportDataType TypeOfDataInput
    {
        get => Settings.TypeOfDataInput;
        set
        {
            if (Settings.TypeOfDataInput == value) return;
            Settings.TypeOfDataInput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowObjectTab));
            OnPropertyChanged(nameof(ShowLogTab));
            OnPropertyChanged(nameof(IsTrajectoryOrMudlog));
            OnPropertyChanged(nameof(ShowFileTypeRestrictedOptions));
            OnPropertyChanged(nameof(WizardTitle));
            OnPropertyChanged(nameof(LogNameHint));
            OnPropertyChanged(nameof(LogNameToolTip));
        }
    }

    public string WizardTitle => TypeOfDataInput == ImportDataType.DepthLogData
        ? "Import Depthlog Wizard"
        : "Import Timelog Wizard";

    public string LogNameHint => TypeOfDataInput == ImportDataType.DepthLogData
        ? "Depthlog Name"
        : "Timelog Name";

    public string LogNameToolTip => TypeOfDataInput == ImportDataType.DepthLogData
        ? "Enter the name of the depthlog."
        : "Enter the name of the timelog.";

    public string LogName
    {
        get => Settings.LogName;
        set { Settings.LogName = value; OnPropertyChanged(); }
    }

    private bool _disableDepthLogOption;
    public bool DisableDepthLogOption
    {
        get => _disableDepthLogOption;
        set => SetProperty(ref _disableDepthLogOption, value);
    }

    // ================= FileType tab =================

    public ImportFileType TypeOfFile
    {
        get => Settings.TypeOfFile;
        set
        {
            if (Settings.TypeOfFile == value) return;
            Settings.TypeOfFile = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowLogTab));
        }
    }

    public DelimiterChar ColumnDelimiter
    {
        get => Settings.ColumnDelimiter;
        set { Settings.ColumnDelimiter = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowOtherDelimiterBox)); }
    }

    public string OtherColumnDelimiter
    {
        get => Settings.OtherColumnDelimiter;
        set { Settings.OtherColumnDelimiter = value; OnPropertyChanged(); }
    }

    public bool ShowOtherDelimiterBox => ColumnDelimiter == DelimiterChar.Other;

    public bool ShowFileTypeRestrictedOptions =>
        TypeOfDataInput != ImportDataType.TrajectoryData && TypeOfDataInput != ImportDataType.MudLogData;

    public bool IsTrajectoryOrMudlog =>
        TypeOfDataInput == ImportDataType.TrajectoryData || TypeOfDataInput == ImportDataType.MudLogData;

    public string FileName
    {
        get => Settings.FileName;
        set { Settings.FileName = value; OnPropertyChanged(); }
    }

    private bool _isFileUploaded;
    public bool IsFileUploaded
    {
        get => _isFileUploaded;
        set
        {
            SetProperty(ref _isFileUploaded, value);
            NextStepCommand?.NotifyCanExecuteChanged();
        }
    }

    // ================= ColumnMapping tab =================

    public DrillIntel.Models.MappingType MethodOfColumnMapping
    {
        get => Settings.MethodOfColumnMapping;
        set { Settings.MethodOfColumnMapping = value; OnPropertyChanged(); }
    }

    public string MappingFileName
    {
        get => Settings.MappingFileName;
        set { Settings.MappingFileName = value; OnPropertyChanged(); }
    }

    // ================= MapColumns tab =================

    public int ImportFromRow
    {
        get => Settings.ImportFromRow;
        set { Settings.ImportFromRow = value; OnPropertyChanged(); }
    }

    public int ColumnHeadingRow
    {
        get => Settings.ColumnHeadingRow;
        set { Settings.ColumnHeadingRow = value; OnPropertyChanged(); }
    }

    public bool IsDatetimeInSeperatorColumn
    {
        get => Settings.IsDatetimeInSeperatorColumn;
        set { Settings.IsDatetimeInSeperatorColumn = value; OnPropertyChanged(); }
    }

    public string DatetimeSeparator
    {
        get => Settings.DatetimeSeparator;
        set { Settings.DatetimeSeparator = value; OnPropertyChanged(); }
    }

    public int DateColNo
    {
        get => Settings.DateColNo;
        set { Settings.DateColNo = value; OnPropertyChanged(); }
    }

    public int TimeColNo
    {
        get => Settings.TimeColNo;
        set { Settings.TimeColNo = value; OnPropertyChanged(); }
    }

    public DateFormatType DateFormat
    {
        get => Settings.DateFormat;
        set { Settings.DateFormat = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> PreviewColumns { get; } = new();
    private DataTable _previewRows = new();
    public DataTable PreviewRows
    {
        get => _previewRows;
        set => SetProperty(ref _previewRows, value);
    }
    public ObservableCollection<ColumnMappingRow> ColumnMappings { get; } = new();

    private bool _isDateTimeMappingDone;
    public bool IsDateTimeMappingDone
    {
        get => _isDateTimeMappingDone;
        private set => SetProperty(ref _isDateTimeMappingDone, value);
    }

    private bool _isHDTHMappingDone;
    public bool IsHDTHMappingDone
    {
        get => _isHDTHMappingDone;
        private set => SetProperty(ref _isHDTHMappingDone, value);
    }

    // ================= DataAssociation tab (new data) =================

    public bool ShowDataAssociationTab => OperationType == OperationType.NewData;

    public DataAssociationType DataAssociationValue
    {
        get => Settings.DataAssociation;
        set { Settings.DataAssociation = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowNewWellNameBox)); }
    }

    public bool ShowNewWellNameBox => DataAssociationValue == DataAssociationType.CreateNewWell;

    public string? NewWellName
    {
        get => Settings.NewWellName;
        set { Settings.NewWellName = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Well> ExistingWellList { get; }
    public ObservableCollection<WellboreInfo> ExistingWellboreList { get; } = new();

    public string? ExistingWellID
    {
        get => Settings.ExistingWellID;
        set
        {
            Settings.ExistingWellID = value;
            OnPropertyChanged();
        }
    }

    public string? ExistingWellboreID
    {
        get => Settings.ExistingWellboreID;
        set { Settings.ExistingWellboreID = value; OnPropertyChanged(); }
    }

    // ================= DataAssociationUpdate tab =================

    public bool ShowDataAssociationUpdateTab => OperationType == OperationType.UpdateData;

    public ObservableCollection<Well> UpdateWellList { get; }
    public ObservableCollection<TimeLogInfo> TimeLogList { get; } = new();
    public ObservableCollection<RowMappingUpdate> RowsMappingUpdate { get; } = new();

    public string? UpdateWellId
    {
        get => Settings.UpdateWellId;
        set
        {
            Settings.UpdateWellId = value;
            OnPropertyChanged();
        }
    }

    public string? UpdatedTimelogId
    {
        get => Settings.UpdatedTimelogId;
        set
        {
            Settings.UpdatedTimelogId = value;
            OnPropertyChanged();
        }
    }

    // ================= DateFormat tab =================

    public bool ShowDateFormatTab => OperationType == OperationType.UpdateData;

    public TimeZoneType TimeZoneType
    {
        get => Settings.TimeZoneType;
        set { Settings.TimeZoneType = value; OnPropertyChanged(); }
    }

    public string TimeOffset
    {
        get => Settings.TimeOffset;
        set { Settings.TimeOffset = value; OnPropertyChanged(); }
    }

    public int TxtHH
    {
        get => Settings.TxtHH;
        set { Settings.TxtHH = value; OnPropertyChanged(); }
    }

    public int TxtMM
    {
        get => Settings.TxtMM;
        set { Settings.TxtMM = value; OnPropertyChanged(); }
    }

    // ================= DataComparision tab =================

    public bool ShowDataComparisionTab => OperationType == OperationType.UpdateData;

    public UpdateMethodType UpdateMethod
    {
        get => Settings.UpdateMethod;
        set { Settings.UpdateMethod = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowAdjustDateTimeBox)); }
    }

    public bool ShowAdjustDateTimeBox => IsDateTimeMappingDone && UpdateMethod == UpdateMethodType.DateTimeComaparision;

    public ObservableCollection<ComboData> SideTrackList { get; } = new();

    public ComboData? SideTrack
    {
        get => Settings.SideTrack;
        set { Settings.SideTrack = value; OnPropertyChanged(); }
    }

    public string? SelectedSideTrackId
    {
        get => Settings.SideTrack?.Id;
        set
        {
            var match = SideTrackList.FirstOrDefault(s => s.Id == value);
            if (match != null) SideTrack = match;
        }
    }

    public bool ChkAdjustDateTime
    {
        get => Settings.ChkAdjustDateTime;
        set { Settings.ChkAdjustDateTime = value; OnPropertyChanged(); }
    }

    public string AdjustTimeOffset
    {
        get => Settings.AdjustTimeOffset;
        set { Settings.AdjustTimeOffset = value; OnPropertyChanged(); }
    }

    public int TxtAdjMM
    {
        get => Settings.TxtAdjMM;
        set { Settings.TxtAdjMM = value; OnPropertyChanged(); }
    }

    public int TxtAdjSS
    {
        get => Settings.TxtAdjSS;
        set { Settings.TxtAdjSS = value; OnPropertyChanged(); }
    }

    public string SourceHoleDepth { get => Settings.SourceHoleDepth; set { Settings.SourceHoleDepth = value; OnPropertyChanged(); } }
    public string SourceDateTime { get => Settings.SourceDateTime; set { Settings.SourceDateTime = value; OnPropertyChanged(); } }
    public string TimeLogHoleDepth { get => Settings.TimeLogHoleDepth; set { Settings.TimeLogHoleDepth = value; OnPropertyChanged(); } }
    public string TimeLogDateTime { get => Settings.TimeLogDateTime; set { Settings.TimeLogDateTime = value; OnPropertyChanged(); } }

    // ================= Finish tab =================

    public bool ShowFinishTab => OperationType == OperationType.UpdateData;

    // ================= Object tab (Trajectory / MudLog) =================

    public bool ShowObjectTab => TypeOfDataInput == ImportDataType.TrajectoryData || TypeOfDataInput == ImportDataType.MudLogData;

    public string ObjectTabTitle => TypeOfDataInput == ImportDataType.TrajectoryData ? "Trajectory Log" : "MudLog";

    public string TxtWellID2 { get => Settings.TxtWellID2; set { Settings.TxtWellID2 = value; OnPropertyChanged(); } }
    public string TxtWellboreID2 { get => Settings.TxtWellboreID2; set { Settings.TxtWellboreID2 = value; OnPropertyChanged(); } }
    public string TxtLogID2 { get => Settings.TxtLogID2; set { Settings.TxtLogID2 = value; OnPropertyChanged(); } }
    public string TxtLogName2 { get => Settings.TxtLogName2; set { Settings.TxtLogName2 = value; OnPropertyChanged(); } }

    // ================= Log tab (WITSML Time/Depth log) =================

    public bool ShowLogTab =>
        (TypeOfDataInput == ImportDataType.TimeLogData || TypeOfDataInput == ImportDataType.DepthLogData)
        && TypeOfFile == ImportFileType.WITSMLFile;

    public string TxtW_WellID { get => Settings.TxtW_WellID; set { Settings.TxtW_WellID = value; OnPropertyChanged(); } }
    public string TxtW_WellboreID { get => Settings.TxtW_WellboreID; set { Settings.TxtW_WellboreID = value; OnPropertyChanged(); } }
    public string TxtW_LogID { get => Settings.TxtW_LogID; set { Settings.TxtW_LogID = value; OnPropertyChanged(); } }
    public string TxtW_LogName { get => Settings.TxtW_LogName; set { Settings.TxtW_LogName = value; OnPropertyChanged(); } }

    public ObservableCollection<LogChannelRow> RowsMapChannels { get; } = new();

    // ================= Loading indicator (TopLoadingBar) =================

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                OnPropertyChanged(nameof(CanStartImport));
                OnPropertyChanged(nameof(CanNavigateBack));
                SaveCommand?.NotifyCanExecuteChanged();
                NextStepCommand?.NotifyCanExecuteChanged();
                PreviousStepCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotLoading => !IsLoading;
    public bool CanStartImport => !IsLoading && IsFileUploaded;
    public bool CanNavigateBack => !IsLoading && CurrentTab != WizardTab.FileSelection;

    [ObservableProperty]
    private string _importProgressStatus = string.Empty;

    [ObservableProperty]
    private double _importProgressPercent;

    [ObservableProperty]
    private bool _isIndeterminateProgress = true;

    // =====================================================================
    // Commands
    // =====================================================================

    public RelayCommand NextStepCommand { get; private set; } = null!;
    public RelayCommand PreviousStepCommand { get; private set; } = null!;
    public RelayCommand UploadFileCommand { get; private set; } = null!;
    public RelayCommand<string> DropFileCommand { get; private set; } = null!;
    public AsyncRelayCommand SaveCommand { get; private set; } = null!;
    public RelayCommand UploadMappingFileCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        NextStepCommand = new RelayCommand(NextStep, () => !IsLoading);
        PreviousStepCommand = new RelayCommand(PreviousStep, () => !IsLoading && CurrentTab != WizardTab.FileSelection);
        UploadFileCommand = new RelayCommand(UploadFile, () => !IsLoading);
        DropFileCommand = new RelayCommand<string>(ProcessFile);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsLoading && IsFileUploaded);
        UploadMappingFileCommand = new RelayCommand(UploadMappingFile, () => !IsLoading);
    }
    
    private void NextStep()
    {
        if (CurrentTab == WizardTab.FileSelection && IsFileUploaded)
            CurrentTab = WizardTab.Mapping;
        else if (CurrentTab == WizardTab.Mapping)
            CurrentTab = WizardTab.Finalize;
    }

    private void PreviousStep()
    {
        if (CurrentTab == WizardTab.Finalize)
            CurrentTab = WizardTab.Mapping;
        else if (CurrentTab == WizardTab.Mapping)
            CurrentTab = WizardTab.FileSelection;
    }

    // ----------------- File upload -----------------

    private void UploadFile()
    {
        var filter = "Supported files (*.csv;*.las)|*.csv;*.las|CSV files (*.csv)|*.csv|LAS files (*.las)|*.las|All files (*.*)|*.*";

        var dlg = new OpenFileDialog { Filter = filter };
        if (dlg.ShowDialog() != true) return;

        ProcessFile(dlg.FileName);
    }

    private async void ProcessFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        
        FileName = filePath;
        if (string.IsNullOrWhiteSpace(LogName))
        {
            LogName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        }

        try 
        {
            IsLoading = true;
            ImportProgressStatus = "Reading file headers...";

            var (headers, previewRows) = await Task.Run(() =>
            {
                var rawHeaders = _importService.GetHeaders(FileName);
                var distinctHeaders = new List<string>();
                foreach (var h in rawHeaders)
                {
                    var colName = string.IsNullOrWhiteSpace(h) ? $"Column {distinctHeaders.Count + 1}" : h.Trim();
                    int suffix = 1;
                    var finalColName = colName;
                    while (distinctHeaders.Contains(finalColName))
                    {
                        finalColName = $"{colName}_{suffix++}";
                    }
                    distinctHeaders.Add(finalColName);
                }

                var rows = _importService.GetPreviewRows(FileName, 100);
                return (distinctHeaders, rows);
            });

            PreviewColumns.Clear();
            foreach (var c in headers) PreviewColumns.Add(c);

            var dt = new DataTable();
            foreach (var c in headers) dt.Columns.Add(c);
            
            foreach (var r in previewRows)
            {
                var row = dt.NewRow();
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    if (i < r.Count) row[i] = r[i];
                }
                dt.Rows.Add(row);
            }
            PreviewRows = dt;

            ColumnMappings.Clear();
            var targetOptions = TypeOfDataInput == ImportDataType.DepthLogData
                ? new List<MappingChannels>
                  {
                      new MappingChannels { Mnemonic = "DEPTH", ChannelName = "Depth" },
                  }
                : new List<MappingChannels>
                  {
                      new MappingChannels { Mnemonic = "DEPTH", ChannelName = "Depth" },
                      new MappingChannels { Mnemonic = "HKLD",  ChannelName = "Hookload" },
                      new MappingChannels { Mnemonic = "RPM",   ChannelName = "RPM" },
                      new MappingChannels { Mnemonic = "SPPA",  ChannelName = "Pump Pressure" },
                      new MappingChannels { Mnemonic = "BPOS",  ChannelName = "Block Position" },
                      new MappingChannels { Mnemonic = "CIRC",  ChannelName = "Circulation" },
                      new MappingChannels { Mnemonic = "STOR",  ChannelName = "Surface Torque" },
                      new MappingChannels { Mnemonic = "HDTH",  ChannelName = "Hole Depth" },
                  };
            var sourceOptions = new List<string> { "" };
            sourceOptions.AddRange(headers);
            
            foreach (var t in targetOptions)
            {
                var row = new ColumnMappingRow { VuMaxColumnID = t.Mnemonic };
                foreach (var s in sourceOptions) row.AvailableSourceColumns.Add(s);
                
                // Auto-map based on exact match or common naming conventions
                var dictMatch = headers.FirstOrDefault(h => 
                    h.Equals(t.Mnemonic, StringComparison.OrdinalIgnoreCase) ||
                    h.StartsWith(t.Mnemonic + "#", StringComparison.OrdinalIgnoreCase) ||
                    h.StartsWith(t.Mnemonic + "_", StringComparison.OrdinalIgnoreCase) ||
                    (t.Mnemonic == "DEPTH" && (h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) || h.Equals("DMEA", StringComparison.OrdinalIgnoreCase))));

                if (dictMatch != null)
                {
                    row.SourceColumnName = dictMatch;
                    t.MappedMnemonic = dictMatch;
                }
                
                ColumnMappings.Add(row);
            }

            RowsMappingUpdate.Clear();
            foreach (var c in headers)
                RowsMappingUpdate.Add(new RowMappingUpdate { SourceChannel = c });

            IsFileUploaded = true;
            OnPropertyChanged(nameof(CanStartImport));
            SaveCommand?.NotifyCanExecuteChanged();
        }
        catch(Exception ex)
        {
            MessageBox.Show($"Error loading file headers: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            ImportProgressStatus = string.Empty;
        }
    }

    private void UploadMappingFile()
    {
        var dlg = new OpenFileDialog { Filter = "Mapping files (*.json;*.xml;*.csv)|*.json;*.xml;*.csv|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
            MappingFileName = dlg.FileName;
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(FileName))
        {
            MessageBox.Show("Please select a file first.");
            return;
        }

        IsLoading = true;
        IsIndeterminateProgress = true;
        ImportProgressPercent = 0;
        ImportProgressStatus = "Preparing table and import stream...";

        try
        {
            var activeMappings = new List<ChannelMapping>();
            foreach (var col in PreviewColumns)
            {
                var mapping = ColumnMappings.FirstOrDefault(m => m.SourceColumnName == col);
                if (mapping != null)
                {
                    activeMappings.Add(new ChannelMapping { CsvColumnHeader = col, MappedVumaxChannel = mapping.VuMaxColumnID });
                }
                else
                {
                    activeMappings.Add(new ChannelMapping { CsvColumnHeader = col, MappedVumaxChannel = "Dynamic (New Column)" });
                }
            }

            bool isDepthLog = TypeOfDataInput == ImportDataType.DepthLogData;
            string randomTableName = isDepthLog
                ? $"depthlog_{DateTime.UtcNow:yyyyMMddHHmmss}_{new Random().Next(1000, 9999)}"
                : $"timelog_{DateTime.UtcNow:yyyyMMddHHmmss}_{new Random().Next(1000, 9999)}";

            var progress = new Progress<ImportProgressReport>(report =>
            {
                ImportProgressStatus = report.StatusMessage;
                ImportProgressPercent = report.PercentCompleted;
                IsIndeterminateProgress = report.IsIndeterminate;
            });

            // Execute streaming import directly on background thread to keep UI completely responsive
            var importResult = await Task.Run(async () =>
            {
                return await _repository.StreamImportDataAsync(randomTableName, FileName, activeMappings, progress);
            });

            var effectiveWellName = !string.IsNullOrWhiteSpace(ProjectWellName) && ProjectWellName != "Loading Well..."
                ? ProjectWellName.Trim()
                : (!string.IsNullOrWhiteSpace(NewWellName) ? NewWellName.Trim() : "Project Well");
            var finalLogName = string.IsNullOrWhiteSpace(LogName) ? System.IO.Path.GetFileNameWithoutExtension(FileName) : LogName;

            await _repository.EnsureWellAsync(effectiveWellName);

            if (isDepthLog)
            {
                var log = new VmxDepthLog
                {
                    LogName = finalLogName,
                    WellName = effectiveWellName,
                    DataTableName = randomTableName,
                    ImportStatus = "Success",
                    QcScore = importResult.QcScore,
                    ImportDate = DateTime.Now
                };
                await _repository.LogVmxDepthLogAsync(log);
            }
            else
            {
                var log = new VmxTimeLog
                {
                    LogName = finalLogName,
                    WellName = effectiveWellName,
                    DataTableName = randomTableName,
                    ImportStatus = "Success",
                    QcScore = importResult.QcScore,
                    ImportDate = DateTime.Now
                };
                await _repository.LogVmxTimeLogAsync(log);
            }

            MessageBox.Show($"Import successful!\n\nRecords Imported: {importResult.TotalRows:N0}\nQC Score: {importResult.QcScore:F1}%\nTarget Table: {randomTableName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            ColumnMappings.Clear();
            FileName = string.Empty;
            LogName = string.Empty;
            IsFileUploaded = false;

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during import: {ex.Message}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            ImportProgressStatus = string.Empty;
        }
    }

    private void DownloadLog()
    {
        var dlg = new SaveFileDialog { Filter = "XML files (*.xml)|*.xml", FileName = TxtLogName2 + ".xml" };
        if (dlg.ShowDialog() == true)
        {
            MessageBox.Show("Log downloaded to " + dlg.FileName, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
