using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ProjectSession? _session;
    private readonly IWellDataRepository? _repository;

    [ObservableProperty]
    private string _qcStatus = "N/A";

    [ObservableProperty]
    private string _qcColor = "#4CAF50";

    [ObservableProperty]
    private string _recentImports = "No imports yet";

    [ObservableProperty]
    private WellInfo? _selectedWell;

    [ObservableProperty]
    private WellTreeNode? _selectedNode;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<WellInfo> AvailableWells { get; } = new();

    public ObservableCollection<WellTreeNode> WellTree { get; } = new();

    public ObservableCollection<string> ActivityLog { get; } = new();

    public DashboardViewModel() : this(null, null)
    {
    }

    public DashboardViewModel(ProjectSession? session) : this(session, null)
    {
    }

    public DashboardViewModel(ProjectSession? session, IWellDataRepository? repository)
    {
        _session = session;
        _repository = repository ?? (_session != null ? new WellDataRepository(_session) : null);

        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        if (_repository == null || _session?.IsProjectOpen != true)
        {
            HasData = false;
            RecentImports = "No project open";
            QcStatus = "N/A";
            return;
        }

        try
        {
            IsLoading = true;

            var projectWell = await _repository.GetProjectWellAsync();
            var timeLogs = await _repository.GetTimeLogsAsync();
            var depthLogs = await _repository.GetDepthLogsAsync();

            WellTree.Clear();
            AvailableWells.Clear();

            if (projectWell != null)
            {
                SelectedWell = projectWell;
                AvailableWells.Add(projectWell);

                var wellNode = new WellTreeNode
                {
                    Name = projectWell.WellName,
                    Type = WellTreeNodeType.Well,
                    IconKind = "Factory",
                    IconColor = "#2196F3",
                    Badge = projectWell.FieldName,
                    Tag = projectWell,
                    IsExpanded = true
                };

                // Timelogs category
                var timeFolder = new WellTreeNode
                {
                    Name = "Timelogs",
                    Type = WellTreeNodeType.Folder,
                    IconKind = "ClockOutline",
                    IconColor = "#FF9800",
                    Badge = $"({timeLogs.Count})",
                    IsExpanded = true
                };

                foreach (var tl in timeLogs)
                {
                    timeFolder.Children.Add(new WellTreeNode
                    {
                        Name = tl.LogName,
                        Type = WellTreeNodeType.TimeLog,
                        IconKind = "FileClockOutline",
                        IconColor = "#FF9800",
                        Subtitle = $"QC: {tl.QcScore:F1}% • {tl.ImportDate:dd-MM-yyyy hh:mm tt}",
                        Tag = tl,
                        IsExpanded = false
                    });
                }
                wellNode.Children.Add(timeFolder);

                // Depthlogs category
                var depthFolder = new WellTreeNode
                {
                    Name = "Depthlogs",
                    Type = WellTreeNodeType.Folder,
                    IconKind = "FormatVerticalAlignBottom",
                    IconColor = "#9C27B0",
                    Badge = $"({depthLogs.Count})",
                    IsExpanded = true
                };

                foreach (var dl in depthLogs)
                {
                    depthFolder.Children.Add(new WellTreeNode
                    {
                        Name = dl.LogName,
                        Type = WellTreeNodeType.DepthLog,
                        IconKind = "FileDocumentOutline",
                        IconColor = "#9C27B0",
                        Subtitle = $"QC: {dl.QcScore:F1}% • {dl.ImportDate:dd-MM-yyyy hh:mm tt}",
                        Tag = dl,
                        IsExpanded = false
                    });
                }
                wellNode.Children.Add(depthFolder);

                WellTree.Add(wellNode);
                HasData = true;
            }
            else
            {
                HasData = false;
            }

            // Summary metrics
            int totalLogs = timeLogs.Count + depthLogs.Count;
            RecentImports = totalLogs > 0 
                ? $"{totalLogs} log{(totalLogs > 1 ? "s" : "")} in project" 
                : "No logs imported yet";

            var allQc = timeLogs.Select(t => t.QcScore).Concat(depthLogs.Select(d => d.QcScore)).ToList();
            if (allQc.Count > 0)
            {
                double avgQc = allQc.Average();
                QcStatus = $"{avgQc:F1}% - {(avgQc >= 90 ? "Excellent" : avgQc >= 75 ? "Good" : "Needs Review")}";
                QcColor = avgQc >= 90 ? "#4CAF50" : avgQc >= 75 ? "#FF9800" : "#F44336";
            }
            else
            {
                QcStatus = "100% - Ready";
                QcColor = "#4CAF50";
            }

            // Activity Log
            ActivityLog.Clear();
            var combinedActivities = timeLogs
                .Select(t => new { Text = $"{t.ImportDate:HH:mm} - Imported Timelog '{t.LogName}' for {t.WellName} (QC: {t.QcScore:F1}%)", Date = t.ImportDate })
                .Concat(depthLogs.Select(d => new { Text = $"{d.ImportDate:HH:mm} - Imported Depthlog '{d.LogName}' for {d.WellName} (QC: {d.QcScore:F1}%)", Date = d.ImportDate }))
                .OrderByDescending(x => x.Date)
                .Take(10);

            foreach (var item in combinedActivities)
            {
                ActivityLog.Add(item.Text);
            }

            if (ActivityLog.Count == 0)
            {
                ActivityLog.Add("Ready. Use 'Data Import' from the ribbon to import timelogs or depthlogs.");
            }
        }
        catch (Exception ex)
        {
            RecentImports = "Error loading data";
            QcStatus = ex.Message;
            QcColor = "#F44336";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void OnTreeNodeSelected(WellTreeNode? node)
    {
        SelectedNode = node;
        if (node == null) return;

        if (node.Tag is WellInfo well)
        {
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(well.WellName, StringComparison.OrdinalIgnoreCase)) ?? well;
        }
        else if (node.Tag is VmxTimeLog timeLog)
        {
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(timeLog.WellName, StringComparison.OrdinalIgnoreCase));
        }
        else if (node.Tag is VmxDepthLog depthLog)
        {
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(depthLog.WellName, StringComparison.OrdinalIgnoreCase));
        }
    }

    [RelayCommand]
    private async Task EditWellAsync()
    {
        if (_session?.IsProjectOpen != true || _repository == null) return;
        var currentWell = await _repository.GetProjectWellAsync();
        var vm = new WellInformationViewModel(currentWell?.WellName ?? "", currentWell?.FieldName ?? "General Field");
        var window = new DrillIntel.Views.WellInformationWindow
        {
            DataContext = vm,
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            await _repository.SaveProjectWellAsync(new WellInfo
            {
                WellName = vm.WellName.Trim(),
                FieldName = vm.FieldName.Trim()
            });
            await RefreshAsync();
        }
    }
}

