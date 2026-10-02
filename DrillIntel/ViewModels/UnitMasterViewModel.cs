using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

public partial class UnitItemModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _unitName = string.Empty;

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

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isDefault;

    public Unit ToUnit()
    {
        return new Unit
        {
            ID = Id,
            UnitName = UnitName,
            Category = Category,
            Description = Description,
            IsDefault = IsDefault,
            CreatedBy = CreatedBy,
            CreatedDate = CreatedDate,
            ModifiedBy = ModifiedBy,
            ModifiedDate = ModifiedDate
        };
    }

    public static UnitItemModel FromUnit(Unit unit)
    {
        return new UnitItemModel
        {
            Id = unit.ID,
            UnitName = unit.UnitName,
            Category = unit.Category,
            Description = unit.Description,
            IsDefault = unit.IsDefault,
            CreatedBy = unit.CreatedBy,
            CreatedDate = unit.CreatedDate,
            ModifiedBy = unit.ModifiedBy,
            ModifiedDate = unit.ModifiedDate
        };
    }
}

public partial class UnitMasterViewModel : ObservableObject
{
    private readonly IDataServiceDIntel _dataService;
    private readonly string _tableName;
    private readonly IAppDatabaseService? _appDatabaseService;
    private readonly bool _isProjectOpen;

    public event Action<bool?>? RequestClose;

    // Test hooks for headless unit testing
    public Func<Unit, bool>? OpenEditDialogHandler { get; set; }
    public Func<Unit, bool>? OpenAddDialogHandler { get; set; }
    public Func<Unit, bool>? ConfirmDeleteHandler { get; set; }

    [ObservableProperty]
    private string _title = "Unit Master";

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
    private UnitItemModel? _selectedUnit;

    [ObservableProperty]
    private string _totalCountText = "0 Units";

    public ObservableCollection<UnitItemModel> Units { get; } = new();
    public ObservableCollection<UnitItemModel> FilteredUnits { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    public IDataServiceDIntel DataService => _dataService;
    public string TableName => _tableName;

    public UnitMasterViewModel(
        IDataServiceDIntel dataService,
        string? tableName = null,
        string? contextName = null,
        bool isProjectOpen = false,
        IAppDatabaseService? appDatabaseService = null,
        string? databasePath = null)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _isProjectOpen = isProjectOpen;
        IsProjectActive = isProjectOpen;
        _appDatabaseService = appDatabaseService;

        // When project is Open -> table VMX_UNIT_MASTER; when Closed -> table APP_UNIT_MASTER
        _tableName = !string.IsNullOrWhiteSpace(tableName)
            ? tableName
            : (_isProjectOpen ? Unit.ProjectTableName : Unit.TableName);

        TableNameText = _tableName;

        DatabasePath = databasePath ?? (_isProjectOpen
            ? (App.Session?.ProjectFilePath ?? "Active Project Database")
            : (_appDatabaseService?.DatabasePath ?? "DrillIntelApp.sqlite"));

        if (!string.IsNullOrWhiteSpace(contextName))
        {
            Subtitle = contextName;
        }
        else if (_isProjectOpen)
        {
            Subtitle = $"Project Database: {App.Session?.ProjectName ?? "Active Project"}";
        }
        else
        {
            Subtitle = "Application Master Database (Standard Units)";
        }

        LoadUnits();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    public void LoadUnits()
    {
        try
        {
            Unit.EnsureTableExists(_dataService, _tableName);

            var list = Unit.GetList(_dataService, tableName: _tableName);

            // If empty in an active project (VMX_UNIT_MASTER), copy from master template or create defaults
            if (list.Count == 0 && _isProjectOpen)
            {
                if (_appDatabaseService != null)
                {
                    try
                    {
                        var appDataService = _appDatabaseService.GetDataService();
                        var masterUnits = Unit.GetList(appDataService, tableName: Unit.TableName);
                        if (masterUnits.Count > 0)
                        {
                            foreach (var mu in masterUnits)
                            {
                                Unit.Add(_dataService, mu, _tableName);
                            }
                            list = Unit.GetList(_dataService, tableName: _tableName);
                        }
                    }
                    catch
                    {
                        // Fallback
                    }
                }

                if (list.Count == 0)
                {
                    Unit.CreateDefaultUnits(_dataService, _tableName);
                    list = Unit.GetList(_dataService, tableName: _tableName);
                }
            }

            Units.Clear();
            foreach (var u in list)
            {
                Units.Add(UnitItemModel.FromUnit(u));
            }

            RefreshCategories();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to load units from {_tableName}: {ex.Message}", isError: true);
        }
    }

    private void RefreshCategories()
    {
        string currentSelection = SelectedCategory;
        Categories.Clear();
        Categories.Add("All Categories");

        var distinctCategories = Units
            .Select(u => u.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c);

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
        FilteredUnits.Clear();

        var query = Units.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != "All Categories")
        {
            query = query.Where(u => string.Equals(u.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string search = SearchText.Trim();
            query = query.Where(u =>
                u.UnitName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.CreatedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.ModifiedBy.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Id.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in query)
        {
            FilteredUnits.Add(item);
        }

        if (Units.Count == FilteredUnits.Count)
        {
            TotalCountText = $"{Units.Count} {(Units.Count == 1 ? "Unit" : "Units")}";
        }
        else
        {
            TotalCountText = $"Showing {FilteredUnits.Count} of {Units.Count} Units";
        }
    }

    [RelayCommand]
    public void EditUnit(UnitItemModel? item)
    {
        if (item == null) return;

        var unitToEdit = item.ToUnit();

        if (OpenEditDialogHandler != null)
        {
            if (OpenEditDialogHandler(unitToEdit))
            {
                Unit.Edit(_dataService, unitToEdit, _tableName);
                LoadUnits();
                ShowStatus($"Unit '{unitToEdit.UnitName}' updated successfully.", isError: false);
            }
            return;
        }

        var dialogVm = new UnitEditViewModel(unitToEdit, _dataService, tableName: _tableName, isNew: false, existingCategories: Categories);
        var dialog = new DrillIntel.Views.UnitEditDialog
        {
            DataContext = dialogVm,
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialogVm.ResultUnit != null)
        {
            LoadUnits();
            ShowStatus($"Unit '{dialogVm.ResultUnit.UnitName}' updated successfully.", isError: false);
        }
    }

    [RelayCommand]
    public void DeleteUnit(UnitItemModel? item)
    {
        if (item == null) return;

        bool confirmed = false;

        if (ConfirmDeleteHandler != null)
        {
            confirmed = ConfirmDeleteHandler(item.ToUnit());
        }
        else
        {
            var message = $"Are you sure you want to remove unit '{item.UnitName}' (Category: {item.Category})?\n\nThis action cannot be undone.";
            var result = MessageBox.Show(
                message,
                "Confirm Delete Unit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            confirmed = (result == MessageBoxResult.Yes);
        }

        if (!confirmed) return;

        try
        {
            bool deleted = Unit.Delete(_dataService, item.Id, _tableName);
            if (deleted)
            {
                LoadUnits();
                ShowStatus($"Unit '{item.UnitName}' was removed successfully.", isError: false);
            }
            else
            {
                ShowStatus($"Failed to remove unit '{item.UnitName}'.", isError: true);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Error deleting unit: {ex.Message}", isError: true);
        }
    }

    [RelayCommand]
    public void AddUnit()
    {
        if (OpenAddDialogHandler != null)
        {
            var newUnit = new Unit();
            if (OpenAddDialogHandler(newUnit))
            {
                Unit.Add(_dataService, newUnit, _tableName);
                LoadUnits();
                ShowStatus($"Unit '{newUnit.UnitName}' added successfully.", isError: false);
            }
            return;
        }

        var dialogVm = new UnitEditViewModel(new Unit(), _dataService, tableName: _tableName, isNew: true, existingCategories: Categories);
        var dialog = new DrillIntel.Views.UnitEditDialog
        {
            DataContext = dialogVm,
            Owner = Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && dialogVm.ResultUnit != null)
        {
            LoadUnits();
            ShowStatus($"Unit '{dialogVm.ResultUnit.UnitName}' added successfully.", isError: false);
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        LoadUnits();
        ShowStatus("Unit list refreshed successfully.", isError: false);
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
        IsStatusVisible = !string.IsNullOrWhiteSpace(message);
    }

    [RelayCommand]
    public void DismissStatus()
    {
        IsStatusVisible = false;
        StatusMessage = string.Empty;
    }
}

