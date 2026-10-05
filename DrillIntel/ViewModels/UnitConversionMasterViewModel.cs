using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

public partial class UnitConversionItemModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _fromUnit = string.Empty;

    [ObservableProperty]
    private string _toUnit = string.Empty;

    [ObservableProperty]
    private double _multiplier = 1.0;

    [ObservableProperty]
    private double _offset = 0.0;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _createdBy = string.Empty;

    [ObservableProperty]
    private string _createdDate = string.Empty;

    [ObservableProperty]
    private string _modifiedBy = string.Empty;

    [ObservableProperty]
    private string _modifiedDate = string.Empty;

    public string MultiplierDisplay => Multiplier.ToString("0.######", CultureInfo.InvariantCulture);

    public string OffsetDisplay => Math.Abs(Offset) < 1e-12 ? "0" : Offset.ToString("0.######", CultureInfo.InvariantCulture);

    public string ConversionFormula
    {
        get
        {
            if (Math.Abs(Offset) < 1e-12)
            {
                return $"1 {FromUnit} = {MultiplierDisplay} {ToUnit}";
            }
            string sign = Offset >= 0 ? "+" : "-";
            return $"({FromUnit} × {MultiplierDisplay}) {sign} {Math.Abs(Offset).ToString("0.######", CultureInfo.InvariantCulture)} = {ToUnit}";
        }
    }

    public UnitConverter ToUnitConverter()
    {
        return new UnitConverter
        {
            ID = Id,
            FromUnit = FromUnit,
            ToUnit = ToUnit,
            Multiplier = Multiplier,
            Offset = Offset,
            Category = Category,
            CreatedBy = CreatedBy,
            CreatedDate = CreatedDate,
            ModifiedBy = ModifiedBy,
            ModifiedDate = ModifiedDate
        };
    }

    public static UnitConversionItemModel FromUnitConverter(UnitConverter converter)
    {
        return new UnitConversionItemModel
        {
            Id = converter.ID,
            FromUnit = converter.FromUnit,
            ToUnit = converter.ToUnit,
            Multiplier = converter.Multiplier,
            Offset = converter.Offset,
            Category = converter.Category,
            CreatedBy = converter.CreatedBy,
            CreatedDate = converter.CreatedDate,
            ModifiedBy = converter.ModifiedBy,
            ModifiedDate = converter.ModifiedDate
        };
    }
}

public partial class UnitConversionMasterViewModel : ObservableObject
{
    private readonly IDataServiceDIntel _dataService;
    private readonly string _tableName;
    private readonly IAppDatabaseService? _appDatabaseService;
    private readonly IDataServiceDIntel? _projectDataService;
    private readonly bool _isProjectOpen;

    public event Action<bool?>? RequestClose;

    // Test hooks for headless unit testing
    public Func<UnitConverter, bool>? OpenEditDialogHandler { get; set; }
    public Func<UnitConverter, bool>? OpenAddDialogHandler { get; set; }
    public Func<UnitConverter, bool>? ConfirmDeleteHandler { get; set; }

    [ObservableProperty]
    private string _title = "Unit Conversion";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _tableNameText = string.Empty;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private bool _isProjectActive;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All Categories";

    [ObservableProperty]
    private UnitConversionItemModel? _selectedConversion;

    [ObservableProperty]
    private string _totalCountText = "0 Conversions";

    public ObservableCollection<UnitConversionItemModel> Conversions { get; } = new();
    public ObservableCollection<UnitConversionItemModel> FilteredConversions { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public IDataServiceDIntel DataService => _dataService;
    public string TableName => _tableName;

    public UnitConversionMasterViewModel(
        IDataServiceDIntel dataService,
        string? tableName = null,
        string? contextName = null,
        bool isProjectOpen = false,
        IAppDatabaseService? appDatabaseService = null,
        string? databasePath = null,
        IDataServiceDIntel? projectDataService = null)
    {
        _isProjectOpen = isProjectOpen;
        IsProjectActive = isProjectOpen;
        _appDatabaseService = appDatabaseService;
        _projectDataService = projectDataService;

        // Base database is always the single source of truth for Unit Conversions (APP_UNIT_CONVERSIONS)
        _dataService = (_appDatabaseService != null ? _appDatabaseService.GetDataService() : null)
            ?? UnitConverter.ResolveDataService(dataService);

        _tableName = UnitConverter.NormalizeTableName(tableName);
        TableNameText = _tableName;

        DatabasePath = databasePath ?? (_appDatabaseService?.DatabasePath 
            ?? (App.AppDatabaseService != null ? App.AppDatabaseService.DatabasePath : "DrillIntelApp.sqlite"));

        if (!string.IsNullOrWhiteSpace(contextName))
        {
            Subtitle = contextName;
        }
        else if (_isProjectOpen)
        {
            Subtitle = $"Base Database (APP_UNIT_CONVERSIONS) — Active Project: {App.Session?.ProjectName ?? "Active Project"}";
        }
        else
        {
            Subtitle = "Application Base Database (APP_UNIT_CONVERSIONS)";
        }

        LoadConversions();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    public void LoadConversions()
    {
        Conversions.Clear();

        try
        {
            UnitConverter.EnsureTableExists(_dataService, _tableName);
            var list = UnitConverter.GetList(_dataService, tableName: _tableName);

            if (list.Count == 0)
            {
                UnitConverter.CreateDefaultConversion(_dataService, _tableName);
                list = UnitConverter.GetList(_dataService, tableName: _tableName);
            }

            foreach (var conv in list)
            {
                Conversions.Add(UnitConversionItemModel.FromUnitConverter(conv));
            }

            UpdateCategories();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load unit conversions from {_tableName}: {ex.Message}", isError: true);
        }
    }

    private void UpdateCategories()
    {
        string currentSelection = SelectedCategory;
        Categories.Clear();
        Categories.Add("All Categories");

        var distinctCategories = Conversions
            .Select(c => c.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        foreach (var cat in distinctCategories)
        {
            Categories.Add(cat);
        }

        if (Categories.Contains(currentSelection))
        {
            SelectedCategory = currentSelection;
        }
        else
        {
            SelectedCategory = "All Categories";
        }
    }

    private void ApplyFilter()
    {
        FilteredConversions.Clear();

        var query = Conversions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All Categories")
        {
            query = query.Where(c => string.Equals(c.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string search = SearchText.Trim();
            query = query.Where(c =>
                c.FromUnit.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.ToUnit.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Multiplier.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Offset.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.CreatedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.ModifiedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredConversions.Add(item);
        }

        if (Conversions.Count == FilteredConversions.Count)
        {
            TotalCountText = $"{Conversions.Count} {(Conversions.Count == 1 ? "Conversion" : "Conversions")}";
        }
        else
        {
            TotalCountText = $"Showing {FilteredConversions.Count} of {Conversions.Count} Conversions";
        }
    }

    private void SyncSaveConversion(UnitConverter conversion, bool isNew)
    {
        // Centralized Access in Base Database:
        // All operations (Access, Insert, Update, Delete) are performed ONLY on the Base database (APP_UNIT_CONVERSIONS)
        if (isNew)
        {
            UnitConverter.Add(_dataService, conversion, _tableName);
        }
        else
        {
            UnitConverter.Edit(_dataService, conversion, _tableName);
        }
    }

    private bool SyncDeleteConversion(UnitConversionItemModel item)
    {
        // Centralized Access in Base Database:
        // All operations (Access, Insert, Update, Delete) are performed ONLY on the Base database (APP_UNIT_CONVERSIONS)
        bool deleted = UnitConverter.Delete(_dataService, item.Id, _tableName);
        if (!deleted && !string.IsNullOrWhiteSpace(item.FromUnit) && !string.IsNullOrWhiteSpace(item.ToUnit))
        {
            deleted = UnitConverter.Delete(_dataService, item.FromUnit, item.ToUnit, _tableName);
        }
        return deleted;
    }

    [RelayCommand]
    public void EditConversion(UnitConversionItemModel? item)
    {
        if (item == null) return;

        var convToEdit = item.ToUnitConverter();

        if (OpenEditDialogHandler != null)
        {
            if (OpenEditDialogHandler(convToEdit))
            {
                SyncSaveConversion(convToEdit, isNew: false);
                LoadConversions();
                ShowStatus($"Conversion rule '{convToEdit.FromUnit} → {convToEdit.ToUnit}' updated successfully.", isError: false);
            }
            return;
        }

        var units = Conversions.Select(c => c.FromUnit).Concat(Conversions.Select(c => c.ToUnit)).Distinct();
        var dialogVm = new UnitConversionEditViewModel(convToEdit, _dataService, tableName: _tableName, isNew: false, existingCategories: Categories, existingUnits: units);
        var dialog = new DrillIntel.Views.UnitConversionEditDialog
        {
            DataContext = dialogVm,
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialogVm.ResultConversion != null)
        {
            SyncSaveConversion(dialogVm.ResultConversion, isNew: false);
            LoadConversions();
            ShowStatus($"Conversion rule '{dialogVm.ResultConversion.FromUnit} → {dialogVm.ResultConversion.ToUnit}' updated successfully.", isError: false);
        }
    }

    [RelayCommand]
    public void DeleteConversion(UnitConversionItemModel? item)
    {
        if (item == null) return;

        bool confirmed = false;

        if (ConfirmDeleteHandler != null)
        {
            confirmed = ConfirmDeleteHandler(item.ToUnitConverter());
        }
        else
        {
            var message = $"Are you sure you want to remove conversion rule '{item.FromUnit} → {item.ToUnit}' (Category: {item.Category})?\n\nThis action cannot be undone.";
            var result = MessageBox.Show(
                message,
                "Confirm Delete Conversion Rule",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            confirmed = (result == MessageBoxResult.Yes);
        }

        if (!confirmed) return;

        try
        {
            bool deleted = SyncDeleteConversion(item);
            if (deleted)
            {
                LoadConversions();
                ShowStatus($"Conversion rule '{item.FromUnit} → {item.ToUnit}' was removed successfully.", isError: false);
            }
            else
            {
                ShowStatus($"Failed to remove conversion rule '{item.FromUnit} → {item.ToUnit}'.", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error deleting conversion rule: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    public void AddConversion()
    {
        if (OpenAddDialogHandler != null)
        {
            var newConv = new UnitConverter();
            if (OpenAddDialogHandler(newConv))
            {
                SyncSaveConversion(newConv, isNew: true);
                LoadConversions();
                ShowStatus($"Conversion rule '{newConv.FromUnit} → {newConv.ToUnit}' added successfully.", isError: false);
            }
            return;
        }

        var units = Conversions.Select(c => c.FromUnit).Concat(Conversions.Select(c => c.ToUnit)).Distinct();
        var dialogVm = new UnitConversionEditViewModel(new UnitConverter(), _dataService, tableName: _tableName, isNew: true, existingCategories: Categories, existingUnits: units);
        var dialog = new DrillIntel.Views.UnitConversionEditDialog
        {
            DataContext = dialogVm,
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialogVm.ResultConversion != null)
        {
            SyncSaveConversion(dialogVm.ResultConversion, isNew: true);
            LoadConversions();
            ShowStatus($"Conversion rule '{dialogVm.ResultConversion.FromUnit} → {dialogVm.ResultConversion.ToUnit}' added successfully.", isError: false);
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadConversions();
        ShowStatus("Conversion list refreshed successfully.", isError: false);
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke(true);
    }

    public void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsStatusError = isError;
        IsStatusVisible = true;
    }

    [RelayCommand]
    public void DismissStatus()
    {
        IsStatusVisible = false;
        StatusMessage = string.Empty;
    }
}

