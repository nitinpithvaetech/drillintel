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

using System.Globalization;
using DrillIntel.Projects;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;

namespace DrillIntel.ViewModels;

public partial class ImportDataViewModel : ObservableObject
{
    private readonly CsvImportService _importService;
    private readonly IWellDataRepository _repository;
    private readonly ProjectSession _session;
    private readonly IImportDepthLogService _depthLogService;

    public event EventHandler? RequestClose;

    public ObservableCollection<string> AvailableWorksheets { get; } = new();

    private string? _selectedWorksheet;
    public string? SelectedWorksheet
    {
        get => _selectedWorksheet;
        set
        {
            if (SetProperty(ref _selectedWorksheet, value))
            {
                if (IsFileUploaded && !IsLoading && !string.IsNullOrEmpty(value))
                {
                    _ = RefreshPreviewAsync(reloadWorksheets: false);
                }
            }
        }
    }

    private bool _hasMultipleWorksheets;
    public bool HasMultipleWorksheets
    {
        get => _hasMultipleWorksheets;
        set => SetProperty(ref _hasMultipleWorksheets, value);
    }

    public ImportDataViewModel(ProjectSession session)
    {
        _session = session;
        _importService = new CsvImportService();
        _repository = new WellDataRepository(_session);
        _depthLogService = new ImportDepthLogService(_repository);

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

            var depthLogs = await _repository.GetDepthLogsAsync();
            ExistingDepthLogs.Clear();
            foreach (var dl in depthLogs)
            {
                ExistingDepthLogs.Add(dl);
            }
            if (SelectedExistingDepthLog == null && ExistingDepthLogs.Count > 0)
            {
                SelectedExistingDepthLog = ExistingDepthLogs.FirstOrDefault();
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
            OnPropertyChanged(nameof(IsNewDataOperation));
            OnPropertyChanged(nameof(IsUpdateDataOperation));
            OnPropertyChanged(nameof(StartImportButtonText));
            OnPropertyChanged(nameof(MappingSubtitle));
            OnPropertyChanged(nameof(ShowDataAssociationTab));
            OnPropertyChanged(nameof(ShowDataAssociationUpdateTab));
            OnPropertyChanged(nameof(ShowDateFormatTab));
            OnPropertyChanged(nameof(ShowDataComparisionTab));
            OnPropertyChanged(nameof(ShowFinishTab));
        }
    }

    public bool IsNewDataOperation
    {
        get => OperationType == OperationType.NewData;
        set
        {
            if (value && OperationType != OperationType.NewData)
            {
                OperationType = OperationType.NewData;
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
    }

    public bool IsUpdateDataOperation
    {
        get => OperationType == OperationType.UpdateData;
        set
        {
            if (value && OperationType != OperationType.UpdateData)
            {
                OperationType = OperationType.UpdateData;
                if (SelectedExistingDepthLog == null && ExistingDepthLogs.Count > 0)
                {
                    SelectedExistingDepthLog = ExistingDepthLogs.FirstOrDefault();
                }
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
    }

    public ObservableCollection<DepthLog> ExistingDepthLogs { get; } = new();

    private DepthLog? _selectedExistingDepthLog;
    public DepthLog? SelectedExistingDepthLog
    {
        get => _selectedExistingDepthLog;
        set
        {
            if (SetProperty(ref _selectedExistingDepthLog, value))
            {
                if (value != null)
                {
                    Settings.UpdatedTimelogId = value.ObjectID;
                    LogName = value.nameLog;
                }
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
    }

    public string StartImportButtonText => OperationType == OperationType.UpdateData ? "UPDATE LOG" : "START IMPORT";

    public string MappingSubtitle => OperationType == OperationType.UpdateData
        ? "Update Mode: Only mapped VuMax channels will be updated. Unmapped existing columns retain current database values. No new columns will be created."
        : "We have auto-mapped standard VuMax channels. Unmapped columns will be created as dynamic custom fields.";

    public bool ShowDateTimeSettings => TypeOfDataInput == ImportDataType.TimeLogData;
    public bool IsDepthLog => TypeOfDataInput == ImportDataType.DepthLogData;
    public bool IsTimeLog => TypeOfDataInput == ImportDataType.TimeLogData;

    public ImportDataType TypeOfDataInput
    {
        get => Settings.TypeOfDataInput;
        set
        {
            if (Settings.TypeOfDataInput == value) return;
            Settings.TypeOfDataInput = value;
            if (value == ImportDataType.DepthLogData)
            {
                if (!Settings.ColumnHeadingRow.HasValue || Settings.ColumnHeadingRow <= 0)
                    Settings.ColumnHeadingRow = 1;
                if (!Settings.ImportFromRow.HasValue || Settings.ImportFromRow <= 0)
                    Settings.ImportFromRow = 2;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowDateTimeSettings));
            OnPropertyChanged(nameof(IsDepthLog));
            OnPropertyChanged(nameof(IsTimeLog));
            OnPropertyChanged(nameof(ImportFromRow));
            OnPropertyChanged(nameof(ColumnHeadingRow));
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
        set
        {
            if (Settings.ColumnDelimiter == value) return;
            Settings.ColumnDelimiter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowOtherDelimiterBox));
            if (IsFileUploaded && !IsLoading)
            {
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
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

    public int? ImportFromRow
    {
        get => Settings.ImportFromRow;
        set
        {
            if (Settings.ImportFromRow == value) return;
            Settings.ImportFromRow = value;
            OnPropertyChanged();
            if (IsFileUploaded && !IsLoading && value.HasValue && value.Value >= 1)
            {
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
    }

    public int? ColumnHeadingRow
    {
        get => Settings.ColumnHeadingRow;
        set
        {
            if (Settings.ColumnHeadingRow == value) return;
            Settings.ColumnHeadingRow = value;
            OnPropertyChanged();
            if (IsFileUploaded && !IsLoading && value.HasValue && value.Value >= 1)
            {
                _ = RefreshPreviewAsync(reloadWorksheets: false);
            }
        }
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
        {
            CurrentTab = WizardTab.Mapping;
        }
        else if (CurrentTab == WizardTab.Mapping)
        {
            if (TypeOfDataInput == ImportDataType.DepthLogData)
            {
                if (!ColumnHeadingRow.HasValue || ColumnHeadingRow.Value <= 0)
                {
                    MessageBox.Show(
                        "Please specify 'Column Heading Row'. Column Heading Row is mandatory for Depthlog import.",
                        "Import Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }

                if (!ImportFromRow.HasValue || ImportFromRow.Value <= 0)
                {
                    MessageBox.Show(
                        "Please specify 'Import from Row'. Import from Row is mandatory for Depthlog import.",
                        "Import Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }

                var depthMapping = ColumnMappings.FirstOrDefault(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
                if (depthMapping == null || string.IsNullOrWhiteSpace(depthMapping.SourceColumnName))
                {
                    MessageBox.Show(
                        "You must map and select DEPTH channel. Please map and select the depth channel to continue",
                        "Import Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }
            }
            CurrentTab = WizardTab.Finalize;
        }
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
        var filter = "All Supported Files (*.csv;*.txt;*.las;*.witsml;*.xml;*.xlsx;*.xls)|*.csv;*.txt;*.las;*.witsml;*.xml;*.xlsx;*.xls|CSV / Text Files (*.csv;*.txt)|*.csv;*.txt|Excel Files (*.xlsx;*.xls)|*.xlsx;*.xls|LAS Files (*.las)|*.las|WITSML Files (*.witsml;*.xml)|*.witsml;*.xml|All Files (*.*)|*.*";

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

        ColumnMappings.Clear();
        await RefreshPreviewAsync(reloadWorksheets: true);
    }

    private async Task RefreshPreviewAsync(bool reloadWorksheets = false)
    {
        if (string.IsNullOrEmpty(FileName)) return;

        try 
        {
            IsLoading = true;
            ImportProgressStatus = "Reading file headers...";

            var ext = System.IO.Path.GetExtension(FileName).ToLowerInvariant();
            bool isExcel = ext == ".xlsx" || ext == ".xls";

            if (reloadWorksheets && isExcel)
            {
                var sheets = await Task.Run(() => DrillIntel.Services.Readers.ExcelDepthReader.GetWorksheetNames(FileName));
                AvailableWorksheets.Clear();
                foreach (var s in sheets) AvailableWorksheets.Add(s);
                HasMultipleWorksheets = AvailableWorksheets.Count > 1;
                SelectedWorksheet = AvailableWorksheets.FirstOrDefault();
            }
            else if (!isExcel)
            {
                AvailableWorksheets.Clear();
                HasMultipleWorksheets = false;
                SelectedWorksheet = null;
            }

            var effectiveDelimiter = ColumnDelimiter switch
            {
                DelimiterChar.Tab => "\t",
                DelimiterChar.Other => !string.IsNullOrEmpty(OtherColumnDelimiter) ? OtherColumnDelimiter : ",",
                _ => ","
            };

            int headingRowToUse = (ColumnHeadingRow.HasValue && ColumnHeadingRow.Value >= 1) ? ColumnHeadingRow.Value : 1;
            int importRowToUse = (ImportFromRow.HasValue && ImportFromRow.Value >= 1) ? ImportFromRow.Value : 2;

            var (headers, previewRows) = await Task.Run(() =>
            {
                var h = _depthLogService.GetHeaders(FileName, headingRowToUse, SelectedWorksheet, effectiveDelimiter);
                var r = _depthLogService.GetPreviewRows(FileName, importRowToUse, 100, SelectedWorksheet, effectiveDelimiter);
                return (h, r);
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

            var sourceOptions = new List<string> { "" };
            sourceOptions.AddRange(headers);

            if (TypeOfDataInput == ImportDataType.DepthLogData && OperationType == OperationType.UpdateData && SelectedExistingDepthLog != null)
            {
                var existingCols = await _repository.GetTableColumnsAsync(SelectedExistingDepthLog.__dataTableName);
                var targetCols = existingCols.Where(c => !c.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase)).ToList();

                if (!targetCols.Any(c => c.Equals("DEPTH", StringComparison.OrdinalIgnoreCase)))
                {
                    targetCols.Insert(0, "DEPTH");
                }

                var previousSelections = ColumnMappings.ToDictionary(m => m.VuMaxColumnID, m => m.SourceColumnName, StringComparer.OrdinalIgnoreCase);

                ColumnMappings.Clear();
                foreach (var colName in targetCols)
                {
                    var row = new ColumnMappingRow { VuMaxColumnID = colName };
                    foreach (var s in sourceOptions) row.AvailableSourceColumns.Add(s);

                    if (previousSelections.TryGetValue(colName, out var prev) && !string.IsNullOrEmpty(prev) && headers.Contains(prev))
                    {
                        row.SourceColumnName = prev;
                    }
                    else
                    {
                        // Mapping Rules:
                        // "If the VuMax column name matches the imported file column name, auto-map and update.
                        // If no match is found, keep current logic as it is."
                        var dictMatch = headers.FirstOrDefault(h =>
                            h.Equals(colName, StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith(colName + "#", StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith(colName + "_", StringComparison.OrdinalIgnoreCase) ||
                            (colName.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) && (
                                h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("DMEA", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("MD", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("MeasuredDepth", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("Measured Depth", StringComparison.OrdinalIgnoreCase))));

                        if (dictMatch != null)
                        {
                            row.SourceColumnName = dictMatch;
                        }
                    }

                    ColumnMappings.Add(row);
                }
            }
            else if (ColumnMappings.Count == 0)
            {
                var targetOptions = TypeOfDataInput == ImportDataType.DepthLogData
                    ? new List<MappingChannels>
                      {
                          new MappingChannels { Mnemonic = "DEPTH", ChannelName = "Depth" },
                          //new MappingChannels { Mnemonic = "GAMMARAY", ChannelName = "Gamma Ray" },
                          //new MappingChannels { Mnemonic = "HKLD",  ChannelName = "Hookload" },
                          //new MappingChannels { Mnemonic = "WOB",   ChannelName = "Weight on Bit" },
                          //new MappingChannels { Mnemonic = "RPM",   ChannelName = "RPM" },
                          //new MappingChannels { Mnemonic = "SPPA",  ChannelName = "Pump Pressure" },
                          //new MappingChannels { Mnemonic = "TORQ",  ChannelName = "Torque" },
                          //new MappingChannels { Mnemonic = "ROP",   ChannelName = "Rate of Penetration" },
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

                foreach (var t in targetOptions)
                {
                    var row = new ColumnMappingRow { VuMaxColumnID = t.Mnemonic };
                    foreach (var s in sourceOptions) row.AvailableSourceColumns.Add(s);
                    
                    // Auto-map based on exact match or common naming conventions
                    var dictMatch = headers.FirstOrDefault(h => 
                        h.Equals(t.Mnemonic, StringComparison.OrdinalIgnoreCase) ||
                        h.StartsWith(t.Mnemonic + "#", StringComparison.OrdinalIgnoreCase) ||
                        h.StartsWith(t.Mnemonic + "_", StringComparison.OrdinalIgnoreCase) ||
                        (t.Mnemonic == "DEPTH" && (
                            h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) || 
                            h.Equals("DMEA", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("MD", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("MeasuredDepth", StringComparison.OrdinalIgnoreCase) ||
                            h.Equals("Measured Depth", StringComparison.OrdinalIgnoreCase))));

                    if (dictMatch != null)
                    {
                        row.SourceColumnName = dictMatch;
                        t.MappedMnemonic = dictMatch;
                    }
                    
                    row.PropertyChanged += (sender, args) =>
                    {
                        if (args.PropertyName == nameof(ColumnMappingRow.SourceColumnName))
                        {
                            var changedRow = (ColumnMappingRow)sender!;
                            if (!string.IsNullOrEmpty(changedRow.SourceColumnName))
                            {
                                foreach (var other in ColumnMappings)
                                {
                                    if (!ReferenceEquals(other, changedRow) && other.SourceColumnName == changedRow.SourceColumnName)
                                    {
                                        other.SourceColumnName = "";
                                    }
                                }
                            }
                        }
                    };

                    ColumnMappings.Add(row);
                }
            }
            else
            {
                // Preserve existing user selection or explicit mapping
                foreach (var row in ColumnMappings)
                {
                    var currentSelection = row.SourceColumnName;
                    row.AvailableSourceColumns.Clear();
                    foreach (var s in sourceOptions) row.AvailableSourceColumns.Add(s);

                    if (!string.IsNullOrEmpty(currentSelection) && headers.Contains(currentSelection))
                    {
                        row.SourceColumnName = currentSelection;
                    }
                    else if (string.IsNullOrEmpty(row.SourceColumnName))
                    {
                        var dictMatch = headers.FirstOrDefault(h => 
                            h.Equals(row.VuMaxColumnID, StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith(row.VuMaxColumnID + "#", StringComparison.OrdinalIgnoreCase) ||
                            h.StartsWith(row.VuMaxColumnID + "_", StringComparison.OrdinalIgnoreCase) ||
                            (row.VuMaxColumnID == "DEPTH" && (
                                h.Equals("DEPT", StringComparison.OrdinalIgnoreCase) || 
                                h.Equals("DMEA", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("MD", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("MeasuredDepth", StringComparison.OrdinalIgnoreCase) ||
                                h.Equals("Measured Depth", StringComparison.OrdinalIgnoreCase))));

                        if (dictMatch != null)
                        {
                            row.SourceColumnName = dictMatch;
                        }
                    }
                }
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

    private async void UploadMappingFile()
    {
        var dlg = new OpenFileDialog { Filter = "Mapping files (*.vmf;*.json;*.csv)|*.vmf;*.json;*.csv|VMF files (*.vmf)|*.vmf|JSON files (*.json)|*.json|CSV files (*.csv)|*.csv|All files (*.*)|*.*" };
        if (dlg.ShowDialog() == true)
        {
            MappingFileName = dlg.FileName;
            await ApplyMappingFileAsync(dlg.FileName);
        }
    }

    private async Task ApplyMappingFileAsync(string mappingFilePath)
    {
        try
        {
            var mappingResult = _depthLogService.LoadMappingFile(mappingFilePath);
            if (!mappingResult.Success)
            {
                MessageBox.Show(mappingResult.ErrorMessage ?? "Failed to load mapping file.", "Mapping Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool needPreviewRefresh = false;

            if (mappingResult.ColumnHeadingRow.HasValue && mappingResult.ColumnHeadingRow.Value != ColumnHeadingRow)
            {
                Settings.ColumnHeadingRow = mappingResult.ColumnHeadingRow.Value;
                OnPropertyChanged(nameof(ColumnHeadingRow));
                needPreviewRefresh = true;
            }

            if (mappingResult.ImportFromRow.HasValue && mappingResult.ImportFromRow.Value != ImportFromRow)
            {
                Settings.ImportFromRow = mappingResult.ImportFromRow.Value;
                OnPropertyChanged(nameof(ImportFromRow));
                needPreviewRefresh = true;
            }

            // For Depthlog imports, DateTime settings must be ignored
            if (TypeOfDataInput != ImportDataType.DepthLogData)
            {
                if (mappingResult.DatetimeSeparator != null)
                    DatetimeSeparator = mappingResult.DatetimeSeparator;

                if (mappingResult.DateTimeInSeparateCol.HasValue)
                    IsDatetimeInSeperatorColumn = mappingResult.DateTimeInSeparateCol.Value;

                if (mappingResult.DateColNo.HasValue)
                    DateColNo = mappingResult.DateColNo.Value;

                if (mappingResult.TimeColNo.HasValue)
                    TimeColNo = mappingResult.TimeColNo.Value;

                if (!string.IsNullOrEmpty(mappingResult.DateFormat))
                {
                    if (mappingResult.DateFormat.Contains("ISO", StringComparison.OrdinalIgnoreCase))
                        DateFormat = DateFormatType.ISOFormat;
                    else if (mappingResult.DateFormat.Contains("dd", StringComparison.OrdinalIgnoreCase))
                        DateFormat = DateFormatType.DDMMYYYYFormat;
                    else if (mappingResult.DateFormat.Contains("MM", StringComparison.OrdinalIgnoreCase))
                        DateFormat = DateFormatType.MMDDYYYYFormat;
                }
            }

            if (needPreviewRefresh && !string.IsNullOrEmpty(FileName))
            {
                await RefreshPreviewAsync(reloadWorksheets: false);
            }

            var sourceOptions = new List<string> { "" };
            sourceOptions.AddRange(PreviewColumns);

            // 1. TargetToColumnIndex mappings (e.g. from .vmf DEPTH~0, DBPOS~2)
            foreach (var kvp in mappingResult.TargetToColumnIndex)
            {
                var targetMnemonic = kvp.Key;
                var colIndex = kvp.Value;
                if (colIndex >= 0 && colIndex < PreviewColumns.Count)
                {
                    var sourceColName = PreviewColumns[colIndex];
                    var existing = ColumnMappings.FirstOrDefault(m => m.VuMaxColumnID.Equals(targetMnemonic, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.SourceColumnName = sourceColName;
                    }
                    else
                    {
                        var newRow = new ColumnMappingRow { VuMaxColumnID = targetMnemonic, SourceColumnName = sourceColName };
                        foreach (var s in sourceOptions) newRow.AvailableSourceColumns.Add(s);
                        ColumnMappings.Add(newRow);
                    }
                }
            }

            // 2. TargetToSourceColumn mappings (e.g. from JSON or CSV)
            foreach (var kvp in mappingResult.TargetToSourceColumn)
            {
                var targetMnemonic = kvp.Key;
                var sourceName = kvp.Value;
                var matchedCol = PreviewColumns.FirstOrDefault(c => c.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
                if (matchedCol != null)
                {
                    var existing = ColumnMappings.FirstOrDefault(m => m.VuMaxColumnID.Equals(targetMnemonic, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.SourceColumnName = matchedCol;
                    }
                    else
                    {
                        var newRow = new ColumnMappingRow { VuMaxColumnID = targetMnemonic, SourceColumnName = matchedCol };
                        foreach (var s in sourceOptions) newRow.AvailableSourceColumns.Add(s);
                        ColumnMappings.Add(newRow);
                    }
                }
            }

            MessageBox.Show($"Loaded mapping file successfully: {System.IO.Path.GetFileName(mappingFilePath)}", "Mapping Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error applying mapping file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(FileName))
        {
            MessageBox.Show("Please select a file first.");
            return;
        }

        if (TypeOfDataInput == ImportDataType.DepthLogData)
        {
            if (!ColumnHeadingRow.HasValue || ColumnHeadingRow.Value <= 0)
            {
                MessageBox.Show(
                    "Please specify 'Column Heading Row'. Column Heading Row is mandatory for Depthlog import.",
                    "Import Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            if (!ImportFromRow.HasValue || ImportFromRow.Value <= 0)
            {
                MessageBox.Show(
                    "Please specify 'Import from Row'. Import from Row is mandatory for Depthlog import.",
                    "Import Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            var depthMapping = ColumnMappings.FirstOrDefault(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
            if (depthMapping == null || string.IsNullOrWhiteSpace(depthMapping.SourceColumnName))
            {
                MessageBox.Show(
                    "You must map and select DEPTH channel. Please map and select the depth channel to continue",
                    "Import Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }
        }

        IsLoading = true;
        IsIndeterminateProgress = true;
        ImportProgressPercent = 0;
        ImportProgressStatus = "Preparing table and import stream...";

        try
        {
            bool isDepthLog = TypeOfDataInput == ImportDataType.DepthLogData;

            if (isDepthLog && OperationType == OperationType.UpdateData)
            {
                if (SelectedExistingDepthLog == null)
                {
                    MessageBox.Show("Please select an existing DepthLog to update.", "Import Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string updateTargetTableName = SelectedExistingDepthLog.__dataTableName;
                var existingCols = new HashSet<string>(await _repository.GetTableColumnsAsync(updateTargetTableName), StringComparer.OrdinalIgnoreCase);

                // Update Logic:
                // For each mapped column:
                // Take the Source Column value from the uploaded file.
                // Replace/update the value in the corresponding VuMax mapped column.
                // Only update VuMax columns when a valid mapping exists.
                // Restrictions:
                // Do not create any new columns.
                // Do not alter existing column names in the target table (e.g., depthLog11026259#41675093).
                // Only update values in mapped VuMax columns.
                var updateMappings = new List<ChannelMapping>();
                foreach (var mapping in ColumnMappings)
                {
                    if (!string.IsNullOrWhiteSpace(mapping.SourceColumnName) &&
                        existingCols.Contains(mapping.VuMaxColumnID))
                    {
                        updateMappings.Add(new ChannelMapping
                        {
                            CsvColumnHeader = mapping.SourceColumnName,
                            MappedVumaxChannel = mapping.VuMaxColumnID
                        });
                    }
                }

                var updateProgress = new Progress<ImportProgressReport>(report =>
                {
                    ImportProgressStatus = report.StatusMessage;
                    ImportProgressPercent = report.PercentCompleted;
                    IsIndeterminateProgress = report.IsIndeterminate;
                });

                var updateDelimiter = ColumnDelimiter switch
                {
                    DelimiterChar.Tab => "\t",
                    DelimiterChar.Other => !string.IsNullOrEmpty(OtherColumnDelimiter) ? OtherColumnDelimiter : ",",
                    _ => ","
                };

                var updateResult = await Task.Run(async () =>
                {
                    return await _repository.StreamUpdateDepthDataAsync(
                        updateTargetTableName,
                        FileName,
                        updateMappings,
                        ColumnHeadingRow ?? 1,
                        ImportFromRow ?? 2,
                        updateDelimiter,
                        SelectedWorksheet,
                        updateProgress);
                });

                if (updateResult != null)
                {
                    SelectedExistingDepthLog.description = $"QC: {updateResult.QcScore:F1}% • {DateTime.Now:dd-MM-yyyy hh:mm tt}";
                    if (updateResult.MinDepth.HasValue)
                        SelectedExistingDepthLog.startIndex = updateResult.MinDepth.Value.ToString(CultureInfo.InvariantCulture);
                    if (updateResult.MaxDepth.HasValue)
                        SelectedExistingDepthLog.endIndex = updateResult.MaxDepth.Value.ToString(CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(updateResult.StepIncrement))
                        SelectedExistingDepthLog.stepIncrement = updateResult.StepIncrement;
                    if (!string.IsNullOrEmpty(updateResult.LastDataIndex))
                        SelectedExistingDepthLog.lastDataIndex = updateResult.LastDataIndex;

                    await _repository.LogDepthLogAsync(SelectedExistingDepthLog);
                    _session.NotifyDataChanged();

                    MessageBox.Show($"Update successful!\n\nRecords Processed: {updateResult.TotalRows:N0}\nQC Score: {updateResult.QcScore:F1}%\nTarget Table: {updateTargetTableName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                ColumnMappings.Clear();
                FileName = string.Empty;
                LogName = string.Empty;
                IsFileUploaded = false;

                RequestClose?.Invoke(this, EventArgs.Empty);
                return;
            }

            var activeMappings = new List<ChannelMapping>();
            var mappedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mappedVuMaxTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Process mapped VuMax channels first (prioritizing DEPTH channel)
            var orderedMappings = ColumnMappings
                .OrderByDescending(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var mapping in orderedMappings)
            {
                if (!string.IsNullOrWhiteSpace(mapping.SourceColumnName) &&
                    PreviewColumns.Any(c => c.Equals(mapping.SourceColumnName, StringComparison.OrdinalIgnoreCase)))
                {
                    var matchedCol = PreviewColumns.First(c => c.Equals(mapping.SourceColumnName, StringComparison.OrdinalIgnoreCase));
                    if (!mappedSources.Contains(matchedCol) && !mappedVuMaxTargets.Contains(mapping.VuMaxColumnID))
                    {
                        activeMappings.Add(new ChannelMapping
                        {
                            CsvColumnHeader = matchedCol,
                            MappedVumaxChannel = mapping.VuMaxColumnID
                        });
                        mappedSources.Add(matchedCol);
                        mappedVuMaxTargets.Add(mapping.VuMaxColumnID);
                    }
                }
            }

            // 2. Process unmapped imported columns (dynamically create new columns in target table)
            foreach (var col in PreviewColumns)
            {
                // If mapped to a VuMax channel, do NOT create or keep the original imported column
                if (mappedSources.Contains(col))
                    continue;

                // If this unmapped column name conflicts with an already claimed target VuMax channel,
                // skip it to prevent creating duplicate column in table
                if (mappedVuMaxTargets.Contains(col))
                    continue;

                activeMappings.Add(new ChannelMapping
                {
                    CsvColumnHeader = col,
                    MappedVumaxChannel = col
                });
            }

            var effectiveWellName = !string.IsNullOrWhiteSpace(ProjectWellName) && ProjectWellName != "Loading Well..."
                ? ProjectWellName.Trim()
                : (!string.IsNullOrWhiteSpace(NewWellName) ? NewWellName.Trim() : "Project Well");
            var finalLogName = string.IsNullOrWhiteSpace(LogName) ? System.IO.Path.GetFileNameWithoutExtension(FileName) : LogName;

            await _repository.EnsureWellAsync(effectiveWellName);

            string targetTableName;
            DepthLog? depthLog = null;
            TimeLog? timeLog = null;

            if (isDepthLog)
            {
                depthLog = new DepthLog
                {
                    ObjectID = Guid.NewGuid().ToString(),
                    nameLog = finalLogName,
                    nameWell = effectiveWellName,
                    __WellName = effectiveWellName,
                    comments = "Success",
                    creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
                };

                // Populate LogCurves from activeMappings so AddDepthLog creates the schema and VMX_DEPTH_LOG_COLUMNS
                int order = 1;
                var distinctMnemonic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var map in activeMappings)
                {
                    var targetChannel = map.MappedVumaxChannel;
                    var safeMnemonic = WellDataRepository.SanitizeIdentifier(targetChannel, order - 1);
                    var finalMnemonic = safeMnemonic;
                    int suffix = 1;
                    while (distinctMnemonic.Contains(finalMnemonic))
                    {
                        finalMnemonic = $"{safeMnemonic}_{suffix++}";
                    }
                    distinctMnemonic.Add(finalMnemonic);

                    var channel = new LogChannel
                    {
                        mnemonic = finalMnemonic,
                        curveDescription = map.CsvColumnHeader,
                        typeLogData = "Double",
                        unit = finalMnemonic.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : "",
                        ColumnOrder = order++,
                        witsmlMnemonic = finalMnemonic
                    };
                    depthLog.LogCurves[finalMnemonic] = channel;
                }

                // Call public static bool AddDepthLog(IDataServiceDIntel objDataService, DepthLog objDepthLog, ref string LastError)
                string lastError = string.Empty;
                var dataService = _session.GetDataService();
                bool addSuccess = DepthLogService.AddDepthLog(dataService, depthLog, ref lastError);
                if (!addSuccess)
                {
                    MessageBox.Show($"Failed to initialize DepthLog using AddDepthLog: {lastError}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                targetTableName = depthLog.__dataTableName;
            }
            else
            {
                timeLog = new TimeLog
                {
                    ObjectID = Guid.NewGuid().ToString(),
                    nameLog = finalLogName,
                    nameWell = effectiveWellName,
                    __WellName = effectiveWellName,
                    comments = "Success",
                    creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
                };

                int order = 1;
                var distinctMnemonic = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var map in activeMappings)
                {
                    var targetChannel = map.MappedVumaxChannel == "Dynamic (New Column)" ? map.CsvColumnHeader : map.MappedVumaxChannel;
                    var safeMnemonic = WellDataRepository.SanitizeIdentifier(targetChannel, order - 1);
                    var finalMnemonic = safeMnemonic;
                    int suffix = 1;
                    while (distinctMnemonic.Contains(finalMnemonic))
                    {
                        finalMnemonic = $"{safeMnemonic}_{suffix++}";
                    }
                    distinctMnemonic.Add(finalMnemonic);

                    bool isDateTime = finalMnemonic.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
                                      finalMnemonic.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                                      finalMnemonic.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                                      finalMnemonic.Equals("DATE", StringComparison.OrdinalIgnoreCase);

                    var channel = new LogChannel
                    {
                        mnemonic = finalMnemonic,
                        curveDescription = map.CsvColumnHeader,
                        typeLogData = isDateTime ? "DateTime" : "Double",
                        unit = isDateTime ? "" : (finalMnemonic.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                        ColumnOrder = order++,
                        witsmlMnemonic = finalMnemonic
                    };
                    timeLog.logCurves[finalMnemonic] = channel;
                }

                string lastError = string.Empty;
                var dataService = _session.GetDataService();
                bool addSuccess = TimeLogService.addTimeLog(dataService, timeLog, ref lastError);
                if (!addSuccess)
                {
                    MessageBox.Show($"Failed to initialize TimeLog using addTimeLog: {lastError}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                targetTableName = timeLog.__dataTableName;
            }

            var progress = new Progress<ImportProgressReport>(report =>
            {
                ImportProgressStatus = report.StatusMessage;
                ImportProgressPercent = report.PercentCompleted;
                IsIndeterminateProgress = report.IsIndeterminate;
            });

            var effectiveDelimiter = ColumnDelimiter switch
            {
                DelimiterChar.Tab => "\t",
                DelimiterChar.Other => !string.IsNullOrEmpty(OtherColumnDelimiter) ? OtherColumnDelimiter : ",",
                _ => ","
            };

            // Execute streaming import directly on background thread to keep UI completely responsive
            var importResult = await Task.Run(async () =>
            {
                return await _repository.StreamImportDataAsync(
                    targetTableName,
                    FileName,
                    activeMappings,
                    ColumnHeadingRow ?? 1,
                    ImportFromRow ?? 2,
                    effectiveDelimiter,
                    SelectedWorksheet,
                    progress);
            });

            if (importResult != null)
            {
                if (isDepthLog && depthLog != null)
                {
                    depthLog.description = $"QC: {importResult.QcScore:F1}% • {DateTime.Now:dd-MM-yyyy hh:mm tt}";
                    if (importResult.MinDepth.HasValue)
                        depthLog.startIndex = importResult.MinDepth.Value.ToString(CultureInfo.InvariantCulture);
                    if (importResult.MaxDepth.HasValue)
                        depthLog.endIndex = importResult.MaxDepth.Value.ToString(CultureInfo.InvariantCulture);
                    depthLog.stepIncrement = importResult.StepIncrement ?? "0";
                    depthLog.lastDataIndex = importResult.LastDataIndex ?? "0";
                    depthLog.indexCurve = "DEPTH";
                    depthLog.indexType = "measured depth";

                    await _repository.LogDepthLogAsync(depthLog);
                }
                else if (timeLog != null)
                {
                    timeLog.description = $"QC: {importResult.QcScore:F1}% • {DateTime.Now:dd-MM-yyyy hh:mm tt}";
                    if (!string.IsNullOrEmpty(importResult.MinDate))
                        timeLog.startIndex = importResult.MinDate;
                    if (!string.IsNullOrEmpty(importResult.MaxDate))
                        timeLog.endIndex = importResult.MaxDate;

                    await _repository.LogTimeLogAsync(timeLog);
                }

                _session.NotifyDataChanged();

                MessageBox.Show($"Import successful!\n\nRecords Imported: {importResult.TotalRows:N0}\nQC Score: {importResult.QcScore:F1}%\nTarget Table: {targetTableName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
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
