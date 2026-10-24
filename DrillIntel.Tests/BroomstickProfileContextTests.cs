using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickProfileContextTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appDbPath;
    private readonly string _projectDbPath;

    public BroomstickProfileContextTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_bs_context_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appDbPath = Path.Combine(_tempDir, "DrillIntelApp.sqlite");
        _projectDbPath = Path.Combine(_tempDir, "Project.sqlite");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void TargetTableName_DeterminedCorrectly_ForGlobalAndProjectDatabases()
    {
        using var appDs = new DataServiceDIntel(_appDbPath);
        using var projDs = new DataServiceDIntel(_projectDbPath);

        // App DB resolves to APP_BS_GLOBAL_PROFILE
        Assert.True(BroomstickProfile.IsGlobalDatabase(appDs));
        Assert.Equal("APP_BS_GLOBAL_PROFILE", BroomstickProfile.GetTargetTableName(appDs));

        // Project DB resolves to VMX_BS_GLOBAL_PROFILE
        Assert.False(BroomstickProfile.IsGlobalDatabase(projDs));
        Assert.Equal("VMX_BS_GLOBAL_PROFILE", BroomstickProfile.GetTargetTableName(projDs));
    }

    [Fact]
    public void GlobalDatabase_UsesAppBsGlobalProfileTable_Unchanged()
    {
        using var appDs = new DataServiceDIntel(_appDbPath);
        BroomstickProfile.EnsureTableExists(appDs);

        // In DrillIntelApp.sqlite, APP_BS_GLOBAL_PROFILE is the physical table
        Assert.True(appDs.TableExists("APP_BS_GLOBAL_PROFILE"));

        var profile = BroomstickProfile.CreateDefault("Global Master Profile");
        profile.DepthInterval = 125.0;
        profile.PkupPumpCutOff = 95.0;

        bool saved = BroomstickProfile.SaveProfile(appDs, profile, "AdminUser", out string saveErr);
        Assert.True(saved, saveErr);

        // Verify data was written to APP_BS_GLOBAL_PROFILE
        var dt = appDs.GetTable("SELECT * FROM APP_BS_GLOBAL_PROFILE WHERE ID = @ID",
            new System.Collections.Generic.Dictionary<string, object?> { ["@ID"] = profile.ID });
        Assert.Single(dt.Rows);
        Assert.Equal("Global Master Profile", dt.Rows[0]["NAME"].ToString());

        // Load profile loads from APP_BS_GLOBAL_PROFILE
        var loaded = BroomstickProfile.LoadProfile(appDs, profile.ID, out string loadErr);
        Assert.NotNull(loaded);
        Assert.Equal("Global Master Profile", loaded.Name);
        Assert.Equal(125.0, loaded.DepthInterval);
    }

    [Fact]
    public void ProjectDatabase_UsesVmxBsGlobalProfileTable_ForFullCrudOperations()
    {
        using var projDs = new DataServiceDIntel(_projectDbPath);
        BroomstickProfile.EnsureTableExists(projDs);

        // In Project database, VMX_BS_GLOBAL_PROFILE is the physical table
        Assert.True(projDs.TableExists("VMX_BS_GLOBAL_PROFILE"));

        // 1. Insert
        var profile1 = BroomstickProfile.CreateDefault("Project Well Profile 1");
        profile1.DepthInterval = 200.0;
        profile1.PkupPumpCutOff = 80.0;
        bool inserted = BroomstickProfile.InsertProfile(projDs, profile1, "FieldEngineer", out string insertErr);
        Assert.True(inserted, insertErr);

        // Verify inserted into VMX_BS_GLOBAL_PROFILE
        var dtInsert = projDs.GetTable("SELECT * FROM VMX_BS_GLOBAL_PROFILE WHERE ID = @ID",
            new System.Collections.Generic.Dictionary<string, object?> { ["@ID"] = profile1.ID });
        Assert.Single(dtInsert.Rows);
        Assert.Equal("Project Well Profile 1", dtInsert.Rows[0]["NAME"].ToString());

        // 2. Retrieve / Load
        var loaded = BroomstickProfile.LoadProfile(projDs, profile1.ID, out string loadErr);
        Assert.NotNull(loaded);
        Assert.Equal("Project Well Profile 1", loaded.Name);
        Assert.Equal(200.0, loaded.DepthInterval);

        // 3. Update
        loaded.Name = "Project Well Profile 1 Updated";
        loaded.DepthInterval = 300.0;
        bool updated = BroomstickProfile.UpdateProfile(projDs, loaded, "FieldEngineer", out string updateErr);
        Assert.True(updated, updateErr);

        var dtUpdate = projDs.GetTable("SELECT NAME, DEPTH_INTERVAL FROM VMX_BS_GLOBAL_PROFILE WHERE ID = @ID",
            new System.Collections.Generic.Dictionary<string, object?> { ["@ID"] = profile1.ID });
        Assert.Equal("Project Well Profile 1 Updated", dtUpdate.Rows[0]["NAME"].ToString());
        Assert.Equal(300.0, Convert.ToDouble(dtUpdate.Rows[0]["DEPTH_INTERVAL"]));

        // 4. Save (single entry save/replace)
        var profile2 = BroomstickProfile.CreateDefault("Project Saved Profile");
        profile2.DepthInterval = 450.0;
        bool saved = BroomstickProfile.SaveProfile(projDs, profile2, "FieldEngineer", out string saveErr);
        Assert.True(saved, saveErr);

        var singleLoaded = BroomstickProfile.LoadSingleProfile(projDs, out string singleErr);
        Assert.NotNull(singleLoaded);
        Assert.Equal("Project Saved Profile", singleLoaded.Name);
        Assert.Equal(450.0, singleLoaded.DepthInterval);

        // Verify only 1 entry in VMX_BS_GLOBAL_PROFILE
        var dtCount = projDs.GetTable("SELECT COUNT(*) FROM VMX_BS_GLOBAL_PROFILE;");
        Assert.Equal(1, Convert.ToInt32(dtCount.Rows[0][0]));

        // 5. Delete
        bool deleted = BroomstickProfile.DeleteProfile(projDs, profile2.ID, out string delErr);
        Assert.True(deleted, delErr);

        var dtAfterDelete = projDs.GetTable("SELECT COUNT(*) FROM VMX_BS_GLOBAL_PROFILE;");
        Assert.Equal(0, Convert.ToInt32(dtAfterDelete.Rows[0][0]));
    }

    [Fact]
    public void CopyMasterProfilesToProject_CopiesRecordsFromAppDbToProjectVmxTable()
    {
        using var appDs = new DataServiceDIntel(_appDbPath);
        using var projDs = new DataServiceDIntel(_projectDbPath);

        // Populate master database in APP_BS_GLOBAL_PROFILE
        var masterProfile = BroomstickProfile.CreateDefault("Master Template Profile");
        masterProfile.DepthInterval = 175.0;
        masterProfile.PkupPumpCutOff = 88.0;
        masterProfile.SlkPumpCutOff = 77.0;
        bool savedMaster = BroomstickProfile.SaveProfile(appDs, masterProfile, "SYSTEM", out string appSaveErr);
        Assert.True(savedMaster, appSaveErr);

        // Copy to project database
        bool copied = BroomstickProfile.CopyMasterProfilesToProject(appDs, projDs, out string copyErr);
        Assert.True(copied, copyErr);

        // Verify Project table VMX_BS_GLOBAL_PROFILE has the copied record
        var projectProfiles = BroomstickProfile.LoadAllProfiles(projDs, out string projLoadErr);
        Assert.Empty(projLoadErr);
        Assert.Single(projectProfiles);

        var copiedProfile = projectProfiles[0];
        Assert.Equal(masterProfile.ID, copiedProfile.ID);
        Assert.Equal("Master Template Profile", copiedProfile.Name);
        Assert.Equal(175.0, copiedProfile.DepthInterval);
        Assert.Equal(88.0, copiedProfile.PkupPumpCutOff);
        Assert.Equal(77.0, copiedProfile.SlkPumpCutOff);

        // Verify directly queried from VMX_BS_GLOBAL_PROFILE
        var dt = projDs.GetTable("SELECT * FROM VMX_BS_GLOBAL_PROFILE;");
        Assert.Single(dt.Rows);
        Assert.Equal("Master Template Profile", dt.Rows[0]["NAME"].ToString());
    }

    [Fact]
    public void SchemaInitializer_CreatesVmxBsGlobalProfileTable_InProjectDatabase()
    {
        string projectFilePath = Path.Combine(_tempDir, "NewWellProject.dintel");
        SchemaInitializer.CreateDatabase(projectFilePath);

        using var ds = new DataServiceDIntel(projectFilePath);

        // Physical table must be VMX_BS_GLOBAL_PROFILE
        Assert.True(ds.TableExists("VMX_BS_GLOBAL_PROFILE"));

        // Compatibility view APP_BS_GLOBAL_PROFILE also exists
        var dt = ds.GetTable("SELECT COUNT(*) FROM VMX_BS_GLOBAL_PROFILE;");
        Assert.NotNull(dt);
        Assert.Equal(0, Convert.ToInt32(dt.Rows[0][0]));
    }
}

