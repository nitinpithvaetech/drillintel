using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

public partial class UnitConversionEditViewModel : ObservableObject
{
    private readonly IDataServiceDIntel _dataService;
    private readonly string _tableName;
    private readonly int _originalId;

    public event Action<bool?>? RequestClose;

    public UnitConverter? ResultConversion { get; private set; }

    public bool IsNew { get; }

    public string TableName => _tableName;

    [ObservableProperty]
    private string _title = "Edit Unit Conversion";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _fromUnit = string.Empty;

    [ObservableProperty]
    private string _toUnit = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _multiplierText = "1.0";

    [ObservableProperty]
    private string _offsetText = "0.0";

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
    public ObservableCollection<string> AvailableUnits { get; } = new();

    public UnitConversionEditViewModel(
        UnitConverter? conversion,
        IDataServiceDIntel dataService,
        string tableName = UnitConverter.TableName,
        bool isNew = false,
        IEnumerable<string>? existingCategories = null,
        IEnumerable<string>? existingUnits = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _tableName = UnitConverter.NormalizeTableName(tableName);
        IsNew = isNew || conversion == null || conversion.ID <= 0;

        if (existingCategories != null)
        {
            foreach (var cat in existingCategories.Where(c => !string.IsNullOrWhiteSpace(c) && c != "All Categories").Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Categories.Add(cat);
            }
        }

        if (existingUnits != null)
        {
            foreach (var u in existingUnits.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                AvailableUnits.Add(u);
            }
        }

        if (conversion != null)
        {
            _originalId = conversion.ID;
            Id = conversion.ID;
            FromUnit = conversion.FromUnit ?? string.Empty;
            ToUnit = conversion.ToUnit ?? string.Empty;
            Category = conversion.Category ?? string.Empty;
            MultiplierText = conversion.Multiplier.ToString("G", CultureInfo.InvariantCulture);
            OffsetText = conversion.Offset.ToString("G", CultureInfo.InvariantCulture);
            CreatedBy = conversion.CreatedBy ?? string.Empty;
            CreatedDate = conversion.CreatedDate ?? string.Empty;
            ModifiedBy = conversion.ModifiedBy ?? string.Empty;
            ModifiedDate = conversion.ModifiedDate ?? string.Empty;
        }

        if (IsNew)
        {
            Title = "Add New Unit Conversion";
            Subtitle = "Register a new unit conversion rule.";
            CreatedBy = Environment.UserName;
            CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ModifiedBy = CreatedBy;
            ModifiedDate = CreatedDate;
        }
        else
        {
            Title = $"Edit Unit Conversion — {FromUnit} → {ToUnit}";
            Subtitle = "Update unit conversion parameters and multiplier.";
        }
    }

    [RelayCommand]
    public void Save()
    {
        ErrorMessage = string.Empty;
        HasError = false;

        string trimmedFrom = FromUnit?.Trim() ?? string.Empty;
        string trimmedTo = ToUnit?.Trim() ?? string.Empty;
        string trimmedCategory = Category?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedFrom))
        {
            ErrorMessage = "From Unit is required and cannot be empty.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedTo))
        {
            ErrorMessage = "To Unit is required and cannot be empty.";
            HasError = true;
            return;
        }

        if (string.Equals(trimmedFrom, trimmedTo, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "From Unit and To Unit cannot be identical.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedCategory))
        {
            ErrorMessage = "Category is required and cannot be empty.";
            HasError = true;
            return;
        }

        if (!double.TryParse(MultiplierText, NumberStyles.Any, CultureInfo.InvariantCulture, out double multiplier) &&
            !double.TryParse(MultiplierText, NumberStyles.Any, CultureInfo.CurrentCulture, out multiplier))
        {
            ErrorMessage = "Multiplier must be a valid numerical value.";
            HasError = true;
            return;
        }

        if (Math.Abs(multiplier) < 1e-15)
        {
            ErrorMessage = "Multiplier cannot be zero.";
            HasError = true;
            return;
        }

        double offset = 0.0;
        if (!string.IsNullOrWhiteSpace(OffsetText))
        {
            if (!double.TryParse(OffsetText, NumberStyles.Any, CultureInfo.InvariantCulture, out offset) &&
                !double.TryParse(OffsetText, NumberStyles.Any, CultureInfo.CurrentCulture, out offset))
            {
                ErrorMessage = "Offset must be a valid numerical value.";
                HasError = true;
                return;
            }
        }

        // Validate uniqueness within the target table
        try
        {
            UnitConverter.EnsureTableExists(_dataService, _tableName);
            var existing = UnitConverter.GetUnitConversion(_dataService, trimmedFrom, trimmedTo, _tableName);
            if (existing != null && existing.ID > 0 && existing.ID != _originalId)
            {
                ErrorMessage = $"A conversion rule from '{trimmedFrom}' to '{trimmedTo}' already exists (ID #{existing.ID}, Category: {existing.Category}).";
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

        var conversion = new UnitConverter
        {
            ID = _originalId,
            FromUnit = trimmedFrom,
            ToUnit = trimmedTo,
            Multiplier = multiplier,
            Offset = offset,
            Category = trimmedCategory,
            CreatedBy = !string.IsNullOrWhiteSpace(CreatedBy) ? CreatedBy : currentUser,
            CreatedDate = !string.IsNullOrWhiteSpace(CreatedDate) ? CreatedDate : now,
            ModifiedBy = currentUser,
            ModifiedDate = now
        };

        ResultConversion = conversion;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        ResultConversion = null;
        RequestClose?.Invoke(false);
    }
}

