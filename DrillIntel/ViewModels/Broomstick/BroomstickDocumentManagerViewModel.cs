using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels.Broomstick;

/// <summary>
/// Master ViewModel for the Broomstick Document Manager dialog.
/// Implements a hierarchical TreeView Master-Detail interface organizing templates
/// by Well -> Plan -> Template nodes with instant live search and detail inspection.
/// </summary>
public partial class BroomstickDocumentManagerViewModel : ObservableObject
{
    private readonly IBroomstickTemplateRepository _repository;
    private readonly ProjectSession? _session;
    private readonly IWellDataRepository? _wellDataRepo;
    private string _currentWellId;
    private string _currentWellName = "";
    private string _currentWellboreId = "";
    private string _currentLogId = "";
    private string _currentPlanId = "";
    private string _currentPlanName = "";
    private readonly Dictionary<string, string> _planNames = new(StringComparer.OrdinalIgnoreCase);

    public Action? RequestClose { get; set; }
    public bool DialogResult { get; private set; }
    public string? SelectedTemplateIdToOpen { get; private set; }

    // Test / UI Hooks
    public Func<string, string, string?>? PromptNewNameHandler { get; set; }
    public Func<string, string, bool>? ConfirmDeleteHandler { get; set; }
    public Action<string, string>? AlertHandler { get; set; }

    public ObservableCollection<BroomstickDocumentTreeGroupNode> TreeRoots { get; } = new();
    public ObservableCollection<BroomstickDocumentTreeNode> AllDocumentNodes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]
    [NotifyPropertyChangedFor(nameof(CanSetDefaultSelection))]
    private BroomstickDocumentTreeNode? _selectedDocumentNode;

    public bool HasSelection => SelectedDocumentNode != null;
    public bool CanDeleteSelection => SelectedDocumentNode != null && AllDocumentNodes.Count > 1;
    public bool CanSetDefaultSelection => SelectedDocumentNode != null && !SelectedDocumentNode.IsDefault;

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

    public BroomstickDocumentManagerViewModel(
        IBroomstickTemplateRepository repository,
        ProjectSession? session = null,
        IWellDataRepository? wellDataRepo = null,
        string currentWellId = "")
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _session = session;
        _wellDataRepo = wellDataRepo ?? (session != null ? new WellDataRepository(session) : null);
        _currentWellId = currentWellId ?? string.Empty;
    }

    public BroomstickDocumentManagerViewModel(IBroomstickTemplateRepository repository, string currentWellId)
        : this(repository, null, null, currentWellId)
    {
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading Broomstick templates...";
        try
        {
            await _repository.EnsureTableAsync();

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

            // 2. Fetch existing templates from DB
            var templates = await _repository.GetAllTemplatesAsync();

            // 3. Auto-heal any unassigned/empty WellId, LogId, PlanId
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
                        await _repository.SaveTemplateAsync(tpl);
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

            // Auto-select first or default
            SelectedDocumentNode = AllDocumentNodes.FirstOrDefault(n => n.IsDefault) ?? AllDocumentNodes.FirstOrDefault();
            StatusMessage = $"Loaded {AllDocumentNodes.Count} templates";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void BuildTreeHierarchy()
    {
        TreeRoots.Clear();

        // Group by Well
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

            // Sub-group by Plan
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
                         (doc.PlanName?.ToLowerInvariant().Contains(query) ?? false) ||
                         doc.WellId.ToLowerInvariant().Contains(query) ||
                         (doc.WellName?.ToLowerInvariant().Contains(query) ?? false) ||
                         (doc.Description?.ToLowerInvariant().Contains(query) ?? false);

            doc.IsVisibleInFilter = match;
        }

        // Adjust visibility on groups
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
    public async Task NewTemplateAsync()
    {
        string defaultWell = !string.IsNullOrWhiteSpace(SelectedDocumentNode?.WellId) ? SelectedDocumentNode.WellId : _currentWellId;
        string defaultWellName = SelectedDocumentNode?.WellName ?? _currentWellName;
        string defaultWellbore = SelectedDocumentNode?.WellboreId ?? _currentWellboreId;
        string defaultLog = SelectedDocumentNode?.LogId ?? _currentLogId;
        string defaultPlan = !string.IsNullOrWhiteSpace(SelectedDocumentNode?.PlanId) && SelectedDocumentNode.PlanId != "DEFAULT_PLAN"
            ? SelectedDocumentNode.PlanId
            : (!string.IsNullOrWhiteSpace(_currentPlanId) ? _currentPlanId : "DEFAULT_PLAN");
        string defaultPlanName = SelectedDocumentNode?.PlanName ?? _currentPlanName;
        string defaultProfile = !string.IsNullOrWhiteSpace(SelectedDocumentNode?.ProfileId) ? SelectedDocumentNode.ProfileId : "Default Profile";

        string? newName = PromptNewNameHandler != null
            ? PromptNewNameHandler("New Broomstick Template", "Enter a name for the new Broomstick template:")
            : $"Broomstick Template {AllDocumentNodes.Count + 1}";

        if (string.IsNullOrWhiteSpace(newName)) return;

        var newDoc = new BroomstickDocument
        {
            DocumentName = newName.Trim(),
            WellId = defaultWell,
            WellboreId = defaultWellbore,
            LogId = defaultLog,
            PlanId = defaultPlan,
            ProfileId = defaultProfile,
            Profile = new DrillIntel.Data.Objects.DataObjects.Models.BroomstickProfile
            {
                Name = newName.Trim()
            }
        };

        var item = new BroomstickTemplateItem
        {
            TemplateId = newDoc.TemplateId,
            DocumentName = newDoc.DocumentName,
            WellId = newDoc.WellId,
            WellboreId = newDoc.WellboreId,
            LogId = newDoc.LogId,
            PlanId = newDoc.PlanId,
            ProfileId = newDoc.ProfileId,
            TemplateData = newDoc.SerializeToBlob(),
            IsDefault = AllDocumentNodes.Count == 0,
            CreatedBy = Environment.UserName,
            CreatedDate = DateTime.Now,
            ModifiedBy = Environment.UserName,
            ModifiedDate = DateTime.Now
        };

        await _repository.SaveTemplateAsync(item);
        var node = new BroomstickDocumentTreeNode(item)
        {
            WellName = defaultWellName,
            PlanName = defaultPlanName
        };
        AllDocumentNodes.Add(node);
        BuildTreeHierarchy();
        SelectedDocumentNode = node;
        StatusMessage = $"Created template '{newName}'";
    }

    [RelayCommand]
    public async Task RenameTemplateAsync()
    {
        if (SelectedDocumentNode == null) return;

        string current = SelectedDocumentNode.DocumentName;
        string? newName = PromptNewNameHandler != null
            ? PromptNewNameHandler("Rename Broomstick Template", $"Enter a new name for '{current}':")
            : null;

        if (string.IsNullOrWhiteSpace(newName) || newName.Trim().Equals(current, StringComparison.OrdinalIgnoreCase))
            return;

        var item = SelectedDocumentNode.Model;
        item.DocumentName = newName.Trim();
        item.ModifiedDate = DateTime.Now;
        item.ModifiedBy = Environment.UserName;

        await _repository.SaveTemplateAsync(item);
        SelectedDocumentNode.UpdateFromModel(item);
        BuildTreeHierarchy();
        StatusMessage = $"Renamed template to '{newName}'";
    }

    [RelayCommand]
    public async Task SetDefaultAsync()
    {
        if (SelectedDocumentNode == null) return;

        await _repository.SetDefaultTemplateAsync(SelectedDocumentNode.TemplateId, SelectedDocumentNode.WellId);

        // Update local flags
        foreach (var node in AllDocumentNodes.Where(n => n.WellId == SelectedDocumentNode.WellId))
        {
            node.IsDefault = (node.TemplateId == SelectedDocumentNode.TemplateId);
            node.Model.IsDefault = node.IsDefault;
        }

        OnPropertyChanged(nameof(CanSetDefaultSelection));
        StatusMessage = $"Set '{SelectedDocumentNode.DocumentName}' as default template.";
    }

    public Task SetDefaultTemplateAsync() => SetDefaultAsync();

    [RelayCommand]
    public async Task DuplicateTemplateAsync()
    {
        if (SelectedDocumentNode == null) return;

        string copyName = $"{SelectedDocumentNode.DocumentName} (Copy)";
        if (PromptNewNameHandler != null)
        {
            string? input = PromptNewNameHandler("Duplicate Broomstick Template", "Enter a name for the duplicated template:");
            if (!string.IsNullOrWhiteSpace(input)) copyName = input.Trim();
        }

        var clonedItem = await _repository.DuplicateTemplateAsync(SelectedDocumentNode.TemplateId, copyName);
        var node = new BroomstickDocumentTreeNode(clonedItem)
        {
            WellName = SelectedDocumentNode.WellName,
            PlanName = SelectedDocumentNode.PlanName
        };
        AllDocumentNodes.Add(node);
        BuildTreeHierarchy();
        SelectedDocumentNode = node;
        StatusMessage = $"Duplicated template '{copyName}'";
    }

    [RelayCommand]
    public async Task DeleteTemplateAsync()
    {
        if (SelectedDocumentNode == null || !CanDeleteSelection) return;

        bool confirm = ConfirmDeleteHandler == null || ConfirmDeleteHandler(
            "Delete Template",
            $"Are you sure you want to permanently delete template '{SelectedDocumentNode.DocumentName}'?");

        if (!confirm) return;

        string deletedName = SelectedDocumentNode.DocumentName;
        await _repository.DeleteTemplateAsync(SelectedDocumentNode.TemplateId);

        AllDocumentNodes.Remove(SelectedDocumentNode);
        BuildTreeHierarchy();
        SelectedDocumentNode = AllDocumentNodes.FirstOrDefault(n => n.IsDefault) ?? AllDocumentNodes.FirstOrDefault();
        StatusMessage = $"Deleted template '{deletedName}'";
    }

    [RelayCommand]
    public void OpenSelected()
    {
        if (SelectedDocumentNode == null) return;

        SelectedTemplateIdToOpen = SelectedDocumentNode.TemplateId;
        DialogResult = true;
        RequestClose?.Invoke();
    }
}
