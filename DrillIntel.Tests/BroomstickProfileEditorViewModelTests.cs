using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickProfileEditorViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appDbPath;
    private readonly string _projectDbPath;
    private readonly IDataServiceDIntel _appDataService;
    private readonly IDataServiceDIntel _projectDataService;

    public BroomstickProfileEditorViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_bs_vm_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appDbPath = Path.Combine(_tempDir, "TestBroomstickApp.sqlite");
        _projectDbPath = Path.Combine(_tempDir, "TestBroomstickProj.sqlite");

        _appDataService = new DataServiceDIntel(_appDbPath);
        _appDataService.OpenConnection(_appDbPath);

        _projectDataService = new DataServiceDIntel(_projectDbPath);
        _projectDataService.OpenConnection(_projectDbPath);

        // Ensure Broomstick profile table exists in both and populate App DB with master profile
        BroomstickProfile.EnsureTableExists(_appDataService);
        BroomstickProfile.EnsureTableExists(_projectDataService);

        var initial = BroomstickProfile.CreateDefault();
        initial.Name = "Master App Profile";
        initial.PkupPumpCutOff = 175.0;
        initial.SlkPumpCutOff = 135.0;
        BroomstickProfile.SaveProfile(_appDataService, initial, "TestUser", out _);

        // Initialize common rig state setup with all 28 default rig state items
        var defSetup = rigState.CreateDefault();
        rigState.SaveCommonRigStateSetup(_appDataService, defSetup);
        rigState.SaveCommonRigStateSetup(_projectDataService, defSetup);
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
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void LoadProfile_LoadsInitialSingleEntry()
    {
        var vm = new BroomstickProfileEditorViewModel(_appDataService, "Application Master Template", "TestUser");

        Assert.Equal("Master App Profile", vm.ProfileName);
        Assert.Equal(175.0, vm.PkupPumpCutOff);
        Assert.Equal(135.0, vm.SlkPumpCutOff);
        Assert.NotEmpty(vm.PkupRigStates);
    }

    [Fact]
    public void SaveCommand_UpdatesProfileProperties_AndPersistsToDatabase()
    {
        var vm = new BroomstickProfileEditorViewModel(_projectDataService, "Project Well-1", "TestUser", isProjectContext: true);

        vm.ProfileName = "Custom Project Profile";
        vm.Notes = "Project specific calibrations";
        vm.PkupPumpCutOff = 120.0;
        vm.SlkPumpCutOff = 110.0;
        vm.RotMinRPM = 18.0;
        vm.RotMaxRPM = 55.0;

        vm.SaveCommand.Execute(null);

        Assert.False(vm.IsStatusError);

        // Reload fresh from project database
        var loaded = BroomstickProfile.LoadSingleProfile(_projectDataService, out _);
        Assert.NotNull(loaded);
        Assert.Equal("Custom Project Profile", loaded.Name);
        Assert.Equal("Project specific calibrations", loaded.Notes);
        Assert.Equal(120.0, loaded.PkupPumpCutOff);
        Assert.Equal(110.0, loaded.SlkPumpCutOff);
        Assert.Equal(18.0, loaded.RotMinRPM);
        Assert.Equal(55.0, loaded.RotMaxRPM);
    }

    [Fact]
    public void CopyMasterProfilesToProject_CopiesSingleProfile_FromAppToProject()
    {
        // Copy master profile from App DB to Project DB
        bool copied = BroomstickProfile.CopyMasterProfilesToProject(_appDataService, _projectDataService, out string err);
        Assert.True(copied, err);

        var projectProfile = BroomstickProfile.LoadSingleProfile(_projectDataService, out _);
        Assert.NotNull(projectProfile);
        Assert.Equal("Master App Profile", projectProfile.Name);
        Assert.Equal(175.0, projectProfile.PkupPumpCutOff);
        Assert.Equal(135.0, projectProfile.SlkPumpCutOff);

        // Verify only 1 entry exists in the project table
        var dt = _projectDataService.GetTable("SELECT COUNT(*) FROM APP_BS_GLOBAL_PROFILE");
        Assert.Equal(1, Convert.ToInt32(dt.Rows[0][0]));
    }

    [Fact]
    public void SelectAllAndClearAllRigStates_UpdatesCollectionCorrectly()
    {
        var vm = new BroomstickProfileEditorViewModel(_appDataService, "Test Context", "TestUser");

        Assert.Equal(28, vm.PkupRigStates.Count);

        vm.SelectAllRigStatesCommand.Execute("PU");
        Assert.All(vm.PkupRigStates, r => Assert.True(r.IsSelected));

        vm.ClearAllRigStatesCommand.Execute("PU");
        Assert.All(vm.PkupRigStates, r => Assert.False(r.IsSelected));
    }

    [Fact]
    public void SaveProfile_WhenTableDoesNotExistYet_CreatesTableAndSavesSuccessfully()
    {
        string emptyDbPath = Path.Combine(_tempDir, $"EmptyDb_{Guid.NewGuid():N}.dintel");
        using var emptyDb = new DataServiceDIntel(emptyDbPath);
        emptyDb.OpenConnection(emptyDbPath);

        // Do not create APP_BS_GLOBAL_PROFILE table in emptyDb prior to opening
        var vm = new BroomstickProfileEditorViewModel(emptyDb, "Empty Project", "TestUser", isProjectContext: true);
        vm.ProfileName = "First Save In New DB";
        vm.PkupPumpCutOff = 210.0;

        vm.SaveCommand.Execute(null);

        Assert.False(vm.IsStatusError);
        Assert.Contains("saved successfully", vm.StatusMessage);

        var loaded = BroomstickProfile.LoadSingleProfile(emptyDb, out string err);
        Assert.NotNull(loaded);
        Assert.Equal("First Save In New DB", loaded.Name);
        Assert.Equal(210.0, loaded.PkupPumpCutOff);

        emptyDb.CloseConnection();
    }
}
