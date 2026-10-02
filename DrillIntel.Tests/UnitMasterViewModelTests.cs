using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class UnitMasterViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appDbPath;
    private readonly string _projectDbPath;
    private readonly IDataServiceDIntel _appDataService;
    private readonly IDataServiceDIntel _projectDataService;

    public UnitMasterViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_unitmaster_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appDbPath = Path.Combine(_tempDir, "DrillIntelApp.sqlite");
        _projectDbPath = Path.Combine(_tempDir, "ProjectTest.dintel");

        _appDataService = new DataServiceDIntel(_appDbPath);
        _appDataService.OpenConnection(_appDbPath);

        _projectDataService = new DataServiceDIntel(_projectDbPath);
        _projectDataService.OpenConnection(_projectDbPath);

        // Initialize APP_UNIT_MASTER in app database
        Unit.EnsureTableExists(_appDataService, Unit.TableName);
        Unit.CreateDefaultUnits(_appDataService, Unit.TableName);

        // Initialize VMX_UNIT_MASTER in project database
        Unit.EnsureTableExists(_projectDataService, Unit.ProjectTableName);
        Unit.CreateDefaultUnits(_projectDataService, Unit.ProjectTableName);
    }

    public void Dispose()
    {
        _appDataService.CloseConnection();
        _appDataService.Dispose();

        _projectDataService.CloseConnection();
        _projectDataService.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    #region Closed Project Tests (APP_UNIT_MASTER)

    [Fact]
    public void ClosedProject_ConnectsToAppDatabase_LoadsAppUnitMaster()
    {
        var vm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            contextName: "Application Master Template",
            isProjectOpen: false,
            databasePath: _appDbPath);

        Assert.False(vm.IsProjectActive);
        Assert.Equal(Unit.TableName, vm.TableName);
        Assert.NotEmpty(vm.Units);
        Assert.NotEmpty(vm.FilteredUnits);
        Assert.True(vm.Units.Count >= 20);

        // Check columns exist on items
        var first = vm.Units.First();
        Assert.True(first.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.UnitName));
        Assert.False(string.IsNullOrWhiteSpace(first.Category));
        Assert.NotNull(first.CreatedBy);
        Assert.NotNull(first.CreatedDate);
        Assert.NotNull(first.ModifiedBy);
        Assert.NotNull(first.ModifiedDate);
    }

    [Fact]
    public void ClosedProject_EditUnit_UpdatesAppUnitMasterDetails()
    {
        var vm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "psi");
        Assert.NotNull(item);

        vm.OpenEditDialogHandler = unit =>
        {
            unit.Description = "Pounds per square inch (Updated Closed Test)";
            unit.Category = "Pressure";
            return true;
        };

        vm.EditUnit(item);

        var updatedFromDb = Unit.GetUnit(_appDataService, "psi", Unit.TableName);
        Assert.NotNull(updatedFromDb);
        Assert.Equal("Pounds per square inch (Updated Closed Test)", updatedFromDb.Description);
        Assert.False(string.IsNullOrWhiteSpace(updatedFromDb.ModifiedDate));
    }

    [Fact]
    public void ClosedProject_DeleteUnit_Confirmed_RemovesFromAppUnitMaster()
    {
        var vm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        // Add a temporary unit to delete
        var tempUnit = new Unit("temp_closed_del", "Length", "Temporary Unit");
        Unit.Add(_appDataService, tempUnit, Unit.TableName);
        vm.LoadUnits();

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "temp_closed_del");
        Assert.NotNull(item);

        // Test cancel confirmation first
        vm.ConfirmDeleteHandler = _ => false;
        vm.DeleteUnit(item);

        Assert.NotNull(Unit.GetUnit(_appDataService, "temp_closed_del", Unit.TableName));

        // Test confirmed delete
        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteUnit(item);

        Assert.Null(Unit.GetUnit(_appDataService, "temp_closed_del", Unit.TableName));
        Assert.DoesNotContain(vm.Units, u => u.UnitName == "temp_closed_del");
    }

    #endregion

    #region Open Project Tests (VMX_UNIT_MASTER)

    [Fact]
    public void OpenProject_ConnectsToProjectDatabase_LoadsVmxUnitMaster()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: Unit.ProjectTableName,
            contextName: "Project: TestProject",
            isProjectOpen: true,
            databasePath: _projectDbPath);

        Assert.True(vm.IsProjectActive);
        Assert.Equal(Unit.ProjectTableName, vm.TableName);
        Assert.NotEmpty(vm.Units);
        Assert.NotEmpty(vm.FilteredUnits);
        Assert.True(vm.Units.Count >= 20);

        // Check columns exist on items
        var ft = vm.Units.FirstOrDefault(u => u.UnitName == "ft");
        Assert.NotNull(ft);
        Assert.Equal("Length", ft.Category);
        Assert.NotNull(ft.CreatedBy);
        Assert.NotNull(ft.CreatedDate);
        Assert.NotNull(ft.ModifiedBy);
        Assert.NotNull(ft.ModifiedDate);
    }

    [Fact]
    public void OpenProject_EditUnit_UpdatesVmxUnitMasterDetails()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: Unit.ProjectTableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "ft");
        Assert.NotNull(item);

        vm.OpenEditDialogHandler = unit =>
        {
            unit.Description = "Feet (Project Custom)";
            return true;
        };

        vm.EditUnit(item);

        // Verify updated in project VMX_UNIT_MASTER
        var updatedFromProject = Unit.GetUnit(_projectDataService, "ft", Unit.ProjectTableName);
        Assert.NotNull(updatedFromProject);
        Assert.Equal("Feet (Project Custom)", updatedFromProject.Description);

        // Verify APP_UNIT_MASTER in app DB was NOT modified (isolation!)
        var appUnit = Unit.GetUnit(_appDataService, "ft", Unit.TableName);
        Assert.NotNull(appUnit);
        Assert.NotEqual("Feet (Project Custom)", appUnit.Description);
    }

    [Fact]
    public void OpenProject_DeleteUnit_Confirmed_RemovesFromVmxUnitMaster()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: Unit.ProjectTableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        // Add a temporary unit to delete in VMX_UNIT_MASTER
        var tempUnit = new Unit("temp_open_del", "Torque", "Temp Torque Unit");
        Unit.Add(_projectDataService, tempUnit, Unit.ProjectTableName);
        vm.LoadUnits();

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "temp_open_del");
        Assert.NotNull(item);

        // Cancelled delete does not remove
        vm.ConfirmDeleteHandler = _ => false;
        vm.DeleteUnit(item);
        Assert.NotNull(Unit.GetUnit(_projectDataService, "temp_open_del", Unit.ProjectTableName));

        // Confirmed delete removes
        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteUnit(item);
        Assert.Null(Unit.GetUnit(_projectDataService, "temp_open_del", Unit.ProjectTableName));
        Assert.DoesNotContain(vm.Units, u => u.UnitName == "temp_open_del");
    }

    #endregion

    #region UnitEditViewModel & Form Validation Tests

    [Fact]
    public void UnitEditViewModel_Validation_RequiresUnitNameAndCategory()
    {
        var editVm = new UnitEditViewModel(
            new Unit("", ""),
            _appDataService,
            tableName: Unit.TableName,
            isNew: true);

        editVm.Save();
        Assert.True(editVm.HasError);
        Assert.Contains("Unit Name is required", editVm.ErrorMessage);

        editVm.UnitName = "test_unit";
        editVm.Category = "";
        editVm.Save();
        Assert.True(editVm.HasError);
        Assert.Contains("Unit Category is required", editVm.ErrorMessage);
    }

    [Fact]
    public void UnitEditViewModel_DuplicateUnitName_RejectsSave()
    {
        var editVm = new UnitEditViewModel(
            new Unit("ft", "Length"),
            _appDataService,
            tableName: Unit.TableName,
            isNew: true);

        // "ft" already exists in APP_UNIT_MASTER
        editVm.Save();
        Assert.True(editVm.HasError);
        Assert.Contains("already exists", editVm.ErrorMessage);
    }

    [Fact]
    public void UnitEditViewModel_SaveNewUnit_PopulatesAuditFields()
    {
        var newUnit = new Unit("bar_custom", "Pressure", "Custom Bar Unit");
        var editVm = new UnitEditViewModel(
            newUnit,
            _appDataService,
            tableName: Unit.TableName,
            isNew: true);

        bool? closedResult = null;
        editVm.RequestClose += res => closedResult = res;

        editVm.Save();

        Assert.False(editVm.HasError);
        Assert.True(closedResult);

        var saved = Unit.GetUnit(_appDataService, "bar_custom", Unit.TableName);
        Assert.NotNull(saved);
        Assert.Equal("bar_custom", saved.UnitName);
        Assert.Equal("Pressure", saved.Category);
        Assert.False(string.IsNullOrWhiteSpace(saved.CreatedBy));
        Assert.False(string.IsNullOrWhiteSpace(saved.CreatedDate));
        Assert.False(string.IsNullOrWhiteSpace(saved.ModifiedBy));
        Assert.False(string.IsNullOrWhiteSpace(saved.ModifiedDate));
    }

    #endregion

    #region Filtering & Search Tests

    [Fact]
    public void UnitMasterViewModel_CategoryFilter_FiltersCorrectly()
    {
        var vm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: false);

        vm.SelectedCategory = "Pressure";
        Assert.NotEmpty(vm.FilteredUnits);
        Assert.All(vm.FilteredUnits, u => Assert.Equal("Pressure", u.Category));

        vm.SelectedCategory = "Length";
        Assert.NotEmpty(vm.FilteredUnits);
        Assert.All(vm.FilteredUnits, u => Assert.Equal("Length", u.Category));

        vm.SelectedCategory = "All Categories";
        Assert.Equal(vm.Units.Count, vm.FilteredUnits.Count);
    }

    [Fact]
    public void UnitMasterViewModel_SearchText_FiltersMatchingUnits()
    {
        var vm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: false);

        vm.SearchText = "psi";
        Assert.Contains(vm.FilteredUnits, u => u.UnitName == "psi");

        vm.SearchText = "NonExistentSearchXYZ";
        Assert.Empty(vm.FilteredUnits);
    }

    #endregion
}

