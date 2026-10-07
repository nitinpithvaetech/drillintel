using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

[Collection("AppDatabaseCollection")]
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

        // Configure Base database as single source of truth
        Unit.DefaultDataService = _appDataService;
        BaseDatabaseProvider.BaseDataService = _appDataService;

        // Initialize APP_UNIT_MASTER in app database (Base database)
        Unit.EnsureTableExists(_appDataService, Unit.TableName);
        Unit.CreateDefaultUnits(_appDataService, Unit.TableName);

        // Project database schema does NOT contain APP_UNIT_MASTER or VMX_UNIT_MASTER
        _projectDataService.ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS VMX_SCHEMA_INFO (SCHEMA_VERSION INTEGER);
            INSERT INTO VMX_SCHEMA_INFO VALUES (1);
        ");
    }

    public void Dispose()
    {
        Unit.DefaultDataService = null;
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

    #region Open Project Tests (APP_UNIT_MASTER Single Source of Truth)

    [Fact]
    public void OpenProject_AlwaysUsesAppUnitMaster_LoadsUnits()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: "VMX_UNIT_MASTER", // Legacy parameter redirected to APP_UNIT_MASTER
            contextName: "Project: TestProject",
            isProjectOpen: true,
            databasePath: _projectDbPath);

        Assert.True(vm.IsProjectActive);
        Assert.Equal(Unit.TableName, vm.TableName); // Confirms redirection to APP_UNIT_MASTER
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

        // Verify project-level database has NO unit tables or views
        var projectTables = _projectDataService.GetTable("SELECT name FROM sqlite_master WHERE name IN ('APP_UNIT_MASTER', 'VMX_UNIT_MASTER');");
        Assert.Empty(projectTables.Rows);
    }

    [Fact]
    public void OpenProject_EditUnit_UpdatesAppUnitMasterDetails()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: Unit.TableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "ft");
        Assert.NotNull(item);

        vm.OpenEditDialogHandler = unit =>
        {
            unit.Description = "Feet (Global Master Updated)";
            return true;
        };

        vm.EditUnit(item);

        // Verify updated in Base database (APP_UNIT_MASTER)
        var updatedFromBase = Unit.GetUnit(_appDataService, "ft", Unit.TableName);
        Assert.NotNull(updatedFromBase);
        Assert.Equal("Feet (Global Master Updated)", updatedFromBase.Description);

        // Verify reading via legacy VMX_UNIT_MASTER redirects to APP_UNIT_MASTER in Base database and sees the same updated data
        var updatedViaLegacyRedirect = Unit.GetUnit(_projectDataService, "ft", "VMX_UNIT_MASTER");
        Assert.NotNull(updatedViaLegacyRedirect);
        Assert.Equal("Feet (Global Master Updated)", updatedViaLegacyRedirect.Description);

        // Verify project-level database has NO unit tables
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_MASTER';"));
    }

    [Fact]
    public void OpenProject_DeleteUnit_Confirmed_RemovesFromAppUnitMaster()
    {
        var vm = new UnitMasterViewModel(
            _projectDataService,
            tableName: Unit.TableName,
            isProjectOpen: true,
            databasePath: _projectDbPath);

        // Add a temporary unit to delete in Base database (APP_UNIT_MASTER)
        var tempUnit = new Unit("temp_open_del", "Torque", "Temp Torque Unit");
        Unit.Add(_appDataService, tempUnit, Unit.TableName);
        vm.LoadUnits();

        var item = vm.Units.FirstOrDefault(u => u.UnitName == "temp_open_del");
        Assert.NotNull(item);

        // Cancelled delete does not remove
        vm.ConfirmDeleteHandler = _ => false;
        vm.DeleteUnit(item);
        Assert.NotNull(Unit.GetUnit(_appDataService, "temp_open_del", Unit.TableName));

        // Confirmed delete removes from Base database
        vm.ConfirmDeleteHandler = _ => true;
        vm.DeleteUnit(item);
        Assert.Null(Unit.GetUnit(_appDataService, "temp_open_del", Unit.TableName));
        Assert.Null(Unit.GetUnit(_projectDataService, "temp_open_del", "VMX_UNIT_MASTER")); // Legacy query also returns null
        Assert.DoesNotContain(vm.Units, u => u.UnitName == "temp_open_del");

        // Verify project-level database has NO unit tables
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_MASTER';"));
    }

    [Fact]
    public void VmxUnitMaster_ReferencesRedirectToAppUnitMaster()
    {
        // Adding via VMX_UNIT_MASTER tableName should redirect to APP_UNIT_MASTER in Base database
        var unit = new Unit("redirect_test_uom", "Pressure", "Redirection Test");
        bool added = Unit.Add(_projectDataService, unit, "VMX_UNIT_MASTER");
        Assert.True(added);

        // Ensure it is stored in Base database APP_UNIT_MASTER
        var stored = Unit.GetUnit(_appDataService, "redirect_test_uom", Unit.TableName);
        Assert.NotNull(stored);
        Assert.Equal("Pressure", stored.Category);

        // Direct SQL query on Base database VMX_UNIT_MASTER view should return the record from APP_UNIT_MASTER
        var dt = _appDataService.GetTable("SELECT UNIT_NAME, CATEGORY FROM VMX_UNIT_MASTER WHERE UNIT_NAME = 'redirect_test_uom';");
        Assert.NotNull(dt);
        Assert.Single(dt.Rows);
        Assert.Equal("redirect_test_uom", dt.Rows[0]["UNIT_NAME"]?.ToString());

        // Project database should NOT have APP_UNIT_MASTER or VMX_UNIT_MASTER
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_MASTER';"));

        // Cleanup
        Unit.Delete(_projectDataService, "redirect_test_uom", "VMX_UNIT_MASTER");
        Assert.Null(Unit.GetUnit(_appDataService, "redirect_test_uom", Unit.TableName));
    }

    [Fact]
    public void OpenAndClosedProject_AlwaysUseAppUnitMaster_MaintainsConsistencyAsSSOT()
    {
        // 1. Closed Project: add a new unit to APP_UNIT_MASTER
        var closedVm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: false,
            databasePath: _appDbPath);

        closedVm.OpenAddDialogHandler = u =>
        {
            u.UnitName = "ssot_psi_test";
            u.Category = "Pressure";
            u.Description = "SSOT Test Unit Closed";
            return true;
        };
        closedVm.AddUnit();

        // Verify stored in Base database APP_UNIT_MASTER
        var closedUnit = Unit.GetUnit(_appDataService, "ssot_psi_test", Unit.TableName);
        Assert.NotNull(closedUnit);
        Assert.Equal("SSOT Test Unit Closed", closedUnit.Description);

        // 2. Open Project with connection to project database and app database service
        var mockAppService = new AppDatabaseService(_appDbPath, _appDbPath);
        var openVm = new UnitMasterViewModel(
            _appDataService,
            tableName: Unit.TableName,
            isProjectOpen: true,
            appDatabaseService: mockAppService,
            databasePath: _appDbPath,
            projectDataService: _projectDataService);

        // Edit the unit while project is open
        var itemToEdit = openVm.Units.FirstOrDefault(u => u.UnitName == "ssot_psi_test");
        Assert.NotNull(itemToEdit);

        openVm.OpenEditDialogHandler = u =>
        {
            u.Description = "SSOT Test Unit Updated While Open";
            return true;
        };
        openVm.EditUnit(itemToEdit);

        // Verify APP_UNIT_MASTER was updated in the application database (Single Source of Truth)
        var appUpdated = Unit.GetUnit(_appDataService, "ssot_psi_test", Unit.TableName);
        Assert.NotNull(appUpdated);
        Assert.Equal("SSOT Test Unit Updated While Open", appUpdated.Description);

        // Project database should NOT have APP_UNIT_MASTER
        Assert.Null(_projectDataService.GetValue("SELECT 1 FROM sqlite_master WHERE name='APP_UNIT_MASTER';"));

        // 3. Delete unit while project is open
        openVm.ConfirmDeleteHandler = _ => true;
        openVm.DeleteUnit(itemToEdit);

        Assert.Null(Unit.GetUnit(_appDataService, "ssot_psi_test", Unit.TableName));
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

    #region Legacy Project Migration Tests

    [Fact]
    public void LegacyProject_OpeningViaProjectSession_MigratesVmxUnitMasterToAppUnitMaster()
    {
        string legacyDbPath = Path.Combine(_tempDir, "LegacyOldProject.dintel");
        using (var ds = new DataServiceDIntel(legacyDbPath))
        {
            ds.ExecuteNonQuery(@"
                CREATE TABLE VMX_UNIT_MASTER (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    UNIT_NAME TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    CATEGORY TEXT NOT NULL,
                    DESCRIPTION TEXT,
                    IS_DEFAULT INTEGER DEFAULT 0,
                    CREATED_BY TEXT,
                    CREATED_DATE TEXT,
                    MODIFIED_BY TEXT,
                    MODIFIED_DATE TEXT
                );
                INSERT INTO VMX_UNIT_MASTER (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT)
                VALUES ('legacy_psi', 'Pressure', 'Legacy PSI', 1);
            ");
        }

        var session = new DrillIntel.Projects.ProjectSession();
        try
        {
            session.Load(legacyDbPath);

            var projectDs = session.GetDataService();

            // Legacy unit should be migrated into the Base database (single source of truth)
            var migratedUnit = Unit.GetUnit(_appDataService, "legacy_psi", Unit.TableName);
            Assert.NotNull(migratedUnit);
            Assert.Equal("legacy_psi", migratedUnit.UnitName);
            Assert.Equal("Pressure", migratedUnit.Category);

            // Project-level database should NO LONGER have APP_UNIT_MASTER or VMX_UNIT_MASTER
            var projectTables = projectDs.GetTable("SELECT name FROM sqlite_master WHERE name IN ('APP_UNIT_MASTER', 'VMX_UNIT_MASTER');");
            Assert.Empty(projectTables.Rows);
            var vmxType = projectDs.GetValue("SELECT type FROM sqlite_master WHERE name='VMX_UNIT_MASTER';");
            Assert.Null(vmxType);
        }
        finally
        {
            session.Close();
        }
    }

    #endregion
}


