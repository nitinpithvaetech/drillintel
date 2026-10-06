using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models.TChart;
using DrillIntel.Projects;

namespace DrillIntel.ViewModels;

/// <summary>
/// Root node for the TreeView hierarchy containing all Rig State document nodes.
/// </summary>
public partial class RigStateDocumentTreeRootNode : ObservableObject
{
    [ObservableProperty]
    private string _header = "Rig State Documents";

    [ObservableProperty]
    private int _documentCount;

    [ObservableProperty]
    private bool _isExpanded = true;

    public ObservableCollection<RigStateDocumentTreeNode> Children { get; } = new();

    public string BadgeDisplay => DocumentCount.ToString();
}

/// <summary>
/// Master Workspace ViewModel coordinating the Left TreeView navigation pane
/// and the Right Multi-Document tabbed chart workspace.
/// </summary>
public partial class RigStateWorkspaceViewModel : ObservableObject
{
    private readonly ProjectSession _session;
    private readonly IDocTemplateRepository _templateRepo;
    private readonly IChartDataService _chartDataService;
    private readonly IWellDataRepository _wellDataRepository;

    // UI & Testing Handlers / Delegates
    public Func<string, string, bool>? ConfirmDeleteHandler { get; set; }
    public Func<string, string, bool>? ConfirmDeleteDirtyHandler { get; set; }
    public Func<string, string?>? PromptNewDocumentNameHandler { get; set; }
    public Func<DocTemplateItem, string?>? PromptRenameDocumentHandler { get; set; }
    public Action<RigStateDocumentViewModel>? OpenDetachedWindowHandler { get; set; }

    // TreeView Collections
    public ObservableCollection<RigStateDocumentTreeRootNode> TreeRoots { get; } = new();
    public RigStateDocumentTreeRootNode RootCategoryNode { get; } = new();

    // Flat cache of all document nodes
    public ObservableCollection<RigStateDocumentTreeNode> AllDocumentNodes { get; } = new();

    // Open Document Tabs
    public ObservableCollection<RigStateDocumentViewModel> OpenDocuments { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenDocuments))]
    private RigStateDocumentViewModel? _activeDocument;

    public bool HasOpenDocuments => OpenDocuments.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNode))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]
    [NotifyPropertyChangedFor(nameof(CanMakeDefaultSelection))]
    private RigStateDocumentTreeNode? _selectedTreeNode;

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

    // Layout Hook for Fast-Follow Side-by-Side Split View
    [ObservableProperty]
    private bool _isSplitView;

    [ObservableProperty]
    private RigStateDocumentViewModel? _secondaryDocument;

    public RigStateWorkspaceViewModel(
        ProjectSession session,
        IDocTemplateRepository? templateRepo = null,
        IChartDataService? chartDataService = null,
        IWellDataRepository? wellDataRepository = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _templateRepo = templateRepo ?? new DocTemplateRepository(_session);
        _chartDataService = chartDataService ?? new ChartDataService(_session);
        _wellDataRepository = wellDataRepository ?? new WellDataRepository(_session);

        TreeRoots.Add(RootCategoryNode);

        // Default UI Dialog Handlers
        ConfirmDeleteHandler = (title, message) =>
        {
            if (Application.Current == null) return true;
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        };

        ConfirmDeleteDirtyHandler = (title, message) =>
        {
            if (Application.Current == null) return true;
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes;
        };

        OpenDocuments.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasOpenDocuments));
        };
    }

    /// <summary>
    /// Loads document templates from SQLite, builds TreeView nodes, and opens the default document.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading document templates...";
        try
        {
            await RefreshTreeAsync();

            // Automatically open default document tab if no tabs are open yet
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

    /// <summary>
    /// Reloads all templates from the SQLite repository and rebuilds tree nodes.
    /// </summary>
    [RelayCommand]
    public async Task RefreshTreeAsync()
    {
        var templates = await _templateRepo.GetTemplatesAsync(DocTemplateItem.TypeRigState);
        if (templates.Count == 0)
        {
            // Ensure default baseline template exists in database
            var seeded = await _templateRepo.EnsureDefaultRigStateTemplateAsync();
            if (seeded != null)
            {
                templates = new List<DocTemplateItem> { seeded };
            }
        }

        AllDocumentNodes.Clear();
        foreach (var t in templates)
        {
            var node = new RigStateDocumentTreeNode(t);
            AllDocumentNodes.Add(node);
        }

        TotalDocumentCount = AllDocumentNodes.Count;
        RootCategoryNode.DocumentCount = TotalDocumentCount;

        ApplySearchFilter();

        // Update selection if null or removed
        if (SelectedTreeNode == null || !AllDocumentNodes.Any(n => n.TemplateId == SelectedTreeNode.TemplateId))
        {
            SelectedTreeNode = AllDocumentNodes.FirstOrDefault(n => n.IsDefault) ?? AllDocumentNodes.FirstOrDefault();
        }
    }

    private void ApplySearchFilter()
    {
        RootCategoryNode.Children.Clear();
        var filter = SearchFilter?.Trim();

        foreach (var node in AllDocumentNodes)
        {
            bool match = string.IsNullOrWhiteSpace(filter) ||
                         node.DocumentName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                         node.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);

            node.IsVisibleInFilter = match;
            if (match)
            {
                RootCategoryNode.Children.Add(node);
            }
        }

        RootCategoryNode.DocumentCount = RootCategoryNode.Children.Count;
    }

    /// <summary>
    /// Double-click handler: Activates existing tab if already open; otherwise creates and opens a new tab.
    /// </summary>
    [RelayCommand]
    public async Task OpenDocumentNodeAsync(RigStateDocumentTreeNode? node)
    {
        if (node == null) return;

        // 1. Check if tab is already open
        var existingTab = OpenDocuments.FirstOrDefault(d => string.Equals(d.CurrentTemplateId, node.TemplateId, StringComparison.OrdinalIgnoreCase));
        if (existingTab != null)
        {
            ActiveDocument = existingTab;
            return;
        }

        // 2. Fetch template from repository
        var template = await _templateRepo.GetTemplateByIdAsync(node.TemplateType, node.TemplateId);
        if (template == null) return;

        // 3. Construct new RigStateDocumentViewModel with explicit service wiring
        var docVm = new RigStateDocumentViewModel(
            _session,
            _chartDataService,
            _wellDataRepository,
            console: null,
            templateRepo: _templateRepo);

        await docVm.InitializeAsync();
        await docVm.LoadFromTemplateAsync(template, refreshData: true);

        OpenDocuments.Add(docVm);
        ActiveDocument = docVm;
    }

    /// <summary>
    /// Closes an open document tab.
    /// </summary>
    [RelayCommand]
    public async Task CloseDocumentTabAsync(RigStateDocumentViewModel? docVm)
    {
        if (docVm == null) return;

        if (docVm.IsDirty)
        {
            if (Application.Current != null)
            {
                var res = MessageBox.Show(
                    $"Document '{docVm.DocumentName}' has unsaved changes.\n\nDo you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Cancel) return;
                if (res == MessageBoxResult.Yes)
                {
                    await docVm.SaveDocumentAsync();
                }
            }
        }

        int index = OpenDocuments.IndexOf(docVm);
        OpenDocuments.Remove(docVm);

        if (ActiveDocument == docVm)
        {
            if (OpenDocuments.Count > 0)
            {
                int newIndex = Math.Min(index, OpenDocuments.Count - 1);
                ActiveDocument = OpenDocuments[Math.Max(0, newIndex)];
            }
            else
            {
                ActiveDocument = null;
            }
        }
    }

    /// <summary>
    /// Creates a new Rig State Document template and opens it in a new tab.
    /// </summary>
    [RelayCommand]
    public async Task NewDocumentAsync()
    {
        string? docName = null;
        if (PromptNewDocumentNameHandler != null)
        {
            docName = PromptNewDocumentNameHandler("New Rig State Document");
        }
        else
        {
            docName = PromptForDocumentName("New Document", "Enter name for new Rig State Document:", "New Rig State Document");
        }

        if (string.IsNullOrWhiteSpace(docName)) return;

        docName = docName.Trim();
        if (await _templateRepo.TemplateNameExistsAsync(DocTemplateItem.TypeRigState, docName))
        {
            MessageBox.Show($"A document named '{docName}' already exists. Please choose a different name.",
                "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Construct new document ViewModel
        var docVm = new RigStateDocumentViewModel(
            _session,
            _chartDataService,
            _wellDataRepository,
            console: null,
            templateRepo: _templateRepo);

        await docVm.InitializeAsync();
        docVm.DocumentName = docName;
        docVm.CurrentTemplateId = Guid.NewGuid().ToString();
        await docVm.SaveDocumentAsync();

        await RefreshTreeAsync();

        OpenDocuments.Add(docVm);
        ActiveDocument = docVm;
    }

    /// <summary>
    /// Edits / renames the selected document template.
    /// </summary>
    [RelayCommand]
    public async Task EditDocumentAsync(RigStateDocumentTreeNode? node)
    {
        node ??= SelectedTreeNode;
        if (node == null) return;

        var template = await _templateRepo.GetTemplateByIdAsync(node.TemplateType, node.TemplateId);
        if (template == null) return;

        string? newName = null;
        if (PromptRenameDocumentHandler != null)
        {
            newName = PromptRenameDocumentHandler(template);
        }
        else
        {
            newName = PromptForDocumentName("Rename Document", "Enter new document name:", node.DocumentName);
        }

        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), node.DocumentName, StringComparison.OrdinalIgnoreCase))
            return;

        newName = newName.Trim();
        if (await _templateRepo.TemplateNameExistsAsync(node.TemplateType, newName, node.TemplateId))
        {
            MessageBox.Show($"A document named '{newName}' already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        template.DocumentName = newName;
        template.ModifiedDate = DateTime.Now;
        template.ModifiedBy = Environment.UserName;

        // If template has payload, update console name inside payload
        var docData = RigStateDocumentData.FromBytes(template.TemplateData);
        if (docData?.ConsoleModel != null)
        {
            docData.ConsoleModel.Name = newName;
            template.TemplateData = docData.ToBytes();
        }

        await _templateRepo.SaveTemplateAsync(template);
        node.UpdateFromModel(template);

        // Update open tab if active
        var openTab = OpenDocuments.FirstOrDefault(d => string.Equals(d.CurrentTemplateId, node.TemplateId, StringComparison.OrdinalIgnoreCase));
        if (openTab != null)
        {
            openTab.DocumentName = newName;
        }
    }

    /// <summary>
    /// Sets the selected template as the default Rig State Document for the project.
    /// </summary>
    [RelayCommand]
    public async Task MakeDefaultAsync(RigStateDocumentTreeNode? node)
    {
        node ??= SelectedTreeNode;
        if (node == null || node.IsDefault) return;

        await _templateRepo.SetDefaultTemplateAsync(node.TemplateType, node.TemplateId);

        foreach (var n in AllDocumentNodes)
        {
            n.IsDefault = string.Equals(n.TemplateId, node.TemplateId, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(CanMakeDefaultSelection));
    }

    /// <summary>
    /// Deletes the selected template with dirty-check confirmation and tab synchronization.
    /// </summary>
    [RelayCommand]
    public async Task DeleteDocumentAsync(RigStateDocumentTreeNode? node)
    {
        node ??= SelectedTreeNode;
        if (node == null) return;

        if (AllDocumentNodes.Count <= 1)
        {
            MessageBox.Show("Cannot delete the only remaining Rig State document.", "Delete Prevented", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check if document is currently open in a tab
        var openTab = OpenDocuments.FirstOrDefault(d => string.Equals(d.CurrentTemplateId, node.TemplateId, StringComparison.OrdinalIgnoreCase));

        // Requirement 4: Delete-while-dirty behavior
        if (openTab != null && openTab.IsDirty)
        {
            bool proceedDirty = ConfirmDeleteDirtyHandler?.Invoke(
                "Unsaved Changes",
                "This document is currently open with unsaved changes. Delete anyway?") ?? false;

            if (!proceedDirty)
            {
                return; // User aborted deletion
            }
        }
        else
        {
            bool confirm = ConfirmDeleteHandler?.Invoke(
                "Confirm Delete",
                $"Are you sure you want to delete '{node.DocumentName}'?") ?? false;

            if (!confirm)
            {
                return;
            }
        }

        // Perform deletion
        await _templateRepo.DeleteTemplateAsync(node.TemplateType, node.TemplateId);

        // Close tab if open
        if (openTab != null)
        {
            openTab.IsDirty = false; // Prevent redundant unsaved prompt
            OpenDocuments.Remove(openTab);
            if (ActiveDocument == openTab)
            {
                ActiveDocument = OpenDocuments.LastOrDefault();
            }
        }

        await RefreshTreeAsync();
    }

    /// <summary>
    /// Duplicates the selected template and adds it to the tree.
    /// </summary>
    [RelayCommand]
    public async Task DuplicateDocumentAsync(RigStateDocumentTreeNode? node)
    {
        node ??= SelectedTreeNode;
        if (node == null) return;

        string suggestedName = $"{node.DocumentName} (Copy)";
        string? newName = null;

        if (PromptNewDocumentNameHandler != null)
        {
            newName = PromptNewDocumentNameHandler(suggestedName);
        }
        else
        {
            newName = PromptForDocumentName("Duplicate Document", "Enter name for duplicated document:", suggestedName);
        }

        if (string.IsNullOrWhiteSpace(newName)) return;

        newName = newName.Trim();
        if (await _templateRepo.TemplateNameExistsAsync(node.TemplateType, newName))
        {
            MessageBox.Show($"A document named '{newName}' already exists.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var duplicated = await _templateRepo.DuplicateTemplateAsync(node.TemplateType, node.TemplateId, newName);
        await RefreshTreeAsync();

        var newNode = AllDocumentNodes.FirstOrDefault(n => n.TemplateId == duplicated.TemplateId);
        if (newNode != null)
        {
            SelectedTreeNode = newNode;
            await OpenDocumentNodeAsync(newNode);
        }
    }

    /// <summary>
    /// Detaches the specified tab into an independent floating top-level window.
    /// </summary>
    [RelayCommand]
    public void OpenDetachedWindow(RigStateDocumentViewModel? docVm)
    {
        docVm ??= ActiveDocument;
        if (docVm == null) return;

        if (OpenDetachedWindowHandler != null)
        {
            OpenDetachedWindowHandler(docVm);
        }
        else
        {
            var window = new Views.RigStateDocumentWindow
            {
                DataContext = docVm,
                Owner = Application.Current?.MainWindow
            };
            window.Show();
        }
    }

    /// <summary>
    /// Layout Hook: Toggles Side-by-Side split mode (fast-follow feature).
    /// </summary>
    [RelayCommand]
    public void ToggleSplitView()
    {
        IsSplitView = !IsSplitView;
        if (IsSplitView && SecondaryDocument == null && OpenDocuments.Count > 1)
        {
            SecondaryDocument = OpenDocuments.FirstOrDefault(d => d != ActiveDocument);
        }
    }

    private string? PromptForDocumentName(string title, string message, string defaultName)
    {
        // Simple dialog prompt helper
        var vm = new SaveDocumentAsViewModel(_templateRepo, defaultName, string.Empty);
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;
        var dialog = new Views.SaveDocumentAsDialog
        {
            DataContext = vm,
            Owner = owner
        };
        vm.RequestClose = () => dialog.DialogResult = vm.DialogResult;
        bool? res = dialog.ShowDialog();
        return res == true && vm.DialogResult ? vm.DocumentName : null;
    }
}
