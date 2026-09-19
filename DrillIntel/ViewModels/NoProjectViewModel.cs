using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;

namespace DrillIntel.ViewModels;

public partial class NoProjectViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IProjectService _projectService;
    private readonly IRecentProjectsService _recentProjectsService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _hasRecentProjects;

    [ObservableProperty]
    private bool _showEmptyState = true;

    [ObservableProperty]
    private bool _showNoSearchResults;

    [ObservableProperty]
    private bool _showProjectsList;

    public ObservableCollection<RecentProject> RecentProjects { get; } = new();

    public NoProjectViewModel() : this(App.Session, App.ProjectService, App.RecentProjectsService)
    {
    }

    public NoProjectViewModel(ProjectSession session, IProjectService projectService, IRecentProjectsService recentProjectsService)
    {
        _session = session;
        _projectService = projectService;
        _recentProjectsService = recentProjectsService;

        _recentProjectsService.RecentProjectsChanged += OnRecentProjectsChanged;
        RefreshProjects();
    }

    private void OnRecentProjectsChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(RefreshProjects);
        }
        else
        {
            RefreshProjects();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        RefreshProjects();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public void RefreshProjects()
    {
        var all = _recentProjectsService.GetRecentProjects();
        HasRecentProjects = all.Count > 0;

        var query = SearchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(query)
            ? all
            : all.Where(p =>
                (p.ProjectName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (p.WellName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (p.FieldName?.Contains(query, StringComparison.OrdinalIgnoreCase) == true) ||
                (p.FilePath?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)).ToList();

        RecentProjects.Clear();
        foreach (var p in filtered)
        {
            RecentProjects.Add(p);
        }

        ShowEmptyState = !HasRecentProjects;
        ShowNoSearchResults = HasRecentProjects && RecentProjects.Count == 0 && !string.IsNullOrWhiteSpace(query);
        ShowProjectsList = HasRecentProjects && RecentProjects.Count > 0;
    }

    [RelayCommand]
    private void NewProject()
    {
        _projectService.CreateNewProject();
    }

    [RelayCommand]
    private void LoadProject()
    {
        _projectService.OpenProject();
    }

    [RelayCommand]
    private void OpenRecentProject(RecentProject? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.FilePath)) return;

        if (!File.Exists(project.FilePath))
        {
            var res = MessageBox.Show(
                $"The project file could not be found:\n\n{project.FilePath}\n\nWould you like to remove it from the recent projects list?",
                "File Not Found",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                _recentProjectsService.Remove(project.FilePath);
            }
            return;
        }

        _projectService.OpenProject(project.FilePath);
    }

    [RelayCommand]
    private void TogglePin(RecentProject? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.FilePath)) return;
        _recentProjectsService.TogglePin(project.FilePath);
    }

    [RelayCommand]
    private void RemoveRecentProject(RecentProject? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.FilePath)) return;
        _recentProjectsService.Remove(project.FilePath);
    }

    [RelayCommand]
    private void OpenContainingFolder(RecentProject? project)
    {
        if (project == null || string.IsNullOrWhiteSpace(project.FilePath)) return;
        try
        {
            var dir = Path.GetDirectoryName(project.FilePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{project.FilePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open folder:\n{ex.Message}", "Open Folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearRecentProjects()
    {
        if (RecentProjects.Count == 0) return;

        var result = MessageBox.Show(
            "Are you sure you want to clear all recent projects from the list?",
            "Clear Recent Projects",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _recentProjectsService.Clear();
        }
    }
}
