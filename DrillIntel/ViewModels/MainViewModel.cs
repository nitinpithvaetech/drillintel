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

    private void OnProjectStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsProjectOpen));
        OnPropertyChanged(nameof(WindowTitle));
        NavigateToDashboardCommand.NotifyCanExecuteChanged();
        NavigateToImportTimelogCommand.NotifyCanExecuteChanged();
        NavigateToImportDepthlogCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();
        IdentifyRigStatesCommand.NotifyCanExecuteChanged();
        OpenRigStateMasterCommand.NotifyCanExecuteChanged();
        OpenUnitMasterCommand.NotifyCanExecuteChanged();
        OpenWellEditorCommand.NotifyCanExecuteChanged();
        EditWellCommand.NotifyCanExecuteChanged();
        OpenTimelogEditorCommand.NotifyCanExecuteChanged();
        OpenRigStateDocumentCommand.NotifyCanExecuteChanged();
        ManageRigStateDocumentsCommand.NotifyCanExecuteChanged();
        NewRigStateDocumentCommand.NotifyCanExecuteChanged();

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
    private void OpenRigStateMaster()
    {
        DrillIntel.Data.IDataServiceDIntel dataService;
        string contextName;

        if (_session.IsProjectOpen)
        {
            dataService = _session.GetDataService();
            contextName = $"Project: {_session.ProjectName}";
        }
        else
        {
            var appDb = _appDatabaseService ?? App.AppDatabaseService;
            dataService = appDb.GetDataService();
            contextName = "Application Master Template (Default for New Projects)";
        }

        var vm = new RigStateViewModel(dataService, contextName);
        var window = new DrillIntel.Views.RigStateWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenUnitMaster()
    {
        DrillIntel.Data.IDataServiceDIntel dataService;
        string contextName;
        string tableName;
        string dbPath;

        if (_session.IsProjectOpen)
        {
            dataService = _session.GetDataService();
            tableName = DrillIntel.Models.Unit.ProjectTableName;
            contextName = $"Project: {_session.ProjectName}";
            dbPath = _session.ProjectFilePath ?? "Active Project Database";
        }
        else
        {
            var appDb = _appDatabaseService ?? App.AppDatabaseService;
            dataService = appDb.GetDataService();
            tableName = DrillIntel.Models.Unit.TableName;
            contextName = "Application Master Template (DrillIntelApp.sqlite)";
            dbPath = appDb.DatabasePath;
        }

        var vm = new UnitMasterViewModel(
            dataService,
            tableName: tableName,
            contextName: contextName,
            isProjectOpen: _session.IsProjectOpen,
            appDatabaseService: _appDatabaseService ?? App.AppDatabaseService,
            databasePath: dbPath);

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
        IDataServiceDIntel dataService;
        string tableName;
        string contextName;
        string dbPath;

        if (_session.IsProjectOpen)
        {
            dataService = _session.GetDataService();
            tableName = DrillIntel.Models.UnitConverter.ProjectTableName;
            contextName = $"Project: {_session.ProjectName}";
            dbPath = _session.ProjectFilePath ?? "Active Project Database";
        }
        else
        {
            var appDb = _appDatabaseService ?? App.AppDatabaseService;
            dataService = appDb.GetDataService();
            tableName = DrillIntel.Models.UnitConverter.TableName;
            contextName = "Application Master Template (DrillIntelApp.sqlite)";
            dbPath = appDb.DatabasePath;
        }

        var vm = new UnitConversionMasterViewModel(
            dataService,
            tableName: tableName,
            contextName: contextName,
            isProjectOpen: _session.IsProjectOpen,
            appDatabaseService: _appDatabaseService ?? App.AppDatabaseService,
            databasePath: dbPath);

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
    private async Task OpenRigStateDocument()
    {
        if (!_session.IsProjectOpen) return;
        var vm = new RigStateDocumentViewModel(_session);
        await vm.InitializeAsync();
        var window = new DrillIntel.Views.RigStateDocumentWindow
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow
        };
        window.Show();
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task ManageRigStateDocuments()
    {
        if (!_session.IsProjectOpen) return;
        var repo = new DrillIntel.Data.DocTemplateRepository(_session);
        var mgrVm = new RigStateDocumentManagerViewModel(repo);
        await mgrVm.InitializeAsync();
        var dialog = new DrillIntel.Views.RigStateDocumentManagerDialog
        {
            DataContext = mgrVm,
            Owner = Application.Current?.MainWindow
        };
        mgrVm.RequestClose = () => dialog.DialogResult = mgrVm.DialogResult;
        bool? res = dialog.ShowDialog();

        if (res == true && mgrVm.DialogResult && mgrVm.SelectedTemplateForOpen != null)
        {
            var vm = new RigStateDocumentViewModel(_session);
            await vm.InitializeAsync();
            await vm.LoadFromTemplateAsync(mgrVm.SelectedTemplateForOpen, refreshData: true);
            var window = new DrillIntel.Views.RigStateDocumentWindow
            {
                DataContext = vm,
                Owner = Application.Current?.MainWindow
            };
            window.Show();
        }
    }

    [RelayCommand(CanExecute = nameof(IsProjectOpen))]
    private async Task NewRigStateDocument()
    {
        if (!_session.IsProjectOpen) return;
        var vm = new RigStateDocumentViewModel(_session);
        await vm.InitializeAsync();
        await vm.NewDocumentAsync();
        var window = new DrillIntel.Views.RigStateDocumentWindow
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow
        };
        window.Show();
    }
}

