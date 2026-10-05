using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models.TChart;

namespace DrillIntel.ViewModels;

/// <summary>
/// Display item wrapper for a document template in the manager dialog.
/// </summary>
public partial class DocTemplateItemViewModel : ObservableObject
{
    public DocTemplateItem Model { get; }

    public string TemplateId => Model.TemplateId;
    public string TemplateType => Model.TemplateType;

    [ObservableProperty]
    private string _documentName;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _createdBy;

    [ObservableProperty]
    private string _modifiedBy;

    [ObservableProperty]
    private string _modifiedDateDisplay;

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private int _channelCount;

    [ObservableProperty]
    private string _orientationDisplay = "Horizontal";

    public DocTemplateItemViewModel(DocTemplateItem model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _documentName = model.DocumentName;
        _description = model.Description ?? string.Empty;
        _isDefault = model.IsDefault;
        _createdBy = model.CreatedBy ?? "SYSTEM";
        _modifiedBy = model.ModifiedBy ?? "SYSTEM";
        _modifiedDateDisplay = model.ModifiedDate?.ToString("yyyy-MM-dd HH:mm") ?? "-";

        // Parse payload to get track metrics
        var docData = RigStateDocumentData.FromBytes(model.TemplateData);
        if (docData?.ConsoleModel != null)
        {
            _trackCount = docData.ConsoleModel.Tracks?.Count ?? 0;
            _channelCount = docData.ConsoleModel.Tracks?.Sum(t => t.Channels?.Count ?? 0) ?? 0;
            _orientationDisplay = docData.ConsoleModel.TrackOrientation.ToString();
        }
    }
}

/// <summary>
/// Master ViewModel for the Rig State Document Manager dialog.
/// Provides industry-standard search, preview, opening, default setting, duplication, and deletion.
/// </summary>
public partial class RigStateDocumentManagerViewModel : ObservableObject
{
    private readonly IDocTemplateRepository _repository;

    public Action? RequestClose { get; set; }
    public bool DialogResult { get; private set; }

    // Test / UI Hook for Save As / Rename prompt
    public Func<SaveDocumentAsViewModel, bool?>? OpenSaveAsDialogHandler { get; set; }
    public Func<string, string, bool>? ConfirmDeleteHandler { get; set; }

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    public ObservableCollection<DocTemplateItemViewModel> Documents { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]
    private DocTemplateItemViewModel? _selectedDocument;

    public bool HasSelection => SelectedDocument != null;
    public bool CanDeleteSelection => SelectedDocument != null && Documents.Count > 1;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public DocTemplateItem? SelectedTemplateForOpen { get; private set; }

    public RigStateDocumentManagerViewModel(IDocTemplateRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task InitializeAsync()
    {
        await LoadDocumentsAsync();
    }

    public async Task LoadDocumentsAsync()
    {
        var list = await _repository.GetTemplatesAsync(DocTemplateItem.TypeRigState);
        Documents.Clear();

        string filter = SearchFilter?.Trim().ToLowerInvariant() ?? string.Empty;

        foreach (var item in list)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                item.DocumentName.ToLowerInvariant().Contains(filter) ||
                (item.Description ?? string.Empty).ToLowerInvariant().Contains(filter))
            {
                Documents.Add(new DocTemplateItemViewModel(item));
            }
        }

        SelectedDocument = Documents.FirstOrDefault(d => d.IsDefault) ?? Documents.FirstOrDefault();
        StatusMessage = $"{Documents.Count} document(s) in active project.";
    }

    partial void OnSearchFilterChanged(string value)
    {
        _ = LoadDocumentsAsync();
    }

    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedDocument == null) return;
        SelectedTemplateForOpen = SelectedDocument.Model;
        DialogResult = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private async Task SetAsDefaultAsync()
    {
        if (SelectedDocument == null) return;

        await _repository.SetDefaultTemplateAsync(DocTemplateItem.TypeRigState, SelectedDocument.TemplateId);
        string currentId = SelectedDocument.TemplateId;

        await LoadDocumentsAsync();
        SelectedDocument = Documents.FirstOrDefault(d => d.TemplateId == currentId);
        StatusMessage = $"'{SelectedDocument?.DocumentName}' is now set as the default document.";
    }

    [RelayCommand]
    private async Task DuplicateAsync()
    {
        if (SelectedDocument == null) return;

        string baseName = $"Copy of {SelectedDocument.DocumentName}";
        string newName = baseName;
        int counter = 2;
        while (await _repository.TemplateNameExistsAsync(DocTemplateItem.TypeRigState, newName))
        {
            newName = $"{baseName} ({counter++})";
        }

        var vm = new SaveDocumentAsViewModel(_repository, newName, SelectedDocument.Description, false);
        bool? res = OpenSaveAsDialogHandler != null ? OpenSaveAsDialogHandler(vm) : ShowSaveAsDialog(vm);

        if (res == true && vm.DialogResult)
        {
            var clone = await _repository.DuplicateTemplateAsync(DocTemplateItem.TypeRigState, SelectedDocument.TemplateId, vm.DocumentName.Trim());
            await LoadDocumentsAsync();
            SelectedDocument = Documents.FirstOrDefault(d => d.TemplateId == clone.TemplateId);
            StatusMessage = $"Duplicated '{vm.DocumentName}' successfully.";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedDocument == null) return;

        if (Documents.Count <= 1)
        {
            MessageBox.Show("Cannot delete the only remaining document in the project.", "Delete Document", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string docName = SelectedDocument.DocumentName;
        bool confirmed = ConfirmDeleteHandler != null
            ? ConfirmDeleteHandler($"Are you sure you want to permanently delete the document '{docName}'?", "Confirm Delete")
            : MessageBox.Show($"Are you sure you want to permanently delete the document '{docName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        if (confirmed)
        {
            await _repository.DeleteTemplateAsync(DocTemplateItem.TypeRigState, SelectedDocument.TemplateId);
            await LoadDocumentsAsync();
            StatusMessage = $"Deleted document '{docName}'.";
        }
    }

    [RelayCommand]
    private void Close()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }

    private bool? ShowSaveAsDialog(SaveDocumentAsViewModel vm)
    {
        var win = new Views.SaveDocumentAsDialog
        {
            DataContext = vm,
            Owner = Application.Current?.MainWindow
        };
        vm.RequestClose = () => win.DialogResult = vm.DialogResult;
        return win.ShowDialog();
    }
}

