using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;
using DrillIntel.Views;
using DrillIntel.Views.Broomstick;

namespace DrillIntel.ViewModels.Broomstick;

/// <summary>
/// Master Workspace ViewModel coordinating the Left TreeView navigation pane
/// and the Right Multi-Document tabbed Broomstick chart workspace.
/// </summary>
public partial class BroomstickWorkspaceViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IBroomstickTemplateRepository _templateRepo;
    private readonly IBroomstickDataService? _broomstickDataService;
    private readonly IWellDataRepository? _wellDataRepo;
    private string _currentWellId = "";
    private string _currentWellName = "";
    private string _currentWellboreId = "";
    private string _currentLogId = "";
    private string _currentPlanId = "";
    private string _currentPlanName = "";
    private readonly System.Collections.Generic.Dictionary<string, string> _planNames = new(StringComparer.OrdinalIgnoreCase);

    // UI Handlers & Test Hooks
    public Func<string, string, bool>? ConfirmDeleteHandler { get; set; }
    public Func<string, string, string?>? PromptNewDocumentNameHandler { get; set; }
    public Action<BroomstickDocumentViewModel>? OpenDetachedWindowHandler { get; set; }

    // TreeView Collections
    public ObservableCollection<BroomstickDocumentTreeGroupNode> TreeRoots { get; } = new();
    public ObservableCollection<BroomstickDocumentTreeNode> AllDocumentNodes { get; } = new();

    // Open Document Tabs
    public ObservableCollection<BroomstickDocumentViewModel> OpenDocuments { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenDocuments))]
    private BroomstickDocumentViewModel? _activeDocument;

    public bool HasOpenDocuments => OpenDocuments.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNode))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]
    [NotifyPropertyChangedFor(nameof(CanMakeDefaultSelection))]
    private BroomstickDocumentTreeNode? _selectedTreeNode;

    public bool HasSelectedNode => SelectedTreeNode != null;
    public bool CanDeleteSelection => SelectedTreeNode != null && AllDocumentNodes.Count > 1;
    public bool CanMakeDefaultSelection => SelectedTreeNode != null && !SelectedTreeNode.IsDefault;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    partial void OnSearchFilterChanged(string value)
    {
        ApplySearchFilter();
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalDocumentCount;

    public BroomstickWorkspaceViewModel(
        ProjectSession session,
        IBroomstickTemplateRepository? templateRepo = null,
        IBroomstickDataService? broomstickDataService = null,
        IWellDataRepository? wellDataRepo = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _templateRepo = templateRepo ?? new BroomstickTemplateRepository(_session);
        _broomstickDataService = broomstickDataService ?? new BroomstickDataService(_session);
        _wellDataRepo = wellDataRepo ?? new WellDataRepository(_session);

        ConfirmDeleteHandler = (title, message) =>
        {
            if (Application.Current == null) return true;
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        };

        OpenDocuments.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasOpenDocuments));
        };
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading Broomstick documents...";
        try
        {
            await RefreshTreeAsync();

            // Auto-open default document tab if no tabs open
            if (OpenDocuments.Count == 0 && AllDocumentNodes.Count > 0)
            {
                var defaultNode = AllDocumentNodes.FirstOrDefault(n => n.IsDefault) ?? AllDocumentNodes.FirstOrDefault();
                if (defaultNode != null)
                {
                    await OpenDocumentNodeAsync(defaultNode);
                }
            }
        }
        finally
        {
            IsLoading = false;
            StatusMessage = "Ready";
        }
    }

    public async Task RefreshTreeAsync()
    {
        await _templateRepo.EnsureTableAsync();

        // 1. Resolve Active Project Context (Well, Log, Plans)
        if (_wellDataRepo != null)
        {
            var well = await _wellDataRepo.GetProjectWellAsync();
            if (well != null)
            {
                _currentWellId = !string.IsNullOrWhiteSpace(well.ObjectID) ? well.ObjectID : _currentWellId;
                _currentWellName = !string.IsNullOrWhiteSpace(well.name) ? well.name : "Active Well";
                _currentWellboreId = well.__timeLogWellboreID ?? well.wellbores?.Keys.FirstOrDefault() ?? "";
            }

            var timeLogs = await _wellDataRepo.GetTimeLogsAsync();
            var primaryLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();
            if (primaryLog != null)
            {
                _currentLogId = primaryLog.ObjectID ?? "";
            }
        }

        if (_session != null && _session.IsProjectOpen && !string.IsNullOrWhiteSpace(_currentWellId))
        {
            try
            {
                var plans = await Task.Run(() =>
                    AdnlHookloadPlan.getAllPlans(_session.GetDataService(), _currentWellId, _currentWellboreId, _currentLogId));
                if (plans != null && plans.Count > 0)
                {
                    var first = plans.FirstOrDefault();
                    if (first != null)
                    {
                        _currentPlanId = first.PlanID ?? "";
                        _currentPlanName = first.Name ?? first.PlanID ?? "";
                    }
                    foreach (var p in plans)
                    {
                        if (!string.IsNullOrWhiteSpace(p.PlanID))
                        {
                            _planNames[p.PlanID] = !string.IsNullOrWhiteSpace(p.Name) ? p.Name : p.PlanID;
                        }
                    }
                }
            }
            catch { }
        }

        var templates = await _templateRepo.GetAllTemplatesAsync();

        // 2. Auto-heal any empty/unassigned WellId, LogId, PlanId
        foreach (var tpl in templates)
        {
            bool modified = false;
            if (string.IsNullOrWhiteSpace(tpl.WellId) || tpl.WellId == "General / Unassigned")
            {
                if (!string.IsNullOrWhiteSpace(_currentWellId))
                {
                    tpl.WellId = _currentWellId;
                    modified = true;
                }
            }
            if (string.IsNullOrWhiteSpace(tpl.WellboreId) && !string.IsNullOrWhiteSpace(_currentWellboreId))
            {
                tpl.WellboreId = _currentWellboreId;
                modified = true;
            }
            if (string.IsNullOrWhiteSpace(tpl.LogId) && !string.IsNullOrWhiteSpace(_currentLogId))
            {
                tpl.LogId = _currentLogId;
                modified = true;
            }
            if (string.IsNullOrWhiteSpace(tpl.PlanId) || tpl.PlanId == "DEFAULT_PLAN")
            {
                if (!string.IsNullOrWhiteSpace(_currentPlanId))
                {
                    tpl.PlanId = _currentPlanId;
                    modified = true;
                }
            }
            if (string.IsNullOrWhiteSpace(tpl.ProfileId))
            {
                tpl.ProfileId = "Default Profile";
                modified = true;
            }

            if (modified)
            {
                try
                {
                    var doc = BroomstickDocument.DeserializeFromBlob(tpl.TemplateData) ?? new BroomstickDocument();
                    doc.WellId = tpl.WellId;
                    doc.WellboreId = tpl.WellboreId;
                    doc.LogId = tpl.LogId;
                    doc.PlanId = tpl.PlanId ?? "";
                    doc.ProfileId = tpl.ProfileId ?? "Default Profile";
                    tpl.TemplateData = doc.SerializeToBlob();
                    await _templateRepo.SaveTemplateAsync(tpl);
                }
                catch { }
            }
        }

        AllDocumentNodes.Clear();
        foreach (var tpl in templates)
        {
            var node = new BroomstickDocumentTreeNode(tpl);
            if (node.WellId == _currentWellId && !string.IsNullOrWhiteSpace(_currentWellName))
            {
                node.WellName = _currentWellName;
            }
            if (node.PlanId != null && _planNames.TryGetValue(node.PlanId, out var pName))
            {
                node.PlanName = pName;
            }
            else if (node.PlanId == _currentPlanId && !string.IsNullOrWhiteSpace(_currentPlanName))
            {
                node.PlanName = _currentPlanName;
            }
            AllDocumentNodes.Add(node);
        }

        BuildTreeHierarchy();
        TotalDocumentCount = AllDocumentNodes.Count;
    }

    private void BuildTreeHierarchy()
    {
        TreeRoots.Clear();

        var wellGroups = AllDocumentNodes
            .GroupBy(n => string.IsNullOrWhiteSpace(n.WellId) ? "General / Unassigned" : n.WellId)
            .OrderBy(g => g.Key);

        foreach (var wellGrp in wellGroups)
        {
            string wellHeader;
            if (wellGrp.Key == "General / Unassigned")
            {
                wellHeader = "General Templates";
            }
            else
            {
                var matchNode = wellGrp.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n.WellName));
                string name = matchNode?.WellName 
                    ?? (wellGrp.Key == _currentWellId && !string.IsNullOrWhiteSpace(_currentWellName) ? _currentWellName : wellGrp.Key);
                wellHeader = $"Well: {name}";
            }

            var wellNode = new BroomstickDocumentTreeGroupNode
            {
                Header = wellHeader,
                GroupType = "Well",
                GroupId = wellGrp.Key,
                DocumentCount = wellGrp.Count(),
                IsExpanded = true
            };

            var planGroups = wellGrp
                .GroupBy(n => string.IsNullOrWhiteSpace(n.PlanId) ? "General Templates" : n.PlanId)
                .OrderBy(g => g.Key);

            foreach (var planGrp in planGroups)
            {
                string planHeader;
                if (planGrp.Key == "General Templates")
                {
                    planHeader = "General Templates";
                }
                else
                {
                    var matchNode = planGrp.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n.PlanName));
                    string pName = matchNode?.PlanName 
                        ?? (_planNames.TryGetValue(planGrp.Key, out var pVal) ? pVal : planGrp.Key);
                    planHeader = $"Plan: {pName}";
                }

                var planNode = new BroomstickDocumentTreeGroupNode
                {
                    Header = planHeader,
                    GroupType = "Plan",
                    GroupId = planGrp.Key,
                    DocumentCount = planGrp.Count(),
                    IsExpanded = true
                };

                foreach (var docNode in planGrp.OrderBy(d => d.DocumentName))
                {
                    planNode.Children.Add(docNode);
                }

                wellNode.Children.Add(planNode);
            }

            TreeRoots.Add(wellNode);
        }
    }

    private void ApplySearchFilter()
    {
        string query = SearchFilter?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var doc in AllDocumentNodes)
        {
            bool match = string.IsNullOrEmpty(query) ||
                         doc.DocumentName.ToLowerInvariant().Contains(query) ||
                         (doc.PlanId?.ToLowerInvariant().Contains(query) ?? false) ||
                         doc.WellId.ToLowerInvariant().Contains(query);

            doc.IsVisibleInFilter = match;
        }

        foreach (var wellNode in TreeRoots)
        {
            bool anyInWell = false;
            foreach (var planObj in wellNode.Children)
            {
                if (planObj is BroomstickDocumentTreeGroupNode planNode)
                {
                    bool anyInPlan = false;
                    foreach (var childObj in planNode.Children)
                    {
                        if (childObj is BroomstickDocumentTreeNode docNode)
                        {
                            if (docNode.IsVisibleInFilter) anyInPlan = true;
                        }
                    }
                    planNode.IsVisibleInFilter = anyInPlan;
                    if (anyInPlan) anyInWell = true;
                }
            }
            wellNode.IsVisibleInFilter = anyInWell;
        }
    }

    [RelayCommand]
    public async Task OpenDocumentNodeAsync(BroomstickDocumentTreeNode? node)
    {
        if (node == null) return;

        // Check if tab already open
        var existing = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == node.TemplateId);
        if (existing != null)
        {
            ActiveDocument = existing;
            return;
        }

        // Load document
        var item = await _templateRepo.GetTemplateByIdAsync(node.TemplateId);
        if (item == null) return;

        var doc = BroomstickDocument.DeserializeFromBlob(item.TemplateData) ?? new BroomstickDocument();
        doc.TemplateId = item.TemplateId;
        doc.DocumentName = item.DocumentName;
        doc.WellId = item.WellId;
        doc.PlanId = item.PlanId ?? "";
        doc.IsDefault = item.IsDefault;

        var docVm = new BroomstickDocumentViewModel(_session, _templateRepo, _broomstickDataService, doc);
        WireDialogHandlers(docVm);
        await docVm.InitializeAsync();

        OpenDocuments.Add(docVm);
        ActiveDocument = docVm;
    }

    private void WireDialogHandlers(BroomstickDocumentViewModel docVm)
    {
        docVm.OpenSetupDialogHandler = (vm) =>
        {
            var setupVm = new BroomstickSetupViewModel(vm.Document);
            var dlg = new BroomstickSetupDialog(setupVm);
            if (Application.Current?.MainWindow != null) dlg.Owner = Application.Current.MainWindow;
            return dlg.ShowDialog();
        };

        docVm.OpenTemplateManagerDialogHandler = (mgrVm) =>
        {
            var dlg = new BroomstickDocumentManagerDialog(mgrVm);
            if (Application.Current?.MainWindow != null) dlg.Owner = Application.Current.MainWindow;
            return dlg.ShowDialog();
        };

        docVm.OpenDataSelectorDialogHandler = (dataVm) =>
        {
            var dlg = new DataSelectorWindow(dataVm)
            {
                Owner = Application.Current?.MainWindow
            };
            return dlg.ShowDialog();
        };

        docVm.PromptSaveAsHandler = (currentName) =>
        {
            if (PromptNewDocumentNameHandler != null)
            {
                return PromptNewDocumentNameHandler("Save As Template", "Enter new template name:");
            }
            return $"{currentName} (Copy)";
        };
    }

    [RelayCommand]
    public void CloseDocumentTab(BroomstickDocumentViewModel? doc)
    {
        if (doc == null) return;

        OpenDocuments.Remove(doc);
        if (ActiveDocument == doc)
        {
            ActiveDocument = OpenDocuments.LastOrDefault();
        }
    }

    [RelayCommand]
    public async Task NewDocumentAsync()
    {
        string? newName = PromptNewDocumentNameHandler != null
            ? PromptNewDocumentNameHandler("New Broomstick Document", "Enter new template name:")
            : $"Broomstick Document {AllDocumentNodes.Count + 1}";

        if (string.IsNullOrWhiteSpace(newName)) return;

        string targetWell = !string.IsNullOrWhiteSpace(SelectedTreeNode?.WellId) ? SelectedTreeNode.WellId : _currentWellId;
        string targetWellbore = !string.IsNullOrWhiteSpace(SelectedTreeNode?.WellboreId) ? SelectedTreeNode.WellboreId : _currentWellboreId;
        string targetLog = !string.IsNullOrWhiteSpace(SelectedTreeNode?.LogId) ? SelectedTreeNode.LogId : _currentLogId;
        string targetPlan = !string.IsNullOrWhiteSpace(SelectedTreeNode?.PlanId) && SelectedTreeNode.PlanId != "DEFAULT_PLAN"
            ? SelectedTreeNode.PlanId
            : (!string.IsNullOrWhiteSpace(_currentPlanId) ? _currentPlanId : "DEFAULT_PLAN");
        string targetProfile = !string.IsNullOrWhiteSpace(SelectedTreeNode?.ProfileId) ? SelectedTreeNode.ProfileId : "Default Profile";

        var doc = new BroomstickDocument
        {
            DocumentName = newName.Trim(),
            WellId = targetWell,
            WellboreId = targetWellbore,
            LogId = targetLog,
            PlanId = targetPlan,
            ProfileId = targetProfile
        };

        var item = new BroomstickTemplateItem
        {
            TemplateId = doc.TemplateId,
            DocumentName = doc.DocumentName,
            WellId = doc.WellId,
            WellboreId = doc.WellboreId,
            LogId = doc.LogId,
            PlanId = doc.PlanId,
            ProfileId = doc.ProfileId,
            TemplateData = doc.SerializeToBlob(),
            IsDefault = AllDocumentNodes.Count == 0,
            CreatedBy = Environment.UserName,
            CreatedDate = DateTime.Now,
            ModifiedBy = Environment.UserName,
            ModifiedDate = DateTime.Now
        };

        await _templateRepo.SaveTemplateAsync(item);
        await RefreshTreeAsync();

        var node = AllDocumentNodes.FirstOrDefault(n => n.TemplateId == item.TemplateId);
        if (node != null)
        {
            await OpenDocumentNodeAsync(node);
        }
    }

    [RelayCommand]
    public async Task MakeDefaultAsync(BroomstickDocumentTreeNode? node)
    {
        var target = node ?? SelectedTreeNode;
        if (target == null) return;

        await _templateRepo.SetDefaultTemplateAsync(target.TemplateId, target.WellId);
        await RefreshTreeAsync();
    }

    [RelayCommand]
    public async Task DeleteDocumentAsync(BroomstickDocumentTreeNode? node)
    {
        var target = node ?? SelectedTreeNode;
        if (target == null || !CanDeleteSelection) return;

        bool confirm = ConfirmDeleteHandler == null || ConfirmDeleteHandler(
            "Delete Template",
            $"Are you sure you want to permanently delete '{target.DocumentName}'?");

        if (!confirm) return;

        // Close tab if open
        var tab = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == target.TemplateId);
        if (tab != null) OpenDocuments.Remove(tab);

        await _templateRepo.DeleteTemplateAsync(target.TemplateId);
        await RefreshTreeAsync();
    }
}

