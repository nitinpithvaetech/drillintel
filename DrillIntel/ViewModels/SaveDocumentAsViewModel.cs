using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models.TChart;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for the Save Document As / Name Document modal dialog.
/// Enforces name uniqueness, provides description entry, and default designation.
/// </summary>
public partial class SaveDocumentAsViewModel : ObservableObject
{
    private readonly IDocTemplateRepository _repository;
    private readonly string? _excludeTemplateId;

    public Action? RequestClose { get; set; }
    public bool DialogResult { get; private set; }

    [ObservableProperty]
    private string _title = "Save Rig State Document";

    [ObservableProperty]
    private string _documentName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SaveDocumentAsViewModel(IDocTemplateRepository repository, string initialName = "", string initialDescription = "", bool isDefault = false, string? excludeTemplateId = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _documentName = initialName;
        _description = initialDescription;
        _isDefault = isDefault;
        _excludeTemplateId = excludeTemplateId;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        string name = DocumentName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "Document name cannot be empty.";
            HasError = true;
            return;
        }

        bool exists = await _repository.TemplateNameExistsAsync(DocTemplateItem.TypeRigState, name, _excludeTemplateId);
        if (exists)
        {
            ErrorMessage = $"A document named '{name}' already exists in this project.";
            HasError = true;
            return;
        }

        HasError = false;
        ErrorMessage = string.Empty;
        DialogResult = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        RequestClose?.Invoke();
    }
}

