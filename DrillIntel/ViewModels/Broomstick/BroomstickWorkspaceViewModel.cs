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



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(HasFieldOrOperator))]

    [NotifyPropertyChangedFor(nameof(CurrentFieldAndOperator))]

    private string _currentWellId = "";



    [ObservableProperty]

    private string _currentWellName = "";



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(HasFieldOrOperator))]

    [NotifyPropertyChangedFor(nameof(CurrentFieldAndOperator))]

    private string _currentFieldName = "";



    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(HasFieldOrOperator))]

    [NotifyPropertyChangedFor(nameof(CurrentFieldAndOperator))]

    private string _currentOperatorName = "";



    [ObservableProperty]

    private string _currentWellboreId = "";



    [ObservableProperty]

    private string _currentLogId = "";



    [ObservableProperty]

    private string _currentLogName = "";



    [ObservableProperty]

    private string _currentPlanId = "";



    [ObservableProperty]

    private string _currentPlanName = "";



    public bool HasFieldOrOperator => !string.IsNullOrWhiteSpace(CurrentFieldName) || !string.IsNullOrWhiteSpace(CurrentOperatorName);



    public string CurrentFieldAndOperator

    {

        get

        {

            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrWhiteSpace(CurrentFieldName)) parts.Add(CurrentFieldName);

            if (!string.IsNullOrWhiteSpace(CurrentOperatorName)) parts.Add(CurrentOperatorName);

            return parts.Count > 0 ? string.Join(" • ", parts) : "";

        }

    }



    public string ActiveProfileName => !string.IsNullOrWhiteSpace(ActiveDocument?.Document?.ProfileId)

        ? ActiveDocument.Document.ProfileId

        : "Default Profile";



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

    [NotifyPropertyChangedFor(nameof(ActiveProfileName))]

    private BroomstickDocumentViewModel? _activeDocument;



    public bool HasOpenDocuments => OpenDocuments.Count > 0;



    [ObservableProperty]



    [NotifyPropertyChangedFor(nameof(HasSelectedNode))]



    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]



    [NotifyPropertyChangedFor(nameof(CanMakeDefaultSelection))]



    [NotifyPropertyChangedFor(nameof(CanDuplicateSelection))]

    [NotifyPropertyChangedFor(nameof(CanRenameSelection))]

    private BroomstickDocumentTreeNode? _selectedTreeNode;



    public bool HasSelectedNode => SelectedTreeNode != null;



    public bool CanDeleteSelection => SelectedTreeNode != null && AllDocumentNodes.Count > 1;



    public bool CanMakeDefaultSelection => SelectedTreeNode != null && !SelectedTreeNode.IsDefault;

    public bool CanDuplicateSelection => SelectedTreeNode != null;

    public bool CanRenameSelection => SelectedTreeNode != null;



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



            var activeWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) 



                               ?? Application.Current?.MainWindow;



            if (activeWindow != null)



            {



                return MessageBox.Show(activeWindow, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;



            }



            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;



        };



        OpenDocuments.CollectionChanged += (s, e) =>



        {



            OnPropertyChanged(nameof(HasOpenDocuments));



        };



    }



    private bool _isInitialized;



    public async Task InitializeAsync()



    {



        if (_isInitialized) return;



        _isInitialized = true;



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



        catch (Exception ex)



        {



            StatusMessage = $"Error: {ex.Message}";



        }



        finally



        {



            IsLoading = false;



            if (StatusMessage == "Loading Broomstick documents...")



            {



                StatusMessage = "Ready";



            }



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

                CurrentWellId = !string.IsNullOrWhiteSpace(well.ObjectID) ? well.ObjectID : CurrentWellId;

                CurrentWellName = !string.IsNullOrWhiteSpace(well.name) ? well.name : "Active Well";

                CurrentFieldName = well.field ?? "";

                CurrentOperatorName = well.operatorName ?? "";

                CurrentWellboreId = well.__timeLogWellboreID ?? well.wellbores?.Keys.FirstOrDefault() ?? "";

            }



            var timeLogs = await _wellDataRepo.GetTimeLogsAsync();

            var primaryLog = timeLogs.FirstOrDefault(t => t.PrimaryLog) ?? timeLogs.FirstOrDefault();

            if (primaryLog != null)

            {

                CurrentLogId = primaryLog.ObjectID ?? "";

                CurrentLogName = !string.IsNullOrWhiteSpace(primaryLog.nameLog) ? primaryLog.nameLog : (primaryLog.ObjectID ?? "");

            }



        }



        if (_session != null && _session.IsProjectOpen && !string.IsNullOrWhiteSpace(CurrentWellId))



        {



            try



            {



                var plans = await Task.Run(() =>



                {



                    var pList = AdnlHookloadPlan.GetMainPlanHeaders(_session.GetDataService(), CurrentWellId, CurrentWellboreId, CurrentLogId);



                    if ((pList == null || pList.Count == 0) && !string.IsNullOrWhiteSpace(CurrentWellId))



                    {



                        pList = AdnlHookloadPlan.GetMainPlanHeaders(_session.GetDataService(), CurrentWellId, null, null);



                    }



                    if (pList == null || pList.Count == 0)



                    {



                        pList = AdnlHookloadPlan.GetMainPlanHeaders(_session.GetDataService(), null, null, null);



                    }



                    return pList;



                });



                if (plans != null && plans.Count > 0)



                {



                    var first = plans.FirstOrDefault();



                    if (first != null)



                    {



                        CurrentPlanId = first.PlanID ?? "";



                        CurrentPlanName = first.Name ?? first.PlanID ?? "";



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



                if (!string.IsNullOrWhiteSpace(CurrentWellId))



                {



                    tpl.WellId = CurrentWellId;



                    modified = true;



                }



            }



            if (string.IsNullOrWhiteSpace(tpl.WellboreId) && !string.IsNullOrWhiteSpace(CurrentWellboreId))



            {



                tpl.WellboreId = CurrentWellboreId;



                modified = true;



            }



            if (string.IsNullOrWhiteSpace(tpl.LogId) && !string.IsNullOrWhiteSpace(CurrentLogId))



            {



                tpl.LogId = CurrentLogId;



                modified = true;



            }



            if (string.IsNullOrWhiteSpace(tpl.PlanId) || tpl.PlanId == "DEFAULT_PLAN")



            {



                if (!string.IsNullOrWhiteSpace(CurrentPlanId))



                {



                    tpl.PlanId = CurrentPlanId;



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



            if (node.WellId == CurrentWellId && !string.IsNullOrWhiteSpace(CurrentWellName))



            {



                node.WellName = CurrentWellName;



            }



            if (node.PlanId != null && _planNames.TryGetValue(node.PlanId, out var pName))



            {



                node.PlanName = pName;



            }



            else if (node.PlanId == CurrentPlanId && !string.IsNullOrWhiteSpace(CurrentPlanName))



            {



                node.PlanName = CurrentPlanName;



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



                    ?? (wellGrp.Key == CurrentWellId && !string.IsNullOrWhiteSpace(CurrentWellName) ? CurrentWellName : wellGrp.Key);



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



        try



        {



            // Check if tab already open



            var existing = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == node.TemplateId);



            if (existing != null)



            {



                ActiveDocument = existing;



                return;



            }



            // Load document



            var item = (await _templateRepo.GetTemplateByIdAsync(node.TemplateId)) ?? node.Model;



            if (item == null) return;



            var doc = BroomstickDocument.DeserializeFromBlob(item.TemplateData) ?? new BroomstickDocument();



            doc.TemplateId = item.TemplateId;



            doc.DocumentName = item.DocumentName;



            doc.WellId = !string.IsNullOrWhiteSpace(item.WellId) ? item.WellId : doc.WellId;



            doc.WellboreId = !string.IsNullOrWhiteSpace(item.WellboreId) ? item.WellboreId : doc.WellboreId;



            doc.LogId = !string.IsNullOrWhiteSpace(item.LogId) ? item.LogId : doc.LogId;



            doc.PlanId = !string.IsNullOrWhiteSpace(item.PlanId) ? item.PlanId : doc.PlanId;



            doc.ProfileId = !string.IsNullOrWhiteSpace(item.ProfileId) ? item.ProfileId : doc.ProfileId;



            doc.IsDefault = item.IsDefault;



            var docVm = new BroomstickDocumentViewModel(_session, _templateRepo, _broomstickDataService, doc);



            WireDialogHandlers(docVm);



            OpenDocuments.Add(docVm);



            ActiveDocument = docVm;



            SelectedTreeNode = node;



            StatusMessage = $"Opening document '{doc.DocumentName}'...";



            await docVm.InitializeAsync();



            StatusMessage = $"Opened document '{doc.DocumentName}'.";



        }



        catch (Exception ex)



        {



            StatusMessage = $"Failed to open document '{node.DocumentName}': {ex.Message}";



        }



    }



    public async Task OpenTemplateByIdAsync(string templateId)



    {



        if (string.IsNullOrWhiteSpace(templateId)) return;



        var existing = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == templateId);



        if (existing != null)



        {



            ActiveDocument = existing;



            return;



        }



        var node = AllDocumentNodes.FirstOrDefault(n => n.TemplateId == templateId);



        if (node != null)



        {



            SelectedTreeNode = node;



            await OpenDocumentNodeAsync(node);



        }



        else



        {



            var item = await _templateRepo.GetTemplateByIdAsync(templateId);



            if (item != null)



            {



                var docNode = new BroomstickDocumentTreeNode(item);



                await OpenDocumentNodeAsync(docNode);



            }



        }



    }



    private static Window? GetActiveDialogOwner()
    {
        if (Application.Current == null) return null;
        return Application.Current.Windows.OfType<BroomstickWorkspaceWindow>().FirstOrDefault(w => w.IsVisible)
            ?? Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
            ?? Application.Current.MainWindow;
    }

    private void WireDialogHandlers(BroomstickDocumentViewModel docVm)
    {
        docVm.OpenSetupDialogHandler = (vm) =>
        {
            if (Application.Current == null) return false;
            bool? result = false;
            void ShowAction()
            {
                var ds = _session?.GetDataService();
                var appDs = Application.Current != null ? App.AppDatabaseService?.GetDataService() : null;
                var setupVm = new BroomstickSetupViewModel(vm.Document, ds, appDs);
                var dlg = new BroomstickSetupDialog(setupVm);
                var owner = GetActiveDialogOwner();
                if (owner != null && owner.IsVisible)
                {
                    dlg.Owner = owner;
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                result = dlg.ShowDialog();
            }

            if (Application.Current.Dispatcher.CheckAccess()) ShowAction();
            else Application.Current.Dispatcher.Invoke(ShowAction);
            return result;
        };

        docVm.OpenBroomstickSetupDialogHandler = (vm) =>
        {
            if (Application.Current == null) return false;
            bool? result = false;
            void ShowAction()
            {
                var ds = _session?.GetDataService();
                var appDb = App.AppDatabaseService;
                var appDataService = appDb?.GetDataService();
                var contextName = _session != null && _session.IsProjectOpen ? $"Project: {_session.ProjectName}" : "Application Master Template";
                var editorVm = new BroomstickProfileEditorViewModel(ds, contextName, appDataService: appDataService, isProjectContext: _session?.IsProjectOpen == true);
                var dlg = new BroomstickProfileEditorWindow
                {
                    DataContext = editorVm
                };
                var owner = GetActiveDialogOwner();
                if (owner != null && owner.IsVisible)
                {
                    dlg.Owner = owner;
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                result = dlg.ShowDialog();
            }

            if (Application.Current.Dispatcher.CheckAccess()) ShowAction();
            else Application.Current.Dispatcher.Invoke(ShowAction);
            return result;
        };

        docVm.OpenTemplateManagerDialogHandler = (mgrVm) =>
        {
            if (Application.Current == null) return false;
            bool? res = false;
            void ShowAction()
            {
                var dlg = new BroomstickDocumentManagerDialog(mgrVm);
                var owner = GetActiveDialogOwner();
                if (owner != null && owner.IsVisible)
                {
                    dlg.Owner = owner;
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                res = dlg.ShowDialog();
                if (res == true && !string.IsNullOrWhiteSpace(mgrVm.SelectedTemplateIdToOpen))
                {
                    _ = OpenTemplateByIdAsync(mgrVm.SelectedTemplateIdToOpen);
                }
            }

            if (Application.Current.Dispatcher.CheckAccess()) ShowAction();
            else Application.Current.Dispatcher.Invoke(ShowAction);
            return res;
        };

        docVm.OpenDataSelectorDialogHandler = (dataVm) =>
        {
            if (Application.Current == null) return false;
            bool? result = false;
            void ShowAction()
            {
                var dlg = new DataSelectorWindow(dataVm)
                {
                    Topmost = true,
                    ShowInTaskbar = true
                };
                var owner = GetActiveDialogOwner();
                if (owner != null && owner.IsVisible)
                {
                    dlg.Owner = owner;
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                result = dlg.ShowDialog();
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                ShowAction();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(ShowAction);
            }
            return result;
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



        string targetWell = !string.IsNullOrWhiteSpace(SelectedTreeNode?.WellId) ? SelectedTreeNode.WellId : CurrentWellId;



        string targetWellbore = !string.IsNullOrWhiteSpace(SelectedTreeNode?.WellboreId) ? SelectedTreeNode.WellboreId : CurrentWellboreId;



        string targetLog = !string.IsNullOrWhiteSpace(SelectedTreeNode?.LogId) ? SelectedTreeNode.LogId : CurrentLogId;



        string targetPlan = !string.IsNullOrWhiteSpace(SelectedTreeNode?.PlanId) && SelectedTreeNode.PlanId != "DEFAULT_PLAN"



            ? SelectedTreeNode.PlanId



            : (!string.IsNullOrWhiteSpace(CurrentPlanId) ? CurrentPlanId : "DEFAULT_PLAN");



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



        if (target == null || target.IsDefault) return;



        await _templateRepo.SetDefaultTemplateAsync(target.TemplateId, target.WellId);



        foreach (var n in AllDocumentNodes.Where(n => n.WellId == target.WellId))



        {



            n.IsDefault = (n.TemplateId == target.TemplateId);



        }



        BuildTreeHierarchy();



        SelectedTreeNode = target;



        StatusMessage = $"'{target.DocumentName}' set as default template.";



    }



    [RelayCommand]



    public async Task DeleteDocumentAsync(BroomstickDocumentTreeNode? node)



    {



        var target = node ?? SelectedTreeNode;



        if (target == null || AllDocumentNodes.Count <= 1) return;



        bool confirm = ConfirmDeleteHandler == null || ConfirmDeleteHandler(



            "Delete Template",



            $"Are you sure you want to permanently delete '{target.DocumentName}'?");



        if (!confirm) return;



        string deletedName = target.DocumentName;



        bool wasDefault = target.IsDefault;



        string wellId = target.WellId;



        // Close tab if open



        var tab = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == target.TemplateId);



        if (tab != null)



        {



            OpenDocuments.Remove(tab);



            if (ActiveDocument == tab)



            {



                ActiveDocument = OpenDocuments.LastOrDefault();



            }



        }



        await _templateRepo.DeleteTemplateAsync(target.TemplateId);



        AllDocumentNodes.Remove(target);



        // If the deleted document was default, promote another document in the same well to default



        if (wasDefault && AllDocumentNodes.Count > 0)



        {



            var newDefault = AllDocumentNodes.FirstOrDefault(n => n.WellId == wellId) ?? AllDocumentNodes.FirstOrDefault();



            if (newDefault != null)



            {



                newDefault.IsDefault = true;



                await _templateRepo.SetDefaultTemplateAsync(newDefault.TemplateId, newDefault.WellId);



            }



        }



        BuildTreeHierarchy();



        TotalDocumentCount = AllDocumentNodes.Count;



        SelectedTreeNode = AllDocumentNodes.FirstOrDefault(n => n.IsDefault) ?? AllDocumentNodes.FirstOrDefault();



        StatusMessage = $"Deleted document '{deletedName}'.";



    }



    [RelayCommand]

    public async Task DuplicateDocumentAsync(BroomstickDocumentTreeNode? node)

    {

        var target = node ?? SelectedTreeNode;

        if (target == null) return;



        string defaultCopy = $"{target.DocumentName} (Copy)";

        string? copyName = PromptNewDocumentNameHandler != null

            ? PromptNewDocumentNameHandler("Copy Broomstick Template", "Enter a name for the copied template:")

            : PromptDocumentNameDialog("Copy Broomstick Template", "Enter a name for the copied template:", defaultCopy);



        if (string.IsNullOrWhiteSpace(copyName)) return;



        var clonedItem = await _templateRepo.DuplicateTemplateAsync(target.TemplateId, copyName.Trim());

        var newNode = new BroomstickDocumentTreeNode(clonedItem)

        {

            WellName = target.WellName,

            PlanName = target.PlanName

        };



        AllDocumentNodes.Add(newNode);

        BuildTreeHierarchy();

        TotalDocumentCount = AllDocumentNodes.Count;

        SelectedTreeNode = newNode;



        await OpenDocumentNodeAsync(newNode);

        StatusMessage = $"Copied template to '{copyName.Trim()}'.";

    }



    [RelayCommand]

    public async Task RenameDocumentAsync(BroomstickDocumentTreeNode? node)

    {

        var target = node ?? SelectedTreeNode;

        if (target == null) return;



        string current = target.DocumentName;

        string? newName = PromptNewDocumentNameHandler != null

            ? PromptNewDocumentNameHandler("Rename Broomstick Template", $"Enter a new name for '{current}':")

            : PromptDocumentNameDialog("Rename Broomstick Template", $"Enter a new name for '{current}':", current);



        if (string.IsNullOrWhiteSpace(newName) || newName.Trim().Equals(current, StringComparison.OrdinalIgnoreCase))

            return;



        var item = (await _templateRepo.GetTemplateByIdAsync(target.TemplateId)) ?? target.Model;

        if (item == null) return;



        item.DocumentName = newName.Trim();

        item.ModifiedDate = DateTime.Now;

        item.ModifiedBy = Environment.UserName;



        try

        {

            var doc = BroomstickDocument.DeserializeFromBlob(item.TemplateData);

            if (doc != null)

            {

                doc.DocumentName = item.DocumentName;

                item.TemplateData = doc.SerializeToBlob();

            }

        }

        catch { }



        await _templateRepo.SaveTemplateAsync(item);

        target.UpdateFromModel(item);



        var openTab = OpenDocuments.FirstOrDefault(d => d.CurrentTemplateId == target.TemplateId);

        if (openTab != null)

        {

            openTab.DocumentName = item.DocumentName;

        }



        BuildTreeHierarchy();

        StatusMessage = $"Renamed template to '{item.DocumentName}'.";

    }



    [RelayCommand]

    public async Task RefreshTreeCommandAsync()

    {

        await RefreshTreeAsync();

        StatusMessage = "Template list refreshed.";

    }



    private static string? PromptDocumentNameDialog(string title, string message, string defaultName)

    {

        if (Application.Current == null) return defaultName;



        string? result = null;

        Application.Current.Dispatcher.Invoke(() =>

        {

            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)

                        ?? Application.Current?.MainWindow;

            var dlg = new Views.Broomstick.BroomstickPromptDialog(title, message, defaultName)

            {

                Owner = owner

            };

            if (dlg.ShowDialog() == true)

            {

                result = dlg.ResponseText?.Trim();

            }

        });

        return result;

    }

}

