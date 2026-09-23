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
using DrillIntel.Data.Objects.DataObjects.Models;

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
    private Well? _selectedWell;

    [ObservableProperty]
    private WellTreeNode? _selectedNode;

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<Well> AvailableWells { get; } = new();

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

        if (_session != null)
        {
            _session.DataChanged += OnDataChanged;
        }

        _ = LoadDataAsync();
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshAsync);
        }
        else
        {
            _ = RefreshAsync();
        }
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

            // Strict separation: prevent any cross-listing between Depthlogs and Timelogs
            var depthLogIds = new HashSet<string>(depthLogs.Select(dl => dl.ObjectID).Where(id => !string.IsNullOrEmpty(id)), StringComparer.OrdinalIgnoreCase);
            var depthTables = new HashSet<string>(depthLogs.Select(dl => dl.__dataTableName).Where(t => !string.IsNullOrEmpty(t)), StringComparer.OrdinalIgnoreCase);

            var filteredTimeLogs = timeLogs.Where(tl =>
                !depthLogIds.Contains(tl.ObjectID) &&
                (string.IsNullOrEmpty(tl.__dataTableName) || (!depthTables.Contains(tl.__dataTableName) && !tl.__dataTableName.StartsWith("depthLog", StringComparison.OrdinalIgnoreCase)))
            ).ToList();

            var timeLogIds = new HashSet<string>(filteredTimeLogs.Select(tl => tl.ObjectID).Where(id => !string.IsNullOrEmpty(id)), StringComparer.OrdinalIgnoreCase);
            var timeTables = new HashSet<string>(filteredTimeLogs.Select(tl => tl.__dataTableName).Where(t => !string.IsNullOrEmpty(t)), StringComparer.OrdinalIgnoreCase);

            var filteredDepthLogs = depthLogs.Where(dl =>
                !timeLogIds.Contains(dl.ObjectID) &&
                (string.IsNullOrEmpty(dl.__dataTableName) || (!timeTables.Contains(dl.__dataTableName) && !dl.__dataTableName.StartsWith("timeLog", StringComparison.OrdinalIgnoreCase)))
            ).ToList();

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
                    Badge = $"({filteredTimeLogs.Count})",
                    IsExpanded = true
                };

                foreach (var tl in filteredTimeLogs)
                {
                    timeFolder.Children.Add(new WellTreeNode
                    {
                        Name = !string.IsNullOrWhiteSpace(tl.nameLog) ? tl.nameLog : tl.ObjectID,
                        Type = WellTreeNodeType.TimeLog,
                        IconKind = "FileClockOutline",
                        IconColor = "#FF9800",
                        Subtitle = !string.IsNullOrWhiteSpace(tl.description) ? tl.description : tl.creationDate,
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
                    Badge = $"({filteredDepthLogs.Count})",
                    IsExpanded = true
                };

                foreach (var dl in filteredDepthLogs)
                {
                    depthFolder.Children.Add(new WellTreeNode
                    {
                        Name = !string.IsNullOrWhiteSpace(dl.nameLog) ? dl.nameLog : dl.ObjectID,
                        Type = WellTreeNodeType.DepthLog,
                        IconKind = "FileDocumentOutline",
                        IconColor = "#9C27B0",
                        Subtitle = !string.IsNullOrWhiteSpace(dl.description) ? dl.description : dl.creationDate,
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
            int totalLogs = filteredTimeLogs.Count + filteredDepthLogs.Count;
            RecentImports = totalLogs > 0 
                ? $"{totalLogs} log{(totalLogs > 1 ? "s" : "")} in project" 
                : "No logs imported yet";

            double GetTimeLogQc(TimeLog t)
            {
                if (!string.IsNullOrWhiteSpace(t.description))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(t.description, @"QC:\s*([0-9.]+)\s*%");
                    if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double score))
                        return score;
                }
                return 100.0;
            }

            DateTime GetTimeLogDate(TimeLog t)
            {
                if (!string.IsNullOrWhiteSpace(t.creationDate) && DateTime.TryParse(t.creationDate, out DateTime dt))
                    return dt;
                return DateTime.Now;
            }

            double GetDepthLogQc(DepthLog d)
            {
                if (!string.IsNullOrWhiteSpace(d.description))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(d.description, @"QC:\s*([0-9.]+)\s*%");
                    if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double score))
                        return score;
                }
                return 100.0;
            }

            DateTime GetDepthLogDate(DepthLog d)
            {
                if (!string.IsNullOrWhiteSpace(d.creationDate) && DateTime.TryParse(d.creationDate, out DateTime dt))
                    return dt;
                return DateTime.Now;
            }

            var allQc = filteredTimeLogs.Select(t => GetTimeLogQc(t)).Concat(filteredDepthLogs.Select(d => GetDepthLogQc(d))).ToList();
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
            var combinedActivities = filteredTimeLogs
                .Select(t =>
                {
                    var tDate = GetTimeLogDate(t);
                    var tQc = GetTimeLogQc(t);
                    var tName = !string.IsNullOrWhiteSpace(t.nameLog) ? t.nameLog : t.ObjectID;
                    var wName = !string.IsNullOrWhiteSpace(t.nameWell) ? t.nameWell : t.__WellName;
                    return new { Text = $"{tDate:HH:mm} - Imported Timelog '{tName}' for {wName} (QC: {tQc:F1}%)", Date = tDate };
                })
                .Concat(filteredDepthLogs.Select(d =>
                {
                    var dDate = GetDepthLogDate(d);
                    var dQc = GetDepthLogQc(d);
                    var dName = !string.IsNullOrWhiteSpace(d.nameLog) ? d.nameLog : d.ObjectID;
                    var wName = !string.IsNullOrWhiteSpace(d.nameWell) ? d.nameWell : d.__WellName;
                    return new { Text = $"{dDate:HH:mm} - Imported Depthlog '{dName}' for {wName} (QC: {dQc:F1}%)", Date = dDate };
                }))
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

        if (node.Tag is Well well)
        {
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(well.WellName, StringComparison.OrdinalIgnoreCase)) ?? well;
        }
        else if (node.Tag is TimeLog timeLog)
        {
            var wellName = !string.IsNullOrWhiteSpace(timeLog.nameWell) ? timeLog.nameWell : timeLog.__WellName;
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(wellName, StringComparison.OrdinalIgnoreCase));
        }
        else if (node.Tag is VmxTimeLog vmxTimeLog)
        {
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(vmxTimeLog.WellName, StringComparison.OrdinalIgnoreCase));
        }
        else if (node.Tag is DepthLog depthLog)
        {
            var wellName = !string.IsNullOrWhiteSpace(depthLog.nameWell) ? depthLog.nameWell : depthLog.__WellName;
            SelectedWell = AvailableWells.FirstOrDefault(w => w.WellName.Equals(wellName, StringComparison.OrdinalIgnoreCase));
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
            var wellToSave = currentWell ?? new Well();
            wellToSave.name = vm.WellName.Trim();
            wellToSave.field = vm.FieldName.Trim();
            if (string.IsNullOrWhiteSpace(wellToSave.ObjectID))
            {
                wellToSave.ObjectID = Guid.NewGuid().ToString();
            }
            await _repository.SaveProjectWellAsync(wellToSave);
            await RefreshAsync();
        }
    }
}

