using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

public partial class UnitEditViewModel : ObservableObject
{
    private readonly IDataServiceDIntel _dataService;
    private readonly string _tableName;
    private readonly int _originalId;

    public event Action<bool?>? RequestClose;

    public Unit? ResultUnit { get; private set; }

    public bool IsNew { get; }

    public string TableName => _tableName;

    [ObservableProperty]
    private string _title = "Edit Unit";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _unitName = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _createdBy = string.Empty;

    [ObservableProperty]
    private string _createdDate = string.Empty;

    [ObservableProperty]
    private string _modifiedBy = string.Empty;

    [ObservableProperty]
    private string _modifiedDate = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public ObservableCollection<string> Categories { get; } = new();

    public UnitEditViewModel(
        Unit? unit,
        IDataServiceDIntel dataService,
        string tableName = Unit.TableName,
        bool isNew = false,
        IEnumerable<string>? existingCategories = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _tableName = !string.IsNullOrWhiteSpace(tableName) ? tableName : Unit.TableName;
        IsNew = isNew || unit == null || unit.ID <= 0;

        if (existingCategories != null)
        {
            foreach (var cat in existingCategories.Where(c => !string.IsNullOrWhiteSpace(c) && c != "All Categories").Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Categories.Add(cat);
            }
        }

        if (unit != null)
        {
            _originalId = unit.ID;
            Id = unit.ID;
            UnitName = unit.UnitName ?? string.Empty;
            Category = unit.Category ?? string.Empty;
            Description = unit.Description ?? string.Empty;
            IsDefault = unit.IsDefault;
            CreatedBy = unit.CreatedBy ?? string.Empty;
            CreatedDate = unit.CreatedDate ?? string.Empty;
            ModifiedBy = unit.ModifiedBy ?? string.Empty;
            ModifiedDate = unit.ModifiedDate ?? string.Empty;
        }

        if (IsNew)
        {
            Title = "Add New Unit";
            Subtitle = "Register a new measurement unit.";
            CreatedBy = Environment.UserName;
            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ModifiedBy = CreatedBy;
            ModifiedDate = CreatedDate;
        }
        else
        {
            Title = $"Edit Unit — {UnitName}";
            Subtitle = "Update unit properties and settings.";
        }
    }

    [RelayCommand]
    public void Save()
    {
        ErrorMessage = string.Empty;
        HasError = false;

        string trimmedName = UnitName?.Trim() ?? string.Empty;
        string trimmedCategory = Category?.Trim() ?? string.Empty;
        string trimmedDescription = Description?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            ErrorMessage = "Unit Name is required and cannot be empty.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedCategory))
        {
            ErrorMessage = "Unit Category is required and cannot be empty.";
            HasError = true;
            return;
        }

        // Validate uniqueness within the target table
        try
        {
            Unit.EnsureTableExists(_dataService, _tableName);
            var existing = Unit.GetUnit(_dataService, trimmedName, _tableName);
            if (existing != null && existing.ID != _originalId)
            {
                ErrorMessage = $"A unit named '{trimmedName}' already exists (ID #{existing.ID}, Category: {existing.Category}).";
                HasError = true;
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Validation check failed: {ex.Message}";
            HasError = true;
            return;
        }

        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string currentUser = Environment.UserName;

        var unit = new Unit
        {
            ID = _originalId,
            UnitName = trimmedName,
            Category = trimmedCategory,
            Description = trimmedDescription,
            IsDefault = IsDefault,
            CreatedBy = !string.IsNullOrWhiteSpace(CreatedBy) ? CreatedBy : currentUser,
            CreatedDate = !string.IsNullOrWhiteSpace(CreatedDate) ? CreatedDate : now,
            ModifiedBy = currentUser,
            ModifiedDate = now
        };

        try
        {
            bool success = IsNew
                ? Unit.Add(_dataService, unit, _tableName)
                : Unit.Edit(_dataService, unit, _tableName);

            if (!success)
            {
                ErrorMessage = $"Database operation failed. Unable to save unit in {_tableName}.";
                HasError = true;
                return;
            }

            ResultUnit = unit;
            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving unit: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        ResultUnit = null;
        RequestClose?.Invoke(false);
    }
}

