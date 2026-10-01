using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;
using Xunit;

namespace DrillIntel.Tests;

[Collection("AppDatabaseCollection")]
public class AppDatabaseServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _appDbPath;
    private readonly string _templatePath;
    private readonly string _projectDbPath;

    public AppDatabaseServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _appDbPath = Path.Combine(_tempDir, "ProgramData", "DrillIntelApp.sqlite");
        _templatePath = Path.Combine(_tempDir, "OutputData", "DrillIntelApp.sqlite");
        _projectDbPath = Path.Combine(_tempDir, "TestProject.dintel");

        // Copy real seed file from repository into test output template location
        var solutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
        var repoSeedPath = Path.Combine(solutionDir, "DrillIntel", "Data", "DrillIntelApp.sqlite");

        Directory.CreateDirectory(Path.GetDirectoryName(_templatePath)!);
        File.Copy(repoSeedPath, _templatePath);
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
        catch { }
    }

    [Fact]
    public void Initialize_CopiesTemplateToTarget_WhenTargetDoesNotExist()
    {
        Assert.False(File.Exists(_appDbPath));

        using (var appDbService = new AppDatabaseService(_appDbPath, _templatePath))
        {
            appDbService.Initialize();

            Assert.True(appDbService.IsInitialized);
            Assert.True(File.Exists(_appDbPath));

            var ds = appDbService.GetDataService();
            Assert.True(ds.IsConnectionOpen());

            // Verify seed data in target
            var loaded = RigStateService.LoadCommonRigStateSetup(ds);
            Assert.NotNull(loaded);
            Assert.Equal(28, loaded.rigStates.Count);
            Assert.Equal("Unknown", loaded.UnknownName);
            Assert.Equal(90.0, loaded.HookloadCutOff);
        }
    }

    [Fact]
    public void Initialize_ThrowsFileNotFoundException_WhenTemplateIsMissing()
    {
        var nonExistentTemplate = Path.Combine(_tempDir, "MissingTemplate.sqlite");
        var targetDb = Path.Combine(_tempDir, "TargetDb.sqlite");

        using (var appDbService = new AppDatabaseService(targetDb, nonExistentTemplate))
        {
            var ex = Assert.Throws<FileNotFoundException>(() => appDbService.Initialize());
            Assert.Contains("master application database template was not found", ex.Message);
        }
    }

    [Fact]
    public void LoadAndSaveMasterSetup_OnAppDatabase_PreservesModifications()
    {
        using (var appDbService = new AppDatabaseService(_appDbPath, _templatePath))
        {
            appDbService.Initialize();
            var ds = appDbService.GetDataService();

            var setup = RigStateService.LoadCommonRigStateSetup(ds);
            Assert.NotNull(setup);

            // Modify values
            setup.HookloadCutOff = 125.5;
            setup.RPMCutOff = 15.0;
            setup.rigStates[0].Name = "Custom Rotary Drill";

            bool saved = RigStateService.SaveCommonRigStateSetup(ds, setup);
            Assert.True(saved);

            // Re-load to verify persistence
            var reloaded = RigStateService.LoadCommonRigStateSetup(ds);
            Assert.NotNull(reloaded);
            Assert.Equal(125.5, reloaded.HookloadCutOff);
            Assert.Equal(15.0, reloaded.RPMCutOff);
            Assert.Equal("Custom Rotary Drill", reloaded.rigStates[0].Name);
        }
    }

    [Fact]
    public void ProjectCreation_CopiesMasterRigStateSetup_IntoNewProjectDatabase()
    {
        using (var appDbService = new AppDatabaseService(_appDbPath, _templatePath))
        {
            appDbService.Initialize();
            var appDs = appDbService.GetDataService();

            // Set a distinct master threshold
            var masterSetup = RigStateService.LoadCommonRigStateSetup(appDs)!;
            masterSetup.HookloadCutOff = 188.8;
            masterSetup.rigStates[1].Name = "Custom Slide Drill Master";
            RigStateService.SaveCommonRigStateSetup(appDs, masterSetup);

            // Now create a project database and copy master setup
            SchemaInitializer.CreateDatabase(_projectDbPath);

            using (var projectSession = new ProjectSession())
            {
                projectSession.Load(_projectDbPath);
                var projDs = projectSession.GetDataService();

                // Call copy routine as ProjectService does
                var loadedMaster = RigStateService.LoadCommonRigStateSetup(appDs)!;
                bool savedToProject = RigStateService.SaveCommonRigStateSetup(projDs, loadedMaster);
                Assert.True(savedToProject);

                // Verify project has the copied values
                var projectSetup = RigStateService.LoadCommonRigStateSetup(projDs);
                Assert.NotNull(projectSetup);
                Assert.Equal(188.8, projectSetup.HookloadCutOff);
                Assert.Equal("Custom Slide Drill Master", projectSetup.rigStates[1].Name);
                Assert.Equal(28, projectSetup.rigStates.Count);

                // Now modify the project setup — it should NOT alter the App Database
                projectSetup.HookloadCutOff = 77.7;
                RigStateService.SaveCommonRigStateSetup(projDs, projectSetup);

                // Re-verify isolation
                var reloadedMaster = RigStateService.LoadCommonRigStateSetup(appDs)!;
                Assert.Equal(188.8, reloadedMaster.HookloadCutOff); // App DB remains unchanged

                var reloadedProject = RigStateService.LoadCommonRigStateSetup(projDs)!;
                Assert.Equal(77.7, reloadedProject.HookloadCutOff); // Project DB has isolated update
            }
        }
    }

    [Fact]
    public void GetChannelMappings_LoadsAllMappingsFromAppChannelMapping()
    {
        using var appDbService = new AppDatabaseService(_appDbPath, _templatePath);
        appDbService.Initialize();

        var mappings = appDbService.GetChannelMappings();
        Assert.NotEmpty(mappings);
        Assert.True(mappings.Count >= 14);

        // Verify key mnemonics are present
        var depthMapping = mappings.FirstOrDefault(m => m.Mnemonic == "DEPTH");
        Assert.NotNull(depthMapping);
        Assert.Equal("Depth", depthMapping.StandardChannel);
        Assert.Equal("ft", depthMapping.DefaultUnit);

        var hkldMapping = mappings.FirstOrDefault(m => m.Mnemonic == "HKLD");
        Assert.NotNull(hkldMapping);
        Assert.Equal("Hookload", hkldMapping.StandardChannel);
        Assert.Equal("klb", hkldMapping.DefaultUnit);

        var rpmMapping = mappings.FirstOrDefault(m => m.Mnemonic == "RPM");
        Assert.NotNull(rpmMapping);
        Assert.Equal("RPM", rpmMapping.StandardChannel);

        var sppaMapping = mappings.FirstOrDefault(m => m.Mnemonic == "SPPA");
        Assert.NotNull(sppaMapping);
        Assert.Equal("Pump Pressure", sppaMapping.StandardChannel);
    }

    [Fact]
    public async Task WellDataRepository_GetChannelMappingsAsync_ReadsFromAppDatabase()
    {
        using var appDbService = new AppDatabaseService(_appDbPath, _templatePath);
        appDbService.Initialize();

        SchemaInitializer.CreateDatabase(_projectDbPath);
        using var projectSession = new ProjectSession();
        projectSession.Load(_projectDbPath);

        var repo = new WellDataRepository(projectSession, appDbService);

        var mappings = await repo.GetChannelMappingsAsync();
        Assert.NotEmpty(mappings);
        Assert.Contains(mappings, m => m.Mnemonic == "DEPTH" && m.StandardChannel == "Depth");
        Assert.Contains(mappings, m => m.Mnemonic == "HKLD" && m.StandardChannel == "Hookload");

        // Test backward-compatible GetCurveDictionariesAsync as well
        var curveDicts = await repo.GetCurveDictionariesAsync();
        Assert.NotEmpty(curveDicts);
        Assert.Contains(curveDicts, d => d.Mnemonic == "DEPTH" && d.StandardChannel == "Depth");
    }
}

