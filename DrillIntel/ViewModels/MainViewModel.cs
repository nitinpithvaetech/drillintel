using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;

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

    public MainViewModel(ProjectSession session, IProjectService projectService, IRecentProjectsService? recentProjectsService = null)
    {
        _session = session;
        _projectService = projectService;
        _recentProjectsService = recentProjectsService ?? App.RecentProjectsService;

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

    private void OnProjectStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsProjectOpen));
        OnPropertyChanged(nameof(WindowTitle));
        NavigateToDashboardCommand.NotifyCanExecuteChanged();
        NavigateToImportTimelogCommand.NotifyCanExecuteChanged();
        NavigateToImportDepthlogCommand.NotifyCanExecuteChanged();
        CloseProjectCommand.NotifyCanExecuteChanged();

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
        if (window.ShowDialog() == true)
        {
            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
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
        if (window.ShowDialog() == true)
        {
            if (CurrentViewModel is DashboardViewModel dashboard)
            {
                await dashboard.RefreshAsync();
            }
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
}

