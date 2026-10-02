using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class UnitConversionMasterViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appDbPath;
    private readonly string _projectDbPath;
    private readonly IDataServiceDIntel _appDataService;
    private readonly IDataServiceDIntel _projectDataService;

    public UnitConversionMasterViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_unitconv_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appDbPath = Path.Combine(_tempDir, "DrillIntelApp.sqlite");
        _projectDbPath = Path.Combine(_tempDir, "ProjectTest.dintel");

        _appDataService = new DataServiceDIntel(_appDbPath);
        _appDataService.OpenConnection(_appDbPath);

        _projectDataService = new DataServiceDIntel(_projectDbPath);
        _projectDataService.OpenConnection(_projectDbPath);

        // Initialize APP_UNIT_CONVERSIONS in app database
        UnitConverter.EnsureTableExists(_appDataService, UnitConverter.TableName);
        UnitConverter.CreateDefaultConversion(_appDataService, UnitConverter.TableName);

        // Initialize VMX_UNIT_CONVERSIONS in project database
        UnitConverter.EnsureTableExists(_projectDataService, UnitConverter.ProjectTableName);
        UnitConverter.CreateDefaultConversion(_projectDataService, UnitConverter.ProjectTableName);
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

    #region Closed Project Tests (APP_UNIT_CONVERSIONS)

    [Fact]
    public void ClosedProject_ConnectsToAppDatabase_LoadsAppUnitConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            contextName: "Application Master Template",
            isProjectOpen: false,
            databasePath: _appDbPath);

        Assert.False(vm.IsProjectActive);
        Assert.Equal(UnitConverter.TableName, vm.TableName);
        Assert.NotEmpty(vm.Conversions);
        Assert.NotEmpty(vm.FilteredConversions);
        Assert.True(vm.Conversions.Count >= 10);

        // Check columns exist on items
        var first = vm.Conversions.First();
        Assert.True(first.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.FromUnit));
        Assert.False(string.IsNullOrWhiteSpace(first.ToUnit));
        Assert.False(string.IsNullOrWhiteSpace(first.Category));
        Assert.True(first.Multiplier > 0);
        Assert.NotNull(first.CreatedBy);
        Assert.NotNull(first.CreatedDate);
        Assert.NotNull(first.ModifiedBy);
        Assert.NotNull(first.ModifiedDate);
    }

    [Fact]
    public void ClosedProject_EditConversion_UpdatesAppUnitConversionsDetails()
    {
        var vm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        var item = vm.Conversions.FirstOrDefault(c => c.FromUnit == "ft" && c.ToUnit == "m");
        Assert.NotNull(item);

        vm.OpenEditDialogHandler = conv =>
        {
            conv.Multiplier = 0.304801;
            conv.ModifiedBy = "TestAdmin";
            return true;
        };

        vm.EditConversion(item);

        var updatedFromDb = UnitConverter.GetUnitConversion(_appDataService, "ft", "m", UnitConverter.TableName);
        Assert.NotNull(updatedFromDb);
        Assert.Equal(0.304801, updatedFromDb.Multiplier, precision: 6);
        Assert.Equal("TestAdmin", updatedFromDb.ModifiedBy);
    }

    [Fact]
    public void ClosedProject_DeleteConversion_PromptsAndRemovesFromAppUnitConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        var tempConv = new UnitConverter("temp_from_c", "temp_to_c", 2.5, 0.0, "TestCat");
        UnitConverter.Add(_appDataService, tempConv, UnitConverter.TableName);
        vm.LoadConversions();

        var item = vm.Conversions.FirstOrDefault(c => c.FromUnit == "temp_from_c");
        Assert.NotNull(item);

        // Confirmation declined -> record remains
        vm.ConfirmDeleteHandler = _ => false;
        vm.DeleteConversion(item);
        Assert.NotNull(UnitConverter.GetUnitConversion(_appDataService, "temp_from_c", "temp_to_c", UnitConverter.TableName));

        // Confirmation accepted -> record removed
        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteConversion(item);
        Assert.Null(UnitConverter.GetUnitConversion(_appDataService, "temp_from_c", "temp_to_c", UnitConverter.TableName));
    }

    #endregion

    #region Open Project Tests (VMX_UNIT_CONVERSIONS)

    [Fact]
    public void OpenProject_ConnectsToProjectDatabase_LoadsVmxUnitConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: UnitConverter.ProjectTableName,
            contextName: "Project: MyWellProject",
            isProjectOpen: true,
            databasePath: _projectDbPath);

        Assert.True(vm.IsProjectActive);
        Assert.Equal(UnitConverter.ProjectTableName, vm.TableName);
        Assert.NotEmpty(vm.Conversions);
        Assert.NotEmpty(vm.FilteredConversions);
        Assert.True(vm.Conversions.Count >= 10);

        var first = vm.Conversions.First();
        Assert.True(first.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.FromUnit));
        Assert.False(string.IsNullOrWhiteSpace(first.ToUnit));
        Assert.NotNull(first.CreatedBy);
        Assert.NotNull(first.ModifiedBy);
    }

    [Fact]
    public void OpenProject_EditConversion_UpdatesVmxWithoutMutatingAppUnitConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: UnitConverter.ProjectTableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        var item = vm.Conversions.FirstOrDefault(c => c.FromUnit == "psi" && c.ToUnit == "bar");
        Assert.NotNull(item);

        vm.OpenEditDialogHandler = conv =>
        {
            conv.Multiplier = 0.068947;
            conv.ModifiedBy = "ProjectEngineer";
            return true;
        };

        vm.EditConversion(item);

        var updatedFromProject = UnitConverter.GetUnitConversion(_projectDataService, "psi", "bar", UnitConverter.ProjectTableName);
        Assert.NotNull(updatedFromProject);
        Assert.Equal(0.068947, updatedFromProject.Multiplier, precision: 6);
        Assert.Equal("ProjectEngineer", updatedFromProject.ModifiedBy);

        // Verify master DB was NOT mutated
        var appConv = UnitConverter.GetUnitConversion(_appDataService, "psi", "bar", UnitConverter.TableName);
        Assert.NotNull(appConv);
        Assert.NotEqual("ProjectEngineer", appConv.ModifiedBy);
    }

    [Fact]
    public void OpenProject_DeleteConversion_RemovesFromVmxWithoutMutatingAppDb()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: UnitConverter.ProjectTableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        var tempConv = new UnitConverter("temp_from_o", "temp_to_o", 1.5, 0.0, "TestCat");
        UnitConverter.Add(_projectDataService, tempConv, UnitConverter.ProjectTableName);
        vm.LoadConversions();

        var item = vm.Conversions.FirstOrDefault(c => c.FromUnit == "temp_from_o");
        Assert.NotNull(item);

        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteConversion(item);

        Assert.Null(UnitConverter.GetUnitConversion(_projectDataService, "temp_from_o", "temp_to_o", UnitConverter.ProjectTableName));
    }

    #endregion

    #region Filtering and Validation Tests

    [Fact]
    public void FilterBySearchText_FiltersConversionsAcrossUnitsAndCategory()
    {
        var vm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        vm.SearchText = "degF";
        Assert.All(vm.FilteredConversions, c =>
            Assert.True(
                c.FromUnit.Contains("degF", StringComparison.OrdinalIgnoreCase) ||
                c.ToUnit.Contains("degF", StringComparison.OrdinalIgnoreCase) ||
                c.Category.Contains("degF", StringComparison.OrdinalIgnoreCase)));

        vm.SearchText = string.Empty;
        Assert.Equal(vm.Conversions.Count, vm.FilteredConversions.Count);
    }

    [Fact]
    public void FilterByCategory_FiltersConversionsByCategory()
    {
        var vm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        vm.SelectedCategory = "Pressure";
        Assert.NotEmpty(vm.FilteredConversions);
        Assert.All(vm.FilteredConversions, c => Assert.Equal("Pressure", c.Category, ignoreCase: true));

        vm.SelectedCategory = "All Categories";
        Assert.Equal(vm.Conversions.Count, vm.FilteredConversions.Count);
    }

    [Fact]
    public void EditViewModel_ValidateDuplicateConversion_SetsError()
    {
        var existing = UnitConverter.GetUnitConversion(_appDataService, "ft", "m", UnitConverter.TableName);
        Assert.NotNull(existing);

        var editVm = new UnitConversionEditViewModel(
            new UnitConverter(),
            _appDataService,
            tableName: UnitConverter.TableName,
            isNew: true);

        editVm.FromUnit = "ft";
        editVm.ToUnit = "m";
        editVm.Category = "Length";
        editVm.MultiplierText = "0.3048";
        editVm.Save();

        Assert.True(editVm.HasError);
        Assert.Contains("already exists", editVm.ErrorMessage);
        Assert.Null(editVm.ResultConversion);
    }

    [Fact]
    public void EditViewModel_IdenticalUnits_SetsError()
    {
        var editVm = new UnitConversionEditViewModel(
            new UnitConverter(),
            _appDataService,
            tableName: UnitConverter.TableName,
            isNew: true);

        editVm.FromUnit = "psi";
        editVm.ToUnit = "psi";
        editVm.Category = "Pressure";
        editVm.MultiplierText = "1.0";
        editVm.Save();

        Assert.True(editVm.HasError);
        Assert.Contains("cannot be identical", editVm.ErrorMessage);
    }

    [Fact]
    public void EditViewModel_InvalidMultiplier_SetsError()
    {
        var editVm = new UnitConversionEditViewModel(
            new UnitConverter(),
            _appDataService,
            tableName: UnitConverter.TableName,
            isNew: true);

        editVm.FromUnit = "ft";
        editVm.ToUnit = "yd";
        editVm.Category = "Length";
        editVm.MultiplierText = "abc";
        editVm.Save();

        Assert.True(editVm.HasError);
        Assert.Contains("valid numerical value", editVm.ErrorMessage);
    }

    #endregion
}

