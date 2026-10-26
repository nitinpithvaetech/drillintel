using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DrillIntel.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IProjectService _projectService;
    private readonly IRecentProjectsService _recentProjectsService;

    public const string AppTagline = "The rig's second brain — drill smarter, not harder";

    public string WindowTitle => _session.IsProjectOpen 
        ? $"DrillIntel — {AppTagline} [{_session.ProjectName}]"
        : $"DrillIntel — {AppTagline}";

    [ObservableProperty]
    private ObservableObject _currentViewModel;

    public ProjectSession Session => _session;
    public bool IsProjectOpen => _session.IsProjectOpen;

    public ObservableCollection<RecentProject> RecentProjects { get; } = new();

    private readonly DrillIntel.Data.IAppDatabaseService? _appDatabaseService;

    public object? ActiveDocument { get; set; }
    public Action<string, string>? UserAlertHandler { get; set; }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void RecalculateBroomstickPoints()
    {
        OnCalculateBroomstickPointsClicked();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void CalculateBroomstickPoints()
    {
        OnCalculateBroomstickPointsClicked();
    }

    public void OnCalculateBroomstickPointsClicked()
    {
        if (ActiveDocument is DrillIntel.Models.HookloadDocument hookloadDoc)
        {
            hookloadDoc.CalculateBroomstickPoints();
        }
        else
        {
            if (UserAlertHandler != null)
            {
                UserAlertHandler("Please select an active Hookload Document to calculate Broomstick points.", "Broomstick Calculation");
            }
            else
            {
                _ = ExecuteProjectWideBroomstickRecalculationAsync();
            }
        }
    }

    private async Task ExecuteProjectWideBroomstickRecalculationAsync()
    {
        if (!_session.IsProjectOpen)
        {
            MessageBox.Show("Please open a project before recalculating Broomstick points.", "Broomstick Calculation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            DrillIntel.Models.HookloadDocument.ConfirmationMessage,
            DrillIntel.Models.HookloadDocument.DialogTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var repo = new DrillIntel.Data.WellDataRepository(_session);
            var well = await repo.GetProjectWellAsync();
            var timeLogs = await repo.GetTimeLogsAsync();
            var primaryTimeLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
            string wellId = well?.ObjectID ?? primaryTimeLog?.WellID ?? _session.ProjectName ?? "WELL_1";

            var bsDataService = new DrillIntel.Data.BroomstickDataService(_session);
            var bsProcessor = new DrillIntel.Services.BroomstickProcessor(bsDataService, _session);

            await bsDataService.ClearBroomstickPointsAsync(wellId);
            var result = await bsProcessor.RecalculatePointsAsync(wellId);

            _session.NotifyDataChanged();

            if (result.Success)
            {
                MessageBox.Show("Broomstick points recalculation completed successfully.", "Broomstick Calculation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"Broomstick points recalculation completed with warning:\n{result.ErrorMessage}", "Broomstick Calculation", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error calculating broomstick points:\n{ex.Message}", "Broomstick Calculation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public MainViewModel(
        ProjectSession session,
        IProjectService projectService,
        IRecentProjectsService? recentProjectsService = null,
        DrillIntel.Data.IAppDatabaseService? appDatabaseService = null)
    {
        _session = session;
        _projectService = projectService;
        _recentProjectsService = recentProjectsService ?? App.RecentProjectsService;
        _appDatabaseService = appDatabaseService;

        _session.ProjectChanged += OnProjectStateChanged;
        _recentProjectsService.RecentProjectsChanged += OnRecentProjectsChanged;

        RefreshRecentProjects();

        _currentViewModel = _session.IsProjectOpen 
            ? new DashboardViewModel(_session) 
            : new NoProjectViewModel(_session, _projectService, _recentProjectsService);
        if (_currentViewModel is DashboardViewModel initDb)
        {
            initDb.PropertyChanged += OnDashboardPropertyChanged;
        }
    }

    partial void OnCurrentViewModelChanged(ObservableObject? oldValue, ObservableObject newValue)
    {
        if (oldValue is DashboardViewModel oldDb)
        {
            oldDb.PropertyChanged -= OnDashboardPropertyChanged;
        }
        if (newValue is DashboardViewModel newDb)
        {
            newDb.PropertyChanged += OnDashboardPropertyChanged;
        }
        SyncDataToParentTimelogCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSyncDataToParentTimelog));
    }

    private void OnDashboardPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.SelectedNode))
        {
            SyncDataToParentTimelogCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanSyncDataToParentTimelog));
        }
    }

    private void OnRecentProjectsChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(RefreshRecentProjects);
        }
        else
        {
            RefreshRecentProjects();
        }
    }

    private void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var p in _recentProjectsService.GetRecentProjects())
        {
            RecentProjects.Add(p);
        }
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    public bool HasRecentProjects => RecentProjects.Count > 0;

    [RelayCommand]
    private void OpenRecentProject(RecentProject? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.FilePath)) return;
        _projectService.OpenProject(project.FilePath);
    }

    public WellInformationViewModel? LastEditWellViewModel { get; private set; }
    public Func<WellInformationViewModel, bool?>? OpenEditWellDialogHandler { get; set; }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task OpenWellEditor()
    {
        if (!_session.IsProjectOpen) return;

        var repo = new WellDataRepository(_session);
        var currentWell = await repo.GetProjectWellAsync();
        WellInformationViewModel vm;
        if (currentWell != null)
        {
            vm = new WellInformationViewModel(currentWell);
        }
        else
        {
            string suggestedWellName = _session.ProjectName ?? "New Well";
            var timeLogs = await repo.GetTimeLogsAsync();
            var depthLogs = await repo.GetDepthLogsAsync();
            suggestedWellName = timeLogs.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.nameWell))?.nameWell
                             ?? timeLogs.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.__WellName))?.__WellName
                             ?? depthLogs.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.nameWell))?.nameWell
                             ?? depthLogs.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.__WellName))?.__WellName
                             ?? suggestedWellName;
            vm = new WellInformationViewModel(suggestedWellName, "General Field");
        }
        LastEditWellViewModel = vm;

        bool? result = false;
        if (OpenEditWellDialogHandler != null)
        {
            result = OpenEditWellDialogHandler(vm);
        }
        else if (Application.Current != null)
        {
            var window = new DrillIntel.Views.WellInformationWindow
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            result = window.ShowDialog();
        }

        if (result == true)
        {
            var wellToSave = currentWell ?? new Well();
            vm.ApplyToWell(wellToSave);
            if (string.IsNullOrWhiteSpace(wellToSave.ObjectID))
            {
                wellToSave.ObjectID = Guid.NewGuid().ToString();
            }
            if (wellToSave.wellbores.Count == 0)
            {
                var wellboreId = Guid.NewGuid().ToString();
                var wellbore = new Wellbore
                {
                    ObjectID = wellboreId,
                    WellID = wellToSave.ObjectID,
                    nameWell = wellToSave.name,
                    name = wellToSave.name
                };
                wellToSave.wellbores[wellboreId] = wellbore;
                wellToSave.__timeLogWellboreID = wellboreId;
            }
            await repo.SaveProjectWellAsync(wellToSave);
            if (!string.IsNullOrWhiteSpace(_session.ProjectFilePath))
            {
                _recentProjectsService?.AddOrUpdate(_session.ProjectFilePath, wellToSave.name, wellToSave.field);
            }
            _session.NotifyDataChanged();

            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task EditWell()
    {
        await OpenWellEditor();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task OpenTimelogEditor()
    {
        if (!_session.IsProjectOpen) return;
        if (CurrentViewModel is DashboardViewModel dashboardVm)
        {
            await dashboardVm.EditTimeLogAsync();
        }
        else
        {
            var repo = new WellDataRepository(_session);
            var timeLogs = await repo.GetTimeLogsAsync();
            var targetLog = timeLogs.FirstOrDefault();
            if (targetLog != null)
            {
                var vm = new EditTimeLogViewModel(_session, repo, targetLog.ObjectID, targetLog);
                await vm.InitializeAsync();
                var window = new DrillIntel.Views.EditTimeLogWindow
                {
                    DataContext = vm,
                    Owner = Application.Current?.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    _session.NotifyDataChanged();
                }
            }
            else
            {
                MessageBox.Show("No Timelog available to edit. Please import a timelog first.", "Edit Timelog", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task OpenDepthlogEditor()
    {
        if (!_session.IsProjectOpen) return;
        if (CurrentViewModel is DashboardViewModel dashboardVm)
        {
            await dashboardVm.EditDepthLogAsync();
        }
        else
        {
            var repo = new WellDataRepository(_session);
            var depthLogs = await repo.GetDepthLogsAsync();
            var targetLog = depthLogs.FirstOrDefault();
            if (targetLog != null)
            {
                var vm = new EditDepthLogViewModel(_session, repo, targetLog.ObjectID, targetLog);
                await vm.InitializeAsync();
                var window = new DrillIntel.Views.EditDepthLogWindow
                {
                    DataContext = vm,
                    Owner = Application.Current?.MainWindow
                };
                if (window.ShowDialog() == true)
                {
                    _session.NotifyDataChanged();
                }
            }
            else
            {
                MessageBox.Show("No Depthlog available to edit. Please import a depth log first.", "Edit Depthlog", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void OnProjectStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsProjectOpen));
        OnPropertyChanged(nameof(WindowTitle));
        NavigateToDashboardCommand.NotifyCanExecuteChanged();
        NavigateToImportTimelogCommand.NotifyCanExecuteChanged();
        NavigateToImportDepthlogCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        IdentifyRigStatesCommand.NotifyCanExecuteChanged();
        RecalculateRigStateCommand.NotifyCanExecuteChanged();
        ProcessTagCreationCommand.NotifyCanExecuteChanged();
        ViewTagGraphCommand.NotifyCanExecuteChanged();
        OpenRigStateMasterCommand.NotifyCanExecuteChanged();
        OpenGlobalRigStateMasterCommand.NotifyCanExecuteChanged();
        OpenSystemSettingsCommand.NotifyCanExecuteChanged();
        OpenProjectRigStateMasterCommand.NotifyCanExecuteChanged();
        OpenUnitMasterCommand.NotifyCanExecuteChanged();
        OpenWellEditorCommand.NotifyCanExecuteChanged();
        EditWellCommand.NotifyCanExecuteChanged();
        OpenTimelogEditorCommand.NotifyCanExecuteChanged();
        OpenDepthlogEditorCommand.NotifyCanExecuteChanged();
        OpenRigStateDocumentCommand.NotifyCanExecuteChanged();
        ManageRigStateDocumentsCommand.NotifyCanExecuteChanged();
        NewRigStateDocumentCommand.NotifyCanExecuteChanged();
        SyncDataToParentTimelogCommand.NotifyCanExecuteChanged();
        OpenProjectBroomstickSettingsCommand.NotifyCanExecuteChanged();
        OpenBroomstickSettingsCommand.NotifyCanExecuteChanged();
        RecalculateBroomstickPointsCommand.NotifyCanExecuteChanged();
        CalculateBroomstickPointsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSyncDataToParentTimelog));

        if (Application.Current?.Dispatcher != null)
        {
            if (Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                Application.Current.Dispatcher.InvokeAsync(System.Windows.Input.CommandManager.InvalidateRequerySuggested);
            }
        }

        if (_session.IsProjectOpen)
        {
            CurrentViewModel = new DashboardViewModel(_session);
        }
        else
        {
            CurrentViewModel = new NoProjectViewModel(_session, _projectService, _recentProjectsService);
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NavigateToDashboard()
    {
        if (CurrentViewModel is not DashboardViewModel)
        {
            CurrentViewModel = new DashboardViewModel(_session);
        }
        else if (CurrentViewModel is DashboardViewModel dashboard)
        {
            await dashboard.RefreshAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NavigateToImportTimelog()
    {
        var well = await EnsureProjectWellAsync();
        if (well == null) return;

        var vm = new ImportDataViewModel(_session);
        var window = new DrillIntel.Views.ImportDataWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        vm.RequestClose += (s, e) =>
        {
            window.DialogResult = true;
        };
        window.ShowDialog();
        if (CurrentViewModel is DashboardViewModel dashboard)
        {
            await dashboard.RefreshAsync();
        }
        else
        {
            CurrentViewModel = new DashboardViewModel(_session);
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NavigateToImportDepthlog()
    {
        var well = await EnsureProjectWellAsync();
        if (well == null) return;

        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = DrillIntel.Models.ImportDataType.DepthLogData
        };
        var window = new DrillIntel.Views.ImportDataWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        vm.RequestClose += (s, e) =>
        {
            window.DialogResult = true;
        };
        window.ShowDialog();
        if (CurrentViewModel is DashboardViewModel depthDashboard)
        {
            await depthDashboard.RefreshAsync();
        }
        else
        {
            CurrentViewModel = new DashboardViewModel(_session);
        }
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (await _projectService.CreateNewProjectAsync())
        {
            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
        }
    }

    [RelayCommand]
    private void LoadProject()
    {
        _projectService.OpenProject();
    }

    public async Task<DrillIntel.Data.Objects.DataObjects.Models.Well?> EnsureProjectWellAsync()
    {
        if (!_session.IsProjectOpen) return null;

        var repo = new DrillIntel.Data.WellDataRepository(_session);
        var existingWell = await repo.GetProjectWellAsync();

        if (existingWell != null)
        {
            return existingWell;
        }

        // If project lacks a well in VMX_WELL (e.g. older project), prompt before importing
        string suggestedWellName = _session.ProjectName ?? "New Well";
        var timeLogs = await repo.GetTimeLogsAsync();
        var depthLogs = await repo.GetDepthLogsAsync();
        suggestedWellName = timeLogs.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.nameWell))?.nameWell
                         ?? timeLogs.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.__WellName))?.__WellName
                         ?? depthLogs.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.nameWell))?.nameWell
                         ?? depthLogs.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.__WellName))?.__WellName
                         ?? suggestedWellName;

        var vm = new WellInformationViewModel(suggestedWellName, "General Field");
        var window = new DrillIntel.Views.WellInformationWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            var wellId = Guid.NewGuid().ToString();
            var wellboreId = Guid.NewGuid().ToString();
            var well = new DrillIntel.Data.Objects.DataObjects.Models.Well
            {
                ObjectID = wellId,
                name = vm.WellName.Trim(),
                field = vm.FieldName.Trim(),
                dTimSpud = DateTime.Now.ToString("o")
            };
            var wellbore = new DrillIntel.Data.Objects.DataObjects.Models.Wellbore
            {
                ObjectID = wellboreId,
                WellID = wellId,
                nameWell = vm.WellName.Trim(),
                name = vm.WellName.Trim()
            };
            well.wellbores[wellboreId] = wellbore;
            well.__timeLogWellboreID = wellboreId;

            await repo.SaveProjectWellAsync(well);

            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }

            return well;
        }

        return null;
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void CloseProject()
    {
        _session.Close();
    }

    [RelayCommand]
    private void OpenSystemSettings()
    {
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var dataService = appDb?.GetDataService();
        if (dataService == null)
        {
            MessageBox.Show("Unable to access the Application Database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var vm = new EditSystemSettingsViewModel(dataService);
        var window = new DrillIntel.Views.Common.EditSystemSettings(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenGlobalRigStateMaster()
    {
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var dataService = appDb.GetDataService();
        var contextName = "Application Master Template (Default for New Projects)";

        var vm = new RigStateViewModel(dataService, contextName);
        var window = new DrillIntel.Views.RigStateWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void OpenProjectRigStateMaster()
    {
        if (!_session.IsProjectOpen) return;
        var dataService = _session.GetDataService();
        var contextName = $"Project: {_session.ProjectName}";

        var vm = new RigStateViewModel(dataService, contextName);
        var window = new DrillIntel.Views.RigStateWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenRigStateMaster()
    {
        if (_session.IsProjectOpen)
        {
            OpenProjectRigStateMaster();
        }
        else
        {
            OpenGlobalRigStateMaster();
        }
    }

    [RelayCommand]
    private void OpenGlobalBroomstickSettings()
    {
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var dataService = appDb.GetDataService();
        var contextName = "Application Master Template (Default for New Projects)";

        var vm = new BroomstickProfileEditorViewModel(dataService, contextName, isProjectContext: false);
        var window = new DrillIntel.Views.Broomstick.BroomstickProfileEditorWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private void OpenProjectBroomstickSettings()
    {
        if (!_session.IsProjectOpen) return;
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var appDataService = appDb?.GetDataService();
        var dataService = _session.GetDataService();
        var contextName = $"Project: {_session.ProjectName}";

        var vm = new BroomstickProfileEditorViewModel(dataService, contextName, appDataService: appDataService, isProjectContext: true);
        var window = new DrillIntel.Views.Broomstick.BroomstickProfileEditorWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenBroomstickSettings()
    {
        if (_session.IsProjectOpen)
        {
            OpenProjectBroomstickSettings();
        }
        else
        {
            OpenGlobalBroomstickSettings();
        }
    }

    [RelayCommand]
    private void OpenBroomstickManagePlan()
    {
        var dataService = _session.IsProjectOpen
            ? _session.GetDataService()
            : (_appDatabaseService ?? App.AppDatabaseService)?.GetDataService();

        if (dataService == null) return;

        string wellId = string.Empty;
        string wellboreId = string.Empty;
        string logId = string.Empty;

        if (CurrentViewModel is DashboardViewModel dashVm)
        {
            if (dashVm.SelectedNode?.Tag is TimeLog tl)
            {
                wellId = tl.WellID;
                wellboreId = tl.WellboreID;
                logId = tl.ObjectID;
            }
            else if (dashVm.SelectedNode?.Tag is Wellbore wb)
            {
                wellId = wb.WellID;
                wellboreId = wb.ObjectID;
            }
            else if (dashVm.SelectedWell != null)
            {
                wellId = dashVm.SelectedWell.ObjectID ?? "";
            }
        }

        var vm = new BroomstickPlanManageViewModel(dataService, wellId, wellboreId, logId);
        var window = new DrillIntel.Views.Broomstick.BroomstickPlanManageWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenUnitMaster()
    {
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var dataService = appDb.GetDataService();
        var tableName = DrillIntel.Models.Unit.TableName;
        var contextName = _session.IsProjectOpen
            ? $"Global Unit Master (APP_UNIT_MASTER) — Active Project: {_session.ProjectName}"
            : "Global Unit Master (APP_UNIT_MASTER)";
        var dbPath = appDb.DatabasePath;

        var vm = new UnitMasterViewModel(
            dataService,
            tableName: tableName,
            contextName: contextName,
            isProjectOpen: _session.IsProjectOpen,
            appDatabaseService: appDb,
            databasePath: dbPath,
            projectDataService: null);

        var window = new DrillIntel.Views.UnitMasterWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenUnitConversion()
    {
        var appDb = _appDatabaseService ?? App.AppDatabaseService;
        var dataService = appDb.GetDataService();
        var tableName = DrillIntel.Models.UnitConverter.TableName;
        var contextName = _session.IsProjectOpen
            ? $"Global Unit Conversions (APP_UNIT_CONVERSIONS) — Active Project: {_session.ProjectName}"
            : "Global Unit Conversions (APP_UNIT_CONVERSIONS)";
        var dbPath = appDb.DatabasePath;

        var vm = new UnitConversionMasterViewModel(
            dataService,
            tableName: tableName,
            contextName: contextName,
            isProjectOpen: _session.IsProjectOpen,
            appDatabaseService: appDb,
            databasePath: dbPath,
            projectDataService: null);

        var window = new DrillIntel.Views.UnitConversionMasterWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task IdentifyRigStates()
    {
        if (!_session.IsProjectOpen) return;
        var repo = new DrillIntel.Data.WellDataRepository(_session);
        var timeLogs = await repo.GetTimeLogsAsync();
        var tl = timeLogs.FirstOrDefault();
        if (tl != null)
        {
            var vm = new RecalculateRigStateViewModel(_session, tl);
            var window = new DrillIntel.Views.RecalculateRigStateWindow
            {
                DataContext = vm,
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
        }
        else
        {
            OpenRigStateMaster();
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task RecalculateRigState()
    {
        await IdentifyRigStates();
    }

    public Func<ProcessTagCreationViewModel, bool?>? OpenProcessTagCreationDialogHandler { get; set; }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task ProcessTagCreation()
    {
        if (!_session.IsProjectOpen) return;

        Well? well = null;
        TimeLog? timeLog = null;

        if (CurrentViewModel is DashboardViewModel dashVm)
        {
            if (dashVm.SelectedNode?.Tag is TimeLog tl)
            {
                timeLog = tl;
            }
            else if (dashVm.SelectedNode?.Tag is Well w)
            {
                well = w;
            }
            else if (dashVm.SelectedWell != null)
            {
                well = dashVm.SelectedWell;
            }
        }

        var repo = new WellDataRepository(_session);
        if (well == null)
        {
            well = await repo.GetProjectWellAsync();
        }

        if (timeLog == null)
        {
            var timeLogs = await repo.GetTimeLogsAsync();
            timeLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
        }

        if (well == null && timeLog == null)
        {
            if (UserAlertHandler != null)
            {
                UserAlertHandler("No well or timelog found in the active project to process tag creation.", "Process Tag Creation");
            }
            else
            {
                MessageBox.Show("No well or timelog found in the active project to process tag creation.",
                    "Process Tag Creation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        var vm = new ProcessTagCreationViewModel(_session, well, timeLog);
        bool? result = false;

        if (OpenProcessTagCreationDialogHandler != null)
        {
            result = OpenProcessTagCreationDialogHandler(vm);
        }
        else if (Application.Current != null)
        {
            var window = new Views.ProcessTagCreationWindow
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            result = window.ShowDialog();
        }

        if (result == true)
        {
            _session.NotifyDataChanged();
            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
        }
    }

    public Func<TagVisualizerViewModel, bool?>? OpenTagVisualizerDialogHandler { get; set; }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task ViewTagGraph()
    {
        if (!_session.IsProjectOpen) return;

        string? wellId = null;
        if (CurrentViewModel is DashboardViewModel dashVm)
        {
            if (dashVm.SelectedNode?.Tag is TimeLog tl)
            {
                wellId = tl.WellID;
            }
            else if (dashVm.SelectedNode?.Tag is Well w)
            {
                wellId = w.ObjectID;
            }
        }

        var vm = new TagVisualizerViewModel(_session, wellId);
        bool? result = false;

        if (OpenTagVisualizerDialogHandler != null)
        {
            result = OpenTagVisualizerDialogHandler(vm);
        }
        else if (Application.Current != null)
        {
            var window = new Views.TagVisualizerWindow
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            result = window.ShowDialog();
        }

        if (result == true || vm.HasUnsavedChanges)
        {
            _session.NotifyDataChanged();
            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
        }
    }

    private Views.RigStateWorkspaceWindow? _activeWorkspaceWindow;

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task OpenRigStateDocument()
    {
        await ShowRigStateWorkspaceAsync(triggerNew: false);
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task ManageRigStateDocuments()
    {
        await ShowRigStateWorkspaceAsync(triggerNew: false);
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NewRigStateDocument()
    {
        await ShowRigStateWorkspaceAsync(triggerNew: true);
    }

    private async Task ShowRigStateWorkspaceAsync(bool triggerNew)
    {
        if (!_session.IsProjectOpen) return;

        if (_activeWorkspaceWindow != null && _activeWorkspaceWindow.IsLoaded)
        {
            _activeWorkspaceWindow.Activate();
            if (_activeWorkspaceWindow.WindowState == WindowState.Minimized)
            {
                _activeWorkspaceWindow.WindowState = WindowState.Normal;
            }
            if (triggerNew && _activeWorkspaceWindow.DataContext is RigStateWorkspaceViewModel existingVm)
            {
                await existingVm.NewDocumentAsync();
            }
            return;
        }

        var repo = new DrillIntel.Data.DocTemplateRepository(_session);
        var chartDataService = new DrillIntel.Data.ChartDataService(_session);
        var wellRepo = new DrillIntel.Data.WellDataRepository(_session);

        var workspaceVm = new RigStateWorkspaceViewModel(_session, repo, chartDataService, wellRepo);
        await workspaceVm.InitializeAsync();

        var window = new DrillIntel.Views.RigStateWorkspaceWindow
        {
            DataContext = workspaceVm,
            Owner = Application.Current?.MainWindow
        };

        _activeWorkspaceWindow = window;
        window.Closed += (s, e) => _activeWorkspaceWindow = null;
        window.Show();

        if (triggerNew)
        {
            await workspaceVm.NewDocumentAsync();
        }
    }

    public bool CanSyncDataToParentTimelog =>
        _session.IsProjectOpen &&
        CurrentViewModel is DashboardViewModel db &&
        db.SelectedNode?.IsLinkedTimeLog == true;

    public Func<SyncDataWithParentTimelogViewModel, bool?>? OpenSyncDataDialogHandler { get; set; }
    public Action? SyncDataToParentTimelogHandler { get; set; }

    [RelayCommand(CanExecute = nameof(CanSyncDataToParentTimelog))]
    private async Task SyncDataToParentTimelog()
    {
        if (!CanSyncDataToParentTimelog) return;

        TimeLog? timeLog = null;
        if (CurrentViewModel is DashboardViewModel db)
        {
            if (db.SelectedNode?.Tag is TimeLog tl)
            {
                timeLog = tl;
            }
            else if (db.SelectedNode != null)
            {
                var repo = new WellDataRepository(_session);
                var timeLogs = await repo.GetTimeLogsAsync();
                timeLog = timeLogs.FirstOrDefault(t => t.ObjectID == db.SelectedNode.Name || t.nameLog == db.SelectedNode.Name);
            }
        }

        if (timeLog != null)
        {
            await OpenSyncDataDialogAsync(timeLog);
        }

        SyncDataToParentTimelogHandler?.Invoke();
    }

    public async Task<bool?> OpenSyncDataDialogAsync(TimeLog timeLog)
    {
        var repo = new WellDataRepository(_session);
        var vm = new SyncDataWithParentTimelogViewModel(_session, timeLog, repo);
        await vm.InitializeAsync();

        if (OpenSyncDataDialogHandler != null)
        {
            return OpenSyncDataDialogHandler(vm);
        }

        if (Application.Current != null)
        {
            var window = new DrillIntel.Views.SyncDataWithParentTimelogWindow
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            return window.ShowDialog();
        }

        return false;
    }
}

