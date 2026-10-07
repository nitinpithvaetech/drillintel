using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

[Collection("AppDatabaseCollection")]
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

        // Configure Base database as single source of truth
        UnitConverter.DefaultDataService = _appDataService;
        BaseDatabaseProvider.BaseDataService = _appDataService;

        // Initialize APP_UNIT_CONVERSIONS in app database (Base database)
        UnitConverter.EnsureTableExists(_appDataService, UnitConverter.TableName);
        UnitConverter.CreateDefaultConversion(_appDataService, UnitConverter.TableName);

        // Project database schema does NOT contain APP_UNIT_CONVERSIONS or VMX_UNIT_CONVERSIONS
        _projectDataService.ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS VMX_SCHEMA_INFO (SCHEMA_VERSION INTEGER);
            INSERT INTO VMX_SCHEMA_INFO VALUES (1);
        ");
    }

    public void Dispose()
    {
        UnitConverter.DefaultDataService = null;
        BaseDatabaseProvider.BaseDataService = null;

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

    #region Open Project Tests (APP_UNIT_CONVERSIONS Single Source of Truth)

    [Fact]
    public void OpenProject_AlwaysUsesAppUnitConversions_LoadsConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: "VMX_UNIT_CONVERSIONS", // Legacy parameter redirected to APP_UNIT_CONVERSIONS
            contextName: "Project: MyWellProject",
            isProjectOpen: true,
            databasePath: _projectDbPath);

        Assert.True(vm.IsProjectActive);
        Assert.Equal(UnitConverter.TableName, vm.TableName); // Confirms redirection to APP_UNIT_CONVERSIONS
        Assert.NotEmpty(vm.Conversions);
        Assert.NotEmpty(vm.FilteredConversions);
        Assert.True(vm.Conversions.Count >= 10);

        var first = vm.Conversions.First();
        Assert.True(first.Id > 0);
        Assert.False(string.IsNullOrWhiteSpace(first.FromUnit));
        Assert.False(string.IsNullOrWhiteSpace(first.ToUnit));
        Assert.NotNull(first.CreatedBy);
        Assert.NotNull(first.ModifiedBy);

        // Verify project-level database has NO unit conversion tables or views
        var projectTables = _projectDataService.GetTable("SELECT name FROM sqlite_master WHERE name IN ('APP_UNIT_CONVERSIONS', 'VMX_UNIT_CONVERSIONS');");
        Assert.Empty(projectTables.Rows);
    }

    [Fact]
    public void OpenProject_EditConversion_UpdatesAppUnitConversionsDetails()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: UnitConverter.TableName,
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

        // Verify updated in Base database (APP_UNIT_CONVERSIONS)
        var updatedFromBase = UnitConverter.GetUnitConversion(_appDataService, "psi", "bar", UnitConverter.TableName);
        Assert.NotNull(updatedFromBase);
        Assert.Equal(0.068947, updatedFromBase.Multiplier, precision: 6);
        Assert.Equal("ProjectEngineer", updatedFromBase.ModifiedBy);

        // Verify querying via legacy VMX_UNIT_CONVERSIONS redirects to APP_UNIT_CONVERSIONS in Base database and sees the same updated data
        var updatedViaLegacyRedirect = UnitConverter.GetUnitConversion(_projectDataService, "psi", "bar", "VMX_UNIT_CONVERSIONS");
        Assert.NotNull(updatedViaLegacyRedirect);
        Assert.Equal(0.068947, updatedViaLegacyRedirect.Multiplier, precision: 6);
        Assert.Equal("ProjectEngineer", updatedViaLegacyRedirect.ModifiedBy);

        // Verify project-level database has NO unit conversion tables
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_CONVERSIONS';"));
    }

    [Fact]
    public void OpenProject_DeleteConversion_Confirmed_RemovesFromAppUnitConversions()
    {
        var vm = new UnitConversionMasterViewModel(
            _projectDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        // Add a temporary conversion to Base database (APP_UNIT_CONVERSIONS)
        var tempConv = new UnitConverter("temp_from_o", "temp_to_o", 1.5, 0.0, "TestCat");
        UnitConverter.Add(_appDataService, tempConv, UnitConverter.TableName);
        vm.LoadConversions();

        var item = vm.Conversions.FirstOrDefault(c => c.FromUnit == "temp_from_o");
        Assert.NotNull(item);

        // Cancelled delete does not remove
        vm.ConfirmDeleteHandler = _ => false;
        vm.DeleteConversion(item);
        Assert.NotNull(UnitConverter.GetUnitConversion(_appDataService, "temp_from_o", "temp_to_o", UnitConverter.TableName));

        // Confirmed delete removes from Base database
        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteConversion(item);
        Assert.Null(UnitConverter.GetUnitConversion(_appDataService, "temp_from_o", "temp_to_o", UnitConverter.TableName));
        Assert.Null(UnitConverter.GetUnitConversion(_projectDataService, "temp_from_o", "temp_to_o", "VMX_UNIT_CONVERSIONS")); // Legacy query also returns null
        Assert.DoesNotContain(vm.Conversions, c => c.FromUnit == "temp_from_o");

        // Verify project-level database has NO unit conversion tables
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_CONVERSIONS';"));
    }

    [Fact]
    public void VmxUnitConversions_ReferencesRedirectToAppUnitConversions()
    {
        // Adding via VMX_UNIT_CONVERSIONS tableName should redirect to APP_UNIT_CONVERSIONS in Base database
        var conv = new UnitConverter("redirect_from", "redirect_to", 3.14, 0.0, "RedirectCat");
        bool added = UnitConverter.Add(_projectDataService, conv, "VMX_UNIT_CONVERSIONS");
        Assert.True(added);

        // Ensure it is stored in Base database APP_UNIT_CONVERSIONS
        var stored = UnitConverter.GetUnitConversion(_appDataService, "redirect_from", "redirect_to", UnitConverter.TableName);
        Assert.NotNull(stored);
        Assert.Equal(3.14, stored.Multiplier);
        Assert.Equal("RedirectCat", stored.Category);

        // Direct SQL query on Base database VMX_UNIT_CONVERSIONS view should return the record from APP_UNIT_CONVERSIONS
        var dt = _appDataService.GetTable("SELECT FROM_UNIT, TO_UNIT, MULTIPLIER FROM VMX_UNIT_CONVERSIONS WHERE FROM_UNIT = 'redirect_from';");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal("redirect_from", dt.Rows[0]["FROM_UNIT"]?.ToString());

        // Project database should NOT have APP_UNIT_CONVERSIONS or VMX_UNIT_CONVERSIONS
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_CONVERSIONS';"));

        // Cleanup via legacy redirection
        UnitConverter.Delete(_projectDataService, "redirect_from", "redirect_to", "VMX_UNIT_CONVERSIONS");
        Assert.Null(UnitConverter.GetUnitConversion(_appDataService, "redirect_from", "redirect_to", UnitConverter.TableName));
    }

    [Fact]
    public void OpenAndClosedProject_AlwaysUseAppUnitConversions_MaintainsConsistencyAsSSOT()
    {
        // 1. Closed Project: add a new conversion to APP_UNIT_CONVERSIONS
        var closedVm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        closedVm.OpenAddDialogHandler = c =>
        {
            c.FromUnit = "ssot_from";
            c.ToUnit = "ssot_to";
            c.Multiplier = 12.34;
            c.Offset = 0.0;
            c.Category = "General";
            return true;
        };
        closedVm.AddConversion();

        // Verify stored in Base database APP_UNIT_CONVERSIONS
        var closedConv = UnitConverter.GetUnitConversion(_appDataService, "ssot_from", "ssot_to", UnitConverter.TableName);
        Assert.NotNull(closedConv);
        Assert.Equal(12.34, closedConv.Multiplier);

        // 2. Open Project with connection to app database service and project data service
        var mockAppService = new AppDatabaseService(_appDbPath, _appDbPath);
        var openVm = new UnitConversionMasterViewModel(
            _appDataService,
            tableName: UnitConverter.TableName,
            isProjectOpen: true,
            appDatabaseService: mockAppService,
            databasePath: _appDbPath,
            projectDataService: _projectDataService);

        // Edit the conversion while project is open
        var itemToEdit = openVm.Conversions.FirstOrDefault(c => c.FromUnit == "ssot_from" && c.ToUnit == "ssot_to");
        Assert.NotNull(itemToEdit);

        openVm.OpenEditDialogHandler = c =>
        {
            c.Multiplier = 99.99;
            return true;
        };
        openVm.EditConversion(itemToEdit);

        // Verify APP_UNIT_CONVERSIONS was updated in the application database (Single Source of Truth)
        var appUpdated = UnitConverter.GetUnitConversion(_appDataService, "ssot_from", "ssot_to", UnitConverter.TableName);
        Assert.NotNull(appUpdated);
        Assert.Equal(99.99, appUpdated.Multiplier);

        // Project database should NOT have APP_UNIT_CONVERSIONS
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_CONVERSIONS';"));

        // 3. Delete conversion while project is open
        openVm.ConfirmDeleteHandler = _ => true;
        openVm.DeleteConversion(itemToEdit);

        Assert.Null(UnitConverter.GetUnitConversion(_appDataService, "ssot_from", "ssot_to", UnitConverter.TableName));
    }

    [Fact]
    public void LegacyProject_OpeningViaProjectSession_MigratesVmxUnitConversionsToAppUnitConversions()
    {
        var legacyDbPath = Path.Combine(_tempDir, $"Legacy_Conv_{Guid.NewGuid():N}.dintel");
        using (var ds = new DataServiceDIntel(legacyDbPath))
        {
            ds.ExecuteNonQuery(@"
                CREATE TABLE VMX_UNIT_CONVERSIONS (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    FROM_UNIT TEXT NOT NULL,
                    TO_UNIT TEXT NOT NULL,
                    MULTIPLIER REAL NOT NULL,
                    OFFSET REAL NOT NULL DEFAULT 0.0,
                    CATEGORY TEXT,
                    CREATED_BY TEXT,
                    CREATED_DATE TEXT,
                    MODIFIED_BY TEXT,
                    MODIFIED_DATE TEXT,
                    UNIQUE(FROM_UNIT, TO_UNIT)
                );
                INSERT INTO VMX_UNIT_CONVERSIONS (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY)
                VALUES ('legacy_from', 'legacy_to', 42.0, 0.0, 'LegacyCategory');
            ");
        }

        var session = new DrillIntel.Projects.ProjectSession();
        try
        {
            session.Load(legacyDbPath);

            var projectDs = session.GetDataService();

            // Legacy conversion should be migrated into the Base database (single source of truth)
            var migratedConv = UnitConverter.GetUnitConversion(_appDataService, "legacy_from", "legacy_to", UnitConverter.TableName);
            Assert.NotNull(migratedConv);
            Assert.Equal(42.0, migratedConv.Multiplier);
            Assert.Equal("LegacyCategory", migratedConv.Category);

            // Project-level database should NO LONGER have APP_UNIT_CONVERSIONS or VMX_UNIT_CONVERSIONS
            var projectTables = projectDs.GetTable("SELECT name FROM sqlite_master WHERE name IN ('APP_UNIT_CONVERSIONS', 'VMX_UNIT_CONVERSIONS');");
            Assert.Empty(projectTables.Rows);
            var vmxType = projectDs.GetValue("SELECT type FROM sqlite_master WHERE name='VMX_UNIT_CONVERSIONS';");
            Assert.Null(vmxType);
        }
        finally
        {
            session.Close();
        }
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

